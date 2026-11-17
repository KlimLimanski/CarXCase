# Alex door interaction source

Alex_Door_Interaction.blend contains two editable Actions at 30 fps, Door_Reach_Push and Door_Reach_Pull, authored on the existing Alex rig. The Blender file is retained as reference for reworking the animation. It is outside Unity's Assets directory.

To regenerate the Blender file and its local GLB export from the procedural source, run:

    blender -b --python ArtSource/Hero/DoorInteraction/build_door_animation.py
