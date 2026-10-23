"""Castle texture grading + knight recolour masks.

Usage:  python castle_and_knight.py <repo_root>

1. Castle: the asset pack's textures are very dark (~50/255), which reads as near-black stone in
   linear lighting. Writes warmer, brighter graded copies (_D) and luminance-derived normal maps (_N)
   to Assets/Environment/Textures/Castle. The pack's own files are never modified.
2. Knight: copies the atlas to Assets/Resources/Customization/KnightBase.png and writes KnightMask.png
   (R = tabard/shield cloth, G = metal, B = leather) used by KnightAppearance for recolouring.
"""
import glob
import os
import sys

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

root = sys.argv[1] if len(sys.argv) > 1 else "."
A = os.path.join(root, "Assets")

# ------------------------------------------------------------------ castle
src = os.path.join(A, "Advance Studios/Medieval Castle/Art/Textures")
dst = os.path.join(A, "Environment/Textures/Castle")
os.makedirs(dst, exist_ok=True)
for f in glob.glob(src + "/*.png"):
    name = os.path.splitext(os.path.basename(f))[0]
    im = Image.open(f).convert("RGB")
    a = np.asarray(im).astype(np.float32) / 255
    house = name.startswith("Casa")
    doors = name.startswith("Doors")
    blur = ndimage.gaussian_filter(a, sigma=(18, 18, 0))
    base = blur * 0.7 + blur.mean(axis=(0, 1)) * 0.3          # even out the grime streaks
    g = np.clip(base + (a - blur) * 1.2, 0, 1)
    gamma, gain = (0.5, 1.3) if house else (0.72, 1.3) if doors else (0.55, 1.22)
    tint = np.array([1.1, 1.0, 0.82]) if house else np.array([1.0, 0.97, 0.9]) if doors else np.array([1.06, 0.99, 0.87])
    out = np.clip(np.power(g, gamma) * gain * tint, 0, 1)
    Image.fromarray((out * 255).astype(np.uint8)).save(f"{dst}/{name}_D.jpg", quality=92)
    # normal map from luminance: bricks stand out, mortar sinks
    lum = ndimage.gaussian_filter(np.asarray(im.convert("L")).astype(np.float32) / 255, 1.2)
    dx = ndimage.sobel(lum, axis=1)
    dy = ndimage.sobel(lum, axis=0)
    n = np.dstack([-dx * 3.2, dy * 3.2, np.ones_like(lum)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8)).save(f"{dst}/{name}_N.jpg", quality=92)
    print("castle", name)

# ------------------------------------------------------------------ knight masks
cust = os.path.join(A, "Resources/Customization")
os.makedirs(cust, exist_ok=True)
im = Image.open(os.path.join(A, "Toon_RTS_demo/models/Materials/DemoTexture.png")).convert("RGB")
im.save(os.path.join(cust, "KnightBase.png"))
hsv = np.asarray(im.convert("HSV")).astype(np.float32) / 255
h, s, v = hsv[..., 0] * 360, hsv[..., 1], hsv[..., 2]


def ss(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


warm = ss(5, 14, h) * (1 - ss(48, 60, h))
cloth = warm * ss(0.38, 0.5, s) * ss(0.32, 0.42, v)
leather = np.maximum(warm * ss(0.2, 0.3, s) * (1 - ss(0.32, 0.42, v)) * ss(0.08, 0.14, v) - cloth, 0)
metal = (1 - ss(0.12, 0.22, s)) * ss(0.36, 0.5, v)
mask = Image.fromarray((np.stack([cloth, metal, leather], -1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7))
mask.save(os.path.join(cust, "KnightMask.png"))
print("knight masks done")
