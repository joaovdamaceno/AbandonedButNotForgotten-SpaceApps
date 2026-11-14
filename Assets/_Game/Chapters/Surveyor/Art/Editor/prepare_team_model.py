"""Run with Blender --background --factory-startup --disable-autoexec --python ... -- source.glb output-folder.
Controlled conversion of the user-supplied Surveyor, not execution of embedded scripts.
"""
import bpy,sys,json,math
from pathlib import Path
from mathutils import Vector

args=sys.argv[sys.argv.index('--')+1:]
source=Path(args[0]);out=Path(args[1]);out.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source))
bpy.context.view_layer.update()
originals=[o for o in bpy.context.scene.objects if o.type=='MESH']
groups={name:[] for name in ['StaticBody','MirrorHead','SolarPanel','HighGainAntenna','ScoopLinkage','ScoopTool']}
def has_parent(o,name):
 while o:
  if o.name==name:return True
  o=o.parent
 return False
for o in originals:
 key='StaticBody'
 for marker,part in [('Node_301','MirrorHead'),('Node_481','SolarPanel'),('Node_492','HighGainAntenna'),('Node_630','ScoopLinkage'),('Node_628','ScoopTool')]:
  if has_parent(o,marker):key=part
 groups[key].append(o)

parts=[]
for name,objects in groups.items():
 if not objects:continue
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:
  matrix=o.matrix_world.copy();o.parent=None;o.matrix_world=matrix;o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0]
 bpy.ops.object.join();o=bpy.context.object;o.name=name
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 parts.append(o)
# Joined semantic groups keep mechanical parts independent; small details are preserved in the original source.
before=sum(len(o.data.polygons) for o in parts)
for o in parts:
 bpy.context.view_layer.objects.active=o
 mod=o.modifiers.new('Web geometry reduction','DECIMATE');mod.ratio=.37;mod.use_collapse_triangulate=True
 bpy.ops.object.modifier_apply(modifier=mod.name)
 if o.data.uv_layers:
  o.data.uv_layers[0].name='SourceUV'
 o.data.uv_layers.new(name='AtlasUV');o.data.uv_layers.active=o.data.uv_layers['AtlasUV'];o.data.uv_layers['AtlasUV'].active_render=True
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.0025)
bpy.ops.object.mode_set(mode='OBJECT')

materials=list({m for o in parts for m in o.data.materials if m})
inputs={}
for m in materials:
 tree=m.node_tree
 source_uv=tree.nodes.new('ShaderNodeUVMap');source_uv.uv_map='SourceUV'
 for node in list(tree.nodes):
  if node.type=='UVMAP':node.uv_map='SourceUV'
  if node.type=='TEX_IMAGE' and not node.inputs['Vector'].is_linked:
   tree.links.new(source_uv.outputs['UV'],node.inputs['Vector'])
  if node.type=='TEX_COORD':
   for link in list(node.outputs['UV'].links):
    destination=link.to_socket;tree.links.remove(link);tree.links.new(source_uv.outputs['UV'],destination)
 bsdf=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
 inputs[m]={}
 for key in ['Base Color','Alpha','Metallic','Roughness']:
  inp=bsdf.inputs[key]
  inputs[m][key]=(inp.links[0].from_socket if inp.is_linked else None,tuple(inp.default_value) if key=='Base Color' else inp.default_value)
 emission=tree.nodes.new('ShaderNodeEmission');emission.name='AtlasBakeEmission'
 output=next(n for n in tree.nodes if n.type=='OUTPUT_MATERIAL')
 for link in list(output.inputs['Surface'].links):tree.links.remove(link)
 tree.links.new(emission.outputs[0],output.inputs['Surface'])
 image_node=tree.nodes.new('ShaderNodeTexImage');image_node.name='AtlasBakeTarget'

scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1
scene.render.bake.use_selected_to_active=False
images={}
for key in ['Base Color','Alpha','Metallic','Roughness']:
 image=bpy.data.images.new('Surveyor '+key,width=1024,height=1024,alpha=True,float_buffer=False)
 image.colorspace_settings.name='sRGB' if key=='Base Color' else 'Non-Color'
 for m in materials:
  tree=m.node_tree;emission=tree.nodes['AtlasBakeEmission'];node=tree.nodes['AtlasBakeTarget']
  for link in list(emission.inputs['Color'].links):tree.links.remove(link)
  socket,value=inputs[m][key]
  if socket:tree.links.new(socket,emission.inputs['Color'])
  else:emission.inputs['Color'].default_value=value if isinstance(value,tuple) else (value,value,value,1)
  node.image=image;tree.nodes.active=node;node.select=True
 bpy.ops.object.bake(type='EMIT',margin=3,use_clear=False)
 images[key]=image

color=list(images['Base Color'].pixels[:]);alpha=images['Alpha'].pixels[:]
metal=images['Metallic'].pixels[:];rough=images['Roughness'].pixels[:]
packed=[0.0]*len(color)
for i in range(0,len(color),4):
 color[i+3]=alpha[i]
 packed[i]=metal[i];packed[i+1]=0;packed[i+2]=0;packed[i+3]=1-rough[i]
images['Base Color'].pixels[:]=color
images['Base Color'].filepath_raw=str(out/'SurveyorAlbedo.png');images['Base Color'].file_format='PNG';images['Base Color'].save()
images['Metallic'].pixels[:]=packed
images['Metallic'].filepath_raw=str(out/'SurveyorMetalGloss.png');images['Metallic'].file_format='PNG';images['Metallic'].save()

mat=bpy.data.materials.new('SurveyorAtlas');mat.use_nodes=True
nodes=mat.node_tree.nodes;bsdf=nodes.get('Principled BSDF')
node=nodes.new('ShaderNodeTexImage');node.image=images['Base Color']
mat.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Base Color'])
mat.node_tree.links.new(node.outputs['Alpha'],bsdf.inputs['Alpha'])
bsdf.inputs['Roughness'].default_value=.5
for o in parts:
 o.data.materials.clear();o.data.materials.append(mat)
 for p in o.data.polygons:p.material_index=0
 for layer in list(o.data.uv_layers):
  if layer.name!='AtlasUV':o.data.uv_layers.remove(layer)
 o.data.uv_layers.active=o.data.uv_layers['AtlasUV'];o.data.uv_layers['AtlasUV'].active_render=True
# Contact/optics markers export with the model and undergo the same axis conversion.
markers={'FootContact_1':(0,-2.15,-.017),'FootContact_2':(-1.86195457,1.075,-.017),
 'FootContact_3':(1.86195457,1.075,-.017),'CameraMount':(0,.965,1.60),
 'MirrorPivot':(0,.82,1.56),'ScoopPivot':(.9252783,2.2901441,.434)}
for name,location in markers.items():
 o=bpy.data.objects.new(name,None);scene.collection.objects.link(o);o.location=location
 o.select_set(True)
for o in parts:o.select_set(True)
# Remove emptied input hierarchy; only meshes and named production markers are exported.
selected=parts+[bpy.data.objects[n] for n in markers]
bpy.ops.object.select_all(action='DESELECT')
for o in selected:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.export_scene.fbx(filepath=str(out/'SurveyorTeamWeb.fbx'),use_selection=True,object_types={'MESH','EMPTY'},
 apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_space_transform=True,bake_anim=False,add_leaf_bones=False,
 use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='STRIP',embed_textures=False)
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts)
(out/'conversion.json').write_text(json.dumps(dict(source=source.name,triangles=triangles,renderParts=[o.name for o in parts],
 atlasResolution=1024,textureCount=2,materials=1,decimateRatio=.37,originalPolygons=before,
 notes='Original GLB preserved. Blender glTF import, semantic merge, decimation, UV packing and emission channel baking. URP metallic in R, smoothness in A.'),indent=2),encoding='utf-8')
print('TEAM_MODEL_CONVERTED',triangles)
