# Arquitetura e contratos de integração

Estado: **CORE-001 (projeto) e CORE-002 (tipos de contrato) implementados**; os serviços que implementam as interfaces (CORE-003, CORE-004, CORE-005) ainda não existem. As assinaturas reais estão na seção *Contratos publicados* abaixo e em `Assets/_Game/Core/Contracts/`. R1 congela os contratos antes dos PRs consumidores. Evitar um framework de missões: três diretores de capítulo pequenos compartilham entrada, apresentação, áudio e registro.

## Organização prevista

```text
Assets/_Game/
  Core/          InputRouter, ChapterDirector, SessionClock, InteractionService
  Presentation/  SequenceDirector, SubtitlePresenter, AudioDirector, Settings
  UI/            Dossier, HUD, HoldPrompt, EvidencePanel, Pause
  Chapters/
    Mars/        Runtime, Data, Prefabs, Art, Scenes, Timelines
    Pioneer10/   Scripts, Data, Prefabs, Art, Scenes
    Surveyor/    Scripts, Data, Prefabs, Art, Scenes
  Shared/        Materials, Fonts, Audio, VFX
  Editor/        SceneBuilder, validation, build commands
  Tests/         EditMode, PlayMode
Docs/ Data/ Tools/ QA/
```

Boot e Dossier têm R1 como único integrador; R2 entrega prefabs/UI e um método de instalação. Cada capítulo possui uma cena própria. Somente uma região 3D ativa por vez. Não colocar a campanha inteira numa pasta Resources que carregue todos os modelos e áudios juntos. Inicialmente um pacote Web sem serviços remotos/Addressables; carregamento aditivo da cena é controle de RAM, não promessa de diminuir o download inicial. Os JSONs autorais em Data/ fora de Assets são entradas de produção: um importador de Editor os converte nos assets do capítulo; o player Web não lê caminhos do disco do repositório.

Marte: R3 possui scripts, dados e prefab RoverGameplay; R4 possui Environment, LandingRig e CinematicRig. Ambos entregam prefabs, R1/R3 fazem a montagem final da cena. R5 é dono de Pioneer; R6 de Surveyor. O diretório Shared e BuildSettings exigem revisão de R1; ninguém edita esse YAML junto.

## Estados e relógios

Aplicação: `Boot → Dossier → Loading → Playing → Completion → Dossier`. Pausa é uma camada sobre Playing, preservando subestado e relógio. Reiniciar descarrega cena, buffers, fotos e áudio antes de carregar. Não reutilizar estado residual.

`ChapterDirector` possui `Begin(ChapterDefinition)`, `CompleteStep(stepId)`, `FinishChapter()`, `RestartChapter()`. Repetir CompleteStep com o mesmo ID é idempotente. Cada capítulo possui tabela explícita de transições; não somar 1 no enum. Toda etapa terminal é validada e o próximo capítulo depende somente da seleção no arquivo.

`SessionClock.ActiveSeconds` cresce apenas em Playing não pausado. Dossiê, loading e pausa não contam; observações/minigames contam. `SequenceClock` é local ao plano, congelado em pausa. Datas históricas são strings/dados localizados com sistema UTC documentado, nunca derivadas do tempo da partida.

Sequências: `EnterStep → máscara de entrada → câmera/atores/áudio/textos → evento de conclusão → ExitStep`. Cancelamento, skip permitido, reinício e foco perdido devem liberar recursos e obedecer ao mesmo caminho de saída. Só uma sequência controla a câmera e a entrada a cada instante.

## Input e interações

`InputRouter` expõe Move(Vector2), Look(Vector2), InteractDown, InteractHeld, InteractUp, PauseDown e ações UI. WASD/setas; E; P/Esc/botão. Os instrumentos consomem os eventos do InputRouter; não leem a tecla diretamente. Controle de câmera manual é opcional para concluir qualquer objetivo; câmera assistida enquadra os alvos.

Contrato original proposto (a versão final publicada, com as diferenças, está em *Contratos publicados*):

```csharp
InteractionCandidate {
    string Id; Transform Anchor; string VerbKey; string ObjectKey;
    bool Available; float DistanceMeters; float HoldSeconds;
}
InteractionService.SetCandidate(candidate);
InteractionService.BeginHold(id); // somente candidato ativo e disponível
InteractionService.CancelHold();
// Eventos: HoldStarted(id), HoldProgress(id,01), HoldCompleted(id), HoldCanceled(id)
```

A mesma função calcula disponibilidade visual e execução. Nunca mostrar E disponível se a ação falha por uma distância diferente. Apenas um candidato selecionado, com prioridade explícita por etapa; alvos opcionais não roubam a ação principal. Input de navegação não atravessa painéis de evidência/menus.

**Rearme obrigatório:** o E que abre a observação não pode contar como início da medição seguinte. Exigir InteractUp antes de novo Hold. Ao perder foco/pausar, cancelar estado pressionado e zerar o comando retido; retomar exige clique/soltura. Progresso físico já produzido permanece, salvo quando o roteiro define uma nova amostra. Soltar E pausa a operação, não apaga trabalho nem dispara outra atividade.

`HoldAction` tem duração e apresentação configuráveis; o instrumento responde imediatamente com som/animação/progresso. Falta de habilidade não bloqueia: alinhamento automático e alvo grande; nunca concluir uma descoberta apenas porque o cronômetro avançou. A confirmação histórica de corte dos motores da Surveyor é uma exceção explícita de segurança: se a pessoa não agir, a equipe executa o corte, conforme o roteiro, sem premiar destreza nem inventar uma evidência. Essa exceção não autoriza completar minigames científicos automaticamente. Mouse, teclado e botões clicáveis equivalentes quando houver arrastar/encaixar/sintonia.

SUR-018 substitui a fila como mecânica obrigatória da Surveyor por operação direta assistida; os controladores antigos permanecem nos previews. Pouso é cinema predeterminado, sem confirmação do jogador. O tempo representativo de rádio pertence à explicação documental, sem atrasar mouse, pausa ou UI.

**Extensão de entrada SUR-018, aprovada no plano pelo proprietário:** `InputRouter.PrimaryDown`, `PrimaryUp`, `PrimaryHeld` e `PointerPosition`. O comando primário do mouse é entregue somente em Gameplay; Panel/Paused/Cinematic não recebem contato. Troca de contexto e perda de foco soltam o comando e exigem soltura/nova pressão. `PointerPosition` responde também em menus, sem acionar ferramenta. E mantém seu contrato anterior. O consumidor filtra o retângulo do instrumento e elementos de UI antes de iniciar contato; E e mouse representam um único contato lógico, nunca duas operações.

## Câmeras, planos e UI

`ShotDefinition`: ID, duração, ponto inicial/final ou keyframes, LookAt, FOV, easing, rig, limite de chão, trilha de eventos. `SequenceDefinition`: shots, cues e marcas de atores no **mesmo relógio**. Builders validam referências. Evitar Timeline separado do cronômetro de outro script; Timeline só quando consumir o relógio e eventos acordados.

Gameplay: câmera com acompanhamento amortecido, colisão contra cenário, horizonte legível. Cinemática: enquadramento e intenção no roteiro, sem dois cortes quase idênticos. Usar um único CameraDirector; câmeras especiais são rigs, não AudioListeners concorrentes. Pousos têm continuidade do mesmo módulo; conferência em cada corte e câmera acima do terreno real.

Tela dividida das gêmeas: dois enquadramentos pequenos com RTs limitadas; testar custo antes de ativar duas câmeras com sombras. Fallback obrigatório: duas renderizações alternadas a 15 Hz, ambas atualizadas no mesmo relógio; UI e legendas continuam atualizadas em cada quadro da aplicação, com meta mínima de 30 FPS; o fallback limita apenas a atualização das duas vistas 3D. Se ainda caro, montagem em metades com um quadro congelado identificado, preservando datas e a elipse. Não duplicar um mundo inteiro.

`SubtitleCue`: id, start/end ou gatilho, chave PT/EN, posição e prioridade. Texto vazio oculta placa/fundo. Objetivo persistente e fala temporária têm camadas distintas. R2 controla tipografia e escala 960×600–1920×1080, incluindo 4:3. Legenda de leitura rápida não pode ser cortada por fim de shot; fonte de duração compartilhada e teste de leitura.

## Dados e evidências

`ChapterDefinition`: ID estável, título/descrição localizados, capa, pergunta, cena, tempo-alvo, sequência inicial. `EvidenceRecord`: evidenceId, tipo (Photo/Composition/Observation), chave de resultado, referência científica, thumbnail opcional, autoria (PlayerCapture/Archive/Reconstruction). Nenhum resultado simulado vira telemetria histórica literal.

`EvidenceStore.Add(record)` é idempotente. Só fotografias do jogador podem ser apresentadas como suas; imagem real de arquivo recebe crédito e data. Três imagens de até 960×540 por capítulo; liberar anteriores na saída, salvo imagem do memorial. Mapas/destinos e trajetórias são diagramas com escala explicada, sem sugerir pontos distantes adjacentes.

`LocalizationService.Get(key)`, troca de idioma com pausa segura; nenhuma frase de jogo hardcoded em C#. `SettingsData` persiste idioma, volume, texto, sensibilidade, qualidade e assistência; progresso da sessão é separado. Tutorial visto nunca impede repetir tutorial ou jogar uma instalação nova.

## Áudio e recursos

`AudioDirector.SetCue(cueId, fadeSeconds)` e buses Master/Music/SFX/UI. Um único responsável pelo crossfade; loops curtos verificados; volume exposto no dossiê e pausa. Som no espaço/Lua é música e sonificação da interface; não passos externos num vácuo tratados como som ambiente real.

A fonte de energia é específica da missão: os rovers usam painéis solares; Pioneer usa RTGs; Surveyor combina painéis e bateria conforme a documentação. Não compartilhar um modelo único de bateria entre três histórias diferentes. Valores de gameplay podem ser mostrados como níveis simplificados sem espalhar avisos em cada canto.

## Contratos publicados (CORE-002)

Tipos em `Assets/_Game/Core/Contracts/` (assembly `Abandoned.SpaceApps`, namespace `Abandoned.SpaceApps`). Quem consome usa estes tipos; ninguém cria um equivalente local. Mudança de assinatura exige acordo com R1 e atualização do exemplo e dos consumidores no mesmo PR.

| Tipo | Arquivo | Papel |
| --- | --- | --- |
| `ChapterDefinition` (ScriptableObject), `ChapterStep` | `ChapterDefinition.cs`, `ChapterStep.cs` | Identidade, chaves de texto, cena, tempo-alvo, sequência inicial e tabela explícita de etapas |
| `IChapterDirector` | `IChapterDirector.cs` | Contrato do `ChapterDirector` (CORE-005) |
| `InteractionCandidate` (struct), `IInteractionService` | `InteractionCandidate.cs`, `IInteractionService.cs` | Contrato do `InteractionService`/`HoldAction` (CORE-003) |
| `EvidenceRecord`, `EvidenceKind`, `EvidenceAuthorship`, `IEvidenceStore` | `EvidenceRecord.cs`, `IEvidenceStore.cs` | Evidências da sessão (CORE-005) |
| `ShotDefinition`, `ShotKeyframe`, `ShotEvent`, `ShotEasing` | `ShotDefinition.cs` | Plano de câmera (CORE-004). `ContinueFromPreviousShot` (acrescentado na CORE-004) faz o plano partir da pose/FOV atuais da câmera, sem salto |
| `SubtitleCue`, `SubtitlePosition` | `SubtitleCue.cs` | Legenda por chave; janela de tempo **ou** gatilho (UI-004) |
| `SequenceDefinition` (ScriptableObject), `ActorMark` | `SequenceDefinition.cs` | Planos, legendas e marcas de atores num único relógio (CORE-004) |
| `ContractValidation` | `ContractValidation.cs` | Regra de ID e mensagens de erro |

### Regras comuns

- **IDs e chaves** (capítulo, etapa, evidência, plano, cue, chave de localização): letra inicial + letras ASCII, dígitos, `.`, `_`, `-`; até 64 caracteres, sem espaço (ex.: `MAR.C12`, `FirstImage`).
- **Unidades no nome**: `...Seconds`, `...Meters`, `...Degrees`.
- **Validação clara antes da execução**: todo tipo de dado tem `Validate()` que devolve `List<string>` (vazia = válida) e nunca lança NullReference por campo ausente. `ChapterDefinition` e `SequenceDefinition` também avisam no Inspector (`OnValidate`). Menu **Abandoned Space Apps → CORE-002 → Validar contratos de dados** e `ContractAssetValidator.ValidateFromCommandLine` (saída 0 = conforme) conferem todos os assets, inclusive `chapterId` duplicado.
- Os `Create(...)` estáticos de `ChapterDefinition` e `SequenceDefinition` existem para builders determinísticos e testes; não validam, chame `Validate()`.

### `IChapterDirector`

```csharp
ChapterDefinition Current { get; }   string CurrentStepId { get; }   bool IsFinished { get; }
event Action<string> StepEntered;    event Action<string> ChapterFinished;   // stepId / chapterId
void Begin(ChapterDefinition chapter);            // ArgumentException se Validate() falhar
bool CompleteStep(string stepId);                 // uma única sucessora
bool CompleteStep(string stepId, string nextStepId); // várias sucessoras
void FinishChapter();                             // só na etapa terminal; idempotente
void RestartChapter();
```

`CompleteStep` é idempotente: repetir o ID, ou passar uma etapa que não é a atual, devolve `false` sem alterar nada. Concluir a etapa terminal encerra o capítulo. Transições vêm só da tabela de `ChapterStep`; a validação exige etapas únicas, sucessoras existentes, ao menos uma terminal alcançável, nenhuma etapa inalcançável e nenhuma etapa não terminal sem sucessora.

### `IInteractionService`

```csharp
struct InteractionCandidate { string Id; Transform Anchor; string VerbKey, ObjectKey; bool Available;
                              float DistanceMeters, HoldSeconds; int Priority; }

bool TryGetActive(out InteractionCandidate c);  bool AwaitingRelease { get; }  string HoldingId { get; }
event Action<string> HoldStarted;  event Action<string,float> HoldProgress;
event Action<string> HoldCompleted;  event Action<string> HoldCanceled;
void SetCandidate(InteractionCandidate c);  void ClearCandidate(string id);
bool CanBeginHold(string id);  bool BeginHold(string id);  void CancelHold();
void RequireRelease();  void NotifyInteractReleased();
```

Diferenças em relação à proposta original: `Priority` explícito; `BeginHold` devolve `bool`; `CanBeginHold` é a mesma regra sem efeito, que o prompt consulta (disponibilidade visual = execução); `ClearCandidate`; `RequireRelease`/`NotifyInteractReleased` formalizam o **rearme** (o E que abre uma observação não inicia a medição seguinte; soltar E cancela o hold e libera o rearme). O candidato ativo é o disponível de maior prioridade (empate: menor distância), de modo que alvos opcionais não roubam a ação principal.

### `IEvidenceStore` e `EvidenceRecord`

`Add` é idempotente por `EvidenceId` (o primeiro registro é mantido; sem evento repetido); registro nulo ou inválido lança `ArgumentException` com a lista de erros. `PlayerCapture` só vale para `Photo`; `Archive` exige `creditKey`; miniatura até 960×540. `CanBePresentedAsPlayers` só é verdadeiro para fotografia do jogador.

### Exemplo mínimo e mocks

`Assets/_Game/Core/Contracts/Examples/` (assembly `Abandoned.SpaceApps.ContractExamples`, `defineConstraints: UNITY_EDITOR`: **não entra no player**). `ContractExampleFlow` mostra os três passos com as interfaces: solicitar interação, apresentar evidência, concluir etapa. A cena `ContractExample.unity` (fora das Build Settings) roda o fluxo com E simulado por 2 s; recriar pelo menu **CORE-002 → Criar cena de exemplo dos contratos**. Os `Mock*` implementam a semântica acima para testes e **não substituem** as implementações de CORE-003 e CORE-005; as mesmas regras devem ser reproduzidas por elas.

## Narração inglesa (NAR-001)

`Shared/Narration/NarrationCatalog` associa IDs de legenda a gravações humanas.
`NarrationPlayer` é o único proprietário de voz por cena e acompanha a chave
narrativa que `SubtitlePresenter` publica por `BodyKeyChanged`/`CurrentBodyKey`.
A atualização de idioma não muda essa chave; inglês é a única dublagem.
O player usa pausa/foco separados e o playhead real do áudio; libera a voz ao
descarregar o capítulo. Não cria entrada, relógio ou progressão paralelos.

Os hosts recebem as dependências existentes (SessionClock/AudioDirector/
ChapterDirector). Opportunity adapta seu relógio ativo e amplia janelas com
`NarrationTimeMap`, usado também por câmeras/atores para manter continuidade.
O assembly Opportunity passa a referenciar `Abandoned.SpaceApps` em uma direção;
o core não referencia Opportunity. O assembly Editor referencia os dois para
instalar as referências nas cenas reais. Sem nova dependência de pacote.

Builders próprios anexam os catálogos; `NarrationSetup.InstallAndValidate`
configura/valida as quatro cenas e ajusta as janelas sem acelerar gravações.
Detalhes e lacunas reais em `Docs/Production/NARRATION_EN.md`.

## Contratos de entrega e testes

Cada prefab consumidor precisa de um `ValidateSetup()` no Editor ou validação equivalente: referências, materiais, chaves de texto e eventos terminais. Builders determinísticos geram/reconstroem apenas a área de sua tarefa; não apagar configurações de colega. PR com teste do estado anterior/seguinte, repetição de E, pausa/foco e reinício quando pertinentes.

R1 aceita o contrato em CORE-002; R2/R3/R5/R6 podem trabalhar contra esses tipos e dados sem esperar a cena final. Mock só em cena de teste, removido da distribuição. Não inventar que dependência mockada está concluída.
