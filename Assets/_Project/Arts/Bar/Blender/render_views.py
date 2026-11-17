"""Render reference camera checks and useful detail checks from the final saved scene."""
import bpy,os
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
s=bpy.data.scenes['Bar1999_Reference'];bpy.context.window.scene=s
views=[('Camera_01_Entry','01_Entry',1440,642),('Camera_02_Bar','02_Bar',1440,900),('Camera_03_Reverse','03_Reverse',1152,720),('Camera_04_North','04_North',1000,680)]
s.cycles.samples=64
for name,file,w,h in views:
    s.camera=s.objects[name];s.render.resolution_x=w;s.render.resolution_y=h
    s.render.filepath=ROOT+'/Renders/'+file+'.png';bpy.ops.render.render(write_still=True)
s.camera=s.objects['Camera_01_Entry'];s.render.resolution_x=1440;s.render.resolution_y=642
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Blender/Bar1999.blend')
print('Four reference views rendered and final camera restored.')
