#!/usr/bin/env python3
"""CORE-006 · Build reproduzível do Abandoned, But Not Forgotten.

Cada alvo roda numa execução SEPARADA e SEQUENCIAL da Unity, aberta já com ``-buildTarget``: a troca de plataforma
termina (importação + recarga de domínio) antes de ``BuildPlayer``. Depois de cada build o relatório
``space-apps-build.json`` é lido e os SHA-256 de todos os arquivos são recalculados aqui, para provar que o relatório
descreve exatamente o que está no disco.

Exemplos:
    python Tools/build_release.py --targets windows,webgl
    python Tools/build_release.py --targets webgl --scenes Assets/_Game/Scenes/Boot.unity
    python Tools/build_release.py --preflight-only --targets webgl
    python Tools/build_release.py --rebuild-scenes --targets webgl
    python Tools/build_release.py --dry-run --targets webgl,windows

O Editor é procurado em $UNITY_EDITOR ou no caminho padrão do Unity Hub para a versão de ProjectSettings/ProjectVersion.txt.
Nenhum serviço externo é usado; a primeira importação de pacotes pode precisar de rede.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import platform
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPORT_NAME = "space-apps-build.json"
TARGETS = {
    "webgl": {"unity_target": "WebGL", "platform": "WebGL"},
    "windows": {"unity_target": "Win64", "platform": "Windows"},
}
BUILD_METHOD = "Abandoned.SpaceApps.EditorTools.SpaceAppsBuild.BuildFromCommandLine"
PREFLIGHT_METHOD = "Abandoned.SpaceApps.EditorTools.SpaceAppsBuild.PreflightFromCommandLine"
SCENES_METHOD = "Abandoned.SpaceApps.EditorTools.SpaceAppsSceneBuilder.BuildFromCommandLine"


def editor_version() -> str:
    for line in (ROOT / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8").splitlines():
        if line.startswith("m_EditorVersion:"):
            return line.split(":", 1)[1].strip()
    raise SystemExit("ProjectVersion.txt sem m_EditorVersion.")


def find_unity(explicit: str | None) -> Path:
    if explicit:
        return Path(explicit)
    if os.environ.get("UNITY_EDITOR"):
        return Path(os.environ["UNITY_EDITOR"])
    version = editor_version()
    system = platform.system()
    if system == "Windows":
        return Path(f"C:/Program Files/Unity/Hub/Editor/{version}/Editor/Unity.exe")
    if system == "Darwin":
        return Path(f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity")
    return Path.home() / f"Unity/Hub/Editor/{version}/Editor/Unity"


def git_commit() -> str:
    try:
        sha = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.strip()
        dirty = subprocess.run(["git", "status", "--porcelain"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.strip()
        return sha + ("-dirty" if dirty else "")
    except (OSError, subprocess.CalledProcessError):
        return "desconhecido"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def run_unity(unity: Path, log: Path, args: list[str], dry_run: bool) -> int:
    command = [str(unity), "-batchmode", "-quit", "-projectPath", str(ROOT), "-logFile", str(log), *args]
    print("  $ " + " ".join(f'"{c}"' if " " in c else c for c in command))
    if dry_run:
        return 0
    log.parent.mkdir(parents=True, exist_ok=True)
    return subprocess.run(command, check=False).returncode


def verify_report(output: Path) -> tuple[bool, dict | None, list[str]]:
    report_path = output / REPORT_NAME
    if not report_path.exists():
        return False, None, [f"relatório ausente: {report_path}"]

    report = json.loads(report_path.read_text(encoding="utf-8"))
    problems: list[str] = []
    for entry in report.get("files", []):
        file_path = output / entry["path"]
        if not file_path.exists():
            problems.append(f"arquivo listado não existe: {entry['path']}")
        elif file_path.stat().st_size != entry["bytes"] or sha256(file_path) != entry["sha256"]:
            problems.append(f"tamanho/hash divergente: {entry['path']}")
    if report.get("result") != "Succeeded":
        problems.append(f"resultado {report.get('result')}")
    problems += [f"shader removido: {s}" for s in report.get("shadersMissing", [])]
    return not problems, report, problems


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--targets", default="webgl", help="Lista em ordem, separada por vírgula: webgl, windows.")
    parser.add_argument("--output", default="Builds", help="Pasta base das builds (ignorada pelo Git).")
    parser.add_argument("--scenes", help="Cenas separadas por ';'. Padrão: cenas habilitadas nas Build Settings.")
    parser.add_argument("--unity", help="Caminho do executável da Unity.")
    parser.add_argument("--preflight-only", action="store_true", help="Só a pré-verificação, sem compilar o player.")
    parser.add_argument("--rebuild-scenes", action="store_true", help="Roda os builders de cena antes do build.")
    parser.add_argument("--dry-run", action="store_true", help="Mostra os comandos sem executar.")
    args = parser.parse_args()

    targets = [t.strip().lower() for t in args.targets.split(",") if t.strip()]
    unknown = [t for t in targets if t not in TARGETS]
    if unknown or not targets:
        parser.error(f"alvo(s) inválido(s): {unknown or args.targets}. Use: {', '.join(TARGETS)}")

    unity = find_unity(args.unity)
    if not args.dry_run and not unity.exists():
        print(f"Unity não encontrada em {unity}. Defina UNITY_EDITOR ou use --unity.", file=sys.stderr)
        return 2

    stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%d-%H%M%S")
    commit = git_commit()
    logs = ROOT / args.output / "logs" / stamp
    summary = {"commit": commit, "started": stamp, "editor": editor_version(), "builds": []}
    print(f"Unity {editor_version()} · commit {commit}")

    if args.rebuild_scenes:
        first = TARGETS[targets[0]]["unity_target"]
        print("Builders de cena:")
        code = run_unity(unity, logs / "scenes.log", ["-buildTarget", first, "-executeMethod", SCENES_METHOD], args.dry_run)
        if code != 0:
            print(f"  FALHOU (saída {code}); veja {logs / 'scenes.log'}", file=sys.stderr)
            return code

    exit_code = 0
    for name in targets:  # sequencial: nunca duas Unity no mesmo projeto
        target = TARGETS[name]
        output = ROOT / args.output / target["platform"] / stamp
        unity_args = ["-buildTarget", target["unity_target"], "-spaceAppsPlatform", target["platform"],
                      "-buildOutput", str(output), "-buildCommit", commit]
        if args.scenes:
            unity_args += ["-buildScenes", args.scenes]

        method = PREFLIGHT_METHOD if args.preflight_only else BUILD_METHOD
        print(f"{target['platform']}: {'pré-verificação' if args.preflight_only else 'build'}")
        code = run_unity(unity, logs / f"{name}.log", [*unity_args, "-executeMethod", method], args.dry_run)
        entry = {"target": target["platform"], "exitCode": code, "output": str(output), "log": str(logs / f"{name}.log")}

        if not args.preflight_only and not args.dry_run:
            ok, report, problems = verify_report(output)
            entry.update({"verified": ok, "problems": problems})
            if report:
                entry.update({"result": report.get("result"), "files": len(report.get("files", [])),
                              "totalBytes": report.get("totalBytes"), "renderPipeline": report.get("renderPipeline"),
                              "shaders": f"{len(report.get('shadersChecked', [])) - len(report.get('shadersMissing', []))}/{len(report.get('shadersChecked', []))}"})
            status = "OK" if ok and code == 0 else "FALHOU"
            print(f"  {status}: {entry.get('result')} · {entry.get('files', 0)} arquivo(s) · "
                  f"{(entry.get('totalBytes') or 0) / 1048576:.2f} MiB · shaders {entry.get('shaders', '?')} · hashes conferidos: {ok}")
            for problem in problems:
                print(f"    - {problem}")
            if not ok:
                exit_code = exit_code or 1
        elif code != 0:
            print(f"  FALHOU (saída {code}); veja {entry['log']}")

        if code != 0:
            exit_code = exit_code or code
        summary["builds"].append(entry)

    if not args.dry_run:
        summary_path = ROOT / args.output / f"release-summary-{stamp}.json"
        summary_path.parent.mkdir(parents=True, exist_ok=True)
        summary_path.write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"Resumo: {summary_path}")
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
