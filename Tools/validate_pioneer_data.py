#!/usr/bin/env python3
"""PIO-001 · Valida os dados do capítulo Pioneer 10 (só stdlib).

Confere Data/Chapters/Pioneer10.json e Data/Localization/Pioneer10.{pt-BR,en}.json:
  - JSON válido, schemaVersion e IDs únicos;
  - nove estados com ordem explícita, soma nominal de 411 s e cadeia de tempos global sem buracos;
  - todo cue existe nas duas línguas; PT e EN têm as MESMAS chaves e os mesmos marcadores {count};
  - janelas de legenda dentro do estado, sem sobreposição (lacunas e velocidade de leitura são relatadas);
  - fatos com fontes existentes em 02_PIONEER_SOURCES.md, estados e divergências existentes, datas ISO válidas;
  - nenhuma afirmação que o registro de fontes manda evitar.

Saída 0 = conforme. Uso: python Tools/validate_pioneer_data.py [--quiet]
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CHAPTER = ROOT / "Data" / "Chapters" / "Pioneer10.json"
LOCALES = {"pt-BR": ROOT / "Data" / "Localization" / "Pioneer10.pt-BR.json", "en": ROOT / "Data" / "Localization" / "Pioneer10.en.json"}
SOURCES = ROOT / "Docs" / "Chapters" / "02_PIONEER_SOURCES.md"

EXPECTED_STATES = ["PIO.Launch", "PIO.Belt", "PIO.JupiterApproach", "PIO.Imaging", "PIO.Closest", "PIO.Plaque", "PIO.Outbound", "PIO.Listening", "PIO.Farewell"]
EXPECTED_NOMINAL = [40, 54, 27, 74, 40, 47, 39, 52, 38]
TOTAL_SECONDS = 411
MAX_CHARS_PER_SECOND = 22.0  # aviso (não erro): legenda de duas linhas, leitura revista na build real
ID_PATTERN = re.compile(r"^[A-Za-z][A-Za-z0-9._-]{0,63}$")
PLACEHOLDER = re.compile(r"\{[a-zA-Z]+\}")
FORBIDDEN = {  # termos que o registro de fontes manda evitar no texto de jogo, por idioma
    "pt-BR": ["baterias nucleares", "painéis solares", "heliopausa", "ouro maciço", "três semanas", "Game Over"],
    "en": ["nuclear batteries", "solar panels", "heliopause", "solid gold", "three weeks", "Game Over"],
}


def load(path: Path, errors: list[str]):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"{path.relative_to(ROOT)}: JSON inválido ({exc})")
        return None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--quiet", action="store_true", help="Só os erros.")
    args = parser.parse_args()

    errors: list[str] = []
    notes: list[str] = []
    chapter = load(CHAPTER, errors)
    locales = {code: load(path, errors) for code, path in LOCALES.items()}
    if errors or chapter is None or any(v is None for v in locales.values()):
        return report(errors, notes, args.quiet)

    # ---- Estados ----
    states = chapter["states"]
    ids = [s["id"] for s in states]
    if ids != EXPECTED_STATES:
        errors.append(f"estados fora da ordem ou incompletos: {ids}")
    if len(set(ids)) != len(ids):
        errors.append("IDs de estado duplicados")
    for i, state in enumerate(states):
        if state["order"] != i + 1:
            errors.append(f"{state['id']}: order {state['order']} deveria ser {i + 1}")
        if not ID_PATTERN.match(state["id"]):
            errors.append(f"{state['id']}: ID inválido")
    nominal = [s["nominalSeconds"] for s in states]
    if nominal != EXPECTED_NOMINAL:
        errors.append(f"durações nominais {nominal}, esperado {EXPECTED_NOMINAL}")
    if sum(nominal) != TOTAL_SECONDS or chapter["timeBudget"]["nominalSeconds"] != TOTAL_SECONDS:
        errors.append(f"soma nominal {sum(nominal)} s (esperado {TOTAL_SECONDS})")
    cursor = 0
    for state in states:
        if state["globalStartSeconds"] != cursor or state["globalEndSeconds"] != cursor + state["nominalSeconds"]:
            errors.append(f"{state['id']}: tempo global {state['globalStartSeconds']}–{state['globalEndSeconds']} não encadeia em {cursor}")
        cursor = state["globalEndSeconds"]
    stretch = sum(item["maxSeconds"] - item["nominalSeconds"] for item in chapter["timeBudget"]["readingStretch"])
    if TOTAL_SECONDS + stretch != chapter["timeBudget"]["maxWithStretchSeconds"]:
        errors.append(f"alongamento de leitura soma {TOTAL_SECONDS + stretch} s, declarado {chapter['timeBudget']['maxWithStretchSeconds']} s")
    notes.append(f"{len(states)} estados, {sum(nominal)} s nominais, até {TOTAL_SECONDS + stretch} s com o alongamento de leitura")

    # ---- Localização ----
    keys = {}
    for code, data in locales.items():
        entries = data["entries"]
        mapping = {}
        for entry in entries:
            key = entry["key"]
            if key in mapping:
                errors.append(f"[{code}] chave duplicada: {key}")
            mapping[key] = entry
            if not entry["text"].strip():
                errors.append(f"[{code}] texto vazio: {key}")
            if entry["status"] not in ("approved", "derived"):
                errors.append(f"[{code}] {key}: status inválido '{entry['status']}'")
            if entry["group"] not in ("narrative", "hud", "instrument", "card"):
                errors.append(f"[{code}] {key}: grupo inválido '{entry['group']}'")
            for term in FORBIDDEN[code]:
                if term.lower() in entry["text"].lower():
                    errors.append(f"[{code}] {key}: contém '{term}', que o registro de fontes manda evitar")
        keys[code] = mapping
    pt, en = keys["pt-BR"], keys["en"]
    for missing in sorted(set(pt) ^ set(en)):
        errors.append(f"chave só em um idioma: {missing}")
    for key in sorted(set(pt) & set(en)):
        if sorted(PLACEHOLDER.findall(pt[key]["text"])) != sorted(PLACEHOLDER.findall(en[key]["text"])):
            errors.append(f"{key}: marcadores diferentes entre PT e EN")
        if pt[key]["group"] != en[key]["group"]:
            errors.append(f"{key}: grupo diferente entre PT e EN")
    notes.append(f"{len(pt)} chaves por idioma ({sum(1 for e in pt.values() if e['status'] == 'derived')} PT e {sum(1 for e in en.values() if e['status'] == 'derived')} EN com status 'derived', a revisar pela R7)")

    # ---- Cues ----
    cue_ids = [c["id"] for c in chapter["cues"]]
    if len(set(cue_ids)) != len(cue_ids):
        errors.append("IDs de cue duplicados")
    by_state: dict[str, list[dict]] = {s: [] for s in ids}
    slow: list[str] = []
    for cue in chapter["cues"]:
        if cue["state"] not in by_state:
            errors.append(f"{cue['id']}: estado inexistente {cue['state']}")
            continue
        by_state[cue["state"]].append(cue)
        for code in keys:
            if cue["textKey"] not in keys[code]:
                errors.append(f"{cue['id']}: textKey {cue['textKey']} ausente em {code}")
        if cue["textKey"] in pt and not cue["textKey"].startswith("PIO.UI") and pt[cue["textKey"]]["group"] != "narrative":
            errors.append(f"{cue['id']}: textKey não é narrativa")
        timing = cue["timing"]
        if timing["kind"] == "window":
            if not timing["startSeconds"] < timing["endSeconds"]:
                errors.append(f"{cue['id']}: janela invertida")
            duration = timing["endSeconds"] - timing["startSeconds"]
            for code in keys:
                cps = len(keys[code][cue["textKey"]]["text"]) / duration
                if cps > MAX_CHARS_PER_SECOND:
                    slow.append(f"{cue['id']} [{code}] {cps:.1f} caracteres/s")
        elif timing["kind"] == "trigger":
            if not timing.get("triggerId") or timing.get("minimumReadSeconds", 0) <= 0:
                errors.append(f"{cue['id']}: gatilho sem triggerId ou sem tempo mínimo de leitura")
        else:
            errors.append(f"{cue['id']}: timing.kind inválido '{timing['kind']}'")

    gaps = []
    for state in states:
        windows = sorted((c["timing"]["startSeconds"], c["timing"]["endSeconds"], c["id"]) for c in by_state[state["id"]] if c["timing"]["kind"] == "window")
        if not by_state[state["id"]]:
            errors.append(f"{state['id']}: sem nenhuma legenda")
        previous_end = 0
        for start, end, cue_id in windows:
            if end > state["nominalSeconds"]:
                errors.append(f"{cue_id}: termina em {end} s, depois dos {state['nominalSeconds']} s do estado")
            if start < previous_end:
                errors.append(f"{cue_id}: sobrepõe a legenda anterior (começa em {start}, anterior termina em {previous_end})")
            if start > previous_end:
                gaps.append(f"{state['id']} {previous_end}–{start} s")
            previous_end = max(previous_end, end)
        if windows and previous_end < state["nominalSeconds"]:
            gaps.append(f"{state['id']} {previous_end}–{state['nominalSeconds']} s")
    notes.append(f"{len(cue_ids)} cues; lacunas intencionais sem legenda ({len(gaps)}): " + "; ".join(gaps))
    if slow:
        notes.append("AVISO leitura acima de %.0f caracteres/s (duas linhas; conferir na build real): %s" % (MAX_CHARS_PER_SECOND, "; ".join(slow)))

    # ---- Chaves de HUD/instrumento/cartão referenciadas ----
    referenced = set()
    for state in states:
        for key in ("hold",):
            if key in state and state[key].get("promptKey"):
                referenced.add(state[key]["promptKey"])
    for key in chapter["textGroups"]["instrument"]:
        referenced.add(key)
    for item in chapter["textGroups"]["relativeIndicators"]:
        referenced.add(item["key"])
    for key in sorted(referenced):
        for code in keys:
            if key not in keys[code]:
                errors.append(f"chave referenciada ausente em {code}: {key}")

    # ---- Fatos e fontes ----
    source_text = SOURCES.read_text(encoding="utf-8")
    known_sources = set(re.findall(r"P-S\d\d", source_text))
    conflicts = {c["id"] for c in chapter["sourceConflicts"]}
    fact_ids = [f["id"] for f in chapter["facts"]]
    if len(set(fact_ids)) != len(fact_ids):
        errors.append("IDs de fato duplicados")
    for fact in chapter["facts"]:
        if not fact["sources"]:
            errors.append(f"{fact['id']}: sem fonte")
        for source in fact["sources"]:
            if source not in known_sources:
                errors.append(f"{fact['id']}: fonte {source} não existe em 02_PIONEER_SOURCES.md")
        for state in fact.get("states", []):
            if state not in ids:
                errors.append(f"{fact['id']}: estado inexistente {state}")
        if fact.get("conflict") and fact["conflict"] not in conflicts:
            errors.append(f"{fact['id']}: divergência inexistente {fact['conflict']}")
        if "utc" in fact:
            try:
                dt.datetime.fromisoformat(fact["utc"].replace("Z", "+00:00")) if "T" in fact["utc"] else dt.date.fromisoformat(fact["utc"])
            except ValueError:
                errors.append(f"{fact['id']}: data UTC inválida '{fact['utc']}'")
    # Datas críticas (roteiro): lançamento 2/3 local = 3/3 UTC; últimos dados 27/4/2002; último sinal 23/1/2003; sem resposta 7/2/2003.
    facts = {f["id"]: f for f in chapter["facts"]}
    expected = {"F-LAUNCH": "1972-03-03T01:49Z", "F-CONTACT-2002-03": "2002-03-03", "F-CONTACT-2002-04": "2002-04-27", "F-CONTACT-2003-01": "2003-01-23", "F-CONTACT-2003-02": "2003-02-07"}
    for fact_id, utc in expected.items():
        if facts.get(fact_id, {}).get("utc") != utc:
            errors.append(f"{fact_id}: data UTC deveria ser {utc}")
    for fact_id in ("F-CONTACT-2002-03", "F-CONTACT-2002-04", "F-CONTACT-2003-01", "F-CONTACT-2003-02"):
        if facts.get(fact_id, {}).get("kind") != "receptionOnEarth":
            errors.append(f"{fact_id}: datas do rádio são de RECEPÇÃO na Terra (receptionOnEarth)")
    if facts.get("F-LAUNCH", {}).get("kind") != "operationOnProbe":
        errors.append("F-LAUNCH: o lançamento é operação (operationOnProbe), não recepção")
    notes.append(f"{len(fact_ids)} fatos mapeados para {len(known_sources)} fontes conhecidas; {len(conflicts)} divergências registradas")

    return report(errors, notes, args.quiet)


def report(errors: list[str], notes: list[str], quiet: bool) -> int:
    if not quiet:
        for note in notes:
            print("·", note)
    if errors:
        print(f"{len(errors)} erro(s):")
        for error in errors:
            print(" -", error)
        return 1
    print("Dados da Pioneer 10 conformes.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
