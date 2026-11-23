"""Low-poly modeling primitives in the Marchwarden prop's local coordinate system."""
from dataclasses import dataclass
from math import cos, pi, sin, sqrt, tau


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def sub(a, b):
    return tuple(x - y for x, y in zip(a, b))


def scale(a, k):
    return tuple(x * k for x in a)


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def unit(v):
    return scale(v, 1 / sqrt(dot(v, v)))


def mean(points):
    return tuple(sum(v) / len(points) for v in zip(*points))


@dataclass
class Part:
    name: str
    slot: int
    vertices: list
    faces: list
    smooth: float = 0  # Creases above this angle retain independent face normals.


class Model:
    def __init__(self, id_, name, finishes):
        self.id = id_
        self.name = name
        self.finishes = finishes
        self.parts = []

    def part(self, name, slot, vertices, faces, smooth=0):
        self.parts.append(Part(name, slot, vertices, faces, smooth))

    def loft(self, name, slot, rings, smooth=0, end=None):
        """Connect counterclockwise sections. A point can close the last section."""
        n = len(rings[0])
        vertices = [p for ring in rings for p in ring]
        faces = []
        for i in range(len(rings)-1):
            for j in range(n):
                k = (j + 1) % n
                faces.append((i*n+j, i*n+k, (i+1)*n+k, (i+1)*n+j))
        base = len(vertices)
        vertices.append(mean(rings[0]))
        for j in range(n):
            faces.append((base, (j+1) % n, j))
        last = (len(rings)-1)*n
        top = len(vertices)
        vertices.append(end if end is not None else mean(rings[-1]))
        for j in range(n):
            faces.append((last+j, last+(j+1) % n, top))
        self.part(name, slot, vertices, faces, smooth)

    def stroke(self, name, slot, points, width, depth=None, sides=8, smooth=40):
        """Sweep beveled ribs, cords or ribbons along a three-dimensional path."""
        widths = [width]*len(points) if isinstance(width, (float, int)) else width
        if depth is None:
            depths = widths
        else:
            depths = [depth]*len(points) if isinstance(depth, (float, int)) else depth
        rings = []
        for i, (p, w, d) in enumerate(zip(points, widths, depths)):
            tangent = unit(sub(points[min(i+1, len(points)-1)], points[max(0, i-1)]))
            ref = (0, 0, 1) if abs(tangent[2]) < .95 else (0, 1, 0)
            across = unit(cross(ref, tangent))
            up = cross(tangent, across)
            ring = [add(p, add(scale(across, w*cos(tau*j/sides)), scale(up, d*sin(tau*j/sides))))
                    for j in range(sides)]
            rings.append(ring)
        self.loft(name, slot, rings, smooth)

    def lathe(self, name, slot, stations, sides=12, smooth=42):
        rings = [[(x, y+ry*cos(tau*j/sides), z+rz*sin(tau*j/sides)) for j in range(sides)]
                 for x, y, z, ry, rz in stations]
        self.loft(name, slot, rings, smooth)

    def bead(self, name, slot, center, radii, sides=10, bands=6, smooth=44):
        x, y, z = center
        a, b, c = radii
        stations = [(x-a*cos(pi*i/bands), y, z, b*sin(pi*i/bands), c*sin(pi*i/bands))
                    for i in range(1, bands)]
        rings = [[(px, py+ry*cos(tau*j/sides), pz+rz*sin(tau*j/sides)) for j in range(sides)]
                 for px, py, pz, ry, rz in stations]
        self.loft(name, slot, rings, smooth, end=(x+a, y, z))
        self.parts[-1].vertices[-2] = (x-a, y, z)

    def grip(self, stations, turns=0, binding=2, handed=1):
        # All profiles include the original palm location and retain an oval section.
        self.lathe("Grip scales", 2, [(x, y, 0, w, d) for x,y,w,d in stations], 16)
        if not turns:
            return
        path = []
        for step in range(turns*14+1):
            t = step/(turns*14)
            x = stations[0][0]+.007 + (stations[-1][0]-stations[0][0]-.014)*t
            hi = next(i for i, station in enumerate(stations) if station[0] >= x)
            lo = stations[hi-1]
            upper = stations[hi]
            f = (x-lo[0])/(upper[0]-lo[0])
            cy, w, d = (lo[i]+f*(upper[i]-lo[i]) for i in (1,2,3))
            angle = handed*tau*turns*t
            path.append((x, cy+w*cos(angle), d*sin(angle)))
        self.stroke("Spiral binding", binding, path, .00145, sides=5, smooth=64)

    def collar(self, name, x, width, ry, rz, slot=1, y=0):
        self.lathe(name, slot, [(x-width/2, y, 0, ry*.91, rz*.91),
                    (x-width*.3, y, 0, ry, rz), (x+width*.3, y, 0, ry, rz),
                    (x+width/2, y, 0, ry*.91, rz*.91)], sides=14)

    def blade(self, profile, cross_section):
        # The contour is given from negative to positive Y across the display face.
        section = cross_section + [(y, -z) for y, z in reversed(cross_section[1:-1])]
        rings = []
        for x, cy, half_width, height in profile[:-1]:
            rings.append([(x, cy+half_width*y, height*z) for y,z in reversed(section)])
        self.loft("Blade bevels and fuller", 0, rings, end=(profile[-1][0], profile[-1][1], 0))

    def seal(self, name, slot, x, y, layers, sides=16, phase=0):
        """Stepped repoussé boss or gem, modeled on both faces."""
        for sign in (-1, 1):
            rings = []
            for z, rx, ry in layers:
                rings.append([(x+rx*cos(sign*tau*i/sides+phase),
                               y+ry*sin(sign*tau*i/sides+phase), sign*z) for i in range(sides)])
            self.loft(name, slot, rings)

    def guard(self, name, sections, slot=1):
        """A continuous quillon with a beveled octagonal cross section."""
        self.stroke(name, slot, [(x,y,0) for x,y,w,d in sections],
                    [w for x,y,w,d in sections], [d for x,y,w,d in sections],
                    sides=8, smooth=26)


def mirror_guard(half):
    return [(x,-y,w,d) for x,y,w,d in reversed(half[1:])] + half


def arc_points(p0, p1, p2, p3, steps=10):
    """Sample an ornamental cubic curve including both terminals."""
    points = []
    for i in range(steps+1):
        t = i/steps
        weights = ((1-t)**3, 3*(1-t)**2*t, 3*(1-t)*t*t, t**3)
        points.append(tuple(sum(w*v for w,v in zip(weights, axis)) for axis in zip(p0,p1,p2,p3)))
    return points
