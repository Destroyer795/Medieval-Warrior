# Procedural medieval UI sprite set: dark iron/leather + gold trim + parchment + blood red.
import sys, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT = sys.argv[1]
SS = 4  # supersampling
random.seed(3); np.random.seed(3)

GOLD_D = (122, 86, 32); GOLD = (201, 155, 62); GOLD_L = (247, 214, 128)
IRON_D = (24, 21, 20); IRON = (46, 40, 37); LEATHER = (58, 34, 26)
BLOOD = (150, 22, 24)

def noise(w, h, scale=8, amp=1.0, seed=0):
    rng = np.random.RandomState(seed)
    small = rng.rand(max(2, h // scale), max(2, w // scale)).astype(np.float32)
    img = Image.fromarray((small * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    n = np.asarray(img).astype(np.float32) / 255.0
    fine = rng.rand(h, w).astype(np.float32)
    return (n * 0.75 + fine * 0.25) * amp

def textured(w, h, base, var=18, scale=10, seed=0):
    n = noise(w, h, scale, 1.0, seed) - 0.5
    arr = np.zeros((h, w, 4), np.float32)
    for c in range(3): arr[..., c] = base[c] + n * var * 2
    arr[..., 3] = 255
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGBA')

def vgrad(w, h, top, bottom):
    t = np.linspace(0, 1, h, dtype=np.float32)[:, None]
    arr = np.zeros((h, w, 4), np.float32)
    for c in range(3): arr[..., c] = top[c] * (1 - t) + bottom[c] * t
    arr[..., 3] = 255
    return Image.fromarray(arr.astype(np.uint8), 'RGBA')

def down(img, w, h): return img.resize((w, h), Image.LANCZOS)

def rounded_mask(w, h, r):
    m = Image.new('L', (w, h), 0); ImageDraw.Draw(m).rounded_rectangle([0, 0, w - 1, h - 1], r, fill=255); return m

def gold_frame(d, box, width, r):
    # bevelled gold: dark outer, bright mid, dark inner line
    x0, y0, x1, y1 = box
    d.rounded_rectangle(box, r, outline=GOLD_D + (255,), width=width)
    w2 = max(1, width * 2 // 3)
    d.rounded_rectangle([x0 + width // 6, y0 + width // 6, x1 - width // 6, y1 - width // 6], r, outline=GOLD + (255,), width=w2)
    d.rounded_rectangle([x0 + width // 4, y0 + width // 4, x1 - width // 2, y1 - width // 2], r, outline=GOLD_L + (200,), width=max(1, width // 5))
    d.rounded_rectangle([x0 + width, y0 + width, x1 - width, y1 - width], max(0, r - width), outline=(10, 8, 6, 255), width=max(1, width // 4))

def diamond(d, cx, cy, s, fill=GOLD, outline=GOLD_D):
    d.polygon([(cx, cy - s), (cx + s, cy), (cx, cy + s), (cx - s, cy)], fill=fill + (255,), outline=outline + (255,))
    d.polygon([(cx, cy - s * 0.5), (cx + s * 0.5, cy), (cx, cy + s * 0.5), (cx - s * 0.5, cy)], fill=GOLD_L + (255,))

def rivet(d, cx, cy, r):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=GOLD_D + (255,))
    d.ellipse([cx - r * 0.75, cy - r * 0.8, cx + r * 0.6, cy + r * 0.6], fill=GOLD + (255,))
    d.ellipse([cx - r * 0.45, cy - r * 0.55, cx - r * 0.05, cy - r * 0.15], fill=GOLD_L + (255,))

def corner_filigree(d, W, H, inset, s):
    for (cx, cy, sx, sy) in [(inset, inset, 1, 1), (W - inset, inset, -1, 1), (inset, H - inset, 1, -1), (W - inset, H - inset, -1, -1)]:
        diamond(d, cx, cy, s)
        d.arc([cx - s * 2.2 if sx > 0 else cx, cy - s * 2.2 if sy > 0 else cy,
               cx if sx > 0 else cx + s * 2.2, cy if sy > 0 else cy + s * 2.2], 0, 360, fill=GOLD + (255,), width=max(1, s // 4))
        d.line([cx + sx * s, cy, cx + sx * s * 3.2, cy], fill=GOLD + (255,), width=max(1, s // 3))
        d.line([cx, cy + sy * s, cx, cy + sy * s * 3.2], fill=GOLD + (255,), width=max(1, s // 3))

def inner_shadow(img, strength=120, blur=18):
    w, h = img.size
    a = np.asarray(img.split()[3]).astype(np.float32) / 255
    m = Image.new('L', (w, h), 0); ImageDraw.Draw(m).rectangle([blur, blur, w - blur, h - blur], fill=255)
    m = m.filter(ImageFilter.GaussianBlur(blur))
    shade = 1 - (1 - np.asarray(m).astype(np.float32) / 255) * (strength / 255)
    arr = np.asarray(img).astype(np.float32)
    arr[..., :3] *= shade[..., None]
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGBA')

def save(img, name): img.save(f'{OUT}/{name}.png')

# ---------------------------------------------------------------- panels
def panel_dark(name, base=LEATHER, size=256):
    W = H = size * SS
    img = textured(W, H, base, 14, 24 * SS, seed=hash(name) % 1000)
    img = inner_shadow(img, 150, 30 * SS)
    img.putalpha(rounded_mask(W, H, 14 * SS))
    d = ImageDraw.Draw(img)
    gold_frame(d, [2 * SS, 2 * SS, W - 3 * SS, H - 3 * SS], 9 * SS, 12 * SS)
    corner_filigree(d, W, H, 22 * SS, 6 * SS)
    save(down(img, size, size), name)

panel_dark('panel_dark', LEATHER)
panel_dark('panel_iron', IRON)

def parchment(size=256):
    W = H = size * SS
    base = textured(W, H, (226, 204, 160), 22, 30 * SS, 11)
    stains = noise(W, H, 60 * SS, 1, 12)
    arr = np.asarray(base).astype(np.float32)
    arr[..., :3] *= (0.9 + 0.1 * stains)[..., None]
    img = Image.fromarray(arr.astype(np.uint8), 'RGBA')
    img = inner_shadow(img, 110, 26 * SS)
    # burnt, uneven edge
    m = Image.new('L', (W, H), 0); d = ImageDraw.Draw(m)
    pts = []
    steps = 64
    for i in range(steps):
        t = i / steps
        for side in range(4):
            pass
    d.rounded_rectangle([6 * SS, 6 * SS, W - 6 * SS, H - 6 * SS], 10 * SS, fill=255)
    m = m.filter(ImageFilter.GaussianBlur(3 * SS))
    edge = np.asarray(m).astype(np.float32) / 255
    rough = noise(W, H, 6 * SS, 1, 13)
    alpha = np.clip((edge - 0.35 + (rough - 0.5) * 0.35) * 4, 0, 1)
    burn = np.clip(1 - (edge - 0.3) * 2.2, 0, 1)
    arr = np.asarray(img).astype(np.float32)
    arr[..., 0] *= 1 - burn * 0.55; arr[..., 1] *= 1 - burn * 0.65; arr[..., 2] *= 1 - burn * 0.75
    arr[..., 3] = alpha * 255
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGBA')
    save(down(img, size, size), 'panel_parchment')

parchment()

# ---------------------------------------------------------------- buttons
def button(name, top, bottom, glow=False):
    w, h = 320, 72
    W, H = w * SS, h * SS
    img = vgrad(W, H, top, bottom)
    tex = noise(W, H, 4 * SS, 1, 21)
    arr = np.asarray(img).astype(np.float32); arr[..., :3] *= (0.88 + 0.16 * tex)[..., None]
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGBA')
    img.putalpha(rounded_mask(W, H, 10 * SS))
    d = ImageDraw.Draw(img)
    gold_frame(d, [1 * SS, 1 * SS, W - 2 * SS, H - 2 * SS], 6 * SS, 9 * SS)
    for cx in (16 * SS, W - 16 * SS): diamond(d, cx, H // 2, 5 * SS)
    if glow:
        g = Image.new('RGBA', (W, H), (0, 0, 0, 0)); gd = ImageDraw.Draw(g)
        gd.rounded_rectangle([8 * SS, 8 * SS, W - 8 * SS, H - 8 * SS], 8 * SS, outline=GOLD_L + (160,), width=4 * SS)
        g = g.filter(ImageFilter.GaussianBlur(5 * SS)); img = Image.alpha_composite(img, g)
    save(down(img, w, h), name)

button('button', (92, 34, 28), (48, 16, 14))
button('button_hover', (138, 52, 36), (72, 24, 18), glow=True)
button('button_pressed', (52, 18, 15), (80, 30, 24))
button('button_dark', (52, 46, 42), (26, 22, 20))

# ---------------------------------------------------------------- bars
def bar_frame():
    w, h = 512, 56
    W, H = w * SS, h * SS
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    cap = 34 * SS
    # pointed end caps
    for left in (True, False):
        x = 0 if left else W
        s = 1 if left else -1
        poly = [(x, H / 2), (x + s * cap * 0.55, 4 * SS), (x + s * cap, 4 * SS), (x + s * cap, H - 4 * SS), (x + s * cap * 0.55, H - 4 * SS)]
        d.polygon(poly, fill=GOLD_D + (255,))
        inner = [(x + s * 6 * SS, H / 2), (x + s * cap * 0.6, 9 * SS), (x + s * cap, 9 * SS), (x + s * cap, H - 9 * SS), (x + s * cap * 0.6, H - 9 * SS)]
        d.polygon(inner, fill=GOLD + (255,))
        diamond(d, x + s * cap * 0.55, H / 2, 7 * SS, fill=BLOOD, outline=GOLD_D)
    # rails
    d.rectangle([cap - 2 * SS, 4 * SS, W - cap + 2 * SS, 11 * SS], fill=GOLD + (255,))
    d.rectangle([cap - 2 * SS, 4 * SS, W - cap + 2 * SS, 6 * SS], fill=GOLD_L + (255,))
    d.rectangle([cap - 2 * SS, H - 11 * SS, W - cap + 2 * SS, H - 4 * SS], fill=GOLD + (255,))
    d.rectangle([cap - 2 * SS, H - 6 * SS, W - cap + 2 * SS, H - 4 * SS], fill=GOLD_D + (255,))
    d.rectangle([cap - 2 * SS, 11 * SS, W - cap + 2 * SS, 13 * SS], fill=(12, 9, 8, 255))
    d.rectangle([cap - 2 * SS, H - 13 * SS, W - cap + 2 * SS, H - 11 * SS], fill=(12, 9, 8, 255))
    for x in range(cap + 30 * SS, W - cap - 10 * SS, 60 * SS):
        rivet(d, x, 7.5 * SS, 2.6 * SS); rivet(d, x, H - 7.5 * SS, 2.6 * SS)
    save(down(img, w, h), 'bar_frame')
    # background trough
    bg = textured(64 * SS, 32 * SS, (20, 12, 12), 10, 6 * SS, 31)
    save(down(bg, 64, 32), 'bar_bg')
    # fill: white, slight top highlight + grain (tinted at runtime)
    fill = vgrad(64 * SS, 32 * SS, (255, 255, 255), (150, 150, 150))
    arr = np.asarray(fill).astype(np.float32)
    arr[: 6 * SS, :, :3] = np.minimum(255, arr[: 6 * SS, :, :3] * 1.1)
    grain = noise(64 * SS, 32 * SS, 3 * SS, 1, 32)
    arr[..., :3] *= (0.9 + 0.1 * grain)[..., None]
    save(down(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGBA'), 64, 32), 'bar_fill')

bar_frame()

# ---------------------------------------------------------------- banner / divider / slots
def banner():
    w, h = 1024, 150
    W, H = w * SS, h * SS
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    fold = 90 * SS
    # tails
    for left in (True, False):
        x = 0 if left else W; s = 1 if left else -1
        d.polygon([(x, 30 * SS), (x + s * fold * 1.4, 30 * SS), (x + s * fold * 1.4, H - 10 * SS), (x, H - 10 * SS), (x + s * fold * 0.5, (H + 20 * SS) / 2)], fill=(96, 14, 16, 255))
    body = vgrad(W - 2 * fold, H - 40 * SS, (178, 30, 30), (110, 16, 18))
    tex = noise(body.size[0], body.size[1], 5 * SS, 1, 41)
    arr = np.asarray(body).astype(np.float32); arr[..., :3] *= (0.88 + 0.18 * tex)[..., None]
    body = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGBA')
    img.paste(body, (fold, 10 * SS))
    d = ImageDraw.Draw(img)
    for y in (10 * SS, H - 30 * SS - 1):
        d.rectangle([fold, y, W - fold, y + 7 * SS], fill=GOLD + (255,))
        d.rectangle([fold, y, W - fold, y + 2 * SS], fill=GOLD_L + (255,))
    for x in (fold + 20 * SS, W - fold - 20 * SS): diamond(d, x, (H - 20 * SS) / 2, 10 * SS)
    save(down(img, w, h), 'banner')

banner()

def divider():
    w, h = 512, 32
    W, H = w * SS, h * SS
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    for i in range(W // 2):
        a = int(255 * min(1, i / (W * 0.35)))
        d.line([(W / 2 - i, H / 2 - 1.5 * SS), (W / 2 - i, H / 2 + 1.5 * SS)], fill=GOLD + (a,))
        d.line([(W / 2 + i, H / 2 - 1.5 * SS), (W / 2 + i, H / 2 + 1.5 * SS)], fill=GOLD + (a,))
    diamond(d, W / 2, H / 2, 12 * SS)
    diamond(d, W / 2 - 30 * SS, H / 2, 6 * SS); diamond(d, W / 2 + 30 * SS, H / 2, 6 * SS)
    save(down(img, w, h), 'divider')

divider()

def slot(name, active):
    s = 128; S = s * SS
    img = textured(S, S, (30, 24, 22), 12, 8 * SS, 51 + active)
    img = inner_shadow(img, 170, 20 * SS)
    img.putalpha(rounded_mask(S, S, 14 * SS))
    d = ImageDraw.Draw(img)
    gold_frame(d, [2 * SS, 2 * SS, S - 3 * SS, S - 3 * SS], 7 * SS, 12 * SS)
    if active:
        g = Image.new('RGBA', (S, S), (0, 0, 0, 0)); gd = ImageDraw.Draw(g)
        gd.rounded_rectangle([10 * SS, 10 * SS, S - 10 * SS, S - 10 * SS], 10 * SS, outline=GOLD_L + (230,), width=6 * SS)
        g = g.filter(ImageFilter.GaussianBlur(6 * SS)); img = Image.alpha_composite(img, g)
    save(down(img, s, s), name)

slot('slot', 0); slot('slot_active', 1)

def vignette():
    s = 256
    y, x = np.mgrid[0:s, 0:s].astype(np.float32)
    r = np.sqrt(((x - s / 2) / (s / 2)) ** 2 + ((y - s / 2) / (s / 2)) ** 2)
    a = np.clip((r - 0.55) / 0.6, 0, 1) ** 1.6
    arr = np.zeros((s, s, 4), np.uint8); arr[..., :3] = 255; arr[..., 3] = (a * 255).astype(np.uint8)
    save(Image.fromarray(arr, 'RGBA'), 'vignette')

vignette()

def radial_glow():
    s = 128
    y, x = np.mgrid[0:s, 0:s].astype(np.float32)
    r = np.sqrt(((x - s / 2) / (s / 2)) ** 2 + ((y - s / 2) / (s / 2)) ** 2)
    a = np.clip(1 - r, 0, 1) ** 2
    arr = np.zeros((s, s, 4), np.uint8); arr[..., :3] = 255; arr[..., 3] = (a * 255).astype(np.uint8)
    save(Image.fromarray(arr, 'RGBA'), 'glow')

radial_glow()

# ---------------------------------------------------------------- icons (white-ish with gold outline where it helps)
def icon(name, draw_fn, size=128):
    S = size * SS
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(img), S, img)
    # dark drop outline for legibility
    a = img.split()[3].filter(ImageFilter.MaxFilter(5 * SS + 1 if (5 * SS) % 2 == 0 else 5 * SS))
    shadow = Image.new('RGBA', (S, S), (8, 5, 4, 0)); shadow.putalpha(a.point(lambda v: int(v * 0.85)))
    img = Image.alpha_composite(shadow, img)
    save(down(img, size, size), 'icon_' + name)

def P(S, pts): return [(x * S, y * S) for x, y in pts]

def heart(d, S, img):
    d.ellipse(P(S, [(0.14, 0.18), (0.52, 0.56)]), fill=(196, 28, 32, 255))
    d.ellipse(P(S, [(0.48, 0.18), (0.86, 0.56)]), fill=(196, 28, 32, 255))
    d.polygon(P(S, [(0.155, 0.43), (0.5, 0.86), (0.845, 0.43), (0.5, 0.36)]), fill=(196, 28, 32, 255))
    d.ellipse(P(S, [(0.24, 0.26), (0.38, 0.38)]), fill=(255, 140, 130, 255))
icon('heart', heart)

def bolt(d, S, img):
    d.polygon(P(S, [(0.58, 0.08), (0.24, 0.55), (0.47, 0.55), (0.38, 0.92), (0.78, 0.4), (0.54, 0.4), (0.66, 0.08)]), fill=(236, 196, 74, 255))
icon('stamina', bolt)

def sword(d, S, img):
    d.polygon(P(S, [(0.47, 0.06), (0.53, 0.06), (0.56, 0.62), (0.5, 0.7), (0.44, 0.62)]), fill=(214, 220, 228, 255))
    d.line(P(S, [(0.5, 0.08), (0.5, 0.62)]), fill=(150, 160, 172, 255), width=2 * SS)
    d.rounded_rectangle(P(S, [(0.28, 0.62), (0.72, 0.69)]), 3 * SS, fill=GOLD + (255,))
    d.rectangle(P(S, [(0.46, 0.69), (0.54, 0.86)]), fill=(96, 56, 30, 255))
    d.ellipse(P(S, [(0.44, 0.85), (0.56, 0.95)]), fill=GOLD + (255,))
icon('sword', sword)

def bow(d, S, img):
    d.arc(P(S, [(0.12, 0.06), (0.78, 0.94)]), -70, 70, fill=(150, 98, 52, 255), width=7 * SS)
    d.line(P(S, [(0.56, 0.12), (0.56, 0.88)]), fill=(236, 230, 210, 255), width=2 * SS)
    d.line(P(S, [(0.18, 0.5), (0.86, 0.5)]), fill=(200, 170, 120, 255), width=3 * SS)
    d.polygon(P(S, [(0.86, 0.44), (0.97, 0.5), (0.86, 0.56)]), fill=(214, 220, 228, 255))
    d.polygon(P(S, [(0.14, 0.44), (0.24, 0.5), (0.14, 0.56), (0.09, 0.5)]), fill=(200, 40, 40, 255))
icon('bow', bow)

def arrow(d, S, img):
    d.line(P(S, [(0.15, 0.85), (0.8, 0.2)]), fill=(200, 160, 110, 255), width=4 * SS)
    d.polygon(P(S, [(0.86, 0.14), (0.7, 0.2), (0.8, 0.3)]), fill=(220, 226, 232, 255))
    d.polygon(P(S, [(0.15, 0.85), (0.1, 0.72), (0.22, 0.78)]), fill=(210, 50, 50, 255))
    d.polygon(P(S, [(0.15, 0.85), (0.28, 0.9), (0.22, 0.78)]), fill=(210, 50, 50, 255))
icon('arrow', arrow)

def skull(d, S, img):
    bone = (232, 224, 200, 255)
    d.ellipse(P(S, [(0.2, 0.12), (0.8, 0.68)]), fill=bone)
    d.rounded_rectangle(P(S, [(0.32, 0.55), (0.68, 0.86)]), 6 * SS, fill=bone)
    d.ellipse(P(S, [(0.29, 0.36), (0.46, 0.54)]), fill=(30, 10, 10, 255))
    d.ellipse(P(S, [(0.54, 0.36), (0.71, 0.54)]), fill=(30, 10, 10, 255))
    d.polygon(P(S, [(0.5, 0.56), (0.45, 0.66), (0.55, 0.66)]), fill=(30, 10, 10, 255))
    for x in (0.4, 0.5, 0.6): d.line(P(S, [(x, 0.74), (x, 0.86)]), fill=(60, 40, 30, 255), width=2 * SS)
icon('skull', skull)

def shield(d, S, img):
    d.polygon(P(S, [(0.18, 0.12), (0.82, 0.12), (0.82, 0.48), (0.5, 0.92), (0.18, 0.48)]), fill=GOLD + (255,))
    d.polygon(P(S, [(0.24, 0.18), (0.76, 0.18), (0.76, 0.47), (0.5, 0.84), (0.24, 0.47)]), fill=(255, 255, 255, 255))
icon('shield', shield)

def potion(d, S, img):
    d.ellipse(P(S, [(0.22, 0.38), (0.78, 0.92)]), fill=(200, 30, 40, 255))
    d.rectangle(P(S, [(0.42, 0.18), (0.58, 0.42)]), fill=(180, 210, 220, 255))
    d.rectangle(P(S, [(0.38, 0.1), (0.62, 0.2)]), fill=(140, 90, 50, 255))
    d.ellipse(P(S, [(0.32, 0.5), (0.44, 0.62)]), fill=(255, 150, 150, 255))
icon('potion', potion)

def crown(d, S, img):
    d.polygon(P(S, [(0.14, 0.75), (0.14, 0.3), (0.32, 0.5), (0.5, 0.22), (0.68, 0.5), (0.86, 0.3), (0.86, 0.75)]), fill=GOLD + (255,))
    d.rectangle(P(S, [(0.14, 0.72), (0.86, 0.84)]), fill=GOLD_L + (255,))
    for x in (0.32, 0.5, 0.68): d.ellipse(P(S, [(x - 0.04, 0.74), (x + 0.04, 0.82)]), fill=(190, 30, 40, 255))
icon('crown', crown)

def hourglass(d, S, img):
    d.rectangle(P(S, [(0.22, 0.08), (0.78, 0.15)]), fill=(140, 90, 50, 255))
    d.rectangle(P(S, [(0.22, 0.85), (0.78, 0.92)]), fill=(140, 90, 50, 255))
    d.polygon(P(S, [(0.28, 0.15), (0.72, 0.15), (0.54, 0.5), (0.72, 0.85), (0.28, 0.85), (0.46, 0.5)]), fill=(200, 225, 235, 255))
    d.polygon(P(S, [(0.36, 0.3), (0.64, 0.3), (0.5, 0.48)]), fill=(230, 190, 110, 255))
    d.polygon(P(S, [(0.5, 0.62), (0.68, 0.84), (0.32, 0.84)]), fill=(230, 190, 110, 255))
icon('hourglass', hourglass)

def swords(d, S, img):
    for flip in (False, True):
        tmp = Image.new('RGBA', img.size, (0, 0, 0, 0)); sword(ImageDraw.Draw(tmp), S, tmp)
        tmp = tmp.rotate(40 if flip else -40, resample=Image.BICUBIC)
        img.alpha_composite(tmp)
icon('swords', swords)

def horde(d, S, img):
    for i, (ox, sc) in enumerate([(-0.2, 0.7), (0.2, 0.7), (0, 0.85)]):
        tmp = Image.new('RGBA', img.size, (0, 0, 0, 0)); skull(ImageDraw.Draw(tmp), S, tmp)
        tmp = tmp.resize((int(S * sc), int(S * sc)), Image.LANCZOS)
        img.alpha_composite(tmp, (int(S * (0.5 + ox) - S * sc / 2), int(S * (0.95 - sc))))
icon('horde', horde)

def chevron(d, S, img):
    d.polygon(P(S, [(0.5, 0.1), (0.9, 0.7), (0.5, 0.52), (0.1, 0.7)]), fill=(230, 40, 40, 255))
icon('threat', chevron, 64)

def crosshair():
    s = 64; S = s * SS
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    c = S / 2
    for a in range(4):
        ang = a * math.pi / 2
        x0, y0 = c + math.cos(ang) * 10 * SS, c + math.sin(ang) * 10 * SS
        x1, y1 = c + math.cos(ang) * 24 * SS, c + math.sin(ang) * 24 * SS
        d.line([(x0, y0), (x1, y1)], fill=(10, 8, 6, 200), width=6 * SS)
        d.line([(x0, y0), (x1, y1)], fill=(255, 240, 210, 255), width=3 * SS)
    d.ellipse([c - 3 * SS, c - 3 * SS, c + 3 * SS, c + 3 * SS], fill=(255, 240, 210, 255), outline=(10, 8, 6, 200), width=SS)
    save(down(img, s, s), 'crosshair')
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    for sx, sy in [(1, 1), (-1, 1), (1, -1), (-1, -1)]:
        d.line([(c + sx * 8 * SS, c + sy * 8 * SS), (c + sx * 22 * SS, c + sy * 22 * SS)], fill=(255, 255, 255, 255), width=4 * SS)
    save(down(img, s, s), 'hitmarker')

crosshair()

def arrows_lr():
    s = 64; S = s * SS
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.polygon(P(S, [(0.7, 0.15), (0.25, 0.5), (0.7, 0.85)]), fill=GOLD + (255,), outline=GOLD_D + (255,))
    d.polygon(P(S, [(0.62, 0.3), (0.38, 0.5), (0.62, 0.7)]), fill=GOLD_L + (255,))
    save(down(img, s, s), 'arrow_left')
    save(down(img.transpose(Image.FLIP_LEFT_RIGHT), s, s), 'arrow_right')

arrows_lr()

def gem(name, color):
    s = 64; S = s * SS
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.ellipse([4 * SS, 4 * SS, S - 4 * SS, S - 4 * SS], fill=GOLD_D + (255,))
    d.ellipse([8 * SS, 8 * SS, S - 8 * SS, S - 8 * SS], fill=GOLD + (255,))
    d.ellipse([14 * SS, 14 * SS, S - 14 * SS, S - 14 * SS], fill=color + (255,))
    d.ellipse([20 * SS, 18 * SS, 32 * SS, 28 * SS], fill=(255, 255, 255, 170))
    save(down(img, s, s), name)

gem('knob', BLOOD)

def checkbox():
    s = 64; S = s * SS
    img = textured(S, S, (26, 20, 18), 10, 6 * SS, 71); img.putalpha(rounded_mask(S, S, 8 * SS))
    d = ImageDraw.Draw(img); gold_frame(d, [1 * SS, 1 * SS, S - 2 * SS, S - 2 * SS], 5 * SS, 8 * SS)
    save(down(img, s, s), 'checkbox')
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.line(P(S, [(0.22, 0.52), (0.42, 0.72), (0.8, 0.26)]), fill=GOLD_L + (255,), width=9 * SS, joint='curve')
    save(down(img, s, s), 'check')

checkbox()

def emblem():
    s = 256; S = s * SS
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    outer = [(0.1, 0.06), (0.9, 0.06), (0.9, 0.5), (0.5, 0.96), (0.1, 0.5)]
    inner = [(0.16, 0.12), (0.84, 0.12), (0.84, 0.49), (0.5, 0.88), (0.16, 0.49)]
    d.polygon(P(S, outer), fill=GOLD_D + (255,))
    d.polygon(P(S, [(x * 0.98 + 0.01, y * 0.98 + 0.01) for x, y in outer]), fill=GOLD + (255,))
    d.polygon(P(S, inner), fill=(255, 255, 255, 255))
    save(down(img, s, s), 'emblem')
    # cross / chevron charge drawn on top, separately so the field can be tinted
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.rectangle(P(S, [(0.44, 0.16), (0.56, 0.8)]), fill=GOLD_L + (255,))
    d.rectangle(P(S, [(0.22, 0.34), (0.78, 0.45)]), fill=GOLD_L + (255,))
    save(down(img, s, s), 'emblem_charge')

emblem()
print('ui ok')
