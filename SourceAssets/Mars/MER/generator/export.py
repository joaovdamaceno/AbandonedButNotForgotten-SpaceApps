import bpy, os, sys
OUT=os.path.join(os.path.dirname(os.path.abspath(__file__)),'out')
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT,'MER_Rover.blend'))
bpy.context.scene.frame_set(1)
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,'MER_Rover.glb'), export_format='GLB',
    export_image_format='JPEG', export_jpeg_quality=90, export_animations=True,
    export_animation_mode='NLA_TRACKS', export_yup=True, export_apply=False, export_cameras=False, export_lights=False,
    export_extras=False, export_tangents=True)
print('size MB', os.path.getsize(os.path.join(OUT,'MER_Rover.glb'))/1e6)
