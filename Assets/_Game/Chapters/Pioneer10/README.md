# Capítulo II — Pioneer 10 — R5

Scripts, dados, prefabs, arte e cena do capítulo. Roteiro: `Docs/Chapters/02_PIONEER_10.md`. Fontes: `Docs/Chapters/02_PIONEER_SOURCES.md`.
Código de jogo em `Runtime/` (caminho usado por todas as tarefas PIO), ferramentas de Editor em `Editor/` (assembly `Abandoned.SpaceApps.Editor`), testes em `Tests/` (assembly `Abandoned.SpaceApps.Core.Tests`). A pasta vazia `Scripts/` continua porque o validador da CORE-001 e a arquitetura a listam; unificar com `Runtime/` é decisão de R1.

## Dados do capítulo (PIO-001)

Fonte única de ordem, tempos, cues, textos e fatos. Fica em `Data/` (entrada de produção), fora de `Assets/`.

| Arquivo | Conteúdo |
| --- | --- |
| `Data/Chapters/Pioneer10.json` | Oito estados com ID estável e ordem explícita (375 s nominais, até 381 s com o alongamento de leitura de PIO.07.06 e PIO.04.09), 45 cues (janela local ou gatilho com tempo mínimo de leitura), grupos de texto, **mapa de fatos → fontes**, divergências entre fontes e correções aplicadas |
| `Data/Localization/Pioneer10.pt-BR.json`, `Pioneer10.en.json` | 111 chaves iguais nos dois idiomas, por grupo (`narrative`, `hud`, `instrument`, `card`) e `status` (`approved` = transcrito do roteiro; `derived` = montado ou traduzido a partir do roteiro, **a revisar pela R7**) |

- **Tempo** é local ao estado, no relógio ativo; datas históricas são dados. A recepção na Terra (rádio) fica separada da operação na sonda (lançamento) no campo `kind` de cada fato.
- **IDs de gatilho** de `PIO.Imaging` seguem o código integrado (`Gate4`, `Gate6`, `GateRecovery`, `Gate8`); os de `PIO.Plaque` foram **confirmados** pela PIO-009 (`PioneerPlaqueActivity` aciona `PIO.Plaque.Hydrogen`, `.Pulsars`, `.SolarSystem`, `.PioneerScale` e `.People`); os de `PIO.Listening` continuam **propostos** e devem ser confirmados pela PIO-011.
- **Validar:** `python Tools/validate_pioneer_data.py` (JSON, IDs, soma de 375 s, janelas sem sobreposição, chaves PT/EN iguais, fontes, datas UTC, termos que o registro de fontes manda evitar) e o teste `PioneerDataTests`, que cruza os dados com `PioneerPresentation.StateIds`, as constantes de texto do código e as sequências `PIO_Launch/Belt/Imaging`.
- **Cartões de data** (`PIO.Card.*`): o roteiro só cita o texto de alguns; os marcados `derived` usam apenas datas e frases do roteiro.

## Legendas e áudio ligados aos estados (PIO-013)

| Arquivo | Papel |
| --- | --- |
| `Data/PioneerData.asset` | Estados, 45 cues e 111 textos PT/EN importados de `Data/` (PIO-001). `GetText(chave, idioma)` devolve vazio para chave ou idioma desconhecidos (a caixa de legenda some); `ReadingSeconds` estima o tempo de leitura sem nunca acelerá-la |
| `Audio/PioneerCueMap.asset` | Música por estado (Partida, Curiosidade, Distância, Escuta; crossfade de 1,75 s, dentro de 1,5–2 s), efeitos de interface, o quase silêncio da virada do cinturão (nível 0,15 em 1,5 s) e o silêncio real de 2,5 s da tentativa 4. **Só IDs de cue da folha de cues (AUD-001): nenhum clipe, nenhuma licença adquirida** |
| `Runtime/PioneerNarration.cs` | Escuta a `SequenceDirector`: mostra e esconde a legenda, troca de idioma sem reiniciar, pede música só quando o cue muda e aplica os efeitos do mapa. `Cancel()` encerra tudo e ignora eventos até o próximo `EnterState` |
| `Runtime/IPioneerAudioPort.cs` | Porta de pedidos de áudio. Implementação: `PioneerAudioAdapter` (AUD-002), que repassa ao `AudioDirector` (`SetCue`, nível, efeito, silêncio, pausa, parar). Os clipes ainda não existem (cues pendentes) |
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

A sonda é `Regions/DeepSpace/Probe/ProbeSpin`: eixo Z local = eixo da antena = eixo de rotação (P-S05). **Desde 15/11/2026 o palco instancia o modelo da PIO-002** (`Art/PioneerActors.prefab`, instância `PioneerActors` dentro de `ProbeSpin`, refletor em escala real de 2,74 m); a silhueta provisória (G-PIO-03) foi removida junto com suas malhas e materiais. Os **sockets** do palco (`Reflector`, `Feed`, `EquipmentCompartment`, `RtgBoomA`, `RtgBoomB`, `MagnetometerBoom`, `Plaque`, `Photopolarimeter`) são os do próprio modelo; os das hastes acompanham a articulação. A rotação continua sendo do `ProbeSpin`: o `Spin_Root` do modelo fica parado. O close da placa (`PIO.Plaque`) é calculado a partir da placa do modelo pelo `PioneerStageBuilder`; o do fotopolarímetro aponta para um socket de posição **hipotética** (o modelo não traz uma peça identificada como o instrumento). O custo Web subiu de 14,74 para 15,67 MiB na cena (build medida, não o navegador).

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
## Placa de cinco símbolos (PIO-009)

| Arquivo | Papel |
| --- | --- |
| `Sequences/PIO_Plaque.asset` | 47 s: chegada à placa 0–9, cinco peças 9–38 (marco `PIO.Plaque.Gate` em 37,5 s), placa inteira 38–47. Legendas PIO.05.01 e .07 por janela; PIO.05.02–06 **por gatilho**, 4 s cada |
| `Runtime/PioneerPlaqueActivity.cs` | Lógica do estado (sem UI): seleção, encaixe, setas + E, ajuda de segurar E, contorno errado sem perda, marco 5/5 |
| `Runtime/PlaquePanelView.cs` + `UI/PlaquePanel.uxml/.uss/.prefab` | Painel UI Toolkit: quadro 2:3 com cinco contornos nomeados e a bandeja com as cinco peças; só desenha um `PlaqueViewState` e devolve cliques e soltar do arrastar. Textos por chave (`PIO.UI.*`) |
| `Art/PlaqueEducational.svg` | SVG mestre da placa educativa (traço dourado). Gerado: **não editar à mão** |
| `Art/Plaque/Textures/PIO_Plaque_*.png` | Os cinco símbolos (256 px, transparentes) usados pelo painel; saem da MESMA lista de formas do SVG |
| `Editor/PioneerPlaqueArt.cs` | Lista de formas (círculos, traços, polígonos) que gera o SVG e rasteriza os PNGs com antisserrilhado 4×4 |
| `Editor/PioneerPlaqueBuilder.cs` | Builder CORE-006 `pioneer10.stage.plaque` (raiz `[R5] Pioneer Plaque`, depois de `pioneer10.stage`) |
| `Editor/PioneerPlaqueCapture.cs` | Prévias em `Docs/Art/Pioneer10_Plaque/<estado>_<L>x<A>.png` nas 3 resoluções do UI_SPEC, com as auditorias `QA/PIO-009/contrast-audit.txt` e `layout-audit.txt` (menu PIO-009) |
| `Tests/PioneerPlaqueTests.cs` | 19 testes EditMode |

**Como joga.** Toda peça mostra símbolo e nome, e cada contorno também (o símbolo esmaecido e o nome escrito): ninguém decifra código. Caminhos equivalentes e sem penalidade:
- **Mouse:** clicar a peça e depois o contorno, ou arrastar. Soltar a até **96 px** (lógicos, a 720p) de um contorno livre encaixa; solto longe, a peça só volta, sem mensagem.
- **Teclado:** as setas percorrem as peças restantes; E (toque) seleciona; as setas percorrem os contornos; E encaixa.
- **Ajuda:** segurar E por 2 s encaixa a peça focada (ou a selecionada) no contorno correto. As cinco levam ≤ 30 s assistidas (teste).

O contorno **correto** da peça selecionada pulsa devagar (1,4 s de período; com movimento reduzido fica parado e destacado). Um contorno errado devolve a peça e mostra uma mensagem **neutra** (`PIO.UI.plaque_retry`); nenhum encaixe é removido nem contado. Cada encaixe abre a legenda da peça por gatilho e o próximo item já está disponível. A sequência espera 5/5 no marco de 37,5 s; nada conclui por tempo. A placa 3D continua presa à sonda: a atividade não a move nem a oculta.

**Prompt de E.** Uma só classe `sa-hold--available|rearm|holding|paused|done`. Pressionar e soltar em até 0,35 s é um **toque**; segurar mais mostra a ajuda; soltar no meio pausa sem apagar nada. O E que ainda estava pressionado quando a atividade abriu fica em *rearme* até soltar.

**Arte.** Adaptação **educativa**, identificada como tal, não a reprodução fiel da gravura (NASA P-S11/P-S12): hidrogênio, mapa de pulsares, Sistema Solar (terceiro corpo, a Terra, marcado, e o arco até Júpiter), escala da Pioneer e **pessoas em silhueta lisa**, sem detalhe anatômico. As linhas dos pulsares, as marcas e a régua são **esquemáticas** e não carregam períodos nem medidas reais. Material: alumínio anodizado dourado, não ouro maciço. Registro em `ASSET_CREDITS.md` (G-PIO-06) e `Docs/Production/REUSE.md`.

**Textos novos (PIO-001).** `PIO.UI.hydrogen`, `.pulsars`, `.solar`, `.scale`, `.people` e `PIO.UI.plaque_retry` (PT/EN, status `derived`: a R7 precisa revisar) em `Data/Localization/`, reimportados em `PioneerData.asset` pelo importador da PIO-013. `PIO.UI.plaque`, `PIO.UI.snap` e as legendas PIO.05.* já existiam.

**Uso pelo controlador (PIO-014):** `plaque.Initialize(sequenceDirector, interactionService, inputRouter, sessionClock, resolverDeTexto)`, `Completed += () => chapterDirector.CompleteStep("PIO.Plaque")`, `Begin(skippable)`; `Cancel()` na mudança de estado. Eventos: `PieceFitted(peça, n)`, `WrongOutline`, `AllPiecesFitted` (uma vez) e `RevealStarted` (38 s). `SetReducedMotion(true)` para a configuração global. Os gatilhos das legendas saem pela `SequenceDirector` da própria atividade; `PioneerNarration.RaiseTrigger` é só uma fachada para o mesmo `director.Trigger`.

**Limitações.** Os textos dos contornos e das peças ainda são chaves até a UI-004. Os nomes e a frase neutra são `derived` (R7). O quadro é um layout simplificado (contornos lado a lado, sem a sobreposição das figuras com a silhueta da Pioneer da gravura). A arrastada só foi exercitada pela lógica e pela função de snap (`NearestOutline`): os eventos de ponteiro do painel não foram simulados em Play Mode. Play Mode e navegador não foram executados.

**Reconstruir:** Abandoned Space Apps → CORE-006 → Reconstruir todas as cenas, ou `-executeMethod Abandoned.SpaceApps.EditorTools.SpaceAppsSceneBuilder.BuildFromCommandLine -builders pioneer10.stage.plaque`.

## Escuta DSN (PIO-011)

| Arquivo | Papel |
| --- | --- |
| `Sequences/PIO_Listening.asset` | 52 s em seis planos: ensino 0–7, contatos 7–16, 16–25 e 25–37, tentativa 37–45, respirar 45–52. Marcos `PIO.Listening.Open1–4` e `Breathe`, cada um espera a etapa anterior. Legendas por janela (PIO.07.01, .05, .06) e **por gatilho** (PIO.07.02–04, leitura mínima 5/5/7 s) |
| `Runtime/PioneerListening.cs` | Lógica do estado (sem UI): zona de 30%, varredura assistida, escuta de 2 s, quatro resultados distintos, silêncio de 2,5 s, cancelar e pular |
| `Runtime/ListeningPanelView.cs` + `UI/ListeningPanel.uxml/.uss/.prefab` | Painel UI Toolkit embaixo à direita (UiFrame do anchor): onda de barras fixas, escala com zona e botão, setas, medidor de proximidade e o resultado. Só desenha um `ListeningViewState` |
| `Art/DsnSilhouette.prefab` + `Art/Dsn/` | **Desde 15/11/2026, o modelo NASA "Deep Space Network 70-meter"** (NASA/Kumanchik e Lopez) convertido por `Tools/convert_dsn70.py` (hierarquia base › azimute › elevação preservada, 33.112 triângulos, atlas 1024² em DXT1 na Web), no `DishSocket` do palco, reduzido a 13/35 (prato de 26 m, a câmera continua a 36 m); azimute a 90° e prato a 40° do zênite, voltado à câmera. A silhueta por código saiu |
| `Editor/PioneerListeningBuilder.cs` | Builder CORE-006 `pioneer10.stage.listening` (raiz `[R5] Pioneer Listening`) |
| `Editor/PioneerListeningCapture.cs` | Prévia do painel em `Docs/Art/Pioneer10_Listening/` (menu PIO-011; **precisa da janela da Unity em primeiro plano**) |
| `Tests/PioneerListeningTests.cs` | 16 testes EditMode |

**Como joga.** Setas/WASD e mouse movem o botão (contexto Gameplay do InputRouter). O botão começa a no máximo 15% da borda da zona (0,12 / 0,10 / 0,14) e a zona tem 30% da escala nas três rodadas, levemente deslocada. Um medidor e uma seta dizem de que lado está a zona (o equivalente visual do som). **Segurar E** fora da zona executa uma varredura assistida lenta que pára na primeira zona válida; dentro dela, E mantido escuta por 2 s (o progresso fica ao soltar ou sair). Concluir uma rodada exige soltar E antes da seguinte. Nada exige audição nem timing estreito. Na 3ª rodada o sinal é mais fraco (0,45) e a zona continua legível.

**Quatro resultados diferentes** (selo, cartão de data e forma de onda diferentes): 3 MAR 2002 sinal recebido (onda suave); 27 ABR 2002 últimos dados de telemetria (blocos regulares); 23 JAN 2003 portadora muito fraca, **sem dados novos** (uma linha fina com um único pico, o último sinal); 7 FEV 2003 nenhum sinal detectado. Na tentativa 4 **não há zona nem frequência correta**: "Segure E · escutar" por 2 s e "Nenhum sinal detectado · Sua escuta funcionou. Desta vez, não houve resposta." Depois os controles recolhem e há **2,5 s de silêncio real** (onda parada e mínima, nada animado). Datas são de recepção na Terra em UTC.

**Eventos para a integração (PIO-014).** `StateEvent` dispara `PIO.Listening.Tune` (a cada rodada), `.Contact1/2/3` e `.Attempt4` (início do silêncio): ligar a `PioneerNarration.RaiseEvent`. Os mesmos ids `Contact1/2/3` são os gatilhos das legendas, que a atividade abre direto no `SequenceDirector.Trigger`. Isso **confirma** os ids propostos na PIO-001/013. Uso: `listening.Initialize(sequenceDirector, interactionService, inputRouter, sessionClock, resolverDeTexto)`, `Completed += () => chapterDirector.CompleteStep("PIO.Listening")`, `Begin(skippable)`; `Cancel()` na mudança de estado.

**Textos novos (PIO-001):** `PIO.UI.listen`, `PIO.UI.kind_data` e `PIO.UI.kind_carrier` (os três `derived`, a R7 precisa revisar). Os cartões e as chaves `tune`, `nearer`, `farther`, `received`, `no_signal`, `no_fault` e `help` já existiam.

**Limitações.** **A antena é o modelo NASA da DSN de 70 m, sem sítio identificado** (Goldstone, Canberra ou Madri; a página não diz) e sem comparação lado a lado com fotografia pela R7. Ela foi reduzida à escala do palco (13/35), então não está em escala real em relação à sonda. A pose (azimute e elevação) é decisão de cena, não dado histórico; as articulações existem no prefab, mas nada as anima. (A versão anterior era uma silhueta por código.) A duração de 2 s da escuta na tentativa 4, as velocidades do botão e da varredura e os deslocamentos das zonas são decisão de jogabilidade, não dado histórico. Parte do lado direito do prato fica sob o painel no 1280×720. Sem áudio nem legenda real (AUD-002/UI-004), textos por chave. Mouse não foi testado (só teclado, que é o caminho obrigatório). Play Mode e navegador não foram exercitados. Contraste não medido nos pixels.

**Reconstruir:** Abandoned Space Apps → CORE-006 → Reconstruir todas as cenas, ou `-executeMethod Abandoned.SpaceApps.EditorTools.SpaceAppsSceneBuilder.BuildFromCommandLine -builders pioneer10.stage.listening`.

## Despedida e conclusão (PIO-012)

| Arquivo | Papel |
| --- | --- |
| `Sequences/PIO_Farewell.asset` | 38 s, seis planos: antena terrestre 0–4, contorno do refletor 4–8 (o match cut), placa em close 8–14, plano aberto com a direção de Aldebaran 14–20, a Pioneer diminuindo 20–30 e o preto do memorial 30–38. Legendas PIO.08.01–04 por janela |
| `Runtime/PioneerFarewellActivity.cs` | Lógica do estado (sem UI): marcos, guia, preto, conclusão em 36 s, ações finais por setas + E/Enter |
| `Runtime/FarewellPanelView.cs` + `UI/FarewellPanel.uxml/.uss/.prefab` | Painel UI Toolkit: guia "direção de Aldebaran", preto intencional, `PIO.UI.ending` em duas linhas e os botões `PIO.UI.return` e `PIO.UI.replay`. Só desenha um `FarewellViewState` |
| `Editor/PioneerFarewellBuilder.cs` | Builder CORE-006 `pioneer10.stage.farewell` (raiz `[R5] Pioneer Farewell`, depois de `pioneer10.stage`) |
| `Editor/PioneerFarewellCapture.cs` | Prévias em `Docs/Art/Pioneer10_Farewell/<estado>_<L>x<A>.png` nas 3 resoluções do UI_SPEC, com as auditorias `QA/PIO-012/contrast-audit.txt` e `layout-audit.txt` (menu PIO-012) |
| `Tests/PioneerFarewellTests.cs` | 14 testes EditMode |

**A cena.** Quatro enquadramentos narrativos, cada plano com um rig do palco **diferente do anterior** (nunca o mesmo zoom): `PIO.Listening` (o círculo da antena) → `PIO.Launch.Reveal` (o contorno do refletor, o match cut) → `PIO.Plaque` (a placa, com a luz de apresentação dela) → `PIO.Farewell` (plano aberto) → `PIO.Outbound` (a Pioneer diminuindo, a antena ainda reconhecível: a câmera **recua** 25 m ao longo do eixo do rig durante os 10 s e mais ~7 m no preto; a primeira versão avançava +13 m e a sonda crescia, erro visto só nos quadros 3D e coberto agora por teste) → preto, que continua o plano anterior sem salto. A luz da placa apaga com a troca de rig, **antes** do plano aberto. A direção de Aldebaran é o ponto avermelhado distante do palco, que **não ilumina a nave**, mais o guia discreto `PIO.UI.aldebaran` (14–20 s): direção geral, nunca "a sonda chegou". Nenhum plano aproxima planetas e a escala "milhões de anos" fica na legenda PIO.08.03. O eixo e o spin de cruzeiro vêm da `PioneerPresentation` e não são tocados.

**Conclusão e memorial.** O preto sobe em 1,5 s de tempo **ativo** a partir dos 30 s (instantâneo com movimento reduzido, congelado na pausa), seguido de "PIONEER 10 · 1972–2003" e "Em viagem" (a linha de datas é o intervalo de comunicação, não a existência da sonda) e da legenda PIO.08.04. Em 36 s abrem as duas ações finais e `Completed` dispara **uma vez**: dali em diante o capítulo está concluído e a tela fica até o jogador escolher "Voltar ao arquivo" (foco inicial) ou "Repetir capítulo" (setas, E toque ou segurado por 0,5 s, Enter ou clique; só a primeira escolha vale). **Nada inicia outro capítulo sozinho e nenhum relógio corre nessa tela**: 10 minutos parado não mudam nada (teste). O prompt de E sempre fala do botão em foco.

**Uso pelo controlador (PIO-014):** `farewell.Initialize(sequenceDirector, interactionService, inputRouter, sessionClock, resolverDeTexto)`, `Completed += () => chapterDirector.CompleteStep("PIO.Farewell")` (a tela de conclusão pertence ao estado de aplicação Completion, que não conta como tempo ativo), `ReturnToArchiveRequested += voltarAoDossie`, `ReplayRequested += reiniciarCapitulo`, `Begin(skippable)`; `Cancel()` reinicia ou fecha o memorial sem levantar escolha. `SetReducedMotion(true)` para a configuração global.

**Texto novo (PIO-001).** `PIO.UI.aldebaran` ("Direção de Aldebaran" / "Direction of Aldebaran", `derived`: a R7 precisa revisar). `PIO.UI.ending`, `.return`, `.replay` e as legendas PIO.08.* já existiam e já estavam `approved`.

**Limitações.** O match cut depende da silhueta da antena da PIO-011 (`DishSocket`) e do alinhamento dos rigs, que não foram verificados visualmente em Play Mode: o que os testes provam é a estrutura (rigs, durações, marcos, ordem). O gráfico opcional de atlas (4 s) não foi feito. Os textos aparecem como chaves até a UI-004. Frames pretos intencionais (30–38 s) e a ausência de caixa de legenda vazia dependem do apresentador global (UI-004). Play Mode e navegador não foram executados.

**Reconstruir:** Abandoned Space Apps → CORE-006 → Reconstruir todas as cenas, ou `-executeMethod Abandoned.SpaceApps.EditorTools.SpaceAppsSceneBuilder.BuildFromCommandLine -builders pioneer10.stage.farewell`.

## Rota completa e reset (PIO-014)

| Arquivo | Papel |
| --- | --- |
| `Runtime/PioneerChapterController.cs` | Liga os oito estados ao contrato global (`IChapterDirector`): entra no estado quando a etapa é anunciada, inicia a atividade, entrega a etapa quando a ATIVIDADE conclui, cancela e zera tudo na troca, no reinício e na destruição da cena, e mede o tempo ativo por estado |
| `Data/PioneerChapter.asset` | `ChapterDefinition` `pioneer10`: cena `Pioneer10`, 375 s de referência, oito etapas em fila (`PIO.Launch` → … → `PIO.Farewell`, a última terminal), textos do dossiê `slot.pioneer10.*` |
| `Sequences/PIO_JupiterApproach.asset` | 27 s de cinema (PIO03.A–C) para o estado 3, **que nenhuma outra tarefa montava**; legendas PIO.03.01–03 |
| `Editor/PioneerChapterBuilder.cs` | Builder CORE-006 `pioneer10.zchapter` (raiz `[R5] Pioneer Chapter`; o `z` o põe depois do palco e de todas as atividades): gera o asset do capítulo e a sequência do Júpiter e liga o controlador, a narração (PIO-013) e um `SequenceDirector` |
| `Tests/PioneerFlowTests.cs` | 19 testes EditMode com os serviços REAIS do Core |

**Rota.** Dossiê → `ChapterDirector.Begin(PioneerChapter)` → cena carregada → R1 chama `controller.Initialize(...)` **antes** da primeira etapa → Launch → Belt → JupiterApproach → Imaging → Plaque → Outbound → Listening → Farewell → `CompleteStep("PIO.Farewell")` (terminal: `FinishChapter`, uma vez, estado Completion) → memorial → **Voltar ao arquivo** (`ReturnToDossier`) ou **Repetir** (`RestartChapter`, recarrega a cena). A ordem vem da tabela de etapas do asset, nunca de um enum. Nenhum cronômetro conclui nada: cada etapa espera a conclusão da atividade, que espera o jogador (E mantido, toque, ajuda). A conclusão do capítulo acontece com o memorial aberto (36 s), então a tela de conclusão pertence ao estado Completion e não conta como tempo ativo.

**Uma fonte de duração e eventos.** Cada estado tem uma sequência no `SequenceDirector` (relógio único, congelado na pausa): câmera, atores, legendas e término vêm dela. O controlador só soma o **tempo ativo** do `SessionClock` por estado (`LastReport`); a duração medida é do **operador** e `PioneerRunReport.CountsAsHumanResult` é sempre falso: resultado humano só vem das sessões da QA-004.

**Reset.** `Teardown()` (chamado ao reinicializar, ao mudar de estado e no `OnDestroy`) cancela a atividade do estado, a narração (legenda, música, efeitos), a recuperação de radiação, solta o contexto de entrada e todos os ouvintes. O E é consumido uma vez por atividade (rearme do Core). Os testes reiniciam em cada um dos oito estados e confirmam: estado volta ao Launch, relatório vazio, nenhuma atividade antiga rodando, nenhum lease antigo, **o mesmo número de GameObjects** e um único `StepEntered` depois do reinício; reiniciar duas vezes seguidas não duplica a conclusão de nenhum estado.

**Uso pela integração (R1/INT-001).**
- Depois de carregar a cena `Pioneer10` e antes da primeira etapa (no `ChapterStarted`): `controller.Initialize(chapterDirector, sessionClock, inputRouter, interactionService, audioPort, idioma, retornoAoArquivo)`. `audioPort` pode ser nulo (as legendas continuam); sem `retornoAoArquivo`, o controlador usa `ChapterDirector.ReturnToDossier()` quando o diretor é o real e sempre levanta `ReturnToArchiveRequested`.
- Pausa global: o controlador escuta `ChapterDirector.PausedChanged` sozinho; para outro diretor, chame `controller.SetPaused(bool)`. Depois do primeiro clique que libera o áudio: `controller.Resync()`.
- **Ligação ao dossiê (NÃO feita aqui, é do shell da R2):** o slot `pioneer10` de `Core/ChapterCatalog.asset` e a tabela `Slots` de `UI/Dossier/Editor/DossierAssetsBuilder.cs` ainda têm `chapter` vazio. Para a pasta abrir o capítulo: apontar `chapter` para `Data/PioneerChapter.asset` (mesmo padrão do `MarsChapter.asset`); os textos já existem em `DossierTexts.json`, então `chapterTexts` pode ficar vazio.
- **Build Settings (R1):** a cena `Assets/_Game/Chapters/Pioneer10/Scenes/Pioneer10.unity` precisa entrar nas Build Settings; nenhuma cena de prévia deve entrar (um teste confere).

**Prévia fora do pacote.** `Art/Probe/Preview/ProbePreview.unity` e as ferramentas de captura são só de desenvolvimento: a cena do capítulo não as referencia e nenhuma entra nas Build Settings (testes).

**Estado 3 (PIO.JupiterApproach).** O roteiro tem o estado (27 s, PIO.03.01–03, cartão "NOV–DEZ 1973"), mas **nenhuma tarefa do backlog o montava**. Esta tarefa entrega o mínimo para a rota fechar: a sequência `PIO_JupiterApproach` (a abertura do campo de visão de 70° a 44°, plano aberto e corte ao fotopolarímetro) nos anchors da PIO-003, sem interação. **Enquadramento conferido no 3D depois da integração** (quadros em `QA/PIO-014/framing/`, renderizados por uma ferramenta de desenvolvimento local que **não** faz parte do repositório): o close `PIO03.Instrument` saía do quadro porque a sonda continuava girando 24°/s durante os 8 s (corrigido com `SpinScale = 0` no anchor `PIO.JupiterApproach.Instrument`, como o da placa; teste novo). **Limite que continua:** a "abertura" 70°→44° faz Júpiter crescer só ~1,7× (raio de ~125 a ~210 px); ele **não** nasce como um ponto, e o plano aberto mostra a sonda e Júpiter o tempo todo. O cartão de data e a animação editorial Terra→sonda→Terra não foram feitos, e o texto/legenda dependem da UI-004.

**Limitações.** A rota foi exercitada por um operador automatizado no EditMode (teclado de verdade, serviços reais do Core, `SceneManager` substituído por um carregador instantâneo); **Play Mode, a cena carregada de verdade pelo `SceneManager`, o navegador e a Build Settings não foram exercitados**. O operador usa o mesmo padrão de E em todos os estados (mantido 3,4 s, solto, toque): isso prova que cada atividade conclui sem penalidade, não que uma pessoa a conclui em 375 s. Evidências (galeria) não são registradas pelo controlador. O áudio real (clipes) continua pendente (AUD-002 liga só o `AudioDirector`).

**Reconstruir:** Abandoned Space Apps → CORE-006 → Reconstruir todas as cenas, ou `-executeMethod Abandoned.SpaceApps.EditorTools.SpaceAppsSceneBuilder.BuildFromCommandLine -builders pioneer10.zchapter`.

## Roteiro v2, fase 1 (PIO-018)

Recebido em 15/11/2026 um roteiro de 9 cenas e legendas em inglês britânico (cópias em `Docs/Chapters/PioneerV2/`). Esta fase muda o texto e a estrutura, não as interações.

| Arquivo | Papel |
| --- | --- |
| `Data/Localization/Pioneer10.pt-BR.json` e `.en.json` | Todas as legendas narrativas reescritas (120 chaves por idioma, status `derived`), na regra de 16 caracteres por segundo do projeto; `Assets/_Game/Shared/Localization/Pioneer10Texts.json` reexportado |
| `Sequences/PIO_Closest.asset` | **Estado novo `PIO.Closest`** (cena 5, "O mais perto possível"): 40 s só de cinema, cinco planos nos anchors do palco, legendas `PIO.09.01–07` |
| `Runtime/PioneerChapterController.cs` | Nove estados; os estados só de cinema (`PIO.JupiterApproach` e `PIO.Closest`) usam o mesmo caminho e concluem pelo fim da sequência |
| `Editor/PioneerStageBuilder.cs` | Anchor `PIO.Closest` (Júpiter a ~130 m do centro, raio 80 m, escala de composição) |
| `Docs/Chapters/PioneerV2/FASES.md` | Mapeamento das 9 cenas, trocas de frase (fatos seguros) e as fases seguintes |

Total nominal: **415 s (6:55)**, 421 s com o alongamento de leitura de dois cues. As chaves do estado novo são `PIO.09.xx` (as dos demais estados não mudaram); o mapa de áudio ganhou `PIO.Closest` com a música `MUS_PIO_CUR` continuando, sem reiniciar.

**Limitações da fase 1.** As cenas de cinema que o roteiro descreve (Lua e Marte, dolly zoom do cinturão, luas de Júpiter em 3D, linha da trajetória, medidor de radiação, montagem dos anos, as seis estrelas) **não existem ainda**: as legendas novas acompanham as imagens que já havia, e o `PIO.Closest` usa só Júpiter, o fotopolarímetro e a sonda saindo. Cinturão, placa e cartão de energia ainda têm interação, e o roteiro as quer como cinema; a foto é de oito faixas, e não de três passos. As legendas foram escritas com as formulações do registro de fontes, e algumas frases do roteiro foram trocadas (lista em `FASES.md`); precisam de revisão da R7, inclusive o inglês. Textos de arquivo (cartões e HUD) não foram reescritos.

## Roteiro v2, fase 2 (PIO-019): só duas interações

O roteiro v2 tem só duas interações (a foto e a sintonia de rádio); o lançamento, o cinturão, a placa e os anos viram cinema.

| Estado | Como corre agora |
| --- | --- |
| `PIO.Launch` | `PioneerLaunchActivity.CinemaMode`: no marco dos geradores (32,5 s) a demonstração roda sozinha por `HoldSeconds` (3 s), sem lease de painel, sem candidato de E e sem pausar a sequência |
| `PIO.Belt` | O controlador toca só a sequência `PIO_Belt` (`BeginCinema`), como o estado de Júpiter: sem a arena de poeira e sem painel; a atividade `BeltInvestigation` continua no código e nos testes, mas o capítulo não a inicia |
| `PIO.Plaque` | `PioneerPlaqueActivity.CinemaMode`: as cinco peças encaixam sozinhas (a primeira 1,5 s depois do início do encaixe, as demais a cada 5,5 s), cada uma com a legenda de 4 s; o painel esconde as instruções de escolher peça e de E |
| `PIO.Outbound` | `OutboundAtlas.CinemaMode`: a priorização da comunicação corre sozinha em 3 s; o painel esconde o prompt de E |

O padrão das atividades é a versão interativa (os testes de cada uma seguem iguais); o controlador liga `CinemaMode` ao inicializar. **Limitações:** as durações dos estados não mudaram (a retemporização para os tempos do roteiro fica para a fase 4); no cinturão a fantasia de 15 s agora é só a legenda "Imagine o medo..." sobre o plano 3D da sonda, sem a arena nem o selo; a placa e o cartão de energia ainda são os painéis 2D da atividade, não cinema 3D.

## Roteiro v2, fase 3 (PIO-020): as duas interações, simplificadas

| Interação | Como corre agora |
| --- | --- |
| **Foto** (`PIO.Imaging`) | `PioneerImaging.ThreeStepMode`: passo 1/3 segurar E gira a sonda e as quatro faixas vermelhas entram (2 s de E por faixa, 8 s por cor); passo 2/3, um toque de E troca para azul e segurar E gira de novo (quatro faixas azuis); passo 3/3, um toque de E junta as duas imagens (8/8, `AllStripsCaptured`). Sem janela de fase e sem passagem perdida; o E que já estava mantido ao mudar de passo não vale como toque (é preciso soltar). Sem E por 12 s a ajuda gira sozinha, sem punição. **Não há radiação nem recuperação nesta cena** (a radiação é da cena 5 e só aparece nas legendas) |
| **Escuta** (`PIO.Listening`) | `PioneerListening.SingleRound`: só o primeiro contato (3 MAR 2002) pede o botão e o E; depois dele os controles somem (sem lease, sem candidato); o segundo, o terceiro e a quarta tentativa sem sinal chegam sozinhos nos marcos da sequência, com as mesmas legendas e eventos de estado |

O painel da foto ganhou o rótulo "Passo n/3" e prompts por passo (`PIO.UI.step`, `spin`, `switch_blue`, `combine`, PT-BR e EN) e esconde o cursor de fase na versão nova; as quatro legendas 04.02–04.05 foram reescritas para a história de três passos. O padrão das duas atividades continua sendo a versão antiga (os testes de cada uma seguem iguais) e o controlador liga os modos novos.

**Limitações.** As durações não mudaram (74 s e 52 s; o roteiro quer 45 s e 40 s, ver `Docs/Chapters/PioneerV2/FASES.md`). A atividade de recuperação (`ImagingRecovery`) continua no código e nos testes, mas o capítulo não a inicia. O painel mostra oito faixas de imagem (quatro por cor), não duas imagens inteiras que se sobrepõem: a "montagem" de Júpiter é a existente. Quadros do painel renderizados com as chaves no lugar do texto (a captura não resolve idioma); Play Mode com a UI e teste humano não foram exercitados.

## Roteiro v2, fase 4, parte 1 (PIO-021): o cinturão como cinema

O estado `PIO.Belt` toca a sequência própria `PIO_BeltCinema` (54 s; `PIO_Belt`, de 58 s, segue como a da atividade interativa e dos testes dela).

| Tempo | Plano | O que mostra |
| --- | --- | --- |
| 0–8 s | `PIO02.Mars` (anchor `PIO.Belt`) | A sonda cruza a órbita de Marte; só a data "15 JUL 1972" |
| 8–19 s | `PIO02.Fear` (anchor `PIO.Belt.Field`) | Teleobjetiva de 10°: a parede de pedras compacta atrás da sonda |
| 19–34 s | `PIO02.Days` (mesmo anchor) | Dolly zoom: o campo de visão abre de 10° a 30° e a câmera se aproxima (a distância vezes a tangente da metade do campo é constante, então a sonda não muda de tamanho) enquanto a parede se abre; o contador "Dia n / 215" acelera até o Dia 140 |
| 34–44 s | `PIO02.Nike` (anchor `PIO.Belt.Nike`) | Corte para um céu vazio e uma pedra solitária; rótulo "307 Nike · o asteroide conhecido mais próximo" apontando para ela; evento `PIO.Belt.Reality` baixa a música; contador parado em 2 DEZ 1972 |
| 44–54 s | `PIO02.Exit` (anchor `PIO.Belt`) | A sonda sai para o escuro limpo; o contador chega ao Dia 215 (15 FEV 1973) |

- `PioneerBeltCounter` lê o relógio da sequência e desenha no `BeltPanelView` (`counter-root`): data só nos primeiros 6 s, depois "Dia n / 215" e a data; o texto vem das chaves `PIO.UI.belt_day`, `belt_months` e `belt_nearest`. Os dias são datas reais nos extremos; a curva entre eles é estilizada.
- O campo de pedras (`BeltField`, 1400 pedras, uma malha e uma chamada de desenho, 20 faces por pedra) e a pedra solitária (`BeltNike`) só aparecem nos anchors com `showBeltField` / `showBeltNike`. O campo é gerado no cone da câmera da teleobjetiva, com tamanho angular parecido em qualquer profundidade; o gerador é determinístico pela semente.
- A duração nominal do estado cai para 54 s e a do capítulo para 411 s (teto de leitura 417 s).

**Limitações.** O campo é de baixa resolução, sem textura. A teleobjetiva é de 10° porque o contrato de planos aceita no mínimo 10° (o roteiro pedia uma lente mais fechada; mudar o contrato exige acordo com a R1). O contador e o rótulo foram verificados por testes de lógica e de layout, não por quadros de UI nem no Play Mode. A Lua e Marte (cena 1) ainda não estão no primeiro plano. Evidência em `QA/PIO-021/README.md`.

## Roteiro v2, fase 4, parte 2 (PIO-022): Marte e a Lua em 3D

- **Marte** (`PioneerPresentation.Mars`, anchor `PIO.Belt.Mars`, `showMars`): no plano de abertura do cinturão (`PIO02.Mars`, 8 s) uma esfera de 85 m de raio a 880 m da câmera, 9° à direita e 3° abaixo do centro do quadro, iluminada pela luz `PIO.Light.Cruise`; a sonda segue no centro e não é coberta. Só aparece nesse anchor.
- **Superfícies procedurais** (`PioneerStageArt.MoonSurface` e `MarsSurface`, 1024×512, determinísticas pela semente, sem asset externo): a Lua tem terras claras, mares escuros e crateras com borda clara; Marte é ocre-alaranjado com regiões escuras e calotas polares claras. Texturas em `Art/Stage/Textures/PIO_MarsSurface.png` e `Art/Launch/Textures/PIO_MoonSurface.png`; a Lua substitui o disco cinza do gráfico Terra–Lua (`PIO01.C`).
- **Limitações.** As superfícies são estilizadas, não mapas reais. O contador "11 horas depois" e um sobrevoo da Lua em 3D (cena 1) não foram feitos: exigem alongar o lançamento e mexem nos marcos de tempo dos testes dele, então ficam para a retemporização. Evidência em `QA/PIO-022/README.md`.

## Roteiro v2, fase 4, parte 3 (PIO-023): o rei dos planetas

- `PioneerJupiterReveal` (novo; `JupiterReveal` no capítulo) acompanha o relógio de `PIO_JupiterApproach`: nos primeiros 9 s (`PIO03.Grow`) a escala de Júpiter vai de 0,015 a 1 em crescimento exponencial (o olho vê uma taxa constante) enquanto o campo de visão abre de 70° a 36°; a câmera continua perto da sonda, que fica em silhueta no primeiro plano. Fora da sequência, `Hide` devolve a escala do palco (as cenas 4 e 5 usam o mesmo Júpiter).
- A Terra de comparação (`PioneerPresentation.Earth`, 1/11,2 do diâmetro de Júpiter, razão real dos diâmetros equatoriais, textura procedural `PioneerStageArt.EarthSurface`) entra por fora do quadro à esquerda em 9,5 s, anda até o ponto previsto em 14 s e sai no corte para o fotopolarímetro (19 s). A posição vem de `PioneerStageBuilder.ViewportPoint` (um ponto do quadro, não coordenadas à mão).
- O campo de visão do plano aberto passa de 44° a 36° (`ApproachOpenFov`) para Júpiter ocupar 70% ou mais da altura do quadro, como o roteiro pede.
- **Limitações.** A Terra é estilizada. O crescimento é do próprio planeta, não uma viagem por distâncias reais. Faltam a Grande Mancha Vermelha cruzando, o brilho no limbo e o terminador marcado; a duração segue 27 s (o roteiro pede 30 s). Evidência em `QA/PIO-023/README.md`.

## Roteiro v2, fase 4, parte 4 (PIO-024): o mais perto possível

- **HUD de cinema** (`UI/CinemaHud.uxml/.uss`, `CinemaHudView`, builder `pioneer10.stage.hud`, prefab `UI/CinemaHud.prefab`, raiz `[R5] Pioneer Cinema HUD`): painel UI Toolkit com o tema da UI-001, sem frases literais (chaves `PIO.UI.rad_title`, `speed_label`, `dist_moon`, `dist_clouds`), reaproveitado pelas partes seguintes. Capturado por `Editor/PioneerHudCapture.cs` (`QA/PIO-024/hud/`).
- `PioneerClosestHud` (driver preso ao relógio de `PIO_Closest`): medidor de radiação que sobe de 6 a 15 s (curva estilizada) com estática (ruído tileável que treme a 24 quadros por segundo, opacidade de 0,05 a 0,4) até 17 s; de 24 a 29 s o texto "Mais de 120.000 km/h" e as duas barras de distância (Terra–Lua 384.400 km; sonda–nuvens 130.000 km, 1/2,96 da primeira).
- **Luas** (`PioneerPresentation.Moons`, anchors `PIO.Closest.Ganymede/Callisto/Europa`, `showMoons`): três esferas com texturas procedurais estilizadas e raios na razão real entre elas (4,4; 4,0 e 2,6 m, maiores que a razão com Júpiter para aparecerem); cada uma cruza o quadro por 2 s (os 6 s da legenda 09.01). O plano de chegada antigo saiu; a duração segue 40 s.
- **Trajetória** (`PioneerPresentation.Trajectory`, anchor `PIO.Closest.Bend`, `showTrajectory`): faixa de luz que chega a 130 m do centro de Júpiter, curva ~38° na direção dele e segue, vista por uma câmera 620 m acima do plano da linha. Linha de composição, não cálculo orbital.
- **Limitações.** Superfícies estilizadas; faltam as fotos reais das luas, Io com o "X", comandos falsos num painel, o mergulho rente às nuvens, linhas de campo gravitacional, a seta de Júpiter ao redor do Sol e o velocímetro subindo. Evidência em `QA/PIO-024/README.md`.

## Roteiro v2, fase 4, parte 5 (PIO-025): os anos

- `PioneerYearsHud` (driver preso ao relógio de `PIO_Outbound`) desenha no HUD de cinema (`CinemaHudView.SetYears`, seção "years" de `UI/CinemaHud.uxml`): o **ano em destaque** de 1974 a 1998, parado nos marcos reais (Saturno 1976, Urano 1979, Netuno 13 jun 1983, fim do acompanhamento de rotina 31 mar 1997, Voyager 1 em 1998); a **linha do tempo** (1973–1998, anos de marco mais altos e com número); cinco **ícones de instrumentos** (câmera, radiação, plasma, meteoroides, campo magnético) que apagam em turnos conforme a energia cai; e o **medidor de energia dos RTG** com só as duas marcas reais, 155 W no lançamento e ~140 W em Júpiter, e uma queda exponencial ESTILIZADA depois (o painel diz "curva estilizada").
- `OutboundAtlas.YearsMode` (ligado pelo controlador) esconde o painel do atlas para a sonda em 3D aparecer; a lógica de fases, hold e conclusão não muda, e o padrão (falso) segue mostrando o atlas.
- Dez textos novos (`PIO.UI.past_uranus`, `end_tracking`, `years_energy`, `years_launch`, `years_jupiter`, `inst_*`), 141 chaves.
- **Limitações.** Os números do roteiro foram conferidos na web: 155 W e ~140 W aparecem em fontes; "~65 W em 2001" e as datas de desligamento (1980 a 1995) não foram confirmados (as fontes dão fevereiro de 1985 para a divisão de energia e 30 set 1995 para o fim da ciência de rotina), por isso o painel não os mostra; pendente de R7. Ícones geométricos, sem som de clique; o ano vai a 1998 (o roteiro vai a 2001). Evidência em `QA/PIO-025/README.md`.

## Roteiro v2, fase 4, parte 6 (PIO-026): as seis primeiras vezes

- `PioneerFirstsHud` (driver preso ao relógio de `PIO_Farewell`; `CinemaHudView.SetFirsts`, seção "firsts" de `UI/CinemaHud.uxml`): no plano `PIO08.Receding` (20–30 s) seis estrelas acendem ao redor da sonda, uma a cada 1,6 s, cada uma com um rótulo curto (`PIO.UI.first_1` a `first_6`); somem quando entra o memorial. As estrelas ficam numa elipse em volta do centro (a primeira no alto à esquerda, em sentido horário) e os rótulos, com no máximo 240 px, saem para fora dela.
- **As seis** (cada uma com fonte): primeira missão aos planetas exteriores; primeira projetada para ir além de Marte; primeira a cruzar o cinturão de asteroides; primeira a sobrevoar Júpiter; primeira em rota para sair do Sistema Solar; primeira além das órbitas dos planetas · 1983. O "primeira a usar energia totalmente nuclear" do roteiro ficou de fora (a NASA só diz que usava RTGs).
- Seis textos novos, 147 chaves. **Limitações:** estrelas simples (discos luminosos); sem o plano final com a placa refletindo Aldebaran e sem a constelação de Touro; os rótulos fazem o papel das legendas curtas. Evidência em `QA/PIO-026/README.md`.

## Roteiro v2, fase 5 (PIO-027): barras de cinema e cor por grupo de luz

- **Barras de cinema** (`PioneerLetterbox`, `CinemaHudView.SetLetterbox`, elementos `bar-top` e `bar-bottom` de `UI/CinemaHud.uxml`, desenhados abaixo de todo o resto do HUD): duas barras pretas de 7% da altura cada deslizam em 0,8 s nos sete estados que são só cinema (`PIO.Launch`, `Belt`, `JupiterApproach`, `Closest`, `Plaque`, `Outbound` e `Farewell`) e saem quando o estado acaba. Ficam recolhidas nas duas interações (`PIO.Imaging` e `PIO.Listening`) e no memorial da despedida (a partir de 30 s), que tem botões. As legendas (ordem 20) ficam acima das barras (ordem 12), então o texto cai sobre o preto. O avanço usa o tempo ativo do capítulo.
- **Cor por grupo de luz** (`PioneerStageBuilder.LightGroups`): o novo `PIO.Light.EarthWarm` (luz principal âmbar, 0,9 de intensidade) substitui o frio `PIO.Light.Night` nos planos da Terra (plataforma de lançamento e antena da Deep Space Network); no espaço, `Cruise` e `FarSun` ficaram mais frios (branco-azulado) e `FarewellRim` e `ColdRim` já eram frios. Júpiter e a placa seguem quentes (iluminados pelo Sol).
- **Limitações.** Deslize linear, sem easing; sem correção de cor por cena nem pós-processamento (perfil Web leve); as cenas 4 e 8 ficam sem barras inclusive nos trechos de cinema; os quadros do cinturão interativo em `Docs/Art/Pioneer10_Belt` mantêm a luz antiga. Evidência em `QA/PIO-027/README.md`.

## Qualidade visual do lote de 15/11/2026 (PIO-031): céu, Aldebaran e a Terra da NASA

- **Céu** (`PioneerStageArt.Starfield` e `BrightStars`): estrelas brilhantes redondas (discos de 10 lados); Via Láctea com poeira e núcleo mais quente; 9.000 estrelas fracas mais nítidas, quase metade agrupada perto da faixa; base mais escura.
- **Aldebaran** (`AldebaranMarker` com `Core` e `Halo`): miolo liso de 14 m e halo aditivo de 110 m (`PioneerStageArt.GlowTexture`, `GlowQuad`; material aditivo `PIO_AldebaranHalo`). Novo anchor `PIO.Farewell.Away` (a Pioneer a 16 m, Aldebaran no alto à esquerda: `PioneerStageBuilder.FarewellAwayForward` resolve a direção de olhar) usado pelos planos de afastamento e do memorial; o plano aberto da despedida (`PIO.Farewell`) passou de 9,6 m para ~16 m.
- **Terra da NASA** (`PioneerStageBuilder.EarthTexture`, mapa Blue Marble 2048×1024 de `ArtSource/Pioneer10/PIO-031/`): na Terra do gráfico Terra–Lua do lançamento (girada para mostrar as Américas) e na Terra de comparação com Júpiter; substitui a textura procedural.
- **Tempo:** o teto de aprovação do capítulo nos dados passa de 420 s para 600 s, sem contar o tempo que a pessoa leva nas duas interações.
- Evidência em `QA/PIO-031/README.md`.
