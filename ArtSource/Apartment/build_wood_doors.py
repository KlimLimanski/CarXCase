from pathlib import Path
import bpy
import numpy as np
root=Path(__file__).resolve().parents[2]
out=root/'Assets/_Project/ArtDirection/ApartmentWood'
out.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
def mat(name,color,metal=0):
    m=bpy.data.materials.new(name)
    m.diffuse_color=(*color,1)
    m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal
    return m
frame=mat('Frame_Walnut',(.27,.14,.065))
panel=mat('Panel_Walnut',(.31,.16,.076))
brass=mat('Tarnished_Brass',(.34,.20,.075),.65)
def box(name,pos,size,material,b=.005,horizontal=False):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    o=bpy.context.object
    o.name=name
    o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    uv=o.data.uv_layers.active.data
    for face in o.data.polygons:
        for i in face.loop_indices:
            v=o.data.vertices[o.data.loops[i].vertex_index].co
            if abs(face.normal.y)>.5:
                u=(v.x/size[0]+.5)
                w=(v.z/size[2]+.5)
                uv[i].uv=(w,u) if horizontal else (u,w)
    o.data.materials.append(material)
    m=o.modifiers.new('Eased joinery','BEVEL');m.width=b;m.segments=3
    o.modifiers.new('Weighted corners','WEIGHTED_NORMAL')
def knob(name,pos,scale):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,location=pos)
    o=bpy.context.object
    o.name=name
    o.scale=scale
    o.data.materials.append(brass)
    for p in o.data.polygons:p.use_smooth=True
collections=[]
for name,room in [('Apartment_Room_Leaf',True),('Apartment_Cabinet_Leaf',False)]:
    box('Solid core',(0,0,0),(.993,.025,.993),panel,.002)
    for x in [-.436,.436]:box('Stile',(x,0,0),(.128,.064,1),frame)
    for z,h in ([(-.45,.10),(0,.084),(.45,.10)] if room else [(-.438,.124),(.438,.124)]):
        box('Rail',(0,0,z),(.744,.064,h),frame,horizontal=True)
    for z,h in ([(-.222,.365),(.222,.365)] if room else [(0,.752)]):
        box('Inset veneer',(0,0,z),(.744,.038,h),panel)
        for side in [-1,1]:
            box('Vertical bead',(side*.354,0,z),(.032,.055,h),frame,.007)
            box('Cross bead',(0,0,z+side*(h/2-.015)),(.685,.055,.030),frame,.007,True)
    for sign in ([-1,1] if room else [-1]):
        x=-.385 if room else -.33
        z=-.044 if room else .31
        knob('Escutcheon',(x,sign*.033,z),(.035,.010,.022 if room else .031))
        knob('Knob',(x,sign*.052,z),(.025,.023,.014 if room else .025))
    objs=list(scene.objects)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.gltf(filepath=str(out/(name+'.glb')),export_format='GLB',use_selection=True)
    coll=bpy.data.collections.new(name)
    scene.collection.children.link(coll)
    for obj in objs:
        for parent in list(obj.users_collection):parent.objects.unlink(obj)
        coll.objects.link(obj)
    scene.collection.children.unlink(coll)
    collections.append(coll)
for i,coll in enumerate(collections):
    scene.collection.children.link(coll)
    for o in coll.objects:o.location.x+=i*1.35
bpy.ops.wm.save_as_mainfile(filepath=str(root/'ArtSource/Apartment/Apartment_Wood_Doors.blend'),compress=True)
n=1024
x,y=np.meshgrid(np.arange(n)/n,np.arange(n)/n)
p=2*np.pi
u=x+.012*np.sin(p*(x+y))+.004*np.sin(p*(2*x-3*y))+.003*np.sin(p*(5*y+x))
rings=(.5+.5*np.sin(p*(24*u+.36*np.sin(p*(3*x-y)))))**6
fibres=.5+.5*np.sin(p*(163*u+.44*np.sin(p*(7*x+2*y))))
pores=(.5+.5*np.sin(p*(101*u+.72*np.sin(p*(13*x+3*y)))))**19
pores*=.3+.7*(.5+.5*np.sin(p*(18*x+8*y)))**2
figure=.5+.5*np.sin(p*(5*u+.65*np.sin(p*y)))
tone=.63+.082*figure-.058*rings-.067*pores+.020*fibres
height=.55-.042*rings-.11*pores+.012*fibres
def png(name,data,space):
    image=bpy.data.images.new(name,width=n,height=n,alpha=True)
    image.colorspace_settings.name=space
    if data.ndim==2:data=np.repeat(data[:,:,None],3,axis=2)
    image.pixels.foreach_set(np.dstack([data,np.ones((n,n))]).astype('float32').ravel())
    image.filepath_raw=str(out/(name+'.png'))
    image.file_format='PNG'
    image.save()
rgb=np.stack([tone,tone*.76,tone*.53],-1)
png('Apartment_Walnut_Albedo',np.where(rgb<.04045,rgb/12.92,((rgb+.055)/1.055)**2.4),'sRGB')
png('Apartment_Walnut_Height',height,'Non-Color')
