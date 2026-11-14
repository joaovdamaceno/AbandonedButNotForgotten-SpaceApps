# Capítulo II — Pioneer 10 — R5

Scripts, dados, prefabs, arte e cena do capítulo. Roteiro: `Docs/Chapters/02_PIONEER_10.md`. Fontes: `Docs/Chapters/02_PIONEER_SOURCES.md`.
Código de jogo em `Runtime/` (caminho usado por todas as tarefas PIO), ferramentas de Editor em `Editor/` (assembly `Abandoned.SpaceApps.Editor`), testes em `Tests/` (assembly `Abandoned.SpaceApps.Core.Tests`). A pasta vazia `Scripts/` continua porque o validador da CORE-001 e a arquitetura a listam; unificar com `Runtime/` é decisão de R1.

## Palco espacial (PIO-003)

| Arquivo | Papel |
| --- | --- |
| `Scenes/Pioneer10.unity` | Cena única do capítulo, com a raiz `[R5] Pioneer Stage` (instância de `SpaceStage`). Abre isolada já enquadrada em `PIO.Launch` |
| `Art/SpaceStage.prefab` | Câmera única (`CameraDirector`, sem pós-processamento nem sombras), key + fill sem sombra, céu, três regiões, sonda, Júpiter, marcador de Aldebaran e anchors |
| `Runtime/PioneerPresentation.cs` | Compõe o palco por anchor: região ativa, grupo de luz, Júpiter/marcador, alinhamento da rotação; rotação visual pelo tempo ativo |
| `Editor/PioneerStageBuilder.cs` | Builder CORE-006 (`pioneer10.stage`): gera prefab, cena, malhas, materiais e texturas de forma determinística |
| `Editor/PioneerStageChecks.cs` | Câmera fora do volume varrido pela sonda girando, fora de Júpiter, acima do chão; coordenadas ≤ 1 km; planos consecutivos distintos |
| `Editor/PioneerStageCapture.cs` | Prévia: PNG por anchor, folha de contato e `STAGE_STATS.md` em `Docs/Art/Pioneer10_Stage/` |

**Reconstruir:** Abandoned Space Apps → CORE-006 → Reconstruir todas as cenas, ou `-executeMethod Abandoned.SpaceApps.EditorTools.SpaceAppsSceneBuilder.BuildFromCommandLine -builders pioneer10.stage`. Sem mudanças no código, os arquivos ficam idênticos (teste `RebuildingTheStageLeavesEveryFileIdentical`). Não edite o prefab à mão: altere o builder.

**Prévia:** selecione `SpaceStage` na cena; o Inspector lista um botão por anchor (● = anchor de estado) e “Verificar palco”. A prévia muda regiões/luzes na cena aberta; reconstruir restaura. Capturas: Abandoned Space Apps → PIO-003 → Capturar anchors.

### Escala e coordenadas

Distâncias astronômicas **não** viram coordenadas da Unity. Júpiter é uma esfera de composição (raio 80 m a ~340 m), o céu e o marcador de Aldebaran acompanham a câmera (“no infinito”), e órbitas/DSN ficam em diagramas 2D ou silhuetas (PIO-010/PIO-011). Nenhum anchor passa de ~540 m da origem.

### Anchors (rigs de câmera)

Cada anchor é registrado como rig no `CameraDirector` com o próprio ID. Um `ShotDefinition` com `rigId = "PIO.Imaging"` e keyframes em zero é exatamente a pose do anchor (`PioneerPresentation.CreateHoldShot`); dollies curtos partem dele. Alvos de enquadramento: `PIO.Probe`, `PIO.Jupiter`, `PIO.Aldebaran` e `PIO.Probe.<socket>`.

| Anchor | Estado | Função |
| --- | --- | --- |
| `PIO.Launch.Reveal` | Launch | PIO01.A · contorno do refletor em contraluz fria |
| `PIO.Launch.Pad` | Launch | PIO01.B · Atlas-Centaur à noite (silhueta da PIO-004 no `LaunchSite/AtlasSocket`) |
| `PIO.Launch.Ellipse` | Launch | PIO01.C · gráfico Terra–Lua (PIO-004 no `DeepSpace/LaunchDiagramSocket`) |
| `PIO.Launch` ● | Launch | PIO01.D · sonda livre, antena e RTGs (primeiro hold) |
| `PIO.Belt` ● | Belt | Plano longo quase vazio: sonda pequena no cinturão real |
| `PIO.Belt.Sensor` | Belt | Registro de poeira: dorso do refletor à esquerda, instrumento de UI à direita (PIO-005) |
| `PIO.JupiterApproach` ● | JupiterApproach | PIO03.B · Júpiter em ~2/3 do quadro, sonda no terço inferior |
| `PIO.JupiterApproach.Instrument` | JupiterApproach | PIO03.C · close do fotopolarímetro voltado a Júpiter |
| `PIO.Imaging` ● | Imaging | Júpiter e sonda à esquerda; **quadro de UI reservado à direita** (`TryGetUiFrame`) |
| `PIO.Plaque` ● | Plaque | Placa nos suportes da antena; rotação visual suspensa só neste close |
| `PIO.Outbound` ● | Outbound | Sonda se afasta com a antena voltada à Terra; o diagrama 2D (PIO-010) assume |
| `PIO.Listening` ● | Listening | Antena terrestre à noite (silhueta da PIO-011 no `GroundStation/DishSocket`); painel de onda embaixo à direita |
| `PIO.Farewell` ● | Farewell | Sonda no campo de estrelas, direção geral de Aldebaran (o marcador não ilumina a nave) |

### Contrato com o modelo definitivo (PIO-002 → PIO-014)

A sonda é `Regions/DeepSpace/Probe/ProbeSpin`: eixo Z local = eixo da antena = eixo de rotação (P-S05). O modelo NASA entra como filho de `ProbeSpin` no lugar de `ProvisionalSilhouette`, com o refletor em escala real (2,74 m). Os **sockets** em `ProbeSpin/Sockets` (`Reflector`, `Feed`, `EquipmentCompartment`, `RtgBoomA`, `RtgBoomB`, `MagnetometerBoom`, `Plaque`, `Photopolarimeter`) são o ponto de troca: posicione-os sobre as peças do modelo e as atividades continuam funcionando. A silhueta atual é **provisória** (G-PIO-03 em `ASSET_CREDITS.md`).

### Perfil leve

Uma câmera, sem pós-processamento (nenhum SSAO, DOF ou motion blur), sem MSAA/HDR; duas luzes direcionais sem sombra; materiais URP Lit/Unlit sem reflexo de ambiente; ambiente plano escuro e sem skybox. Medições estimadas da prévia em `Docs/Art/Pioneer10_Stage/STAGE_STATS.md` (não substituem o profiler da build Web).

## Lançamento (PIO-004)

| Arquivo | Papel |
| --- | --- |
| `Sequences/PIO_Launch.asset` | 40 s num único relógio: PIO01.A refletor (0–7), B Atlas noturno (7–17, ignição 9 s, subida a partir de 11,5 s), C gráfico Terra–Lua (17–26), D sonda livre e RTGs (26–40, hold em 32,5 s). Legendas PIO.01.01–05 nas janelas do roteiro |
| `Art/LaunchSilhouette.prefab` | Atlas-Centaur em silhueta (provisório, ~38 m), plataforma em quatro peças, chama em quads aditivos, luz da chama sem sombra, fumaça ≤ 80 partículas |
| `Runtime/PioneerLaunchActivity.cs` | Conduz o estado: pendura silhueta/gráfico/brilhos nos sockets do palco, abre o hold de 3 s do RTG, segura a sequência até concluir, limpa tudo ao sair e emite `Completed` uma vez |
| `Editor/PioneerLaunchBuilder.cs` | Builder CORE-006 `pioneer10.stage.launch` (raiz `[R5] Pioneer Launch`, roda depois de `pioneer10.stage`) |
| `Editor/PioneerLaunchCapture.cs` | Prévia por instantes da sequência em `Docs/Art/Pioneer10_Launch/` (menu PIO-004 → Capturar lançamento) |

**Uso pelo controlador (PIO-014):** `activity.Initialize(sequenceDirector, interactionService, inputRouter, sessionClock)`, `Completed += () => chapterDirector.CompleteStep("PIO.Launch")`, `Begin(skippable)`; `Cancel()` na mudança de estado pelo shell. O palco segue a sequência sozinho (`PioneerPresentation.BindSequenceDirector`): cada plano compõe a região e a luz do anchor do seu rig.

**Hold do RTG:** o cartão abre o contexto `Panel` com o candidato `PIO.Launch.Rtg` (texto `PIO.UI.rtg`). O E mantido desde antes do cartão é ignorado até ser solto (InputRouter); soltar pausa sem perder o progresso; a pausa do shell congela tudo. Retorno visual: calor nos RTGs (laranja) → pulsos pela haste → eletricidade no compartimento (ciano). Os cartões de título, data (“2 MAR 1972 · FLÓRIDA / 3 MAR · UTC”) e linhas de antena/RTG são eventos da sequência (`PIO.Launch.Title`, `.DateCard`, `.Callouts`) para a camada de UI; os textos vêm da PIO-001/UI-004.

**Sem tremor:** não há vibração de câmera em nenhum modo (a vibração do roteiro é opcional); o movimento é dolly curto e o acompanhamento do foguete.

## Cinturão (PIO-005)

| Arquivo | Papel |
| --- | --- |
| `Sequences/PIO_Belt.asset` | 58 s: mapa 0–12, fantasia 12–27 (15 s), espaço real 27–40, registros 40–58. A trava `PIO.Belt.Gate` segura PIO.02.06 até os 5 registros |
| `Runtime/BeltInvestigation.cs` | Lógica do estado (sem UI): fantasia sem dano, 5 registros por E mantido (2 s cada), clique, ajuda aos 6/12 s, saída e conclusão únicas |
| `Runtime/BeltPanelView.cs` + `UI/BeltPanel.uxml/.uss/.prefab` | Painel UI Toolkit; só desenha um `BeltViewState` e devolve cliques. Textos por chave `PIO.UI.*` |
| `Editor/PioneerBeltBuilder.cs` | Builder CORE-006 `pioneer10.stage.belt` (raiz `[R5] Pioneer Belt`, depois de `pioneer10.stage`) |
| `Editor/PioneerBeltCapture.cs` | Prévia do painel em `Docs/Art/Pioneer10_Belt/` (menu PIO-005). **Precisa da janela da Unity em primeiro plano**; sem foco o painel sai preto |

**Uso pelo controlador (PIO-014):** `belt.Initialize(sequenceDirector, interactionService, inputRouter, sessionClock, resolverDeTexto)`, `Completed += () => chapterDirector.CompleteStep("PIO.Belt")`, `Begin(skippable)`; `Cancel()` na mudança de estado. O resolvedor devolve o texto da chave (PT/EN) e `{count}` recebe o número de registros.

**Sem Game Over:** não há vida, dano nem repetição. A fantasia termina pelo relógio narrativo e os registros só avançam por ação do jogador (E mantido, clique ou "Ajuda de alinhamento" acionada).

### Movimento reduzido e pausa

`SetReducedMotion(true)` reduz a rotação visual a 35% e nunca adiciona vibração; a câmera nunca se move por causa da rotação. `SetTimeSource(SessionClock)` faz a rotação congelar na pausa (sem fonte, na cena isolada, usa o delta do quadro).
