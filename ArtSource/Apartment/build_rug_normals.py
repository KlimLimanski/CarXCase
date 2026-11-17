"""Generate apartment rug tangent normals from staggered wool tufts."""
from pathlib import Path
import bpy
import numpy as np

project = Path(__file__).resolve().parents[2]
output = project / "Assets/_Project/ArtDirection/ApartmentRugs"
output.mkdir(parents=True, exist_ok=True)

def save(name, data):
    size = data.shape[0]
    img = bpy.data.images.new(name, width=size, height=size, alpha=True)
    img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(data.astype(np.float32).ravel())
    img.filepath_raw = str(output / (name + ".png"))
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)

def wool(name, seed, columns, rows, size, loop, nap):
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[:size, :size].astype(np.float32) / size
    v = y * rows + .07 * np.sin(x * np.pi * 12)
    row = np.floor(v).astype(np.int32)
    u = x * columns + .48 * (row % 2) + .09 * np.sin(y * np.pi * 10)
    col = np.floor(u).astype(np.int32)
    u = (u - col - .5) * 2
    v = (v - row - .5) * 2
    fiber = rng.uniform(.64, 1.16, (rows, columns))[row % rows, col % columns]
    tufts = np.exp(-(u*u*1.68 + v*v*.94) * 1.8)
    yarn = np.cos((u*.78 + v*.22)*np.pi*2) * tufts
    height = fiber * (tufts * loop + yarn * nap)
    height *= .84 + .16*np.sin((1.37*x - 2.1*y)*2*np.pi)**2
    height += rng.uniform(-nap*.12,nap*.12,x.shape)
    dx = (np.roll(height,-1,1) - np.roll(height,1,1))*.52
    dy = (np.roll(height,-1,0) - np.roll(height,1,0))*.52
    normal = np.stack([-dx,-dy,np.ones_like(dx)],axis=-1)
    normal /= np.linalg.norm(normal,axis=-1,keepdims=True)
    save(name+"_Normal",np.dstack([normal * .5 + .5,np.ones_like(dx)]))
    if name=="Bath_Terry":
        shade = .90 + height*.125 + .021*np.sin((x*5-y*4)*2*np.pi)
        rgb = shade[:,:,None]*np.array([.57,.52,.43])
        save(name+"_Albedo",np.dstack([rgb,np.ones_like(dx)]))

wool("Living_Worn_Pile",34,153,167,1024,.60,.14)
wool("Bedroom_Indigo_Wool",48,176,194,1024,.56,.11)
wool("Bedside_Flatweave",57,83,155,1024,.37,.14)
wool("Bath_Terry",102,61,77,512,.81,.13)

def patterned_rug():
    size = 1024
    y, x = np.mgrid[:size, :size].astype(np.float32) / size
    rng = np.random.default_rng(129)
    # A faded wine field, two dye colors for the ornament, and an indigo selvedge.
    wine = np.array([.47,.235,.208])
    dark = np.array([.27,.185,.196])
    indigo = np.array([.245,.307,.313])
    ochre = np.array([.68,.558,.385])
    ivory = np.array([.76,.675,.539])
    image = np.broadcast_to(wine,(size,size,3)).copy()
    d = np.minimum(np.minimum(x,1-x),np.minimum(y,1-y))
    image[d<.043] = dark
    image[(d>=.043)&(d<.132)] = indigo
    for offset in [.033,.043,.122,.132,.145]:
        image[np.abs(d-offset)<.0024] = ochre
    where = np.argmin(np.stack([x,1-x,y,1-y]),axis=0)
    along = np.where(where<2,y,x)
    chain = np.abs((along*15+.5)%1-.5)+np.abs(d-.083)/.050
    image[(chain<.64)&(d>.047)&(d<.119)]=ochre
    image[(chain<.38)&(d>.047)&(d<.119)]=wine

    for cx,cy in [(.5,.5),(.285,.285),(.715,.285),(.285,.715),(.715,.715)]:
        radius = .244 if cx==.5 else .121
        diamond = np.abs(x-cx)+np.abs(y-cy)
        image[diamond<radius]=dark
        image[diamond<radius-.010]=ochre
        image[diamond<radius-.021]=indigo
        image[diamond<radius*.38]=wine
        image[np.abs(diamond-radius*.38)<.0035]=ivory
        petals = (np.abs(x-cx)<radius*.102)&(np.abs(y-cy)<radius*.24)
        petals |= (np.abs(y-cy)<radius*.102)&(np.abs(x-cx)<radius*.24)
        image[petals] = ivory

    u = (x*164 + .48*(np.floor(y*176)%2))%1*2-1
    v = (y*176)%1*2-1
    yarn = np.exp(-(u*u*1.8+v*v*.79)*1.8)
    mottling = .95 + .036*np.sin((x*5+y*3)*2*np.pi) + .025*np.sin((x*13-y*15)*2*np.pi)
    wear = .15*np.exp(-(((x-.58)/.23)**2+((y-.38)/.34)**2))
    image = image * (1-wear[:,:,None]) + ivory * wear[:,:,None]
    image *= (mottling + yarn*.08 + rng.uniform(-.034,.034,x.shape))[:,:,None]
    save("Faded_Medallion_Albedo",np.dstack([image,np.ones_like(x)]))

patterned_rug()
wool("Faded_Medallion",182,164,176,1024,.70,.16)
