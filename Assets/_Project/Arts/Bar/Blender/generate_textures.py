from PIL import Image, ImageDraw, ImageFilter
import numpy as np, os, json, shutil
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT=ROOT+'/Textures'
os.makedirs(OUT,exist_ok=True)
rng=np.random.default_rng(91)
N=1024
y,x=np.mgrid[0:N,0:N].astype(float)/N
def smooth_noise(size,stretch=(1,1)):
    a=rng.random((max(2,int(size*stretch[1])),max(2,int(size*stretch[0]))))
    return np.asarray(Image.fromarray(np.uint8(a*255)).resize((N,N),Image.Resampling.BICUBIC)).astype(float)/255
def save_set(name,color,height,rough,metal=0):
    color=np.clip(color,0,1)
    Image.fromarray(np.uint8(color*255)).save(OUT+'/'+name+'_BaseColor.png')
    if np.isscalar(rough): rough=np.full((N,N),rough)
    rough=np.clip(rough,0,1)
    Image.fromarray(np.uint8(rough*255)).save(OUT+'/'+name+'_Roughness.png')
    gy,gx=np.gradient(height)
    v=np.stack((-gx*13,-gy*13,np.ones((N,N))),axis=2)
    v/=np.linalg.norm(v,axis=2)[:,:,None]
    Image.fromarray(np.uint8((v*.5+.5)*255)).save(OUT+'/'+name+'_Normal.png')
    pack=np.zeros((N,N,4),dtype=np.uint8)
    pack[:,:,:3]=int(metal*255); pack[:,:,3]=np.uint8((1-rough)*255)
    Image.fromarray(pack).save(OUT+'/'+name+'_MetallicSmoothness.png')
for name,base,rough in [('Walnut',(0.29,.155,.078),.4),('CounterWood',(.40,.225,.117),.24),('FloorWood',(.245,.161,.108),.58)]:
    warp=smooth_noise(9,(1,.35))
    grain=np.sin(x*340+warp*8+np.sin(y*14)*2)
    fine=np.sin(x*1350+warp*38)
    noise=smooth_noise(220,(1,.07))
    h=.46+.09*grain+.035*fine+.20*(noise-.5)+.09*(smooth_noise(10)-.5)
    c=np.array(base)[None,None,:]*(.60+h[:,:,None]*.8)
    c+=((smooth_noise(150)-.5)*.022)[:,:,None]
    r=rough+.10*(noise-.5)
    if name=='FloorWood':
        cuts=np.zeros((N,N)); scratches=np.zeros((N,N))
        for i in range(600):
            xx=int(rng.integers(N)); yy=int(rng.integers(N)); ll=int(rng.integers(3,80))
            scratches[yy:min(N,yy+ll),xx]=rng.random()*.12
        c+=scratches[:,:,None]; r+=scratches*.5
    save_set(name,c,h,r)
h=smooth_noise(600)*.2+smooth_noise(80)*.13
save_set('GreenVinyl',np.array([.10,.16,.105])[None,None,:]*(.85+h[:,:,None]),h,.37+smooth_noise(100)*.12)
h=smooth_noise(400)*.12+smooth_noise(12)*.18
save_set('GreenWall',np.array([.19,.225,.135])[None,None,:]*(.80+h[:,:,None]),h,.83)
h=smooth_noise(380)*.1+smooth_noise(6)*.30
save_set('AgedBrass',np.array([.63,.43,.18])[None,None,:]*(.75+h[:,:,None]),h,.28+smooth_noise(12)*.18,1)
h=smooth_noise(700,(.02,1))*.15
save_set('BrushedSteel',np.array([.52,.54,.51])[None,None,:]*(.9+h[:,:,None]),h,.32+h,1)
h=smooth_noise(25)*.3+smooth_noise(240)*.1
save_set('Ceiling',np.array([.32,.25,.16])[None,None,:]*(.8+h[:,:,None]),h,.82)
ref=Image.open(ROOT+'/Reference_Collage.png').convert('RGB')
for name,box in {'Art_North':(796,522,845,557),'Art_01':(53,35,98,77),'Art_02':(125,54,143,111),'Art_03':(274,81,281,108),'FamilyPhoto':(558,878,590,915)}.items():
    ref.crop(box).resize((512,512),Image.Resampling.LANCZOS).save(OUT+'/'+name+'_BaseColor.png')
print('Textures saved', len(os.listdir(OUT)))
