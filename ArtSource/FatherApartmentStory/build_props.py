"""Generate editable Blender sources and Unity GLBs for the apartment evidence."""
from pathlib import Path
import math
import random
import bpy
import numpy as np

PROJECT = Path(__file__).resolve().parents[2]
SOURCE = Path(__file__).parent
MODELS = PROJECT / 'Assets/_Project/Arts/Apartaments/StoryProps'
MAPS = PROJECT / 'Assets/_Project/ArtDirection/ApartmentStory/Textures'
MODELS.mkdir(parents=True, exist_ok=True)
MAPS.mkdir(parents=True, exist_ok=True)

def material(name, color, rough=.45, metal=0, emission=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = (*color, 1)
    node.inputs['Roughness'].default_value = rough
    node.inputs['Metallic'].default_value = metal
    if emission:
        node.inputs['Emission Color'].default_value = (*color, 1)
        node.inputs['Emission Strength'].default_value = emission
    return mat

def box(name, pos, dim, mat, bevel=.002, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    o = bpy.context.object
    o.name = name
    o.dimensions = dim
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = o.modifiers.new('Eased edges', 'BEVEL')
        modifier.width = bevel
        modifier.segments = 3
        o.modifiers.new('Weighted normals', 'WEIGHTED_NORMAL')
    if rotation:
        o.rotation_euler = rotation
    o.data.materials.append(mat)
    return o

def cylinder(name, pos, rad, height, mat, edges=48, rotation=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=edges, radius=rad, depth=height, location=pos)
    o = bpy.context.object
    o.name = name
    if rotation:
        o.rotation_euler = rotation
    o.data.materials.append(mat)
    for poly in o.data.polygons:
        poly.use_smooth = len(poly.vertices) == 4
    return o

def torus(name, pos, radius, wire, mat):
    bpy.ops.mesh.primitive_torus_add(major_segments=36, minor_segments=8, location=pos,
                                   major_radius=radius, minor_radius=wire)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    for poly in o.data.polygons:
        poly.use_smooth = True
    return o

def graphic(name, text, pos, width, mat, upright=False, raised=.00017, serif=False):
    bpy.ops.object.text_add(location=pos)
    o = bpy.context.object
    o.name = name
    o.data.body = text
    o.data.align_x = 'CENTER'
    o.data.align_y = 'CENTER'
    family = 'timesbd.ttf' if serif else 'arialbd.ttf'
    o.data.font = bpy.data.fonts.load(str(Path('C:/Windows/Fonts') / family), check_existing=True)
    bpy.context.view_layer.update()
    o.data.size *= width / max(.001, o.dimensions.x)
    o.data.extrude = raised
    o.data.bevel_depth = raised * .37
    o.data.resolution_u = 6
    if upright:
        o.rotation_euler.x = math.pi / 2
    o.data.materials.append(mat)
    bpy.ops.object.convert(target='MESH')
    return bpy.context.object

def face(name, points, mat):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(points, [], [list(range(len(points)))])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    return obj

brass = material('Tarnished brass', (.41,.245,.096), .35, .68)
polished = material('Worn nickel', (.64,.57,.45), .31, .73)
dark = material('Graphite bakelite', (.027,.024,.020), .53)
ivory = material('Yellowed ivory ABS', (.56,.474,.329), .43)
white = material('Old lettering ivory', (.71,.62,.44), .56)
green = material('Forest green key', (.06,.15,.105), .39)
rust = material('Oxide red paper', (.245,.058,.039), .63)
paper = material('Uncoated buff card', (.57,.44,.285), .79)
ink = material('Indigo ink', (.026,.074,.089), .59)
amber = material('Amber indicator', (.94,.216,.025), .39, emission=.25)
tape = material('Ferric tape', (.052,.026,.016), .42)
turn = (math.pi/2,0,0)

def screw(name, p, radius, upright=False, mat=polished):
    cylinder(name, p, radius, .0012, mat, 24, turn if upright else None)
    x, y, z = p
    if upright:
        box('Slotted screw cut', (x,y-.00063,z), (radius*1.46,.00022,.00057),dark,.00005)
    else:
        box('Slotted screw cut', (x,y,z+.00063), (radius*1.46,.00057,.00022),dark,.00005)

def plaque(number):
    box('Rolled brass rim',(0,0,0),(.124,.010,.069),brass,.004)
    box('Blackened inset',(0,-.005,0),(.110,.004,.054),dark,.0028)
    graphic('Raised apartment number', number,(0,-.008,0),.052,polished,True,.00065,True)
    for x in [-.048,.048]:
        screw('Brass mounting screw',(x,-.0081,0),.0041,True,brass)

def matchbox():
    box('Card sleeve',(0,0,0),(.108,.064,.022),paper,.0014)
    box('Partially withdrawn tray',(.015,0,0),(.101,.054,.017),white,.0012)
    box('Striking strip',(-.006,-.0322,0),(.091,.0013,.013),tape,.0003)
    for i in range(12):
        box('Grit fragment',(-.044+i*.0068,-.033,.0014*(i%4-1)),(.0015,.0005,.0018),dark,.0001)
    box('Indigo label border',(0,0,.0112),(.100,.055,.0007),ink,.0012)
    box('Faded label stock',(0,0,.0117),(.096,.051,.0007),paper,.0012)
    fish=[(-.038,.006),(-.020,.010),(-.008,.014),(.008,.013),(.022,.006),(.034,.012),
          (.030,.003),(.035,-.005),(.022,.000),(.005,-.003),(-.001,-.009),(-.009,-.004),
          (-.022,.000)]
    face('Printed sturgeon',[(x,y,.0121) for x,y in reversed(fish)],ink)
    cylinder('Sturgeon eye',(-.019,.005,.0124),.00105,.00045,paper,12)
    graphic('Bar name','СТЕРЛЯДЬ',(0,-.016,.0122),.076,ink,serif=True,raised=.00009)

def machine():
    box('Weighted bottom plate',(0,0,-.022),(.313,.218,.028),dark,.011)
    box('Cream ABS lower shell',(0,0,.001),(.328,.228,.043),ivory,.011)
    box('Cassette well floor',(-.053,.024,.025),(.162,.121,.007),dark,.003)
    for x in [-.137,.033]:
        box('Cassette door shoulder',(x,.024,.035),(.024,.124,.019),ivory,.006)
    box('Hinge rail',(-.053,.091,.030),(.171,.028,.027),ivory,.006)
    box('Keys fascia',(0,-.072,.026),(.311,.076,.018),ivory,.007)
    box('Speaker grille bank',(.102,.028,.037),(.091,.132,.017),ivory,.009)
    for i in range(8):
        box('Grille slot',(.072+i*.0087,.033,.046),(.003,.081,.0015),dark,.001)
    box('PLAY Key',(.072,-.072,.038),(.077,.045,.016),green,.0035)
    graphic('Play key cap','PLAY',(.077,-.072,.0464),.043,white,raised=.00022)
    face('Play embossed arrow',[(.040,-.078,.047),(.048,-.072,.047),(.040,-.066,.047)][::-1],white)
    box('Stop key',(-.009,-.072,.038),(.047,.041,.015),dark,.0034)
    graphic('Stop key cap','STOP',(-.009,-.072,.046),.032,white,raised=.0002)
    box('Smoked display',(-.099,-.114,.005),(.079,.006,.030),dark,.003)
    graphic('Message count','02',(-.096,-.1176,.006),.030,amber,True,.00013)
    cylinder('Message_LED',(-.105,-.075,.038),.0054,.008,amber,24)
    for x in [-.145,.145]:
        screw('Front fastener',(x,-.097,.034),.0027)
    for x in [-.119,.119]:
        box('Rubber foot',(x,.066,-.037),(.03,.029,.009),dark,.004)

def cassette():
    box('Textured Microcassette shell',(0,0,0),(.077,.048,.013),dark,.0028)
    box('Shaped guide ridge',(0,-.020,.001),(.070,.010,.014),dark,.0021)
    box('Cream paper cassette label',(0,.007,.0068),(.064,.025,.0009),paper,.001)
    box('Tape side band',(0,.012,.0073),(.063,.0035,.00025),rust,.0003)
    graphic('Tape side mark','A',(-.022,.006,.0074),.0049,ink,raised=.00009)
    graphic('Microcassette stock mark','MC-60',(.013,.006,.0074),.020,ink,raised=.00009)
    for x in [-.016,.016]:
        cylinder('Visible tape spool',(x,-.007,.0069),.0074,.0014,tape,32)
        torus('Spool rim',(x,-.007,.0077),.0045,.0006,white)
        for angle in range(0,360,60):
            a=math.radians(angle)
            box('Spool drive tooth',(x+math.cos(a)*.0038,-.007+math.sin(a)*.0038,.0078),
                (.0034,.0013,.0008),white,.0002,(0,0,a))
    for x in [-.033,.033]:
        screw('Shell screw',(x,-.016,.007),.002)

def keyring():
    torus('Split key ring',(0,0,.0045),.021,.0015,polished)
    for i,a in enumerate([-.96,-.44,.28]):
        dx,dy=math.sin(a),math.cos(a)
        x,y=dx*.025,dy*.025
        torus('Cut-out key bow',(x,y,.005+i*.0018),.0082,.00225,brass if i==1 else polished)
        shaft=box('Notched key blade',(x+dx*.021,y+dy*.021,.005+i*.0018),
                  (.0081,.035,.0032),polished,.0008,(0,0,-a))
        for j in range(3):
            span=.021+j*.006
            box('Worn key cut',(x+dx*span-.0042*dy,y+dy*span+.0042*dx,.005+i*.0018),
                (.0044,.0035+(j%2)*.002,.0032),polished,.0003,(0,0,-a))
    x,y=.048,-.013
    torus('Anchor shackle',(x-.015,y,.005),.005,.0015,brass)
    box('Anchor shank',(x+.006,y,.005),(.045,.007,.006),brass,.002)
    box('Anchor crosspiece',(x-.010,y,.005),(.006,.027,.006),brass,.002)
    for sign in [-1,1]:
        box('Anchor fluke',(x+.022,y+sign*.011,.005),(.010,.023,.005),brass,.002,(0,0,sign*.54))
        box('Anchor tip',(x+.016,y+sign*.021,.005),(.016,.007,.005),brass,.002,(0,0,sign*.28))

def lock():
    cylinder('Nickel cylinder collar',(0,-.002,0),.0235,.004,polished,48,turn)
    torus('Machined cylinder face ring',(0,0,0),.019,.0008,brass).rotation_euler.x=math.pi/2
    cylinder('Cylinder plug',(0,-.005,0),.017,.003,polished,40,turn)
    box('Keyway cut',(0,-.0067,0),(.0041,.0006,.020),dark,.0005)
    box('Keyway ward',(.0022,-.0068,-.003),(.0038,.0008,.006),dark,.00035)

def watch():
    box('Stitched leather strap',(0,0,.006),(.019,.152,.009),dark,.003)
    for sign in [-1,1]:
        for i in range(9):
            box('Strap stitching',(sign*.0076,-.065+i*.015,.011),(.0022,.0042,.0008),tape,.0002)
    cylinder('Nickel watch case',(0,0,.014),.021,.012,polished)
    torus('Watch bezel',(0,0,.0202),.0182,.00145,polished)
    cylinder('Ivory enamel dial',(0,0,.0201),.0176,.0012,white)
    for i in range(12):
        a=math.tau*i/12
        box('Hour index',(math.sin(a)*.0143,math.cos(a)*.0143,.0211),(.0011,.0031,.00045),
            dark,.00005,(0,0,-a))
    graphic('Pobeda wordmark','ПОБЕДА',(0,.0074,.0211),.011,dark,raised=.000065)
    box('Minute hand',(.001,-.004,.022),(.0011,.013,.0006),dark,.00015,(0,0,.33))
    box('Hour hand',(-.003,.001,.0224),(.010,.0014,.0006),dark,.00015,(0,0,-.32))
    cylinder('Winding crown',(.022,0,.014),.0036,.0045,polished,24,(0,math.pi/2,0))

def cigarette_case():
    box('Silver base',(0,0,.004),(.088,.113,.010),polished,.006)
    box('Case seam',(0,0,.008),(.085,.110,.004),dark,.005)
    box('Rounded silver lid',(0,0,.013),(.088,.113,.009),polished,.006)
    box('Latch tab',(0,-.057,.010),(.018,.008,.005),brass,.002)
    for i in range(-7,8):
        box('Engine-turned line',(i*.0044,0,.0176),(.00065,.090,.00022),brass,.00008)
    box('Blank monogram space',(0,0,.018),(.029,.019,.0013),polished,.0018)

def record():
    vinyl=material('Carbon black vinyl',(.018,.017,.015),.245)
    grooves=material('Machined record groove',(.014,.013,.012),.39)
    cylinder('12-inch vinyl', (0,0,.0012),.148,.0024,vinyl,128)
    profile=[(.051,.00268)]
    for i in range(42):
        r=.058+i*.00206
        profile.extend([(r-.00048,.00268),(r,.00252),(r+.00048,.00268)])
    profile.extend([(.1476,.00268),(.148,.0024)])
    count=128
    vertices=[(r*math.cos(math.tau*i/count),r*math.sin(math.tau*i/count),z)
              for r,z in profile for i in range(count)]
    faces=[(j*count+i, (j+1)*count+i, (j+1)*count+(i+1)%count, j*count+(i+1)%count)
           for j in range(len(profile)-1) for i in range(count)]
    mesh=bpy.data.meshes.new('Lathed vinyl surface')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    surface=bpy.data.objects.new('Continuous pressed groove surface',mesh)
    bpy.context.collection.objects.link(surface)
    mesh.materials.append(grooves)
    for poly in mesh.polygons:
        poly.use_smooth=True
    cylinder('Oxide label',(0,0,.00285),.051,.00034,rust,96)
    torus('Print perimeter',(0,0,.00306),.045,.00038,paper)
    graphic('Vinyl side','Б',(0,.019,.00311),.012,paper,serif=True,raised=.00004)
    graphic('Vinyl label','ДЖАЗ',(0,-.017,.00315),.043,paper,serif=True,raised=.000035)
    cylinder('Centre bore detail',(0,0,.0030),.0036,.00056,dark,32)

def sleeve():
    box('Scuffed cardboard sleeve',(0,0,.002),(.309,.309,.006),paper,.002)
    box('Burgundy cover plate',(0,0,.0054),(.292,.289,.0006),rust,.002)
    box('Sleeve spine',(0,.154,.003),(.304,.003,.007),white,.0005)
    box('Ink middle band',(0,-.018,.0059),(.282,.109,.00045),dark,.001)
    graphic('Sleeve cover title','ДЖАЗ',(0,.060,.0062),.156,white,raised=.0001,serif=True)
    graphic('Sleeve side note','СТОРОНА Б',(0,-.022,.0063),.139,white,raised=.0001,serif=True)
    rng=random.Random(189)
    for _ in range(32):
        a=rng.choice([-1,1])
        x=rng.uniform(-.147,.147)
        y=a*rng.uniform(.136,.148)
        box('Worn cardboard fleck',(x,y,.006),(.009*rng.random(),.002,.0003),paper,.00006)

def switch():
    box('Beveled ivory plate',(0,0,0),(.079,.015,.119),ivory,.004)
    box('Inset cradle',(0,-.008,0),(.035,.004,.063),dark,.003)
    box('Rocker',(0,-.012,0),(.031,.014,.053),ivory,.004,(.13,0,0))
    for z in [-.046,.046]:
        screw('Mounting screw',(0,-.008,z),.0038,True)

def export(name, builder):
    previous=bpy.context.window.scene
    scene=bpy.data.scenes.new(name)
    bpy.context.window.scene=scene
    try:
        builder()
        bpy.ops.export_scene.gltf(filepath=str(MODELS/(name+'.glb')),export_format='GLB',
                                  use_active_scene=True,export_apply=True)
        bpy.data.libraries.write(str(SOURCE/(name+'.blend')), {scene}, compress=True, fake_user=True)
    finally:
        bpy.context.window.scene=previous
        for obj in list(scene.objects):
            bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(scene)

def maps():
    size=768
    rng=np.random.default_rng(3296)
    x,y=np.meshgrid(np.arange(size)/size,np.arange(size)/size)
    def field(scale):
        a=rng.random((scale,scale)).astype('f')
        xx=x*scale; yy=y*scale
        ix=xx.astype(int); iy=yy.astype(int)
        fx=xx-ix; fy=yy-iy
        fx=fx*fx*(3-2*fx); fy=fy*fy*(3-2*fy)
        return ((1-fx)*(1-fy)*a[iy,ix]+fx*(1-fy)*a[iy,(ix+1)%scale]+
                (1-fx)*fy*a[(iy+1)%scale,ix]+fx*fy*a[(iy+1)%scale,(ix+1)%scale])
    def save(name, rgb):
        image=bpy.data.images.new(name,width=size,height=size,alpha=False)
        pixels=np.ones((size,size,4),dtype='f')
        pixels[:,:,:3]=np.clip(rgb,0,1)
        image.pixels.foreach_set(pixels.ravel())
        image.filepath_raw=str(MAPS/(name+'.png'))
        image.file_format='PNG'
        image.save()
        bpy.data.images.remove(image)
    for name, paint, substrate, chip_cut in [
        ('Stairwell_Plaster',(.58,.53,.45),(.38,.30,.215),.17),
        ('Stairwell_Enamel',(.291,.315,.259),(.64,.54,.423),.10),
        ('Stairwell_Terrazzo',(.325,.301,.266),(.44,.394,.34),.12),
    ]:
        broad=field(5); cloud=field(13)
        aggregates=field(132)
        dust=field(48)
        grain=rng.random((size,size))-.5
        chipped=np.clip((field(12)*.56+field(35)*.28+field(71)*.16-chip_cut-.46)*17,0,1)
        damp=np.clip((broad*.66+cloud*.34-.51)*3.6,0,1)
        if 'Terrazzo' in name:
            chipped=np.clip((aggregates-.75)*10,0,1)
            damp*=.4
        luma=1+.23*(dust-.5)+.29*(cloud-.5)-.31*damp+.12*grain
        rgb=(np.asarray(paint)[None,None,:]*(1-chipped[:,:,None])+
             np.asarray(substrate)[None,None,:]*chipped[:,:,None])*luma[:,:,None]
        save(name+'_BaseColor',rgb)
        height=.18*cloud+.08*dust+.11*aggregates+.033*grain-.19*chipped
        dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*2.7
        dy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*2.7
        n=np.stack((-dx,-dy,np.ones_like(height)),axis=2)
        save(name+'_Normal', n/np.linalg.norm(n,axis=2,keepdims=True)*.5+.5)

only_asset=globals().get('ONLY_ASSET')
for number in ('2A','2B','2C','2D','2E'):
    name='DoorPlaque_'+number
    if not only_asset or name == only_asset:
        export(name, lambda number=number: plaque(number))
for name,builder in [('Sterlyad_Matchbox',matchbox),('Answering_Machine',machine),
                     ('Microcassette_MC60',cassette),('Anchor_Keyring',keyring),('Door_Lock_Cylinder',lock),
                     ('Pobeda_Wristwatch',watch),('Silver_Cigarette_Case',cigarette_case),
                     ('Fathers_Vinyl',record),('Fathers_Record_Sleeve',sleeve),('Entry_Light_Switch',switch)]:
    if not only_asset or name == only_asset:
        export(name,builder)
if not only_asset:
    maps()
    print('15 models, 15 Blender source files, 6 texture maps written.')

