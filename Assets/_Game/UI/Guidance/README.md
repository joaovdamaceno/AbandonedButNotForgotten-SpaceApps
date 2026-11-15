# Tutorial por ações e assistência (UI-005 · R2)

Orientação para quem não joga: duas instruções (mover, interagir) que só somem por uma ação **observada**, e uma ajuda que sobe quando
não há progresso. Nada é teleportado nem resolvido por ela.

| Arquivo | O que faz |
| --- | --- |
| `../GuidanceController.cs` | Observa movimento (`InputRouter.Move` ou uma medida em metros do capítulo), `HoldCompleted` do `IInteractionService` e H. Relógio de ociosidade em tempo **ativo**. Sem entrada capturada. |
| `../TutorialPresenter.cs` | Desenha a placa "Como jogar" na coluna da base do `MissionHUD` (acima do prompt E). Só por eventos; nenhum elemento captura o mouse. |
| `GuidanceLogic.cs` | Sem Unity: `GuidanceTimeline` (12 s / 30 s), `ActionMeter`, `GuidanceChecklist`, `GuidanceViewBuilder` (texto PT/EN). |
| `GuidanceTask.cs` | `GuidanceOptions` (o que o capítulo oferece: H, corredor, candidato de E) e `GuidanceTask` (assistência de placa, sintonia e varredura). |
| `GuidanceKeys.cs`, `GuidanceTexts.json`, `GuidanceContentCheck.cs` | Textos PT/EN; nenhuma frase em C#. O teste confere que toda chave existe nos dois idiomas. |
| `Guidance.uss` | Só estende os tokens da UI-001. |
| `Guidance.prefab` | `GuidanceController` + `TutorialPresenter` já ligados aos textos e aos estilos. Sem câmera, áudio ou `UIDocument`. |
| `Editor/GuidanceAssetsBuilder.cs` | Menu *Abandoned Space Apps > UI-005*: reconstrói o prefab (idempotente) e valida os textos. |
| `Tests/` | EditMode: lógica, controlador com `InputRouter`/`InteractionService` reais, placa e prefab. |

## Instalação (R1, INT-001)

1. Instancie `Guidance.prefab` junto do `MissionHUD` (mesma camada do aplicativo).
2. Depois de ligar o HUD:

   ```csharp
   guidance.Initialize(inputRouter, interactionService, sessionClock, chapterDirector, settingsStore);
   presenter.Initialize(guidance, missionHud, inputRouter, chapterDirector, settingsStore);
   guidance.SetMovementProbe(() => rover.DrivenMeters);   // opcional: metros efetivos (colisão não conta)
   ```
3. O capítulo começa a orientação na etapa certa e a encerra ao sair dela:

   ```csharp
   var options = new GuidanceOptions { InteractCandidateId = "mars.tutorial.inspect", HelpAvailable = true, CorridorAvailable = false };
   if (guidance.ShouldAutoStart("mars.tutorial")) guidance.BeginTutorial("mars.tutorial", options);
   // ...
   guidance.End();
   ```
4. Atividades com controles próprios usam a assistência e avisam o progresso e a conclusão **do contrato do capítulo**:

   ```csharp
   var task = GuidanceTask.Plaque(new GuidanceOptions { HelpAvailable = false });
   guidance.BeginAssist(task);
   guidance.NoteProgress();               // o jogador mexeu em algo (zera a ociosidade, não conclui)
   guidance.CompleteStep(task.Step.Id);   // só quando o capítulo concluiu a ação
   ```
   `GuidanceTask.Plaque` (setas + E), `Tuning` (A/D ou setas) e `Sweep` (manter E) trazem a alternativa de teclado no texto; a pessoa
   nunca precisa de pontaria. Tabelas de texto do capítulo entram por `presenter.AddTextTable`.
5. **Repetir o tutorial**: `guidance.Repeat(id)` começa sempre, mesmo marcado como visto. Quem oferece o botão (pausa/preferências, UI-006) só chama isto.

## Comportamento

- **Mover**: 0,5 s ativo de entrada WASD/setas (≥ 50% do eixo) ou, com `SetMovementProbe`, 1 m efetivo. Encostar na parede com a tecla não conta se a sonda existe.
- **Interagir**: `HoldCompleted` do candidato do capítulo (ou o de `InteractCandidateId`). Segurar E sem concluir só conta como participação.
- **12 s ativos sem progresso**: a primeira instrução pendente ganha texto direto, tecla cheia e borda grossa.
- **30 s ativos**: oferta de H (se o capítulo declarou tratar H) e de voltar ao corredor (se declarou corredor). Fica nesse nível até haver ação; cada nível é anunciado **uma vez**. Não há loop.
- **Tempo ativo**: pausa, cinemática, painel (salvo atividade de painel), dossiê e carregamento não contam. Sem fonte de tempo ativo o relógio não corre.
- **Assistência extra** (preferência): a dica de H aparece desde o início, se o capítulo tem H.
- **Movimento reduzido**: sem pulsação, só mudança de forma e texto.
- **Entrada**: não pede contexto, não chama `BeginHold`/`CancelHold`, não lê tecla. H continua sendo tratado pelo capítulo (enquadrar); aqui só conta como participação.
- Instrução cumprida fica 1,5 s com "feito ✓" e recolhe. Placa oculta na pausa, na cinemática e fora do capítulo.

## Relação com o tutorial de Marte (MAR-002)

`MarsTutorial` já faz o tutorial de M07 com os mesmos tempos de ajuda (12 s / 25 s). Esta orientação é a camada compartilhada: **não** substitui nem
duplica a conclusão dele. Para Marte usar a placa compartilhada, a integração (INT-001) liga `SetMovementProbe(() => rover.DrivenMeters)` e
`InteractCandidateId = MarsTutorial.InspectCandidateId`. Decisão sobre unificar os dois (e o 25 s → 30 s) fica com R1/dono de MAR-002.

## Verificação manual (pendente de uma pessoa com o Editor e o navegador)

Nada abaixo foi executado por quem escreveu o código.

1. Rodar os testes EditMode (`GuidanceLogicTests`, `GuidanceControllerTests`, `GuidanceUiTests`) e **Abandoned Space Apps > UI-005 > Reconstruir prefab da orientação**.
2. Sessão nova só com teclado: a placa aparece; WASD cumpre "mover"; chegar ao alvo e segurar E cumpre "interagir"; as linhas somem sem clique.
3. Ficar parado 12 s e depois 30 s: reforço e ofertas aparecem uma vez; pausar no meio e conferir que o tempo congela.
4. Desorientar a câmera (mouse) e usar H: o capítulo enquadra; nada teleporta.
5. Repetir o tutorial depois de visto (chamar `Repeat`) e recarregar a página: continua repetindo.
6. 960×600, 1280×720 e 4:3: a placa não cobre o prompt E nem a legenda de duas linhas.
