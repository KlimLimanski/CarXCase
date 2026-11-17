import bpy,os,math
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
s=bpy.data.scenes['Bar1999_Reference'];bpy.context.window.scene=s
meshes={o.data:o for o in s.objects if o.type=='MESH'}
done=0
for me,o in meshes.items():
    if me.get('lightmap_packed_v1'):continue
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    layer=me.uv_layers.get('LightmapUV') or me.uv_layers.new(name='LightmapUV')
    me.uv_layers.active=layer
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.012,margin_method='FRACTION',correct_aspect=True,scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    me.uv_layers.active_index=0;me.uv_layers[0].active_render=True
    me['lightmap_packed_v1']=True;done+=1
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Blender/Bar1999.blend')
print('Lightmap UV packed on',done,'unique meshes; base UV untouched.')
