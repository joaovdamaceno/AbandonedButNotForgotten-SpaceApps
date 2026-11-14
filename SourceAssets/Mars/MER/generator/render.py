"""Render review views of out/MER_Rover.blend (Cycles CPU) + comparison sheet with references."""
import bpy, math, os, sys
import numpy as np
from mathutils import Vector

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
RV = os.path.join(OUT, "renders")
os.makedirs(RV, exist_ok=True)

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SAMPLES = int(args[0]) if args else 40
ONLY = args[1].split(",") if len(args) > 1 else None
FRAME = int(args[2]) if len(args) > 2 else 1
RES = (960, 720)

bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT, "MER_Rover.blend"))
sc = bpy.context.scene
sc.frame_set(FRAME)
sc.render.engine = "CYCLES"
sc.cycles.device = "CPU"
sc.cycles.samples = SAMPLES
sc.cycles.use_denoising = True
sc.cycles.max_bounces = 6
sc.render.resolution_x, sc.render.resolution_y = RES
sc.render.film_transparent = True
sc.view_settings.view_transform = "AgX"
sc.view_settings.look = "AgX - Medium High Contrast"

world = bpy.data.worlds.new("W")
sc.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs["Color"].default_value = (0.85, 0.86, 0.88, 1)
bg.inputs["Strength"].default_value = 0.9

sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.2
sun.data.angle = math.radians(4)
sun.rotation_euler = (math.radians(38), math.radians(18), math.radians(-35))
sc.collection.objects.link(sun)
fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", "SUN"))
fill.data.energy = 0.8
fill.rotation_euler = (math.radians(70), 0, math.radians(150))
sc.collection.objects.link(fill)

bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
ground = bpy.context.active_object
ground.is_shadow_catcher = True

cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.lens = 55
cam.data.clip_start = 0.01

TARGET = Vector((0, 0, 0.62))
VIEWS = {
    # name: (azimuth deg from front, elevation deg, distance, target override)
    "front":      (0, 6, 5.2, None),
    "left":       (90, 4, 5.4, None),
    "rear":       (180, 6, 5.2, None),
    "right":      (270, 4, 5.4, None),
    "top":        (0, 89.5, 5.6, None),
    "q_front_l":  (40, 30, 5.2, None),
    "q_rear_r":   (215, 32, 5.2, None),
    "bottom":     (0, -89.5, 5.6, Vector((0, 0, 0.3))),
    "under_side": (90, -28, 5.0, Vector((0, 0, 0.35))),
    "q_front_lo": (-25, -12, 5.0, Vector((0, 0, 0.45))),
    "head":       (25, 12, 1.4, Vector((0.0, -0.53, 1.45))),
    "arm":        (20, 8, 1.5, Vector((0.0, -0.62, 0.32))),
    "wheel":      (75, 8, 1.8, Vector((0.62, -0.5, 0.25))),
    "hga":        (60, 25, 1.6, Vector((0.38, 0.2, 0.82))),
}


def place(name):
    az, el, dist, tgt = VIEWS[name]
    tgt = tgt or TARGET
    # front of rover is -Y; azimuth 0 = camera in front, 90 = rover's left (+X)
    a, e = math.radians(az), math.radians(el)
    pos = tgt + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist
    cam.location = pos
    d = tgt - pos
    if abs(el) < 89:
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    else:
        cam.rotation_euler = (0, 0, 0) if el > 0 else (math.pi, 0, 0)
    ground.hide_render = el < 0


def composite(path, bgc=(0.90, 0.90, 0.91)):
    im = bpy.data.images.load(path)
    w, h = im.size
    px = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    a = px[..., 3:4]
    rgb = px[..., :3] + (1 - a) * np.array(bgc, dtype=np.float32)
    out = np.concatenate([rgb, np.ones((h, w, 1), np.float32)], -1)
    im.pixels.foreach_set(out.ravel())
    im.save()
    bpy.data.images.remove(im)


for name in VIEWS:
    if ONLY and name not in ONLY:
        continue
    place(name)
    path = os.path.join(RV, f"{name}.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    composite(path)
    print("rendered", name, flush=True)
