"""Fetch the two documented CC0 Poly Haven PBR sets into the ignored art cache.

Run with Blender's Python: blender --background --python this_file -- --refresh
The public API supplies exact URLs/checksums; downloads are verified before use.
"""
import json,hashlib,urllib.request,concurrent.futures,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
CACHE=ROOT/'Artifacts/Buildings/Scans';CACHE.mkdir(parents=True,exist_ok=True)
MANIFEST=ROOT/'Tools/ArtPipeline/Buildings/scanned-materials.json'
AGENT='MedievalWarrior-ArtPipeline (https://github.com/Destroyer795/Medieval-Warrior)'
def read(url):return urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':AGENT}),timeout=90)
if '--refresh' in sys.argv or not MANIFEST.exists():
    entries=[]
    for asset in ('castle_wall_slates','rock_wall_02'):
        with read('https://api.polyhaven.com/files/'+asset) as source:files=json.load(source)
        for name in ('Diffuse','nor_gl','Displacement','AO','Rough'):
            format='png' if name=='Displacement' else 'jpg';data=files[name]['4k'][format]
            entries.append(dict(asset=asset,map=name,format=format,url=data['url'],size=data['size'],md5=data['md5']))
    MANIFEST.write_text(json.dumps(entries,indent=2))
entries=json.loads(MANIFEST.read_text(encoding='utf-8-sig'))
def download(entry):
    path=CACHE/(entry['asset']+'_'+entry['map']+'.'+entry['format'])
    if path.exists() and hashlib.md5(path.read_bytes()).hexdigest()==entry['md5']:return str(path.name)+' cached'
    with read(entry['url']) as source,path.open('wb') as target:
        while True:
            data=source.read(1<<20)
            if not data:break
            target.write(data)
    if hashlib.md5(path.read_bytes()).hexdigest()!=entry['md5']:raise RuntimeError('Checksum mismatch: '+str(path))
    return str(path.name)+' verified'
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    for future in concurrent.futures.as_completed([pool.submit(download,e) for e in entries]):print('[CC0 PBR]',future.result(),flush=True)
print('[CC0 PBR] Complete',flush=True)
