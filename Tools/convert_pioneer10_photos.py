#!/usr/bin/env python3
"""PIO-007 · Versões Web das fotos históricas da Pioneer 10 e registro de procedência.

Os dois originais ficam em ArtSource/Pioneer10/PIO-007/original/ (LFS, fora de Assets/). Este script:
  1. confere o SHA-256 de cada original contra o valor registrado aqui;
  2. gera a versão Web em Assets/_Game/Chapters/Pioneer10/Art/Historical/ (JPEG, no máximo 1024 px de largura);
  3. grava Data/Assets/PioneerHistoricalImages.json com origem, data, crédito, hashes, medidas e transformações.

Regras (PIO-007): nenhum detalhe é criado. Só há recorte de margem/enquadramento e REDUÇÃO por reamostragem (Lanczos);
nunca ampliação, nitidez, ruído ou "restauração" por IA. A baixa resolução histórica de Ganimedes é parte do fato.

Uso:
    python Tools/convert_pioneer10_photos.py            # gera os arquivos e o JSON
    python Tools/convert_pioneer10_photos.py --check    # só confere hashes e medidas (saída 0 = conforme)
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ORIGINALS = ROOT / "ArtSource" / "Pioneer10" / "PIO-007" / "original"
WEB = ROOT / "Assets" / "_Game" / "Chapters" / "Pioneer10" / "Art" / "Historical"
RECORD = ROOT / "Data" / "Assets" / "PioneerHistoricalImages.json"
RETRIEVED = "2026-11-14"
MAX_WIDTH = 1024
JPEG_QUALITY = 88

IMAGES = [
    {
        "id": "PIO.Hist.Ganymede",
        "subject": "Ganimedes", "subjectEn": "Ganymede",
        "kind": "single-frame",
        "mission": "Pioneer 10", "year": 1973,
        "eventDate": "1973-12-03",
        "eventDateNote": "Data exibida na página (encontro mais próximo da Pioneer 10 com Júpiter). O horário exato de aquisição da imagem não é informado.",
        "creditLine": "NASA",
        "sourcePage": {"url": "https://science.nasa.gov/resource/pioneer-10-ganymede/", "title": "Pioneer 10 – Ganymede",
                       "pageLastUpdated": "2024-10-27", "downloadDate": "2023-09-07", "retrieved": RETRIEVED},
        "original": {
            "url": "https://assets.science.nasa.gov/content/dam/science/psd/solar/2023/09/p/i/o/Pioneer_10_Ganymede_2679_AC73-9279.jpg",
            "file": "Pioneer_10_Ganymede_2679_AC73-9279.jpg",
            "sha256": "ddee488aadd1007a1162f6d979971add063111260f696d7140eda9c38e1f53fe",
            "announcedSize": "JPEG (636.08 KB)",
        },
        "web": {"file": "Pioneer10_Ganymede_1973_web.jpg", "crop": [0, 475, 2048, 2523]},
        "transformations": [
            "Recorte quadrado de 2048 × 2048 px (x 0–2048, y 475–2523) em torno do disco, que descarta as margens cinza do scan (em cima e embaixo) e mais da metade do fundo escuro; nenhum pixel do disco foi cortado.",
            "Redução de 2048 para 1024 px por reamostragem Lanczos (fator 0,5), sem ampliação.",
            "Salvo como JPEG, qualidade 88, sem nitidez nem filtro.",
        ],
        "caveats": [
            "Imagem de baixíssima resolução e fora de foco: é o que a Pioneer 10 registrou. Não existe detalhe de superfície a recuperar; não aplicar ampliação nem \"melhoria\".",
            "O original é um scan de cópia fotográfica (retrato 2048 × 3072) com margens cinza; o disco ocupa menos de metade da largura.",
        ],
        "usedIn": ["PIO.Imaging"], "cueIds": ["PIO.04.08"], "factIds": ["F-GANYMEDE"],
    },
    {
        "id": "PIO.Hist.Jupiter",
        "subject": "Júpiter", "subjectEn": "Jupiter",
        "kind": "montage",
        "mission": "Pioneer 10", "year": 1973,
        "eventDate": "1973-12-04",
        "eventDateNote": "Data do texto da página (\"On Dec. 4, 1973 ... sent back images of Jupiter of ever-increasing size\"). A data de publicação da página (13/2/2018) NÃO é a data das imagens.",
        "creditLine": "NASA",
        "sourcePage": {"url": "https://science.nasa.gov/resource/pioneer-10-at-jupiter/", "title": "Pioneer 10 at Jupiter",
                       "pageLastUpdated": "2025-05-12", "downloadDate": "2025-05-12", "retrieved": RETRIEVED},
        "original": {
            "url": "https://assets.science.nasa.gov/content/dam/science/psd/solar/2023/09/6/668761main_ac73-9341.jpg",
            "file": "668761main_ac73-9341.jpg",
            "sha256": "a4d9e5c428849cd4cd956d048e9f25f11f488ec23f98bfb97bd2fb40f8829e0b",
            "announcedSize": "Original (tamanho não informado na página)",
        },
        "web": {"file": "Pioneer10_Jupiter_1973_web.jpg", "crop": None},
        "transformations": [
            "Sem recorte: o quadro inteiro da montagem foi mantido.",
            "Redução de 2181 × 1693 para 1024 × 795 px por reamostragem Lanczos, sem ampliação.",
            "Salvo como JPEG, qualidade 88, sem nitidez nem filtro.",
        ],
        "caveats": [
            "É uma MONTAGEM sequencial de imagens da aproximação (do ponto ao disco, e a crescente depois do ponto mais próximo), não uma foto única. As faixas diagonais são do processo de varredura por rotação.",
            "A página não informa o instante de cada quadro da montagem.",
            "A segunda ligação da página (668761main_ac73-9341-1.jpg) é byte a byte idêntica à primeira (mesmo SHA-256).",
        ],
        "usedIn": ["PIO.Imaging"], "cueIds": ["PIO.04.06"], "factIds": ["F-CHANNELS", "F-GANYMEDE"],
    },
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def derive(item: dict, write: bool) -> dict:
    original_path = ORIGINALS / item["original"]["file"]
    if not original_path.exists():
        raise SystemExit(f"Original ausente: {original_path} (git lfs pull).")
    actual = sha256(original_path)
    if actual != item["original"]["sha256"]:
        raise SystemExit(f"SHA-256 do original diverge ({item['id']}): {actual}")

    with Image.open(original_path) as source:
        source.load()
        original_size = source.size
        box = item["web"]["crop"]
        work = source.crop(tuple(box)) if box else source.copy()
        scale = min(1.0, MAX_WIDTH / work.width)  # só reduz: nunca amplia
        size = (round(work.width * scale), round(work.height * scale))
        if scale < 1.0:
            work = work.resize(size, Image.Resampling.LANCZOS)
        web_path = WEB / item["web"]["file"]
        if write:
            WEB.mkdir(parents=True, exist_ok=True)
            work.convert("RGB").save(web_path, "JPEG", quality=JPEG_QUALITY, optimize=True)

    record = {
        "id": item["id"], "subject": item["subject"], "subjectEn": item["subjectEn"], "kind": item["kind"],
        "mission": item["mission"], "year": item["year"], "eventDate": item["eventDate"], "eventDateNote": item["eventDateNote"],
        "creditLine": item["creditLine"],
        "labelTextKey": "PIO.UI.real",
        "sourcePage": item["sourcePage"],
        "original": {**item["original"], "bytes": original_path.stat().st_size, "width": original_size[0], "height": original_size[1],
                     "format": "JPEG", "path": f"ArtSource/Pioneer10/PIO-007/original/{item['original']['file']}"},
        "web": {"path": f"Assets/_Game/Chapters/Pioneer10/Art/Historical/{item['web']['file']}", "bytes": web_path.stat().st_size if web_path.exists() else 0,
                "sha256": sha256(web_path) if web_path.exists() else "", "width": size[0], "height": size[1],
                "format": "JPEG", "quality": JPEG_QUALITY, "crop": box, "scale": round(scale, 4), "upscaled": False},
        "transformations": item["transformations"], "caveats": item["caveats"],
        "usedIn": item["usedIn"], "cueIds": item["cueIds"], "factIds": item["factIds"],
        "review": {"R7": "pendente", "note": "Comparar imagem e fonte lado a lado (critério de aceite da PIO-007); datas e legendas conferidas apenas contra as páginas oficiais da NASA."},
    }
    return record


def build(write: bool) -> dict:
    return {
        "schemaVersion": 1,
        "chapterId": "pioneer10",
        "task": "PIO-007",
        "notes": [
            "Origem, crédito, data, hash e medidas de cada imagem histórica exibida no estado PIO.Imaging. Gerado por Tools/convert_pioneer10_photos.py.",
            "Texto exibido junto da imagem: PIO.UI.real (\"Imagem real · Pioneer 10 · 1973 · NASA\"; \"Actual image · Pioneer 10 · 1973 · NASA\").",
            "Direitos: a página de cada item informa apenas \"Credit: NASA\" e não traz licença própria; valem as diretrizes de imagens e mídia da NASA (P-S18): reconhecer a NASA como fonte, sem sugerir endosso, e sem usar insígnia ou logotipo.",
            "Os originais ficam em ArtSource/ (LFS), fora de Assets/ e do build. A Io perdida NÃO tem imagem aqui (PIO.04.07 só a menciona).",
        ],
        "rights": {"credit": "NASA", "policy": "https://www.nasa.gov/nasa-brand-center/images-and-media/", "licenseOnItemPage": None,
                   "endorsement": "Sem sugerir endosso; sem logotipo NASA na imagem."},
        "images": [derive(item, write) for item in IMAGES],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="Só confere os arquivos gravados contra o registro.")
    args = parser.parse_args()

    if args.check:
        problems = []
        recorded = json.loads(RECORD.read_text(encoding="utf-8"))
        for entry in recorded["images"]:
            original = ROOT / entry["original"]["path"]
            web = ROOT / entry["web"]["path"]
            if not original.exists() or sha256(original) != entry["original"]["sha256"]:
                problems.append(f"{entry['id']}: original ausente ou com hash diferente")
            if not web.exists():
                problems.append(f"{entry['id']}: versão Web ausente")
                continue
            if sha256(web) != entry["web"]["sha256"]:
                problems.append(f"{entry['id']}: SHA-256 da versão Web difere do registro")
            with Image.open(web) as image:
                if image.size != (entry["web"]["width"], entry["web"]["height"]) or image.width > MAX_WIDTH:
                    problems.append(f"{entry['id']}: dimensões {image.size} diferem do registro ou passam de {MAX_WIDTH} px")
        print("\n".join(problems) if problems else "Fotos históricas conformes.")
        return 1 if problems else 0

    record = build(write=True)
    RECORD.parent.mkdir(parents=True, exist_ok=True)
    RECORD.write_text(json.dumps(record, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    for entry in record["images"]:
        web, original = entry["web"], entry["original"]
        print(f"{entry['id']}: {original['width']}×{original['height']} ({original['bytes']} B) -> {web['width']}×{web['height']} ({web['bytes']} B)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
