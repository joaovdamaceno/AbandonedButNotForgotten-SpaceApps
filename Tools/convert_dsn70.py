#!/usr/bin/env python3
"""PIO-011 · Conversor determinístico do modelo NASA "Deep Space Network 70-meter" (GLB com Draco/WebP) para dados do projeto.

O modelo oficial traz a hierarquia mecânica da antena (`_root` > `azimuth` > `elevation` > prato, alimentador, quadripé,
treliças) e a base fixa. Este script decodifica o Draco, preserva a hierarquia (só translações), troca a convenção para a da
Unity (inverte Z e a ordem dos vértices), empacota as três texturas e as cores chapadas num único atlas de 1024² e grava:

  Data/Assets/Dsn70.json             manifesto: origem, SHA-256, medidas, hierarquia, estatísticas, janelas do atlas
  Data/Assets/Dsn70.geometry.json    geometria por nó (posições/normais/UV no espaço local do nó, em metros)
  Assets/_Game/Chapters/Pioneer10/Art/Dsn/Textures/DsnAtlas.png   atlas 1024² (cor base)

O GLB original NÃO é alterado nem entra em Assets/: fica em ArtSource/Pioneer10/PIO-011/original/ (LFS). Um passo de Editor
(`Editor/PioneerListeningBuilder.cs`) transforma estes dados em malhas, material e prefab.

Espaço de saída (Unity, 1 unidade = 1 m, escala real de 70 m): +Y = cima, o chão em y = 0; no modelo o prato está em
"zenite" (eixo do prato = +Y), com a articulação de elevação em (0, 33,9, 0) e o eixo da articulação ao longo de Z.

Uso (precisa de numpy, pillow e DracoPy; por exemplo, `uv run --with numpy --with pillow --with DracoPy python ...`):
    python Tools/convert_dsn70.py --glb "ArtSource/Pioneer10/PIO-011/original/Deep Space Network 70-meter.glb"
    python Tools/convert_dsn70.py --glb ... --dry-run
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from convert_pioneer10 import decode, image_bytes, load_glb, sha256  # noqa: E402  (decodificador Draco já validado na PIO-002)

ROOT = Path(__file__).resolve().parents[1]
ATLAS_SIZE = 1024
PAD = 2
ATLAS_PATH = ROOT / "Assets/_Game/Chapters/Pioneer10/Art/Dsn/Textures/DsnAtlas.png"
MANIFEST_PATH = ROOT / "Data/Assets/Dsn70.json"
GEOMETRY_PATH = ROOT / "Data/Assets/Dsn70.geometry.json"

# Janela do atlas por material do GLB: (x, y, w, h) em pixels, origem no canto superior esquerdo.
WINDOWS = {
    "tex_01": (0, 0, 512, 512),
    "tex_02": (512, 0, 512, 512),
    "tex_02b": (0, 512, 256, 256),
    "flat_gray": (256, 512, 256, 256),
    # Base cilíndrica: o UV original repete a textura 4 vezes ao redor (u de 0 a 4) e só usa a metade de cima (v de 0 a 0,5).
    # Um atlas não repete, então as 4 repetições ficam lado a lado numa janela própria.
    "base_tiles": (0, 768, 512, 64),
}
BASE_TILES = 4
BASE_V_USED = 0.5
FLAT_COLOR = tuple(int(round(255 * (0.8 ** (1 / 2.2)))) for _ in range(3))  # fator linear 0,8 -> sRGB
FLAT_MATERIALS = {"truss_box_AO", "truss_dish_AO"}  # cores chapadas: ignoram o UV original
SKIP_NODES = {"_root", "elevation"}  # caixas auxiliares sem material (12 triângulos cada): só valem como pivôs


def build_atlas(gltf, binary) -> Image.Image:
    atlas = Image.new("RGB", (ATLAS_SIZE, ATLAS_SIZE), (0, 0, 0))
    for index, name in (("0", "tex_01"), ("1", "tex_02"), ("2", "tex_02b")):
        material = next(m for m in gltf["materials"] if m["name"] == name)
        source = gltf["textures"][material["pbrMetallicRoughness"]["baseColorTexture"]["index"]]["source"]
        picture = Image.open(io.BytesIO(image_bytes(gltf, binary, source))).convert("RGB")
        x, y, w, h = WINDOWS[name]
        atlas.paste(picture.resize((w, h), Image.LANCZOS), (x, y))
    x, y, w, h = WINDOWS["flat_gray"]
    atlas.paste(Image.new("RGB", (w, h), FLAT_COLOR), (x, y))
    material = next(m for m in gltf["materials"] if m["name"] == "tex_01")
    source = gltf["textures"][material["pbrMetallicRoughness"]["baseColorTexture"]["index"]]["source"]
    full = Image.open(io.BytesIO(image_bytes(gltf, binary, source))).convert("RGB")
    x, y, w, h = WINDOWS["base_tiles"]
    tile = full.crop((0, 0, full.width, int(full.height * BASE_V_USED))).resize((w // BASE_TILES, h), Image.LANCZOS)
    for i in range(BASE_TILES):
        atlas.paste(tile, (x + i * (w // BASE_TILES), y))
    return atlas


def remap_uv(window: str, uv: np.ndarray) -> np.ndarray:
    """UV glTF (origem superior esquerda) -> UV Unity do atlas (origem inferior esquerda)."""
    x, y, w, h = WINDOWS[window]
    u = np.clip(uv[:, 0], 0, 1)
    v = np.clip(uv[:, 1], 0, 1)
    px = x + PAD + u * (w - 2 * PAD)
    py = y + PAD + v * (h - 2 * PAD)
    return np.stack([px / ATLAS_SIZE, 1.0 - py / ATLAS_SIZE], axis=1)


def remap_uv_tiled(uv: np.ndarray) -> np.ndarray:
    """UV da base (u de 0 a 4, v de 0 a 0,5) -> as quatro repetições lado a lado da janela `base_tiles`."""
    x, y, w, h = WINDOWS["base_tiles"]
    u = np.clip(uv[:, 0], 0, BASE_TILES) / BASE_TILES
    v = np.clip(uv[:, 1], 0, BASE_V_USED) / BASE_V_USED
    px = x + u * w
    py = y + 1 + v * (h - 2)
    return np.stack([px / ATLAS_SIZE, 1.0 - py / ATLAS_SIZE], axis=1)


def convert(glb_path: Path):
    gltf, binary = load_glb(glb_path)
    nodes = gltf["nodes"]
    parent = {}
    for index, node in enumerate(nodes):
        for child in node.get("children", []):
            parent[child] = index
    for node in nodes:
        if "rotation" in node or "matrix" in node or "scale" in node:
            raise SystemExit("O modelo tem rotação/escala por nó; o conversor só trata translação.")

    atlas = build_atlas(gltf, binary)
    out_nodes = []
    stats = {"triangles": 0, "vertices": 0, "uv_clamped_nodes": []}
    for index, node in enumerate(nodes):
        t = node.get("translation", [0, 0, 0])
        entry = {
            "name": node["name"],
            "parent": nodes[parent[index]]["name"] if index in parent else None,
            "translation": [round(float(t[0]), 5), round(float(t[1]), 5), round(-float(t[2]), 5)],
            "triangles": 0,
        }
        if node["name"] not in SKIP_NODES and "mesh" in node:
            pos, nrm, uvs, tri = [], [], [], []
            for primitive in gltf["meshes"][node["mesh"]]["primitives"]:
                material = gltf["materials"][primitive["material"]]["name"]
                d = decode(gltf, binary, primitive)
                points = np.asarray(d.points, dtype=np.float64)
                normals = np.asarray(d.normals, dtype=np.float64)
                faces = np.asarray(d.faces)
                uv = np.asarray(d.tex_coord, dtype=np.float64)
                if material in FLAT_MATERIALS:
                    window, atlas_uv = "flat_gray", remap_uv("flat_gray", np.full_like(uv, 0.5))
                else:
                    window = material
                    if uv[:, 0].max() > 1.5:
                        if node["name"] != "base" or uv[:, 1].max() > BASE_V_USED + 0.05:
                            raise SystemExit("UV repetido em um nó que não é a base: " + node["name"])
                        atlas_uv = remap_uv_tiled(uv)
                    else:
                        if uv.min() < -1e-4 or uv.max() > 1 + 1e-4:
                            stats["uv_clamped_nodes"].append(node["name"])
                        atlas_uv = remap_uv(window, uv)
                base = len(pos) // 3
                for p, n in zip(points, normals):
                    pos.extend([round(float(p[0]), 4), round(float(p[1]), 4), round(-float(p[2]), 4)])
                    n = n / max(np.linalg.norm(n), 1e-9)
                    nrm.extend([round(float(n[0]), 4), round(float(n[1]), 4), round(-float(n[2]), 4)])
                uvs.extend(round(float(c), 5) for c in atlas_uv.ravel())
                for a, b, c in faces:
                    # A inversão de Z espelha o modelo: inverter a ordem preserva a face visível.
                    tri.extend([base + int(a), base + int(c), base + int(b)])
            entry.update({"positions": pos, "normals": nrm, "uv": uvs, "indices": tri, "triangles": len(tri) // 3})
            stats["triangles"] += len(tri) // 3
            stats["vertices"] += len(pos) // 3
        out_nodes.append(entry)
    stats["uv_clamped_nodes"] = sorted(set(stats["uv_clamped_nodes"]))
    return atlas, out_nodes, stats


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--glb", required=True, type=Path)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    glb = args.glb.resolve()
    atlas, nodes, stats = convert(glb)
    manifest = {
        "id": "dsn70",
        "model": "Deep Space Network 70-meter.glb",
        "source": "https://science.nasa.gov/3d-resources/deep-space-network-70-meter",
        "credit": "NASA/Brian E. Kumanchik; NASA/Christian A. Lopez",
        "sha256": sha256(glb),
        "bytes": glb.stat().st_size,
        "conversion": "Tools/convert_dsn70.py",
        "axes": "glTF (mão direita, Y para cima) -> Unity (inverte Z); sem mudança de escala (metros)",
        "atlas": {"size": ATLAS_SIZE, "windows": WINDOWS, "flat_gray_srgb": FLAT_COLOR},
        "stats": stats,
        "nodes": [{k: v for k, v in n.items() if k in ("name", "parent", "translation", "triangles")} for n in nodes],
    }
    print(json.dumps({"triangles": stats["triangles"], "vertices": stats["vertices"], "uv_clamped_nodes": stats["uv_clamped_nodes"]}))
    if args.dry_run:
        return 0

    ATLAS_PATH.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(ATLAS_PATH, optimize=False)
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    GEOMETRY_PATH.write_text(json.dumps({"nodes": nodes}, separators=(",", ":")) + "\n", encoding="utf-8")
    print("gravado:", MANIFEST_PATH.relative_to(ROOT), GEOMETRY_PATH.relative_to(ROOT), ATLAS_PATH.relative_to(ROOT))
    return 0


if __name__ == "__main__":
    sys.exit(main())
