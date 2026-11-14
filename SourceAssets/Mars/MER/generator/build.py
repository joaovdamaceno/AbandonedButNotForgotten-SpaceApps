"""MER (Spirit/Opportunity) rover — detailed, rigged model built procedurally in Blender.

Modeling frame inside this script: +Y forward, +X rover right, +Z up, metres, ground z = 0.
At creation every vertex/pivot is transformed by G (180 deg about Z + centring), so the
exported asset faces Blender -Y  ->  glTF +Z  ->  Unity +Z, rover left on Unity -X.

Rig = transform hierarchy with pivots on the real joint axes and node names from
Docs/Art/IMPORT_RULES.md section 4. Rest rotations are identity everywhere.
"""
import bpy, bmesh, math, sys, os, random
from mathutils import Vector, Matrix, Euler

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import tex

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
os.makedirs(OUT, exist_ok=True)
random.seed(7)

# contact-patch centroid of the six wheels (y) -> root sits there
WHEEL_Y = {"F": 0.68, "M": -0.05, "R": -0.60}
CENTER_Y = sum(WHEEL_Y.values()) / 3.0
G = Matrix.Rotation(math.pi, 4, "Z") @ Matrix.Translation((0.0, -CENTER_Y, 0.0))

PARTS = []        # (node, bpy mesh)
TILE_M = 1.0      # metres covered by one atlas tile


def V(x, y, z):
    return Vector((x, y, z))


def gp(p):
    """Pivot/point from modeling frame to export frame."""
    return (G @ Vector(p).to_4d()).to_3d()


# ----------------------------------------------------------------- bmesh tools
def align_z(d):
    """3x3 rotation whose Z axis points along d."""
    z = Vector(d).normalized()
    ref = Vector((0, 0, 1)) if abs(z.z) < 0.95 else Vector((1, 0, 0))
    x = ref.cross(z).normalized()
    y = z.cross(x).normalized()
    return Matrix((x, y, z)).transposed()


def frame_y(d, up=Vector((0, 0, 1))):
    """3x3 rotation whose Y axis points along d, Z as close to `up` as possible."""
    y = Vector(d).normalized()
    up = Vector(up)
    if abs(y.dot(up.normalized())) > 0.98:
        up = Vector((1, 0, 0)) if abs(y.x) < 0.9 else Vector((0, 1, 0))
    x = y.cross(up).normalized()
    z = x.cross(y).normalized()
    return Matrix((x, y, z)).transposed()


ROT = {
    "X": Matrix.Rotation(math.radians(90), 3, "Y"),   # local Z -> world X
    "Y": Matrix.Rotation(math.radians(-90), 3, "X"),  # local Z -> world Y
    "Z": Matrix.Identity(3),
}


def xf(bm, loc=(0, 0, 0), rot=None):
    m = Matrix.Translation(Vector(loc))
    if rot is not None:
        m = m @ rot.to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm


def bevel_sharp(bm, off, seg=1, angle=35):
    edges = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0) > math.radians(angle)]
    if edges and off > 0:
        bmesh.ops.bevel(bm, geom=edges, offset=off, segments=seg, profile=0.5, affect="EDGES",
                        clamp_overlap=True)
    return bm


def box(sx, sy, sz, bevel=0.0, seg=1):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts)
    if bevel > 0:
        bevel_sharp(bm, bevel, seg)
    return bm


def cyl(r, h, n=16, r2=None, bevel=0.0, caps=True):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=n, radius1=r,
                          radius2=r if r2 is None else r2, depth=h)
    if bevel > 0:
        bevel_sharp(bm, bevel, 1, 40)
    return bm


def sphere(r, u=12, v=8):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    return bm


def lathe(profile, n=24, closed=False):
    """Profile [(r, z)] spun around Z. r == 0 points become poles."""
    bm = bmesh.new()
    vs = [bm.verts.new((r, 0.0, z)) for r, z in profile]
    es = [bm.edges.new((vs[i], vs[i + 1])) for i in range(len(vs) - 1)]
    if closed:
        es.append(bm.edges.new((vs[-1], vs[0])))
    bmesh.ops.spin(bm, geom=vs + es, cent=(0, 0, 0), axis=(0, 0, 1), angle=2 * math.pi,
                   steps=n, use_merge=True)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def prism(poly, z0, z1):
    """Extrude a 2D polygon (CCW, x/y) between z0 and z1."""
    bm = bmesh.new()
    bot = [bm.verts.new((x, y, z0)) for x, y in poly]
    top = [bm.verts.new((x, y, z1)) for x, y in poly]
    bm.faces.new(list(reversed(bot)))
    bm.faces.new(top)
    n = len(poly)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def sweep(points, r=0.01, sides=8, samples=6, profile=None, up=None, closed_caps=True):
    """Tube along a Catmull-Rom path. profile: list of 2D (a, b) offsets; up: fixed binormal."""
    pts = [Vector(p) for p in points]
    if len(pts) == 2:
        path = [pts[0].lerp(pts[1], t / max(samples, 1)) for t in range(samples + 1)]
    else:
        path = []
        ext = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
        for i in range(1, len(ext) - 2):
            p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
            for s in range(samples):
                t = s / samples
                t2, t3 = t * t, t * t * t
                path.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
                                   + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
        path.append(pts[-1])
    if profile is None:
        profile = [(r * math.cos(2 * math.pi * k / sides), r * math.sin(2 * math.pi * k / sides))
                   for k in range(sides)]
    bm = bmesh.new()
    rings = []
    nrm = None
    for i, p in enumerate(path):
        t = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
        if up is not None:
            b = Vector(up).normalized()
            n = b.cross(t).normalized()
            b = t.cross(n).normalized()
        else:
            if nrm is None:
                ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
                nrm = ref.cross(t).normalized()
            nrm = (nrm - t * nrm.dot(t)).normalized()
            n = nrm
            b = t.cross(n).normalized()
        rings.append([bm.verts.new(p + n * a + b * c) for a, c in profile])
    k = len(profile)
    for i in range(len(rings) - 1):
        for j in range(k):
            bm.faces.new((rings[i][j], rings[i][(j + 1) % k], rings[i + 1][(j + 1) % k], rings[i + 1][j]))
    if closed_caps:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def beam(p1, p2, w, h, up=(0, 0, 1), bevel=0.003):
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bm = box(w, d.length, h, bevel)
    return xf(bm, (p1 + p2) / 2, frame_y(d, Vector(up)))


def rod(p1, p2, r, n=10, bevel=0.0):
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bm = cyl(r, d.length, n, bevel=bevel)
    return xf(bm, (p1 + p2) / 2, align_z(d))


# ------------------------------------------------------------------ finalize
def add(bm, node, tile, solar_top=False, smooth_angle=38, flat=False, fit=False):
    """Flip to export frame, UV into atlas tile (box projection), shade, store."""
    bmesh.ops.transform(bm, matrix=G, verts=bm.verts)
    bm.normal_update()
    uvl = bm.loops.layers.uv.new("UVMap")
    if not bm.verts:
        bm.free()
        return
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    mn = Vector((min(xs), min(ys), min(zs)))
    ext = max(max(xs) - mn.x, max(ys) - mn.y, max(zs) - mn.z)
    scale = ext if fit else max(TILE_M, ext)
    slack = max(0.0, 1.0 - ext / scale)
    ou, ov = random.random() * slack, random.random() * slack
    u0, v0, u1, v1 = tex.tile_rect(tile)
    for f in bm.faces:
        nx, ny, nz = abs(f.normal.x), abs(f.normal.y), abs(f.normal.z)
        if solar_top and f.normal.z > 0.9:
            f.material_index = 1
            for lp in f.loops:
                lp[uvl].uv = (lp.vert.co.x / 0.5, lp.vert.co.y / 0.5)
            continue
        f.material_index = 0
        for lp in f.loops:
            c = lp.vert.co - mn
            if nz >= nx and nz >= ny:
                a, b = c.x, c.y
            elif nx >= ny:
                a, b = c.y, c.z
            else:
                a, b = c.x, c.z
            a = ou + a / scale
            b = ov + b / scale
            lp[uvl].uv = (u0 + (u1 - u0) * a, v0 + (v1 - v0) * b)
    for f in bm.faces:
        f.smooth = not flat
    lim = math.radians(smooth_angle)
    for e in bm.edges:
        if not e.is_manifold or e.calc_face_angle(0) > lim:
            e.smooth = False
    me = bpy.data.meshes.new(f"part_{len(PARTS)}")
    bm.to_mesh(me)
    bm.free()
    PARTS.append((node, me))


# =================================================================== GEOMETRY
SIDES = {"L": -1.0, "R": 1.0}
WX = 0.62           # wheel centre |x|
WR = 0.13           # wheel radius
WW = 0.16           # wheel width
DECK_TOP = 0.625


# ------------------------------------------------------------------- wheels
def wheel(node, center, s):
    """Wheel with outer face toward s*X. Built at origin with axle along +X."""
    parts = []
    # drum with outer lip (lathe around Z then rotate to X)
    prof = [(0.119, -0.080), (0.125, -0.080), (0.125, 0.074), (0.129, 0.076), (0.129, 0.080),
            (0.119, 0.080)]
    parts.append((lathe(prof, 48, closed=True), "wheel", 30))
    # cleats (grousers)
    for k in range(24):
        a = 2 * math.pi * k / 24
        b = box(0.0065, 0.005, 0.150)
        xf(b, (0.1275 * math.cos(a), 0.1275 * math.sin(a), -0.004), Matrix.Rotation(a, 3, "Z"))
        parts.append((b, "wheel", 30))
    # curved flexure spokes (in the wheel plane, near the outer face)
    for k in range(6):
        a0 = 2 * math.pi * k / 6
        pts = []
        for i in range(7):
            t = i / 6
            r = 0.042 + (0.120 - 0.042) * t
            a = a0 + math.radians(65) * math.sin(t * math.pi * 0.5) - math.radians(18) * math.sin(t * math.pi)
            pts.append((r * math.cos(a), r * math.sin(a), 0.045))
        prof2 = [(-0.0025, -0.016), (0.0025, -0.016), (0.0025, 0.016), (-0.0025, 0.016)]
        parts.append((sweep(pts, profile=prof2, samples=2, up=(0, 0, 1)), "wheel", 30))
    # hub with domed cap
    hub = [(0.0, 0.080), (0.016, 0.079), (0.028, 0.074), (0.040, 0.066), (0.040, -0.050),
           (0.030, -0.062), (0.0, -0.062)]
    parts.append((lathe(hub, 24), "darkmetal", 40))
    parts.append((lathe([(0.0, 0.0805), (0.012, 0.0805), (0.012, 0.084), (0.0, 0.084)], 16, True), "alu", 40))
    # outer face ring with lightening holes
    ring = [(0.048, 0.064), (0.075, 0.062), (0.078, 0.056), (0.078, 0.050), (0.048, 0.050)]
    parts.append((lathe(ring, 32), "wheel", 30))
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.radians(22.5)
        h = cyl(0.0075, 0.016, 8)
        xf(h, (0.063 * math.cos(a), 0.063 * math.sin(a), 0.058))
        parts.append((h, "darkmetal", 30))
    # hub bolts
    for k in range(6):
        a = 2 * math.pi * k / 6
        b = cyl(0.004, 0.008, 6)
        xf(b, (0.028 * math.cos(a), 0.028 * math.sin(a), 0.084))
        parts.append((b, "alu", 40))
    # inner stiffening ring
    parts.append((lathe([(0.112, -0.074), (0.119, -0.074), (0.119, -0.062), (0.112, -0.062)], 32, True),
                  "wheel", 30))
    rot = ROT["X"] if s > 0 else Matrix.Rotation(math.radians(-90), 3, "Y")
    for bm, tile, ang in parts:
        xf(bm, (0, 0, 0), rot)
        xf(bm, center)
        add(bm, node, tile, smooth_angle=ang)


def hub_motor(node, center, s):
    """Drive actuator entering the wheel hub from the inner side."""
    x = center[0]
    a = V(x - s * 0.135, center[1], center[2])
    b = V(x - s * 0.060, center[1], center[2])
    add(rod(a, b, 0.034, 20, bevel=0.004), node, "darkmetal")
    add(rod(b, b + V(s * 0.012, 0, 0), 0.039, 20, bevel=0.003), node, "alu")
    # cooling fins
    for i in range(4):
        p = a + V(s * (0.012 + i * 0.016), 0, 0)
        add(rod(p, p + V(s * 0.005, 0, 0), 0.040, 20), node, "alu")
    # connector + cable stub
    c = box(0.022, 0.018, 0.016, 0.002)
    xf(c, a + V(s * 0.03, 0, 0.038))
    add(c, node, "darkmetal")


def steer_assembly(node, top, wheel_c, s):
    """Rotating part of a steering actuator + L bracket down to the hub motor."""
    x, y, z = top
    add(rod(V(x, y, z - 0.020), V(x, y, z), 0.036, 24, bevel=0.003), node, "alu")
    # bolt ring
    for k in range(8):
        a = 2 * math.pi * k / 8
        b = cyl(0.0035, 0.006, 6)
        xf(b, (x + 0.030 * math.cos(a), y + 0.030 * math.sin(a), z - 0.002))
        add(b, node, "darkmetal", flat=True)
    xin = x - s * 0.14
    # horizontal arm of the bracket
    add(beam(V(x, y, z - 0.030), V(xin, y, z - 0.030), 0.050, 0.022, bevel=0.004), node, "struct")
    # vertical leg, slightly tapered look via two boxes
    add(beam(V(xin, y, z - 0.028), V(xin, y, wheel_c[2] + 0.02), 0.024, 0.055,
             up=(1, 0, 0), bevel=0.004), node, "struct")
    add(beam(V(xin, y, wheel_c[2] + 0.04), V(xin, y, wheel_c[2] - 0.035), 0.030, 0.075,
             up=(1, 0, 0), bevel=0.005), node, "struct")
    # gusset
    add(beam(V(xin + s * 0.002, y, z - 0.055), V(xin + s * 0.045, y, z - 0.032), 0.008, 0.035,
             bevel=0.002), node, "struct")
    hub_motor(node, wheel_c, s)
    # cable from steering actuator to hub motor
    add(sweep([V(x - s * 0.02, y + 0.03, z - 0.02), V(xin + s * 0.01, y + 0.035, z - 0.04),
               V(xin + s * 0.012, y + 0.035, wheel_c[2] + 0.06), V(xin + s * 0.03, y + 0.02, wheel_c[2] + 0.04)],
              r=0.0055, sides=6, samples=4), node, "cable")


def steer_actuator_fixed(node, top):
    """Static housing of the steering actuator (belongs to rocker/bogie)."""
    x, y, z = top
    add(rod(V(x, y, z), V(x, y, z + 0.055), 0.033, 24, bevel=0.004), node, "darkmetal")
    add(rod(V(x, y, z + 0.055), V(x, y, z + 0.064), 0.026, 20, bevel=0.002), node, "alu")
    c = box(0.02, 0.026, 0.02, 0.002)
    xf(c, (x, y - 0.035, z + 0.03))
    add(c, node, "darkmetal")


# ------------------------------------------------------------- rocker/bogie
def mobility(side):
    s = SIDES[side]
    RP = V(s * 0.42, 0.10, 0.46)
    BP = V(s * 0.46, -0.30, 0.30)
    FT = V(s * WX, WHEEL_Y["F"], 0.315)      # front steering axis (rotating flange top)
    RT = V(s * WX, WHEEL_Y["R"], 0.315)
    rocker, bogie = f"Rocker_{side}", f"Bogie_{side}"

    # --- rocker
    add(rod(V(s * 0.395, 0.10, 0.46), V(s * 0.455, 0.10, 0.46), 0.042, 24, bevel=0.004), rocker, "alu")
    add(rod(V(s * 0.455, 0.10, 0.46), V(s * 0.465, 0.10, 0.46), 0.030, 20), rocker, "darkmetal")
    knee = V(s * 0.50, 0.50, 0.415)
    add(beam(RP + V(0, 0.02, -0.005), knee, 0.040, 0.052, bevel=0.004), rocker, "struct")
    add(beam(knee, V(s * 0.565, WHEEL_Y["F"] - 0.01, 0.40), 0.040, 0.046, bevel=0.004), rocker, "struct")
    add(beam(V(s * 0.55, WHEEL_Y["F"], 0.395), V(FT.x, FT.y, 0.395), 0.060, 0.024, bevel=0.004), rocker, "struct")
    k = cyl(0.030, 0.050, 16, bevel=0.003)
    xf(k, knee, ROT["X"])
    add(k, rocker, "alu")
    steer_actuator_fixed(rocker, FT)
    # rear leg down to bogie clevis
    add(beam(RP + V(0, -0.02, -0.005), BP + V(0, 0.03, 0.035), 0.040, 0.050, bevel=0.004), rocker, "struct")
    for dx in (-0.032, 0.032):
        cl = box(0.008, 0.07, 0.07, 0.003)
        xf(cl, BP + V(s * 0.0 + dx, 0.0, 0.02))
        add(cl, rocker, "struct")
    # rocker cables + clips
    add(sweep([RP + V(-s * 0.01, 0.0, 0.045), knee + V(0, 0, 0.04), V(s * 0.56, WHEEL_Y["F"] - 0.02, 0.43),
               FT + V(-s * 0.03, -0.03, 0.07)], r=0.006, sides=6, samples=5), rocker, "cable")
    add(sweep([RP + V(s * 0.01, -0.02, 0.045), BP + V(0, 0.05, 0.075), BP + V(0, -0.02, 0.06)],
              r=0.0055, sides=6, samples=5), rocker, "cable")
    for t in (0.3, 0.7):
        p = RP.lerp(knee, t) + V(0, 0, 0.03)
        add(beam(p - V(0, 0.006, 0), p + V(0, 0.006, 0), 0.048, 0.02, bevel=0.0), rocker, "darkmetal")

    # --- bogie
    add(rod(BP - V(s * 0.025, 0, 0), BP + V(s * 0.025, 0, 0), 0.026, 20, bevel=0.003), bogie, "alu")
    mid_top = V(s * 0.48, WHEEL_Y["M"], 0.235)
    add(beam(BP, mid_top, 0.036, 0.046, bevel=0.004), bogie, "struct")
    xm = WX - 0.14
    add(beam(V(s * 0.47, WHEEL_Y["M"], 0.235), V(s * xm, WHEEL_Y["M"], 0.235), 0.06, 0.03, bevel=0.004),
        bogie, "struct")
    add(beam(V(s * xm, WHEEL_Y["M"], 0.25), V(s * xm, WHEEL_Y["M"], WR - 0.035), 0.03, 0.075, up=(1, 0, 0),
             bevel=0.005), bogie, "struct")
    hub_motor(bogie, V(s * WX, WHEEL_Y["M"], WR), s)
    rear_knee = V(s * 0.52, -0.53, 0.37)
    add(beam(BP, rear_knee, 0.036, 0.046, bevel=0.004), bogie, "struct")
    add(beam(rear_knee, V(RT.x, RT.y + 0.0, 0.395), 0.06, 0.024, bevel=0.004), bogie, "struct")
    k = cyl(0.026, 0.046, 16, bevel=0.003)
    xf(k, rear_knee, ROT["X"])
    add(k, bogie, "alu")
    steer_actuator_fixed(bogie, RT)
    # long lower tie bar between middle hub bracket and rear steering (as seen in side photos)
    add(beam(V(s * 0.47, WHEEL_Y["M"] - 0.02, 0.21), V(s * 0.50, -0.50, 0.31), 0.018, 0.022, bevel=0.002),
        bogie, "struct")
    add(sweep([BP + V(0, 0, 0.04), mid_top + V(0, 0.03, 0.04), V(s * 0.50, WHEEL_Y["M"], 0.22)],
              r=0.005, sides=6, samples=5), bogie, "cable")
    add(sweep([BP + V(0, 0, 0.04), rear_knee + V(0, 0, 0.035), RT + V(-s * 0.03, 0.03, 0.07)],
              r=0.005, sides=6, samples=5), bogie, "cable")

    # --- steering + wheels
    for pos, top, parent in (("F", FT, rocker), ("R", RT, bogie)):
        wc = V(s * WX, WHEEL_Y[pos], WR)
        steer_assembly(f"Steer_{pos}{side}", top, wc, s)
        wheel(f"Wheel_{pos}{side}", wc, s)
    wheel(f"Wheel_M{side}", V(s * WX, WHEEL_Y["M"], WR), s)

    return {"Rocker": RP, "Bogie": BP, "SteerF": FT, "SteerR": RT}


# --------------------------------------------------------------------- body
DECK_CENTER = [(-0.42, 0.80), (0.42, 0.80), (0.46, 0.66), (0.46, -0.52), (0.31, -0.80), (-0.31, -0.80),
               (-0.46, -0.52), (-0.46, 0.66)]
WING_F = [(0.475, 0.70), (0.475, 0.075), (1.00, 0.095), (0.98, 0.17), (0.62, 0.745)]
WING_R = [(0.475, 0.035), (0.475, -0.53), (0.67, -0.70), (1.06, -0.52), (1.12, -0.46), (1.03, 0.05)]


def mirror_poly(poly):
    return [(-x, y) for x, y in reversed(poly)]


def solar_panel(poly, z0, z1, node="Body"):
    bm = prism(poly, z0, z1)
    bevel_sharp(bm, 0.003, 1, 30)
    add(bm, node, "deckframe", solar_top=True, smooth_angle=30)


def body():
    n = "Body"
    # Warm Electronics Box core
    core = box(0.68, 0.90, 0.32, 0.015, 2)
    xf(core, (0, 0.07, 0.44))
    add(core, n, "kapton")
    # belly pan
    bp = box(0.62, 0.84, 0.02, 0.006)
    xf(bp, (0, 0.07, 0.275))
    add(bp, n, "deckframe")
    # MLI blankets: side panels (gold) and black patches, slightly proud
    for s in (-1, 1):
        for (y0, y1, z0, z1, tile) in ((-0.36, 0.05, 0.30, 0.585, "gold"), (0.07, 0.50, 0.30, 0.585, "gold"),
                                       (-0.10, 0.20, 0.33, 0.43, "kapton"), (0.26, 0.48, 0.47, 0.57, "kapton"),
                                       (-0.34, -0.18, 0.31, 0.40, "kapton")):
            t = 0.006 if tile == "gold" else 0.010
            pnl = box(t, y1 - y0, z1 - z0, 0.002)
            xf(pnl, (s * (0.342 + t / 2), (y0 + y1) / 2, (z0 + z1) / 2))
            add(pnl, n, tile)
        # tape strips over the blanket seams
        for y in (0.06,):
            st = box(0.004, 0.02, 0.29, 0.0)
            xf(st, (s * 0.348, y, 0.44))
            add(st, n, "beige", flat=True)
        # rocker pivot boss + differential housing
        add(rod(V(s * 0.34, 0.10, 0.46), V(s * 0.395, 0.10, 0.46), 0.05, 24, bevel=0.004), n, "darkmetal")
        add(rod(V(s * 0.345, 0.10, 0.46), V(s * 0.352, 0.10, 0.46), 0.065, 24), n, "alu")
        for k in range(6):
            a = 2 * math.pi * k / 6
            b = rod(V(s * 0.352, 0.10 + 0.057 * math.cos(a), 0.46 + 0.057 * math.sin(a)),
                    V(s * 0.358, 0.10 + 0.057 * math.cos(a), 0.46 + 0.057 * math.sin(a)), 0.004, 6)
            add(b, n, "darkmetal", flat=True)
    # front and rear faces
    for (y, sgn) in ((0.52, 1), (-0.38, -1)):
        pnl = box(0.56, 0.006, 0.20, 0.002)
        xf(pnl, (0, y + sgn * 0.003, 0.39))
        add(pnl, n, "gold")
        pnl = box(0.30, 0.008, 0.07, 0.002)
        xf(pnl, (0.0, y + sgn * 0.004, 0.53))
        add(pnl, n, "kapton")
    # rear: RTG-free MER has radiators/vent: add a small radiator grille + connector panel
    for i in range(7):
        g = box(0.008, 0.012, 0.11, 0.0)
        xf(g, (-0.20 + i * 0.022, -0.392, 0.38))
        add(g, n, "alu", flat=True)
    cp = box(0.10, 0.012, 0.06, 0.003)
    xf(cp, (0.17, -0.392, 0.34))
    add(cp, n, "darkmetal")
    for i in range(3):
        c = rod(V(0.14 + i * 0.03, -0.398, 0.34), V(0.14 + i * 0.03, -0.41, 0.34), 0.008, 10)
        add(c, n, "alu")
    # top edge rails of the WEB (support the deck)
    for s in (-1, 1):
        add(beam(V(s * 0.335, -0.37, 0.598), V(s * 0.335, 0.51, 0.598), 0.02, 0.016, bevel=0.002), n, "deckframe")
    for y in (-0.37, 0.51):
        add(beam(V(-0.335, y, 0.598), V(0.335, y, 0.598), 0.02, 0.016, bevel=0.002), n, "deckframe")

    # deck + wings
    solar_panel(DECK_CENTER, 0.600, DECK_TOP)
    for poly in (WING_F, WING_R):
        solar_panel(poly, 0.602, 0.620)
        solar_panel(mirror_poly(poly), 0.602, 0.620)
    # deck perimeter trim (frame lip)
    pts = DECK_CENTER + [DECK_CENTER[0]]
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        add(beam(V(a[0], a[1], DECK_TOP + 0.002), V(b[0], b[1], DECK_TOP + 0.002), 0.012, 0.006, bevel=0.0015),
            n, "deckframe")
    # hinges, stiffeners and support struts of the wings
    for s in (-1, 1):
        for (y0, y1) in ((0.12, 0.66), (-0.48, 0.0)):
            for t in (0.08, 0.5, 0.92):
                y = y0 + (y1 - y0) * t
                add(rod(V(s * 0.462, y - 0.03, 0.611), V(s * 0.462, y + 0.03, 0.611), 0.009, 10), n, "alu")
                add(rod(V(s * 0.462, y - 0.035, 0.611), V(s * 0.462, y - 0.03, 0.611), 0.012, 10), n, "darkmetal")
        # stiffeners under each wing
        for (a, b) in (((0.50, 0.62), (0.90, 0.19)), ((0.50, 0.22), (0.93, 0.13)),
                       ((0.50, -0.05), (1.00, -0.02)), ((0.50, -0.45), (0.95, -0.45))):
            add(beam(V(s * a[0], a[1], 0.596), V(s * b[0], b[1], 0.596), 0.02, 0.012, bevel=0.002), n, "honeycomb")
        # diagonal struts from the WEB to the wing stiffeners
        for (y, ty) in ((0.40, 0.38), (-0.25, -0.24)):
            add(rod(V(s * 0.345, y, 0.50), V(s * 0.72, ty, 0.592), 0.007, 8), n, "alu")
            e1 = sphere(0.011, 8, 6)
            xf(e1, (s * 0.345, y, 0.50))
            add(e1, n, "alu")
        # wing-tip posts / deployment latches (small spikes visible in the photos)
        for (x, y, h) in ((0.99, 0.13, 0.04), (0.64, 0.73, 0.03), (1.09, -0.49, 0.045), (0.69, -0.68, 0.03)):
            add(rod(V(s * x, y, 0.620), V(s * x, y, 0.620 + h), 0.004, 6), n, "alu")
            cap = box(0.014, 0.008, 0.01, 0.0)
            xf(cap, (s * x, y, 0.620 + h))
            add(cap, n, "darkmetal")
    # small bolts along the centre deck edge
    for i in range(14):
        y = -0.70 + i * 0.105
        for s in (-1, 1):
            b = cyl(0.005, 0.006, 6)
            xf(b, (s * 0.44, y, DECK_TOP + 0.006))
            add(b, n, "alu", flat=True)


def body_details():
    n = "Body"
    # raised border frame around every wing panel
    for poly in (WING_F, WING_R, mirror_poly(WING_F), mirror_poly(WING_R)):
        pts = poly + [poly[0]]
        for i in range(len(pts) - 1):
            a, b = pts[i], pts[i + 1]
            add(beam(V(a[0], a[1], 0.6215), V(b[0], b[1], 0.6215), 0.010, 0.004, bevel=0.0), n, "deckframe", flat=True)
        # cell-string busbar strips across the wing
        cx = sum(p[0] for p in poly) / len(poly)
        ys = [p[1] for p in poly]
        add(beam(V(cx, min(ys) + 0.05, 0.6212), V(cx, max(ys) - 0.05, 0.6212), 0.006, 0.002, bevel=0.0), n, "alu", flat=True)
    for s in (-1, 1):
        # connector brackets + harness on the sides of the WEB
        for (y, z) in ((0.30, 0.36), (-0.22, 0.52), (0.42, 0.52)):
            c = box(0.016, 0.05, 0.035, 0.003)
            xf(c, (s * 0.355, y, z))
            add(c, n, "darkmetal")
            for k in range(3):
                add(rod(V(s * 0.362, y - 0.015 + k * 0.015, z), V(s * 0.37, y - 0.015 + k * 0.015, z), 0.004, 8), n, "alu")
        add(sweep([V(s * 0.35, 0.42, 0.50), V(s * 0.355, 0.30, 0.38), V(s * 0.352, 0.16, 0.40), V(s * 0.38, 0.10, 0.43)],
                  r=0.006, sides=6, samples=4), n, "cable")
        add(sweep([V(s * 0.35, -0.22, 0.50), V(s * 0.356, -0.05, 0.47), V(s * 0.38, 0.08, 0.44)],
                  r=0.006, sides=6, samples=4), n, "cable")
        # bolt rows on the blanket edges
        for i in range(10):
            y = -0.34 + i * 0.09
            for z in (0.305, 0.58):
                b = rod(V(s * 0.347, y, z), V(s * 0.352, y, z), 0.0045, 6)
                add(b, n, "alu", flat=True)
        # thermal radiator fins under the deck lip on each side
        for i in range(5):
            f = box(0.004, 0.10, 0.035, 0.0)
            xf(f, (s * (0.37 + i * 0.012), -0.30, 0.575))
            add(f, n, "alu", flat=True)
    # underside: battery box and harness tray
    bb = box(0.36, 0.30, 0.05, 0.008)
    xf(bb, (0, -0.05, 0.245))
    add(bb, n, "kapton")
    tr = box(0.06, 0.70, 0.02, 0.004)
    xf(tr, (0.20, 0.07, 0.255))
    add(tr, n, "deckframe")
    for i in range(6):
        b = rod(V(-0.15 + i * 0.06, -0.20, 0.22), V(-0.15 + i * 0.06, 0.10, 0.22), 0.004, 6)
        add(b, n, "cable")


# --------------------------------------------------------------- deck items
MAST = V(0.03, 0.56, 0.0)
HGA = V(-0.34, -0.22, 0.0)
LGA = V(-0.17, 0.05, 0.0)
UHF = V(0.30, -0.44, 0.0)
CAL = V(0.22, 0.30, 0.0)


def hazcams():
    n = "Body"
    for (y, sgn) in ((0.535, 1), (-0.395, -1)):
        br = box(0.22, 0.03, 0.03, 0.003)
        xf(br, (0, y + sgn * 0.005, 0.565))
        add(br, n, "struct")
        for x in (-0.065, 0.065):
            tilt = Matrix.Rotation(math.radians(-45 * sgn), 3, "X")
            if sgn < 0:
                tilt = Matrix.Rotation(math.radians(180), 3, "Z") @ Matrix.Rotation(math.radians(-45), 3, "X")
            c = box(0.045, 0.05, 0.045, 0.004)
            xf(c, (0, 0, 0), tilt)
            xf(c, (x, y + sgn * 0.03, 0.555))
            add(c, n, "beige")
            fwd = tilt @ Vector((0, 1, 0))
            base = V(x, y + sgn * 0.03, 0.555) + fwd * 0.025
            add(rod(base, base + fwd * 0.018, 0.012, 14, bevel=0.002), n, "darkmetal")
            add(rod(base + fwd * 0.018, base + fwd * 0.021, 0.0085, 14), n, "lens")
            cab = sweep([V(x, y + sgn * 0.01, 0.58), V(x + 0.02, y - sgn * 0.005, 0.59), V(x + 0.04, y - sgn * 0.02, 0.595)],
                        r=0.004, sides=6, samples=3)
            add(cab, n, "cable")


def deck_items():
    n = "Body"
    # ---- PMA base: deploy hinge + clevis (static)
    mx, my = MAST.x, MAST.y
    plate = box(0.16, 0.15, 0.014, 0.003)
    xf(plate, (mx, my, DECK_TOP + 0.007))
    add(plate, n, "alu")
    for dx in (-0.055, 0.055):
        cl = box(0.012, 0.07, 0.06, 0.003)
        xf(cl, (mx + dx, my, DECK_TOP + 0.04))
        add(cl, n, "struct")
    add(rod(V(mx - 0.07, my, 0.66), V(mx + 0.07, my, 0.66), 0.026, 16, bevel=0.003), n, "darkmetal")
    add(rod(V(mx - 0.075, my, 0.66), V(mx - 0.068, my, 0.66), 0.03, 16), n, "alu")
    add(rod(V(mx + 0.068, my, 0.66), V(mx + 0.075, my, 0.66), 0.03, 16), n, "alu")
    # deck feedthrough + cable from mast base into the WEB
    add(rod(V(mx + 0.09, my - 0.06, DECK_TOP), V(mx + 0.09, my - 0.06, DECK_TOP + 0.02), 0.016, 12), n, "darkmetal")
    add(sweep([V(mx + 0.03, my - 0.02, 0.665), V(mx + 0.07, my - 0.05, 0.655), V(mx + 0.09, my - 0.06, DECK_TOP + 0.02)],
              r=0.007, sides=6, samples=4), n, "cable")

    # ---- HGA pedestal (static)
    hx, hy = HGA.x, HGA.y
    add(xf(lathe([(0.0, DECK_TOP), (0.075, DECK_TOP), (0.075, DECK_TOP + 0.01), (0.05, DECK_TOP + 0.02),
                  (0.045, 0.67), (0.0, 0.67)], 24), (hx, hy, 0)), n, "alu")
    for k in range(6):
        a = 2 * math.pi * k / 6
        b = cyl(0.005, 0.006, 6)
        xf(b, (hx + 0.064 * math.cos(a), hy + 0.064 * math.sin(a), DECK_TOP + 0.012))
        add(b, n, "darkmetal", flat=True)
    add(sweep([V(hx + 0.04, hy + 0.02, 0.655), V(hx + 0.10, hy + 0.06, DECK_TOP + 0.01), V(hx + 0.16, hy + 0.06, DECK_TOP + 0.008)],
              r=0.006, sides=6, samples=4), n, "cable")

    # ---- Low-gain antenna (static, thin mast)
    lx, ly = LGA.x, LGA.y
    lb = box(0.05, 0.05, 0.03, 0.004)
    xf(lb, (lx, ly, DECK_TOP + 0.015))
    add(lb, n, "alu")
    add(rod(V(lx, ly, DECK_TOP + 0.03), V(lx, ly, 1.02), 0.012, 12), n, "white")
    for z in (0.80, 0.95):
        add(rod(V(lx, ly, z), V(lx, ly, z + 0.02), 0.016, 12, bevel=0.002), n, "darkmetal")
    add(rod(V(lx, ly, 1.02), V(lx, ly, 1.21), 0.0075, 10), n, "alu")
    add(rod(V(lx, ly, 1.10), V(lx, ly, 1.115), 0.012, 10), n, "stripe")
    add(xf(lathe([(0.0, 1.21), (0.014, 1.212), (0.014, 1.235), (0.008, 1.245), (0.0, 1.246)], 12), (lx, ly, 0)),
        n, "darkmetal")
    # side bracket of the LGA
    add(beam(V(lx, ly, 0.70), V(lx + 0.06, ly, DECK_TOP + 0.005), 0.008, 0.012, up=(0, 1, 0), bevel=0.0), n, "alu")

    # ---- UHF antenna (static whip)
    ux, uy = UHF.x, UHF.y
    add(xf(lathe([(0.0, DECK_TOP), (0.03, DECK_TOP), (0.03, DECK_TOP + 0.02), (0.012, DECK_TOP + 0.04),
                  (0.0, DECK_TOP + 0.04)], 14), (ux, uy, 0)), n, "white")
    add(rod(V(ux, uy, DECK_TOP + 0.04), V(ux, uy, 0.93), 0.005, 8), n, "alu")
    tip = sphere(0.009, 8, 6)
    xf(tip, (ux, uy, 0.93))
    add(tip, n, "darkmetal")
    for z in (0.70, 0.80):
        add(rod(V(ux, uy, z), V(ux, uy, z + 0.012), 0.009, 8), n, "darkmetal")

    # ---- Pancam calibration target ("sundial") on a post
    cx, cy = CAL.x, CAL.y
    add(rod(V(cx, cy, DECK_TOP), V(cx, cy, 0.70), 0.010, 10), n, "alu")
    base = box(0.05, 0.05, 0.01, 0.002)
    xf(base, (cx, cy, DECK_TOP + 0.005))
    add(base, n, "alu")
    plate = box(0.085, 0.085, 0.008, 0.001)
    xf(plate, (cx, cy, 0.704))
    add(plate, n, "caltarget", fit=True)
    add(rod(V(cx, cy, 0.708), V(cx, cy, 0.748), 0.0035, 8), n, "darkmetal")
    for (dx, dy) in ((-0.036, -0.036), (0.036, -0.036), (-0.036, 0.036), (0.036, 0.036)):
        add(rod(V(cx + dx, cy + dy, 0.70), V(cx + dx * 0.4, cy + dy * 0.4, 0.665), 0.003, 6), n, "alu")

    # ---- magnet arrays + small boxes on the deck
    for (x, y) in ((0.30, -0.12), (0.34, -0.06)):
        m = box(0.035, 0.035, 0.012, 0.002)
        xf(m, (x, y, DECK_TOP + 0.006))
        add(m, n, "darkmetal")
        r = rod(V(x, y, DECK_TOP + 0.012), V(x, y, DECK_TOP + 0.016), 0.011, 12)
        add(r, n, "copper")
    for (x, y, sx, sy, sz, tile) in ((-0.10, -0.50, 0.12, 0.08, 0.035, "gold"), (0.12, -0.60, 0.10, 0.07, 0.03, "white"),
                                     (-0.24, 0.40, 0.09, 0.07, 0.03, "beige"), (0.20, 0.06, 0.08, 0.10, 0.025, "gold")):
        b = box(sx, sy, sz, 0.004)
        xf(b, (x, y, DECK_TOP + sz / 2))
        add(b, n, tile)
    # cable runs on deck
    add(sweep([V(-0.24, 0.40, DECK_TOP + 0.03), V(-0.20, 0.30, DECK_TOP + 0.01), V(-0.20, 0.10, DECK_TOP + 0.008),
               V(lx + 0.03, ly + 0.0, DECK_TOP + 0.01)], r=0.005, sides=6, samples=4), n, "cable")
    add(sweep([V(-0.10, -0.46, DECK_TOP + 0.035), V(-0.14, -0.35, DECK_TOP + 0.008), V(hx + 0.16, hy + 0.06, DECK_TOP + 0.008)],
              r=0.005, sides=6, samples=4), n, "cable")


# ------------------------------------------------------------------ PMA mast
def mast():
    m, h = "PMA_Mast", "PMA_Head"
    mx, my = MAST.x, MAST.y
    # lower tube from hinge
    add(rod(V(mx, my, 0.645), V(mx, my, 0.80), 0.046, 24, bevel=0.003), m, "white")
    # copper ring (Mini-TES / azimuth actuator band) with flanges
    add(xf(lathe([(0.0, 0.80), (0.060, 0.80), (0.064, 0.805), (0.064, 0.865), (0.060, 0.87), (0.0, 0.87)], 32),
           (mx, my, 0)), m, "copper")
    for z in (0.80, 0.866):
        add(rod(V(mx, my, z), V(mx, my, z + 0.006), 0.067, 32), m, "darkmetal")
    # upper tube with brown thermal tape bands
    add(rod(V(mx, my, 0.87), V(mx, my, 1.31), 0.041, 24, bevel=0.002), m, "white")
    for (z0, z1) in ((0.97, 1.01), (1.08, 1.095), (1.16, 1.17)):
        add(rod(V(mx, my, z0), V(mx, my, z1), 0.0425, 24), m, "stripe")
    # Mini-TES aperture window (dark slot facing forward)
    w = box(0.03, 0.01, 0.06, 0.002)
    xf(w, (mx, my + 0.041, 1.22))
    add(w, m, "lens")
    # cable running up the mast with clamps
    pts = [V(mx + 0.05, my - 0.02, 0.69), V(mx + 0.052, my - 0.02, 0.90), V(mx + 0.048, my - 0.02, 1.15),
           V(mx + 0.04, my - 0.03, 1.30)]
    add(sweep(pts, r=0.007, sides=6, samples=5), m, "cable")
    for z in (0.75, 0.95, 1.12, 1.25):
        cl = box(0.02, 0.012, 0.012, 0.001)
        xf(cl, (mx + 0.046, my - 0.02, z))
        add(cl, m, "alu")
    # azimuth/elevation head housing
    hb = box(0.10, 0.10, 0.08, 0.008, 2)
    xf(hb, (mx, my, 1.345))
    add(hb, m, "white")
    for s in (-1, 1):
        yk = box(0.016, 0.06, 0.07, 0.004)
        xf(yk, (mx + s * 0.058, my, 1.395))
        add(yk, m, "white")
    add(rod(V(mx - 0.085, my, 1.40), V(mx + 0.085, my, 1.40), 0.026, 20, bevel=0.003), m, "darkmetal")

    # ---- camera head (elevation)
    bar = box(0.40, 0.085, 0.085, 0.006, 2)
    xf(bar, (mx, my, 1.468))
    add(bar, h, "white")
    mount = box(0.09, 0.07, 0.05, 0.005)
    xf(mount, (mx, my, 1.425))
    add(mount, h, "white")
    for s in (-1, 1):
        # Pancam (outer, 30 cm baseline) with filter wheel housing behind
        px = mx + s * 0.15
        pc = box(0.058, 0.11, 0.07, 0.005)
        xf(pc, (px, my + 0.02, 1.468))
        add(pc, h, "beige")
        add(rod(V(px, my + 0.075, 1.468), V(px, my + 0.10, 1.468), 0.019, 18, bevel=0.002), h, "darkmetal")
        add(rod(V(px, my + 0.10, 1.468), V(px, my + 0.103, 1.468), 0.013, 18), h, "lens")
        add(rod(V(px - 0.032 * s, my - 0.01, 1.468), V(px + 0.012 * s, my - 0.01, 1.468), 0.032, 20, bevel=0.002), h, "white")
        sh = box(0.05, 0.02, 0.012, 0.002)
        xf(sh, (px, my + 0.088, 1.494))
        add(sh, h, "darkmetal")
        # Navcam (inner, 20 cm baseline)
        nx = mx + s * 0.095
        nc = box(0.034, 0.075, 0.045, 0.004)
        xf(nc, (nx, my + 0.03, 1.49))
        add(nc, h, "beige")
        add(rod(V(nx, my + 0.067, 1.49), V(nx, my + 0.083, 1.49), 0.011, 14, bevel=0.0015), h, "darkmetal")
        add(rod(V(nx, my + 0.083, 1.49), V(nx, my + 0.085, 1.49), 0.007, 14), h, "lens")
        # end caps of the bar
        ec = box(0.012, 0.075, 0.075, 0.003)
        xf(ec, (mx + s * 0.206, my, 1.468))
        add(ec, h, "darkmetal")
    # top details: sun sensor + small connectors
    ss = box(0.03, 0.03, 0.015, 0.003)
    xf(ss, (mx - 0.04, my - 0.015, 1.518))
    add(ss, h, "darkmetal")
    add(rod(V(mx - 0.04, my - 0.015, 1.525), V(mx - 0.04, my - 0.015, 1.529), 0.008, 12), h, "lens")
    for x in (0.02, 0.06):
        c = box(0.018, 0.022, 0.014, 0.002)
        xf(c, (mx + x, my - 0.02, 1.517))
        add(c, h, "alu")


# ----------------------------------------------------------------------- HGA
def hga():
    g, d = "HGA_Gimbal", "HGA_Dish"
    hx, hy = HGA.x, HGA.y
    add(rod(V(hx, hy, 0.67), V(hx, hy, 0.735), 0.040, 24, bevel=0.004), g, "darkmetal")
    add(rod(V(hx, hy, 0.735), V(hx, hy, 0.745), 0.044, 24), g, "alu")
    # L arm out to the elevation actuator (toward rover left)
    add(beam(V(hx, hy, 0.75), V(hx - 0.05, hy, 0.79), 0.05, 0.02, up=(0, 1, 0), bevel=0.003), g, "struct")
    add(beam(V(hx - 0.045, hy, 0.785), V(hx - 0.045, hy, 0.835), 0.05, 0.016, up=(1, 0, 0), bevel=0.003), g, "struct")
    add(rod(V(hx - 0.10, hy, 0.82), V(hx - 0.04, hy, 0.82), 0.026, 20, bevel=0.003), g, "darkmetal")
    add(sweep([V(hx + 0.03, hy + 0.02, 0.72), V(hx + 0.02, hy + 0.04, 0.78), V(hx - 0.05, hy + 0.03, 0.82)],
              r=0.0045, sides=6, samples=4), g, "cable")

    # dish: flat array face toward +Y, centre offset forward of the elevation axis
    cy = hy + 0.075
    dcen = V(hx - 0.11, cy, 0.82)
    face = lathe([(0.0, 0.012), (0.132, 0.012), (0.142, 0.010), (0.145, 0.0), (0.145, -0.012),
                  (0.12, -0.022), (0.05, -0.03), (0.0, -0.031)], 40)
    xf(face, (0, 0, 0), ROT["Y"])
    xf(face, dcen)
    add(face, d, "white", smooth_angle=50)
    disc = cyl(0.131, 0.004, 40)
    xf(disc, (0, 0, 0), ROT["Y"])
    xf(disc, dcen + V(0, 0.013, 0))
    add(disc, d, "dish")
    rim = lathe([(0.132, 0.0), (0.145, 0.0), (0.145, 0.016), (0.138, 0.016), (0.132, 0.008)], 40, True)
    xf(rim, (0, 0, 0), ROT["Y"])
    xf(rim, dcen)
    add(rim, d, "alu")
    # radiating patch elements (raised rings) on the face
    for r0 in (0.04, 0.08, 0.115):
        ring = lathe([(r0, 0.0), (r0 + 0.006, 0.0), (r0 + 0.006, 0.003), (r0, 0.003)], 40, True)
        xf(ring, (0, 0, 0), ROT["Y"])
        xf(ring, dcen + V(0, 0.015, 0))
        add(ring, d, "darkmetal")
    feed = lathe([(0.0, 0.0), (0.02, 0.0), (0.02, 0.02), (0.012, 0.028), (0.0, 0.028)], 16)
    xf(feed, (0, 0, 0), ROT["Y"])
    xf(feed, dcen + V(0, 0.015, 0))
    add(feed, d, "beige")
    # back mount from elevation actuator to the dish
    add(beam(V(hx - 0.07, hy, 0.82), V(hx - 0.11, cy - 0.03, 0.82), 0.05, 0.03, bevel=0.004), d, "struct")
    for a in range(4):
        ang = math.pi / 4 + a * math.pi / 2
        p = dcen + V(0.09 * math.cos(ang), -0.03, 0.09 * math.sin(ang))
        add(rod(V(hx - 0.11, cy - 0.04, 0.82), p, 0.004, 6), d, "alu")


# ----------------------------------------------------------------------- IDD
S_P = V(0.24, 0.585, 0.40)
E_P = V(-0.21, 0.625, 0.40)
W_P = V(0.07, 0.67, 0.365)
T_P = V(0.03, 0.695, 0.235)


def arm():
    n = "Body"
    # mount bracket on the WEB front face
    mb = box(0.10, 0.07, 0.10, 0.006)
    xf(mb, (S_P.x, 0.555, S_P.z))
    add(mb, n, "struct")
    for dz in (-0.035, 0.035):
        for dx in (-0.035, 0.035):
            b = rod(V(S_P.x + dx, 0.519, S_P.z + dz), V(S_P.x + dx, 0.523, S_P.z + dz), 0.005, 6)
            add(b, n, "darkmetal", flat=True)

    sh, el, wr, tu = "IDD_Shoulder", "IDD_Elbow", "IDD_Wrist", "IDD_Turret"
    # shoulder azimuth + elevation actuators
    add(rod(S_P - V(0, 0, 0.045), S_P + V(0, 0, 0.045), 0.038, 24, bevel=0.004), sh, "darkmetal")
    add(rod(S_P + V(0, 0, 0.045), S_P + V(0, 0, 0.052), 0.030, 20), sh, "alu")
    se = S_P + V(-0.05, 0.0, 0.0)
    add(rod(se - V(0, 0.04, 0), se + V(0, 0.04, 0), 0.032, 24, bevel=0.004), sh, "white")
    add(rod(se + V(0, 0.04, 0), se + V(0, 0.046, 0), 0.026, 20), sh, "alu")
    # upper arm (humerus): tube with end fittings
    a0, a1 = se + V(-0.03, 0, 0), E_P + V(0.045, 0, 0)
    add(rod(a0, a1, 0.024, 16, bevel=0.002), sh, "white")
    for p in (a0, a1):
        add(rod(p - V(0.012, 0, 0), p + V(0.012, 0, 0), 0.029, 16, bevel=0.002), sh, "alu")
    add(sweep([a0 + V(0, 0.0, 0.03), a0.lerp(a1, 0.5) + V(0, 0.0, 0.032), a1 + V(0, 0, 0.03)], r=0.005,
              sides=6, samples=5), sh, "cable")
    # elbow
    add(rod(E_P - V(0, 0, 0.04), E_P + V(0, 0, 0.04), 0.035, 24, bevel=0.004), el, "darkmetal")
    add(rod(E_P + V(0, 0, 0.04), E_P + V(0, 0, 0.046), 0.028, 20), el, "alu")
    fdir = (W_P - E_P).normalized()
    f0, f1 = E_P + fdir * 0.045, W_P - fdir * 0.04
    add(rod(f0, f1, 0.021, 16, bevel=0.002), el, "white")
    for p in (f0, f1):
        add(rod(p - fdir * 0.012, p + fdir * 0.012, 0.026, 16, bevel=0.002), el, "alu")
    add(sweep([f0 + V(0, 0, 0.028), f0.lerp(f1, 0.5) + V(0, 0, 0.03), f1 + V(0, 0, 0.028)], r=0.0045,
              sides=6, samples=5), el, "cable")
    # wrist actuator
    add(rod(W_P - fdir * 0.035, W_P + fdir * 0.035, 0.03, 20, bevel=0.003), wr, "darkmetal")
    add(beam(W_P + V(0, 0.0, -0.01), T_P + V(0.0, -0.005, 0.045), 0.05, 0.03, bevel=0.004), wr, "struct")

    # turret hub: axis along X, 4 tools radiating in the YZ plane
    add(rod(T_P - V(0.04, 0, 0), T_P + V(0.04, 0, 0), 0.046, 24, bevel=0.004), tu, "alu")
    add(rod(T_P + V(0.04, 0, 0), T_P + V(0.05, 0, 0), 0.03, 16), tu, "darkmetal")
    for k in range(4):
        a = k * math.pi / 2
        dvec = V(0, math.cos(a), math.sin(a))
        add(rod(T_P + dvec * 0.04, T_P + dvec * 0.055, 0.036, 20, bevel=0.002), tu, "darkmetal")
    tools()


TOOL_DIR = {"Tool_APXS": V(0, 1, 0), "Tool_MB": V(0, 0, 1), "Tool_MI": V(0, -1, 0), "Tool_RAT": V(0, 0, -1)}


def tool_base(name):
    return T_P + TOOL_DIR[name] * 0.055


def tools():
    # RAT: Rock Abrasion Tool (down)
    d = TOOL_DIR["Tool_RAT"]
    b = tool_base("Tool_RAT")
    add(rod(b, b + d * 0.075, 0.036, 24, bevel=0.004), "Tool_RAT", "beige")
    add(xf(rod(b + d * 0.02, b + d * 0.07, 0.016, 14, bevel=0.002), (0.042, 0.0, 0.0)), "Tool_RAT", "white")
    add(rod(b + d * 0.075, b + d * 0.095, 0.031, 24, bevel=0.002), "Tool_RAT", "darkmetal")
    add(rod(b + d * 0.095, b + d * 0.102, 0.022, 20), "Tool_RAT", "alu")
    for k in range(3):
        a = k * 2 * math.pi / 3
        p = b + d * 0.06 + V(0.036 * math.cos(a), 0.036 * math.sin(a), 0)
        add(rod(p, p + d * 0.045, 0.003, 6), "Tool_RAT", "alu")
    # APXS: Alpha Particle X-ray Spectrometer (forward)
    d = TOOL_DIR["Tool_APXS"]
    b = tool_base("Tool_APXS")
    add(rod(b, b + d * 0.06, 0.03, 20, bevel=0.003), "Tool_APXS", "alu")
    add(rod(b + d * 0.06, b + d * 0.08, 0.024, 20, bevel=0.002), "Tool_APXS", "darkmetal")
    ring = lathe([(0.026, 0.0), (0.036, 0.0), (0.036, 0.004), (0.026, 0.004)], 24, True)
    add(xf(xf(ring, (0, 0, 0), ROT["Y"]), b + d * 0.088), "Tool_APXS", "alu")
    for k in range(3):
        a = k * 2 * math.pi / 3
        p = b + d * 0.06 + V(0.031 * math.cos(a), 0, 0.031 * math.sin(a))
        add(rod(p, p + d * 0.03, 0.0028, 6), "Tool_APXS", "alu")
    add(rod(b + d * 0.08, b + d * 0.083, 0.012, 16), "Tool_APXS", "lens")
    # MB: Mössbauer spectrometer (up) with contact plate
    d = TOOL_DIR["Tool_MB"]
    b = tool_base("Tool_MB")
    add(rod(b, b + d * 0.055, 0.032, 20, bevel=0.003), "Tool_MB", "beige")
    add(rod(b + d * 0.02, b + d * 0.03, 0.034, 20), "Tool_MB", "copper")
    add(rod(b + d * 0.055, b + d * 0.062, 0.042, 24, bevel=0.002), "Tool_MB", "alu")
    add(rod(b + d * 0.062, b + d * 0.066, 0.015, 16), "Tool_MB", "darkmetal")
    # MI: Microscopic Imager (back) with dust cover
    d = TOOL_DIR["Tool_MI"]
    b = tool_base("Tool_MI")
    mi = box(0.045, 0.06, 0.05, 0.004)
    xf(mi, b + d * 0.03)
    add(mi, "Tool_MI", "white")
    add(rod(b + d * 0.06, b + d * 0.085, 0.016, 16, bevel=0.002), "Tool_MI", "darkmetal")
    add(rod(b + d * 0.085, b + d * 0.087, 0.010, 16), "Tool_MI", "lens")
    cov = box(0.036, 0.004, 0.036, 0.002)
    xf(cov, b + d * 0.089 + V(0, 0, 0.022))
    add(cov, "Tool_MI", "copper")
    add(rod(b + d * 0.06 + V(0.02, 0, 0), b + d * 0.095 + V(0.02, 0, -0.01), 0.002, 6), "Tool_MI", "alu")


# ======================================================================= RIG
NODES = []   # (name, parent, pivot_model_frame)
ANCHORS = []  # (name, parent, point_model_frame, doc)


def build_all():
    body()
    body_details()
    hazcams()
    deck_items()
    mast()
    hga()
    arm()
    piv = {s: mobility(s) for s in ("L", "R")}

    NODES.append(("MER_Rover", None, V(0, CENTER_Y, 0)))
    NODES.append(("Body", "MER_Rover", V(0, 0.07, 0.44)))
    for s in ("L", "R"):
        sx = SIDES[s]
        NODES.append((f"Rocker_{s}", "Body", piv[s]["Rocker"]))
        NODES.append((f"Steer_F{s}", f"Rocker_{s}", piv[s]["SteerF"]))
        NODES.append((f"Wheel_F{s}", f"Steer_F{s}", V(sx * WX, WHEEL_Y["F"], WR)))
        NODES.append((f"Bogie_{s}", f"Rocker_{s}", piv[s]["Bogie"]))
        NODES.append((f"Wheel_M{s}", f"Bogie_{s}", V(sx * WX, WHEEL_Y["M"], WR)))
        NODES.append((f"Steer_R{s}", f"Bogie_{s}", piv[s]["SteerR"]))
        NODES.append((f"Wheel_R{s}", f"Steer_R{s}", V(sx * WX, WHEEL_Y["R"], WR)))
    NODES.append(("PMA_Mast", "Body", V(MAST.x, MAST.y, 0.66)))
    NODES.append(("PMA_Head", "PMA_Mast", V(MAST.x, MAST.y, 1.40)))
    NODES.append(("HGA_Gimbal", "Body", V(HGA.x, HGA.y, 0.67)))
    NODES.append(("HGA_Dish", "HGA_Gimbal", V(HGA.x - 0.07, HGA.y, 0.82)))
    NODES.append(("IDD_Shoulder", "Body", S_P))
    NODES.append(("IDD_Elbow", "IDD_Shoulder", E_P))
    NODES.append(("IDD_Wrist", "IDD_Elbow", W_P))
    NODES.append(("IDD_Turret", "IDD_Wrist", T_P))
    for t in TOOL_DIR:
        NODES.append((t, "IDD_Turret", tool_base(t)))

    # gameplay / cinema anchors (empties, no geometry)
    ANCHORS.extend([
        ("Anchor_Pancam", "PMA_Head", V(MAST.x, MAST.y + 0.105, 1.468), "Centro entre as duas Pancams, olhando para a frente."),
        ("Anchor_Navcam", "PMA_Head", V(MAST.x, MAST.y + 0.09, 1.49), "Centro entre as Navcams."),
        ("Anchor_RAT_Contact", "Tool_RAT", tool_base("Tool_RAT") + TOOL_DIR["Tool_RAT"] * 0.102, "Face da broca da RAT (ponto de contato com a rocha)."),
        ("Anchor_APXS_Contact", "Tool_APXS", tool_base("Tool_APXS") + TOOL_DIR["Tool_APXS"] * 0.092, "Anel de contato do APXS."),
        ("Anchor_MB_Contact", "Tool_MB", tool_base("Tool_MB") + TOOL_DIR["Tool_MB"] * 0.066, "Placa de contato do Mössbauer."),
        ("Anchor_MI_Focus", "Tool_MI", tool_base("Tool_MI") + TOOL_DIR["Tool_MI"] * 0.12, "Ponto de foco do Microscopic Imager (~3 cm à frente da lente)."),
        ("Anchor_HGA_Boresight", "HGA_Dish", V(HGA.x - 0.11, HGA.y + 0.115, 0.82), "Centro da face da antena de alto ganho (aponta para +Z local em repouso)."),
        ("Anchor_CamFollow", "Body", V(0, 0.07, 0.90), "Ponto de mira da câmera de acompanhamento (acima do deck)."),
        ("Anchor_Track_L", "MER_Rover", V(-WX, WHEEL_Y["R"], 0.0), "Contato da roda traseira esquerda com o solo (rastro)."),
        ("Anchor_Track_R", "MER_Rover", V(WX, WHEEL_Y["R"], 0.0), "Contato da roda traseira direita com o solo (rastro)."),
    ])


def make_materials():
    a, nrm, orm = tex.build_atlas()
    sa, sn, so = tex.build_solar()

    def img(name, arr, colorspace):
        h, w, _ = arr.shape
        im = bpy.data.images.new(name, w, h, alpha=False)
        im.colorspace_settings.name = colorspace
        px = np.ones((h, w, 4), dtype=np.float32)
        px[..., :3] = arr
        im.pixels.foreach_set(px.ravel())
        path = os.path.join(OUT, "textures", name + ".png")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        im.filepath_raw = path
        im.file_format = "PNG"
        im.save()
        im.filepath = path
        return im

    def mat(name, ia, inn, io):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        bsdf = nt.nodes["Principled BSDF"]
        ta = nt.nodes.new("ShaderNodeTexImage"); ta.image = ia
        to = nt.nodes.new("ShaderNodeTexImage"); to.image = io
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = inn
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(ta.outputs["Color"], bsdf.inputs["Base Color"])
        nt.links.new(to.outputs["Color"], sep.inputs["Color"])
        nt.links.new(sep.outputs["Green"], bsdf.inputs["Roughness"])
        nt.links.new(sep.outputs["Blue"], bsdf.inputs["Metallic"])
        nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
        return m

    m_atlas = mat("M_MER_Atlas", img("T_MER_Atlas_BaseColor", a, "sRGB"), img("T_MER_Atlas_Normal", nrm, "Non-Color"),
                  img("T_MER_Atlas_ORM", orm, "Non-Color"))
    m_solar = mat("M_MER_Solar", img("T_MER_Solar_BaseColor", sa, "sRGB"), img("T_MER_Solar_Normal", sn, "Non-Color"),
                  img("T_MER_Solar_ORM", so, "Non-Color"))
    return m_atlas, m_solar


def assemble(m_atlas, m_solar):
    scene = bpy.context.scene
    col = scene.collection
    objs = {}
    by_node = {}
    for node, me in PARTS:
        by_node.setdefault(node, []).append(me)
    known = {n for n, _, _ in NODES}
    missing = set(by_node) - known
    assert not missing, f"parts without rig node: {missing}"
    for name, parent, pivot in NODES:
        gpiv = gp(pivot)
        meshes = by_node.get(name)
        if meshes:
            bm = bmesh.new()
            for me in meshes:
                bm.from_mesh(me)
            bmesh.ops.translate(bm, vec=-gpiv, verts=bm.verts)
            bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
            data = bpy.data.meshes.new(name + "_Mesh")
            bm.to_mesh(data)
            bm.free()
            data.materials.append(m_atlas)
            data.materials.append(m_solar)
            ob = bpy.data.objects.new(name, data)
        else:
            ob = bpy.data.objects.new(name, None)
            ob.empty_display_type = "ARROWS"
            ob.empty_display_size = 0.08
        col.objects.link(ob)
        if parent:
            ob.parent = objs[parent]
            ob.location = gpiv - gp([p for n, _, p in NODES if n == parent][0])
        else:
            ob.location = gpiv
        objs[name] = ob
    for me in [m for _, m in PARTS]:
        bpy.data.meshes.remove(me)
    for name, parent, point, _doc in ANCHORS:
        ob = bpy.data.objects.new(name, None)
        ob.empty_display_type = "SPHERE"
        ob.empty_display_size = 0.02
        col.objects.link(ob)
        ob.parent = objs[parent]
        ob.location = gp(point) - gp([p for n, _, p in NODES if n == parent][0])
    return objs


def animate(objs):
    """Demo clip (8 s @ 30 fps) exercising every rig node. Export frame: forward = -Y."""
    sc = bpy.context.scene
    sc.render.fps = 30
    sc.frame_start, sc.frame_end = 1, 240

    def key(name, frame, rot_deg):
        ob = objs[name]
        ob.rotation_euler = Euler([math.radians(a) for a in rot_deg], "XYZ")
        ob.keyframe_insert("rotation_euler", frame=frame)

    for w in ("FL", "FR", "ML", "MR", "RL", "RR"):
        key(f"Wheel_{w}", 1, (0, 0, 0))
        key(f"Wheel_{w}", 240, (360 * 2.5, 0, 0))     # rolls forward (top of wheel moves to -Y)
    for s in ("L", "R"):
        for f, a in ((1, 0), (30, 0), (55, 28), (95, 28), (120, 0)):
            key(f"Steer_F{s}", f, (0, 0, a))
            key(f"Steer_R{s}", f, (0, 0, -a))
        sg = 1 if s == "L" else -1
        for f, a in ((1, 0), (130, 0), (160, 7), (190, -5), (220, 0)):
            key(f"Rocker_{s}", f, (sg * a, 0, 0))
            key(f"Bogie_{s}", f, (-sg * a * 1.4, 0, 0))
    for f, az in ((1, 0), (20, 0), (60, 45), (110, -45), (150, 0)):
        key("PMA_Mast", f, (0, 0, az))
    for f, el in ((1, 0), (60, 0), (85, 18), (120, -12), (150, 0)):
        key("PMA_Head", f, (el, 0, 0))
    for f, az in ((1, 0), (100, 0), (150, -55), (230, -55), (240, 0)):
        key("HGA_Gimbal", f, (0, 0, az))
    for f, el in ((1, 0), (120, 0), (165, 35), (230, 35), (240, 0)):
        key("HGA_Dish", f, (el, 0, 0))
    # arm deploy (model frame: shoulder swings -80 deg about Z, elbow +150 deg)
    for f, sz, sy in ((1, 0, 0), (110, 0, 0), (160, -80, 0), (190, -80, 8)):
        key("IDD_Shoulder", f, (0, sy, sz))
    for f, ez in ((1, 0), (120, 0), (175, 150), (190, 150)):
        key("IDD_Elbow", f, (0, 0, ez))
    for f, wz in ((1, 0), (170, 0), (195, -60), (240, -60)):
        key("IDD_Wrist", f, (0, 0, wz))
    for f, tx in ((1, 0), (195, 0), (225, 90), (240, 90)):
        key("IDD_Turret", f, (tx, 0, 0))
    # push every object's action into an NLA track named "RigDemo" -> one glTF clip
    for ob in objs.values():
        ad = ob.animation_data
        if not ad or not ad.action:
            continue
        act = ad.action
        slot = ad.action_slot
        tr = ad.nla_tracks.new()
        tr.name = "RigDemo"
        st = tr.strips.new("RigDemo", 1, act)
        if slot is not None and hasattr(st, "action_slot"):
            st.action_slot = slot
        ad.action = None


def tri_count(objs):
    total = 0
    per = {}
    dg = bpy.context.evaluated_depsgraph_get()
    for name, ob in objs.items():
        if ob.type != "MESH":
            continue
        me = ob.data
        me.calc_loop_triangles()
        per[name] = len(me.loop_triangles)
        total += per[name]
    return total, per


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    build_all()
    m_atlas, m_solar = make_materials()
    objs = assemble(m_atlas, m_solar)
    bpy.context.scene.name = "MER_RigDemo"
    animate(objs)
    total, per = tri_count(objs)
    print("TRIANGLES", total)
    for k, v in sorted(per.items(), key=lambda kv: -kv[1]):
        print(f"  {k:14s} {v}")
    bpy.context.scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "MER_Rover.blend"))
    with open(os.path.join(OUT, "rig.txt"), "w") as fh:
        for name, parent, pivot in NODES:
            fh.write(f"{name}\t{parent}\t{tuple(round(c, 4) for c in gp(pivot))}\n")
        for name, parent, point, doc in ANCHORS:
            fh.write(f"{name}\t{parent}\t{tuple(round(c, 4) for c in gp(point))}\t{doc}\n")
        fh.write(f"TRIANGLES\t{total}\n")


if __name__ == "__main__":
    main()
