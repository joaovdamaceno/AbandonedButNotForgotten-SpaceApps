# HUD compartilhado — UI-003 (R2)

Objetivo principal, âncora e seta de borda, prompt E com progresso, comando em trânsito (Surveyor) e energia. Apresenta o que o `IInteractionService` informa; **não recalcula alcance**.

Os três arquivos da tarefa ficam em `Assets/_Game/UI/`: `MissionHUD.cs`, `InteractionPrompt.cs`, `ObjectiveIndicator.cs`. O resto (lógica pura, UXML, USS, textos, prefab, testes) fica nesta pasta.

## Instalação (R1, INT-001)

1. Instancie `MissionHUD.prefab` (na cena do capítulo ou no aplicativo). Mantenha o GameObject ativo.
2. Depois de criar os serviços do aplicativo:

   ```csharp
   missionHud.Initialize(interactionService, inputRouter, chapterDirector, settingsStore, cameraDirector);
   ```

   Só `interactionService` é obrigatório. Sem `inputRouter` o HUD assume gameplay; sem `chapterDirector`, capítulo em andamento; sem `settingsStore`, usa as preferências gravadas; sem `cameraDirector`, use `missionHud.SetCamera(camera)` (sem câmera não há âncora nem seta).
3. Cada capítulo alimenta o HUD pela interface `IMissionHud` e registra a sua tabela de textos:

   ```csharp
   hud.AddTextTable(marsSubtitlesJson);          // verbo, objeto, objetivo, nome do alvo (formato {"texts":[{key,pt,en}]})
   hud.SetReference(roverTransform);             // mede a distância até âncoras que não são o candidato ativo
   hud.SetObjective(new HudObjective { Id = "mars.objective.mckittrick", StampKey = "MAR.T05", LabelKey = "MAR.OBJ.mckittrick",
       TargetNameKey = "MAR.TARGET.mckittrick", Anchor = rockAnchor, CandidateId = "mars.grind", Step = 2, StepCount = 3,
       AccentClass = "sa-chapter--mars" });
   hud.SetEnergy(0.82f);                         // só quando a energia ajuda a entender a ação; ClearEnergy() esconde
   ```
4. Ao descarregar o capítulo (`ChapterUnloading`) o HUD zera objetivo, energia, comando, progresso e feedback: nada atravessa para o capítulo seguinte.
5. Legenda (UI-004) e pausa (UI-006) **não** criam outro documento: entram em `missionHud.BottomColumn` (logo abaixo do prompt) e `missionHud.RightColumn` (acima do medidor de energia), então não se sobrepõem por construção.

O candidato do `InteractionService` precisa de `VerbKey` e `ObjectKey` resolvíveis na tabela de textos (chave sem texto aparece como a própria chave, visível ao QA). `Anchor` é opcional: com ele, o candidato disponível ganha a âncora no mundo.

## Contrato

| O capítulo faz | O HUD mostra |
| --- | --- |
| `interactions.SetCandidate(...)` | Prompt E `verbo + objeto`, **somente** quando `CanBeginHold(id)` é verdadeiro |
| `HoldStarted` / `HoldCanceled` / `HoldCompleted` | Segurando (tecla cheia) · Pausado (âmbar, trabalho preservado) · Concluído (verde ✓), três formas distintas |
| E mantido depois de concluir | Rearme: "Solte E para começar"; a próxima atividade nunca aparece como E disponível |
| `ReportProgress(id, fração)` | Progresso físico do instrumento quando o hold é retomado só com o tempo restante; `0` = nada preservado |
| `SetCommand(new HudCommandState(stage, progresso, segundos))` | Estado de comando em trânsito no lugar do E (Preparado · Enviado · Em trânsito · Executando · Confirmado); `ClearCommand()` devolve o E |
| `SetObjective(...)` | Placa do topo; âncora no mundo se o alvo está no retângulo interno, seta de borda com nome e distância se não |

Mapeamento sugerido para a Surveyor: `CommandTransitStatus.InTransit` → `HudCommandStage.InTransit`, com `progress = SurveyorCommandDelay.Progress` e `segundos = RemainingSeconds`.

## Regras de apresentação

- **Um alvo de orientação por vez**: a âncora do candidato disponível ou, se não houver, a do objetivo. A orientação nunca duplica.
- **Distância única**: se o candidato ativo é o `CandidateId` do objetivo, a distância é a do candidato (a mesma que decide o E); senão é medida da referência até a âncora.
- **Sem sobreposição**: topo à esquerda objetivo (e distância); topo à direita energia; base prompt e progresso. A orientação fica no retângulo interno (24% do topo, 36% da base, 4% das laterais), então nunca cai sobre essas placas nem sobre prompt + legenda de duas linhas.
- **Pausa**: some a orientação, o prompt e a energia; o objetivo continua legível. **Cinemática e painéis**: nada. **Fora do capítulo em andamento**: nada.
- **Nunca captura o mouse**: todo elemento é `picking-mode="Ignore"`.
- **Sem alocação por quadro**: textos só são refeitos quando estado, progresso, distância (arredondada), idioma ou tabela mudam; idioma e modo do E vêm de evento do `SettingsStore`.

## Arquivos

| Arquivo | Papel |
| --- | --- |
| `../MissionHUD.cs` | Host: liga serviço, entrada, diretor, preferências e câmera; `IMissionHud`; visibilidade por contexto; energia |
| `../InteractionPrompt.cs` | Apresentador do prompt E (eventos do serviço → `PromptView`) |
| `../ObjectiveIndicator.cs` | Placa do objetivo, âncora no mundo e seta de borda |
| `HudLogic.cs` | Sem Unity: `HudVisibility`, `HudFormat` (distância, %, segundos, PT/EN), `EdgeCueMath` |
| `PromptModel.cs` | Sem Unity: máquina de estados do prompt e `PromptViewBuilder` |
| `ObjectiveViews.cs`, `HudKeys.cs`, `HudContentCheck.cs` | Visões do objetivo/energia, chaves de texto e validação de conteúdo |
| `HudObjective.cs` | `HudObjective` (validado) e a interface `IMissionHud` |
| `MissionHud.uxml`, `MissionHud.uss`, `HudTexts.json` | Estrutura, estilos (só tokens da UI-001) e textos PT/EN |
| `MissionHUD.prefab` | `UIDocument` + `ResponsiveLayoutClass` + `MissionHUD`, sem câmera nem áudio |
| `Editor/MissionHudAssetsBuilder.cs` | Menu para recriar o prefab e validar textos/UXML (idempotente) |
| `Tests/` | `HudLogicTests` (sem Unity), `HudIntegrationTests` (serviço e roteador reais, UXML real), `HudAssetsTests`, `HudPrefabTests` |

## O que ficou de fora de propósito

- Placa de **Pausa** e **legenda**: UI-006 e UI-004 (as colunas para elas existem).
- **Toast de evidência** e escolha de fotos: UI-007.
- Texto de **tutorial** e assistência: UI-005 (consome o prompt e o objetivo).
- Fonte dos números de **energia**: o capítulo informa; o HUD só mostra. Nenhum valor é inventado aqui.

## Limitações conhecidas

- Sem verificação visual: aparência no Editor e no navegador, contraste renderizado e a seta girando ainda não foram vistos por uma pessoa. O cabimento dos textos foi **medido** com as métricas das fontes (pior caso: 2 linhas no prompt), não renderizado.
- Textos novos de interface e traduções EN são propostas sem revisão editorial.
- Capítulos ainda não alimentam o HUD: nenhum chama `SetObjective`/`SetEnergy`/`SetCommand` hoje (isso é integração de cada capítulo e de INT-001).
