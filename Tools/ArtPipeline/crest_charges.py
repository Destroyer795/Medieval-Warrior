"""Draw the five charges shared by the heater shield and the HUD.

Run python crest_charges.py path/to/Assets/Resources/UI to regenerate the masks.
Coordinates are normalized to the painted field, which narrows toward the tip.
"""

from pathlib import Path
import sys

from PIL import Image, ImageDraw


SIZE = 256
SUPERSAMPLE = 4
RESOLUTION = SIZE * SUPERSAMPLE


def points(vertices):
    return [(round(x * RESOLUTION), round(y * RESOLUTION)) for x, y in vertices]


def curve(start, sections):
    """Sample cubic sections into a smooth silhouette for the supersampled mask."""
    result = [start]
    for a, b, end in sections:
        for i in range(1, 13):
            t = i / 12
            u = 1 - t
            result.append(tuple(u ** 3 * s + 3 * u ** 2 * t * x + 3 * u * t ** 2 * y + t ** 3 * e
                                for s, x, y, e in zip(start, a, b, end)))
        start = end
    return points(result)


def cross(draw):
    # Flared terminals soften the hard edge of a plain geometric cross.
    draw.polygon(points([
        (.436, .195), (.564, .195), (.542, .284), (.542, .365),
        (.632, .365), (.746, .332), (.746, .474), (.632, .445),
        (.542, .445), (.542, .632), (.565, .755), (.435, .755),
        (.458, .632), (.458, .445), (.368, .445), (.254, .474),
        (.254, .332), (.368, .365), (.458, .365), (.458, .284),
    ]), fill=255)


def chevron(draw):
    draw.polygon(points([
        (.181, .595), (.500, .306), (.819, .595), (.751, .687),
        (.500, .467), (.249, .687),
    ]), fill=255)


def saltire(draw):
    draw.polygon(points([
        (.233, .230), (.321, .181), (.412, .312), (.500, .413),
        (.588, .312), (.679, .181), (.767, .230), (.624, .388),
        (.566, .475), (.622, .562), (.738, .693), (.661, .740),
        (.573, .613), (.500, .539), (.427, .613), (.339, .740),
        (.262, .693), (.378, .562), (.434, .475), (.376, .388),
    ]), fill=255)


def raven(draw):
    # Folded wing, a heavy bill and a staggered tail give the bird its profile.
    draw.polygon(curve((.778, .342), [
        ((.752, .316), (.718, .303), (.679, .302)),
        ((.675, .260), (.628, .229), (.583, .241)),
        ((.539, .247), (.524, .278), (.513, .322)),
        ((.500, .352), (.476, .364), (.441, .384)),
        ((.387, .408), (.340, .457), (.310, .514)),
        ((.278, .556), (.237, .600), (.183, .646)),
        ((.220, .650), (.252, .642), (.291, .631)),
        ((.265, .657), (.242, .680), (.223, .695)),
        ((.275, .693), (.330, .672), (.386, .635)),
        ((.431, .633), (.465, .621), (.502, .602)),
        ((.509, .628), (.519, .648), (.528, .661)),
        ((.514, .665), (.503, .677), (.503, .694)),
        ((.531, .682), (.562, .679), (.584, .688)),
        ((.584, .673), (.567, .663), (.552, .659)),
        ((.546, .628), (.543, .606), (.548, .588)),
        ((.622, .563), (.662, .514), (.675, .446)),
        ((.677, .413), (.662, .383), (.661, .361)),
        ((.695, .348), (.740, .342), (.778, .342)),
    ]), fill=255)
    draw.polygon(curve((.556, .411), [
        ((.480, .397), (.406, .457), (.379, .515)),
        ((.361, .552), (.331, .582), (.305, .605)),
        ((.355, .587), (.403, .553), (.428, .513)),
        ((.462, .460), (.502, .427), (.556, .411)),
    ]), fill=0)
    draw.ellipse(points([(.617, .288), (.654, .318)]), fill=0)


def oak_leaf(draw):
    # Rounded oak lobes with open gaps between the leaves and a carved midrib.
    draw.polygon(curve((.520, .204), [
        ((.536, .221), (.572, .234), (.566, .269)),
        ((.549, .309), (.590, .319), (.602, .283)),
        ((.621, .231), (.670, .302), (.654, .331)),
        ((.638, .367), (.664, .376), (.684, .356)),
        ((.719, .315), (.752, .395), (.720, .430)),
        ((.694, .460), (.706, .484), (.735, .459)),
        ((.772, .433), (.785, .536), (.736, .555)),
        ((.709, .565), (.676, .561), (.677, .589)),
        ((.682, .618), (.721, .581), (.713, .625)),
        ((.702, .665), (.647, .700), (.537, .708)),
        ((.534, .739), (.548, .761), (.565, .781)),
        ((.554, .782), (.537, .788), (.529, .801)),
        ((.510, .774), (.502, .735), (.502, .709)),
        ((.430, .710), (.336, .673), (.313, .625)),
        ((.287, .563), (.343, .613), (.342, .582)),
        ((.341, .554), (.299, .561), (.267, .532)),
        ((.225, .493), (.258, .423), (.288, .457)),
        ((.312, .484), (.335, .457), (.309, .428)),
        ((.271, .385), (.315, .323), (.349, .361)),
        ((.373, .389), (.393, .366), (.371, .338)),
        ((.341, .295), (.399, .241), (.415, .287)),
        ((.427, .328), (.471, .307), (.454, .272)),
        ((.436, .236), (.491, .236), (.520, .204)),
    ]), fill=255)
    draw.polygon(curve((.517, .307), [
        ((.488, .416), (.482, .540), (.510, .667)),
        ((.524, .588), (.512, .398), (.517, .307)),
    ]), fill=0)
    for stem, tips in [
        ((.504, .475), [(.389, .393), (.615, .388)]),
        ((.502, .550), [(.351, .486), (.663, .491)]),
        ((.507, .628), [(.375, .573), (.650, .570)]),
    ]:
        for x, y in tips:
            draw.polygon(points([stem, ((stem[0] + x) / 2, (stem[1] + y) / 2 - .019), (x, y),
                                 ((stem[0] + x) / 2, (stem[1] + y) / 2 + .009)]), fill=0)


def write_charge_masks(directory):
    directory.mkdir(parents=True, exist_ok=True)
    for filename, design in [
        ("emblem_charge", cross),
        ("emblem_chevron", chevron),
        ("emblem_saltire", saltire),
        ("emblem_raven", raven),
        ("emblem_oak_leaf", oak_leaf),
    ]:
        mask = Image.new("L", (RESOLUTION, RESOLUTION))
        design(ImageDraw.Draw(mask))
        sprite = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 255))
        sprite.putalpha(mask.resize(sprite.size, Image.Resampling.LANCZOS))
        sprite.save(directory / f"{filename}.png")


if __name__ == "__main__":
    write_charge_masks(Path(sys.argv[1]))
