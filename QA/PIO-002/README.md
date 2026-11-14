# PIO-002 — evidência de validação

Executado em 14/11/2026 sobre `feature/PIO-002-modelo-sonda` já com `origin/develop` mesclada (f1c75fd, inclui MAR-011; único conflito: `ASSET_CREDITS.md`, resolvido mantendo as duas linhas), Unity **6000.6.7f1** / URP 17.6, Windows, em batchmode (Unity fechada).

| Verificação | Resultado |
| --- | --- |
| Suíte EditMode completa (`-runTests -testPlatform EditMode`) | **417/417 passaram**, 0 falhas, na base mesclada (`editmode.xml`; 369/369 antes de mesclar o develop) |
| Builder `pioneer10.probe` reexecutado sem mudanças | Arquivos versionados ficaram idênticos (`git status` limpo) |
| Captura `PioneerProbeCapture` | Executada; 9 quadros conferidos visualmente (03_tres_quartos). Os PNGs e `PROBE_STATS.md` regravados neste computador **não** foram commitados (ruído de GPU/locale); valem os da sessão anterior |
| Build WebGL só da cena `ProbePreview.unity` (`Tools/build_release.py --targets webgl`) | Sucesso na base mesclada: 17 arquivos, 14,25 MiB, shaders 1/1, hashes conferidos (14,23 MiB antes de mesclar) |

**Nota do build:** com cache incremental (`Library/PlayerDataCache`, `Library/BuildPlayerData`) o relatório do Unity não lista os assets e o `build_release.py` falha com `ShadersStripped` (0/1) sem que nada tenha sido removido. Apagar esses dois diretórios de `Library/` (regeneráveis) e refazer o build dá shaders 1/1.

**Não executado:** Play Mode, abertura da build no navegador (DXT1/WebGL não testado em navegador), profiler Web, FPS.
