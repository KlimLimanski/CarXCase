"""Build the editable harbor and intermediate Unity mesh data from the same modules."""
import json
import math
import random
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
rng = random.Random(1999)
materials = {}
modules = {}
layout = []
lights = []


def surface(name, color, metallic, roughness, grain=0, glow=0):
    materials[name] = dict(name=name, color=list(color), metal=metallic, rough=roughness, grain=grain, glow=glow)


surface("Wet Asphalt", (.105, .122, .143), .03, .26, 1)
surface("Puddle", (.095, .115, .137), .24, .07, 1)
surface("Quay Concrete", (.28, .30, .30), .02, .55, 2)
surface("Old Steel", (.115, .137, .15), .68, .37, 3)
surface("Galvanized Wire", (.21, .23, .23), .75, .41, 3)
surface("Oxide", (.29, .13, .064), .49, .50, 3)
surface("Warehouse Zinc", (.19, .22, .24), .48, .43, 3)
surface("Slate Paint", (.13, .24, .28), .41, .48, 3)
surface("Red Freight Paint", (.31, .143, .105), .37, .47, 3)
surface("Olive Freight Paint", (.25, .26, .18), .38, .51, 3)
surface("Aged Wood", (.29, .216, .149), .01, .63, 4)
surface("Dirty Glass", (.36, .31, .22), .16, .32, 3, .25)
surface("Window Light", (.66, .48, .27), .08, .31, 3, 1.3)
surface("Sodium Diffuser", (1, .74, .44), .04, .32, 0, 4.5)
surface("Faded Lettering", (.67, .64, .54), .02, .66, 2)
surface("Sign Enamel", (.45, .44, .36), .28, .47, 3)
surface("Sign Ink", (.087, .088, .083), .03, .61)
surface("Road Ochre", (.42, .34, .19), .01, .58, 2)
surface("Black Water", (.069, .103, .135), .23, .15, 5)
surface("Tidal Ripples", (.10, .13, .16), .26, .18, 5)
surface("Distant Steel", (.082, .106, .13), .29, .58)
surface("Obstruction Light", (.73, .14, .067), 0, .42, 0, 2.8)


class Module:
    def __init__(self, name):
        self.name = name
        self.parts = {}
        self.colliders = []
        modules[name] = self

    def face(self, mat, points):
        vertices, indices, uv = self.parts.setdefault(mat, ([], [], []))
        offset = len(vertices)
        vertices.extend(points)
        normal = (Vector(points[1]) - Vector(points[0])).cross(Vector(points[2]) - Vector(points[0]))
        axis = max(range(3), key=lambda i: abs(normal[i]))
        a, b = [(2, 1), (0, 2), (0, 1)][axis]
        uv.extend((p[a], p[b]) for p in points)
        for i in range(1, len(points) - 1):
            indices.extend((offset, offset + i, offset + i + 1))

    def box(self, mat, p, size, yaw=0, collision=False):
        x, y, z = (n / 2 for n in size)
        c, s = math.cos(yaw), math.sin(yaw)
        vertices = [(p[0] + a*c + d*s, p[1] + b, p[2] - a*s + d*c)
                    for a,b,d in [(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),
                                  (-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]]
        for f in [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(0,1,5,4),(3,7,6,2)]:
            self.face(mat, [vertices[i] for i in f])
        if collision:
            self.collider(p, size, yaw)

    def collider(self, p, size, yaw=0):
        self.colliders.append(dict(p=list(p), size=list(size), yaw=math.degrees(yaw)))

    def beam(self, mat, a, b, width, depth=None):
        a, b = Vector(a), Vector(b)
        forward = (b-a).normalized()
        if (b-a).length < .0001:
            return
        right = forward.cross(Vector((0, 1, 0)) if abs(forward.y) < .95 else Vector((0, 0, 1))).normalized()
        up = forward.cross(right).normalized()
        u, v = right * width*.5, up*(depth or width)*.5
        ring = [-u-v,u-v,u+v,-u+v]
        self.face(mat, [tuple(a+q) for q in reversed(ring)])
        self.face(mat, [tuple(b+q) for q in ring])
        for i in range(4):
            j = (i+1)%4
            self.face(mat, [tuple(a+ring[i]),tuple(a+ring[j]),tuple(b+ring[j]),tuple(b+ring[i])])

    def tube(self, mat, path, radius, sides=7, closed=False):
        path = [Vector(p) for p in path]
        rings = []
        for i,p in enumerate(path):
            t = path[(i+1)%len(path)]-path[(i-1)%len(path)] if closed else path[min(i+1,len(path)-1)]-path[max(0,i-1)]
            t.normalize()
            horiz = t.cross(Vector((0,1,0)) if abs(t.y) < .95 else Vector((0,0,1))).normalized()
            other = t.cross(horiz).normalized()
            rings.append([tuple(p+radius*(horiz*math.cos(q*math.tau/sides)+other*math.sin(q*math.tau/sides))) for q in range(sides)])
        for i in range(len(path) if closed else len(path)-1):
            n = (i+1)%len(path)
            for q in range(sides):
                r = (q+1)%sides
                self.face(mat, [rings[i][q],rings[i][r],rings[n][r],rings[n][q]])
        if not closed:
            self.face(mat, list(reversed(rings[0])))
            self.face(mat, rings[-1])

    def lathe(self, mat, p, profile, sides=16):
        rings = [[(p[0]+r*math.cos(a*math.tau/sides),p[1]+h,p[2]+r*math.sin(a*math.tau/sides)) for a in range(sides)]
                 for r,h in profile]
        self.face(mat, rings[0])
        self.face(mat, list(reversed(rings[-1])))
        for i in range(len(rings)-1):
            for j in range(sides):
                k=(j+1)%sides
                self.face(mat,[rings[i][j],rings[i+1][j],rings[i+1][k],rings[i][k]])


def put(module, p, yaw=0, name=""):
    layout.append(dict(module=module.name, p=list(p), yaw=yaw, name=name or module.name))


def lamp_at(name, p, intensity=18, extent=12, kind="point"):
    lights.append(dict(name=name, p=list(p), intensity=intensity, extent=extent, kind=kind))


def lettering(module, mat, text, center, height, max_width):
    # Author the lettering as geometry so it survives export and leaves the textures reusable.
    data = bpy.data.curves.new("Sign lettering", "FONT")
    data.body, data.align_x, data.align_y, data.resolution_u = text, "CENTER", "CENTER", 3
    obj = bpy.data.objects.new("Sign lettering", data)
    bpy.context.scene.collection.objects.link(obj)
    deps = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(deps))
    mesh.calc_loop_triangles()
    width = max(v.co.x for v in mesh.vertices) - min(v.co.x for v in mesh.vertices)
    fac = min(height, max_width/max(width, .01))
    for tri in mesh.loop_triangles:
        module.face(mat, [(center[0]+mesh.vertices[i].co.x*fac, center[1]+mesh.vertices[i].co.y*fac, center[2])
                          for i in reversed(tri.vertices)])
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.meshes.remove(mesh)
    bpy.data.curves.remove(data)


def fence(name, x0, x1, h=2.55):
    m = Module(name)
    for x in [x0,x1]:
        m.lathe("Old Steel",(x,0,0),[(.08,0),(.08,h+.10),(.09,h+.11),(.075,h+.15)],10)
    for y in [.15,h,h*.55]:
        m.beam("Old Steel",(x0,y,0),(x1,y,0),.075)
    # Clip individual diagonal strands to the frame bounds.
    for sign in [-1,1]:
        for i in range(-35,85):
            base=x0+i*.155
            ya,yb=sorted(((x0-base)/sign,(x1-base)/sign))
            lo,hi=max(.16,ya),min(h-.03,yb)
            if hi-lo > .025:
                m.beam("Galvanized Wire",(base+sign*lo,lo,.007*sign),(base+sign*hi,hi,.007*sign),.013)
    m.collider(((x0+x1)/2,h/2,0),(x1-x0+.13,h,.15))
    return m


gate_l = fence("Gate Leaf Left",0,2.16,2.7)
gate_r = fence("Gate Leaf Right",-2.16,0,2.7)
gate_l.beam("Oxide",(.04,.25,-.042),(2.12,2.59,-.042),.055)
gate_r.beam("Oxide",(-.04,.25,-.042),(-2.12,2.59,-.042),.055)
put(gate_l,(-2.23,0,0))
put(gate_r,(2.23,0,0))
panel = fence("Chain Link Panel",-2,2)
short = fence("Chain Link Panel Short",-1,1)
for x in [-12.23,-8.23,-4.23,4.23,9.75,13.75]:
    put(panel,(x,0,0))
put(short,(16.75,0,0))
put(short,(7.04,0,0))
for z in [-6,-2,2,6,10,14,18,22,26,30,34,38,42,46,50,54,58]:
    put(panel,(-14.20,0,z),90)
    put(panel,(16.8,0,z),90)
for x in [-12,-8,-4,0,4,8,12,16]:
    put(panel,(x,0,-8),0)

chain = Module("Heavy Gate Chain")
for i in range(10):
    x=-.255+i*.056
    y=1.33-.085*math.sin(i*math.pi/9)
    path=[(x+.057*math.cos(t),y+.038*math.sin(t)*math.cos(i*math.pi*.47),
           -.13+.038*math.sin(t)*math.sin(i*math.pi*.47)) for t in [j*math.tau/14 for j in range(14)]]
    chain.tube("Oxide",path,.014,7,True)
put(chain,(0,0,0))
lock = Module("Rusty Padlock")
lock.box("Oxide",(0,0,0),(.27,.25,.12))
lock.box("Old Steel",(0,-.103,0),(.275,.042,.125))
lock.box("Sign Enamel",(0,.013,-.063),(.232,.182,.012))
lock.box("Sign Ink",(0,-.019,-.074),(.03,.042,.006))
lock.lathe("Sign Ink",(0,.013,-.077),[(.023,-.007),(.023,.007)],12)
lock.tube("Old Steel",[(-.083,.08,0),(-.083,.19,0)]+[(.083*math.cos(t),.19+.078*math.sin(t),0)
          for t in [math.pi*(1-i/10) for i in range(1,11)]]+[(.083,.08,0)],.019,10)
for x in [-.091,.091]:
    for y in [-.055,.064]:
        lock.box("Old Steel",(x,y,-.072),(.019,.019,.013))
put(lock,(.015,1.085,-.175))

sign = Module("Port Authority Sign")
sign.box("Old Steel",(0,0,0),(1.50,.73,.04))
sign.box("Sign Enamel",(0,0,-.025),(1.42,.65,.021))
lettering(sign,"Sign Ink","BRIGHTON BEACH",(0,.146,-.038),.166,1.24)
lettering(sign,"Sign Ink","PORT AUTHORITY",(0,-.033,-.038),.175,1.24)
lettering(sign,"Sign Ink","RESTRICTED ACCESS",(0,-.228,-.038),.111,1.17)
for x in [-.65,.65]:
    for y in [-.27,.27]:
        sign.box("Oxide",(x,y,-.045),(.04,.04,.021))
put(sign,(1.14,1.92,-.086))

access = Module("Controlled Access Sign")
access.box("Old Steel",(0,0,0),(.86,.35,.045))
access.box("Sign Enamel",(0,0,-.027),(.81,.30,.015))
lettering(access,"Sign Ink","CONTROLLED",(0,.045,-.037),.11,.75)
lettering(access,"Sign Ink","ACCESS",(0,-.074,-.037),.107,.74)
put(access,(-1.34,1.91,-.089))

terminal = Module("Gate Code Keypad")
terminal.box("Old Steel",(0,0,0),(.285,.39,.13))
terminal.box("Oxide",(0,0,-.071),(.248,.351,.031))
terminal.box("Sign Ink",(0,.097,-.091),(.18,.054,.015))
terminal.box("Obstruction Light",(.062,.098,-.101),(.023,.013,.009))
for row in range(4):
    for col in range(3):
        terminal.box("Faded Lettering",((col-1)*.053,.035-row*.047,-.096),(.029,.027,.019))
terminal.box("Old Steel",(0,0,.114),(.085,.11,.13))
put(terminal,(.13,1.43,-.12))

booth = Module("Security Booth")
booth.box("Quay Concrete",(0,.06,0),(2.85,.12,2.95),collision=True)
booth.box("Warehouse Zinc",(0,.53,0),(2.66,.92,2.72),collision=True)
booth.box("Old Steel",(0,2.32,0),(2.9,.25,2.95),collision=True)
booth.box("Warehouse Zinc",(0,2.48,.06),(3.03,.12,3.06),-.026)
for x in [-1.24,1.24]:
    for z in [-1.25,1.25]:
        booth.box("Old Steel",(x,1.54,z),(.13,1.58,.13))
for i in range(15):
    x=(i-7)*.17
    booth.box("Old Steel",(x,.55,-1.374),(.028,.91,.033))
for z in [-1.30,1.30]:
    booth.box("Window Light" if z < 0 else "Dirty Glass",(0,1.51,z),(2.42,1.08,.035))
    for x in [-.81,0,.81]:
        booth.box("Old Steel",(x,1.51,z*1.027),(.047,1.10,.072))
    booth.box("Old Steel",(0,1.55,z*1.027),(2.41,.043,.07))
for x in [-1.30,1.30]:
    booth.box("Dirty Glass",(x,1.51,0),(.035,1.08,2.42))
    for z in [-.80,0,.80]:
        booth.box("Old Steel",(x*1.027,1.51,z),(.07,1.10,.046))
booth.box("Warehouse Zinc",(-.64,1.10,1.361),(.90,2.06,.08))
booth.box("Old Steel",(-.94,1.08,1.415),(.08,.08,.05))
lettering(booth,"Faded Lettering","SECURITY",(0,.54,-1.396),.22,1.55)
put(booth,(-4.05,0,-3.35))
lamp_at("Booth interior",(-4.05,1.62,-4.74),4,4)


def container(name, paint):
    m = Module(name)
    m.box(paint,(0,1.31,0),(2.35,2.48,6.00),collision=True)
    for x in [-1.22,1.22]:
        for j in range(24):
            z=(j-11.5)*.248
            m.box(paint,(x,1.30,z),(.105,2.29,.082))
            if rng.random()<.58:
                m.box("Oxide",(x*1.046,.20+rng.uniform(0,.27),z),(.012,rng.uniform(.19,.52),.047))
        for y in [.12,2.52]:
            m.box("Old Steel",(x,y,0),(.11,.11,6.17))
    for x in [-1.18,1.18]:
        for z in [-3.03,3.03]:
            m.box("Old Steel",(x,1.31,z),(.15,2.62,.16))
            for h in [.10,2.51]:
                m.box("Oxide",(x,h,z),(.19,.19,.22))
    for i in range(11):
        x=(i-5)*.205
        m.box(paint,(x,1.30,-3.045),(.08,2.23,.095))
        m.box(paint,(x,2.58,0),(.075,.064,5.93))
    for x in [-.87,-.30,.30,.87]:
        m.lathe("Old Steel",(x,.25,-3.135),[(.026,0),(.026,2.10)],8)
        for h in [.42,2.18]:
            m.box("Oxide",(x,h,-3.125),(.14,.10,.055))
    m.box("Faded Lettering",(.60,1.77,-3.095),(.51,.14,.024))
    m.box("Oxide",(0,.28,3.022),(2.31,.21,.052))
    return m


red=container("Container Oxide Red","Red Freight Paint")
blue=container("Container Desaturated Blue","Slate Paint")
olive=container("Container Faded Olive","Olive Freight Paint")
for module,x,y,z,a in [
    (red,-7.1,0,5,3),(blue,-7.55,0,11.4,0),(red,-7.55,2.65,11.4,0),
    (olive,8.1,0,5.45,-4),(red,7,0,13,3),(blue,7,2.65,13,3),
    (blue,-6.8,0,25.3,4),(olive,-6.95,2.65,25.25,6),(red,-7.4,0,33.3,-2),
    (olive,8.65,0,25.5,90),(blue,7.7,0,33.5,4),(red,7.7,2.65,33.5,4),
    (red,-7.5,0,43,90),(blue,8.5,0,43,90)]:
    put(module,(x,y,z),a)

barrier=Module("Jersey Barrier")
profile=[(-.43,0),(-.43,.16),(-.23,.39),(-.155,.89),(.155,.89),(.23,.39),(.43,.16),(.43,0)]
for i in range(len(profile)):
    a,b=profile[i],profile[(i+1)%len(profile)]
    barrier.face("Quay Concrete",[(-1.22,b[1],b[0]),(1.22,b[1],b[0]),(1.22,a[1],a[0]),(-1.22,a[1],a[0])])
barrier.face("Quay Concrete",[(-1.22,y,z) for z,y in profile][::-1])
barrier.face("Quay Concrete",[(1.22,y,z) for z,y in profile])
barrier.collider((0,.43,0),(2.44,.86,.82))
for x in [-.86,-.38,.10,.58]:
    barrier.face("Red Freight Paint",[(x,.42,-.226),(x+.18,.42,-.226),(x+.33,.83,-.166),(x+.15,.83,-.166)])
for x,z,a in [(-2.8,-5,-11),(4.25,-1,-8),(-3.95,6,84),(4.8,9,93),(-4.35,17,74),
              (-4.5,19.9,83),(4.9,21,92),(5.08,28,85),(-4.1,37,87),(5.25,39,91)]:
    put(barrier,(x,0,z),a)

pallet=Module("Wooden Pallet")
for z in [-.4,0,.4]:
    pallet.box("Aged Wood",(0,.038,z),(1.17,.076,.10))
    for x in [-.47,0,.47]:
        pallet.box("Aged Wood",(x,.108,z),(.13,.09,.12))
for i in range(6):
    pallet.box("Aged Wood",(0,.18,(i-2.5)*.177),(1.23,.052,.14))
pallet.collider((0,.10,0),(1.23,.20,1.0))
pallet_stack=Module("Leaning Pallet")
for i in range(6):
    h=.13+i*.171
    pallet_stack.box("Aged Wood",(0,h,.2*h),(1.19,.125,.06))
for x in [-.48,0,.48]:
    pallet_stack.beam("Aged Wood",(x,.05,.069),(x,1.08,.271),.09,.08)
pallet_stack.collider((0,.54,.12),(1.2,1.08,.30))
for x,z,a in [(5,1,12),(-5.5,12,-8),(5.5,16,8),(-5.5,24,-9),(5.5,30,17),(-5.4,43,3),(4.7,46,-14)]:
    put(pallet,(x,0,z),a)
    put(pallet_stack,(x+(.75 if x>0 else -.75),0,z+.55),a+5)


def drum(name, mat):
    m=Module(name)
    m.lathe(mat,(0,0,0),[(.254,0),(.30,.055),(.30,.80),(.27,.86),(.25,.86)],16)
    for y in [.07,.252,.605,.79]:
        m.lathe("Oxide",(0,y,0),[(.301,0),(.312,.015),(.312,.035),(.299,.046)],16)
    m.lathe("Old Steel",(.11,.865,.06),[(.059,0),(.059,.023)],10)
    m.box("Sign Enamel",(0,.54,-.298),(.17,.14,.014))
    m.collider((0,.43,0),(.61,.86,.61))
    return m


drums=[drum("Blue Steel Drum","Slate Paint"),drum("Oxide Steel Drum","Red Freight Paint")]
for x,z in [(-3.23,-4.3),(4.95,3),(-4.5,11.4),(5.4,19),(-4.8,28),(5.5,34),(-5.2,45),(4.0,45.8)]:
    for i in range(2 if x<0 else 3):
        put(drums[i%2],(x+i*.63,0,z+rng.uniform(-.3,.3)),rng.uniform(-20,20))

utility=Module("Industrial Utility Cabinet")
utility.box("Quay Concrete",(0,.07,0),(1.02,.14,.59),collision=True)
utility.box("Slate Paint",(0,.69,0),(.91,1.16,.48),collision=True)
utility.box("Old Steel",(0,1.30,0),(1.00,.095,.53))
utility.box("Oxide",(.39,.71,-.257),(.042,.14,.035))
for y in [.98,1.05,1.12]:
    utility.box("Sign Ink",(-.13,y,-.245),(.36,.022,.012))
for x,z in [(-5.7,-4.8),(6.5,11),(-4.8,20),(5.8,40),(-5.1,46)]:
    put(utility,(x,0,z),0)

lamp=Module("Sodium Streetlight")
lamp.lathe("Quay Concrete",(0,0,0),[(.20,0),(.20,.18)],12)
lamp.lathe("Old Steel",(0,0,0),[(.117,0),(.117,.48),(.074,.64),(.05,4.13)],12)
lamp.tube("Old Steel",[(0,4.05,0),(.10,4.32,0),(.31,4.48,0),(.61,4.52,0),(.84,4.43,0),(.98,4.24,0)],.047,8)
lamp.lathe("Old Steel",(.98,4.15,0),[(.26,0),(.25,.065),(.16,.14),(.081,.185)],14)
lamp.lathe("Sodium Diffuser",(.98,4.134,0),[(.188,0),(.19,.027)],14)
lamp.collider((0,1.99,0),(.26,3.98,.26))
for x,z in [(-2.75,-1.25),(3.95,6.8),(-3.88,14.4),(4.26,22.7),(-3.9,30.1),(4.4,38.2),(-5.15,44.5)]:
    sign_x=1 if x<0 else -1
    put(lamp,(x,0,z),0 if x<0 else 180)
    lamp_at("Sodium street pool",(x+sign_x*.98,4.02,z),68,13,"spot")

road=Module("Quay and Road")
road.box("Quay Concrete",(1,-.47,26),(33,.9,70),collision=True)
road.box("Wet Asphalt",(.15,-.035,24),(12,.10,67))
for x in [-8.8,9.1,-13.9,13.45]:
    road.box("Quay Concrete",(x,.06,24),(1.2,.14,66))
for j in range(50):
    z=-5+j*1.15
    pilot=.36*math.sin(z*.15)+.78*math.exp(-((z-30)/12)**2)
    for x in [-2.83,2.83]:
        road.box("Road Ochre",(x+pilot,.019,z),(.075,.007,.78),.04*math.cos(z*.12))
    for x in [-.70,.70]:
        road.box("Old Steel",(x+pilot,.025,z),(.052,.023,1.18),.042*math.cos(z*.15))
    if j%7==0:
        for m in range(7):
            road.box("Old Steel",(-3.15+m*.045,.019,z),(.025,.014,.43))
for i in range(106):
    x=rng.uniform(-5.45,5.45)
    z=rng.uniform(-6,49)
    n=rng.randrange(9,15)
    sx,sz=rng.uniform(.21,.82),rng.uniform(.41,1.85)
    p=[(x+sx*math.cos(t)*rng.uniform(.78,1.15),.022+i*.000002,z+sz*math.sin(t)*rng.uniform(.80,1.11))
       for t in [j*math.tau/n for j in range(n)]]
    road.face("Puddle",p[::-1])
put(road,(0,0,0))

warehouse=Module("Pier 3 Warehouse Exterior")
W,H,D=18,6.85,14
for x in [-9,9]:
    warehouse.box("Warehouse Zinc",(x,H/2,D/2),(.22,H,D),collision=True)
    for j in range(52):
        z=j*.268+.14
        warehouse.box("Warehouse Zinc",(x*1.014,H/2,z),(.135,H,.078))
warehouse.box("Warehouse Zinc",(0,H/2,14),(18,H,.25),collision=True)
warehouse.box("Warehouse Zinc",(0,H/2,0),(18,H,.28),collision=True)
for j in range(67):
    x=(j-33)*.267
    warehouse.box("Warehouse Zinc",(x,H/2,-.166),(.078,H,.092))
    top=H+2.28*(1-abs(x)/9)
    warehouse.box("Warehouse Zinc",(x,(top+H)/2,-.154),(.266,top-H,.25))
    if rng.random()<.65:
        warehouse.box("Oxide",(x,rng.uniform(.16,.47),-.222),(.055,rng.uniform(.18,.38),.021))
for i in [-1,1]:
    a=(-.03,9.17,0) if i==-1 else (.03,9.17,0)
    b=(i*9.3,6.73,0)
    warehouse.face("Warehouse Zinc",[(a[0],a[1],-.57),(b[0],b[1],-.57),
                                    (b[0],b[1],14.55),(a[0],a[1],14.55)][::-i])
    for j in range(36):
        z=-.48+j*.42
        warehouse.beam("Old Steel",(a[0],a[1]+.03,z),(b[0],b[1]+.03,z),.039)
    warehouse.beam("Old Steel",(a[0],a[1],-.58),(b[0],b[1],-.58),.16)
    warehouse.beam("Old Steel",(b[0],b[1],-.58),(b[0],b[1],14.58),.14)
warehouse.box("Old Steel",(0,9.18,7),(.20,.13,15.2))

for x in [-1.58,1.58]:
    warehouse.box("Old Steel",(x,1.91,-.34),(3.12,3.82,.18))
    for i in range(16):
        v=x+(i-7.5)*.185
        warehouse.box("Warehouse Zinc",(v,1.91,-.456),(.134,3.59,.062))
    for y in [.16,1.83,3.65]:
        warehouse.box("Old Steel",(x,y,-.52),(3.12,.115,.09))
    warehouse.beam("Old Steel",(x-1.50,.21,-.54),(x+1.5,3.57,-.54),.09)
    warehouse.beam("Old Steel",(x+1.50,.21,-.54),(x-1.5,3.57,-.54),.09)
for x in [-3.19,0,3.19]:
    warehouse.box("Oxide",(x,1.93,-.53),(.072,3.85,.12))
warehouse.box("Old Steel",(0,3.925,-.41),(6.72,.19,.22))
warehouse.box("Old Steel",(0,.052,-.58),(6.85,.095,.24))
warehouse.box("Oxide",(5.65,1.15,-.39),(1.12,2.27,.15))
warehouse.box("Slate Paint",(5.65,1.14,-.48),(.98,2.11,.067))
warehouse.box("Sign Enamel",(5.35,1.03,-.525),(.11,.04,.04))
warehouse.box("Dirty Glass",(5.65,1.66,-.52),(.32,.33,.013))

for x in [-4.35,-1.45,1.45,4.35]:
    warehouse.box("Old Steel",(x,5.63,-.235),(2.68,1.23,.17))
    for i in range(8):
        for j in range(3):
            m="Window Light" if rng.random()<.49 else "Dirty Glass"
            warehouse.box(m,(x+(i-3.5)*.317,5.64+(j-1)*.35,-.32),(.289,.318,.029))
for x in [-2.45,2.45,5.65]:
    y=4.28 if x<5 else 2.83
    warehouse.box("Old Steel",(x,y,-.27),(.35,.16,.27))
    warehouse.box("Sodium Diffuser",(x,y-.087,-.32),(.265,.038,.17))
    lamp_at("Warehouse wall pack",(x,y-.18,46.94),48,10,"spot")
lettering(warehouse,"Faded Lettering","PIER 3",(5.60,3.74,-.239),1.35,3.1)
put(warehouse,(0,0,47.55))

water=Module("Harbor Basin")
water.box("Black Water",(-47,-.72,24),(90,.07,140))
for i in range(150):
    x,z=rng.uniform(-68,-15.7),rng.uniform(-32,71)
    water.box("Tidal Ripples",(x,-.677+rng.uniform(0,.018),z),(rng.uniform(.35,3.1),.008,rng.uniform(.029,.12)),rng.uniform(-.18,.18))
put(water,(0,0,0))

ship=Module("Moored Cargo Ship")
for i in range(9):
    width=6.5 if i in range(1,8) else 3.3
    ship.box("Distant Steel",(0,.68,(i-4)*3.4),(width,2.65,3.44))
    ship.box("Oxide",(0,-.50,(i-4)*3.4),(width+.06,.36,3.44))
ship.box("Distant Steel",(0,2.32,0),(6.27,.32,26.1))
for i in range(3):
    ship.box("Warehouse Zinc",(0,2.85+i*.93,-7.65+i*.43),(5.23-i*.53,.89,3.7))
    for z in [-9.48,-8.63,-7.76,-6.89]:
        ship.box("Dirty Glass",(2.625-i*.265,2.98+i*.93,z+i*.43),(.033,.239,.39))
for i in range(5):
    ship.box("Distant Steel",(0,2.9,(i-1)*3.35),(4.6,1.13,2.7))
ship.beam("Old Steel",(0,4.8,-7),(0,9,-7),.12)
ship.beam("Old Steel",(-1.27,7.8,-7),(1.27,7.8,-7),.08)
ship.box("Obstruction Light",(0,9.07,-7),(.19,.12,.16))
put(ship,(-29,-.65,27),-10)

crane=Module("Lattice Dock Crane")
for x in [-1.28,1.28]:
    for z in [-1.21,1.21]:
        crane.beam("Distant Steel",(x,0,z),(x*.47,11.8,z*.47),.27)
    for i in range(5):
        ya,yb=i*2.26,(i+1)*2.26
        crane.beam("Distant Steel",(x*(1-ya*.044),ya,-1.22*(1-ya*.044)),
                   (x*(1-yb*.044),yb,1.22*(1-yb*.044)),.10)
crane.box("Distant Steel",(0,11.58,0),(2.60,1.19,2.40))
for x in [-.43,.43]:
    crane.beam("Distant Steel",(x,12.01,0),(x,21.5,13),.17)
    crane.beam("Distant Steel",(x,12.74,0),(x,22.0,13),.17)
    for i in range(7):
        crane.beam("Distant Steel",(x,12.01+i*9.49/7,i*13/7),(x,12.74+(i+1)*9.26/7,(i+1)*13/7),.08)
crane.beam("Distant Steel",(0,21.58,13),(0,4.94,13),.037)
crane.box("Distant Steel",(0,4.84,13),(.32,.31,.26))
crane.box("Obstruction Light",(0,22.12,13),(.20,.14,.18))
put(crane,(-19,-.44,10),12)
put(crane,(-30,-.58,44),-21)
put(crane,(-23,-.38,67),29)

city=Module("Distant Harbor Skyline")
for i in range(45):
    x=(i-27)*3+rng.uniform(-.9,.9)
    z=82+rng.uniform(-15,39)
    h=rng.uniform(3,17)
    w=rng.uniform(2.2,5.2)
    city.box("Distant Steel",(x,h/2-1,z),(w,h,rng.uniform(3.9,7.2)))
    if i%4==0:
        city.box("Distant Steel",(x,h-.38,z),(.9,1.2,.72))
    for floor in range(int(h/1.15)):
        for side in [-1,0,1]:
            if rng.random()<.29:
                city.box("Dirty Glass",(x+side*w*.26,.34+floor*1.12,z-3.66),(.18,.19,.025))
for x in [-59,-20]:
    for d in [-.64,.64]:
        city.beam("Distant Steel",(x+d,-.9,76),(x+d,15.2,76),.5)
    city.beam("Distant Steel",(x-.64,11.5,76),(x+.64,11.5,76),.38)
city.beam("Distant Steel",(-77,6.3,76),(-10,6.3,76),.50)
for i in range(24):
    x=-59+i*39/23
    y=15.1-6.8*math.sin(i*math.pi/23)
    if i:
        city.beam("Distant Steel",(last_x,last_y,76),(x,y,76),.06)
    city.beam("Distant Steel",(x,6.35,76),(x,y,76),.034)
    city.box("Dirty Glass",(x,6.66,75.75),(.17,.16,.08))
    last_x,last_y=x,y
put(city,(0,0,0))

shots = [
    ("01 Entrance",(-.86,1.74,-5.75),(.1,1.2,17),53),
    ("02 Elevated Overview",(15,14.5,-9),(0,1.8,25),64),
    ("03 Harbor Road",(1.25,1.83,14),(.20,2.27,49),57),
    ("04 Warehouse Approach",(.96,1.86,35),(.10,3.6,48.2),59),
]


def blender_point(p):
    return p[0], p[2], p[1]


def model_blender():
    scene=bpy.context.scene
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj,do_unlink=True)
    groups={}
    for name in ["Entrance & Security", "Service Road & Quay", "Warehouse Exterior", "Water & Skyline", "Lighting & Cameras"]:
        groups[name]=bpy.data.collections.new(name)
        scene.collection.children.link(groups[name])

    bm={}
    for name,data in materials.items():
        mat=bpy.data.materials.new(name)
        mat.diffuse_color=(*data["color"],1)
        mat.use_nodes=True
        bsdf=mat.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value=(*data["color"],1)
        bsdf.inputs["Metallic"].default_value=data["metal"]
        bsdf.inputs["Roughness"].default_value=data["rough"]
        bsdf.inputs["Emission Color"].default_value=(*data["color"],1)
        bsdf.inputs["Emission Strength"].default_value=data["glow"]
        if data["grain"]:
            nodes=mat.node_tree.nodes
            links=mat.node_tree.links
            coords=nodes.new("ShaderNodeTexCoord")
            noise=nodes.new("ShaderNodeTexNoise")
            noise.inputs["Scale"].default_value=15 if data["grain"] < 3 else 8
            links.new(coords.outputs["Object"],noise.inputs["Vector"])
            ramp=nodes.new("ShaderNodeValToRGB")
            ramp.color_ramp.elements[0].position=.19
            ramp.color_ramp.elements[1].position=.82
            ramp.color_ramp.elements[0].color=(*(v*.46 for v in data["color"]),1)
            ramp.color_ramp.elements[1].color=(*(v*1.53 for v in data["color"]),1)
            links.new(noise.outputs["Fac"],ramp.inputs[0])
            links.new(ramp.outputs[0],bsdf.inputs["Base Color"])
            bump=nodes.new("ShaderNodeBump")
            bump.inputs["Strength"].default_value=.24
            bump.inputs["Distance"].default_value=.026
            links.new(noise.outputs["Fac"],bump.inputs["Height"])
            links.new(bump.outputs[0],bsdf.inputs["Normal"])
        bm[name]=mat

    mesh_assets={}
    for name,module in modules.items():
        verts,faces,uvs,slots=[],[],[],[]
        for mat,(v,f,u) in module.parts.items():
            offset=len(verts)
            verts.extend(blender_point(p) for p in v)
            uvs.extend(u)
            faces.extend((offset+f[i],offset+f[i+2],offset+f[i+1]) for i in range(0,len(f),3))
            slots.extend([mat]*(len(f)//3))
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata(verts,[],faces)
        names=list(module.parts)
        for mat in names:
            mesh.materials.append(bm[mat])
        uv=mesh.uv_layers.new(name="Metre Tiles")
        for poly,name_slot in zip(mesh.polygons,slots):
            poly.material_index=names.index(name_slot)
            for i in poly.loop_indices:
                uv.data[i].uv=uvs[mesh.loops[i].vertex_index]
        mesh.update()
        mesh_assets[name]=mesh

    for obj in layout:
        name,p=obj["module"],obj["p"]
        key="Entrance & Security" if p[2]<1 and name!="Quay and Road" else "Service Road & Quay"
        if name=="Pier 3 Warehouse Exterior":
            key="Warehouse Exterior"
        elif name in ("Harbor Basin","Moored Cargo Ship","Lattice Dock Crane","Distant Harbor Skyline"):
            key="Water & Skyline"
        instance=bpy.data.objects.new(obj["name"],mesh_assets[name])
        groups[key].objects.link(instance)
        instance.location=blender_point(p)
        instance.rotation_euler.z=-math.radians(obj["yaw"])

    rig=groups["Lighting & Cameras"]
    for item in lights:
        light=bpy.data.lights.new(item["name"],item["kind"].upper())
        light.color=(1,.66,.38)
        light.energy=item["intensity"]*31
        if item["kind"]=="spot":
            light.spot_size=math.radians(112)
            light.spot_blend=.61
        light.shadow_soft_size=.20
        obj=bpy.data.objects.new(light.name,light)
        rig.objects.link(obj)
        obj.location=blender_point(item["p"])
    moon=bpy.data.lights.new("Diffuse Moon","SUN")
    moon.energy=.76
    moon.color=(.46,.61,1)
    moon.angle=.16
    obj=bpy.data.objects.new(moon.name,moon)
    rig.objects.link(obj)
    obj.rotation_euler=(.49,-.86,.31)
    scene.world=bpy.data.worlds.new("Harbor Overcast")
    scene.world.use_nodes=True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value=(.19,.27,.42,1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value=.21
    for name,p,target,fov in shots:
        camera=bpy.data.cameras.new(name)
        camera.lens_unit="FOV"
        camera.angle=math.radians(fov)
        obj=bpy.data.objects.new(name,camera)
        rig.objects.link(obj)
        obj.location=blender_point(p)
        obj.rotation_euler=(Vector(blender_point(target))-obj.location).to_track_quat("-Z","Y").to_euler()
        if scene.camera is None:
            scene.camera=obj
    scene.render.engine="CYCLES"
    scene.cycles.samples=20
    scene.cycles.use_denoising=True
    scene.render.resolution_x,scene.render.resolution_y=1440,810
    scene.view_settings.view_transform="AgX"
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/"ArtSource/Harbor/Brighton_Harbor.blend"))


def build():
    model_blender()
    data=dict(materials=list(materials.values()),placements=layout,lights=lights,
              shots=[dict(name=n,p=p,target=t,fov=f) for n,p,t,f in shots],
              modules=[dict(name=m.name,colliders=m.colliders,
                            parts=[dict(mat=k,vertices=[s for p in v for s in p],triangles=f,uv=[s for p in u for s in p])
                                   for k,(v,f,u) in m.parts.items()]) for m in modules.values()])
    out=ROOT/"Temp/HarborGeometry.json"
    out.parent.mkdir(exist_ok=True)
    out.write_text(json.dumps(data,separators=(",",":")),encoding="utf-8")
    return dict(modules=len(modules),placements=len(layout),triangles=sum(len(f)//3 for m in modules.values() for v,f,u in m.parts.values()))


if __name__ == "__main__":
    print(build())
