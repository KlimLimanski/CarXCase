from PIL import Image,ImageFilter
import numpy as np, pathlib, shutil
root=pathlib.Path(__file__).resolve().parent.parent
tex=root/'Textures';rng=np.random.default_rng(1999);N=1024
y,x=np.mgrid[0:N,0:N]/N
def noise(w,h):
    return np.asarray(Image.fromarray(np.uint8(rng.random((h,w))*255)).resize((N,N),Image.Resampling.BICUBIC),dtype=float)/255
for name,base,rough in [('FloorWood',(.235,.145,.087),.63),('Walnut',(.25,.125,.057),.44),('CounterWood',(.35,.182,.083),.28)]:
    warp=noise(9,6)
    grain=(np.sin(x*110+warp*8+np.sin(y*6)*1.5)+1)*.5
    grain2=noise(320,9)
    pores=noise(700,70)
    broad=noise(32,4)
    long=noise(110,2)
    h=.22*grain+.30*grain2+.17*pores+.2*broad+.11*long
    factor=.55+h*1.08
    c=np.asarray(base)[None,None,:]*factor[:,:,None]
    r=rough+(noise(20,10)-.5)*.16
    if name=='FloorWood':
        wear=noise(24,24)*noise(64,10)
        wear=np.clip((wear-.27)*2.0,0,.32)
        c+=wear[:,:,None]*np.array([.17,.13,.085])
        r+=wear*.15
        for i in range(1600):
            xx=int(rng.integers(N)); yy=int(rng.integers(N)); length=int(rng.integers(3,45));w=int(rng.integers(1,3))
            strength=rng.random()*.075
            c[yy:min(N,yy+length),xx:min(N,xx+w)] += strength*np.array([1,.8,.5])
    c=np.clip(c,0,1);r=np.clip(r,0,1)
    Image.fromarray(np.uint8(c*255)).save(tex/(name+'_BaseColor.png'))
    Image.fromarray(np.uint8(r*255)).save(tex/(name+'_Roughness.png'))
    gy,gx=np.gradient(h);v=np.stack((-gx*4,-gy*4,np.ones((N,N))),axis=2);v/=np.linalg.norm(v,axis=2)[:,:,None]
    Image.fromarray(np.uint8((v*.5+.5)*255)).save(tex/(name+'_Normal.png'))
    pack=np.zeros((N,N,4),dtype=np.uint8);pack[:,:,3]=np.uint8((1-r)*255)
    Image.fromarray(pack).save(tex/(name+'_MetallicSmoothness.png'))
print('Refined directional wood grain and moderate wear.')
