"""Move the WHOLE TV camera from StaticBody into CameraHousing in SurveyorRecoverable.fbx (binary FBX, stdlib only).

Why: prepare_hardware.py picked faces whose centre lay within 19 cm of CameraMount. CameraMount sits ~14.5 cm off the
camera axis (MirrorPivot), so only the half of the housing nearest the mount was separated; the other half and its rings
stayed fused to the probe. The astronaut then lifted a half camera and left a hollow shell on the tripod.

Rule used here (per connected piece, never per face, so no piece is ever torn):
  a StaticBody piece belongs to the camera when its bounding box lies inside
  x in [-14.5, 14.5] cm, z in [CAMERA_Z - 15, CAMERA_Z + 15] cm and y in [88.5, 153] cm.
The mounting plate (y 84.5-87.5), its eight bolts, and the tripod legs stay on the probe: they are the mount.

Usage:  python fix_recoverable_camera.py [path/to/SurveyorRecoverable.fbx]      (idempotent; verifies after writing)
"""
import array
import struct
import sys
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DEFAULT = ROOT / "Assets/_Game/Chapters/Surveyor/Documentary/Art/SurveyorRecoverable.fbx"
CAMERA_X, CAMERA_Z, RADIUS, Y_MIN, Y_MAX = 0.0, -82.0, 14.5, 88.5, 153.0
ARRAY_CODES = {"f": "f", "d": "d", "i": "i", "l": "q", "b": "b"}


class Node:
    def __init__(self, name, props, children, sentinel):
        self.name, self.props, self.children, self.sentinel = name, props, children, sentinel

    def find(self, name):
        return [c for c in self.children if c.name == name]

    def first(self, name):
        found = self.find(name)
        return found[0] if found else None


def parse(data):
    version = struct.unpack("<I", data[23:27])[0]
    wide = version >= 7500
    head, head_size = ("<QQQB", 25) if wide else ("<IIIB", 13)

    def read(pos):
        end, count, _, name_len = struct.unpack(head, data[pos:pos + head_size]); pos += head_size
        if end == 0:
            return None, pos
        name = data[pos:pos + name_len].decode("latin1"); pos += name_len
        props = []
        for _ in range(count):
            code = chr(data[pos]); pos += 1
            if code in "YCFDIL":
                fmt = {"Y": "<h", "C": "<B", "F": "<f", "D": "<d", "I": "<i", "L": "<q"}[code]
                props.append((code, struct.unpack(fmt, data[pos:pos + struct.calcsize(fmt)])[0])); pos += struct.calcsize(fmt)
            elif code in ARRAY_CODES:
                length, encoding, compressed = struct.unpack("<III", data[pos:pos + 12]); pos += 12
                raw = data[pos:pos + compressed]; pos += compressed
                if encoding: raw = zlib.decompress(raw)
                values = array.array(ARRAY_CODES[code]); values.frombytes(raw)
                assert len(values) == length
                props.append((code, values))
            elif code in "SR":
                size = struct.unpack("<I", data[pos:pos + 4])[0]; pos += 4
                props.append((code, bytes(data[pos:pos + size]))); pos += size
            else:
                raise ValueError("unknown property type " + code)
        children, sentinel = [], False
        while pos < end:
            child, pos = read(pos)
            if child is None:
                sentinel = True; break
            children.append(child)
        return Node(name, props, children, sentinel), end

    nodes, pos = [], 27
    while True:
        node, pos = read(pos)
        if node is None:
            break
        nodes.append(node)
    return data[:27], nodes, data[pos:]


def write(header, nodes, footer_template):
    out = bytearray(header)
    wide = struct.unpack("<I", header[23:27])[0] >= 7500
    fields, record, null = ("<QQQ", 24, 25) if wide else ("<III", 12, 13)

    def put(node):
        start = len(out)
        out.extend(b"\0" * record)  # end offset, property count, property bytes: patched below
        out.append(len(node.name)); out.extend(node.name.encode("latin1"))
        props_start = len(out)
        for code, value in node.props:
            out.extend(code.encode("latin1"))
            if code in "YCFDIL":
                fmt = {"Y": "<h", "C": "<B", "F": "<f", "D": "<d", "I": "<i", "L": "<q"}[code]
                out.extend(struct.pack(fmt, value))
            elif code in ARRAY_CODES:
                raw = value.tobytes(); packed = zlib.compress(raw, 6)
                if len(packed) < len(raw):
                    out.extend(struct.pack("<III", len(value), 1, len(packed))); out.extend(packed)
                else:
                    out.extend(struct.pack("<III", len(value), 0, len(raw))); out.extend(raw)
            else:
                out.extend(struct.pack("<I", len(value))); out.extend(value)
        props_len = len(out) - props_start
        for child in node.children: put(child)
        if node.sentinel: out.extend(b"\0" * null)
        struct.pack_into(fields, out, start, len(out), len(node.props), props_len)

    for node in nodes: put(node)
    out.extend(b"\0" * null)
    # Footer: 16-byte id, zero padding to a 16-byte boundary, then the fixed tail (int 0, version, 120 zeros, magic).
    ident, tail = footer_template[:16], footer_template[-144:]
    out.extend(ident)
    pad = (-(len(out) + 4)) % 16 or 16  # the version field after the leading int 0 must start on a 16-byte boundary
    out.extend(b"\0" * pad); out.extend(tail)
    return bytes(out)


def polygons(geometry):
    indices = geometry.first("PolygonVertexIndex").props[0][1]
    polys, current = [], []
    for k, value in enumerate(indices):
        if value < 0: current.append((~value, k)); polys.append(current); current = []
        else: current.append((value, k))
    return polys


def layers(geometry):
    normal, uv = geometry.first("LayerElementNormal"), geometry.first("LayerElementUV")
    return (normal.first("Normals").props[0][1], normal.first("NormalsIndex").props[0][1],
            uv.first("UV").props[0][1], uv.first("UVIndex").props[0][1])


def extract(geometry, poly_ids):
    """Standalone copy of the chosen polygons: positions, per-corner normals and per-corner UVs."""
    verts = geometry.first("Vertices").props[0][1]
    normals, normal_index, uvs, uv_index = layers(geometry)
    polys = polygons(geometry); result = []
    for p in poly_ids:
        corners = []
        for vertex, corner in polys[p]:
            corners.append((tuple(verts[3 * vertex:3 * vertex + 3]), tuple(normals[3 * normal_index[corner]:3 * normal_index[corner] + 3]),
                            tuple(uvs[2 * uv_index[corner]:2 * uv_index[corner] + 2])))
        result.append(corners)
    return result


def rebuild(geometry, polys_data):
    """Replace the polygon data of a geometry with the given polygons (dedupes positions, normals and UVs)."""
    vertex_ids, vertex_list, normal_ids, normal_list, uv_ids, uv_list = {}, [], {}, [], {}, []
    indices, n_index, u_index = array.array("i"), array.array("i"), array.array("i")
    for corners in polys_data:
        for k, (pos, nrm, uv) in enumerate(corners):
            vertex = vertex_ids.setdefault(pos, len(vertex_list))
            if vertex == len(vertex_list): vertex_list.append(pos)
            n = normal_ids.setdefault(nrm, len(normal_list))
            if n == len(normal_list): normal_list.append(nrm)
            u = uv_ids.setdefault(uv, len(uv_list))
            if u == len(uv_list): uv_list.append(uv)
            indices.append(~vertex if k == len(corners) - 1 else vertex); n_index.append(n); u_index.append(u)
    geometry.first("Vertices").props[0] = ("d", array.array("d", [c for p in vertex_list for c in p]))
    geometry.first("PolygonVertexIndex").props[0] = ("i", indices)
    geometry.children = [c for c in geometry.children if c.name != "Edges"]
    normal = geometry.first("LayerElementNormal"); uv = geometry.first("LayerElementUV")
    normal.first("Normals").props[0] = ("d", array.array("d", [c for p in normal_list for c in p]))
    normal.first("NormalsIndex").props[0] = ("i", n_index)
    uv.first("UV").props[0] = ("d", array.array("d", [c for p in uv_list for c in p]))
    uv.first("UVIndex").props[0] = ("i", u_index)


def components(geometry):
    verts = geometry.first("Vertices").props[0][1]; polys = polygons(geometry)
    parent = list(range(len(verts) // 3))
    def root(a):
        while parent[a] != a: parent[a] = parent[parent[a]]; a = parent[a]
        return a
    for poly in polys:
        for vertex, _ in poly[1:]: parent[root(vertex)] = root(poly[0][0])
    groups = {}
    for p, poly in enumerate(polys): groups.setdefault(root(poly[0][0]), []).append(p)
    return verts, polys, list(groups.values())


def resolve(objects, connections):
    names = {n.props[0][1]: n.props[1][1].split(b"\0")[0].decode() for n in objects.children if n.name in ("Model", "Geometry")}
    kinds = {n.props[0][1]: n.name for n in objects.children if n.name in ("Model", "Geometry")}
    mesh = {}
    for c in connections.children:
        if c.props[0][1] == b"OO":
            child, parent = c.props[1][1], c.props[2][1]
            if kinds.get(child) == "Geometry" and kinds.get(parent) == "Model": mesh[names[parent]] = child
    return {n.props[0][1]: n for n in objects.children if n.name == "Geometry"}, mesh


def main(path):
    path = Path(path); data = path.read_bytes()
    header, nodes, footer = parse(data)
    top = {n.name: n for n in nodes}
    geometries, mesh = resolve(top["Objects"], top["Connections"])
    static, housing = geometries[mesh["StaticBody"]], geometries[mesh["CameraHousing"]]
    verts, polys, groups = components(static)
    move, pieces = [], 0
    for group in groups:
        ids = {v for p in group for v, _ in polys[p]}
        xs, ys, zs = [verts[3 * v] for v in ids], [verts[3 * v + 1] for v in ids], [verts[3 * v + 2] for v in ids]
        if (min(xs) >= CAMERA_X - RADIUS and max(xs) <= CAMERA_X + RADIUS and min(zs) >= CAMERA_Z - 15 and max(zs) <= CAMERA_Z + 15
                and min(ys) >= Y_MIN and max(ys) <= Y_MAX):
            move.extend(group); pieces += 1
    if not move:
        print("Nothing to move: the camera already lives entirely in CameraHousing."); return
    moved = set(move)
    keep = [p for p in range(len(polys)) if p not in moved]
    camera_polys = extract(housing, range(len(polygons(housing)))) + extract(static, sorted(moved))
    static_polys = extract(static, keep)
    rebuild(housing, camera_polys); rebuild(static, static_polys)
    path.write_bytes(write(header, nodes, footer))
    print("Moved %d polygons in %d pieces into CameraHousing; StaticBody keeps %d polygons." % (len(moved), pieces, len(keep)))
    verify(path)


def verify(path):
    header, nodes, _ = parse(Path(path).read_bytes())
    top = {n.name: n for n in nodes}
    geometries, mesh = resolve(top["Objects"], top["Connections"])
    for name in ("CameraHousing", "StaticBody", "MirrorHead"):
        geometry = geometries[mesh[name]]; v = geometry.first("Vertices").props[0][1]
        polys = polygons(geometry); normals, n_index, uvs, u_index = layers(geometry)
        assert max(max(i for i, _ in p) for p in polys) < len(v) // 3 and max(n_index) < len(normals) // 3 and max(u_index) < len(uvs) // 2
        print("%-14s polys %5d  x %.1f..%.1f  y %.1f..%.1f  z %.1f..%.1f" % (name, len(polys), min(v[0::3]), max(v[0::3]), min(v[1::3]), max(v[1::3]), min(v[2::3]), max(v[2::3])))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else DEFAULT)
