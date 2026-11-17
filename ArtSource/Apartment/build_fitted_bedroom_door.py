"""Create the measured bedroom door, its finish and window projection in Blender."""
from pathlib import Path
import bpy
import bmesh
import numpy as np

root = Path(r"C:/Users/kliml/CarXCase")
output = root / "Assets/_Project/ArtDirection/ApartmentWood"
window = bpy.context.window
previous = window.scene
scene = bpy.data.scenes.new("Bedroom door workshop")
window.scene = scene
collection = bpy.data.collections.new("Bedroom door | fitted joinery")
scene.collection.children.link(collection)

def material(name, color, roughness, metal=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metal
    return mat

def add_obj(obj, name, surface):
    obj.name = name
    obj.data.materials.append(surface)
    for other in list(obj.users_collection):
        other.objects.unlink(obj)
    collection.objects.link(obj)

def timber(name, at, size, finish, radius=.004, horizontal=False, offset=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at)
    obj = bpy.context.object
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    uvs = obj.data.uv_layers.active.data
    for face in obj.data.polygons:
        for i in face.loop_indices:
            v = obj.data.vertices[obj.data.loops[i].vertex_index].co
            if abs(face.normal.y) > .5:
                a, b = (v.x / .85 + .5, v.z / 1.38 + .5)
                uvs[i].uv = (b + offset, a) if horizontal else (a + offset, b)
    add_obj(obj, name, finish)
    bevel = obj.modifiers.new("Rounded edges", "BEVEL")
    bevel.width = radius
    bevel.segments = 3
    obj.modifiers.new("Weighted normals", "WEIGHTED_NORMAL")

def cylinder(name, at, radius, depth, finish, plate=False):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=radius, depth=depth,
                                        location=at, rotation=(np.pi/2, 0, 0))
    obj = bpy.context.object
    if plate:
        obj.scale.y = 2.35
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    add_obj(obj, name, finish)
    bevel = obj.modifiers.new("Soft metal edge", "BEVEL")
    bevel.width = .002
    bevel.segments = 3
    obj.modifiers.new("Weighted normals", "WEIGHTED_NORMAL")

def inset_panel(width, height, z, finish):
    # Contiguous moulding rings avoid thin overlapping strips at the panel edge.
    profile=[(.039,-.016),(.018,-.021),(.004,-.018),(0,-.010),
             (0,.010),(.004,.018),(.018,.021),(.039,.016)]
    vertices=[]
    for inset, y in profile:
        x, half_z = width*.5-inset, height*.5-inset
        vertices.extend([(-x,y,-half_z),(x,y,-half_z),(x,y,half_z),(-x,y,half_z)])
    faces=[(3,2,1,0)]
    for i in range(len(profile)-1):
        for j in range(4):
            a,b=4*i+j,4*i+(j+1)%4
            faces.append((a,b,b+4,a+4))
    faces.append(tuple(range(len(vertices)-4,len(vertices))))
    mesh=bpy.data.meshes.new("Continuous raised-panel profile")
    mesh.from_pydata(vertices,[],faces)
    uv=mesh.uv_layers.new(name="Oak grain")
    for loop in mesh.loops:
        v=mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv=(v.x/.85+.5+z*.17,v.z/1.38+.5)
    bm=bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj=bpy.data.objects.new("Inset oak panel",mesh)
    obj.location.z=z
    collection.objects.link(obj)
    obj.data.materials.append(finish)

try:
    oak = material("Quiet matte oak", (.26,.19,.135), .67)
    brass = material("Patinated brass", (.29,.21,.13), .48, .68)
    lacquer = material("Latch recess", (.06,.043,.031), .64, .20)

    # Clear opening is 1.021 x 2.392 m in Unity.
    width, height, depth = 1.009, 2.372, .047
    stile = .121
    opening = width - 2*stile
    timber("Solid core", (0,0,0),(width-.004,.024,height-.004),oak,.002)
    for sign in [-1,1]:
        timber("Long stile",(sign*(width-stile)/2,0,0),(stile,depth,height),oak,.004,offset=.17*sign)
    for z,h in [(-1.109,.154),(-.133,.133),(1.123,.126)]:
        timber("Cross rail",(0,0,z),(opening,depth,h),oak,.003,True,.19+z*.11)
    for z,h in [(-.594,.878),(.499,1.139)]:
        inset_panel(opening-.001,h,z,oak)
    for sign in [-1,1]:
        cylinder("Brass escutcheon",(-.401,sign*.025,-.124),.024,.005,brass,True)
        cylinder("Knob spindle",(-.401,sign*.039,-.101),.013,.029,brass)
        cylinder("Keyhole",(-.401,sign*.028,-.151),.004,.0016,lacquer)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16,location=(-.401,sign*.054,-.101))
        obj = bpy.context.object
        obj.scale = (.027,.017,.027)
        add_obj(obj,"Knob",brass)
        for face in obj.data.polygons:
            face.use_smooth = True
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=str(output/"Bedroom_Door_Fitted.glb"),export_format="GLB",use_selection=True)
    bpy.data.libraries.write(str(root/"ArtSource/Apartment/Bedroom_Door_Fitted.blend"),{scene},compress=True)
finally:
    window.scene = previous
    bpy.data.scenes.remove(scene)

def png(name,data):
    size = data.shape[0]
    img = bpy.data.images.new(name,width=size,height=size,alpha=True)
    img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(data.astype("float32").ravel())
    img.filepath_raw = str(output/(name+".png"))
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)

# Retain the irregular growth pattern of the apartment's original oak texture.
reference = bpy.data.images.load(str(root/"Assets/_Project/Arts/Apartaments/Textures/OldWalnut_BaseColor.png"))
reference.colorspace_settings.name = "Non-Color"
n = reference.size[0]
buf = np.empty(n*n*4,dtype=np.float32)
reference.pixels.foreach_get(buf)
grain = buf.reshape(n,n,4)[:,:,0]
grain = np.clip((grain - grain.mean()) / max(.02,grain.std()),-2.7,2.7)
color = np.array([.485,.374,.280])[None,None,:] + .017*grain[:,:,None]
png("Quiet_Oak_Albedo",np.dstack([color,np.ones((n,n))]))
grain_height = .5 + .032 * grain
dx = (np.roll(grain_height,-1,axis=1)-np.roll(grain_height,1,axis=1))*.82
dy = (np.roll(grain_height,-1,axis=0)-np.roll(grain_height,1,axis=0))*.82
norm = np.stack([-dx,-dy,np.ones_like(dx)],axis=-1)
norm /= np.linalg.norm(norm,axis=-1,keepdims=True)
png("Quiet_Oak_Normal",np.dstack([norm*.5+.5,np.ones((n,n))]))
bpy.data.images.remove(reference)

# Soft bands from the Venetian blinds, with a broad central sash and a faded edge.
n = 512
x,y=np.meshgrid(np.linspace(-1,1,n),np.linspace(-1,1,n))
def smooth(a,b,v):
    t=np.clip((v-a)/(b-a),0,1)
    return t*t*(3-2*t)
slats=.28+.72*smooth(-.48,.38,np.sin(2*np.pi*(4.35*y+.035*x)))
edge=(1-smooth(.65,.91,np.abs(x)))*(1-smooth(.59,.89,np.abs(y)))
sash=.31+.69*smooth(.020,.067,np.abs(y+.055))
mask=slats*edge*sash
png("Bedroom_Blinds_Projection",np.dstack([mask,mask,mask,mask]))
print("Fitted bedroom door and finish exported")
