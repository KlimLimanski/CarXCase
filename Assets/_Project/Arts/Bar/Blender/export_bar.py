import bpy, os, json, math
from mathutils import Vector, Matrix
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
s=bpy.data.scenes['Bar1999_Reference'];bpy.context.window.scene=s
# Preserve collection hierarchy in FBX as named empty roots.
for c in s.collection.children:
    if c.name.startswith('ENV_') or c.name in ['TECH_SpawnPoints','TECH_Cinematics']:
        root=next((o for o in c.objects if o.name==c.name and o.type=='EMPTY'),None)
        if not root:
            root=bpy.data.objects.new(c.name,None);c.objects.link(root)
        for o in list(c.objects):
            if o!=root and not o.parent:o.parent=root
bpy.context.view_layer.update()
for o in s.objects:
    if o.parent:
        w=o.matrix_world.copy();o.matrix_parent_inverse=Matrix.Identity(4);o.matrix_world=w
bpy.context.view_layer.update()
manifest=json.load(open(ROOT+'/scene_manifest.json'))
manifest['meshBounds']=[]
for o in s.objects:
    if o.type=='MESH':
        points=[o.matrix_world@Vector(v) for v in o.bound_box]
        lo=Vector(tuple(min(v[a] for v in points) for a in range(3)))
        hi=Vector(tuple(max(v[a] for v in points) for a in range(3)))
        manifest['meshBounds'].append(dict(name=o.name,center=list((lo+hi)*.5),size=list(hi-lo)))
for p in [ROOT+'/scene_manifest.json',ROOT+'/UnityProject/Assets/Bar1999/scene_manifest.json']:
    with open(p,'w') as f:json.dump(manifest,f,indent=2)
bpy.ops.object.select_all(action='DESELECT')
for o in s.objects:
    if o.type in ['MESH','EMPTY'] and not o.name.startswith(('COL_','NAV_')):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=ROOT+'/UnityProject/Assets/Bar1999/Models/Bar1999.fbx',use_selection=True,global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_space_transform=False,object_types={'MESH','EMPTY'},use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,use_tspace=True,bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='RELATIVE',embed_textures=False)
s.render.resolution_x=1440;s.render.resolution_y=642;s.cycles.samples=64
p=bpy.context.preferences.addons['cycles'].preferences
p.compute_device_type='METAL';p.refresh_devices()
for d in p.devices:d.use=d.type=='METAL'
s.cycles.device='GPU'
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Blender/Bar1999.blend')
report={'objects':len(s.objects),'mesh_objects':sum(o.type=='MESH' for o in s.objects),'unique_meshes':len({o.data for o in s.objects if o.type=='MESH'}),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in s.objects if o.type=='MESH'),'unapplied_scales':[o.name for o in s.objects if o.type=='MESH' and any(abs(a-1)>1e-5 for a in o.scale)],'missing_uv2':[o.name for o in s.objects if o.type=='MESH' and len(o.data.uv_layers)<2],'modifiers':[o.name for o in s.objects if o.modifiers],'bar_top_dimensions':list(bpy.data.objects['Bar_PolishedTop'].dimensions),'file':ROOT+'/Blender/Bar1999.blend'}
with open(ROOT+'/Blender_QA.json','w') as f:json.dump(report,f,indent=2)
print('Export complete',report)
