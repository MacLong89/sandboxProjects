"""Generate all original PLUNGE pixel art. Actor PNGs are true RGBA transparency."""
from pathlib import Path
from PIL import Image, ImageDraw
import math, random

ROOT = Path(__file__).resolve().parents[1] / "Assets"
SP = ROOT / "sprites"
UI = ROOT / "ui"
BG = ROOT / "backgrounds"
for d in (SP, UI, BG): d.mkdir(parents=True, exist_ok=True)

C = {
    "ink": "#071421", "outline": "#102b3a", "gold": "#e9ae35", "yellow": "#f4ca42",
    "copper": "#a86032", "orange": "#e68432", "glass": "#76d7df", "foam": "#d8f4f2",
    "blue": "#2784a5", "cyan": "#35cbd0", "green": "#4cae73", "purple": "#a25ac4",
    "pink": "#d4589b", "red": "#ce4e50", "rock": "#293646", "rock2": "#182431",
}

def im(w,h): return Image.new("RGBA",(w,h),(0,0,0,0))
def rect(d, xy, c): d.rectangle(xy, fill=c)
def ell(d, xy, c): d.ellipse(xy, fill=c)
def poly(d, xy, c): d.polygon(xy, fill=c)
def save(img, path, scale=4):
    out=img.resize((img.width*scale,img.height*scale),Image.Resampling.NEAREST)
    # Normalize fully transparent pixels to zero RGB.
    data=[(0,0,0,0) if p[3]==0 else p for p in out.getdata()]
    out.putdata(data); out.save(path)

def diver(f):
    a=im(32,32); d=ImageDraw.Draw(a); bob=[0,1,0,-1][f]
    rect(d,(7,9+bob,10,22+bob),C["blue"]); rect(d,(20,9+bob,23,22+bob),C["blue"])
    ell(d,(8,5+bob,24,21+bob),C["gold"]); ell(d,(11,8+bob,22,18+bob),C["glass"])
    ell(d,(13,9+bob,17,13+bob),C["foam"]); rect(d,(10,19+bob,21,27+bob),C["copper"])
    rect(d,(4,20+bob,10,23+bob),C["copper"]); rect(d,(21,20+bob,27,23+bob),C["copper"])
    kick=[0,2,0,-2][f]; rect(d,(8+kick,27+bob,14+kick,30+bob),C["orange"])
    rect(d,(18-kick,27+bob,24-kick,30+bob),C["orange"])
    return a

def submarine(f, color=C["yellow"]):
    a=im(48,28); d=ImageDraw.Draw(a); b=[0,1,0,-1][f]
    ell(d,(5,7+b,42,24+b),color); rect(d,(11,7+b,36,23+b),color)
    ell(d,(7,9+b,19,21+b),C["outline"]); ell(d,(9,11+b,17,19+b),C["glass"])
    ell(d,(10,12+b,13,15+b),C["foam"]); rect(d,(27,4+b,35,8+b),C["copper"])
    rect(d,(40,11+b,45,20+b),C["copper"]); rect(d,(44,8+[0,2,0,-2][f]+b,46,23-[0,2,0,-2][f]+b),C["cyan"])
    rect(d,(2,13+b,7,17+b),C["gold"])
    return a

def fish(f, color, rare=False):
    a=im(20,12); d=ImageDraw.Draw(a); flap=[0,1,0,-1][f]
    ell(d,(4,2,18,10),color); poly(d,[(5,6),(0,2+flap),(0,10-flap)],color)
    if rare: rect(d,(9,3,11,9),C["pink"])
    rect(d,(15,4,16,5),C["ink"]); return a

def shark(f):
    a=im(40,18); d=ImageDraw.Draw(a); b=[0,1,0,-1][f]
    ell(d,(3,5+b,34,16+b),C["rock"]); poly(d,[(33,10+b),(39,3+[0,2,0,-2][f]),(38,16-[0,2,0,-2][f])],C["rock"])
    poly(d,[(14,6+b),(21,0+b),(23,7+b)],C["rock"]); rect(d,(8,9+b,10,10+b),C["red"])
    return a

def jelly(f):
    a=im(16,24); d=ImageDraw.Draw(a); p=[0,1,0,-1][f]
    ell(d,(2-p,1,14+p,12),C["pink"]); rect(d,(3,7,13,11),C["purple"])
    for x in (4,7,10,13):
        d.line([(x,11),(x+([0,1,0,-1][(f+x)%4]),21)], fill=C["pink"], width=1)
    return a

def object_sprite(kind):
    a=im(20,20); d=ImageDraw.Draw(a)
    if kind=="chest":
        rect(d,(2,7,18,17),C["copper"]); rect(d,(2,7,18,10),C["gold"]); rect(d,(9,10,11,14),C["yellow"])
    elif kind=="crate":
        rect(d,(3,4,17,18),C["copper"]); d.rectangle((3,4,17,18),outline=C["gold"],width=2); d.line((3,4,17,18),fill=C["gold"]); d.line((17,4,3,18),fill=C["gold"])
    elif kind=="crystal":
        poly(d,[(10,1),(17,9),(10,19),(3,9)],C["cyan"]); poly(d,[(10,2),(11,15),(7,9)],C["foam"])
    elif kind=="idol":
        rect(d,(5,4,15,18),C["rock"]); ell(d,(4,1,16,10),C["rock2"]); rect(d,(7,5,8,6),C["gold"]); rect(d,(12,5,13,6),C["gold"])
    elif kind=="coral":
        for x,h in ((4,11),(9,16),(14,9)):
            rect(d,(x,20-h,x+2,19),C["purple"]); ell(d,(x-1,19-h,x+3,23-h),C["pink"])
    elif kind=="rock":
        poly(d,[(2,16),(5,6),(11,3),(18,8),(17,18),(4,18)],C["rock"]); poly(d,[(6,14),(9,7),(14,9),(13,15)],C["rock2"])
    elif kind=="drone":
        rect(d,(2,6,18,16),C["rock"]); ell(d,(6,8,14,16),C["cyan"]); rect(d,(0,9,3,12),C["gold"]); rect(d,(17,9,20,12),C["gold"])
    elif kind=="bubble":
        d.ellipse((3,3,17,17),outline=(180,235,255,150),width=2); ell(d,(5,5,8,8),C["foam"])
    return a

def icon(kind):
    a=im(16,16); d=ImageDraw.Draw(a)
    if kind=="heart": ell(d,(1,3,8,10),C["red"]); ell(d,(7,3,14,10),C["red"]); poly(d,[(2,7),(14,7),(8,15)],C["red"])
    elif kind=="o2": rect(d,(5,2,11,14),C["cyan"]); rect(d,(6,0,10,3),C["gold"])
    elif kind=="bolt": poly(d,[(9,0),(3,9),(7,9),(5,16),(13,6),(9,6)],C["yellow"])
    elif kind=="coin": ell(d,(1,1,15,15),C["yellow"]); ell(d,(4,4,12,12),C["gold"])
    elif kind=="gem": poly(d,[(8,1),(14,6),(11,14),(5,14),(2,6)],C["purple"])
    elif kind=="camera": rect(d,(2,5,14,13),C["rock"]); ell(d,(5,6,11,12),C["cyan"])
    elif kind=="knife": poly(d,[(9,1),(11,2),(7,12),(5,11)],C["foam"]); rect(d,(4,11,9,14),C["copper"])
    elif kind=="lamp": ell(d,(3,2,13,12),C["yellow"]); rect(d,(6,11,10,15),C["copper"])
    elif kind=="bag": rect(d,(3,5,13,15),C["copper"]); d.arc((5,1,11,8),180,360,fill=C["gold"],width=2)
    elif kind=="shield": poly(d,[(8,1),(14,4),(12,12),(8,15),(4,12),(2,4)],C["blue"])
    elif kind=="prop": rect(d,(2,7,14,9),C["cyan"]); rect(d,(7,2,9,14),C["cyan"]); ell(d,(6,6,10,10),C["gold"])
    elif kind=="sonar": d.arc((2,2,14,14),200,340,fill=C["cyan"],width=2); d.arc((5,5,11,11),200,340,fill=C["foam"],width=2)
    elif kind=="lock": rect(d,(4,7,12,15),C["gold"]); d.arc((5,1,11,11),180,360,fill=C["gold"],width=2)
    else: ell(d,(3,3,13,13),C["cyan"])
    return a

def background(name, top, bottom, depth_seed):
    random.seed(depth_seed); w,h=320,180; a=Image.new("RGBA",(w,h),top); d=ImageDraw.Draw(a)
    for y in range(h):
        t=y/(h-1)
        c=tuple(int(int(top[i:i+2],16)*(1-t)+int(bottom[i:i+2],16)*t) for i in (1,3,5))
        rect(d,(0,y,w,y),c+(255,))
    # ruins and rock silhouettes
    for i in range(16):
        x=random.randint(0,w); rw=random.randint(12,45); rh=random.randint(20,90)
        rect(d,(x,h-rh,x+rw,h),C["rock2"])
        if i%4==0: d.arc((x+3,h-rh+5,x+rw-3,h-rh+rw),180,360,fill=C["rock"],width=3)
    for i in range(30):
        x=random.randint(0,w); y=random.randint(20,h-15)
        ell(d,(x,y,x+random.randint(1,3),y+random.randint(1,3)), random.choice([C["cyan"],C["purple"],C["blue"]]))
    a.resize((1280,720),Image.Resampling.NEAREST).save(BG/f"{name}.png")

for f in range(4):
    save(diver(f),SP/f"diver_{f}.png")
    save(submarine(f),SP/f"sub_{f}.png")
    save(fish(f,C["orange"]),SP/f"fish_common_{f}.png")
    save(fish(f,C["blue"]),SP/f"fish_blue_{f}.png")
    save(fish(f,C["purple"],True),SP/f"fish_rare_{f}.png")
    save(shark(f),SP/f"shark_{f}.png")
    save(jelly(f),SP/f"jelly_{f}.png")
for o in ("chest","crate","crystal","idol","coral","rock","drone","bubble"): save(object_sprite(o),SP/f"{o}.png")
for i in ("heart","o2","bolt","coin","gem","camera","knife","lamp","bag","shield","prop","sonar","lock","helmet","suit","flippers","harpoon","book","settings"):
    save(icon(i),UI/f"{i}.png",3)
background("shallows","#17658a","#082a48",1)
background("reef","#0e4968","#071c34",2)
background("cavern","#092c4a","#040d1d",3)
background("abyss","#071427","#02040c",4)
print("Generated original PLUNGE RGBA sprites and backgrounds.")
