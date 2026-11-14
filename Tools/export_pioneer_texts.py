#!/usr/bin/env python3
"""UI-004 · Exporta os textos PT/EN da Pioneer 10 (Data/Localization, formato por idioma da PIO-001) para a tabela
compartilhada {"texts":[{key,group,pt,en}]} que o LocalizationService lê (Assets/_Game/Shared/Localization/Pioneer10Texts.json).

A fonte da verdade continua sendo Data/Localization/Pioneer10.*.json. Reexecutar não muda nada se nada mudou.
Uso: python Tools/export_pioneer_texts.py          (grava)
     python Tools/export_pioneer_texts.py --check  (falha se o arquivo exportado estiver desatualizado)
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PT = ROOT / "Data" / "Localization" / "Pioneer10.pt-BR.json"
EN = ROOT / "Data" / "Localization" / "Pioneer10.en.json"
OUT = ROOT / "Assets" / "_Game" / "Shared" / "Localization" / "Pioneer10Texts.json"


def build():
    pt = {e["key"]: e for e in json.loads(PT.read_text(encoding="utf-8"))["entries"]}
    en = {e["key"]: e for e in json.loads(EN.read_text(encoding="utf-8"))["entries"]}
    if list(pt) != list(en):
        raise SystemExit("as chaves PT e EN da Pioneer divergem (mesma ordem e mesmo conjunto são obrigatórios)")
    texts = []
    for key, entry in pt.items():
        if en[key]["group"] != entry["group"]:
            raise SystemExit(f"{key}: grupo diferente entre PT e EN")
        texts.append({"key": key, "group": entry["group"], "pt": entry["text"], "en": en[key]["text"]})
    return {
        "schemaVersion": 1,
        "chapterId": "pioneer10",
        "source": "Exportado de Data/Localization/Pioneer10.pt-BR.json e Pioneer10.en.json (PIO-001) por Tools/export_pioneer_texts.py. Não editar à mão.",
        "notes": ["Mesmo formato das tabelas de Marte e do HUD; 'group' (narrative, hud, instrument, card) define a hierarquia: cartões de data usam o carimbo."],
        "texts": texts,
    }


def main():
    text = json.dumps(build(), ensure_ascii=False, indent=2) + "\n"
    if "--check" in sys.argv:
        current = OUT.read_text(encoding="utf-8") if OUT.exists() else ""
        if current.replace("\r\n", "\n") != text:
            print("Pioneer10Texts.json desatualizado: rode Tools/export_pioneer_texts.py")
            return 1
        print(f"Pioneer10Texts.json conforme ({len(json.loads(text)['texts'])} chaves)")
        return 0
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"{len(json.loads(text)['texts'])} chaves exportadas para {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
