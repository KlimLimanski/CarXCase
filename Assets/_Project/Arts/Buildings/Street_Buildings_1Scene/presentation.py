"""Assemble the asset review scene and car-height camera path."""
import bpy
import math
from mathutils import Vector
from pathlib import Path

ns=bpy.app.driver_namespace['street_builder']
roots=bpy.app.driver_namespace['street_roots']
scene=bpy.context.scene
out=Path('C:/Users/Мария/Documents/ChatGPT/carX/Street_Buildings')
col=bpy.data.collections.new('Review_Street_and_Lighting')
scene.collection.children.link(col)
batch=ns['MeshBatch']('Review_Street',col)
mat=ns['material']
concrete=mat('Review_Concrete',(.32,.315,.285),.95)
asphalt=mat('Review_Asphalt',(.075,.082,.085),.97)
curb=mat('Review_Curb',(.40,.39,.35),.85)
paint=mat('Review_Road_Paint',(.62,.53,.25),.95)
batch.box((0,-1.5,-.13),(52,3,.26),concrete)
batch.box((0,-10,-.34),(65,14,.22),asphalt)
batch.box((0,-3.05,-.08),(52,.15,.16),curb)
for x in range(-25,26):
    batch.box((x,-1.5,.002),(.012,3,.003),asphalt)
for y in (-1,-2):
    batch.box((0,y,.003),(52,.014,.004),asphalt)
for x in range(-30,30,5):
    batch.box((x,-10,-.22),(2.4,.10,.004),paint)
batch.finish(None)
world=bpy.data.worlds.new('Review_Overcast')
world.use_nodes=True
background=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
background.inputs[0].default_value=(.32,.40,.51,1)
background.inputs[1].default_value=.45
scene.world=world
sun_data=bpy.data.lights.new('Late_Afternoon_Sun','SUN')
sun_data.energy=2.0
sun_data.angle=math.radians(12)
sun=bpy.data.objects.new('Late_Afternoon_Sun',sun_data)
col.objects.link(sun)
sun.rotation_euler=(math.radians(32),math.radians(-24),math.radians(-24))
area_data=bpy.data.lights.new('Soft_Facade_Fill','AREA')
area_data.energy=2600
area_data.shape='DISK'
area_data.size=24
area=bpy.data.objects.new('Soft_Facade_Fill',area_data)
col.objects.link(area)
area.location=(0,-10,16)
ns['aim'](area,(0,0,7))
cam_data=bpy.data.cameras.new('Street_Overview')
cam=bpy.data.objects.new('Street_Overview',cam_data)
col.objects.link(cam)
cam.location=(30,-62,23)
ns['aim'](cam,(0,1,8.6))
cam_data.type='PERSP'
cam_data.lens=43
scene.camera=cam
scene.render.resolution_x=1800
scene.render.resolution_y=1050
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.film_transparent=False
scene.view_settings.view_transform='AgX'
car_data=bpy.data.cameras.new('Car_Window_120cm')
car=bpy.data.objects.new('Car_Window_120cm',car_data)
col.objects.link(car)
car_data.lens=24
car_data.clip_start=.05
for frame,x in ((1,-21),(360,21)):
    car.location=(x,-7,.97)
    ns['aim'](car,(x-3,0,3.0))
    car.keyframe_insert(data_path='location',frame=frame)
    car.keyframe_insert(data_path='rotation_euler',frame=frame)
if car.animation_data and car.animation_data.action:
    action=car.animation_data.action
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:
                        key.interpolation='LINEAR'
scene.frame_start=1
scene.frame_end=360
scene.render.fps=24
scene.frame_set(180)
scene['camera_preview']='Car_Window_120cm, frames 1–360, 24 fps, camera height 1.20 m'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.clip_end=1000
            area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.object.select_all(action='DESELECT')
for root in roots:
    root.select_set(True)
bpy.context.view_layer.objects.active=roots[0]
print('Review scene and animated car-height camera ready')
