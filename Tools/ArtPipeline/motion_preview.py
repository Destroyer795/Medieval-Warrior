"""Assemble actual Unity review frames into lossless animated PNGs (stdlib only)."""
import struct, zlib, argparse
from fractions import Fraction
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
FOLDER=ROOT/'Artifacts/Marchwarden/Smoke'
TARGET=ROOT/'Artifacts/Marchwarden'

def chunk(kind,data):
    return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)

def read_png(path):
    blob=path.read_bytes(); offset=8; chunks=[]
    while offset<len(blob):
        length=struct.unpack('>I',blob[offset:offset+4])[0]
        kind,data=blob[offset+4:offset+8],blob[offset+8:offset+8+length]
        crc=struct.unpack('>I',blob[offset+8+length:offset+12+length])[0]
        if crc!=(zlib.crc32(kind+data)&0xffffffff): raise RuntimeError('Invalid PNG chunk CRC: '+str(path))
        chunks.append((kind,data)); offset+=12+length
    return chunks

def assemble(name,ms):
    paths=sorted(FOLDER.glob('motion_'+name+'_*.png'))
    if not paths: raise RuntimeError('No Unity motion captures for '+name)
    first=read_png(paths[0]); header=next(d for k,d in first if k==b'IHDR'); w,h=struct.unpack('>II',header[:8])
    output=bytearray(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',header)+chunk(b'acTL',struct.pack('>II',len(paths),0)))
    sequence=0
    delay=Fraction(ms/1000).limit_denominator(10000)
    for i,path in enumerate(paths):
        parts=read_png(path)
        if next(d for k,d in parts if k==b'IHDR')!=header: raise RuntimeError('Inconsistent PNG capture formats')
        output.extend(chunk(b'fcTL',struct.pack('>IIIIIHHBB',sequence,w,h,0,0,delay.numerator,delay.denominator,0,0))); sequence+=1
        for kind,data in parts:
            if kind!=b'IDAT': continue
            if i==0: output.extend(chunk(b'IDAT',data))
            else: output.extend(chunk(b'fdAT',struct.pack('>I',sequence)+data)); sequence+=1
    output.extend(chunk(b'IEND',b''))
    TARGET.mkdir(parents=True,exist_ok=True)
    target=TARGET/('Preview-'+name+'.png'); target.write_bytes(output)
    verified=read_png(target)
    assert sum(k==b'fcTL' for k,d in verified)==len(paths) and verified[-1][0]==b'IEND'
    print(f'{target.name}: {len(paths)} frames, {len(output)//1024} KiB')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--folder',type=Path,default=FOLDER,help='Unity motion capture folder, absolute or relative to the repository')
    parser.add_argument('--output',type=Path,default=TARGET,help='Preview output directory')
    parser.add_argument('--sword-ms',type=float,default=1000/30,help='Sword frame delay; current captures sample every two frames at 60 Hz')
    args=parser.parse_args()
    FOLDER=args.folder if args.folder.is_absolute() else ROOT/args.folder
    TARGET=args.output if args.output.is_absolute() else ROOT/args.output
    assemble('sword',args.sword_ms); assemble('bow',90)
    if list(FOLDER.glob('motion_block_*.png')):assemble('block',65)
    if list(FOLDER.glob('motion_cloth_*.png')):assemble('cloth',100)
