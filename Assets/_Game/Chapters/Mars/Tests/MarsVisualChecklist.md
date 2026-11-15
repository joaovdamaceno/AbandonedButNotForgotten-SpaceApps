# MAR-020: checklist visual de Marte (apoios, câmeras, RAT, Web)

Complementa `Assets/_Game/Chapters/Mars/Editor/MarsSupportAudit.cs`. A auditoria amostra pontos e mede; este checklist cobre o que só o olho decide: vão sob a rocha em ângulo baixo, mancha branca, ciência legível, clipping fino e o que muda no navegador. Responsável: R4. Revisão: outra função, com as capturas na PR.

Regra da task: ajuste só conta se sobreviver ao rebuild. Arrastar objeto no Editor sem salvar o builder não vale (item 4 abaixo).

## 0. Antes de começar

- [ ] Rebuild limpo dos builders que o capítulo usa: `MAR-012 Reconstruir Eagle`, `MAR-014 Reconstruir pouso split`, `MAR-015 Reconstruir campo do escudo`, `MAR-016 Reconstruir Victoria e o vale`.
- [ ] Menu `Abandoned Space Apps > Marte > MAR-020 Auditar apoios e câmeras` (ou `MarsSupportAudit.RunFromCommandLine` em batchmode). Relatório em `QA/MAR-020/support-audit.txt`.
- [ ] O relatório termina com `0 falha(s)`. Cada `[FALHA]` vira correção no builder, rebuild e nova rodada; cada `[AVISO]` é lido e justificado na PR.
- [ ] Anexar o relatório à PR junto das capturas.

## 1. Apoios e vãos (critério: sem vão nos pontos de apoio definidos)

Pontos de apoio definidos: os `Supports` de `HeatShield_Main` (aro e pontas do corte) e `HeatShield_Flank`, o meteorito, as rochas de `Scatter`, os afloramentos `Blueberries_Ledge`, `McKittrick_Ledge` e `Spur`, e as rochas de `Rocks` em Eagle.

Capturas de ângulo baixo (câmera a cerca de 0,2 m do chão, olhando rente à areia), uma por item:

| Item | Ângulos | O que procurar | OK | Captura |
| --- | --- | --- | --- | --- |
| `HeatShield_Main` | frente, lado, ponta do corte | luz passando entre aro e areia; aro pairando | [ ] | |
| `HeatShield_Flank` | frente, lado | borda flutuando ou engolida pela areia | [ ] | |
| Meteorito | frente, topo | sombra descolada do chão | [ ] | |
| `Blueberries_Ledge` | frente (rente ao solo), 45° | fresta sob a saia da rocha; céu visto através | [ ] | |
| `McKittrick_Ledge` | estação da RAT, 45° | idem; disco de trabalho coberto pela rocha | [ ] | |
| `Spur` | 0°, 90°, 180°, 270° | fresta a qualquer altura do corpo | [ ] | |
| Rochas de `Rocks` perto da rota | 3 rochas escolhidas ao acaso | base acima do chão | [ ] | |

Centro e borda: em cada afloramento e no escudo, uma captura mirando o centro da peça e outra a borda, depois que o terreno está pronto (passo 1 da task).

## 2. Câmeras de pouso acima do chão (critério: cada corte)

Auditoria: M04, M05 e M06, nos palcos Opportunity e Spirit, a cada 0,02 s contra a função de altura e a cada 0,1 s contra o colisor `Ground` de cada palco.

- [ ] Relatório sem `[FALHA]` nas linhas `M04/`, `M05/`, `M06/`.
- [ ] No Editor, parar no quadro de cada corte (início e fim de cada plano) nos dois palcos: o chão aparece abaixo da lente, nunca em volta dela.
- [ ] Ver o pouso inteiro lado a lado (tela dividida) sem ver o interior do terreno nos quiques e na parada.
- [ ] Pouso da Spirit na cena antiga `Opportunity` (`SplitLanding`): o módulo pousa em areia, sem atravessar pedra (PR #210). Conferir uma vez de novo depois do rebuild.

## 3. RAT recua sem clipping (critério)

Auditoria: com a cavidade na profundidade final, a ferramenta é posta em 26 pontos do recuo (0 a 0,25 m) e nenhum vértice passa abaixo do piso nem a altura diminui.

- [ ] Relatório sem `[FALHA]` nas linhas `RAT:`.
- [ ] No jogo, M12 até M13: o recuo termina sem a cabeça mergulhar na rocha nem tocar o rover.
- [ ] Captura de ângulo baixo no último quadro do esmerilhamento e no primeiro do recuo.
- [ ] Captura do recuo completo (3 quadros: contato, 2 cm acima, recolhida).
- [ ] Marca do esmerilhamento ainda aparece como disco claro, sem mancha branca estourada.

## 4. Rastro e persistência

- [ ] Rastro do rover (cena antiga) posiciona nos colisores exclusivos do terreno e limpa no teleporte. A auditoria confere o código; confirmar no jogo: dirigir, trocar de estação (teleporte) e ver que não fica linha ligando os dois pontos.
- [ ] O rover novo de Marte (`MarsRoverController`) não tem `TrailRenderer`. Se a edição final exigir rastro, vira outra tarefa.
- [ ] Toda correção feita nesta tarefa está no builder ou prefab e no commit. Prova: reconstruir o builder do item corrigido e repetir o relatório e a captura.

## 5. Web (shaders, normal RGB, sombras, névoa, material do rover)

Roteiro de `Docs/04_WEB_AND_QA.md`: trocar a plataforma ativa do Editor para WebGL, esperar importação e compilação, só então gerar a build. Sucesso de compilação não prova imagem.

- [ ] Plataforma ativa do Editor = WebGL antes de rodar a auditoria (senão ela avisa).
- [ ] Relatório: materiais de rover, Eagle e campo do escudo com shader válido e do URP.
- [ ] Normal RGB: o aviso de normal lida como cor (se houver) está resolvido ou justificado olhando o navegador. Não aplicar importador global sem ver o resultado.
- [ ] No navegador (Chrome ou Edge): chão opaco, rodas, solo e poeira com cor correta; sem rover cinza chapado nem preto.
- [ ] Sombras: uma luz principal com sombra de 1024 px e duas cascatas; a sombra do rover e dos afloramentos aparece.
- [ ] Névoa alcança o rover e o terreno na tempestade e no vale.
- [ ] Material do rover na Web igual ao do Editor (metal e roughness), sem brilho estourado.

## 6. Perfis leve e equilibrado

Perfis auditados: leve = `Assets/_Game/Settings/Web/WebLight.asset`; equilibrado = `Assets/_Game/Settings/URP/SpaceApps_URP.asset`.

Para cada perfil, nos mesmos 5 pontos: Eagle (estação da McKittrick), RAT em contato, campo do escudo, pouso em tela dividida e tempestade:

| Ponto | Leve | Equilibrado | Mancha branca? | Ciência legível? |
| --- | --- | --- | --- | --- |
| Eagle / McKittrick | [ ] | [ ] | | |
| RAT em contato | [ ] | [ ] | | |
| Escudo e meteorito | [ ] | [ ] | | |
| Pouso em tela dividida | [ ] | [ ] | | |
| Tempestade (poeira densa) | [ ] | [ ] | | |

Critério: nenhuma área do chão, do céu ou do rover estourada em branco, e todo rótulo e instrumento de ciência legível. O relatório lista Bloom, Tonemapping e DepthOfField de cada perfil: valores altos de Bloom com HDR são a causa mais provável de mancha branca.

## 7. Registro das evidências

- Relatório: `QA/MAR-020/support-audit.txt` (rodar e anexar).
- Capturas de ângulo baixo: pasta `QA/MAR-020/` (nome `item-angulo.png`).
- Testes automatizados que a PR precisa manter verdes: `MarsEagleTests`, `MarsShieldFieldTests`, `MarsLandingTests`, `MarsRatTests`, `MarsRoverTests`, `MarsLateMarsTests`.
- Na PR: comportamento, arquivos, testes realmente executados e limitações. A task só conta como concluída para as dependências depois da integração.
