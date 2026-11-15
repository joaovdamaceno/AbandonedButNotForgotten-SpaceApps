"""SUR-018: reproducible full-body A7L reconstruction and NASA LM conversion.
Run with Blender --background --python this_file. No third-party astronaut mesh.
The suit is an artistic reconstruction from Apollo photographs, not a NASA model.
"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Assets/_Game/Chapters/Surveyor/Documentary/Art'
OUT.mkdir(parents=True,exist_ok=True)
def reset():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def mat(name,color,metal=0,rough=.7):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=(*color,1);n.inputs['Metallic'].default_value=metal;n.inputs['Roughness'].default_value=rough
    return m
def smooth(o):
    if o.type=='MESH':
        for p in o.data.polygons:p.use_smooth=True
def group(o,bone):
    g=o.vertex_groups.new(name=bone);g.add(list(range(len(o.data.vertices))),1,'REPLACE')
    mod=o.modifiers.new('A7L skeleton','ARMATURE');mod.object=rig;o.parent=rig
def ellipsoid(name,at,scale,material,bone):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=28,ring_count=16,location=at)
    o=bpy.context.object;o.name=name;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(material);smooth(o);group(o,bone);return o
def box(name,at,size,material,bone,bevel=.018):
    bpy.ops.mesh.primitive_cube_add(size=1,location=at);o=bpy.context.object;o.name=name;o.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(material)
    mod=o.modifiers.new('Rounded seams','BEVEL');mod.width=bevel;mod.segments=3
    bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name);smooth(o);group(o,bone);return o
def profile(name,start,end,radii,material,bone,fold=.025):
    a,b=Vector(start),Vector(end);direction=(b-a).normalized();u=direction.cross(Vector((0,1,0))).normalized()
    if u.length<.1:u=Vector((1,0,0))
    v=direction.cross(u).normalized();verts=[];faces=[];sides=24;rings=25
    for k in range(rings):
        t=k/(rings-1);r=(1-t)*radii[0]+t*radii[1]
        # Irregular sewn fabric gathers, denser at the elbow/knee/cuff.
        wrinkle=fold*math.sin(t*math.pi*18+.4)*(.3+.7*abs(2*t-1))
        for j in range(sides):
            ang=j*2*math.pi/sides;r2=r*(1+wrinkle+fold*.45*math.sin(ang*3+t*19))
            verts.append(tuple(a.lerp(b,t)+r2*(math.cos(ang)*u+.92*math.sin(ang)*v)))
    for k in range(rings-1):
        for j in range(sides):
            n=(j+1)%sides;x=k*sides+j;y=k*sides+n;faces.append((x,y,y+sides,x+sides))
    faces.extend([tuple(reversed(range(sides))),tuple((rings-1)*sides+i for i in range(sides))])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
    o.data.materials.append(material);smooth(o);group(o,bone);return o
def hose(name,points,material,bone,r=.014):
    curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=8;curve.bevel_depth=r;curve.bevel_resolution=3
    s=curve.splines.new('BEZIER');s.bezier_points.add(len(points)-1)
    for bp,p in zip(s.bezier_points,points):bp.co=p;bp.handle_left_type=bp.handle_right_type='AUTO'
    o=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(o);o.data.materials.append(material)
    bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');group(o,bone);o.select_set(False)
reset()
cloth=mat('Suit_Ivory',(.79,.77,.70),0,.86);seam=mat('Suit_Seams',(.50,.49,.46),0,.92)
rubber=mat('Suit_Rubber',(.12,.135,.14),0,.82);gold=mat('Visor_Gold',(.62,.36,.075),.88,.17)
metal=mat('Suit_Metal',(.48,.51,.51),.75,.3);red=mat('Suit_Connectors',(.36,.12,.10),.2,.55)
bpy.ops.object.armature_add();rig=bpy.context.object;rig.name='ApolloA7L';bpy.ops.object.mode_set(mode='EDIT');rig.data.edit_bones.remove(rig.data.edit_bones[0])
bones={
 'Pelvis':((0,0,.85),(0,0,1.02),None),'Spine':((0,0,1.02),(0,0,1.35),'Pelvis'),
 'Chest':((0,0,1.35),(0,0,1.50),'Spine'),'Head':((0,0,1.50),(0,0,1.82),'Chest')}
for side,sign in [('L',-1),('R',1)]:
    bones.update({f'Thigh_{side}':((sign*.13,0,.91),(sign*.145,0,.52),'Pelvis'),f'Shin_{side}':((sign*.145,0,.52),(sign*.145,0,.13),f'Thigh_{side}'),f'Foot_{side}':((sign*.145,0,.13),(sign*.145,-.18,.09),f'Shin_{side}'),f'UpperArm_{side}':((sign*.25,0,1.43),(sign*.48,0,1.17),'Chest'),f'ForeArm_{side}':((sign*.48,0,1.17),(sign*.58,-.04,.95),f'UpperArm_{side}'),f'Hand_{side}':((sign*.58,-.04,.95),(sign*.61,-.06,.82),f'ForeArm_{side}')})
for name,(a,b,parent) in bones.items():
    bone=rig.data.edit_bones.new(name);bone.head=a;bone.tail=b
    if parent:bone.parent=rig.data.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
ellipsoid('Torso pressure garment',(0,0,1.28),(.265,.18,.34),cloth,'Spine')
ellipsoid('Waist articulated fabric',(0,0,.98),(.245,.18,.22),cloth,'Pelvis')
box('PLSS life support',(0,.24,1.31),(.40,.22,.53),cloth,'Chest',.045)
box('PLSS upper cover',(0,.245,1.64),(.35,.20,.12),cloth,'Chest',.025)
box('Chest remote control',(0,-.177,1.36),(.29,.072,.16),cloth,'Chest')
for x in [-.09,0,.09]:
    ellipsoid('Chest dial',(x,-.224,1.36),(.022,.012,.022),rubber,'Chest')
for x in [-.105,.105]:
    ellipsoid('Gas connector',(x,-.166,1.16),(.046,.023,.046),metal,'Spine')
    ellipsoid('Hose lock',(x,-.185,1.16),(.025,.014,.025),red,'Spine')
    hose('Oxygen hose',[(x,-.2,1.16),(x*2.5,-.22,1.02),(x*2.8,.06,1.06),(x*1.8,.26,1.21)],cloth,'Spine',.021)
profile('Neck locking ring',(0,0,1.49),(0,0,1.55),(.145,.145),metal,'Head',0)
ellipsoid('Helmet shell',(0,-.005,1.68),(.205,.19,.225),cloth,'Head')
ellipsoid('Visor gasket',(0,-.139,1.69),(.168,.092,.17),rubber,'Head')
ellipsoid('Gold sun visor',(0,-.171,1.69),(.153,.075,.15),gold,'Head')
for side,sign in [('L',-1),('R',1)]:
    hip=Vector(bones[f'Thigh_{side}'][0]);knee=Vector(bones[f'Shin_{side}'][0]);ankle=Vector(bones[f'Foot_{side}'][0])
    profile('Thigh fabric '+side,hip,knee,(.145,.112),cloth,f'Thigh_{side}',.06)
    ellipsoid('Knee joint '+side,knee,(.115,.11,.12),cloth,f'Shin_{side}')
    profile('Lower leg fabric '+side,knee,ankle,(.112,.09),cloth,f'Shin_{side}',.07)
    box('Lunar overboot '+side,(sign*.145,-.065,.085),(.21,.34,.15),cloth,f'Foot_{side}',.035)
    box('Boot sole '+side,(sign*.145,-.065,.022),(.22,.35,.04),rubber,f'Foot_{side}',.012)
    for i in range(7):box('Sole tread '+side,(sign*.145,-.20+i*.046,.014),(.224,.018,.018),seam,f'Foot_{side}',.003)
    shoulder=Vector(bones[f'UpperArm_{side}'][0]);elbow=Vector(bones[f'ForeArm_{side}'][0]);wrist=Vector(bones[f'Hand_{side}'][0])
    ellipsoid('Shoulder bell '+side,shoulder,(.135,.13,.15),cloth,f'UpperArm_{side}')
    profile('Upper sleeve '+side,shoulder,elbow,(.12,.096),cloth,f'UpperArm_{side}',.075)
    ellipsoid('Elbow joint '+side,elbow,(.10,.09,.10),cloth,f'ForeArm_{side}')
    profile('Forearm sleeve '+side,elbow,wrist,(.097,.067),cloth,f'ForeArm_{side}',.085)
    direction=(wrist-elbow).normalized();profile('Wrist lock '+side,wrist-direction*.025,wrist+direction*.022,(.073,.073),metal,f'Hand_{side}',0)
    palm=wrist+direction*.08;ellipsoid('Glove palm '+side,palm,(.061,.041,.083),cloth,f'Hand_{side}')
    for finger in range(4):
        a=palm+Vector(((finger-1.5)*.027,-.009,-.045));b=a+Vector((sign*.008,-.016,-.065+(abs(finger-1.4)*.01)))
        profile('Glove finger '+side,a,b,(.017,.012),cloth,f'Hand_{side}',.07);ellipsoid('Fingertip '+side,b,(.013,.013,.015),cloth,f'Hand_{side}')
    a=palm+Vector((-sign*.046,-.008,.005));b=a+Vector((-sign*.032,-.022,-.045));profile('Thumb '+side,a,b,(.023,.013),cloth,f'Hand_{side}',.06)
    for i in range(3):box('Suit seam '+side,(sign*(.25+i*.005),.015,1.28-i*.055),(.014,.32,.007),seam,'Chest',.002)
# Baked skeletal clips: complete bodies, two-bone solves and a planted stance half-cycle.
def pose_segment(name,target):
    pb=rig.pose.bones[name];direction=Vector(target)-pb.head
    # Convert global orientation to the bone's local basis, respecting its posed parent.
    desired=direction.to_track_quat('Y','Z').to_matrix().to_4x4();desired.translation=pb.head
    pb.matrix=desired
def chain(upper,lower,end,pole):
    bpy.context.view_layer.update();a=rig.pose.bones[upper].head.copy();end=Vector(end);p=Vector(pole)
    l1=rig.data.bones[upper].length;l2=rig.data.bones[lower].length;delta=end-a;d=min(delta.length,l1+l2-.001);n=delta.normalized()
    u=(p-a)-n*(p-a).dot(n);u.normalize();x=(l1*l1-l2*l2+d*d)/(2*d);h=math.sqrt(max(0,l1*l1-x*x));joint=a+n*x+u*h
    pose_segment(upper,joint);bpy.context.view_layer.update();pose_segment(lower,end);bpy.context.view_layer.update()
def key(frame):
    for pb in rig.pose.bones:
        pb.rotation_mode='QUATERNION';pb.keyframe_insert('rotation_quaternion',frame=frame);pb.keyframe_insert('location',frame=frame)
for clip,frames in [('Walk',49),('Inspect',73),('Stand',2)]:
    action=bpy.data.actions.new(clip);rig.animation_data_create();rig.animation_data.action=action
    for frame in range(1,frames+1):
        bpy.context.scene.frame_set(frame)
        for pb in rig.pose.bones:pb.location=(0,0,0);pb.rotation_quaternion=(1,0,0,0)
        t=(frame-1)/max(1,frames-1)
        if clip=='Walk':
            for side,offset in [('L',0),('R',.5)]:
                phase=(t+offset)%1;sign=-1 if side=='L' else 1
                if phase<.6:y=-.18+.36*phase/.6;z=.13
                else:u=(phase-.6)/.4;y=.18-.36*(u*u*(3-2*u));z=.13+.12*math.sin(math.pi*u)
                chain('Thigh_'+side,'Shin_'+side,(sign*.145,y,z),(sign*.15,-1,.55))
                pb=rig.pose.bones['Foot_'+side];desired=rig.data.bones['Foot_'+side].matrix_local.to_quaternion();pb.rotation_quaternion=rig.pose.bones['Shin_'+side].matrix.to_quaternion().inverted()@desired
                chain('UpperArm_'+side,'ForeArm_'+side,(sign*.56,-.12+(.08 if phase<.5 else -.08),1.0),(sign*.9,0,1.1))
        elif clip=='Inspect':
            u=min(1,t*3);u=u*u*(3-2*u);rig.pose.bones['Pelvis'].location.z=-.20*u
            rig.pose.bones['Spine'].rotation_quaternion=Vector((1,0,0)).rotation_difference(Vector((1,0,0)))
            from mathutils import Quaternion
            rig.pose.bones['Spine'].rotation_quaternion=Quaternion((1,0,0),math.radians(18)*u)
            bpy.context.view_layer.update()
            for side,sign in [('L',-1),('R',1)]:
                chain('Thigh_'+side,'Shin_'+side,(sign*.18,-.05,.13),(sign*.18,-1,.5))
                chain('UpperArm_'+side,'ForeArm_'+side,(sign*.22,-.45,1.0),(sign*.55,-.1,1.15))
        key(frame)
    track=rig.animation_data.nla_tracks.new();track.name=clip;strip=track.strips.new(clip,1,action);strip.action_frame_start=1;strip.action_frame_end=frames
    rig.animation_data.action=None
    # Stash only the current clip; other tracks remain muted for export.
    track.mute=True
for track in rig.animation_data.nla_tracks:track.mute=False
bpy.context.scene.render.fps=24
rig.data.pose_position='REST'
bpy.ops.object.select_all(action='DESELECT')
parts=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();parts[0].name='A7L complete pressure suit'
rig.data.pose_position='POSE'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUT/'ApolloA7L.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0)
tri=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in bpy.context.scene.objects if o.type=='MESH')
(OUT/'A7L-manifest.json').write_text(json.dumps({'author':'Original reconstruction created for this project','reference':'NASA Apollo 12 photographs; A7L family, artistic reconstruction','triangles':tri,'bones':len(bones),'clips':['Walk','Inspect','Stand'],'source':'SourceAssets/Surveyor3/Documentary/build_models.py'},indent=2))
# NASA official LM, preserve original outside runtime, merge by material.
reset();bpy.ops.import_scene.gltf(filepath=str(Path(__file__).parent/'ApolloLunarModule.glb'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in meshes:
    bpy.context.view_layer.objects.active=o;o.select_set(True)
    for m in o.modifiers:bpy.ops.object.modifier_apply(modifier=m.name)
    if len(o.data.polygons)>10000:
        mod=o.modifiers.new('Web LOD','DECIMATE');mod.ratio=.6;bpy.ops.object.modifier_apply(modifier=mod.name)
    o.select_set(False)
by={}
for o in meshes:
    name=o.data.materials[0].name if o.data.materials else 'LM_White';by.setdefault(name,[]).append(o)
for name,objects in by.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();objects[0].name='LM_'+name
for image in bpy.data.images:
    if image.type=='IMAGE' and image.size[0]>0:
        image.filepath_raw=str(OUT/('LM_'+image.name.replace('/','_').split('.')[0]+'.png'));image.file_format='PNG';image.save()
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(OUT/'ApolloLunarModule.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
print('SUR018_MODELS_COMPLETE',tri,flush=True)
