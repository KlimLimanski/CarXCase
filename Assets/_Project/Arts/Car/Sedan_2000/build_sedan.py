"""Metre-scale 1998–2001 sedan interior with a rear-passenger head rig."""
import bpy
import math
import random
import json
from pathlib import Path
from mathutils import Vector, Matrix
import numpy as np

OUT=Path('C:/Users/Мария/Documents/ChatGPT/carX/Sedan_2000')
random.seed(91)
COL={}

def material(name,color,rough=.6,metal=0,grain=0):
    m=bpy.data.materials.new('Sedan_'+name)
    m.diffuse_color=(*color,1)
    m.use_nodes=True
    n=m.node_tree.nodes
    p=next(a for a in n if a.type=='BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=rough
    p.inputs['Metallic'].default_value=metal
    if grain:
        tex=n.new('ShaderNodeTexNoise')
        tex.inputs['Scale'].default_value=grain
        tex.inputs['Detail'].default_value=2
        coord=n.new('ShaderNodeTexCoord')
        m.node_tree.links.new(coord.outputs['Object'],tex.inputs['Vector'])
        bump=n.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value=.18
        bump.inputs['Distance'].default_value=.00045
        m.node_tree.links.new(tex.outputs['Fac'],bump.inputs['Height'])
        m.node_tree.links.new(bump.outputs['Normal'],p.inputs['Normal'])
        ramp=n.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].color=(*(v*.79 for v in color),1)
        ramp.color_ramp.elements[1].color=(*(v*1.13 for v in color),1)
        m.node_tree.links.new(tex.outputs['Fac'],ramp.inputs[0])
        m.node_tree.links.new(ramp.outputs[0],p.inputs['Base Color'])
    return m

def fabric():
    m=material('Woven_Taupe',(.21,.205,.18),.93)
    n,l=m.node_tree.nodes,m.node_tree.links
    p=next(a for a in n if a.type=='BSDF_PRINCIPLED')
    p.inputs['Sheen Weight'].default_value=.25
    coord=n.new('ShaderNodeTexCoord')
    noise=n.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value=440
    noise.inputs['Detail'].default_value=2
    l.new(coord.outputs['Object'],noise.inputs['Vector'])
    ramp=n.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color=(.09,.086,.072,1)
    ramp.color_ramp.elements[1].color=(.25,.24,.205,1)
    l.new(noise.outputs['Fac'],ramp.inputs[0])
    l.new(ramp.outputs[0],p.inputs['Base Color'])
    wave=n.new('ShaderNodeTexWave')
    wave.inputs['Scale'].default_value=270
    wave.inputs['Distortion'].default_value=1
    l.new(coord.outputs['Object'],wave.inputs[0])
    bump=n.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value=.24
    bump.inputs['Distance'].default_value=.0007
    l.new(wave.outputs['Color'],bump.inputs['Height'])
    l.new(bump.outputs['Normal'],p.inputs['Normal'])
    return m

def to_collection(obj,col,parent=True):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    COL[col].objects.link(obj)
    if parent:
        obj.parent=ROOT
    return obj

def mesh_obj(name,verts,faces,mat,col,smooth=False):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    COL[col].objects.link(obj)
    obj.parent=ROOT
    obj.data.materials.append(mat)
    if smooth:
        for p in mesh.polygons:p.use_smooth=True
    return obj

def bevel(obj,width=.02,segments=3):
    for p in obj.data.polygons:p.use_smooth=True
    mod=obj.modifiers.new('Soft manufactured edges','BEVEL')
    mod.width=width
    mod.segments=segments
    mod.affect='EDGES'
    mod.harden_normals=True
    mod=obj.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL')
    mod.keep_sharp=True
    return obj

def box(name,loc,size,mat,col,rounding=.02,rotation=None):
    x,y,z=(s/2 for s in size)
    verts=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
    faces=[(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]
    obj=mesh_obj(name,verts,faces,mat,col)
    obj.location=loc
    if rounding:bevel(obj,min(rounding,min(size)*.45),3)
    if rotation:obj.rotation_euler=rotation
    return obj

def tube(name,points,radius,mat,col,sides=8,cyclic=False):
    verts=[]
    for i,p in enumerate(points):
        prev=Vector(points[(i-1)%len(points)] if cyclic or i>0 else points[0])
        nex=Vector(points[(i+1)%len(points)] if cyclic or i<len(points)-1 else points[-1])
        direction=(nex-prev).normalized()
        u=direction.cross(Vector((0,0,1)))
        if u.length<.01:u=direction.cross(Vector((0,1,0)))
        u.normalize()
        v=direction.cross(u)
        verts.extend(Vector(p)+radius*(math.cos(j*math.tau/sides)*u+math.sin(j*math.tau/sides)*v) for j in range(sides))
    faces=[]
    for i in range(len(points) if cyclic else len(points)-1):
        ni=(i+1)%len(points)
        for j in range(sides):
            k=(j+1)%sides
            faces.append((i*sides+j,i*sides+k,ni*sides+k,ni*sides+j))
    if not cyclic:
        faces.extend([tuple(range(sides-1,-1,-1)),tuple((len(points)-1)*sides+j for j in range(sides))])
    return mesh_obj(name,verts,faces,mat,col,True)

def cylinder(name,center,radius,depth,mat,col,axis=(0,0,1),sides=32):
    direction=Vector(axis).normalized()
    return tube(name,[Vector(center)-direction*depth/2,Vector(center)+direction*depth/2],radius,mat,col,sides)

def ring(name,center,radius,thickness,mat,col,normal=(0,1,0),steps=48,sides=10):
    axis=Vector(normal).normalized()
    u=axis.cross(Vector((0,0,1))).normalized()
    if u.length<.1:u=Vector((1,0,0))
    v=axis.cross(u)
    points=[Vector(center)+radius*(math.cos(i*math.tau/steps)*u+math.sin(i*math.tau/steps)*v) for i in range(steps)]
    return tube(name,points,thickness,mat,col,sides,True)

def seam(name,points,col,mat=None):
    return tube(name,points,.0018,mat or M['seam'],col,5)

def text(name,body,loc,size,col,mat=None,rotation=(math.pi/2,0,math.pi),align='CENTER'):
    curve=bpy.data.curves.new(name,'FONT')
    curve.body=body
    curve.size=size
    curve.align_x=align
    curve.align_y='CENTER'
    curve.resolution_u=3
    curve.font=FONT
    obj=bpy.data.objects.new(name,curve)
    COL[col].objects.link(obj)
    obj.parent=ROOT
    obj.location=loc
    obj.rotation_euler=rotation
    curve.materials.append(mat or M['ink'])
    return obj

def front_text(name,body,loc,size,col,mat=None):
    # Local font +Y is vertical +Z, and its front normal faces cabin +Y.
    return text(name,body,loc,size,col,mat,rotation=(math.pi/2,0,math.pi))

def seat(prefix,x):
    col='02_Seats'
    box(prefix+'_Lower_Frame',(x,-.12,.35),(.53,.55,.15),M['plastic'],col,.055)
    box(prefix+'_Cushion',(x,-.15,.49),(.58,.62,.17),M['fabric'],col,.078,(-.05,0,0))
    for s in (-1,1):
        box(prefix+'_Cushion_Bolster_'+str(s),(x+s*.248,-.14,.525),(.12,.57,.16),M['clothborder'],col,.055,(-.04,0,s*.05))
    box(prefix+'_Cushion_Insert',(x,-.17,.572),(.37,.48,.028),M['fabric'],col,.012)
    # Backrest reclines towards the rear of the vehicle.
    box(prefix+'_Backrest',(x,.165,.84),(.58,.18,.63),M['clothborder'],col,.085,(.15,0,0))
    box(prefix+'_Front_Backrest_Insert',(x,.055,.845),(.36,.04,.45),M['fabric'],col,.018,(.15,0,0))
    box(prefix+'_Rear_Back_Panel',(x,.269,.82),(.48,.038,.46),M['fabric'],col,.017,(.15,0,0))
    box(prefix+'_Seatback_Pocket',(x,.307,.73),(.39,.032,.19),M['clothborder'],col,.013,(.15,0,0))
    seam(prefix+'_Pocket_Opening',[(x-.18,.328,.82),(x,.334,.81),(x+.18,.328,.82)],col)
    for s in (-1,1):
        tube(prefix+'_Headrest_Post_'+str(s),[(x+s*.085,.19,1.08),(x+s*.085,.20,1.22)],.009,M['chrome'],col,10)
        seam(prefix+'_Back_Stitch_'+str(s),[(x+s*.235,.285,.59),(x+s*.235,.30,.84),(x+s*.235,.24,1.08)],col)
    box(prefix+'_Headrest',(x,.205,1.22),(.33,.17,.215),M['clothborder'],col,.066,(.10,0,0))
    seam(prefix+'_Headrest_Seam',[(x-.12,.289,1.15),(x-.14,.291,1.23),(x-.11,.274,1.30),(x+.11,.274,1.30),(x+.14,.291,1.23),(x+.12,.289,1.15)],col)
    for s in (-1,1):
        box(prefix+'_Seat_Rail_'+str(s),(x+s*.20,-.11,.29),(.035,.66,.035),M['metal'],col,.005)
    box(prefix+'_Recline_Lever',(x+(-.30 if x<0 else .30),.06,.42),(.035,.13,.04),M['plastic'],col,.014)
    for i in range(4):
        yy=-.37+i*.115
        seam(prefix+'_Cushion_Stitch_'+str(i),[(x-.17,yy,.589),(x+.17,yy,.589)],col,M['clothborder'])

def rear_bench():
    col='02_Seats'
    box('Rear_Bench_Base',(0,1.02,.38),(1.39,.59,.23),M['clothborder'],col,.065)
    box('Rear_Bench_Cushion',(0,1.00,.51),(1.37,.60,.17),M['fabric'],col,.065)
    box('Rear_Bench_Backrest',(0,1.32,.84),(1.35,.20,.60),M['clothborder'],col,.08,(.15,0,0))
    for x in (-.45,0,.45):
        box('Rear_Back_Insert_'+str(x),(x,1.20,.83),(.37,.04,.43),M['fabric'],col,.018,(.15,0,0))
        for xx in (-.16,.16):
            seam('Rear_Upholstery_Stitch',[(x+xx,.75,.59),(x+xx,1.20,.58)],col)
    for x in (-.46,.46):
        for xx in (-.075,.075):
            tube('Rear_Headrest_Post',[(x+xx,1.33,1.10),(x+xx,1.34,1.19)],.009,M['chrome'],col,8)
        box('Rear_Headrest_'+str(x),(x,1.33,1.20),(.32,.17,.19),M['clothborder'],col,.06)
    box('Rear_Parcel_Shelf',(0,1.56,.92),(1.35,.38,.055),M['carpet'],col,.02)
    for x in (-.47,.47):
        speaker=cylinder('Rear_Speaker_Grille',(x,1.52,.956),.10,.008,M['rubber'],col)
        speaker.scale.y=.65
        speaker.location.y=(1-.65)*1.52

def belts():
    col='06_Belts_and_Small_Details'
    for side in (-1,1):
        x=side*.715
        for prefix,y,z in [('Front',.27,1.22),('Rear',1.18,1.17)]:
            box(prefix+'_Belt_Upper_Anchor_'+str(side),(x,y,z),(.06,.08,.11),M['plastic'],col,.02)
            # Flat webbing folds in 3D between pillar, tongue and floor mounting.
            pts=[(x,y,z),(x-side*.04,y-.015,.74),(x-side*.03,y-.06,.34)]
            verts=[]
            for p in pts:
                verts.extend([(p[0],p[1]-.023,p[2]),(p[0],p[1]+.023,p[2])])
            obj=mesh_obj(prefix+'_Belt_Webbing_'+str(side),verts,[(0,1,3,2),(2,3,5,4)],M['belt'],col)
            mod=obj.modifiers.new('Webbing thickness','SOLIDIFY');mod.thickness=.0013
            box(prefix+'_Belt_Tongue_'+str(side),(x-side*.04,y-.015,.72),(.009,.063,.09),M['chrome'],col,.004)
            box(prefix+'_Belt_Lower_Anchor_'+str(side),(x,y,.32),(.08,.09,.045),M['plastic'],col,.012)
        for yy in (.22,.99):
            xx=side*(.13 if yy<.4 else .22)
            tube('Buckle_Stalk',[(xx,yy,.40),(xx,yy,.59)],.014,M['belt'],col)
            box('Seat_Belt_Buckle',(xx,yy,.60),(.055,.065,.08),M['plastic'],col,.013)
            box('Red_Buckle_Release',(xx,yy-.006,.642),(.039,.04,.012),M['red'],col,.004)

def door(side,front):
    col='03_Door_Panels'
    prefix=('Left' if side<0 else 'Right')+('_Front' if front else '_Rear')
    y=-.58 if front else .79
    length=1.19 if front else .91
    x=side*.767
    box(prefix+'_Door_Card',(x,y,.60),(.10,length,.46),M['trim'],col,.045)
    box(prefix+'_Upper_Padded_Rail',(x-side*.025,y,.807),(.11,length,.075),M['plastic'],col,.033)
    box(prefix+'_Cloth_Insert',(x-side*.056,y+.02,.63),(.024,length*.78,.24),M['fabric'],col,.010)
    box(prefix+'_Armrest',(x-side*.105,y,.59),(.16,length*.67,.085),M['plastic'],col,.035)
    box(prefix+'_Lower_Storage_Pocket',(x-side*.058,y+.05,.415),(.07,length*.67,.14),M['plastic'],col,.026)
    box(prefix+'_Pocket_Mouth',(x-side*.095,y+.05,.487),(.05,length*.54,.012),M['rubber'],col,.005)
    handle_y=y-.29 if front else y-.18
    box(prefix+'_Handle_Recess',(x-side*.062,handle_y,.72),(.018,.19,.065),M['rubber'],col,.008)
    tube(prefix+'_Chrome_Handle',[(x-side*.083,handle_y-.07,.73),(x-side*.11,handle_y-.04,.727),(x-side*.11,handle_y+.065,.727)],.010,M['chrome'],col,10)
    panelx=x-side*.12
    box(prefix+'_Switch_Panel',(panelx,y-.11,.64),(.09,.20,.014),M['rubber'],col,.005)
    for k in range(4 if front and side<0 else 1):
        yy=y-.17+(k//2)*.07
        xx=panelx+(k%2-.5)*.035
        box(prefix+'_Window_Switch_'+str(k),(xx,yy,.655),(.025,.045,.017),M['plastic'],col,.005)
        box(prefix+'_Window_Icon_'+str(k),(xx,yy,.664),(.012,.008,.001),M['ink'],col,.0003)
    cylinder(prefix+'_Door_Lock_Pin',(x-side*.015,y+length*.36,.863),.009,.052,M['plastic'],col,sides=10)
    # Speaker holes use a dark inset and 20 physical grille slats.
    speaker_y=y-.37 if front else y-.24
    speaker=cylinder(prefix+'_Speaker',(x-side*.056,speaker_y,.47),.095,.01,M['rubber'],col,axis=(1,0,0))
    for i in range(13):
        z=.395+i*.012
        half=math.sqrt(max(0,.083**2-(z-.47)**2))
        box(prefix+'_Grille_'+str(i),(x-side*.066,speaker_y,z),(.009,half*2,.005),M['trim'],col,.001)
    seam(prefix+'_Door_Trim_Seam',[(x-side*.064,y-length*.43,.77),(x-side*.064,y+length*.43,.77)],col,M['clothborder'])

def dash_surface():
    col='04_Dashboard_and_Controls'
    # Swept cross-section produces the broad, curved padded instrument panel.
    verts=[]
    sections=19
    for i in range(sections):
        x=-.735+1.47*i/(sections-1)
        curve=.035*(1-(x/.76)**2)
        profile=[(-1.49,.87),(-1.41,.947+curve),(-1.19,.985+curve),(-.96,.94+curve),(-.87,.85),(-.94,.73),(-1.36,.68)]
        verts.extend((x,y,z) for y,z in profile)
    faces=[]
    for i in range(sections-1):
        for j in range(7):faces.append((i*7+j,(i+1)*7+j,(i+1)*7+(j+1)%7,i*7+(j+1)%7))
    faces.extend([tuple(range(6,-1,-1)),tuple((sections-1)*7+j for j in range(7))])
    mesh_obj('Curved_Dashboard_Upper',verts,faces,M['dash'],col,True)
    box('Dashboard_Lower',(0,-1.09,.64),(1.43,.35,.30),M['trim'],col,.085)
    box('Glovebox_Lid',(.46,-.879,.66),(.49,.035,.235),M['trim'],col,.014)
    box('Glovebox_Handle',(.46,-.852,.73),(.11,.026,.025),M['plastic'],col,.010)
    seam('Passenger_Airbag_Seam',[(.23,-.958,.91),(.24,-1.16,.993),(.65,-1.16,.974),(.65,-.96,.90)],col,M['plastic'])
    front_text('Airbag_Label','SRS AIRBAG',(.50,-.86,.79),.012,col,M['dimink'])
    # Instrument binnacle open towards the rear of the cabin.
    box('Instrument_Binnacle',(-.40,-.99,.964),(.58,.22,.205),M['dash'],col,.08)
    box('Gauge_Backplate',(-.40,-.864,.968),(.52,.018,.16),M['rubber'],col,.008)
    for x,z,r,title,maxval in [(-.49,.969,.069,'MPH',120),(-.319,.969,.059,'RPM x1000',7)]:
        cylinder(title+'_Dial',(x,-.850,z),r,.008,M['dial'],col,axis=(0,1,0),sides=48)
        ring(title+'_Bezel',(x,-.844,z),r+.003,.004,M['plastic'],col,steps=48,sides=6)
        for j in range(41):
            angle=math.radians(225-j*270/40)
            ra=r*.80 if j%5 else r*.72
            tube(title+'_Tick_'+str(j),[(x+ra*math.cos(angle),-.838,z+ra*math.sin(angle)),(x+r*.91*math.cos(angle),-.838,z+r*.91*math.sin(angle))],.00065 if j%5 else .0011,M['ink'],col,4)
        for j in range(7):
            angle=math.radians(225-j*45)
            front_text(title+'_Number_'+str(j),str(round(j*maxval/6)),(x+r*.57*math.cos(angle),-.833,z+r*.57*math.sin(angle)),.010,col)
        angle=math.radians(155 if maxval==120 else 170)
        tube(title+'_Needle',[(x,-.826,z),(x+r*.74*math.cos(angle),-.826,z+r*.74*math.sin(angle))],.0013,M['red'],col,5)
        cylinder(title+'_Hub',(x,-.825,z),.006,.006,M['metal'],col,(0,1,0),16)
        front_text(title+'_Legend',title,(x,-.830,z-.025),.0075,col,M['dimink'])
    front_text('Odometer','084271',(-.49,-.825,.937),.009,col,M['lcdtext'])
    for k in range(2):
        x=-.65+k*.50
        cylinder('Auxiliary_Gauge_'+str(k),(x,-.849,.967),.027,.006,M['dial'],col,(0,1,0),24)
        tube('Aux_Gauge_Needle',[(x,-.837,.955),(x-.01,-.837,.982)],.0012,M['red'],col,4)
        front_text('Gauge_Letter','E  F' if k==0 else 'C  H',(x,-.830,.95),.007,col)
    # Rectangular vents are softly rounded and recessed.
    for x,w in ((-.665,.11),(-.10,.15),(.10,.15),(.65,.13)):
        box('Dash_Vent_Housing',(x,-.893,.864),(w,.037,.076),M['plastic'],col,.017)
        box('Dash_Vent_Recess',(x,-.868,.864),(w-.02,.012,.054),M['rubber'],col,.005)
        for j in range(4):
            box('Dash_Vent_Louvre',(x,-.855,.846+j*.012),(w-.024,.018,.0045),M['dimink'],col,.001)
    for x in (-.48,.48):
        box('Windshield_Defroster',(x,-1.36,.967),(.36,.055,.012),M['rubber'],col,.005)
        for i in range(14):
            box('Defroster_Slat',(x-.16+i*.024,-1.36,.975),(.007,.045,.006),M['plastic'],col,.002)
    console()
    steering()

def console():
    col='04_Dashboard_and_Controls'
    box('Central_Console_Surround',(0,-.91,.672),(.365,.10,.35),M['plastic'],col,.045)
    box('Factory_Radio_Fascia',(0,-.848,.738),(.302,.026,.111),M['radio'],col,.011)
    box('Green_LCD_Display',(0,-.829,.766),(.13,.006,.027),M['lcd'],col,.002)
    front_text('Radio_LCD','FM  101.9',(0,-.824,.768),.013,col,M['lcdtext'])
    box('Cassette_Slot',(0,-.828,.733),(.155,.009,.014),M['rubber'],col,.003)
    front_text('Radio_Cassette_Label','AM/FM  •  STEREO',(0,-.822,.702),.007,col,M['dimink'])
    for x in (-.126,.126):
        cylinder('Radio_Knob',(x,-.817,.755),.014,.022,M['plastic'],col,(0,1,0),20)
        front_text('Radio_Knob_Label','VOL' if x<0 else 'TUNE',(x,-.826,.731),.0065,col,M['ink'])
    for i in range(6):
        x=-.102+i*.041
        box('Radio_Preset_'+str(i),(x,-.826,.711),(.033,.015,.012),M['plastic'],col,.004)
        front_text('Preset_Number_'+str(i),str(i+1),(x,-.816,.712),.006,col)
    box('Climate_Fascia',(0,-.846,.602),(.30,.025,.103),M['radio'],col,.012)
    for k,x in enumerate((-.10,0,.10)):
        ring('Climate_Dial_Ring',(x,-.825,.607),.026,.003,M['dimink'],col,steps=32,sides=6)
        cylinder('Climate_Rotary_Knob',(x,-.811,.607),.020,.029,M['plastic'],col,(0,1,0),24)
        box('Climate_Knob_Pointer',(x,-.792,.619),(.003,.003,.010),M['ink'],col,.001)
        front_text('Climate_Control_Label_'+str(k),['FAN','TEMP','VENT'][k],(x,-.821,.569),.007,col)
        if k==1:
            for j in range(9):
                a=math.radians(20+j*17)
                tube('Temperature_Scale',[(x+.030*math.cos(a),-.825,.607+.030*math.sin(a)),(x+.036*math.cos(a),-.825,.607+.036*math.sin(a))],.0015,M['red'] if j<4 else M['blue'],col,4)
    box('Ashtray',(0,-.809,.52),(.20,.09,.05),M['trim'],col,.013)
    box('Ashtray_Pull',(0,-.756,.52),(.07,.018,.018),M['plastic'],col,.007)
    box('Center_Tunnel',(0,-.18,.36),(.27,1.12,.20),M['trim'],col,.05)
    box('Gear_Selector_Plate',(0,-.51,.479),(.22,.28,.02),M['plastic'],col,.009)
    box('Automatic_Gear_Slot',(-.035,-.51,.494),(.028,.19,.008),M['rubber'],col,.003)
    tube('Automatic_Shift_Lever',[(-.035,-.54,.49),(-.035,-.52,.61),(-.035,-.49,.66)],.012,M['chrome'],col,12)
    box('Shift_Knob',(-.035,-.49,.674),(.10,.057,.055),M['leather'],col,.023)
    text('PRNDL','P R N D 2 1',(.068,-.51,.494),.013,col,M['ink'],rotation=(0,0,math.pi/2))
    for yy in (-.21,.01):
        cylinder('Cupholder_Recess',(0,yy,.47),.079,.012,M['rubber'],col,sides=32)
        ring('Cupholder_Rim',(0,yy,.476),.079,.009,M['plastic'],col,(0,0,1),32,8)
        for side in (-1,1):
            box('Cupholder_Grip',(side*.061,yy,.478),(.022,.045,.022),M['plastic'],col,.008)
    box('Center_Armrest',(0,.29,.64),(.255,.37,.11),M['leather'],col,.048)
    box('Console_Rear_Panel',(0,.47,.44),(.24,.05,.24),M['trim'],col,.02)
    box('Rear_Console_Ashtray',(0,.503,.48),(.15,.014,.08),M['plastic'],col,.006)
    for i in range(4):
        box('Rear_Console_Seam',(0,.512,.41+i*.014),(.17,.008,.004),M['rubber'],col,.001)

def steering():
    col='04_Dashboard_and_Controls'
    center=Vector((-.43,-.67,.89))
    normal=Vector((0,1,.24)).normalized()
    ring('Four_Spoke_Steering_Wheel',center,.176,.021,M['leather'],col,normal,64,12)
    tube('Steering_Column',[(-.43,-1.02,.75),(-.43,-.72,.87)],.052,M['plastic'],col,16)
    box('Steering_Airbag_Hub',center,(.20,.067,.13),M['leather'],col,.035,(.20,0,0))
    for sx in (-1,1):
        for sz in (-1,1):
            tube('Steering_Spoke',[(center.x+sx*.075,center.y,center.z+sz*.035),(center.x+sx*.14,center.y-sz*.02,center.z+sz*.10)],.023,M['plastic'],col,8)
    front_text('Steering_SRS','AIRBAG',(-.43,-.632,.87),.009,col,M['dimink'])
    for sx in (-1,1):
        box('Cruise_Control_Button',(-.43+sx*.10,-.628,.915),(.031,.012,.022),M['radio'],col,.005)
    tube('Turn_Signal_Stalk',[(-.48,-.80,.86),(-.66,-.78,.87)],.009,M['plastic'],col,8)
    box('Turn_Signal_Tip',(-.66,-.78,.87),(.045,.026,.025),M['plastic'],col,.01)
    tube('Wiper_Stalk',[(-.38,-.80,.86),(-.26,-.78,.88)],.009,M['plastic'],col,8)
    for x,y,w in [(-.48,-1.12,.09),(-.29,-1.14,.046)]:
        box('Pedal',(x,y,.38),(w,.035,.12),M['rubber'],col,.012,(-.35,0,0))
        for j in range(4):box('Pedal_Tread',(x,y+.022,.34+j*.025),(w*.85,.008,.004),M['plastic'],col,.001)

def glass(name,points):
    obj=mesh_obj(name,points,[tuple(range(len(points)))],M['glass'],'01_Glass_and_Body')
    mod=obj.modifiers.new('Laminated glass thickness','SOLIDIFY')
    mod.thickness=.003
    obj['glass_thickness_m']=.003
    return obj

def shell():
    col='01_Glass_and_Body'
    box('Floor_Pan',(0,0,.27),(1.53,2.91,.055),M['carpet'],'05_Roof_and_Carpet',.02)
    box('Transmission_Hump',(0,-.05,.31),(.27,2.30,.13),M['carpet'],'05_Roof_and_Carpet',.055)
    for x in (-.42,.42):
        for y in (-.75,.59):
            box('Floor_Mat',(x,y,.308),(.52,.54,.013),M['carpet'],'05_Roof_and_Carpet',.006)
            for yy in (-.24,.24):seam('Mat_Binding',[(x-.24,y+yy,.317),(x+.24,y+yy,.317)],'05_Roof_and_Carpet',M['rubber'])
    # Headliner forms a shallow crown with a visible perimeter edge.
    verts=[]
    for j in range(13):
        y=-.96+1.99*j/12
        for i in range(17):
            x=-.66+1.32*i/16
            z=1.437+.055*(1-(x/.66)**2)+.008*math.sin(j*math.pi/12)
            verts.append((x,y,z))
    faces=[]
    for j in range(12):
        for i in range(16):
            a=j*17+i
            faces.append((a,a+17,a+18,a+1))
    roof=mesh_obj('Headliner',verts,faces,M['headliner'],'05_Roof_and_Carpet',True)
    mod=roof.modifiers.new('Headliner backing','SOLIDIFY');mod.thickness=.018
    # Exterior roof is a light shell, while all the interior pillars are padded.
    outside=roof.copy();outside.data=roof.data.copy();outside.name='Simple_Exterior_Roof';COL[col].objects.link(outside)
    outside.location.z=.025
    outside.data.materials.clear();outside.data.materials.append(M['paint'])
    for y,name in [(-.96,'Windshield_Header'),(1.03,'Rear_Window_Header')]:
        box(name,(0,y,1.447),(1.32,.09,.10),M['headliner'],'05_Roof_and_Carpet',.034)
    for side in (-1,1):
        a=(side*.77,-1.47,.87);b=(side*.66,-.96,1.437)
        c=(side*.66,.25,1.437);d=(side*.78,.25,.80)
        e=(side*.66,1.03,1.437);f=(side*.77,1.60,.88)
        tube(('Left' if side<0 else 'Right')+'_A_Pillar',[a,b],.042,M['headliner'],col,10)
        tube(('Left' if side<0 else 'Right')+'_B_Pillar',[d,c],.047,M['trim'],col,10)
        tube(('Left' if side<0 else 'Right')+'_C_Pillar',[e,f],.065,M['headliner'],col,10)
        tube('Roof_Side_Rail',[b,c,e],.039,M['headliner'],col,10)
        tube('Window_Sill_Rail',[(side*.78,-1.45,.82),(side*.79,.25,.82),(side*.78,1.52,.82)],.035,M['plastic'],col,8)
        # Four separately editable roll-down windows and two fixed rear quarterlights.
        front=[(side*.765,-1.42,.86),(side*.765,.19,.86),(side*.66,.19,1.392),(side*.66,-.94,1.392)]
        rear=[(side*.765,.31,.86),(side*.765,1.08,.86),(side*.68,.99,1.392),(side*.66,.31,1.392)]
        quarter=[(side*.765,1.12,.86),(side*.765,1.52,.90),(side*.68,1.03,1.375)]
        for label,pts in [('Front',front),('Rear',rear),('Quarter',quarter)]:
            glass(('Left' if side<0 else 'Right')+'_'+label+'_Glass',pts)
            tube(label+'_Glass_Weatherstrip',pts,.009,M['rubber'],col,6,True)
        # Sills and lower exterior skins are intentionally simple.
        box('Exterior_Lower_Side_'+str(side),(side*.83,.05,.54),(.09,3.05,.48),M['paint'],col,.035)
        box('Door_Sill_Step_'+str(side),(side*.74,.07,.32),(.10,2.55,.07),M['trim'],col,.02)
    windshield=[(-.74,-1.47,.91),(.74,-1.47,.91),(.625,-.96,1.414),(-.625,-.96,1.414)]
    rear=[(-.625,1.045,1.414),(.625,1.045,1.414),(.735,1.60,.94),(-.735,1.60,.94)]
    glass('Windshield_Laminated',windshield)
    glass('Rear_Window_Heated',rear)
    for name,pts in [('Windshield',windshield),('Rear_Window',rear)]:
        tube(name+'_Rubber_Gasket',pts,.018,M['rubber'],col,8,True)
    for j in range(1,10):
        t=j/10;y=1.045+(1.60-1.045)*t;z=1.414+(.94-1.414)*t
        width=.625+(.735-.625)*t
        tube('Rear_Defroster_Trace_'+str(j),[(-width+.02,y-.001,z-.001),(width-.02,y-.001,z-.001)],.0005,M['copper'],col,4)
    box('Simple_Hood',(0,-1.91,.82),(1.60,.86,.14),M['paint'],col,.065)
    box('Simple_Trunk',(0,1.96,.80),(1.60,.73,.17),M['paint'],col,.065)
    box('Simple_Front_Bumper',(0,-2.36,.51),(1.69,.14,.33),M['paint'],col,.05)
    box('Simple_Rear_Bumper',(0,2.32,.51),(1.69,.14,.33),M['paint'],col,.05)
    for side in (-1,1):
        box('Exterior_Mirror_Housing',(side*.90,-1.12,.92),(.16,.19,.10),M['paint'],col,.04)
        box('Exterior_Mirror_Glass',(side*.90,-1.014,.925),(.125,.008,.069),M['mirror'],col,.003)
        for y in (-1.48,1.48):
            cylinder('Simple_Tire',(side*.78,y,.31),.30,.16,M['rubber'],col,axis=(1,0,0),sides=32)
            cylinder('Simple_Wheel',(side*.867,y,.31),.19,.015,M['metal'],col,axis=(1,0,0),sides=24)
    for x in (-.40,.40):
        tube('Windshield_Wiper',[(x-.20,-1.49,.932),(x+.25,-1.485,.937)],.008,M['rubber'],col,6)

def upper_details():
    col='05_Roof_and_Carpet'
    for side in (-1,1):
        box('Sun_Visor_'+str(side),(side*.38,-.81,1.411),(.39,.24,.027),M['headliner'],col,.012)
        tube('Visor_Hinge_'+str(side),[(side*.20,-.905,1.43),(side*.55,-.905,1.43)],.009,M['trim'],col,8)
        box('Visor_Label_'+str(side),(side*.38,-.81,1.395),(.22,.09,.001),M['label'],col,0)
        for yy in (-.34,.73):
            x=side*.638
            tube('Passenger_Grab_Handle',[(x,yy-.10,1.375),(x-side*.045,yy-.075,1.348),(x-side*.045,yy+.075,1.348),(x,yy+.10,1.375)],.014,M['trim'],col,10)
            for y in (yy-.10,yy+.10):box('Grab_Handle_Cover',(x,y,1.38),(.035,.035,.027),M['trim'],col,.01)
    box('Dome_Light_Bezel',(0,.43,1.468),(.20,.14,.025),M['trim'],col,.01)
    box('Dome_Light_Diffuser',(0,.43,1.451),(.135,.09,.01),M['lamp'],col,.004)
    box('Dome_Light_Switch',(.08,.43,1.45),(.018,.034,.014),M['plastic'],col,.004)
    tube('Rearview_Mirror_Stem',[(0,-.99,1.38),(0,-.91,1.30)],.011,M['plastic'],col,10)
    box('Rearview_Mirror_Frame',(0,-.894,1.285),(.255,.035,.088),M['plastic'],col,.016,(-.08,0,0))
    box('Rearview_Mirror_Surface',(0,-.872,1.287),(.228,.003,.067),M['mirror'],col,.001,(-.08,0,0))
    box('Mirror_Day_Night_Tab',(0,-.879,1.233),(.035,.026,.02),M['plastic'],col,.008)

def rig():
    col='07_Passenger_Camera_Rig'
    head=bpy.data.objects.new('Rear_Passenger_HEAD_PIVOT',None)
    COL[col].objects.link(head)
    head.parent=ROOT
    head.location=(0,.95,1.22)
    head.empty_display_type='SPHERE';head.empty_display_size=.065
    head.rotation_mode='XYZ'
    head['yaw_degrees']=0.0
    head['pitch_degrees']=-4.0
    head['head_motion']=1.0
    head['instructions']='Yaw looks left/right around the seated head. Pitch looks up/down. Set head_motion to 0 for a still head. Camera is on the rear centre seat.'
    head.id_properties_ui('yaw_degrees').update(min=-180,max=180,soft_min=-110,soft_max=110,description='Head turn. +90 right window, -90 left window, 180 rear window.')
    head.id_properties_ui('pitch_degrees').update(min=-65,max=65,soft_min=-25,soft_max=25,description='Positive raises gaze, negative lowers it.')
    head.id_properties_ui('head_motion').update(min=0,max=1,description='Subtle head drift and roll amplitude. Set to 0 to disable.')
    for axis,prop,expr in [(2,'yaw_degrees','-v*pi/180'),(0,'pitch_degrees','-v*pi/180')]:
        fc=head.driver_add('rotation_euler',axis)
        fc.driver.type='SCRIPTED'
        var=fc.driver.variables.new();var.name='v';var.type='SINGLE_PROP'
        var.targets[0].id=head;var.targets[0].data_path='["'+prop+'"]'
        fc.driver.expression=expr
    motion=bpy.data.objects.new('Passenger_Subtle_Head_Motion',None)
    COL[col].objects.link(motion);motion.parent=head
    for path,axis,expr in [('rotation_euler',1,'a*0.003*sin(frame*0.053)'),('location',2,'a*0.002*sin(frame*0.081)'),('location',0,'a*0.0015*sin(frame*0.047)')]:
        fc=motion.driver_add(path,axis);var=fc.driver.variables.new();var.name='a';var.type='SINGLE_PROP'
        var.targets[0].id=head;var.targets[0].data_path='["head_motion"]';fc.driver.expression=expr
    data=bpy.data.cameras.new('Rear_Passenger_Camera')
    cam=bpy.data.objects.new('Rear_Passenger_Camera',data)
    COL[col].objects.link(cam);cam.parent=motion
    cam.rotation_euler=(math.pi/2,0,math.pi)
    data.lens=22;data.sensor_width=36;data.clip_start=.025;data.clip_end=250
    data.dof.use_dof=False
    SCENE.camera=cam
    SCENE.frame_start=1;SCENE.frame_end=360;SCENE.render.fps=24
    # Vehicle travel and the head controls are independent.
    for frame,x in [(1,-7),(360,7)]:
        ROOT.location=(x,-6,-.23)
        ROOT.keyframe_insert(data_path='location',frame=frame)
    action=ROOT.animation_data.action
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for kp in fc.keyframe_points:kp.interpolation='LINEAR'
    for frame,label in [(1,'FORWARD • rear passenger'),(90,'TURN RIGHT • yaw +90'),(180,'TURN LEFT • yaw -90'),(270,'LOOK BACK • yaw 180')]:
        SCENE.timeline_markers.new(label,frame=frame)
    SCENE.frame_set(180)
    return head

def environment(source):
    col=COL['08_Street_Context']
    # Existing building collections are shared, with a second linked row across the road.
    context=bpy.data.collections.new('Street_Houses_For_Car')
    for child in source.collection.children:
        if child.name.startswith(('01_','02_','03_','04_','05_')):
            context.children.link(child)
    col.children.link(context)
    opposite=bpy.data.objects.new('Opposite_Linked_Street',None)
    col.objects.link(opposite)
    opposite.instance_type='COLLECTION';opposite.instance_collection=context
    opposite.location=(3,-18,0);opposite.rotation_euler.z=math.pi
    for k in (-2,-1,1,2):
        for far in (False,True):
            instance=bpy.data.objects.new('Street_Context_Extension',None)
            col.objects.link(instance)
            instance.instance_type='COLLECTION';instance.instance_collection=context
            instance.location=(k*46.5+(3 if far else 0),-18 if far else 0,0)
            instance.rotation_euler.z=math.pi if far else 0
    for name in ('Review_Street',):
        obj=bpy.data.objects.get(name)
        if obj:col.objects.link(obj)
    box('Extended_Road',(0,-10,-.267),(900,15,.06),M['asphalt'],'08_Street_Context',.0).parent=None
    for x in range(-80,81,6):
        marker=box('Road_Center_Dash',(x,-10,-.234),(2.4,.10,.002),M['roadmark'],'08_Street_Context',0)
        marker.parent=None
    sidewalk=box('Opposite_Sidewalk',(0,-16.5,-.13),(325,3,.26),M['concrete'],'08_Street_Context',0);sidewalk.parent=None
    sidewalk=box('Extended_Near_Sidewalk',(0,-1.5,-.132),(450,3,.26),M['concrete'],'08_Street_Context',0);sidewalk.parent=None
    world=bpy.data.worlds.new('Overcast_New_York')
    world.use_nodes=True
    back=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
    back.inputs[0].default_value=(.60,.67,.76,1)
    back.inputs[1].default_value=.7
    SCENE.world=world
    data=bpy.data.lights.new('Clouded_Daylight','AREA');data.energy=1300;data.shape='DISK';data.size=12
    light=bpy.data.objects.new('Clouded_Daylight',data);col.objects.link(light);light.location=(1,-6,9)
    data=bpy.data.lights.new('Soft_Overcast_Sun','SUN');data.energy=.65;data.angle=math.radians(25)
    light=bpy.data.objects.new('Soft_Overcast_Sun',data);col.objects.link(light);light.rotation_euler=(.35,-.4,-.3)
    for side in (-1,1):
        data=bpy.data.lights.new('Window_Daylight_'+str(side),'AREA')
        data.energy=140;data.shape='RECTANGLE';data.size=2.4;data.size_y=1.5;data.specular_factor=0
        light=bpy.data.objects.new(data.name,data);col.objects.link(light);light.parent=ROOT
        light.visible_glossy=False
        light.location=(side*2,.1,2.1)
        light.rotation_euler=(Vector((0,.2,.75))-light.location).to_track_quat('-Z','Y').to_euler()

def setup():
    global SCENE,ROOT,M,FONT
    source=bpy.data.scenes['Street_Buildings']
    SCENE=bpy.data.scenes.new('Sedan_Rear_Passenger_Cutscene')
    bpy.context.window.scene=SCENE
    SCENE.unit_settings.system='METRIC';SCENE.unit_settings.scale_length=1
    for name in ['00_Car_Root','01_Glass_and_Body','02_Seats','03_Door_Panels','04_Dashboard_and_Controls','05_Roof_and_Carpet','06_Belts_and_Small_Details','07_Passenger_Camera_Rig','08_Street_Context']:
        COL[name]=bpy.data.collections.new('Sedan_'+name);SCENE.collection.children.link(COL[name])
    ROOT=bpy.data.objects.new('SEDAN_2000_MOVE_CAR',None);COL['00_Car_Root'].objects.link(ROOT)
    ROOT.rotation_euler.z=math.pi/2
    ROOT.empty_display_type='CUBE';ROOT.empty_display_size=.3
    ROOT['vehicle_length_m']=4.86;ROOT['vehicle_width_m']=1.96;ROOT['vehicle_roof_height_m']=1.54
    FONT=bpy.data.fonts.load('C:/Windows/Fonts/arial.ttf')
    M={
        'fabric':fabric(),'clothborder':material('Seat_Bolsters',(.15,.147,.13),.9,grain=380),
        'trim':material('Warm_Gray_Trim',(.20,.19,.16),.66,grain=260),
        'plastic':material('Graphite_Plastic',(.058,.063,.062),.61,grain=320),
        'dash':material('Padded_Dashboard',(.085,.092,.088),.73,grain=430),
        'radio':material('Radio_Plastic',(.028,.034,.033),.53,grain=260),
        'leather':material('Worn_Matte_Leather',(.065,.068,.062),.62,grain=510),
        'headliner':material('Headliner_Felt',(.34,.325,.285),.94,grain=320),
        'carpet':material('Charcoal_Carpet',(.065,.071,.065),.98,grain=360),
        'rubber':material('Black_Rubber',(.012,.016,.015),.85),
        'chrome':material('Brushed_Metal',(.45,.49,.50),.28,.9),
        'metal':material('Dark_Metal',(.09,.105,.11),.45,.75),
        'mirror':material('Mirror',(.91,.94,.96),.025,1),
        'belt':material('Woven_Seatbelt',(.045,.048,.044),.91,grain=500),
        'seam':material('Upholstery_Thread',(.11,.108,.092),.93),
        'ink':material('Warm_White_Lettering',(.72,.76,.68),.55),
        'dimink':material('Faded_Gray_Lettering',(.24,.28,.25),.65),
        'lcd':material('Dark_Green_LCD',(.01,.034,.021),.5),
        'lcdtext':material('LCD_Digits',(.20,.48,.23),.6),
        'red':material('Faded_Red',(.48,.045,.027),.57),
        'blue':material('Climate_Blue',(.03,.15,.43),.6),
        'dial':material('Analog_Gauge_Face',(.008,.013,.013),.7),
        'lamp':material('Dome_Diffuser',(.57,.55,.43),.45),
        'label':material('Visor_Safety_Label',(.48,.44,.31),.8),
        'copper':material('Heater_Traces',(.12,.07,.025),.5,.3),
        'paint':material('Muted_Silver_Green_Paint',(.18,.23,.21),.36,.55),
        'asphalt':material('Road_Asphalt',(.075,.082,.085),.96),
        'concrete':material('Sidewalk',(.32,.315,.285),.94),
        'roadmark':material('Road_Paint',(.62,.53,.25),.9),
    }
    # Thin optical glass with Fresnel reflections, a clear view and no dark alpha tint.
    m=material('Clear_Automotive_Glass',(.94,.985,.98),.035)
    n,l=m.node_tree.nodes,m.node_tree.links
    p=next(a for a in n if a.type=='BSDF_PRINCIPLED')
    p.inputs['Metallic'].default_value=1
    transparent=n.new('ShaderNodeBsdfTransparent')
    geometry=n.new('ShaderNodeNewGeometry')
    dot=n.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT'
    l.new(geometry.outputs['Normal'],dot.inputs[0]);l.new(geometry.outputs['Incoming'],dot.inputs[1])
    absolute=n.new('ShaderNodeMath');absolute.operation='ABSOLUTE';l.new(dot.outputs['Value'],absolute.inputs[0])
    complement=n.new('ShaderNodeMath');complement.operation='SUBTRACT';complement.inputs[0].default_value=1;l.new(absolute.outputs[0],complement.inputs[1])
    power=n.new('ShaderNodeMath');power.operation='POWER';power.inputs[1].default_value=5;l.new(complement.outputs[0],power.inputs[0])
    fresnel=n.new('ShaderNodeMath');fresnel.operation='MULTIPLY_ADD';fresnel.inputs[1].default_value=.965;fresnel.inputs[2].default_value=.035;l.new(power.outputs[0],fresnel.inputs[0])
    mix=n.new('ShaderNodeMixShader')
    l.new(fresnel.outputs[0],mix.inputs[0]);l.new(transparent.outputs[0],mix.inputs[1]);l.new(p.outputs[0],mix.inputs[2])
    output=next(a for a in n if a.type=='OUTPUT_MATERIAL');l.new(mix.outputs[0],output.inputs['Surface'])
    methods=[i.identifier for i in m.bl_rna.properties['surface_render_method'].enum_items]
    m.surface_render_method=next((i for i in methods if 'DITHER' in i),methods[0])
    M['glass']=m
    shell();seat('Driver_Seat',-.43);seat('Passenger_Seat',.43);rear_bench()
    for side in (-1,1):
        door(side,True);door(side,False)
    dash_surface();belts();upper_details()
    # Mirror the layout to place the driver on the left when facing vehicle forward -Y.
    # Lettering retains its readable character orientation.
    reflection=Matrix.Diagonal((-1,1,1,1))
    for obj in list(SCENE.objects):
        if obj.parent==ROOT:
            if obj.type=='FONT':
                obj.location.x=-obj.location.x
            else:
                obj.matrix_basis=reflection @ obj.matrix_basis
    head=rig();environment(source)
    SCENE.render.engine='CYCLES'
    SCENE.cycles.samples=32;SCENE.cycles.use_denoising=True
    SCENE.cycles.max_bounces=8;SCENE.cycles.transparent_max_bounces=12
    SCENE.render.resolution_x=1400;SCENE.render.resolution_y=900;SCENE.render.resolution_percentage=100
    SCENE.render.image_settings.file_format='PNG'
    SCENE.view_settings.view_transform='AgX'
    SCENE.view_settings.exposure=.5
    SCENE.render.filepath=str(OUT/'Passenger_Forward.png')
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_perspective='CAMERA'
                area.spaces.active.clip_start=.02
                area.spaces.active.clip_end=300
                area.spaces.active.shading.type='MATERIAL'
    bpy.ops.object.select_all(action='DESELECT')
    head.select_set(True);bpy.context.view_layer.objects.active=head
    bpy.context.view_layer.update()
    print('Sedan built',len([o for o in SCENE.objects if o.parent==ROOT]),'editable main objects')
    bpy.app.driver_namespace['sedan']={'scene':SCENE,'root':ROOT,'head':head,'collections':COL,'materials':M}
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Sedan_2000_Rear_Passenger.blend'))

if __name__=='__main__':
    setup()
