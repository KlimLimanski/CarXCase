"""Reference-led bar. Coordinates: X east, Y north, Z up; metres.
Run in Blender's Python console. See README for reference ambiguities.
"""
import bpy, bmesh, math, os, json, random
from mathutils import Vector, Matrix
from math import pi, sin, cos
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX=ROOT+'/Textures'
random.seed(1999)
previous=bpy.data.scenes.get('Bar1999_Reference')
if previous:
    old_mats={m for o in previous.objects if o.type=='MESH' for m in o.data.materials if m}
    old_cols=list(previous.collection.children)
    if bpy.context.window.scene==previous:
        bpy.context.window.scene=next(s for s in bpy.data.scenes if s!=previous)
    for o in list(previous.objects):bpy.data.objects.remove(o,do_unlink=True)
    bpy.data.scenes.remove(previous)
    for c in old_cols:bpy.data.collections.remove(c)
    for me in list(bpy.data.meshes):
        if me.users==0:bpy.data.meshes.remove(me)
    for m in old_mats:
        if m.users==0:bpy.data.materials.remove(m)
S=bpy.data.scenes.new('Bar1999_Reference')
bpy.context.window.scene=S
S.unit_settings.system='METRIC'; S.unit_settings.scale_length=1
S.render.engine='CYCLES'
S.cycles.samples=48; S.cycles.use_denoising=True
S.render.resolution_x=1440; S.render.resolution_y=900; S.render.resolution_percentage=100
S.render.image_settings.file_format='PNG'
S.render.film_transparent=False
S.world=bpy.data.worlds.new('Bar_Interior_World'); S.world.use_nodes=True
next(n for n in S.world.node_tree.nodes if n.type=='BACKGROUND').inputs[0].default_value=(.13,.16,.20,1)
next(n for n in S.world.node_tree.nodes if n.type=='BACKGROUND').inputs[1].default_value=.045
S.view_settings.exposure=.75
cols={}
for name in ['ENV_Architecture','ENV_Bar','ENV_Furniture','ENV_Props','ENV_Doors','ENV_Lighting','ENV_Neon','TECH_Collision','TECH_Navigation','TECH_SpawnPoints','TECH_Cinematics']:
    c=bpy.data.collections.new(name); S.collection.children.link(c); cols[name]=c
M={}; cache={}; collisions=[]; interactives=[]; lights=[]
def mat(name,color,rough=.45,metal=0,texture=None,emit=0,alpha=1):
    m=bpy.data.materials.new(name); m.use_nodes=True
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value=(*color,alpha)
    bs.inputs['Roughness'].default_value=rough; bs.inputs['Metallic'].default_value=metal
    bs.inputs['Alpha'].default_value=alpha
    m.diffuse_color=(*color,alpha)
    if emit:
        bs.inputs['Emission Color'].default_value=(*color,1); bs.inputs['Emission Strength'].default_value=emit
    if texture:
        for suffix,socket in [('BaseColor','Base Color'),('Roughness','Roughness'),('Normal','Normal')]:
            path=f'{TEX}/{texture}_{suffix}.png'
            if not os.path.isfile(path):continue
            n=m.node_tree.nodes.new('ShaderNodeTexImage'); n.image=bpy.data.images.load(path,check_existing=True)
            if suffix!='BaseColor': n.image.colorspace_settings.name='Non-Color'
            if suffix=='Normal':
                norm=m.node_tree.nodes.new('ShaderNodeNormalMap'); norm.inputs['Strength'].default_value=.45
                m.node_tree.links.new(n.outputs['Color'],norm.inputs['Color']); m.node_tree.links.new(norm.outputs['Normal'],bs.inputs[socket])
            else:m.node_tree.links.new(n.outputs['Color'],bs.inputs[socket])
        m['texture_set']=texture
    M[name]=m; return m
wal=mat('Walnut',(.12,.055,.021),texture='Walnut')
top=mat('CounterWood',(.22,.105,.038),.23,texture='CounterWood')
floor=mat('FloorWood',(.13,.07,.032),texture='FloorWood')
vinyl=mat('GreenVinyl',(.037,.075,.04),.4,texture='GreenVinyl')
green=mat('GreenWall',(.06,.09,.04),texture='GreenWall')
brass=mat('AgedBrass',(.50,.29,.08),.34,1,'AgedBrass')
steel=mat('BrushedSteel',(.4,.42,.4),.32,1,'BrushedSteel')
ceiling=mat('Ceiling',(.21,.16,.09),texture='Ceiling')
black=mat('BlackMetal',(.014,.017,.015),.47,.3)
rubber=mat('Rubber',(.012,.014,.012),.86)
cream=mat('Register_Ivory',(.52,.44,.31),.46)
paper=mat('Paper',(.72,.64,.46),.7)
burgundy=mat('Chair_Leather',(.13,.028,.018),.35)
gold=mat('LabelGold',(.65,.42,.15),.58)
glass=mat('ClearGlass',(.68,.79,.72),.12,0,alpha=.32)
bs=next(n for n in glass.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Transmission Weight'].default_value=.92;bs.inputs['IOR'].default_value=1.46
mirror=mat('Mirror',(.83,.86,.80),.07,1)
screen=mat('Screen',(.012,.025,.020),.3)
amber=mat('BottleAmber',(.115,.038,.009),.19,.12)
bgreen=mat('BottleGreen',(.018,.075,.024),.20,.12)
clear=mat('BottleClear',(.31,.35,.25),.2,.1,alpha=.70)
red=mat('RedNeon',(1,.012,.005),.25,emit=7)
neongreen=mat('GreenNeon',(.01,1,.065),.3,emit=5)
lamp=mat('LampGlow',(1,.63,.24),.32,emit=3)
white=mat('GlassLamp',(1,.78,.45),.3,emit=1.4)
straw=mat('StrawRed',(.52,.008,.006),.6)
digit=mat('DisplayGreen',(.08,.8,.08),.5,emit=1.4)
artm={n:mat(n,(.6,.5,.3),.78,texture=n) for n in ['Art_North','Art_01','Art_02','Art_03','FamilyPhoto']}
def link(o,col):
    cols[col].objects.link(o); return o
def empty(name,loc=(0,0,0),col='ENV_Props'):
    o=bpy.data.objects.new(name,None);link(o,col);o.location=loc;o.empty_display_size=.12;return o
def parent(o,p):
    if p:
        o.parent=p;o.matrix_parent_inverse=Matrix.Translation(-p.location)
    return o
def uvmesh(me):
    me.update()
    a=me.uv_layers.new(name='UV0'); b=me.uv_layers.new(name='LightmapUV')
    k=math.ceil(math.sqrt(max(1,len(me.polygons))))
    for i,p in enumerate(me.polygons):
        normal=p.normal; axis=max(range(3),key=lambda a:abs(normal[a])); axes=[a for a in range(3) if a!=axis]
        coords=[(me.vertices[me.loops[l].vertex_index].co[axes[0]],me.vertices[me.loops[l].vertex_index].co[axes[1]]) for l in p.loop_indices]
        mn=[min(c[j] for c in coords) for j in range(2)]; mx=[max(c[j] for c in coords) for j in range(2)]
        d=max(mx[0]-mn[0],mx[1]-mn[1],.001)
        for l,c in zip(p.loop_indices,coords):
            a.data[l].uv=(c[0]/.65+.17,c[1]/.65+.11)
            b.data[l].uv=((i%k+.12+.76*(c[0]-mn[0])/d)/k,(i//k+.12+.76*(c[1]-mn[1])/d)/k)
def mesh(name,verts,faces,matl,col='ENV_Props',loc=(0,0,0),p=None,bevel=0,smooth=False,key=None):
    if key is not None and key in cache:me=cache[key]
    else:
        me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata(verts,[],faces);me.update()
        if bevel:
            bm=bmesh.new();bm.from_mesh(me)
            bmesh.ops.bevel(bm,geom=list(bm.edges),offset=bevel,segments=3,affect='EDGES')
            bm.to_mesh(me);bm.free()
        me.materials.append(matl)
        if smooth:
            for poly in me.polygons:poly.use_smooth=True
        uvmesh(me)
        if key is not None:cache[key]=me
    o=bpy.data.objects.new(name,me);link(o,col);o.location=loc;parent(o,p);return o
def box(name,loc,size,ma=wal,col='ENV_Props',bevel=.006,p=None):
    a,b,c=[v/2 for v in size]
    v=[(-a,-b,-c),(-a,-b,c),(-a,b,-c),(-a,b,c),(a,-b,-c),(a,-b,c),(a,b,-c),(a,b,c)]
    f=[(2,6,4,0),(5,7,3,1),(4,5,1,0),(3,7,6,2),(1,3,2,0),(6,7,5,4)]
    return mesh(name,v,f,ma,col,loc,p,min(bevel,min(size)/4),key=('box',tuple(size),ma.name,bevel))
def lathe(name,loc,profile,ma,col='ENV_Props',n=24,p=None):
    verts=[]
    for z,r in profile:
        verts.extend([(r*cos(2*pi*i/n),r*sin(2*pi*i/n),z) for i in range(n)])
    faces=[]
    for j in range(len(profile)-1):
        for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    faces.extend([tuple(reversed(range(n))),tuple((len(profile)-1)*n+i for i in range(n))])
    return mesh(name,verts,faces,ma,col,loc,p,smooth=True,key=('lathe',tuple(profile),ma.name,n))
def cyl(name,loc,r,depth,ma,col='ENV_Props',p=None,n=24):
    return lathe(name,loc,[(-depth/2,r),(depth/2,r)],ma,col,n,p)
def rod(name,a,b,r,ma,col='ENV_Props',p=None):
    a=Vector(a);b=Vector(b);o=cyl(name,(a+b)*.5,r,(b-a).length,ma,col,p,16);o.rotation_mode='QUATERNION';o.rotation_quaternion=(b-a).to_track_quat('Z','Y');return o
def tube(name,pts,r,ma,col='ENV_Props',p=None,closed=False):
    verts=[];faces=[];n=10
    for j,pt in enumerate(pts):
        pt=Vector(pt); tangent=Vector(pts[min(j+1,len(pts)-1)])-Vector(pts[max(j-1,0)])
        if tangent.length<1e-6:tangent=Vector((0,0,1))
        tangent.normalize(); a=tangent.cross(Vector((0,0,1)))
        if a.length<.01:a=tangent.cross(Vector((0,1,0)))
        a.normalize();b=tangent.cross(a)
        verts.extend([pt+r*(a*cos(2*pi*i/n)+b*sin(2*pi*i/n)) for i in range(n)])
    count=len(pts) if closed else len(pts)-1
    for j in range(count):
        for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,((j+1)%len(pts))*n+(i+1)%n,((j+1)%len(pts))*n+i))
    return mesh(name,verts,faces,ma,col,p=p,smooth=True)
def ring(name,loc,r,thick,ma,col='ENV_Props',p=None):
    return tube(name,[(loc[0]+r*cos(i*2*pi/48),loc[1]+r*sin(i*2*pi/48),loc[2]) for i in range(48)],thick,ma,col,p,True)
def label(name,text,loc,size,ma=black,rot=(pi/2,0,0),col='ENV_Props',font=None,p=None):
    cu=bpy.data.curves.new(name+'_Text','FONT');cu.body=text;cu.size=size;cu.align_x='CENTER';cu.align_y='CENTER';cu.extrude=.0003;cu.resolution_u=6
    if font:cu.font=font
    cu.materials.append(ma);o=bpy.data.objects.new(name,cu);link(o,col);o.location=loc;o.rotation_euler=rot;parent(o,p);return o
def decal(name,loc,w,h,ma,rot=(pi/2,0,0),col='ENV_Props',p=None):
    o=mesh(name,[(-w/2,-h/2,0),(w/2,-h/2,0),(w/2,h/2,0),(-w/2,h/2,0)],[(0,1,2,3)],ma,col,loc,p)
    o.rotation_euler=rot
    for l,uv in zip(o.data.polygons[0].loop_indices,[(0,0),(1,0),(1,1),(0,1)]):o.data.uv_layers[0].data[l].uv=uv
    return o
def coll(name,loc,size,owner='',rot=(0,0,0),zone='static'):
    o=empty('COL_'+name,loc,'TECH_Collision');o.empty_display_size=.08;o['size']=size;o['owner']=owner;o['zone']=zone
    collisions.append(dict(name=name,center=list(loc),size=list(size),rotation=list(rot),owner=owner,zone=zone))
def inter(o,kind,locked=False):
    o['interactive']=kind;o['locked']=locked;interactives.append(dict(name=o.name,kind=kind,locked=locked))
def join_children(root,name=None):
    ch=[o for o in root.children_recursive if o.type=='MESH']
    if not ch:return
    ch[0].data=ch[0].data.copy() # keep the primitive cache immutable
    bpy.ops.object.select_all(action='DESELECT')
    for o in ch:o.select_set(True)
    bpy.context.view_layer.objects.active=ch[0];bpy.ops.object.join()
    o=ch[0];o.name=name or root.name+'_Geometry';return o
def duplicate(root,name,offset,col='ENV_Furniture'):
    bpy.context.view_layer.update()
    nr=root.copy();nr.name=name;cols[col].objects.link(nr);nr.location+=Vector(offset)
    for child in root.children:
        cp=child.copy();cp.data=child.data;cols[col].objects.link(cp);cp.parent=nr
        cp.matrix_parent_inverse=child.matrix_parent_inverse.copy()
        cp.matrix_basis=child.matrix_basis.copy()
    return nr
def light(name,loc,power,color,kind='POINT',size=.2,target=None):
    d=bpy.data.lights.new(name,kind);d.energy=power;d.color=color
    if kind=='AREA':d.shape='DISK';d.size=size
    else:d.shadow_soft_size=size
    o=bpy.data.objects.new(name,d);link(o,'ENV_Lighting');o.location=loc
    if target:o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    lights.append(o);return o

# Interior shell, floorboards and continuous wainscot.
box('Floor_Slab',(3.2,5,-.11),(6.4,10,.16),black,'ENV_Architecture',0)
coll('Floor',(3.2,5,-.08),(6.4,10,.16))
for i in range(32):
    x=.1+i*.2
    for j in range(6):
        a=max(0,j*1.65+(.81 if i%2 else 0)-.82);b=min(8,(j+1)*1.65+(.81 if i%2 else 0)-.82)
        if b>a:
            o=box(f'Floor_Plank_{i:02}_{j}',(x,(a+b)/2,-.014),(.197,b-a-.006,.028),floor,'ENV_Architecture',.001)
for n,loc,sz in [('WestWall',(-.09,5,1.35),(.18,10,2.7)),('EastWall',(6.49,5,1.35),(.18,10,2.7)),('RearShell',(3.2,10.09,1.35),(6.4,.18,2.7))]:
    box(n,loc,sz,green,'ENV_Architecture',0);coll(n,loc,sz)
box('Ceiling',(3.2,5,2.76),(6.4,10,.12),ceiling,'ENV_Architecture',0);coll('Ceiling',(3.2,5,2.76),(6.4,10,.12))
# South wall: entry opening 0.95 m; the single window visible in view 03.
for n,loc,sz in [('South_West',(.325,-.09,1.35),(.65,.18,2.7)),('South_DoorHeader',(1.125,-.09,2.465),(.95,.18,.47)),('South_Middle',(2.025,-.09,1.35),(.85,.18,2.7)),('South_East',(6.0,-.09,1.35),(.8,.18,2.7)),('South_UnderWindow',(4.025,-.09,.48),(3.15,.18,.96)),('South_OverWindow',(4.025,-.09,2.55),(3.15,.18,.30))]:
    box(n,loc,sz,green,'ENV_Architecture',0);coll(n,loc,sz)
# North public partition: Restroom x0.90..1.85; Staff x5.15..6.10.
for n,a,b in [('NW',0,.90),('NC',1.85,5.15),('NE',6.10,6.4)]:
    box('North_'+n,((a+b)/2,8.09,1.35),(b-a,.18,2.7),green,'ENV_Architecture',0);coll('North_'+n,((a+b)/2,8.09,1.35),(b-a,.18,2.7))
for x in [1.375,5.625]:
    box('North_Lintel',(x,8.09,2.47),(.95,.18,.46),green,'ENV_Architecture',0);coll('North_Lintel_'+str(x),(x,8.09,2.47),(.95,.18,.46))
# Opaque restroom boundary, no interior fixtures or traversable floor.
box('Restroom_ClosedVolume',(1.14,9.10,1.35),(2.28,1.80,2.7),black,'ENV_Architecture',0);coll('Restroom_ClosedVolume',(1.14,9.10,1.35),(2.28,1.8,2.7))
box('Rear_UnseenBoundary',(3.42,9.1,1.35),(2.28,1.8,2.7),green,'ENV_Architecture',0);coll('Rear_UnseenBoundary',(3.42,9.1,1.35),(2.28,1.8,2.7))
def wainscot(axis,a,b,fixed,prefix):
    length=b-a;count=round(length/.56)
    for i in range(count):
        pos=a+(i+.5)*length/count;w=length/count
        loc=(fixed,pos,.51) if axis=='Y' else (pos,fixed,.51)
        sz=(.05,w-.012,1.02) if axis=='Y' else (w-.012,.05,1.02)
        box(prefix+f'_Panel_{i}',loc,sz,wal,'ENV_Architecture',.006)
        l=(fixed+(.03 if fixed<1 else -.03),pos,.51) if axis=='Y' else (pos,fixed+(.03 if fixed<1 else -.03),.51)
        s=(.025,w-.10,.84) if axis=='Y' else (w-.10,.025,.84)
        box(prefix+f'_Inset_{i}',l,s,wal,'ENV_Architecture',.01)
    for z,h,depth in [(.07,.14,.075),(1.035,.055,.09),(1.08,.025,.065)]:
        box(prefix+'_Rail',(fixed,(a+b)/2,z) if axis=='Y' else ((a+b)/2,fixed,z),(depth,length,h) if axis=='Y' else (length,depth,h),wal,'ENV_Architecture',.008)
wainscot('Y',0,8,.025,'West');wainscot('Y',0,8,6.375,'East')
for a,b in [(0,.85),(1.9,5.1),(6.15,6.4)]:wainscot('X',a,b,7.975,'North')
for a,b in [(0,.6),(1.65,6.4)]:wainscot('X',a,b,.025,'South')
for z,width,height in [(2.49,.08,.09),(2.57,.13,.08),(2.65,.19,.07)]:
    for x in [.035,6.365]:box('Crown_EW',(x,4,z),(width,8,height),wal,'ENV_Architecture',.01)
    for y in [.035,7.965]:box('Crown_NS',(3.2,y,z),(6.4,width,height),wal,'ENV_Architecture',.01)
for x in [2.42,5.63]:box('Window_Jamb',(x,.035,1.68),(.10,.12,1.50),wal,'ENV_Architecture')
for z in [.97,2.40]:box('Window_Rail',(4.025,.035,z),(3.30,.12,.10),wal,'ENV_Architecture')
box('Window_NightGlass',(4.025,-.01,1.69),(3.11,.018,1.37),glass,'ENV_Architecture',0)
box('Window_ExteriorDark',(4.025,-.17,1.69),(3.11,.02,1.37),black,'ENV_Architecture',0)
for i in range(24):
    o=box(f'Window_VenetianSlat_{i}',(4.025,.055,1.02+i*.056),(3.08,.045,.025),wal,'ENV_Architecture',.001);o.rotation_euler.x=.18
for x in [3,4.025,5.05]:rod('BlindCord',(x,.09,1),(x,.09,2.38),.003,black,'ENV_Architecture')
print('Architecture complete',len(S.objects))

# Doors use hinge-origin parent objects, independent leaf geometry and hardware.
def door(name,x,y,entry=False,locked=False):
    r=empty(name,(x,y,0),'ENV_Doors');inter(r,'Door',locked)
    if entry:
        box(name+'_LowerPanel',(x+.475,y,.48),(.95,.075,.96),wal,'ENV_Doors',.01,r)
        for xx in [x+.065,x+.885]:box(name+'_Stile',(xx,y,1.58),(.13,.075,1.18),wal,'ENV_Doors',.008,r)
        for z in [1.02,2.16]:box(name+'_GlazingRail',(x+.475,y,z),(.82,.075,.13),wal,'ENV_Doors',.008,r)
        box(name+'_Glass',(x+.475,y,1.59),(.69,.018,1.01),glass,'ENV_Doors',.001,r)
        box('HoursCard',(x+.475,y+.046,1.52),(.36,.009,.49),paper,'ENV_Doors',.002,r)
        label('Hours','HOURS\nMon - Sat\n11 AM - 2 AM\nSun\n12 PM - 12 AM',(x+.475,y+.054,1.52),.043,black,(-pi/2,0,pi),'ENV_Doors',p=r)
        box(name+'_Kickplate',(x+.475,y+.043,.13),(.86,.012,.21),brass,'ENV_Doors',.003,r)
    else:
        box(name+'_Leaf',(x+.475,y,1.10),(.95,.075,2.20),wal,'ENV_Doors',.008,r)
        box(name+'_Recess',(x+.475,y-.045,1.14),(.73,.02,1.84),wal,'ENV_Doors',.01,r)
        box(name+'_Kickplate',(x+.475,y-.06,.13),(.84,.013,.22),brass,'ENV_Doors',.003,r)
        box(name+'_Plaque',(x+.475,y-.062,1.68),(.51,.014,.13),gold,'ENV_Doors',.003,r)
        label(name+'_Sign','RESTROOM' if locked else 'STAFF ONLY',(x+.475,y-.071,1.68),.057,black,p=r,col='ENV_Doors')
    for xx in [x-.055,x+1.005]:box(name+'_Frame',(xx,y,1.14),(.11,.17,2.28),wal,'ENV_Architecture')
    box(name+'_Header',(x+.475,y,2.25),(1.17,.17,.12),wal,'ENV_Architecture')
    side=1 if entry else -1
    box(name+'_HandlePlate',(x+.82,y+side*.055,1.0),(.075,.018,.30),brass,'ENV_Doors',.004,r)
    rod(name+'_Lever',(x+.82,y+side*.09,1.03),(x+.72,y+side*.09,1.03),.013,brass,'ENV_Doors',r)
    for z in [.25,1.1,1.95]:cyl(name+'_Hinge',(x+.015,y,z),.015,.10,brass,'ENV_Doors',r)
    coll(name,(x+.475,y,1.1),(.95,.08,2.2),name,zone='door')
    r['open_degrees']=90 if entry else -90
    return r
entry=door('Door_Entry',.65,.025,True)
rest=door('Door_Restroom',.90,8.0,locked=True)
staff=door('Door_StaffOnly',5.15,8.0)
box('Exit_Housing',(1.125,.08,2.43),(.48,.13,.21),cream,'ENV_Neon')
box('Exit_RedPanel',(1.125,.151,2.43),(.43,.012,.17),red,'ENV_Neon',.002)
label('EXIT','EXIT',(1.125,.162,2.43),.15,paper,(-pi/2,0,pi),'ENV_Neon')

# The bar counter: 0.65 m deep and 1.10 m high; no additional return.
bar=empty('Bar_Counter',(0,0,0),'ENV_Bar')
box('Bar_Carcass',(4.585,4.475,.51),(.56,5.67,1.02),wal,'ENV_Bar',.012,bar)
box('Bar_PolishedTop',(4.575,4.475,1.055),(.65,5.75,.09),top,'ENV_Bar',.032,bar)
box('Bar_Bullnose',(4.267,4.475,1.065),(.06,5.71,.063),top,'ENV_Bar',.025,bar)
for i in range(7):
    y=1.65+(i+.5)*5.65/7
    box(f'Bar_FrontPanel_{i}',(4.292,y,.56),(.035,.69,.71),wal,'ENV_Bar',.016,bar)
    for yy in [y-.37,y+.37]:box('Bar_PanelStile',(4.263,yy,.54),(.06,.045,.91),wal,'ENV_Bar',.006,bar)
for z in [.12,.94]:box('Bar_FrontRail',(4.262,4.475,z),(.065,5.65,.065),wal,'ENV_Bar',.012,bar)
for y in [1.625,7.325]:
    box('Bar_EndPanel',(4.58,y,.54),(.45,.035,.76),wal,'ENV_Bar',.015,bar)
    box('Bar_EndKick',(4.58,y,.12),(.52,.05,.065),wal,'ENV_Bar',.008,bar)
railx=4.07
tube('Brass_FootRail_38mm',[(4.52,1.43,.20),(4.15,1.43,.20),(4.07,1.52,.20),(4.07,7.26,.20),(4.15,7.34,.20),(4.52,7.34,.20)],.019,brass,'ENV_Bar',bar)
for y in [1.8,3.15,4.5,5.85,7.1]:
    rod('FootRail_Bracket',(4.30,y,.16),(4.07,y,.20),.014,brass,'ENV_Bar',bar)
    cyl('FootRail_FloorMount',(4.07,y,.055),.029,.09,brass,'ENV_Bar',bar)
coll('Bar_Counter',(4.575,4.475,.55),(.65,5.75,1.10))
# Seven identical stools with the correct seat diameter and height.
def stool(name,x,y):
    r=empty(name,(x,y,0),'ENV_Furniture');inter(r,'Stool')
    lathe(name+'_Cushion',(x,y,0),[(.704,.17),(.71,.18),(.746,.18),(.76,.166)],vinyl,'ENV_Furniture',40,r)
    ring(name+'_SeatPiping',(x,y,.735),.18,.005,brass,'ENV_Furniture',r)
    cyl(name+'_SeatBase',(x,y,.694),.164,.025,wal,'ENV_Furniture',r)
    for a in [pi/4,3*pi/4,5*pi/4,7*pi/4]:
        rod(name+'_Leg',(x+.215*cos(a),y+.215*sin(a),.035),(x+.13*cos(a),y+.13*sin(a),.69),.021,wal,'ENV_Furniture',r)
        cyl(name+'_BrassShoe',(x+.208*cos(a),y+.208*sin(a),.064),.023,.105,brass,'ENV_Furniture',r)
    ring(name+'_FootRing',(x,y,.28),.189,.012,brass,'ENV_Furniture',r)
    join_children(r)
    return r
st=stool('BarStool_01',3.83,1.88)
coll('BarStool_01',(3.83,1.88,.38),(.43,.43,.76),'BarStool_01',zone='movable')
for i in range(1,7):
    o=duplicate(st,f'BarStool_{i+1:02}',(0,i*.815,0));inter(o,'Stool');coll(o.name,(3.83,1.88+i*.815,.38),(.43,.43,.76),o.name,zone='movable')
# Two booths, each with opposing 1.20 m benches and a single tabletop.
def bench(name,y,flip=False):
    x=.79;r=empty(name,(x,y,0),'ENV_Furniture')
    box(name+'_Base',(x,y,.22),(1.20,.56,.42),wal,'ENV_Furniture',.018,r)
    box(name+'_Seat',(x,y,.47),(1.20,.57,.14),vinyl,'ENV_Furniture',.055,r)
    dy=.24 if flip else -.24
    box(name+'_BackFrame',(x,y+dy,.76),(1.22,.16,.83),wal,'ENV_Furniture',.025,r)
    for i in range(6):
        box(name+f'_BackChannel_{i}',(.29+i*.20,y+dy+(-.045 if flip else .045),.83),(.196,.19,.64),vinyl,'ENV_Furniture',.045,r)
    for xx in [.205,1.375]:box(name+'_EndBolster',(xx,y,.67),(.07,.54,.36),vinyl,'ENV_Furniture',.026,r)
    join_children(r);coll(name,(x,y,.59),(1.25,.61,1.18))
    return r
for i,cy in enumerate([3.95,6.45]):
    bench(f'Booth_{i+1}_BenchSouth',cy-.75);bench(f'Booth_{i+1}_BenchNorth',cy+.75,True)
    r=empty(f'Booth_{i+1}_Table',(0,0,0),'ENV_Furniture')
    box('BoothTable_Top',(.80,cy,.76),(1.20,.71,.065),top,'ENV_Furniture',.025,r)
    cyl('BoothTable_Stem',(.80,cy,.37),.045,.70,black,'ENV_Furniture',r)
    box('BoothTable_Base',(.80,cy,.035),(.64,.44,.07),black,'ENV_Furniture',.025,r)
    coll(f'Booth_{i+1}_Table',(.8,cy,.4),(1.20,.71,.8))
    box('Table_NapkinHolder',(.80,cy,.83),(.10,.08,.105),steel,'ENV_Props',.01)
    box('Table_Napkins',(.80,cy,.875),(.085,.055,.035),paper,'ENV_Props',.002)
    box('Table_Menu',(.58,cy,.91),(.11,.018,.25),paper,'ENV_Props',.002)
    label('Table_MenuText','MENU',(.58,cy-.012,.98),.019,black)
# Single round table and two chairs, matching the southwest furniture cluster.
roundtable=empty('RoundTable',(1.32,1.55,0),'ENV_Furniture')
lathe('RoundTable_Top',(1.32,1.55,0),[(.715,.445),(.73,.46),(.765,.455),(.78,.435)],top,'ENV_Furniture',64,roundtable)
cyl('RoundTable_Stem',(1.32,1.55,.375),.045,.69,black,'ENV_Furniture',roundtable)
for a in [0,pi/2,pi,3*pi/2]:rod('RoundTable_Foot',(1.32,1.55,.16),(1.32+.36*cos(a),1.55+.36*sin(a),.03),.025,black,'ENV_Furniture',roundtable)
coll('RoundTable',(1.32,1.55,.39),(.92,.92,.78))
def chair(name,x,y,angle):
    r=empty(name,(x,y,0),'ENV_Furniture');inter(r,'Chair')
    lathe(name+'_Seat',(x,y,0),[(.425,.205),(.44,.22),(.48,.215),(.49,.19)],burgundy,'ENV_Furniture',32,r)
    ring(name+'_SeatRim',(x,y,.438),.215,.016,wal,'ENV_Furniture',r)
    for dx in [-.14,.14]:
        for dy in [-.14,.14]:rod(name+'_Leg',(x+dx*1.35,y+dy*1.35,.02),(x+dx,y+dy,.44),.017,wal,'ENV_Furniture',r)
    for dx in [-.17,.17]:rod(name+'_BackPost',(x+dx,y+.17,.4),(x+dx,y+.21,.91),.019,wal,'ENV_Furniture',r)
    for dx in [-.075,0,.075]:rod(name+'_Spindle',(x+dx,y+.18,.49),(x+dx,y+.21,.86),.011,wal,'ENV_Furniture',r)
    box(name+'_BackRail',(x,y+.21,.89),(.38,.055,.09),wal,'ENV_Furniture',.03,r)
    for z in [.17]:
        for dy in [-.16,.16]:rod(name+'_Stretcher',(x-.17,y+dy,z),(x+.17,y+dy,z),.012,wal,'ENV_Furniture',r)
    join_children(r);r.rotation_euler.z=angle
    coll(name,(x,y,.47),(.47,.47,.94),name,zone='movable');return r
chair('RoundTable_Chair_01',1.32,2.24,0)
chair('RoundTable_Chair_02',2.01,1.55,-pi/2)
box('RoundTable_Menu',(1.34,1.57,.91),(.12,.025,.26),paper,'ENV_Props',.003)
label('RoundTable_MenuText','MENU',(1.34,1.553,.985),.022,black)

# Back bar remains east, with a 1.10 m clear strip x=4.90..6.00.
back=empty('BackBar',(0,0,0),'ENV_Bar')
box('BackBar_Base',(6.20,4.45,.45),(.40,5.68,.90),wal,'ENV_Bar',.008,back)
box('BackBar_Worktop',(6.19,4.45,.925),(.40,5.72,.05),steel,'ENV_Bar',.008,back)
coll('BackBar_Base',(6.20,4.45,.48),(.4,5.72,.96))
for j,y in enumerate([2.25,3.55,4.85,6.15]):
    box(f'BackBar_Refrigerator_{j}',(5.987,y,.45),(.025,1.17,.72),black,'ENV_Bar',.015,back)
    box('BackBar_FridgeGlass',(5.970,y,.47),(.012,1.04,.58),glass,'ENV_Bar',.004,back)
    rod('Fridge_Handle',(5.95,y-.40,.40),(5.95,y-.40,.67),.012,steel,'ENV_Bar',back)
    for zz in [.25,.54]:box('Fridge_Shelf',(6.15,y,zz),(.28,1.05,.012),steel,'ENV_Bar',.002,back)
    for zz in [.14,.18]:box('Fridge_Vent',(5.968,y,zz),(.012,.99,.013),steel,'ENV_Bar',.002,back)
for y in [1.6,2.8,4.0,5.2,6.4,7.30]:
    box('BackBar_Pilaster',(6.13,y,1.78),(.33,.10,1.62),wal,'ENV_Bar',.012,back)
    for z in [1.02,2.51]:box('BackBar_ColumnCap',(6.12,y,z),(.39,.17,.08),wal,'ENV_Bar',.012,back)
for z,sz in [(1.01,.08),(2.52,.10),(2.60,.08)]:box('BackBar_Cornice',(6.18,4.45,z),(.39,5.85,sz),wal,'ENV_Bar',.015,back)
for j,(a,b) in enumerate([(1.67,2.74),(2.86,3.94),(4.06,5.14),(5.26,6.34),(6.46,7.24)]):
    box('BackBar_Mirror',(6.37,(a+b)/2,1.77),(.018,b-a,1.40),mirror,'ENV_Bar',.003,back)
    for z in [1.17,1.62,2.06]:
        box('BackBar_GlassShelf',(6.18,(a+b)/2,z),(.36,b-a,.018),glass,'ENV_Bar',.002,back)
        rod('Shelf_BrassLip',(5.998,a,z),(5.998,b,z),.006,brass,'ENV_Bar',back)

# Narrow service area shown through Staff Only. Nothing is built in the unknown rear block.
box('Staff_Floor',(5.49,9.10,-.015),(1.82,1.8,.03),floor,'ENV_Architecture',0)
box('Staff_RubberMat',(5.47,8.98,.012),(1.04,1.65,.024),rubber,'ENV_Props',.008)
for side,x in [('Left',4.74),('Right',6.18)]:
    box('Staff_'+side+'_Counter',(x,9.10,.91),(.35,1.72,.06),steel,'ENV_Bar',.008)
    box('Staff_'+side+'_Cabinet',(x,9.10,.43),(.35,1.68,.84),steel,'ENV_Bar',.008)
    coll('Staff_'+side,(x,9.10,.46),(.35,1.72,.92))
    for z in [1.5,1.98]:box('Staff_'+side+'_Shelf',(x,9.10,z),(.32,1.64,.025),steel,'ENV_Bar',.003)
# A shallow steel sink has a recessed basin surface and visible rim, not an added room.
box('Staff_SinkBasin',(4.74,8.72,.87),(.29,.42,.02),black,'ENV_Props',.015)
for xx in [4.575,4.905]:box('Staff_SinkSide',(xx,8.72,.94),(.02,.49,.025),steel,'ENV_Props',.004)
for yy in [8.475,8.965]:box('Staff_SinkRim',(4.74,yy,.94),(.35,.02,.025),steel,'ENV_Props',.004)
tube('Staff_Faucet',[(4.62,8.98,.95),(4.62,8.98,1.19),(4.67,8.96,1.25),(4.74,8.88,1.25),(4.77,8.83,1.17)],.012,steel)
for i in range(12):
    for side in ['Left','Right']:
        x=4.73 if side=='Left' else 6.18
        cyl('Staff_Glass',(x,8.39+i*.115,1.57),.027,.12,glass)
print('Furniture and bar complete',len(S.objects))

# Small props: identical bottles, open glasses, bar service details.
def bottle(name,x,y,z,kind=0,col='ENV_Props',interactive=False):
    r=empty(name,(x,y,z),col)
    if interactive:inter(r,'Bottle')
    ma=[amber,bgreen,clear][kind%3]
    prof=[(0,.027),(.012,.031),(.16,.031),(.178,.028),(.20,.013),(.265,.012),(.273,.015),(.282,.015)]
    lathe(name+'_Glass',(x,y,z),prof,ma,col,20,r)
    cyl(name+'_Cap',(x,y,z+.283),.015,.012,[gold,black,brass][kind%3],col,r,16)
    lathe(name+'_PaperLabel',(x,y,z),[(.057,.0315),(.127,.0315)],paper if kind%2 else gold,col,20,r)
    lathe(name+'_LabelBand',(x,y,z),[(.071,.0319),(.077,.0319)],burgundy,col,20,r)
    join_children(r)
    return r
prototypes=[]
for k in range(3):
    r=bottle(f'BackBar_Bottle_Prototype_{k}',6.14,1.8+k*.115,1.18,k);prototypes.append(r)
idx=3
for bay,(a,b) in enumerate([(1.68,2.73),(2.87,3.93),(4.07,5.13),(5.27,6.33),(6.47,7.23)]):
    for level,z in enumerate([1.18,1.63,2.07]):
        if bay==1 and level>=1:continue # Coors sign occupies its shown mirror bay.
        for k in range(7 if bay<4 else 5):
            yy=a+.085+k*.132
            if bay==0 and level==0 and k<3:continue
            proto=prototypes[(k+bay+level)%3]
            duplicate(proto,f'BackBar_Bottle_{idx:03}',(0,yy-proto.location.y,z-proto.location.z),'ENV_Props');idx+=1
for i in range(22):
    y=1.77+i*.245
    lathe('BackBar_Tumbler',(6.075,y,.963),[(0,.026),(.009,.028),(.107,.031),(.113,.031),(.113,.027),(.013,.024)],glass,n=20)
def tumbler(name,x,y,z,interactive=False):
    r=empty(name,(x,y,z));
    if interactive:inter(r,'Glass')
    lathe(name+'_Glass',(x,y,z),[(0,.026),(.006,.031),(.087,.035),(.093,.035),(.093,.031),(.013,.026)],glass,n=28,p=r)
    ring(name+'_Rim',(x,y,z+.093),.033,.0016,glass,p=r)
    return r
for i,(x,y) in enumerate([(4.46,2.10),(4.55,2.27),(4.47,4.45),(4.48,6.7),(1.05,1.55),(1.6,1.45)]):tumbler(f'Tumbler_{i+1:02}',x,y,1.101 if x>4 else .781,i<4)
for i,y in enumerate([4.70,6.20]):bottle(f'Counter_Bottle_{i+1}',4.65,y,1.10,i,interactive=True)
for i,y in enumerate([2.4,4.65,6.87]):
    box('Counter_NapkinHolder',(4.51,y,1.16),(.12,.095,.12),steel,'ENV_Props',.012)
    box('Counter_Napkins',(4.51,y,1.208),(.10,.078,.045),paper,'ENV_Props',.003)
for i in range(8):cyl('Coaster_Stack',(4.68,3.16,1.104+i*.0025),.047,.002,paper)
for y in [2.10,4.45,6.7]:cyl('Counter_Coaster',(4.46,y,1.101),.055,.0015,burgundy)
# Vintage register, keypad, paper roll and display. Separate interactive root.
reg=empty('Cash_Register',(6.05,3.75,.95));inter(reg,'CashRegister')
box('Register_CashDrawer',(6.055,3.75,1.015),(.36,.41,.13),black,p=reg,bevel=.012)
box('Register_Base',(6.055,3.75,1.105),(.35,.39,.07),cream,p=reg,bevel=.015)
# Wedge slopes toward the bartender on the west side.
verts=[(-.175,-.185,0),(-.175,.185,0),(.175,-.185,0),(.175,.185,0),(-.175,-.185,.065),(-.175,.185,.065),(.175,-.185,.255),(.175,.185,.255)]
mesh('Register_Wedge',verts,[(0,2,3,1),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5),(4,5,7,6)],cream,loc=(6.055,3.75,1.14),p=reg,bevel=.009)
for row in range(6):
    for col in range(7):
        xx=5.925+row*.035;yy=3.68+col*.031;zz=1.213+(xx-5.88)*.543
        o=box('Register_Key',(xx,yy,zz),(.028,.025,.012),paper if col<5 else black,bevel=.003,p=reg);o.rotation_euler.y=-.497
box('Register_DisplayCase',(6.16,3.75,1.39),(.055,.28,.085),cream,bevel=.008,p=reg)
box('Register_Display',(6.128,3.75,1.39),(.007,.23,.055),screen,bevel=.002,p=reg)
label('Register_Total','42.75',(6.122,3.75,1.39),.040,digit,(pi/2,0,-pi/2),p=reg)
box('Register_PaperSlot',(6.03,3.60,1.31),(.13,.09,.018),black,bevel=.004,p=reg)
box('Register_Receipt',(5.98,3.60,1.323),(.12,.067,.005),paper,bevel=.001,p=reg)
# Small family frame and chess set beside register, never large themed decor.
box('FamilyPhoto_Frame',(6.13,4.12,1.055),(.028,.14,.19),wal,bevel=.006)
decal('FamilyPhoto_Image',(6.11,4.12,1.055),.113,.163,artm['FamilyPhoto'],(pi/2,0,-pi/2))
boardx=6.16;boardy=4.35
box('MiniChess_Board',(boardx,boardy,.966),(.19,.19,.017),top,bevel=.004)
for ix in range(8):
    for iy in range(8):
        box('MiniChess_Square',(boardx+(ix-3.5)*.021,boardy+(iy-3.5)*.021,.976),(.021,.021,.001),paper if (ix+iy)%2 else wal,bevel=0)
for ix in range(8):
    for iy in [0,1,6,7]:
        h=.027 if iy in [1,6] else [.035,.04,.045,.053,.05,.045,.04,.035][ix]
        lathe('MiniChess_Piece',(boardx+(ix-3.5)*.021,boardy+(iy-3.5)*.021,.977),[(0,.007),(.004,.007),(.008,.004),(h-.007,.003),(h-.004,.005),(h,.001)],paper if iy<2 else wal,n=12)
cyl('Soviet_Coin',(5.99,3.43,.956),.012,.0015,brass,n=32)
label('Coin_Denomination','1',(5.99,3.43,.957),.009,wal,rot=(0,0,0))
label('Coin_Year','1981',(5.99,3.426,.957),.0022,wal,rot=(0,0,0))
cyl('Straw_Container',(4.68,3.00,1.156),.034,.11,black)
for i in range(23):
    a=i*2.399;r=.024*math.sqrt((i+.5)/23);xx=4.68+r*cos(a);yy=3+r*sin(a)
    rod('RedStraw',(xx,yy,1.17),(xx+random.uniform(-.015,.015),yy+random.uniform(-.015,.015),1.35+random.uniform(-.016,.016)),.0016,straw)
# Three brass beer taps grouped together as in the reference.
taps=empty('Beer_Taps',(4.67,5.90,1.10));inter(taps,'BeerTaps')
box('Beer_DripTray',(4.55,5.90,1.105),(.19,.43,.011),steel,bevel=.008,p=taps)
for j in range(9):box('Beer_TraySlot',(4.55,5.735+j*.041,1.112),(.15,.008,.003),black,bevel=.001,p=taps)
for i in [-1,0,1]:
    y=5.9+i*.115
    cyl('Beer_TapBase',(4.69,y,1.125),.039,.05,brass,p=taps)
    rod('Beer_Column',(4.69,y,1.15),(4.69,y,1.43),.019,brass,p=taps)
    tube('Beer_Spout',[(4.69,y,1.39),(4.60,y,1.39),(4.56,y,1.35)],.012,brass,p=taps)
    lathe('Beer_CeramicHandle',(4.69,y,1.44),[(0,.012),(.025,.014),(.05,.026),(.09,.029),(.12,.013)],paper,p=taps)

# Jukebox, west wall phone and small CRT are the only entertainment equipment.
juke=empty('Jukebox',(.24,1.72,0),'ENV_Furniture');inter(juke,'Jukebox')
outline=[(-.35,0),(.35,0),(.35,1.11)]+[(.35*cos(a),1.11+.35*sin(a)) for a in [i*pi/16 for i in range(1,17)]]
vv=[(xx,1.72+yy,zz+.03) for xx in [.025,.45] for yy,zz in outline];nn=len(outline)
ff=[tuple(reversed(range(nn))),tuple(range(nn,2*nn))]+[(i,(i+1)%nn,(i+1)%nn+nn,i+nn) for i in range(nn)]
mesh('Jukebox_Cabinet',vv,ff,wal,'ENV_Furniture',p=juke,bevel=.014)
box('Jukebox_Face',(.461,1.72,.75),(.025,.60,1.05),black,'ENV_Furniture',.035,juke)
for r in [.285,.32,.347]:
    pts=[(.49,1.72-r,.12),(.49,1.72-r,1.12)]+[(.49,1.72+r*cos(a),1.12+r*sin(a)) for a in [pi-i*pi/24 for i in range(25)]]+[(.49,1.72+r,.12)]
    tube('Jukebox_ArchTrim',pts,.012,brass,'ENV_Furniture',juke)
box('Jukebox_Grille',(.49,1.72,.44),(.025,.47,.58),gold,'ENV_Furniture',.025,juke)
for i in range(20):rod('Jukebox_GrilleSlat',(.507,1.50+i*.023,.17),(.507,1.50+i*.023,.70),.004,wal,'ENV_Furniture',juke)
box('Jukebox_RedNameplate',(.52,1.72,.80),(.025,.39,.09),straw,'ENV_Furniture',.007,juke)
for i in range(5):
    for j in range(2):box('Jukebox_SelectionCard',(.509,1.51+i*.105,.925+j*.10),(.01,.083,.073),paper,'ENV_Furniture',.003,juke)
for i in range(10):box('Jukebox_Button',(.52,1.50+i*.046,.858),(.02,.023,.018),cream,'ENV_Furniture',.004,juke)
join_children(juke);coll('Jukebox',(.26,1.72,.75),(.52,.74,1.5),'Jukebox')
phone=empty('Wall_Phone',(.10,.70,1.38));inter(phone,'Telephone')
box('Phone_Box',(.11,.70,1.38),(.17,.25,.46),black,bevel=.015,p=phone)
box('Phone_Front',(.205,.70,1.38),(.025,.21,.39),steel,bevel=.007,p=phone)
for yy in [.63,.77]:box('Phone_CoinSlot',(.222,yy,1.53),(.015,.046,.01),black,bevel=.001,p=phone)
for i in range(3):
    for j in range(4):box('Phone_Key',(.225,.66+i*.036,1.42-j*.035),(.014,.027,.024),black,bevel=.003,p=phone)
tube('Phone_Handset',[(.24,.53,1.28),(.25,.52,1.32),(.24,.52,1.51),(.24,.53,1.55)],.034,black,p=phone)
pts=[(.22,.53+.012*cos(i*.55),1.27-i*.003) for i in range(130)]
pts.extend([(.22,.53, .88),(.22,.60,.83),(.22,.71,.88),(.22,.73,1.12)])
tube('Phone_CoiledCord',pts,.003,black,p=phone)
join_children(phone)
tv=empty('Small_CRT',(6.12,7.50,2.17),'ENV_Props')
box('CRT_Case',(6.18,7.50,2.18),(.32,.40,.34),black,bevel=.045,p=tv)
box('CRT_Screen',(6.008,7.50,2.20),(.025,.32,.24),screen,bevel=.045,p=tv)
for y in [7.36,7.40]:cyl('CRT_Knob',(5.99,y,2.04),.008,.01,cream,p=tv)
box('CRT_Bracket',(6.22,7.5,1.97),(.33,.30,.025),black,p=tv)

# Framed images are sampled from the supplied collage, not new artwork.
def artwork(name,x,y,z,w,h,texture,wall):
    if wall=='W':
        box(name+'_Frame',(x,y,z),(.055,w,h),wal,bevel=.008)
        box(name+'_Mat',(x+.031,y,z),(.008,w-.04,h-.04),paper,bevel=.001)
        decal(name+'_Image',(x+.039,y,z),w-.09,h-.09,artm[texture],(pi/2,0,pi/2))
    else:
        box(name+'_Frame',(x,y,z),(w,.055,h),wal,bevel=.008)
        box(name+'_Mat',(x,y-.031,z),(w-.04,.008,h-.04),paper,bevel=.001)
        decal(name+'_Image',(x,y-.039,z),w-.09,h-.09,artm[texture])
artwork('North_Picture',3.25,7.945,1.76,.75,.56,'Art_North','N')
for i,(y,z,w,h,tex) in enumerate([(.45,2.05,.52,.63,'Art_01'),(1.46,2.0,.43,.77,'Art_02'),(3.33,2.10,.36,.53,'Art_02'),(4.12,2.03,.35,.43,'Art_01'),(4.82,1.98,.30,.40,'Art_03'),(5.53,1.98,.36,.43,'Art_01'),(6.24,1.95,.34,.44,'Art_03')]):artwork(f'West_Picture_{i+1}',.08,y,z,w,h,tex,'W')

# Neon signs: two Budweiser signs are visible (views 01 and 03), one Coors.
brush=bpy.data.fonts.load('/System/Library/Fonts/Supplemental/Brush Script.ttf')
def neon_sign(name,origin,wall,brand):
    x,y,z=origin
    if wall=='W':
        rot=(pi/2,0,pi/2); u=Vector((0,1,0));normal=Vector((1,0,0))
    elif wall=='E':
        rot=(pi/2,0,-pi/2);u=Vector((0,-1,0));normal=Vector((-1,0,0))
    else:
        rot=(-pi/2,0,pi);u=Vector((-1,0,0));normal=Vector((0,1,0))
    c=Vector(origin)
    if brand=='Budweiser':
        label(name+'_RedScript','Budweiser',c,.225,red,rot,'ENV_Neon',brush)
        shape=[(-.34,-.19),(-.20,-.19),(-.14,-.29),(-.05,-.19),(.15,-.19),(.22,-.12),(.35,-.12),(.35,.17),(.23,.17),(.18,.28),(.08,.28),(.03,.17),(-.14,.17),(-.18,.10),(-.34,.10)]
        tube(name+'_GreenOutline',[c+u*a+Vector((0,0,b))-normal*.005 for a,b in shape],.007,neongreen,'ENV_Neon',closed=True)
    else:
        label(name+'_RedScript','Coors',c+Vector((0,0,.12)),.29,red,rot,'ENV_Neon',brush)
        label(name+'_GreenText','BEER',c+Vector((0,0,-.16)),.18,neongreen,rot,'ENV_Neon')
neon_sign('Budweiser_West',(.14,2.65,1.98),'W','Budweiser')
neon_sign('Budweiser_South',(4.02,.12,1.70),'S','Budweiser')
neon_sign('Coors_BackBar',(6.01,3.40,1.96),'E','Coors')

# Three brass pendants, two ceiling fixtures and the shown wall sconces.
for i,y in enumerate([2.12,4.475,6.83]):
    r=empty(f'Bar_Pendant_{i+1}',(4.57,y,0),'ENV_Lighting')
    cyl('Pendant_Canopy',(4.57,y,2.66),.065,.055,brass,'ENV_Lighting',r)
    rod('Pendant_Stem',(4.57,y,2.64),(4.57,y,2.15),.009,brass,'ENV_Lighting',r)
    lathe('Pendant_Shade',(4.57,y,0),[(1.99,.235),(2.018,.235),(2.05,.21),(2.11,.16),(2.16,.09),(2.19,.045),(2.23,.025)],brass,'ENV_Lighting',64,r)
    cyl('Pendant_Diffuser',(4.57,y,2.008),.215,.008,white,'ENV_Lighting',r,48)
    light(f'Pendant_Light_{i+1}',(4.57,y,1.98),75,(1,.67,.34),'AREA',.39,(4.57,y,0))
for i,y in enumerate([2.10,5.95]):
    cyl('CeilingFixture_Base',(2.70,y,2.64),.12,.08,brass,'ENV_Lighting')
    lathe('CeilingFixture_Glass',(2.70,y,0),[(2.52,.065),(2.535,.13),(2.58,.15),(2.61,.13)],white,'ENV_Lighting',40)
    light(f'Ceiling_Light_{i+1}',(2.70,y,2.49),95,(1,.71,.42),'POINT',.14)
def sconce(name,wall,v):
    if wall=='W':
        x=.22;y=v;box(name+'_Plate',(.09,y,1.78),(.04,.11,.20),brass,'ENV_Lighting')
        rod(name+'_Arm',(.11,y,1.76),(.28,y,1.76),.014,brass,'ENV_Lighting')
    else:
        x=v;y=7.73;box(name+'_Plate',(x,7.94,1.80),(.11,.04,.20),brass,'ENV_Lighting')
        rod(name+'_Arm',(x,7.92,1.76),(x,y,1.76),.014,brass,'ENV_Lighting')
    lathe(name+'_Shade',(x,y,0),[(1.79,.10),(1.92,.057),(1.95,.043)],white,'ENV_Lighting',32)
    light(name+'_Light',(x,y,1.83),28,(1,.64,.30),'POINT',.09)
for i,y in enumerate([4.75,6.55]):sconce(f'West_Sconce_{i+1}','W',y)
for i,x in enumerate([.40,4.56]):sconce(f'North_Sconce_{i+1}','N',x)
light('Exit_RedBounce',(1.125,.23,2.42),2,(1,.025,.012),'POINT',.11)
light('Budweiser_West_RedBounce',(.24,2.65,1.96),4,(1,.025,.01),'POINT',.24)
light('Budweiser_South_GreenBounce',(4.02,.24,1.70),3,(.06,1,.14),'POINT',.25)
light('Coors_Glow',(5.91,3.40,1.95),3,(.3,1,.15),'POINT',.22)
light('Staff_Ceiling',(5.5,9.13,2.52),45,(1,.78,.49),'AREA',.32,(5.5,9.13,0))

# Non-rendering technical markers; no people or gameplay UI geometry.
spawns={
 'Bartender_Spawn':(5.46,3.80,0),'Bartender_Interaction_Point':(5.45,3.80,0),
 'Player_Bar_Interaction_Point':(3.25,3.80,0),'NPC_01_Spawn':(1.72,4.0,0),
 'NPC_02_Spawn':(2.55,1.95,0),'Player_Start':(1.12,.48,0),
 'LookAt_Bartender':(5.46,3.80,1.62),'LookAt_Player':(3.25,3.80,1.62),
 'Camera_Target_Bar':(4.70,4.45,1.20),'Camera_Target_North':(3.2,7.95,1.4),
 'Camera_Target_Guest':(1.60,4.00,1.5)}
for n,loc in spawns.items():empty(n,loc,'TECH_SpawnPoints')
for n,loc,sz in [('Guest',(1.975,4,.01),(3.95,8,.01)),('Bartender',(5.45,4.75,.01),(1.10,6.50,.01)),('Staff',(5.48,9.10,.01),(1.10,1.80,.01))]:
    o=empty('NAV_'+n,loc,'TECH_Navigation');o['bounds']=sz;o['agent_radius']=.23 if n=='Guest' else .22
def camera(name,loc,target,lens):
    d=bpy.data.cameras.new(name);d.lens=lens;d.clip_start=.04;d.clip_end=40
    o=bpy.data.objects.new(name,d);link(o,'TECH_Cinematics');o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();o['target']=target;return o
cams=[camera('Camera_01_Entry',(1.12,.38,1.62),(3.75,5.35,1.30),20),
camera('Camera_02_Bar',(1.06,4.47,1.53),(6.24,4.47,1.46),20),
camera('Camera_03_Reverse',(3.10,7.42,1.60),(3.05,.10,1.40),20),
camera('Camera_04_North',(3.10,4.55,1.59),(3.14,8.0,1.43),21),
camera('Camera_Dialogue_Player',(3.10,3.80,1.64),(5.46,3.80,1.59),48),
camera('Camera_Dialogue_Bartender',(5.48,3.80,1.64),(3.25,3.80,1.59),45),
camera('Camera_Bartender_Profile',(5.45,6.50,1.65),(5.45,3.80,1.52),55),
camera('Camera_Guest_Wide',(3.20,.68,1.65),(1.4,5.20,1.25),25)]
S.camera=cams[0]
for a in bpy.context.screen.areas:
    if a.type=='VIEW_3D':a.spaces.active.region_3d.view_perspective='CAMERA'
S['reference']='Reference_Collage.png';S['coordinate_system']='X east; Y north; Z up; metres'
S['reference_conflict']='View02 east StaffOnly conflicts with FloorPlan05 and View04 north; use north. View01 duplicate restroom lettering corrected by View04.'
S['interior_dimensions_m']=[6.4,10,2.7]
S['bar_clear_aisle_m']=1.1;S['bar_height_m']=1.1;S['bar_depth_m']=.65
# Convert lettering to meshes so FBX never depends on fonts.
for o in list(S.objects):
    if o.type=='FONT':
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH')
        if not o.data.uv_layers:uvmesh(o.data)
# Bake object rotations into vertices without changing hinges or shared stool meshes.
for o in S.objects:
    if o.type=='MESH' and (sum(abs(a) for a in o.rotation_euler)>.00001 or o.rotation_mode=='QUATERNION'):
        rot=o.rotation_quaternion.to_matrix().to_4x4() if o.rotation_mode=='QUATERNION' else o.rotation_euler.to_matrix().to_4x4()
        if o.data.users>1:o.data=o.data.copy()
        o.data.transform(rot);o.rotation_mode='XYZ';o.rotation_euler=(0,0,0)
bpy.context.view_layer.update()
manifest={
 'version':1,'units':'metres','coordinates':'Blender X=east,Y=north,Z=up; Unity X=east,Y=up,Z=north',
 'dimensions':{'width':6.4,'depth':10,'height':2.7,'publicDepth':8,'rearDepth':2,'barHeight':1.1,'barDepth':.65,'staffAisle':1.1,'entryWidth':.95,'stoolDiameter':.36,'stoolHeight':.76,'footRailDiameter':.038},
 'colliders':collisions,'interactives':interactives,'markers':[{'name':n,'position':list(v)} for n,v in spawns.items()],
 'cameras':[{'name':o.name,'position':list(o.location),'target':list(o['target']),'lens':o.data.lens} for o in cams],
 'lights':[{'name':o.name,'position':list(o.location),'rotation':list(o.rotation_euler),'type':o.data.type,'energy':o.data.energy,'color':list(o.data.color),'size':getattr(o.data,'size',.1)} for o in lights],
 'materials':[{'name':m.name,'color':list(m.diffuse_color),'texture':m.get('texture_set',''),'metallic':next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Metallic'].default_value,'roughness':next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Roughness'].default_value,'emission':list(next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Emission Color'].default_value),'emissionStrength':next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Emission Strength'].default_value} for m in M.values()]
}
with open(ROOT+'/scene_manifest.json','w') as f:json.dump(manifest,f,indent=2)
for im in bpy.data.images:
    if im.source=='FILE' and im.filepath:im.pack()
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Blender/Bar1999.blend')
print('BUILD COMPLETE',len(S.objects),'objects',sum(len(o.data.polygons) for o in S.objects if o.type=='MESH'),'polygons')
