# PIO-002 — evidência de validação

Executado em 14/11/2026 sobre `feature/PIO-002-modelo-sonda` (base 7827d0c), Unity **6000.6.7f1** / URP 17.6, Windows, em batchmode (Unity fechada).

| Verificação | Resultado |
| --- | --- |
| Suíte EditMode completa (`-runTests -testPlatform EditMode`) | **369/369 passaram**, 0 falhas (`editmode.xml`) |
| Builder `pioneer10.probe` reexecutado sem mudanças | Arquivos versionados ficaram idênticos (`git status` limpo) |
| Captura `PioneerProbeCapture` | Executada; 9 quadros conferidos visualmente (03_tres_quartos). Os PNGs e `PROBE_STATS.md` regravados neste computador **não** foram commitados (ruído de GPU/locale); valem os da sessão anterior |
| Build WebGL só da cena `ProbePreview.unity` (`Tools/build_release.py --targets webgl`) | Sucesso: 17 arquivos, 14,23 MiB, shaders 1/1, hashes conferidos |

**Não executado:** Play Mode, abertura da build no navegador (DXT1/WebGL não testado em navegador), profiler Web, FPS.
