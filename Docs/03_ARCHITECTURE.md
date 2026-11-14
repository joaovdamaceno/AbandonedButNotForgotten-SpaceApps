# Arquitetura e contratos de integração

Estado: especificação para CORE-001 e CORE-002, ainda sem implementação. R1 congela os contratos antes dos PRs consumidores. Evitar um framework de missões: três diretores de capítulo pequenos compartilham entrada, apresentação, áudio e registro.

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

Contrato proposto:

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

Surveyor tem um `CommandDelayQueue` exclusivo: confirmação imediata, execução depois de 1,3 s **de ida**, como aproximação acessível do trânsito Terra–Lua, um comando pendente por ação, cancelar/limpar em mudança de estado. O atraso não altera mouse, pausa, feedback, legendas nem áudio de UI. O pouso tem sequência histórica predeterminada e janela acessível de confirmação: o jogador não reescreve a história nem precisa reagir contra um atraso impossível.

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

## Contratos de entrega e testes

Cada prefab consumidor precisa de um `ValidateSetup()` no Editor ou validação equivalente: referências, materiais, chaves de texto e eventos terminais. Builders determinísticos geram/reconstroem apenas a área de sua tarefa; não apagar configurações de colega. PR com teste do estado anterior/seguinte, repetição de E, pausa/foco e reinício quando pertinentes.

R1 aceita o contrato em CORE-002; R2/R3/R5/R6 podem trabalhar contra esses tipos e dados sem esperar a cena final. Mock só em cena de teste, removido da distribuição. Não inventar que dependência mockada está concluída.
