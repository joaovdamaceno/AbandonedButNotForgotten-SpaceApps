#!/usr/bin/env python3
"""Música da Pioneer 10 · baixa as faixas de Scott Buckley (CC BY 4.0), confere o SHA-256, recorta o trecho usado, normaliza o
volume e codifica em Ogg Vorbis para Assets/_Game/Chapters/Pioneer10/Audio/Music/.

Os MP3 originais (320 kbps, 5 a 15 MB cada) NÃO ficam no repositório: este script os baixa de novo das páginas da fonte e confere o
hash registrado em Data/Assets/PioneerMusic.json. Precisa de ffmpeg com libvorbis (--ffmpeg CAMINHO ou a variável FFMPEG).

Uso:  python Tools/convert_pioneer10_music.py --ffmpeg <ffmpeg.exe>            (baixa, converte e grava o registro)
      python Tools/convert_pioneer10_music.py --check                          (confere os .ogg e o registro, sem ffmpeg)
"""
import argparse
import hashlib
import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "Assets" / "_Game" / "Chapters" / "Pioneer10" / "Audio" / "Music"
REGISTRY = ROOT / "Data" / "Assets" / "PioneerMusic.json"

LICENSE = "CC BY 4.0"
LICENSE_URL = "https://creativecommons.org/licenses/by/4.0/"
AUTHOR = "Scott Buckley"
VORBIS_QUALITY = 0.0       # libvorbis -q:a 0 (~64 kbps estéreo)
LOUDNESS_LUFS = -20.0      # alvo de loudnorm de uma passada; o nível relativo entre faixas é do volume do cue
FADE_IN = 1.0
FADE_OUT = 3.0
LOCAL_MUSIC = Path("C:/Users/Usuario/OneDrive/Documentos/Carlos/dev/AbandonedButNotForgotten/AbandonedButNotForgotten/Assets/_Game/Audio/ScottBuckley")

# id do cue, arquivo de saída, título, página da faixa, URL do MP3, início e duração do trecho (s), intenção no roteiro.
TRACKS = [
    dict(cue="MUS_PIO_LANCAMENTO", file="AKindOfHope_0-80.ogg", title="A Kind Of Hope", page="https://www.scottbuckley.com.au/library/a-kind-of-hope/",
         local="a-kind-of-hope.mp3", start=0.0, duration=80.0, volume=0.9,
         use="Cena 1: abertura tranquila e introdutória, começa quase em silêncio (substitui Chronicle, que a pessoa que testou achou fora de tom)"),
    dict(cue="MUS_PIO_CINTURAO", file="Descent_0-60.ogg", title="Descent", page="https://www.scottbuckley.com.au/library/descent/",
         url="http://www.scottbuckley.com.au/library/wp-content/uploads/2018/04/descent.mp3", start=0.0, duration=60.0, volume=0.85,
         use="Cena 2: o medo do cinturão de asteroides (escura, misteriosa), até o corte limpo para a calma"),
    dict(cue="MUS_PIO_MARAVILHA", file="TheDistantSun_0-120.ogg", title="The Distant Sun", page="https://www.scottbuckley.com.au/library/the-distant-sun/",
         url="https://www.scottbuckley.com.au/library/wp-content/uploads/2019/07/sb_monomyth_1_thedistantsun.mp3", start=0.0, duration=120.0, volume=0.9,
         use="Cenas 2 (revelação), 3 e 4: espanto calmo diante de Júpiter e a foto montada linha por linha (discreta, não disputa com as legendas)"),
    dict(cue="MUS_PIO_JUPITER", file="ThingsThatKeepUsHere_0-90.ogg", title="The Things That Keep Us Here", page="https://www.scottbuckley.com.au/library/the-things-that-keep-us-here/",
         local="the-things-that-keep-us-here.mp3", start=0.0, duration=90.0, volume=0.95,
         use="Cena 5: Ganimedes, Europa, o mergulho e o empurrão gravitacional (piano e cordas, sem cor regional; substitui Sanctum, que soou asiática no teste)"),
    dict(cue="MUS_PIO_ANOS", file="Extrapolation_0-110.ogg", title="Extrapolation", page="https://www.scottbuckley.com.au/library/extrapolation/",
         url="http://www.scottbuckley.com.au/library/wp-content/uploads/2014/07/sb_extrapolation.mp3", start=0.0, duration=110.0, volume=0.85,
         use="Cenas 6 e 7: a placa dourada e os anos (piano sombrio com um fio de esperança)"),
    dict(cue="MUS_PIO_RADIO", file="SignalToNoise_0-80.ogg", title="Signal to Noise", page="https://www.scottbuckley.com.au/library/signal-to-noise/",
         url="https://www.scottbuckley.com.au/library/wp-content/uploads/2020/04/sb_signaltonoise.mp3", start=0.0, duration=80.0, volume=0.85,
         use="Cena 8: a antena à noite e a escuta de um sinal fraco (misteriosa, melancólica e esperançosa)"),
    dict(cue="MUS_PIO_FINAL", file="Filaments_245-95.ogg", title="Filaments", page="https://www.scottbuckley.com.au/library/filaments/",
         url="https://www.scottbuckley.com.au/library/wp-content/uploads/2019/10/sb_filaments.mp3", start=245.0, duration=95.0, volume=0.95,
         use="Cena 9: rumo a Aldebaran (trecho do clímax emotivo ao fim da faixa, que a página descreve como final dramático por volta de 4:10)"),
]


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def credit(track):
    return f"'{track['title']}' by {AUTHOR} - released under CC-BY 4.0. www.scottbuckley.com.au"


def convert(ffmpeg):
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    registry = json.loads(REGISTRY.read_text(encoding="utf-8")) if REGISTRY.exists() else {"tracks": []}
    known = {t["cue"]: t for t in registry.get("tracks", [])}
    result = []
    with tempfile.TemporaryDirectory() as tmp:
        for t in TRACKS:
            if "local" in t:
                # Faixa já baixada da mesma página do autor em outro projeto da equipe (PIONEER_MUSIC_DIR ou LOCAL_MUSIC).
                src = Path(os.environ.get("PIONEER_MUSIC_DIR", LOCAL_MUSIC)) / t["local"]
            else:
                src = Path(tmp) / (t["cue"] + ".mp3")
                # O servidor da fonte recusa o urllib (406); o curl é aceito.
                subprocess.run(["curl", "-sSL", "--fail", "--max-time", "300", "-o", str(src), t["url"]], check=True)
            digest = sha256(src)
            if t["cue"] in known and known[t["cue"]].get("originalSha256") not in (None, digest) and known[t["cue"]].get("file") == t["file"]:
                raise SystemExit(f"{t['cue']}: o MP3 da fonte mudou (SHA-256 {digest}); reveja a faixa antes de converter")
            dst = OUT_DIR / t["file"]
            filters = f"loudnorm=I={LOUDNESS_LUFS}:TP=-2:LRA=11,afade=t=in:st=0:d={FADE_IN},afade=t=out:st={t['duration'] - FADE_OUT}:d={FADE_OUT}"
            subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-ss", str(t["start"]), "-t", str(t["duration"]), "-i", str(src),
                            "-af", filters, "-ac", "2", "-ar", "44100", "-c:a", "libvorbis", "-q:a", str(VORBIS_QUALITY), str(dst)], check=True)
            result.append(dict(t, originalSha256=digest, originalBytes=src.stat().st_size, oggSha256=sha256(dst), oggBytes=dst.stat().st_size,
                               license=LICENSE, licenseUrl=LICENSE_URL, author=AUTHOR, credit=credit(t),
                               loudnessTargetLufs=LOUDNESS_LUFS, vorbisQuality=VORBIS_QUALITY, fadeInSeconds=FADE_IN, fadeOutSeconds=FADE_OUT))
            print(f"{t['cue']}: {dst.name} {dst.stat().st_size / 1024:.0f} KB")
    REGISTRY.parent.mkdir(parents=True, exist_ok=True)
    REGISTRY.write_text(json.dumps({
        "schemaVersion": 1,
        "note": "Gerado por Tools/convert_pioneer10_music.py. Os MP3 originais não ficam no repositório; reexecutar o script baixa de novo e confere originalSha256.",
        "tracks": result}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    total = sum(r["oggBytes"] for r in result)
    print(f"total {total / 1024 / 1024:.2f} MB em {len(result)} arquivos")


def check():
    registry = json.loads(REGISTRY.read_text(encoding="utf-8"))
    errors = []
    for t in registry["tracks"]:
        path = OUT_DIR / t["file"]
        if not path.exists():
            errors.append(f"{t['cue']}: {t['file']} ausente")
        elif sha256(path) != t["oggSha256"]:
            errors.append(f"{t['cue']}: SHA-256 do .ogg diferente do registro")
    cues = {t["cue"] for t in registry["tracks"]}
    if cues != {t["cue"] for t in TRACKS}:
        errors.append("o registro e a lista do script divergem")
    for error in errors:
        print(error)
    if errors:
        return 1
    print(f"conforme: {len(registry['tracks'])} faixas, {sum(t['oggBytes'] for t in registry['tracks']) / 1024 / 1024:.2f} MB")
    return 0


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--ffmpeg", default=os.environ.get("FFMPEG", "ffmpeg"))
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    sys.exit(check() if args.check else convert(args.ffmpeg) or 0)
