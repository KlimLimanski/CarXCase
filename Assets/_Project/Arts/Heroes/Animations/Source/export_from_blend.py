"""Run in Blender background mode, then use Tools/Hero/Import updated walk and run."""
from pathlib import Path
import bpy

output = Path(__file__).resolve().parent
source = output.parent.parent / "animation main char NEW.blend"
bpy.ops.wm.open_mainfile(filepath=str(source))
rig = bpy.data.objects["Armature"]
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
rig.animation_data_create()
for track in rig.animation_data.nla_tracks:
    track.mute = True

for action_name, filename in [
    ("tpose", "Hero_TPose.fbx"),
    ("walking", "Hero_Walk.fbx"),
    ("running", "Hero_Run.fbx"),
]:
    action = bpy.data.actions[action_name]
    rig.animation_data.action = action
    rig.animation_data.action_slot = action.slots[0]
    bpy.context.scene.frame_start = int(action.frame_range[0])
    bpy.context.scene.frame_end = int(action.frame_range[1])
    bpy.context.scene.frame_set(bpy.context.scene.frame_start)
    bpy.ops.export_scene.fbx(
        filepath=str(output / filename),
        use_selection=True,
        object_types={"ARMATURE"},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0.0,
        axis_forward="-Z",
        axis_up="Y",
    )
    print("Exported", action_name, "to", filename)
