# Capítulo II — Pioneer 10 — R5

Scripts, dados, prefabs, arte e cena do capítulo. Roteiro: `Docs/Chapters/02_PIONEER_10.md`. Fontes: `Docs/Chapters/02_PIONEER_SOURCES.md`.
Código de jogo em `Runtime/` (caminho usado por todas as tarefas PIO), ferramentas de Editor em `Editor/` (assembly `Abandoned.SpaceApps.Editor`), testes em `Tests/` (assembly `Abandoned.SpaceApps.Core.Tests`). A pasta vazia `Scripts/` continua porque o validador da CORE-001 e a arquitetura a listam; unificar com `Runtime/` é decisão de R1.

## Dados do capítulo (PIO-001)

Fonte única de ordem, tempos, cues, textos e fatos. Fica em `Data/` (entrada de produção), fora de `Assets/`.

| Arquivo | Conteúdo |
| --- | --- |
| `Data/Chapters/Pioneer10.json` | Oito estados com ID estável e ordem explícita (375 s nominais, até 381 s com o alongamento de leitura de PIO.07.06 e PIO.04.09), 45 cues (janela local ou gatilho com tempo mínimo de leitura), grupos de texto, **mapa de fatos → fontes**, divergências entre fontes e correções aplicadas |
| `Data/Localization/Pioneer10.pt-BR.json`, `Pioneer10.en.json` | 102 chaves iguais nos dois idiomas, por grupo (`narrative`, `hud`, `instrument`, `card`) e `status` (`approved` = transcrito do roteiro; `derived` = montado ou traduzido a partir do roteiro, **a revisar pela R7**) |

- **Tempo** é local ao estado, no relógio ativo; datas históricas são dados. A recepção na Terra (rádio) fica separada da operação na sonda (lançamento) no campo `kind` de cada fato.
- **IDs de gatilho** de `PIO.Imaging` seguem o código integrado (`Gate4`, `Gate6`, `GateRecovery`, `Gate8`); os de `PIO.Plaque` e `PIO.Listening` são **propostos** e devem ser confirmados por PIO-009 e PIO-011.
- **Validar:** `python Tools/validate_pioneer_data.py` (JSON, IDs, soma de 375 s, janelas sem sobreposição, chaves PT/EN iguais, fontes, datas UTC, termos que o registro de fontes manda evitar) e o teste `PioneerDataTests`, que cruza os dados com `PioneerPresentation.StateIds`, as constantes de texto do código e as sequências `PIO_Launch/Belt/Imaging`.
- **Cartões de data** (`PIO.Card.*`): o roteiro só cita o texto de alguns; os marcados `derived` usam apenas datas e frases do roteiro.

## Legendas e áudio ligados aos estados (PIO-013)

| Arquivo | Papel |
| --- | --- |
| `Data/PioneerData.asset` | Estados, 45 cues e 102 textos PT/EN importados de `Data/` (PIO-001). `GetText(chave, idioma)` devolve vazio para chave ou idioma desconhecidos (a caixa de legenda some); `ReadingSeconds` estima o tempo de leitura sem nunca acelerá-la |
| `Audio/PioneerCueMap.asset` | Música por estado (Partida, Curiosidade, Distância, Escuta; crossfade de 1,75 s, dentro de 1,5–2 s), efeitos de interface, o quase silêncio da virada do cinturão (nível 0,15 em 1,5 s) e o silêncio real de 2,5 s da tentativa 4. **Só IDs de cue da folha de cues (AUD-001): nenhum clipe, nenhuma licença adquirida** |
| `Runtime/PioneerNarration.cs` | Escuta a `SequenceDirector`: mostra e esconde a legenda, troca de idioma sem reiniciar, pede música só quando o cue muda e aplica os efeitos do mapa. `Cancel()` encerra tudo e ignora eventos até o próximo `EnterState` |
| `Runtime/IPioneerAudioPort.cs` | Porta de pedidos de áudio, **sem implementação**: o `AudioDirector` (AUD-002) a adapta (`SetCue(cueId, fadeSeconds)` do contrato) |
| `Editor/PioneerDataBuilder.cs` | Importador determinístico (menu **PIO-013 → Importar dados e mapa de cues**); reimportar mantém GUIDs e arquivos idênticos |

**Uso pelo controlador (PIO-014):** `narration.Configure(data, cueMap)`, `narration.Initialize(sequenceDirector, audioPort, idioma)`, `EnterState(estado)` ao entrar em cada estado, `RaiseTrigger(id)` quando o jogo aciona uma legenda por gatilho (peça encaixada, contato), `RaiseEvent(id)` para eventos que não vêm da sequência, `SetPaused(bool)`, `Cancel()` ao reiniciar ou sair e `Resync()` depois do clique que libera o áudio no Web.

**O que ainda depende de outras tarefas:** o áudio de verdade e o clique inicial (AUD-002), a caixa de legenda global e o `LocalizationService` (UI-004) e a escolha e a licença das faixas (AUD-001/AUD-002). Até lá, `TextChanged` entrega o texto e vazio significa esconder a caixa.

## Fotos históricas (PIO-007)

Duas imagens reais da Pioneer 10 para a revelação em `PIO.Imaging`, ambas com crédito **NASA**:

| Imagem | Arquivo Web | Natureza |
| --- | --- | --- |
| Ganimedes, 1973 (AC73-9279) | `Art/Historical/Pioneer10_Ganymede_1973_web.jpg` (1024 × 1024) | Foto de baixíssima resolução, fora de foco: é o que a sonda registrou. Recorte em torno do disco e redução 2:1; **nada foi ampliado** |
| Júpiter, 1973 (AC73-9341) | `Art/Historical/Pioneer10_Jupiter_1973_web.jpg` (1024 × 795) | **Montagem sequencial** de quadros da aproximação, não uma foto única |

- **Procedência completa** (páginas oficiais, URLs, datas, hashes, dimensões, transformações e limites): `Data/Assets/PioneerHistoricalImages.json` e `ArtSource/Pioneer10/PIO-007/SOURCE.md`. Os originais ficam em `ArtSource/` (LFS), fora de `Assets/` e do build.
- **Texto exibido junto da imagem:** `PIO.UI.real` ("Imagem real · Pioneer 10 · 1973 · NASA"). A Io perdida **não** tem imagem: `PIO.04.07` só a menciona.
- **Regenerar e conferir:** `python Tools/convert_pioneer10_photos.py` (gera as versões Web e o registro) e `python Tools/convert_pioneer10_photos.py --check`. A importação das texturas (sRGB, sem mipmaps, 1024 px, não legível pela CPU) está em **PIO-007 → Configurar importação das fotos históricas**.
- Quem exibe as imagens (PIO-008 e UI-007) usa estes dois arquivos e deve mostrar o rótulo acima; nenhuma outra versão deve entrar na build.

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

## Modelo da sonda (PIO-002)

Prefab `Art/PioneerActors.prefab` (raiz `PioneerActors`, componente `PioneerProbeRig`), derivado do GLB da NASA fornecido pela equipe. Origem, SHA-256, medidas e alterações em `ArtSource/Pioneer10/PIO-002/SOURCE.md`; direitos em `ASSET_CREDITS.md`.

| Arquivo | Papel |
| --- | --- |
| `ArtSource/Pioneer10/PIO-002/original/Pioneer 10.glb` | Original (LFS), fora do build. Origem **verificada**: idêntico ao espelho oficial da NASA (SHA-1 de blob Git; ver SOURCE.md) |
| `Tools/convert_pioneer10.py` (raiz do repositório) | Decodifica Draco, separa 6 peças, converte eixos, reduz 11 materiais a 1 (atlas 2048). Precisa de Python com DracoPy, numpy e Pillow; sem Blender |
| `Data/Assets/Pioneer10.json` e `Pioneer10.geometry.json` | Manifesto e geometria derivada (entrada do builder) |
| `Art/Probe/Meshes/`, `Art/Probe/Textures/PioneerAtlas.png`, `Art/PioneerMaterials/` | Malhas, atlas e 2 materiais (`PIO_ProbeAtlas`, `PIO_PlaqueAnodized_PROXY`) |
| `Runtime/PioneerProbeRig.cs` | Pivôs: `SetSpinDegrees`, `SetBoomDegrees`, `SetCruise`, `TryGetSocket`, `ValidateSetup`. Sem ciência, entrada nem estados de capítulo |
| `Editor/PioneerProbeBuilder.cs` | Builder CORE-006 `pioneer10.probe`: prefab, materiais e cena `Art/Probe/Preview/ProbePreview.unity` |
| `Editor/PioneerProbeChecks.cs`, `Tests/PioneerProbeTests.cs` | Verificações e testes EditMode do prefab/rig |
| `Editor/PioneerProbeCapture.cs` | Prévia em `Docs/Art/Pioneer10_Probe/` (menu PIO-002 → Capturar sonda) |

**Hierarquia.** `PioneerActors/Spin_Root` (sobre o eixo da antena, +Z local, z = 0 no vértice do refletor) contém `Bus`, `Dish`, `Feed`, as três hastes (`RtgBoomA`, `RtgBoomB`, `MagnetometerBoom`, cada uma com pivô na dobradiça) e `Plaque`; `Sockets` contém `Reflector`, `Feed`, `EquipmentCompartment`, `RtgBoomA`, `RtgBoomB`, `MagnetometerBoom`, `Plaque` e `Photopolarimeter`. Não há esqueleto skinned (IMPORT_RULES §7: Animation Type None); o “rig” é só hierarquia de pivôs.

**Contrato com o palco (PIO-003 → PIO-014).** O `Spin_Root` do modelo corresponde ao `ProbeSpin` do palco: ao trocar a silhueta, instancie `PioneerActors` no lugar de `ProvisionalSilhouette` dentro de `Regions/DeepSpace/Probe/ProbeSpin` e não gire o `Spin_Root` por conta própria (a rotação continua sendo do `PioneerPresentation`; evite aplicar duas vezes). Os sockets têm os mesmos nomes dos de `ProbeSpin/Sockets`, então as atividades continuam encontrando seus pontos: copie a pose de cada socket do modelo para o socket do palco, ou use `TryGetSocket`. Escala real (refletor 2,74 m), nenhuma escala na raiz.

**Limitações (hipóteses e provisórios).** Ângulo 0 das hastes = pose do modelo = cruzeiro; o eixo de cada dobradiça é **hipótese** (`axisVerified = false`) e o limite de 90° é do rig, não dado histórico. O socket `Photopolarimeter` é provisório. O modelo **não traz a placa**: o nó `Plaque` (malha `_PROXY`, 152 × 229 mm) está entre os dois suportes da antena do lado +X, posição não verificada em referência. O refletor mede 2,740 m em X e 2,828 m em Y. O LOD1 foi criado (ver abaixo).

**LOD1 e custo Web (IMPORT_RULES §5 e §7).** `PioneerActors` tem um `LODGroup`: LOD0 = as 6 peças e a placa (8.199 triângulos) até 12% da altura da tela; LOD1 = a malha única `Lod1` (filha do `Spin_Root`, mesmo material e atlas), obtida por agrupamento de vértices numa grade de 15 cm e por face da normal (a frente e o verso do refletor não se fundem), com no máximo 30% dos triângulos e a mesma caixa do LOD0 (±30 cm); abaixo de 1% a sonda é cortada. As malhas têm Optimize Mesh e compressão média; o Read/Write fica ligado porque são assets gerados que o builder reescreve e as verificações leem (não passam pelo `ModelImporter`). `Docs/Art/Pioneer10_Probe/PROBE_STATS.md` traz a estimativa de custo Web (atlas 2048² em DXT1 ≈ 2,7 MiB com mipmaps; malhas) e o número de triângulos do LOD1.

**Reconstruir:** Abandoned Space Apps → CORE-006 → Reconstruir todas as cenas, ou `-executeMethod Abandoned.SpaceApps.EditorTools.SpaceAppsSceneBuilder.BuildFromCommandLine -builders pioneer10.probe`. Para regerar os dados a partir do GLB: `python Tools/convert_pioneer10.py`. Não edite o prefab à mão: altere o builder.

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

## Varredura IPP (PIO-006)

| Arquivo | Papel |
| --- | --- |
| `Sequences/PIO_Imaging.asset` | 74 s: aprender 0–16, oito passagens 16–48, revelação 48–74. Marcos (gates) em 28, 34, 40 e 46 s; legendas PIO.04.01–09 |
| `Runtime/PioneerImaging.cs` | Lógica do estado (sem UI): cursor de fase, janela, uma faixa por passagem, ajuda, interrupção em 6/8, retomada idempotente, 8/8 |
| `Runtime/ImagingPanelView.cs` + `UI/ImagingPanel.uxml/.uss/.prefab` | Painel UI Toolkit no `UiFrame` do anchor `PIO.Imaging` (direita). Só desenha um `ImagingViewState`. Textos por chave (`PIO.UI.red`, `.blue`, `.scan`) |
| `Editor/PioneerImagingBuilder.cs` | Builder CORE-006 `pioneer10.stage.imaging` (raiz `[R5] Pioneer Imaging`, depois de `pioneer10.stage`) |
| `Editor/PioneerImagingCapture.cs` | Prévia do painel em `Docs/Art/Pioneer10_Imaging/<estado>_<L>x<A>.png` nas 3 resoluções do UI_SPEC (1280×720, 960×600, 1024×768) sobre fundo claro (pior caso) e as auditorias `QA/PIO-006/contrast-audit.txt` e `layout-audit.txt` (menu PIO-006). Textos aparecem como chaves |
| `Tests/PioneerImagingTests.cs` | 20 testes EditMode |

**Como joga.** Cada passagem do cursor leva 3 s e a zona larga (25–75% da passagem, contorno grosso) é a travessia do planeta. Se E estiver mantido em qualquer momento da zona, a passagem registra UMA faixa (um conjunto de linhas: síntese de muitos registros, não oito linhas históricas). As faixas 1–4 são do canal vermelho e as 5–8 do azul; o verde **não é medido**: a legenda de composição (contorno verde sem preenchimento) aparece em 8/8 e o cue PIO.04.06 explica a estimativa. Não há ritmo: E mantido desde o aprendizado vale para a primeira janela; um toque curto entre dois quadros também conta. Perder uma passagem só oferece outra volta (`PassMissed`) e nunca apaga faixas. Após 6 s sem faixa, o prompt reforça o contorno do E; após 12 s a janela cobre toda a passagem (assistido, até o fim da execução). Soltar E, pausar ou perder o foco só pára; as faixas ficam.

**Uso pelo controlador (PIO-014):** `imaging.Initialize(sequenceDirector, interactionService, inputRouter, sessionClock, resolverDeTexto)`, `Completed += () => chapterDirector.CompleteStep("PIO.Imaging")`, `Begin(skippable)`; `Cancel()` na mudança de estado. O concluído vem do fim da sequência (depois de 8/8), nunca do tempo.

**Contrato com a PIO-008 (recuperação e revelação):**
- `RadiationInterruption` dispara UMA vez ao fim da passagem da faixa 6. A aquisição pára com as seis faixas e a sequência segura o marco de 40 s.
- A PIO-008 mostra ruído local, aviso e o botão de recuperação (`PIO.UI.recover`) e chama `ResumeAfterRecovery()`. Ela é **idempotente**: só a primeira chamada durante a interrupção retoma (devolve `true`); as demais devolvem `false` e não mudam nada. `Recovered` dispara uma vez. O painel expõe `RecoverySlot` (contêiner vazio) e `ImagingViewState.Interrupted` para a PIO-008 ligar sua UI sem editar este painel.
- `AllStripsCaptured` (8/8) dispara UMA vez; `RevealStarted` marca os 48 s da sequência (composição, crossfade para a foto real e Ganimedes são da PIO-008).
- **Enquanto a PIO-008 não existir, nada chama `ResumeAfterRecovery()`: o capítulo espera na interrupção (sem softlock por inatividade, mas sem avançar).** Os testes chamam a retomada diretamente.

**Conformidade com o UI_SPEC e a arquitetura.** `PioneerImaging.ValidateSetup()` e `ImagingPanelView.ValidateSetup()` listam erros de montagem (referências, sequência de 74 s com os 7 marcos e as 9 legendas, anchor com quadro de UI, elementos do UXML, `ResponsiveLayoutClass`, chaves `PIO.UI.*`); o builder chama os dois em `Validate`. O `UIDocument` tem `ResponsiveLayoutClass` (classe `sa-layout--narrow` em 4:3, com regras no USS). O prompt de E vira **uma só** classe `sa-hold--available|rearm|holding|paused|done` (`ImagingViewState.Hold`): rearme = E fisicamente pressionado sem hold (o router já suprime o E que abre a atividade), pausado = soltou com faixas registradas ou interrupção em 6/8. Os textos de status do prompt ("PAUSADO · PRESERVADO" etc.) pertencem à UI-003/UI-004 e não foram inventados aqui.

**Marcos da sequência:** em ritmo nominal (uma faixa por passagem) nenhum marco espera e as legendas PIO.04.03–05 caem nas lacunas do roteiro (28, 34 e 40 s). Quem joga mais devagar vê a sequência parar no marco (legendas e câmera congelam juntas) até a condição: 4/8 em 28 s, interrupção em 34 s, recuperação em 40 s, 8/8 em 46 s.

**Limitações.** O quadro de imagem é uma representação gráfica das faixas, sem a fotografia (PIO-008 e PIO-007). Ruído, aviso de radiação, botão de recuperação e revelação são da PIO-008. Os textos ainda são as chaves (UI-004). Duração de passagem (3 s) e largura da janela são decisão de jogabilidade, não dado histórico. Play Mode e navegador não foram exercitados.

**Reconstruir:** Abandoned Space Apps → CORE-006 → Reconstruir todas as cenas, ou `-executeMethod Abandoned.SpaceApps.EditorTools.SpaceAppsSceneBuilder.BuildFromCommandLine -builders pioneer10.stage.imaging`.

## Recuperação de radiação e revelação histórica (PIO-008)

| Arquivo | Papel |
| --- | --- |
| `Runtime/ImagingRecovery.cs` | Em `RadiationInterruption` (6/8): ruído estático e localizado no quadro da imagem, aviso (`PIO.UI.radiation`) e UM botão de E (`PIO.UI.recover`, toque confirma; também clicável). A confirmação envia o pacote (`PIO.UI.sent`) por uma elipse temporal de 4 s de tempo ATIVO e só então chama `ResumeAfterRecovery()`. Vive no `RecoverySlot` do painel da PIO-006, sem editar o UXML dela |
| `Runtime/HistoricalImageReveal.cs` + `UI/HistoricalImageReveal.uxml/.uss/.prefab` | Depois de `RevealStarted` (48 s): crossfade de 1 s para a montagem real de Júpiter aos +7 s (legenda PIO.04.07, que fala de Io) e para Ganimedes aos +15 s (PIO.04.08). Rodapé com `PIO.UI.real` (Pioneer 10 · 1973 · NASA) e a data (`PIO.UI.jupiter`, `PIO.UI.ganymede`); Io aparece como "imagem perdida" (`PIO.UI.io_lost`) |
| `UI/ImagingRecovery.uss` | Estilo do aviso, do botão, da barra do pacote e do ruído |
| `Editor/PioneerRecoveryBuilder.cs` | Builder CORE-006 `pioneer10.stage.imaging.recovery` (raiz `[R5] Pioneer Imaging Recovery`, depois de `pioneer10.stage.imaging`) |
| `Editor/PioneerRecoveryCapture.cs` | Prévia em `Docs/Art/Pioneer10_Recovery/` (menu PIO-008; **precisa da janela da Unity em primeiro plano**) |
| `Tests/PioneerRecoveryTests.cs` | 16 testes EditMode |

**Sem falha, sem flash.** Nada reinicia nem apaga faixas: o E repetido envia **um** pacote, o E que ainda estava mantido da varredura não confirma sozinho (o hold é rearmado), pausar congela o pacote (tempo ativo) e cancelar a varredura zera o estado. O ruído é estático (sete faixas finas e opacas a 12–26%), o aviso não pulsa e as passagens de imagem são só opacidade, uma vez. Tudo funciona sem som. O aviso tem triângulo e borda grossa (não depende só de cor). O prompt de E da varredura se recolhe enquanto o botão de recuperação está à vista (um único E por vez).

**Uso pelo controlador (PIO-014):** depois de `imaging.Initialize(...)`, chamar `recovery.Initialize(interactionService, sessionClock, resolverDeTexto)` e `reveal.Initialize(sessionClock, resolverDeTexto)`. Ambos escutam os eventos da `PioneerImaging` e se limpam sozinhos quando a varredura termina ou é cancelada.

**Textos novos (PIO-001):** `PIO.UI.radiation` (do roteiro, `approved`) e `PIO.UI.sent`, `PIO.UI.jupiter`, `PIO.UI.ganymede`, `PIO.UI.io_lost` (`derived`: a R7 precisa revisar). A legenda de Júpiter diz que é uma **montagem**.

**Limitações.** Revisão da R7 pendente (textos `derived` e a data de Júpiter só como "dezembro de 1973", porque a página da NASA não dá o instante de cada quadro). A duração de 4 s do pacote e os instantes de entrada das fotos são decisão editorial, não dado histórico. A revelação é guiada por tempo ativo próprio (não pelo relógio da sequência): em pausa congelam juntos, mas não foi medida a deriva em sessões longas. Sem áudio (AUD-002/UI-004 ainda não existem) e sem caixa de legenda real. Play Mode e navegador não foram exercitados.

## Caminho de saída (PIO-010)

| Arquivo | Papel |
| --- | --- |
| `Sequences/PIO_Outbound.asset` | 39 s: cartão 1976 (0–12), cartão 13 JUN 1983 (12–24), energia e gráfico de 1998 (24–39). Marco `PIO.Outbound.Power` aos 24 s segura a sequência até o hold; `PIO.Outbound.Chart` (25 s) só vem depois. Legendas PIO.06.01–05 |
| `Runtime/OutboundAtlas.cs` | Lógica do estado (sem UI): fases, hold de 3 s (E), conclusão única, cancelar e pular |
| `Runtime/OutboundAtlasView.cs` + `UI/OutboundAtlas.uxml/.uss/.prefab` | Painel UI Toolkit que ocupa o quadro (o anchor `PIO.Outbound` não reserva `UiFrame`). Diagrama orbital, barras de energia e gráfico por `Painter2D`; só desenha um `OutboundViewState`. Textos por chave |
| `Editor/PioneerOutboundBuilder.cs` | Builder CORE-006 `pioneer10.stage.outbound` (raiz `[R5] Pioneer Outbound`) |
| `Editor/PioneerOutboundCapture.cs` | Prévia em `Docs/Art/Pioneer10_Outbound/` com os textos em português (menu PIO-010; **precisa da janela da Unity em primeiro plano**) |
| `Tests/PioneerOutboundTests.cs` | 12 testes EditMode |

**O que se vê.** 1976: quatro anéis esquemáticos (Júpiter, Saturno, Urano, Netuno, **espaçamento igual e sem escala**) e o arco de saída; o marcador da sonda cai no anel de Saturno ("distância orbital de Saturno · a sonda não visita o planeta"). 1983: o marcador vai ao anel de Netuno ("além das órbitas dos planetas") com a nota de que isso não é sair do Sistema Solar. Nenhum planeta é desenhado junto da sonda. Aos 24 s aparecem as barras de energia (**relativas, sem números**; o rádio tem contorno e ícone próprios) e o prompt "Segure E · priorizar a comunicação". Depois do hold, os instrumentos secundários baixam (nunca a zero), o rádio enche e entra o gráfico de 1998: DUAS linhas de **distância ao Sol** (Pioneer 10 contínua com marcador quadrado; Voyager 1 tracejada com círculo), cruzando no ponto de 1998, sem encontro e sem segunda sonda 3D. O **tempo de comunicação** é outra grandeza, com barra e rótulo próprios, que cresce com a distância e nunca mostra valor.

**Regras de entrada (as mesmas da PIO-004).** O E que já estava pressionado antes do hold não conta (rearme); soltar E pausa e o progresso fica; a pausa do shell congela tudo; cancelar ou pular encerra hold, contexto e candidato; `Completed` e `PowerConfirmed` disparam uma vez. Não existe falha: esperar não consome nem pune.

**Uso pelo controlador (PIO-014):** `atlas.Initialize(sequenceDirector, interactionService, inputRouter, sessionClock, resolverDeTexto)`, `Completed += () => chapterDirector.CompleteStep("PIO.Outbound")`, `Begin(skippable)`; `Cancel()` na mudança de estado.

**Textos novos (PIO-001):** 19 chaves nos dois idiomas, todas `derived` (a R7 precisa revisar). Os cartões usam as chaves `PIO.Card.y1976/y1983/y1998` já existentes.

**Limitações.** O diagrama e o gráfico são **qualitativos**: as inclinações das linhas, o espaçamento dos anéis, o ângulo do arco e as barras são decisão gráfica, não dados. Que o cruzamento de 1998 é de distância ao Sol vem da fonte P-S13; o que 1976 e 13 JUN 1983 marcam (distância orbital de Saturno e de Netuno) vem do roteiro e a R7 precisa conferir as datas contra fonte primária. A câmera 3D fica atrás do painel (a sonda não aparece). Sem áudio nem legenda real (AUD-002/UI-004), com textos por chave. Play Mode e navegador não foram exercitados. O contraste não foi medido nos pixels.

**Reconstruir:** Abandoned Space Apps → CORE-006 → Reconstruir todas as cenas, ou `-executeMethod Abandoned.SpaceApps.EditorTools.SpaceAppsSceneBuilder.BuildFromCommandLine -builders pioneer10.stage.outbound`.
