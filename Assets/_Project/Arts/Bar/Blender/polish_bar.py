"""Apply final reference refinements after build_bar.py; safe to run repeatedly."""
import bpy, math, os, json
from mathutils import Vector
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
s=bpy.data.scenes['Bar1999_Reference'];bpy.context.window.scene=s
for o in s.objects:
    if o.type=='MESH' and len(o.data.uv_layers)==1:
        dst=o.data.uv_layers.new(name='LightmapUV')
        k=math.ceil(math.sqrt(max(1,len(o.data.polygons))))
        for i,p in enumerate(o.data.polygons):
            for j,l in enumerate(p.loop_indices):
                a=2*math.pi*j/len(p.loop_indices)
                dst.data[l].uv=((i%k+.5+.35*math.cos(a))/k,(i//k+.5+.35*math.sin(a))/k)
vinyl=bpy.data.materials.get('GreenVinyl')
for o in s.objects:
    if o.type=='MESH' and o.name.startswith('Booth_') and 'Bench' in o.name:
        mats=list(o.data.materials)
        if vinyl not in mats:o.data.materials.append(vinyl);mats=list(o.data.materials)
        vi=mats.index(vinyl)
        for p in o.data.polygons:
            if (o.matrix_world@p.center).z>.59 and mats[p.material_index].name.startswith('Walnut'):p.material_index=vi
for n,hingex in [('Door_Restroom',1.85),('Door_StaffOnly',6.10)]:
    r=s.objects[n]
    if not r.get('right_hinge_applied'):
        worlds={o:o.matrix_world.copy() for o in r.children}
        r.location.x=hingex;bpy.context.view_layer.update()
        for o,w in worlds.items():o.matrix_world=w
        for o in r.children:
            if '_HandlePlate' in o.name:o.location.x-=.69
            elif '_Lever' in o.name:o.location.x-=.59
            elif '_Hinge' in o.name:o.location.x+=.92
        r['right_hinge_applied']=True
    r['open_degrees']=-90
# Keep the entire service-side furniture envelope at x >= 6.00 m.
# Hardware must not steal centimetres from the specified 1.10 m clear aisle.
if not s.get('clear_aisle_refined'):
    for o in s.objects:
        if o.type!='MESH':continue
        if o.name.startswith('BackBar_FridgeGlass'):o.location.x+=.058
        elif o.name.startswith('BackBar_Refrigerator'):o.location.x+=.056
        elif o.name.startswith('Fridge_Handle'):o.location.x+=.062
        elif o.name.startswith('Fridge_Vent'):o.location.x+=.057
        elif o.name.startswith('Shelf_BrassLip'):o.location.x+=.008
        elif o.name=='BackBar_Worktop':o.location.x+=.01
        elif o.name.startswith(('BackBar_Pilaster','BackBar_ColumnCap','BackBar_Cornice','BackBar_Base')):
            desired={'BackBar_Pilaster':.26,'BackBar_ColumnCap':.24,'BackBar_Cornice':.36,'BackBar_Base':.34}
            prefix=next(p for p in desired if o.name.startswith(p))
            o.data=o.data.copy(); factor=desired[prefix]/o.dimensions.x
            for v in o.data.vertices:v.co.x*=factor
            if prefix=='BackBar_Base':o.location.x+=.03
    s.objects['Cash_Register'].location.x+=.145
    for o in s.objects:
        if o.name.startswith(('Soviet_Coin','Coin_Denomination','Coin_Year')):o.location.x+=.04
        if o.type=='MESH' and o.name.startswith('Booth_') and 'Bench' in o.name:
            o.data=o.data.copy();inv=o.matrix_world.inverted()
            for v in o.data.vertices:
                w=o.matrix_world@v.co;w.x=.79+(w.x-.79)*(1.2/1.24);v.co=inv@w
    s['clear_aisle_refined']=True
s.view_settings.exposure=.1
c=s.objects['Camera_01_Entry'];c.location=(.85,.09,1.57);c.data.lens=16.5;c['target']=(2.65,6.0,1.32)
c.rotation_euler=(Vector(c['target'])-c.location).to_track_quat('-Z','Y').to_euler()
s.objects['Camera_02_Bar'].data.lens=18.5
s.camera=c;s.render.resolution_x=1440;s.render.resolution_y=642;s.cycles.samples=64
bpy.context.view_layer.update()
manifest=json.load(open(ROOT+'/scene_manifest.json'))
for c in manifest['colliders']:
    if c['name'].startswith('Booth_') and 'Bench' in c['name']:c['size'][0]=1.2
manifest['cameras']=[dict(name=o.name,position=list(o.location),target=list(o['target']),lens=o.data.lens) for o in s.objects if o.type=='CAMERA' and 'target' in o]
for p in [ROOT+'/scene_manifest.json',ROOT+'/UnityProject/Assets/Bar1999/scene_manifest.json']:
    with open(p,'w') as f:json.dump(manifest,f,indent=2)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Blender/Bar1999.blend')
print('Final refinements applied; hinges, camera, upholstery and UV2 verified.')
