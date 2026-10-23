# 2K terrain layers (same grading as before, full resolution) + hand-built grass detail textures.
import sys, math, random
import numpy as np
from PIL import Image, ImageOps, ImageEnhance, ImageDraw, ImageFilter

D, O = sys.argv[1], sys.argv[2]
S = 2048

def gradmap(img, stops):
    g = ImageOps.autocontrast(ImageOps.grayscale(img), cutoff=1)
    lut = []
    for c in range(3):
        for i in range(256):
            t = i / 255.0
            for j in range(len(stops) - 1):
                t0, c0 = stops[j]; t1, c1 = stops[j + 1]
                if t0 <= t <= t1:
                    k = (t - t0) / (t1 - t0 or 1); lut.append(int(c0[c] + (c1[c] - c0[c]) * k)); break
    return Image.merge('RGB', [g.point(lut[c*256:(c+1)*256]) for c in range(3)])

def load(n, kind): return Image.open(f'{D}/{n}_{kind}.jpg').convert('RGB').resize((S, S), Image.LANCZOS)
def save(img, name): img.save(f'{O}/{name}.jpg', quality=93)

save(gradmap(load('forrest_ground_01', 'diff'), [(0, (28, 52, 16)), (0.45, (58, 104, 30)), (0.8, (92, 142, 44)), (1, (136, 172, 70))]), 'Grass_Lush_D')
save(gradmap(load('leafy_grass', 'diff'), [(0, (44, 64, 20)), (0.5, (92, 126, 40)), (1, (158, 174, 84))]), 'Grass_Meadow_D')
d = load('park_dirt', 'diff'); d = ImageEnhance.Color(d).enhance(1.25); d = Image.blend(d, Image.new('RGB', (S, S), (214, 170, 112)), 0.28)
save(ImageEnhance.Brightness(d).enhance(1.12), 'Dirt_Path_D')
c = load('cobblestone_floor_08', 'diff'); save(Image.blend(c, Image.new('RGB', (S, S), (196, 176, 150)), 0.15), 'Cobble_D')
save(ImageEnhance.Color(load('mossy_rock', 'diff')).enhance(1.15), 'Rock_Mossy_D')
for src, dst in [('forrest_ground_01', 'Grass_Lush'), ('leafy_grass', 'Grass_Meadow'), ('park_dirt', 'Dirt_Path'), ('cobblestone_floor_08', 'Cobble'), ('mossy_rock', 'Rock_Mossy')]:
    save(load(src, 'nor'), dst + '_N')

# ------------------------------------------------------------ grass detail textures (RGBA, 4x supersampled)
random.seed(21)
SS = 4

def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))

def blade(draw, x, base_y, height, width, lean, curve, c_base, c_tip, steps=14):
    # tapered, curved blade drawn as a strip of quads with a base->tip colour gradient
    pts = []
    for i in range(steps + 1):
        t = i / steps
        bend = lean * t + curve * t * t
        pts.append((x + bend, base_y - height * t, width * (1 - t) ** 0.85 * 0.5 + 0.4 * SS))
    for i in range(steps):
        (x0, y0, w0), (x1, y1, w1) = pts[i], pts[i + 1]
        col = lerp(c_base, c_tip, (i + 0.5) / steps)
        draw.polygon([(x0 - w0, y0), (x0 + w0, y0), (x1 + w1, y1), (x1 - w1, y1)], fill=col + (255,))
    return pts[-1]

def grass_texture(name, W, H, count, h_range, w_range, palette, seeds=0, seed_col=None, clover=0):
    Wp, Hp = W * SS, H * SS
    img = Image.new('RGBA', (Wp, Hp), (0, 0, 0, 0))
    dr = ImageDraw.Draw(img)
    blades = []
    for _ in range(count):
        depth = random.random()              # 0 = back, 1 = front
        x = random.uniform(0.06, 0.94) * Wp
        h = random.uniform(*h_range) * Hp * (0.75 + 0.25 * depth)
        w = random.uniform(*w_range) * SS
        lean = random.uniform(-0.18, 0.18) * h
        curve = random.uniform(-0.25, 0.25) * h
        base, tip = random.choice(palette)
        shade = 0.6 + 0.4 * depth             # blades at the back sit in the shade of the clump
        blades.append((depth, x, h, w, lean, curve, lerp((0, 0, 0), base, shade), lerp((0, 0, 0), tip, 0.75 + 0.25 * depth)))
    blades.sort()
    tips = []
    for depth, x, h, w, lean, curve, cb, ct in blades:
        tips.append(blade(dr, x, Hp, h, w, lean, curve, cb, ct))
    for i in range(min(seeds, len(tips))):
        tx, ty, _ = random.choice(tips)
        for k in range(7):                   # oat-like seed head
            sx = tx + random.uniform(-6, 6) * SS; sy = ty + k * 5 * SS
            dr.ellipse([sx - 3 * SS, sy - 6 * SS, sx + 3 * SS, sy + 6 * SS], fill=seed_col + (255,))
    for i in range(clover):                  # little trefoil leaves low in the clump
        cx = random.uniform(0.1, 0.9) * Wp; cy = Hp - random.uniform(0.04, 0.3) * Hp
        r = random.uniform(9, 14) * SS
        col = random.choice([(70, 120, 34), (84, 140, 40), (60, 108, 30)])
        for a in (0, 2.1, 4.2):
            dr.ellipse([cx + math.cos(a) * r * 0.8 - r, cy + math.sin(a) * r * 0.8 - r, cx + math.cos(a) * r * 0.8 + r, cy + math.sin(a) * r * 0.8 + r], fill=col + (255,))
    small = img.resize((W, H), Image.LANCZOS)
    # premultiply-friendly edge: bleed colour into transparent pixels so mipmaps don't halo
    arr = np.asarray(small).astype(np.float32)
    rgb, a = arr[..., :3], arr[..., 3:] / 255
    blurred = np.asarray(Image.fromarray((rgb * a).astype(np.uint8)).filter(ImageFilter.GaussianBlur(4))).astype(np.float32)
    wsum = np.asarray(Image.fromarray((a[..., 0] * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(4))).astype(np.float32)[..., None] / 255
    bleed = blurred / np.maximum(wsum, 1e-3)
    out = np.where(a > 0.02, rgb, bleed)
    Image.fromarray(np.dstack([np.clip(out, 0, 255), arr[..., 3]]).astype(np.uint8), 'RGBA').save(f'{O}/{name}.png')

greens = [((34, 72, 18), (120, 170, 52)), ((40, 80, 20), (140, 182, 60)), ((30, 62, 16), (104, 150, 44)), ((46, 86, 24), (156, 190, 74))]
grass_texture('GrassDense', 512, 512, 160, (0.45, 0.95), (6, 11), greens, clover=6)
grass_texture('GrassWild', 512, 512, 90, (0.6, 1.0), (5, 9), greens + [((60, 80, 24), (190, 182, 96))], seeds=10, seed_col=(206, 186, 120))
grass_texture('GrassShort', 512, 256, 140, (0.35, 0.9), (6, 10), greens, clover=14)
print('2k + grass ok')
