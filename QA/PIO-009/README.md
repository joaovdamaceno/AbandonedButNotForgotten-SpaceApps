# PIO-009 — evidência de validação

Executado em 14/11/2026 sobre `feature/PIO-009-placa-cinco-simbolos` já com `origin/develop` mesclada (531caea, inclui PIO-008, PIO-010, PIO-013 e MAR-018), Unity **6000.6.7f1** / URP 17.6, Windows, batchmode (Unity fechada).

| Verificação | Resultado |
| --- | --- |
| Suíte EditMode completa (base mesclada) | **707/707**, 0 falhas (`editmode.xml`); `PioneerPlaqueTests` tem 19 testes |
| Builder `pioneer10.stage.plaque` reexecutado | Arquivos idênticos (scene, prefab, sequência, SVG e PNGs) |
| Importador de dados (PIO-013) reexecutado depois da mescla | `PioneerData.asset` idêntico ao mesclado: 108 textos (102 do develop + 6 desta tarefa) |
| Capturas (`PioneerPlaqueCapture`) | 18 PNGs em `Docs/Art/Pioneer10_Plaque/<estado>_<L>x<A>.png`: 8 estados em 1280×720; 5 também em 960×600 e 1024×768 (4:3, `sa-layout--narrow`). Fundo claro #C99A6E (pior caso). Textos aparecem como chaves (resolvedor é da UI-004) |
| Contraste medido no render (`contrast-audit.txt`) | 0 falhas. Pior texto: nome da peça 5,81:1 (mín. 4,5:1); pior não-texto: contorno × quadro 7,67:1 (mín. 3:1) |
| Layout (`layout-audit.txt`) | 0 falhas: palco dentro da tela; quadro, bandeja e prompt dentro do palco; exatamente uma classe `sa-hold--*` por captura |
| Build WebGL da cena `Pioneer10.unity` (cache limpo, base mesclada) | Sucesso: 17 arquivos, 14,67 MiB, shaders 3/3, hashes conferidos |

**Cobertura dos testes:** teclado sozinho (setas + E) até 5/5 com as cinco legendas por gatilho; cliques em qualquer ordem e clicar de novo para soltar; arrastar (função de snap: 95 px encaixa, 97 px volta, o mais próximo vence, contorno ocupado não recebe) e `DropPiece`; contorno errado devolve a peça com mensagem neutra, some sozinha e não remove acertos; contorno correto pulsa só após a seleção; ajuda de segurar E (2 s por peça; as cinco em ≤ 30 s ativos); toque × segurar × soltar cedo (pausado, sem perda); E mantido antes de abrir fica em rearme; pausa congela pulso, ajuda e mensagem; marco de 37,5 s espera 5/5 e a revelação dispara uma vez; reiniciar em 3/5 volta a 0 sem vazar leases nem eventos; movimento reduzido (pulso parado) e pulso normal ≤ 3 mudanças por segundo; pular conclui uma vez; SVG determinístico, sem bitmap nem texto embutido, peças nos contornos da tabela, PNGs transparentes com traço dourado; chaves PT/EN e gatilhos iguais aos dados do capítulo.

**Resolução de conflitos na mescla com o develop:** README do capítulo (mantidos os dois blocos: PIO-010 e PIO-009), `PioneerNarrationTests` (contagem de textos 102 + 6 = 108, confirmada pela suíte) e a cena `Pioneer10.unity` (usada a versão do develop e reexecutado o builder da placa, que reacrescenta só a sua raiz). `ASSET_CREDITS.md` e `REUSE.md` fundiram sem conflito.

**Não executado:** Play Mode e navegador; os eventos de ponteiro do painel (arrastar de verdade com o mouse) não foram simulados (a lógica e a função de snap sim); teste humano de compreensão; o contraste foi medido no render offscreen do Editor, não no player WebGL.
