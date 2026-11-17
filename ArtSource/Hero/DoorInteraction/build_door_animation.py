"""Author and bake the existing Alex rig in Blender, without retargeting its skin."""
from pathlib import Path
import bpy
import math
from mathutils import Vector, Quaternion

project = Path(__file__).resolve().parents[3]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(project / 'Assets/_Project/Arts/Heroes/MainHero_Rigged.glb'))
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
idle = bpy.data.actions['Idle']
rig.animation_data.action = idle
rig.animation_data.action_slot = idle.slots[0]
for track in rig.animation_data.nla_tracks:
    track.mute = True
bpy.context.scene.frame_set(0)
base = {b.name: b.matrix_basis.copy() for b in rig.pose.bones}
rig.animation_data.action = None
scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start, scene.frame_end = 0, 60

def smooth(a, b, t):
    x = max(0., min(1., (t-a)/(b-a)))
    return x*x*(3-2*x)

def rotate_world(b, q):
    m = b.matrix.copy()
    p = m.translation.copy()
    m = q.to_matrix().to_4x4() @ m
    m.translation = p
    b.matrix = m
    bpy.context.view_layer.update()

def solve(target):
    a,b,c = [rig.pose.bones[n] for n in ('R_UpperArm','R_LowerArm','R_Hand')]
    av,bv,cv = a.head.copy(),b.head.copy(),c.head.copy()
    ab,bc = (bv-av).length,(cv-bv).length
    direction = (target-av).normalized()
    dist = max(.02,min((target-av).length,ab+bc-.002))
    pole = Vector((-.65,-.15,.95))-av
    pole = (pole-direction*pole.dot(direction)).normalized()
    along = (ab*ab+dist*dist-bc*bc)/(2*dist)
    elbow = av + direction*along + pole*math.sqrt(max(0,ab*ab-along*along))
    rotate_world(a,(bv-av).rotation_difference(elbow-av))
    rotate_world(b,(c.head-b.head).rotation_difference(target-b.head))

for name, pull in [('Door_Reach_Push',False),('Door_Reach_Pull',True)]:
    action = bpy.data.actions.new(name)
    rig.animation_data.action = action
    for frame in range(61):
        scene.frame_set(frame)
        for b in rig.pose.bones:
            b.matrix_basis = base[b.name].copy()
            b.rotation_mode = 'QUATERNION'
        t = frame/30.
        reach = smooth(.12,.52,t)*(1-smooth(1.04,1.72,t))
        turn = smooth(.58,1.1,t)*(1-smooth(1.1,1.8,t))
        for bone, degrees, axis in [('Spine',-3,'X'),('Chest',-4,'X'),('UpperChest',-5 if pull else 5,'Y'),('Head',3,'X')]:
            b = rig.pose.bones[bone]
            b.rotation_quaternion = b.rotation_quaternion @ Quaternion(Vector({'X':(1,0,0),'Y':(0,1,0)}[axis]), math.radians(degrees)*reach)
        bpy.context.view_layer.update()
        rest = rig.pose.bones['R_Hand'].head.copy()
        handle = Vector((-.26 + .10*turn, -.43 + (.13 if pull else -.09)*turn,1.10))
        if reach > .0001:
            solve(rest.lerp(handle,reach))
        hand=rig.pose.bones['R_Hand']
        hand.rotation_quaternion = hand.rotation_quaternion @ Quaternion(Vector((1,0,0)),math.radians(-18)*reach)
        for b in rig.pose.bones:
            b.keyframe_insert('rotation_quaternion',frame=frame,group=b.name)
            b.keyframe_insert('location',frame=frame,group=b.name)
            b.keyframe_insert('scale',frame=frame,group=b.name)
    action.use_fake_user=True

# Export only the two authored actions. Idle/walk originals remain in the source model.
for action in list(bpy.data.actions):
    if not action.name.startswith('Door_Reach_'):
        bpy.data.actions.remove(action)
rig.animation_data.action=bpy.data.actions['Door_Reach_Push']
rig.animation_data.action_slot=rig.animation_data.action.slots[0]
scene.frame_set(17)
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).parent/'Alex_Door_Interaction.blend'))
bpy.ops.export_scene.gltf(filepath=str(Path(__file__).parent/'Alex_Door_Interaction.glb'),export_format='GLB',export_animations=True,export_animation_mode='ACTIONS',export_force_sampling=True,export_frame_range=True,export_materials='NONE')
print('DOOR ANIMATION EXPORTED')
