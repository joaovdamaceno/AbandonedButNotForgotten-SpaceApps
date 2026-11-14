#!/usr/bin/env python3
"""PIO-002 · Conversor determinístico do modelo da Pioneer 10 (GLB com Draco/WebP) para dados do projeto.

Não há Blender nesta máquina e o GLB usa Draco: este script decodifica a geometria, separa as peças pelo que o
roteiro anima (IMPORT_RULES §4), troca para a convenção da Unity, empacota as texturas num único atlas e grava:

  Data/Assets/Pioneer10.json            manifesto: origem, SHA-256, medidas, peças, pivôs, sockets (conferência)
  Data/Assets/Pioneer10.geometry.json   geometria por peça (posições/normais/UV em metros, espaço local do pivô)
  Assets/_Game/Chapters/Pioneer10/Art/Probe/Textures/PioneerAtlas.png   atlas 2048² (cor base com AO já assado)

O GLB original NÃO é alterado nem entra em Assets/: fica em ArtSource/Pioneer10/PIO-002/original/ (LFS).
Um passo de Editor (Editor/PioneerProbeBuilder.cs) transforma esses dados em malhas, materiais e prefab.

Espaço de saída (Unity, 1 unidade = 1 m): +Z = eixo da antena = eixo de spin, saindo da boca do refletor (a mesma
convenção do palco da PIO-003), com z = 0 no vértice do refletor; +Y = cima do glTF. A conversão para mão esquerda
inverte Z e a ordem dos vértices.

Uso:
    python Tools/convert_pioneer10.py --glb "C:/.../Pioneer 10.glb"                 # grava os dados
    python Tools/convert_pioneer10.py --glb ... --plot Docs/Art/Pioneer10_Probe      # + projeções coloridas das peças
    python Tools/convert_pioneer10.py --glb ... --dry-run                            # só relata
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import struct
import sys
from pathlib import Path

import DracoPy
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ATLAS_SIZE = 2048
PAD = 2  # pixels de respiro dentro de cada janela do atlas (reduz vazamento nos mips)

# Peças separadas porque o roteiro as usa (IMPORT_RULES §4) ou porque o rig as articula. Ordem = ordem no prefab.
# "Bus" é o corpo estático: compartimento, suportes, tripé do alimentador e ferragens do aro.
PARTS = ["Bus", "Dish", "Feed", "RtgBoomA", "RtgBoomB", "MagnetometerBoom"]

BOOM_ANGLE_DEGREES = 30.0   # hastes dos RTGs a 30° e 150° no plano XY (medido nas pontas dos geradores do modelo)
BOOM_HINGE_T = 0.9          # dobradiça a 0,9 m do eixo, na borda do corpo (a parte interna da haste fica no corpo)
BOOM_CAPSULE_RADIUS = 0.30  # distância máxima ao eixo da haste, em metros
BOOM_MAX_Z = 0.05           # a haste fica atrás do plano do aro do refletor (z glTF > −0,05)
AXIS_XY = np.array([0.002, 0.056])  # eixo de spin = eixo da antena: centro do refletor e do alimentador


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_glb(path: Path):
    data = path.read_bytes()
    magic, version, _ = struct.unpack("<4sII", data[:12])
    if magic != b"glTF" or version != 2:
        raise SystemExit("Não é um GLB 2.0.")
    json_len, _ = struct.unpack("<I4s", data[12:20])
    gltf = json.loads(data[20:20 + json_len])
    offset = 20 + json_len
    bin_len, _ = struct.unpack("<I4s", data[offset:offset + 8])
    return gltf, data[offset + 8:offset + 8 + bin_len]


def node_matrix(node) -> np.ndarray:
    if "rotation" in node or "matrix" in node:
        raise SystemExit("O modelo tem rotação/matriz por nó; o conversor só trata translação e escala.")
    m = np.eye(4)
    m[:3, :3] = np.diag(node.get("scale", [1, 1, 1]))
    m[:3, 3] = node.get("translation", [0, 0, 0])
    return m


def decode(gltf, binary, primitive):
    ext = primitive["extensions"]["KHR_draco_mesh_compression"]
    view = gltf["bufferViews"][ext["bufferView"]]
    start = view.get("byteOffset", 0)
    return DracoPy.decode(binary[start:start + view["byteLength"]])


def image_bytes(gltf, binary, index):
    view = gltf["bufferViews"][gltf["images"][index]["bufferView"]]
    start = view.get("byteOffset", 0)
    return binary[start:start + view["byteLength"]]


def components(faces: np.ndarray, n_vertices: int):
    parent = list(range(n_vertices))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    for a, b, c in faces:
        ra = find(int(a))
        parent[find(int(b))] = ra
        parent[find(int(c))] = ra
    groups = {}
    for tri in faces:
        groups.setdefault(find(int(tri[0])), []).append(tri)
    return [np.asarray(g) for g in groups.values()]


def boom_projection(point_xy, boom_a: bool):
    """(distância perpendicular ao eixo da haste, posição ao longo dela) para um ponto XY em coordenadas glTF."""
    angle = np.radians(BOOM_ANGLE_DEGREES if boom_a else 180.0 - BOOM_ANGLE_DEGREES)
    direction = np.array([np.cos(angle), np.sin(angle)])
    rel = np.asarray(point_xy) - AXIS_XY
    return abs(float(rel[0] * direction[1] - rel[1] * direction[0])), float(rel @ direction)


def classify(centroid: np.ndarray, extent: np.ndarray, node_name: str) -> str:
    """Peça a que um componente conexo pertence: origem, tamanho e centroide em coordenadas glTF (metros)."""
    x, y, z = centroid
    if node_name == "AO_dish" and extent[0] >= 2.5:
        return "Dish"  # casca do refletor: os componentes grandes (face, dorso e aro)
    if y < -1.15:
        return "MagnetometerBoom"
    if z < -0.55:
        return "Feed"  # alimentador, na frente do refletor (em glTF o refletor abre para −Z)
    for boom_a, name in ((True, "RtgBoomA"), (False, "RtgBoomB")):
        distance, along = boom_projection((x, y), boom_a)
        if distance < BOOM_CAPSULE_RADIUS and along > BOOM_HINGE_T and z > -BOOM_MAX_Z:
            return name
    return "Bus"


# ---------------------------------------------------------------------------------------------------------------
# Atlas: janelas de 1024 (cor com AO), 512/256 (texturas reduzidas) e cores chapadas, tudo em 2048².
# ---------------------------------------------------------------------------------------------------------------


class Atlas:
    def __init__(self):
        self.image = Image.new("RGB", (ATLAS_SIZE, ATLAS_SIZE), (0, 0, 0))
        self.windows = {}  # nome -> (x, y, w, h) em pixels de imagem (origem no canto superior esquerdo)

    def place(self, name, picture: Image.Image, x, y, w, h):
        self.image.paste(picture.convert("RGB").resize((w, h), Image.LANCZOS), (x, y))
        self.windows[name] = (x, y, w, h)

    def flat(self, name, color, x, y, w, h):
        self.image.paste(Image.new("RGB", (w, h), color), (x, y))
        self.windows[name] = (x, y, w, h)

    def remap(self, name, uv: np.ndarray) -> np.ndarray:
        """UV glTF (origem superior esquerda) -> UV Unity do atlas (origem inferior esquerda)."""
        x, y, w, h = self.windows[name]
        u = np.clip(uv[:, 0], 0, 1)
        v = np.clip(uv[:, 1], 0, 1)
        px = x + PAD + u * (w - 2 * PAD)
        py = y + PAD + v * (h - 2 * PAD)
        return np.stack([px / ATLAS_SIZE, 1.0 - py / ATLAS_SIZE], axis=1)


def build_atlas(gltf, binary):
    def png(material_name):
        for material in gltf["materials"]:
            if material["name"] == material_name:
                texture = gltf["textures"][material["pbrMetallicRoughness"]["baseColorTexture"]["index"]]
                return Image.open(io.BytesIO(image_bytes(gltf, binary, texture["source"]))).copy()
        raise SystemExit("material sem textura: " + material_name)

    atlas = Atlas()
    atlas.place("dish_AO", png("dish_AO"), 0, 0, 1024, 1024)
    atlas.place("other_AO", png("other_AO"), 1024, 0, 1024, 1024)
    atlas.place("RTG_AO_new", png("RTG_AO_new"), 0, 1024, 1024, 1024)
    atlas.place("Pioneer_tex_01", png("Pioneer_tex_01"), 1024, 1024, 512, 512)
    atlas.place("pioneer_tex_02", png("pioneer_tex_02"), 1536, 1024, 512, 512)
    atlas.place("Tex_black", png("Tex_black"), 1024, 1536, 256, 256)
    atlas.place("foil_gold", png("foil_gold"), 1280, 1536, 256, 256)
    atlas.place("foil_silver", png("foil_silver"), 1536, 1536, 256, 256)
    silver = tuple(int(round(255 * (0.6432599425315857 ** (1 / 2.2)))) for _ in range(3))  # fator linear -> sRGB
    atlas.flat("silver_flat", silver, 1792, 1536, 256, 256)
    return atlas


MATERIAL_WINDOW = {
    "Pioneer_silver_01": "silver_flat",
    "dish_AO": "dish_AO",
    "Tex_black": "Tex_black",
    "Pioneer_silver_02": "silver_flat",
    "other_AO": "other_AO",
    "Pioneer_tex_01": "Pioneer_tex_01",
    "pioneer_tex_02": "pioneer_tex_02",
    "foil_gold": "foil_gold",
    "foil_silver": "foil_silver",
    "RTG_AO_new": "RTG_AO_new",
}
FLAT_WINDOWS = {"silver_flat"}  # cor chapada: ignora o UV original


def convert(glb_path: Path):
    gltf, binary = load_glb(glb_path)
    atlas = build_atlas(gltf, binary)

    # Vértice do refletor = z (glTF) máximo da casca; vira z = 0 do espaço de saída (convenção da PIO-003).
    dish_node = next(n for n in gltf["nodes"] if n["name"] == "AO_dish")
    dish_points = np.asarray(decode(gltf, binary, gltf["meshes"][dish_node["mesh"]]["primitives"][0]).points, dtype=np.float64)
    dish_world = (np.c_[dish_points, np.ones(len(dish_points))] @ node_matrix(dish_node).T)[:, :3]
    dish_vertex_z = float(dish_world[:, 2].max())

    def to_unity(p):
        # glTF (mão direita) -> Unity (mão esquerda): inverte Z (o refletor passa a abrir para +Z); recentra no eixo.
        return np.array([p[0] - AXIS_XY[0], p[1] - AXIS_XY[1], -p[2] + dish_vertex_z])

    buckets = {name: {"pos": [], "nrm": [], "uv": [], "tri": []} for name in PARTS}
    stats = {name: 0 for name in PARTS}
    dropped = []
    for node in gltf["nodes"]:
        matrix = node_matrix(node)
        for primitive in gltf["meshes"][node["mesh"]]["primitives"]:
            material = gltf["materials"][primitive["material"]]["name"]
            d = decode(gltf, binary, primitive)
            points = np.asarray(d.points, dtype=np.float64)
            world = (np.c_[points, np.ones(len(points))] @ matrix.T)[:, :3]
            faces = np.asarray(d.faces)

            if material == "norm_trans":
                # Quadrilátero sem textura (2 triângulos) dentro de uma caixa de instrumento no topo do corpo: parece
                # uma janela transparente, mas não traz alfa nem textura e fecharia a caixa de branco. Descartado.
                dropped.append({"node": node["name"], "material": material, "triangles": int(len(faces)),
                                "reason": "quadrilátero sem textura nem alfa dentro de uma caixa de instrumento; não é a placa"})
                continue

            window = MATERIAL_WINDOW[material]
            normals = np.asarray(d.normals, dtype=np.float64)
            uv = np.asarray(d.tex_coord, dtype=np.float64)
            atlas_uv = atlas.remap(window, np.full_like(uv, 0.5)) if window in FLAT_WINDOWS else atlas.remap(window, uv)
            for component in components(faces, len(points)):
                used = np.unique(component.ravel())
                extent = world[used].max(axis=0) - world[used].min(axis=0)
                part = classify(world[used].mean(axis=0), extent, node["name"])
                bucket = buckets[part]
                remap = {int(old): i for i, old in enumerate(used)}
                base = len(bucket["pos"]) // 3
                for old in used:
                    bucket["pos"].extend(to_unity(world[old]).tolist())
                    n = normals[old] / max(np.linalg.norm(normals[old]), 1e-9)
                    bucket["nrm"].extend([n[0], n[1], -n[2]])
                    bucket["uv"].extend(atlas_uv[old].tolist())
                for a, b, c in component:
                    # A inversão de Z espelha o modelo: inverter a ordem preserva a face visível.
                    bucket["tri"].extend([base + remap[int(a)], base + remap[int(c)], base + remap[int(b)]])
                stats[part] += len(component)
    return atlas, buckets, stats, dropped


def np_pos(bucket):
    return np.asarray(bucket["pos"]).reshape(-1, 3)


def round_list(values, digits=5):
    return [round(float(v), digits) for v in values]


def find_support_struts(bus_bucket):
    """Pares de suportes da antena no corpo: hastes finas e longas do aro do refletor até o corpo.

    Devolve, por lado (+1 = +X, -1 = -X), as duas hastes como (ponta no aro, ponta no corpo), em espaço Unity.
    """
    pos = np_pos(bus_bucket)
    tri = np.asarray(bus_bucket["tri"]).reshape(-1, 3)
    sides = {1: [], -1: []}
    for component in components(tri, len(pos)):
        used = np.unique(component.ravel())
        pts = pos[used]
        c = pts - pts.mean(axis=0)
        _, _, vt = np.linalg.svd(c, full_matrices=False)
        along = c @ vt[0]
        length = float(along.max() - along.min())
        thick = max(float((c @ vt[1]).max() - (c @ vt[1]).min()), float((c @ vt[2]).max() - (c @ vt[2]).min()))
        if not (1.0 <= length <= 1.3 and thick < 0.02):
            continue
        ends = (pts[np.argmin(along)], pts[np.argmax(along)])
        rim, body = (ends[0], ends[1]) if abs(ends[0][0]) > abs(ends[1][0]) else (ends[1], ends[0])
        if abs(rim[0]) > 1.3 and abs(body[0]) < 0.6:
            sides[1 if rim[0] > 0 else -1].append((rim, body))
    return sides


def plaque_on_struts(bus_bucket):
    """Placa entre os dois suportes do lado +X, onde eles se aproximam a ~0,15 m (a largura da placa, 152 mm)."""
    pair = find_support_struts(bus_bucket)[1]
    if len(pair) != 2:
        return None, 0
    (rim_a, body_a), (rim_b, body_b) = pair
    apex = (rim_a + rim_b) / 2
    body_mid = (body_a + body_b) / 2
    t = 0.70  # fração do corpo até o aro, onde a separação entre os suportes é ~0,15 m
    center = body_mid + t * (apex - body_mid)
    along = apex - body_mid
    along /= np.linalg.norm(along)
    normal = np.cross(body_a - apex, body_b - apex)
    normal /= np.linalg.norm(normal)
    if normal[2] > 0:
        normal = -normal  # voltada para trás do refletor, para fora do corpo
    separation = float(np.linalg.norm(((1 - t) * body_a + t * rim_a) - ((1 - t) * body_b + t * rim_b)))
    return {"center": center.tolist(), "normal": normal.tolist(), "up": along.tolist(),
            "sizeMeters": [0.152, 0.229, 0.008], "strutSeparationMeters": round(separation, 3),
            "struts": [[rim_a.tolist(), body_a.tolist()], [rim_b.tolist(), body_b.tolist()]],
            "note": "O modelo NÃO traz a placa. Posição: entre os dois suportes da antena do lado +X, a 70% do caminho do corpo ao aro; "
                    "a posição real na sonda não foi verificada (conferir com foto oficial). 152 x 229 mm (P-S12) a conferir"}, 2


def rig_definition(buckets):
    """Pivôs, dobradiças e sockets em espaço Unity (metros): raiz Spin_Root no eixo de spin, z = 0 no vértice do refletor."""
    angle = np.radians(BOOM_ANGLE_DEGREES)
    bus, mag = np_pos(buckets["Bus"]), np_pos(buckets["MagnetometerBoom"])
    dish, feed = np_pos(buckets["Dish"]), np_pos(buckets["Feed"])
    boom_z = {name: float(np.median(np_pos(buckets[name])[:, 2])) for name in ("RtgBoomA", "RtgBoomB")}
    hinge = {
        "RtgBoomA": [BOOM_HINGE_T * np.cos(angle), BOOM_HINGE_T * np.sin(angle), boom_z["RtgBoomA"]],
        "RtgBoomB": [-BOOM_HINGE_T * np.cos(angle), BOOM_HINGE_T * np.sin(angle), boom_z["RtgBoomB"]],
        "MagnetometerBoom": [0.0, float(bus[:, 1].min()), float(np.median(mag[:, 2]))],
    }
    # Eixo de articulação (hipótese NÃO verificada em referência): perpendicular à haste, no plano XY.
    hinge_axis = {
        "RtgBoomA": [-float(np.sin(angle)), float(np.cos(angle)), 0.0],
        "RtgBoomB": [-float(np.sin(np.pi - angle)), float(np.cos(np.pi - angle)), 0.0],
        "MagnetometerBoom": [1.0, 0.0, 0.0],
    }
    sockets = {
        "Reflector": [0.0, 0.0, float(dish[:, 2].max()) * 0.5],
        "Feed": [float(feed[:, 0].mean()), float(feed[:, 1].mean()), float(feed[:, 2].max())],
        "EquipmentCompartment": [0.0, 0.0, float(bus[:, 2].min() + 0.3)],
    }
    for name in ("RtgBoomA", "RtgBoomB"):
        pos = np_pos(buckets[name])
        sockets[name] = pos[np.argmax(np.linalg.norm(pos[:, :2], axis=1))].tolist()  # ponta do gerador
    sockets["MagnetometerBoom"] = mag[np.argmin(mag[:, 1])].tolist()

    plaque, struts_found = plaque_on_struts(buckets["Bus"])
    if plaque is None:
        raise SystemExit("Não encontrei o par de suportes da antena no lado +X para ancorar a placa.")
    sockets["Plaque"] = plaque["center"]
    # PROVISÓRIO: nenhuma peça do modelo foi identificada como o fotopolarímetro; mesma posição do palco da PIO-003.
    sockets["Photopolarimeter"] = [0.67, -0.39, -0.30]
    return hinge, hinge_axis, sockets, plaque


def write_outputs(glb: Path, atlas: Atlas, buckets, stats, dropped):
    hinge, hinge_axis, sockets, plaque = rig_definition(buckets)
    geometry = {"units": "meters", "space": "Unity (mão esquerda, +Z = eixo da antena saindo do refletor, +Y = cima)", "parts": []}
    manifest_parts = {}
    for part in PARTS:
        bucket = buckets[part]
        pos = np_pos(bucket)
        pivot = np.array(hinge.get(part, [0.0, 0.0, 0.0]))
        geometry["parts"].append({
            "name": part,
            "pivot": pivot.tolist(),
            "positions": round_list((pos - pivot).ravel()),  # relativas ao pivô da peça
            "normals": round_list(bucket["nrm"], 4),
            "uv": round_list(bucket["uv"], 5),
            "triangles": bucket["tri"],
        })
        manifest_parts[part] = {"triangles": stats[part], "vertices": len(pos), "pivot": pivot.tolist(),
                                "boundsMin": np.round(pos.min(axis=0), 3).tolist(), "boundsMax": np.round(pos.max(axis=0), 3).tolist()}

    allpos = np.concatenate([np_pos(buckets[p]) for p in PARTS])
    dish = np_pos(buckets["Dish"])
    manifest = {
        "id": "PIO-002",
        "model": "Pioneer 10",
        "source": {
            "file": glb.name,
            "sha256": sha256(glb),
            "bytes": glb.stat().st_size,
            "generator": "Khronos glTF Blender I/O v4.2.57",
            "extensions": ["KHR_draco_mesh_compression", "EXT_texture_webp"],
            "reference": "P-S17 — NASA 3D Resources · Pioneer 10 (a página anuncia cerca de 1,95 MB)",
            "provenanceNote": "Arquivo fornecido pela equipe e conferido em 14/11/2026: tamanho (2.049.840 bytes) e SHA-1 de blob Git (62d2608617424e7b1d6d4d45bae13024cd9781c8) idênticos ao Pioneer 10.glb do espelho oficial github.com/nasa/NASA-3D-Resources (3D Models/Pioneer 10), pela API de conteúdo do GitHub (só metadados; nada baixado).",
        },
        "conversion": {
            "tool": "Tools/convert_pioneer10.py",
            "axisConversion": "glTF (mão direita) -> Unity (mão esquerda): Z invertido e ordem dos vértices invertida; centrado no eixo de spin; z = 0 no vértice do refletor",
            "atlas": {"file": "PioneerAtlas.png", "size": ATLAS_SIZE, "windows": [{"name": k, "rect": list(v)} for k, v in atlas.windows.items()]},
            "materialsBefore": 11,
            "materialsAfter": 1,
            "dropped": dropped,
        },
        "triangles": int(sum(stats.values())),
        "measurements": {
            "dishDiameterMetersX": round(float(dish[:, 0].max() - dish[:, 0].min()), 3),
            "dishExtentMetersY": round(float(dish[:, 1].max() - dish[:, 1].min()), 3),
            "dishDepthMeters": round(float(dish[:, 2].max() - dish[:, 2].min()), 3),
            "boundsMin": np.round(allpos.min(axis=0), 3).tolist(),
            "boundsMax": np.round(allpos.max(axis=0), 3).tolist(),
            "reference": "P-S05: refletor de 2,74 m",
        },
        "parts": [dict(name=k, **v) for k, v in manifest_parts.items()],
        "rig": {
            "root": "Spin_Root",
            "hinges": [{"name": k, "pivot": v, "axis": hinge_axis[k], "axisVerified": False} for k, v in hinge.items()],
            "sockets": [{"name": k, "position": v} for k, v in sockets.items()],
            "plaque": plaque,
            "unverified": ["eixo de articulação das hastes", "posição do Photopolarimeter"],
        },
    }
    data = ROOT / "Data" / "Assets"
    data.mkdir(parents=True, exist_ok=True)
    (data / "Pioneer10.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    (data / "Pioneer10.geometry.json").write_text(json.dumps(geometry, separators=(",", ":")) + "\n", encoding="utf-8")
    textures = ROOT / "Assets" / "_Game" / "Chapters" / "Pioneer10" / "Art" / "Probe" / "Textures"
    textures.mkdir(parents=True, exist_ok=True)
    atlas.image.save(textures / "PioneerAtlas.png", optimize=True)
    print("Gravados: Data/Assets/Pioneer10.json, Data/Assets/Pioneer10.geometry.json,", textures / "PioneerAtlas.png")
    return manifest


def plot(buckets, folder: Path):
    folder.mkdir(parents=True, exist_ok=True)
    colors = {"Bus": (230, 200, 90), "Dish": (140, 190, 230), "Feed": (240, 120, 120), "RtgBoomA": (130, 220, 140),
              "RtgBoomB": (200, 140, 230), "MagnetometerBoom": (240, 170, 80)}
    views = {"frente_XY": (0, 1, 2), "lado_ZY": (2, 1, 0), "topo_XZ": (0, 2, 1)}
    for name, (ax, ay, depth) in views.items():
        size = 900
        image = Image.new("RGB", (size, size), (20, 22, 28))
        draw = ImageDraw.Draw(image)
        allpos = np.concatenate([np_pos(b) for b in buckets.values() if b["pos"]])
        lo, hi = allpos[:, [ax, ay]].min(0), allpos[:, [ax, ay]].max(0)
        scale = (size - 60) / max(hi - lo)
        triangles = []
        for part, bucket in buckets.items():
            pos, tri = np_pos(bucket), np.asarray(bucket["tri"]).reshape(-1, 3)
            for a, b, c in tri:
                pts = [pos[a], pos[b], pos[c]]
                triangles.append((np.mean([p[depth] for p in pts]), part, pts))
        for _, part, pts in sorted(triangles, key=lambda t: t[0]):
            xy = [((p[ax] - lo[0]) * scale + 30, size - ((p[ay] - lo[1]) * scale + 30)) for p in pts]
            draw.polygon(xy, fill=colors[part], outline=(10, 10, 12))
        image.save(folder / f"{name}.png")
    print("Projeções em", folder)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--glb", required=True, help="Caminho do 'Pioneer 10.glb' original.")
    parser.add_argument("--plot", help="Pasta onde gravar projeções coloridas das peças (conferência visual).")
    parser.add_argument("--dry-run", action="store_true", help="Só relata, sem gravar dados.")
    args = parser.parse_args()

    glb = Path(args.glb)
    atlas, buckets, stats, dropped = convert(glb)
    print("Triângulos por peça:", stats, "| total", sum(stats.values()), "| descartados:", [d["node"] for d in dropped])
    for part in PARTS:
        pos = np_pos(buckets[part])
        print(f"  {part:18s} min {np.round(pos.min(0), 3)} max {np.round(pos.max(0), 3)}")
    if args.plot:
        plot(buckets, Path(args.plot))
    if not args.dry_run:
        manifest = write_outputs(glb, atlas, buckets, stats, dropped)
        print("Medidas:", manifest["measurements"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
