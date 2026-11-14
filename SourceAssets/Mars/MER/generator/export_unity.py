"""MAR-011: convert the approved MER rig (out/MER_Rover.blend) into the Unity runtime asset.

Output (folder given as argv after --):
  MER_Rover.fbx                    LOD0 + LOD1 per rig node (<Node>_LOD0 / <Node>_LOD1), anchors as empties
  Textures/T_MER_*_{BaseColor,Normal,MetallicSmoothness}.png   URP/Lit: metallic R, smoothness A
  conversion.json                  triangles, scale audit, nodes, parameters

Same FBX conventions as Assets/_Game/Chapters/Surveyor/Art/Editor/prepare_team_model.py:
metres (FBX Scale All), axis_forward -Z / axis_up Y, no bake_space_transform (unsafe with nested empties),
no animation; the Unity importer bakes the axis conversion (Docs/Art/IMPORT_RULES.md section 3).
"""
import bpy, bmesh, json, math, os, sys, hashlib
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import tex

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
DEST = args[0] if args else os.path.join(HERE, "out", "unity")
os.makedirs(os.path.join(DEST, "Textures"), exist_ok=True)

# LOD1 decimation ratio per node family (collapse). Small hardware keeps more of its shape.
RATIO = {"Wheel": 0.30, "Body": 0.40, "Steer": 0.40, "Rocker": 0.45, "Bogie": 0.40}
DEFAULT_RATIO = 0.45

bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "out", "MER_Rover.blend"))
scene = bpy.context.scene
scene.frame_set(1)
for ob in scene.objects:
    if ob.animation_data:
        ob.animation_data_clear()
        ob.rotation_euler = (0, 0, 0)


def tri_count(me):
    me.calc_loop_triangles()
    return len(me.loop_triangles)


def strip_unused_slots(ob):
    used = {p.material_index for p in ob.data.polygons}
    for i in reversed(range(len(ob.data.materials))):
        if i not in used:
            ob.active_material_index = i
            with bpy.context.temp_override(object=ob, active_object=ob):
                bpy.ops.object.material_slot_remove()


mesh_nodes = [ob for ob in scene.objects if ob.type == "MESH"]
stats = {"nodes": {}}
for ob in mesh_nodes:
    name = ob.name
    parent = ob.parent
    local = ob.matrix_local.copy()
    kids = list(ob.children)
    ob.name = name + "_LOD0"
    ob.data.name = name + "_LOD0"
    node = bpy.data.objects.new(name, None)
    scene.collection.objects.link(node)
    node.parent = parent
    node.matrix_local = local
    for k in kids:
        kl = k.matrix_local.copy()
        k.parent = node
        k.matrix_local = kl
    ob.parent = node
    ob.matrix_local.identity()
    strip_unused_slots(ob)
    # LOD1: decimated copy sharing the same materials/UVs
    lod1 = ob.copy()
    lod1.data = ob.data.copy()
    lod1.name = name + "_LOD1"
    lod1.data.name = name + "_LOD1"
    scene.collection.objects.link(lod1)
    lod1.parent = node
    lod1.matrix_local.identity()
    ratio = RATIO.get(name.split("_")[0], DEFAULT_RATIO)
    mod = lod1.modifiers.new("dec", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(lod1.evaluated_get(dg))
    lod1.modifiers.clear()
    old = lod1.data
    lod1.data = me
    me.name = name + "_LOD1"
    bpy.data.meshes.remove(old)
    # decimation drops the sharp-edge flags: rebuild them by angle (same 38 deg rule as LOD0)
    bm = bmesh.new()
    bm.from_mesh(me)
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        e.smooth = e.is_manifold and e.calc_face_angle(0) <= math.radians(38)
    bm.to_mesh(me)
    bm.free()
    stats["nodes"][name] = {"lod0": tri_count(ob.data), "lod1": tri_count(lod1.data), "ratio": ratio}

stats["lod0_triangles"] = sum(v["lod0"] for v in stats["nodes"].values())
stats["lod1_triangles"] = sum(v["lod1"] for v in stats["nodes"].values())


# --------------------------------------------------------------- scale audit
def wpos(n):
    return tuple(round(c, 4) for c in bpy.data.objects[n].matrix_world.translation)


stats["scale_audit_blender_frame"] = {
    "Anchor_Pancam_height_m": round(bpy.data.objects["Anchor_Pancam"].matrix_world.translation.z, 3),
    "Wheel_FL": wpos("Wheel_FL"), "Wheel_RR": wpos("Wheel_RR"),
    "wheel_radius_m": 0.13, "track_m": 1.24, "wheelbase_m": 1.28,
    "overall_length_m": 1.60, "overall_width_with_wings_m": 2.24,
    "forward": "Blender -Y -> FBX +Z -> Unity +Z", "left": "Blender +X -> Unity -X",
}

# ---------------------------------------------------------------- FBX export
for ob in scene.objects:
    ob.select_set(ob.type in {"MESH", "EMPTY"})
bpy.ops.export_scene.fbx(filepath=os.path.join(DEST, "MER_Rover.fbx"), use_selection=True,
                         object_types={"MESH", "EMPTY"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                         axis_forward="-Z", axis_up="Y", bake_space_transform=False, bake_anim=False, add_leaf_bones=False, use_mesh_modifiers=True,
                         mesh_smooth_type="FACE", use_custom_props=False, path_mode="STRIP", embed_textures=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "out", "MER_Rover_unity.blend"))


# ------------------------------------------------------------------ textures
def save_png(arr, path, alpha=None):
    from PIL import Image
    rgb = (np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8)
    rgb = rgb[::-1]  # numpy tiles are bottom-up (v = 0 at row 0); PNG is top-down
    if alpha is not None:
        a = (np.clip(alpha, 0, 1) * 255 + 0.5).astype(np.uint8)[::-1]
        img = Image.fromarray(np.dstack([rgb, a]), "RGBA")
    else:
        img = Image.fromarray(rgb, "RGB")
    img.save(path, optimize=True)


def write_set(prefix, albedo, normal, orm):
    d = os.path.join(DEST, "Textures")
    save_png(albedo, os.path.join(d, f"{prefix}_BaseColor.png"))
    save_png(normal, os.path.join(d, f"{prefix}_Normal.png"))
    metal = orm[..., 2]
    smooth = 1.0 - orm[..., 1]
    save_png(np.dstack([metal, np.zeros_like(metal), np.zeros_like(metal)]),
             os.path.join(d, f"{prefix}_MetallicSmoothness.png"), alpha=smooth)


write_set("T_MER_Atlas", *tex.build_atlas())
write_set("T_MER_Solar", *tex.build_solar())

files = {}
for root, _, fs in os.walk(DEST):
    for f in sorted(fs):
        if f.endswith((".fbx", ".png")):
            p = os.path.join(root, f)
            files[os.path.relpath(p, DEST)] = hashlib.sha256(open(p, "rb").read()).hexdigest()
stats["files_sha256"] = files
stats["materials"] = ["M_MER_Atlas", "M_MER_Solar"]
stats["textures"] = {"atlas_px": 2048, "solar_px": 1024, "channels": "BaseColor sRGB; Normal OpenGL +Y; MetallicSmoothness R=metal A=smoothness"}
stats["source"] = "SourceAssets/Mars/MER/MER_Rover.blend (gerado por generator/build.py)"
stats["blender"] = bpy.app.version_string
with open(os.path.join(DEST, "conversion.json"), "w", encoding="utf-8") as fh:
    json.dump(stats, fh, indent=2, ensure_ascii=False)
print("LOD0", stats["lod0_triangles"], "LOD1", stats["lod1_triangles"])
