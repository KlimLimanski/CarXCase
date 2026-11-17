"""Consolidate repeated small details and organise independently editable assemblies."""
import bpy
from mathutils import Matrix

state=bpy.app.driver_namespace['sedan']
root=state['root']
scene=state['scene']

def join_details(prefix,name):
    objects=[o for o in scene.objects if o.name.startswith(prefix) and o.type in {'MESH','FONT'}]
    if len(objects)<2:
        return
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.convert(target='MESH')
    bpy.ops.object.join()
    bpy.context.object.name=name

for prefix,name in [
    ('MPH_Tick_','Speedometer_Tick_Marks'),('RPM x1000_Tick_','Tachometer_Tick_Marks'),
    ('MPH_Number_','Speedometer_Numbers'),('RPM x1000_Number_','Tachometer_Numbers'),
    ('Dash_Vent_Louvre','Dashboard_Vent_Louvres'),('Defroster_Slat','Windshield_Defroster_Grilles'),
    ('Left_Front_Grille_','Left_Front_Speaker_Grille'),('Right_Front_Grille_','Right_Front_Speaker_Grille'),
    ('Left_Rear_Grille_','Left_Rear_Speaker_Grille'),('Right_Rear_Grille_','Right_Rear_Speaker_Grille'),
    ('Rear_Defroster_Trace_','Rear_Glass_Heating_Traces'),('Temperature_Scale','Climate_Temperature_Scale'),
    ('Preset_Number_','Radio_Preset_Numbers'),('Climate_Control_Label_','Climate_Control_Legends'),
    ('Rear_Console_Seam','Rear_Console_Vents'),('Pedal_Tread','Pedal_Grip_Ribs'),
    ('Road_Center_Dash','Review_Road_Centre_Markings'),
    ('Driver_Seat_Cushion_Stitch_','Driver_Seat_Cushion_Stitching'),
    ('Passenger_Seat_Cushion_Stitch_','Passenger_Seat_Cushion_Stitching'),
    ('Rear_Upholstery_Stitch','Rear_Bench_Cushion_Stitching'),
    ('Mat_Binding','Floor_Mat_Binding'),
]:
    join_details(prefix,name)

for prefix,col in [('Driver_Seat_','02_Seats'),('Passenger_Seat_','02_Seats'),
                   ('Rear_Bench_','02_Seats'),('Left_Front_','03_Door_Panels'),
                   ('Right_Front_','03_Door_Panels'),('Left_Rear_','03_Door_Panels'),
                   ('Right_Rear_','03_Door_Panels')]:
    objects=[o for o in scene.objects if o.parent==root and o.name.startswith(prefix)]
    empty=bpy.data.objects.new(prefix+'ASSEMBLY',None)
    state['collections'][col].objects.link(empty)
    empty.parent=root
    empty.empty_display_size=.06
    for obj in objects:
        basis=obj.matrix_basis.copy()
        obj.parent=empty
        obj.matrix_basis=basis

# Save the camera controls as the initial selection.
bpy.ops.object.select_all(action='DESELECT')
head=state['head']
head.select_set(True)
bpy.context.view_layer.objects.active=head
head['yaw_degrees']=0.0
head['pitch_degrees']=-4.0
head.update_tag()
bpy.context.view_layer.update()
print('Small details consolidated and assemblies organised')
