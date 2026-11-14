# PIO-006 — evidência de validação

Executado em 14/11/2026 sobre `feature/PIO-006-varredura-ipp` (base `develop` f4ebea0, com PIO-002 integrada), Unity **6000.6.7f1** / URP 17.6, Windows, batchmode (Unity fechada).

| Verificação | Resultado |
| --- | --- |
| Suíte EditMode completa | **469/469 passaram**, 0 falhas (`editmode.xml`); 16 testes são de `PioneerImagingTests` |
| Builder `pioneer10.stage.imaging` reexecutado sem mudanças | Arquivos versionados idênticos (`git status` sem diferenças novas) |
| Captura do painel (`PioneerImagingCapture`) | 9 quadros em `Docs/Art/Pioneer10_Imaging/`, conferidos visualmente (02, 06, 09). Textos aparecem como chaves (resolvedor é da UI-004) |
| Build WebGL só da cena `Pioneer10.unity` (`Tools/build_release.py --targets webgl`, cache de player limpo) | Sucesso: 17 arquivos, 13,91 MiB, shaders 3/3, hashes conferidos |

Cobertura dos testes: hold antecipado durante o aprendizado, soltar e retomar, passagem perdida sem apagar faixas, toque entre dois quadros, ajuda após 6/12 s (janela ampliada), 15/30/60 FPS com o mesmo resultado, 6/8 e 8/8 disparando uma vez, retomada idempotente (e sem efeito fora da interrupção), pausa congelando o cursor, marcos da sequência, execução completa sem vazar leases, pular e cancelar.

**Não executado:** Play Mode, build aberta no navegador, profiler Web, FPS real (a execução "a 15–30 FPS" é simulada com relógio ativo manual). Sem PIO-008 nada chama a retomada depois da faixa 6 no jogo integrado (os testes a chamam diretamente).
