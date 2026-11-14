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

## Conformidade com o UI_SPEC e a arquitetura (PR de correção)

Base `develop` a partir do commit de PR #122 (última CORE integrada: CORE-006). Executado em batchmode, Unity 6000.6.7f1.

| Verificação | Resultado |
| --- | --- |
| Suíte EditMode completa | **494/494**, 0 falhas (`editmode.xml`); `PioneerImagingTests` passou a 20 testes (ValidateSetup, `ResponsiveLayoutClass`, estados do prompt) |
| Capturas | 25 PNGs em `Docs/Art/Pioneer10_Imaging/<estado>_<L>x<A>.png`: 11 estados em 1280×720; 7 também em 960×600 e 1024×768 (4:3, classe `sa-layout--narrow`). Fundo claro #C99A6E (pior caso) |
| Contraste medido no render (`contrast-audit.txt`) | 0 falhas. Pior texto: números das faixas 8,83:1 (mín. 4,5:1); pior não-texto: cursor × zona 4,07:1 (mín. 3:1) |
| Layout (`layout-audit.txt`) | 0 falhas: quadro dentro da tela, imagem, trilha, prompt e legenda dentro do quadro; exatamente uma classe `sa-hold--*` por captura |
| Builder reexecutado | Arquivos idênticos |
| Build WebGL (`Pioneer10.unity`, cache limpo) | Sucesso: 17 arquivos, 13,91 MiB, shaders 3/3, hashes conferidos |

A auditoria achou e corrigi: o cursor de fase (#C9C2B4) sobre a trilha translúcida dava 2,59:1 (abaixo de 3:1). A trilha agora usa `--sa-color-plate-strong` e o cursor `--sa-color-on-plate`. Cores translúcidas do cursor e da zona são compostas por cálculo, e o texto é medido nos pixels.

**Não executado:** Play Mode, navegador, teste humano de compreensão, leitura com 3D atrás do painel. O contraste foi medido no render offscreen do Editor, não no player WebGL.
