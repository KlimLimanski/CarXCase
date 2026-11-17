"""Write five independent files with packed textures and local origins."""
import bpy
import json
from pathlib import Path

out=Path('C:/Users/Мария/Documents/ChatGPT/carX/Street_Buildings')
main=bpy.context.scene
roots=bpy.app.driver_namespace['street_roots']
report=[]
for root in roots:
    asset=bpy.data.scenes.new(root.name+'_Asset')
    asset.unit_settings.system=main.unit_settings.system
    asset.unit_settings.scale_length=1
    asset.world=main.world
    local_root=root.copy()
    asset.collection.objects.link(local_root)
    local_root.location=(0,0,0)
    children=[]
    for obj in root.children:
        copy=obj.copy()
        copy.data=obj.data.copy()
        asset.collection.objects.link(copy)
        copy.parent=local_root
        children.append(copy)
    bpy.context.window.scene=asset
    bpy.context.view_layer.update()
    triangles=sum(len(p.vertices)-2 for o in children for p in o.data.polygons)
    vertices=sum(len(o.data.vertices) for o in children)
    bpy.data.libraries.write(str(out/(root.name+'.blend')),{asset},fake_user=True,compress=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in children:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=children[0]
    bpy.ops.object.join()
    joined=bpy.context.object
    joined.name=root.name+'_Geometry'
    local_root.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(out/(root.name+'.glb')),export_format='GLB',
                              use_selection=True,use_active_scene=True,export_apply=True,export_tangents=True,
                              export_extras=True,export_animations=False)
    report.append({'name':root.name,'width_m':root['width_m'],'depth_m':root['depth_m'],
                   'height_m':root['height_m'],'triangles':triangles,'materials':len(joined.data.materials),
                   'front':'-Y (Blender)','detailed_side':'+X','origin':'front centre at ground level',
                   'textures_embedded':True})
    bpy.context.window.scene=main
    for obj in list(asset.objects):
        bpy.data.objects.remove(obj,do_unlink=True)
    bpy.data.scenes.remove(asset)
    print('EXPORTED',root.name,triangles,flush=True)
(out/'Asset_Specifications.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('EXPORT COMPLETE')
