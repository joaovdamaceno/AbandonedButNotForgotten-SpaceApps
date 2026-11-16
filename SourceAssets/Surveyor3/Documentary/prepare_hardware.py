"""NOTE (SUR-021): the face selection below cut the camera in half; run fix_recoverable_camera.py afterwards.
Split recoverable camera from existing team UV atlas; extract the team's real retro stage."""
import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Assets/_Game/Chapters/Surveyor/Documentary/Art'
def reset():bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
reset();bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/_Game/Chapters/Surveyor/Art/TeamModel/SurveyorTeamWeb.fbx'))
body=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.name=='StaticBody')
mount=next(o for o in bpy.context.scene.objects if o.name=='CameraMount');center=mount.matrix_world.translation
bm=bmesh.new();bm.from_mesh(body.data)
for f in bm.faces:
    p=body.matrix_world@f.calc_center_median()
    f.select=(Vector((p.x-center.x,p.y-center.y)).length<.19 and center.z-.67<p.z<center.z+.06)
for o in bpy.context.selected_objects:o.select_set(False)
body.select_set(True);bpy.context.view_layer.objects.active=body
bm.to_mesh(body.data);bm.free();bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.separate(type='SELECTED');bpy.ops.object.mode_set(mode='OBJECT')
housing=next(o for o in bpy.context.selected_objects if o!=body);housing.name='CameraHousing'
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(OUT/'SurveyorRecoverable.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
manifest={'cameraMountBlender':list(center),'cameraHousingTriangles':sum(len(p.vertices)-2 for p in housing.data.polygons),'notes':'Split of existing team atlas; unchanged UVs and full camera removed with recovered housing.'}
reset();bpy.ops.import_scene.gltf(filepath=str(ROOT/'SourceAssets/Surveyor3/surveyor3_com_retrofoguete.glb'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('retro_')]
for o in bpy.context.scene.objects:o.select_set(False)
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();retro=bpy.context.object;retro.name='Team retro stage'
# Apply world-space source transform: the recovered stage retains its real relation to the team rover.
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
for o in list(bpy.context.scene.objects):
    if o!=retro:bpy.data.objects.remove(o,do_unlink=True)
retro.select_set(True);bpy.context.view_layer.objects.active=retro
bpy.ops.export_scene.fbx(filepath=str(OUT/'SurveyorRetro.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
manifest['retroTriangles']=sum(len(p.vertices)-2 for p in retro.data.polygons)
(OUT/'Hardware-manifest.json').write_text(json.dumps(manifest,indent=2));print('HARDWARE_COMPLETE',manifest)
