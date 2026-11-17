"""Five street-facing, metre-scale game buildings. Run inside Blender."""
import bpy
import math
import json
import random
from pathlib import Path
from mathutils import Vector
import numpy as np

OUT = Path('C:/Users/Мария/Documents/ChatGPT/carX/Street_Buildings')
TEX = OUT / 'Textures'
TEX.mkdir(exist_ok=True)
random.seed(43)

def image_file(name, pixels, noncolor=False):
    h, w = pixels.shape[:2]
    image = bpy.data.images.new(name, width=w, height=h, alpha=True)
    if noncolor:
        image.colorspace_settings.name = 'Non-Color'
    image.pixels.foreach_set(pixels.astype(np.float32).ravel())
    image.filepath_raw = str(TEX / (name + '.png'))
    image.file_format = 'PNG'
    image.save()
    image.pack()
    return image

def material(name, color, rough=.7, metal=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = rough
    bsdf.inputs['Metallic'].default_value = metal
    return mat

def textured_material(name, image, normal=None, rough=.8):
    mat = material(name, (.45, .35, .25), rough)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    shader = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    tex = nodes.new('ShaderNodeTexImage')
    tex.image = image
    links.new(tex.outputs['Color'], shader.inputs['Base Color'])
    if normal:
        texn = nodes.new('ShaderNodeTexImage')
        texn.image = normal
        norm = nodes.new('ShaderNodeNormalMap')
        links.new(texn.outputs['Color'], norm.inputs['Color'])
        links.new(norm.outputs['Normal'], shader.inputs['Normal'])
    return mat

def make_textures():
    rng = np.random.default_rng(19)
    n = 1024
    y, x = np.mgrid[0:n, 0:n]
    row = y // 32
    bx = (x + (row % 2) * 48) % 96
    by = y % 32
    joint = np.minimum(np.minimum(bx, 96-bx)/3.2, np.minimum(by, 32-by)/3.0)
    bevel = np.clip(joint, 0, 1)
    # Periodicity along x is exact with 8 bricks per 2.4 metre tile.
    bx = (x / n * 8 + (row % 2)*.5) % 1
    edge = np.minimum(bx, 1-bx)*128
    bevel = np.clip(np.minimum(edge/3.5, np.minimum(by, 32-by)/3), 0, 1)
    brickid = np.floor(x / n * 8 + (row % 2)*.5).astype(int) % 8
    variation = rng.uniform(.78, 1.22, (32, 8))[row, brickid]
    grain = rng.normal(0, .035, (n, n))
    height = bevel * (.77 + grain) + .10*grain
    dx = (np.roll(height, -1, 1)-np.roll(height, 1, 1))*.55
    dy = (np.roll(height, -1, 0)-np.roll(height, 1, 0))*.55
    normals = np.stack([-dx, -dy, np.ones_like(dx)], -1)
    normals /= np.linalg.norm(normals, axis=-1, keepdims=True)
    normal = image_file('Brick_Normal', np.concatenate([normals*.5+.5, np.ones((n,n,1))], -1), True)
    mats = []
    palettes = [('Hudson_Red', (.39,.135,.075)), ('Mercer_Ochre', (.49,.32,.17)),
                ('Fulton_Umber', (.19,.15,.115)), ('Rosette_Russet', (.38,.19,.115)),
                ('Union_Painted', (.60,.60,.52))]
    for name, color in palettes:
        rgb = np.asarray(color)[None,None,:] * variation[...,None] * (1+grain[...,None])
        mortar = np.asarray((.29,.265,.215)) if name != 'Union_Painted' else np.asarray((.38,.39,.34))
        mix = np.clip((bevel-.18)*1.5, 0, 1)[...,None]
        rgb = mortar[None,None,:]*(1-mix)+rgb*mix
        # Slight streaks and uneven fired surfaces remain readable close to the car.
        rgb *= (.95 + .05*np.sin(x*.071)*np.sin(y*.09))[...,None]
        im = image_file(name+'_BaseColor', np.concatenate([np.clip(rgb,0,1),np.ones((n,n,1))], -1))
        mats.append(textured_material(name, im, normal))
    # Eight opaque window treatments, no room geometry or transparency sorting.
    n = 1024
    pixels = np.ones((n,n,4), dtype=float)
    yy, xx = np.mgrid[0:256,0:512]
    for k in range(8):
        tint = np.asarray([(.058,.10,.12),(.10,.14,.15),(.18,.19,.17),(.24,.22,.16),
                           (.07,.13,.16),(.13,.15,.15),(.20,.23,.23),(.075,.09,.10)][k])
        reflection = .022*np.sin(xx*.018+yy*.006) + .04*(yy/256)
        rgb = np.broadcast_to(tint, (256,512,3)).copy()+reflection[...,None]
        curtain = (xx < 82+12*np.sin(yy*.025)) | (xx > 428-12*np.sin(yy*.025))
        rgb[curtain] = np.asarray((.26,.245,.205))*(.85+.15*np.cos(xx[curtain]*.08))[:,None]
        if k in (1,2,5,6):
            blind = yy > (90 if k%2 else 155)
            rgb[blind] = np.asarray((.32,.31,.26))*(.82+.18*np.cos(yy[blind]*1.2))[:,None]
        pixels[(k//2)*256:(k//2+1)*256,(k%2)*512:(k%2+1)*512,:3] = np.clip(rgb,0,1)
    glass = textured_material('Opaque_Window_Atlas',image_file('Window_Atlas',pixels),rough=.26)
    return mats, glass

class MeshBatch:
    def __init__(self, name, collection, transform=None):
        self.name, self.collection = name, collection
        self.transform = transform or (lambda p:p)
        self.verts, self.faces, self.uv, self.mi, self.mats = [], [], [], [], []

    def face(self, points, mat, uv=None):
        start = len(self.verts)
        self.verts.extend(self.transform(p) for p in points)
        self.faces.append(tuple(range(start,start+len(points))))
        if mat not in self.mats:
            self.mats.append(mat)
        self.mi.append(self.mats.index(mat))
        if uv is None:
            # Wall UVs use real scale and remain aligned across separate masonry pieces.
            a,b,c = map(Vector,points[:3])
            normal = (b-a).cross(c-a)
            axis = max(range(3), key=lambda i:abs(normal[i]))
            axes = [(1,2),(0,2),(0,1)][axis]
            uv = [(p[axes[0]]/2.4,p[axes[1]]/2.4) for p in points]
        self.uv.extend(uv)

    def box(self, center, size, mat, rear=True):
        x,y,z = center
        a,b,c = (v/2 for v in size)
        p=[(x-a,y-b,z-c),(x+a,y-b,z-c),(x+a,y+b,z-c),(x-a,y+b,z-c),
           (x-a,y-b,z+c),(x+a,y-b,z+c),(x+a,y+b,z+c),(x-a,y+b,z+c)]
        faces=[(0,1,5,4),(1,2,6,5),(3,0,4,7),(4,5,6,7),(3,2,1,0)]
        if rear:
            faces.append((2,3,7,6))
        for f in faces:
            self.face([p[i] for i in f],mat)

    def rod(self, a, b, radius, mat, sides=6):
        a,b=Vector(a),Vector(b)
        direction=(b-a).normalized()
        tangent=direction.cross(Vector((0,0,1)))
        if tangent.length < .01:
            tangent=direction.cross(Vector((0,1,0)))
        tangent.normalize()
        bitangent=direction.cross(tangent)
        ring=[radius*(math.cos(i*2*math.pi/sides)*tangent+math.sin(i*2*math.pi/sides)*bitangent) for i in range(sides)]
        for i in range(sides):
            j=(i+1)%sides
            self.face([a+ring[i],a+ring[j],b+ring[j],b+ring[i]],mat)

    def finish(self, parent):
        if not self.faces:
            return None
        mesh=bpy.data.meshes.new(self.name)
        mesh.from_pydata(self.verts,[],self.faces)
        mesh.update()
        uv=mesh.uv_layers.new(name='UVMap')
        uv.data.foreach_set('uv',np.asarray(self.uv,dtype=np.float32).ravel())
        for mat in self.mats:
            mesh.materials.append(mat)
        mesh.polygons.foreach_set('material_index',self.mi)
        obj=bpy.data.objects.new(self.name,mesh)
        self.collection.objects.link(obj)
        obj.parent=parent
        return obj

def pane(batch,x,z,w,h,k,depth=.11):
    c,r=k%2,k//2
    u0,u1=c*.5+.004,(c+1)*.5-.004
    v0,v1=r*.25+.004,(r+1)*.25-.004
    batch.face([(x-w/2,depth,z-h/2),(x+w/2,depth,z-h/2),(x+w/2,depth,z+h/2),(x-w/2,depth,z+h/2)],GLASS,[(u0,v0),(u1,v0),(u1,v1),(u0,v1)])

def window(batch,x,z,w,h,frame,seed,ornate=False,ac=False):
    pane(batch,x,z,w,h,seed%8)
    for s in (-1,1):
        batch.box((x+s*(w/2-.045),.035,z),(.09,.14,h),frame,False)
        batch.box((x,.025,z+s*(h/2-.045)),(w,.16,.09),frame,False)
    batch.box((x,-.002,z-.02),(w,.13,.065),frame,False)
    batch.box((x,.02,z),(.045,.12,h),frame,False)
    batch.box((x,-.075,z-h/2-.065),(w+.24,.30,.13),STONE,False)
    batch.box((x,-.04,z+h/2+.095),(w+.22,.20,.19),STONE,False)
    for s in (-1,1):
        batch.box((x+s*(w/2+.055),.035,z),(.11,.10,h+.05),STONE,False)
    if ornate:
        batch.box((x,-.095,z+h/2+.23),(w+.36,.26,.09),STONE,False)
        batch.box((x,-.14,z+h/2+.14),(.18,.17,.27),STONE,False)
    if ac:
        zz=z-h*.27
        batch.box((x+.10,-.28,zz),(.72,.57,.42),AC,False)
        batch.box((x+.10,-.578,zz),(.65,.018,.34),DARK,False)
        for j in range(7):
            batch.box((x+.10,-.595,zz-.13+j*.043),(.59,.025,.018),AC,False)
        for s in (-1,1):
            batch.rod((x+.10+s*.28,-.07,zz-.40),(x+.10+s*.28,-.52,zz-.21),.018,METAL)

def wall_with_openings(batch,start,end,height,openings,mat):
    levels=sorted({0,height,*[o[1]-o[3]/2 for o in openings],*[o[1]+o[3]/2 for o in openings]})
    for low,high in zip(levels,levels[1:]):
        middle=(low+high)/2
        intervals=sorted((x-w/2,x+w/2) for x,z,w,h in openings if z-h/2<middle<z+h/2)
        cursor=start
        for left,right in intervals+[(end,end)]:
            if left-cursor>.001:
                batch.box(((cursor+left)/2,.14,middle),(left-cursor,.28,high-low),mat)
            cursor=right

def arch(batch,x,z,w,mat):
    ri=w/2+.02
    ro=ri+.17
    for j in range(13):
        a=j*math.pi/13
        b=(j+1)*math.pi/13-.012
        points=[(x+ri*math.cos(a),-.13,z+ri*math.sin(a)),(x+ro*math.cos(a),-.13,z+ro*math.sin(a)),
                (x+ro*math.cos(b),-.13,z+ro*math.sin(b)),(x+ri*math.cos(b),-.13,z+ri*math.sin(b))]
        batch.face(points,mat)

def text_label(body,loc,size,mat,collection,parent,max_width=None):
    curve=bpy.data.curves.new('Sign lettering','FONT')
    curve.body=body
    curve.align_x='CENTER'
    curve.align_y='CENTER'
    curve.size=size
    curve.space_character=1.12
    curve.resolution_u=3
    curve.font=FONT
    obj=bpy.data.objects.new(body,curve)
    collection.objects.link(obj)
    obj.parent=parent
    obj.location=loc
    obj.rotation_euler=(math.pi/2,0,0)
    curve.materials.append(mat)
    bpy.context.view_layer.update()
    if max_width and obj.dimensions.x>max_width:
        obj.scale.x*=max_width/obj.dimensions.x
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH')
    return obj

def escape(batch,center,width,levels):
    for index,z in enumerate(levels):
        left,right=center-width/2,center+width/2
        for x in (left,right):
            batch.box((x,-.66,z),(.065,1.25,.10),METAL)
            batch.rod((x,-.06,z-.65),(x,-1.20,z-.04),.028,METAL)
        batch.box((center,-1.23,z),(width,.07,.11),METAL)
        batch.box((center,-.15,z),(width,.08,.11),METAL)
        for j in range(12):
            batch.box((center,-.2-j*.087,z),(width,.032,.038),METAL)
        for zz in (.12,1.02):
            batch.rod((left,-1.24,z+zz),(right,-1.24,z+zz),.024,METAL)
            for x in (left,right):
                batch.rod((x,-.15,z+zz),(x,-1.24,z+zz),.024,METAL)
        for j in range(int(width/.19)+1):
            x=left+j*width/int(width/.19)
            batch.rod((x,-1.24,z+.1),(x,-1.24,z+1.03),.012,METAL,4)
        for x in (left,right):
            for j in range(6):
                y=-.2-j*.19
                batch.rod((x,y,z+.1),(x,y,z+1.03),.012,METAL,4)
        if index<len(levels)-1:
            upper=levels[index+1]
            xa,xb=left+.18,right-.18
            if index%2:
                xa,xb=xb,xa
            for y in (-.37,-.96):
                batch.rod((xa,y,z+.08),(xb,y,upper+.08),.038,METAL)
                batch.rod((xa,y,z+.94),(xb,y,upper+.94),.024,METAL)
            for k in range(15):
                t=k/14
                x=xa+(xb-xa)*t
                zz=z+(upper-z)*t
                batch.box((x,-.665,zz),(.19,.62,.045),METAL)
                if k%2==0:
                    for y in (-.37,-.96):
                        batch.rod((x,y,zz),(x,y,zz+.94),.013,METAL,4)
    z=levels[0]
    for x in (center-width/2+.12,center-width/2+.58):
        batch.rod((x,-.75,2.55),(x,-.75,z+.7),.025,METAL)
    for zz in np.arange(2.65,z+.65,.27):
        batch.rod((center-width/2+.12,-.75,zz),(center-width/2+.58,-.75,zz),.018,METAL)

def build_house(index,spec):
    name,w,d,floors,brick,accent=spec
    h=3.65+(floors-1)*3.1
    collection=bpy.data.collections.new(name)
    SCENE.collection.children.link(collection)
    root=bpy.data.objects.new(name,None)
    collection.objects.link(root)
    root['front_direction']='-Y'
    root['detailed_side']='+X'
    root['units']='metres'
    root['width_m']=w
    root['depth_m']=d
    root['height_m']=h+.55
    root['placement']='Origin at front centre, ground level. Align front Y=0. Shell edges touch at half-width sums.'
    front=MeshBatch(name+'_Facade',collection)
    side=MeshBatch(name+'_Right_Side',collection,lambda p:(w/2-p[1],p[0],p[2]))
    shell=MeshBatch(name+'_Simple_Shell',collection)
    iron=MeshBatch(name+'_Fire_Escape',collection)
    trim=MeshBatch(name+'_Street_Details',collection)
    bays=4 if w>9 else 3
    xs=[-w/2+w*(j+.5)/bays for j in range(bays)]
    levels=[3.65+j*3.1 for j in range(floors-1)]
    openings=[]
    for j,base in enumerate(levels):
        for k,x in enumerate(xs):
            z=base+1.39
            openings.append((x,z,1.22,1.92))
            window(front,x,z,1.22,1.92,accent,index*13+j*3+k,ornate=index in (1,3),ac=(j+k+index)%4==0)
            if index==1 and j==len(levels)-1:
                arch(front,x,z+.98,1.22,STONE)
    # Ground floor combines one full-height entrance and two recessed shopfronts.
    doorx=-w/2+1.05
    openings.append((doorx,1.45,1.18,2.90))
    pane(front,doorx,1.40,1.12,2.70,7)
    for x in (doorx-.58,doorx+.58):
        front.box((x,-.045,1.45),(.12,.20,2.90),accent,False)
    for z in (.13,2.70,2.91):
        front.box((doorx,-.05,z),(1.28,.22,.12),accent,False)
    front.box((doorx,.0,.42),(1.0,.16,.55),accent,False)
    front.rod((doorx+.34,-.20,1.02),(doorx+.34,-.20,1.39),.022,BRASS,8)
    front.box((doorx+.76,-.12,1.38),(.15,.07,.23),BRASS,False)
    front.box((doorx,-.32,.055),(1.40,.70,.11),STONE)
    shopstart=doorx+.95
    shopend=w/2-.38
    sw=(shopend-shopstart-.22)/2
    for k in range(2):
        x=shopstart+sw/2+k*(sw+.22)
        openings.append((x,1.52,sw,2.36))
        pane(front,x,1.52,sw,2.36,index+2)
        for xx in (x-sw/2,x+sw/2):
            front.box((xx,-.065,1.52),(.10,.23,2.48),accent,False)
        for z in (.34,2.70):
            front.box((x,-.065,z),(sw+.1,.23,.12),accent,False)
        front.box((x,-.08,.56),(sw,.21,.42),accent,False)
        front.box((x,-.07,2.20),(sw,.18,.065),accent,False)
        front.box((x,-.05,1.48),(.07,.16,1.34),accent,False)
    wall_with_openings(front,-w/2,w/2,h,openings,brick)
    for z,depth,height in ((.16,.10,.32),(3.62,.25,.20),(h-.23,.25,.20),(h+.02,.38,.16),(h+.23,.46,.16)):
        front.box((0,-depth/2,z),(w,depth,height),STONE,False)
        side.box((d/2,-depth/2,z),(d,depth,height),STONE,False)
    for x in (-w/2+.16,w/2-.16):
        front.box((x,-.045,1.78),(.32,.20,3.4),STONE,False)
    # Repeated cornice brackets are front-facing and use open rear faces.
    for x in np.linspace(-w/2+.30,w/2-.30,int(w/.42)):
        front.box((x,-.17,h-.43),(.15,.30,.30),STONE,False)
        front.box((x,-.24,h-.30),(.23,.34,.09),STONE,False)
    side_open=[]
    for j,base in enumerate(levels):
        for k,u in enumerate((1.7,4.0,6.3,8.6)):
            if u>d-.7:
                continue
            side_open.append((u,base+1.39,1.0,1.78))
            window(side,u,base+1.39,1.0,1.78,accent,40+index+j+k,ac=(j+k+index)%6==0)
    wall_with_openings(side,0,d,h,side_open,brick)
    # Back and left party wall are each one quad. Roof is one flat cap.
    shell.face([(-w/2,d,0),(-w/2,d,h),(w/2,d,h),(w/2,d,0)],BACK)
    shell.face([(-w/2,0,0),(-w/2,0,h),(-w/2,d,h),(-w/2,d,0)],BACK)
    shell.face([(-w/2,0,h), (w/2,0,h),(w/2,d,h),(-w/2,d,h)],ROOF)
    # Only the visible parapets are modeled.
    front.box((0,.13,h+.26),(w,.26,.48),brick)
    side.box((d/2,.13,h+.26),(d,.26,.48),brick)
    front.box((0,-.02,h+.51),(w,.38,.08),STONE,False)
    side.box((d/2,-.02,h+.51),(d,.38,.08),STONE,False)
    for x in (-w/2+.43,w/2-.43):
        trim.rod((x,-.20,.4),(x,-.20,h-.5),.047,METAL,8)
        trim.rod((x,-.20,.4),(x,-.40,.25),.047,METAL,8)
        for zz in np.arange(.9,h,2.3):
            trim.box((x,-.20,zz),(.15,.14,.055),METAL,False)
    # Different escape positions and widths make the five silhouettes distinct.
    center=[.45,.4,0,.6,.35][index]
    ew=min(w-2.5,4.4 if index==2 else 3.65)
    escape(iron,center,ew,[b+.32 for b in levels])
    signs=['HUDSON MARKET','MERCER PHARMACY','FULTON HARDWARE','CAFE ROSETTE','UNION LAUNDRY']
    small=['GROCERIES  •  DELI','EST. 1936','TOOLS & SUPPLIES','BAKERY  •  COFFEE','WASH  •  DRY  •  FOLD']
    sx=(shopstart+shopend)/2
    signw=shopend-shopstart+.15
    trim.box((sx,-.19,3.12),(signw,.30,.66),accent,False)
    for zz in (2.82,3.42):
        trim.box((sx,-.36,zz),(signw,.055,.045),BRASS,False)
    text_label(signs[index],(sx,-.355,3.17),.34,LETTER,collection,root,signw-.32)
    text_label(small[index],(sx,-.358,2.94),.115,LETTER,collection,root,signw-.35)
    trim.box((doorx,-.13,3.17),(1.0,.08,.36),DARK,False)
    text_label(str(118+index*4),(doorx,-.181,3.17),.20,LETTER,collection,root)
    # Pitched canvas canopy on two buildings, kept clear of escape landing.
    if index in (0,3):
        sections=14
        for j in range(sections):
            a=shopstart+(shopend-shopstart)*j/sections
            b=shopstart+(shopend-shopstart)*(j+1)/sections
            mat=accent if j%2==0 else CANVAS
            trim.face([(a,-.2,2.77),(b,-.2,2.77),(b,-1.02,2.50),(a,-1.02,2.50)],mat)
            trim.face([(a,-1.02,2.35),(b,-1.02,2.35),(b,-1.02,2.50),(a,-1.02,2.50)],mat)
        for x in (shopstart,shopend):
            trim.rod((x,-.12,2.15),(x,-1.00,2.51),.022,METAL)
    # Wall lamps, service boxes, vents and legible small facade signs.
    for x in (shopstart+.25,shopend-.25):
        trim.box((x,-.10,3.43),(.13,.12,.20),METAL,False)
        trim.rod((x,-.12,3.45),(x,-.48,3.61),.025,METAL)
        trim.box((x,-.48,3.60),(.31,.21,.07),METAL)
        trim.box((x,-.48,3.56),(.24,.16,.018),LETTER,False)
    trim.box((w/2-.68,-.15,.95),(.34,.18,.48),AC,False)
    for j in range(5):
        trim.box((w/2-.68,-.25,.82+j*.045),(.25,.02,.012),DARK,False)
    for batch in (front,side,shell,iron,trim):
        batch.finish(root)
    return root,collection

def aim(obj,point):
    obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()

def setup():
    global SCENE, GLASS, STONE, METAL, DARK, AC, BRASS, BACK, ROOF, LETTER, CANVAS, FONT
    SCENE=bpy.data.scenes.new('Street_Buildings')
    bpy.context.window.scene=SCENE
    SCENE.unit_settings.system='METRIC'
    SCENE.unit_settings.scale_length=1.0
    FONT=bpy.data.fonts.load('C:/Windows/Fonts/arialbd.ttf')
    bricks,GLASS=make_textures()
    STONE=material('Warm_Limestone',(.47,.43,.34),.84)
    METAL=material('Painted_Iron',(.037,.044,.042),.56,.6)
    DARK=material('Deep_Shadow',(.025,.034,.032),.7)
    AC=material('Weathered_AC',(.44,.46,.42),.65,.35)
    BRASS=material('Aged_Brass',(.36,.23,.095),.48,.7)
    BACK=material('Simple_Party_Walls',(.24,.23,.21),.95)
    ROOF=material('Flat_Roof',(.09,.095,.09),.94)
    LETTER=material('Sign_Ivory',(.86,.77,.55),.55)
    CANVAS=material('Canvas_Cream',(.65,.59,.43),.9)
    accents=[material('Market_Bottle_Green',(.035,.11,.085)),material('Pharmacy_Oxblood',(.15,.035,.028)),
             material('Hardware_Midnight',(.035,.065,.083)),material('Cafe_Wine',(.19,.045,.036)),
             material('Laundry_Petrol',(.035,.13,.14))]
    specs=[('01_Hudson',9.6,10.4,5),('02_Mercer',8.4,10.0,4),('03_Fulton',10.8,11.0,6),
           ('04_Rosette',9.0,10.3,5),('05_Union',8.7,10.0,4)]
    roots=[]
    cursor=-sum(s[1] for s in specs)/2
    for i,spec in enumerate(specs):
        root,col=build_house(i,(*spec,bricks[i],accents[i]))
        root.location.x=cursor+spec[1]/2
        cursor+=spec[1]
        roots.append(root)
    SCENE['asset_brief']='Five game buildings, detailed front -Y and right +X, simplified rear and left, no interiors.'
    SCENE['car_eye_height_m']=1.2
    bpy.context.view_layer.update()
    print('BUILT',[(r.name,len(r.children)) for r in roots])
    return roots

if __name__=='__main__':
    ROOTS=setup()
