#!/usr/bin/env python3
"""Efeitos sonoros da Pioneer 10 · sintetiza com o ffmpeg (ruído e senoides), sem nenhuma gravação ou amostra de terceiros.
Saída em Assets/_Game/Chapters/Pioneer10/Audio/Sfx/ (Ogg Vorbis). Obra original desta equipe: não exige atribuição.

Uso: python Tools/generate_pioneer10_sfx.py --ffmpeg <ffmpeg.exe>
"""
import argparse
import os
import subprocess
import sys
from pathlib import Path

OUT = Path(__file__).resolve().parents[1] / "Assets" / "_Game" / "Chapters" / "Pioneer10" / "Audio" / "Sfx"

# cue, arquivo, descrição, argumentos de entrada e filtro (lavfi).
SFX = [
    dict(cue="SFX_PIO_LAUNCH", file="Launch_Rocket.ogg", seconds=7.0,
         use="Ignição e subida do Atlas-Centaur: estrondo grave que cresce, rugido de banda larga e estalidos; fade de 0,3 s a 1,2 s, cauda de 2,5 s",
         graph=("anoisesrc=c=brown:a=1.0:d=7:r=44100[a];anoisesrc=c=white:a=1.0:d=7:r=44100[b];anoisesrc=c=pink:a=1.0:d=7:r=44100[c];"
                "[a]lowpass=f=260,volume='min(1,0.25+t*0.35)':eval=frame[low];"
                "[b]highpass=f=300,lowpass=f=3200,tremolo=f=38:d=0.55,volume='min(0.55,0.05+t*0.16)':eval=frame[roar];"
                "[c]lowpass=f=900,tremolo=f=7:d=0.4,volume=0.55[mid];"
                "[low][roar][mid]amix=inputs=3:normalize=0,afade=t=in:st=0:d=0.4,afade=t=out:st=4.5:d=2.5,loudnorm=I=-18:TP=-2:LRA=7")),
    dict(cue="SFX_PIO_POWERDOWN", file="Instrument_PowerDown.ogg", seconds=1.1,
         use="Equipamento desligado para poupar energia: varredura descendente curta com cauda e um clique grave",
         graph=("aevalsrc='0.45*sin(2*PI*(820*t-820*t*t/1.8))*exp(-2.6*t)+0.2*sin(2*PI*(205*t-205*t*t/1.8))*exp(-3.2*t)':d=1.1:s=44100[s];"
                "[s]lowpass=f=3500,afade=t=in:st=0:d=0.01,afade=t=out:st=0.7:d=0.4,loudnorm=I=-20:TP=-2:LRA=7")),
]


def main(ffmpeg):
    OUT.mkdir(parents=True, exist_ok=True)
    for sfx in SFX:
        dst = OUT / sfx["file"]
        subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-filter_complex", sfx["graph"], "-ac", "2", "-ar", "44100",
                        "-c:a", "libvorbis", "-q:a", "3", str(dst)], check=True)
        print(f"{sfx['cue']}: {dst.name} {dst.stat().st_size / 1024:.0f} KB")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--ffmpeg", default=os.environ.get("FFMPEG", "ffmpeg"))
    sys.exit(main(parser.parse_args().ffmpeg) or 0)
