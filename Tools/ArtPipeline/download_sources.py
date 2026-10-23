"""Download the CC0 source textures and sky from Poly Haven (https://polyhaven.com).

Usage:  python download_sources.py <out_dir>
Fetches 2K diffuse + OpenGL normal maps for the five terrain materials and the 2K sky HDRI.
Output names match what terrain_textures.py expects: <asset>_diff.jpg, <asset>_nor.jpg, sky.hdr
"""
import json
import os
import sys
import urllib.request

ASSETS = ["forrest_ground_01", "leafy_grass", "park_dirt", "cobblestone_floor_08", "mossy_rock"]
SKY = "kloofendal_48d_partly_cloudy_puresky"


def get_json(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "MedievalWarrior-pipeline"})) as r:
        return json.load(r)


def download(url, path):
    print("  ", os.path.basename(path))
    req = urllib.request.Request(url, headers={"User-Agent": "MedievalWarrior-pipeline"})
    with urllib.request.urlopen(req) as r, open(path, "wb") as f:
        while True:
            chunk = r.read(1 << 16)
            if not chunk:
                break
            f.write(chunk)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "sources"
    os.makedirs(out, exist_ok=True)
    for a in ASSETS:
        files = get_json(f"https://api.polyhaven.com/files/{a}")
        download(files["Diffuse"]["2k"]["jpg"]["url"], os.path.join(out, f"{a}_diff.jpg"))
        download(files["nor_gl"]["2k"]["jpg"]["url"], os.path.join(out, f"{a}_nor.jpg"))
    files = get_json(f"https://api.polyhaven.com/files/{SKY}")
    download(files["hdri"]["2k"]["hdr"]["url"], os.path.join(out, "sky.hdr"))
    print("done ->", out)


if __name__ == "__main__":
    main()
