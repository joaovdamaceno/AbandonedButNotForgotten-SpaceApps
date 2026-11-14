# Core — R1

| Arquivo/pasta | Tarefa |
| --- | --- |
| estrutura, `Editor/ProjectSetup*.cs`, `Tests/EditMode/` | CORE-001 |
| `Contracts/` (tipos, interfaces, validação) e `Contracts/Examples/` (exemplo + mocks, só no Editor) | CORE-002 — **implementado** |
| `Editor/ContractAssetValidator.cs`, `Editor/ContractExampleBuilder.cs` | CORE-002 |
| `InputRouter.cs`, `InteractionService.cs`, `HoldAction.cs`, `IActiveTimeSource.cs` | CORE-003 — **implementado** |
| `ChapterDirector.cs`, `SessionClock.cs`, `EvidenceStore.cs`, `SettingsStore.cs` | CORE-005 — **implementado** |
| `ChapterCatalog.asset` | UI-002 (R2) |

## Entrada e interação (CORE-003)

`InputRouter` é o **único** leitor de teclado/mouse (Input System, ações criadas em código): WASD/setas, mouse (opcional), E, H, Enter, P/Esc. Ninguém mais lê teclas.

| Contexto (`InputContext`) | Entrega |
| --- | --- |
| `Panel` (padrão sem pedido) | E, navegação (setas/WASD discretas), Enter, pausa |
| `Gameplay` | movimento, olhar, E, H, pausa |
| `Cinematic` | Enter (pular, se o roteiro permitir) e pausa |
| `Paused` | navegação, Enter e pausa (para retomar) |

- Quem precisa de um contexto usa `using (router.Acquire(InputContext.Gameplay, "dono")) { ... }`; vale o último pedido e descartar fora de ordem ou duas vezes é seguro. Sem pedido, a entrada de **gameplay fica desligada** (restaurada só na saída válida do estado).
- Ao **trocar de contexto, perder foco ou desabilitar**: movimento e olhar zeram, o E lógico é solto (`InteractUp`) e um E ainda pressionado só volta a valer depois de soltar. Uma direção mantida durante a pausa não move o jogador ao retomar.
- `InteractionService` implementa `IInteractionService`: o prompt usa `CanBeginHold(id)` e a execução usa a mesma condição. Um pressionamento inicia no máximo um hold; terminar um hold exige soltar E antes do seguinte (o rearme só espera se a tecla ainda está pressionada); E mantido ao entrar numa área não inicia nada; soltar, pausar, trocar de contexto ou perder foco cancelam uma única vez; o progresso só é emitido quando muda. `Mode = HoldMode.Toggle` é a alternativa de acessibilidade (pressionar para iniciar, de novo para parar).
- O tempo do hold vem de `IActiveTimeSource` (o `SessionClock` da CORE-005 vai implementá-la): sem fonte, o hold **não avança**. O serviço nunca toca em `Time.timeScale`.
- Integração: `service.Initialize(router, timeSource)`; o instrumento chama `SetCandidate` (pode ser a cada quadro, atualizar o candidato ativo não cancela o hold) e `ClearCandidate` ao concluir. Quem conserva o progresso físico quando E é solto é o instrumento (ex.: novo candidato só com o tempo restante).

## Fluxo, relógio, evidências e preferências (CORE-005)

- `ChapterDirector` (implementa `IChapterDirector`): `Boot → Dossier → Loading → Playing → Completion → Dossier`. `Begin(chapter)` vale em Boot/Dossier/Completion e é ignorado durante Loading/Unloading (cliques rápidos não carregam duas vezes); `RestartChapter()`, `ReturnToDossier()`, `Pause(fonte)`/`Resume(fonte)` (camadas: menu, foco…).
- Antes de qualquer troca, `ChapterUnloading(chapterId)` avisa quem deve soltar RenderTextures e áudio; depois o diretor limpa evidências (destruindo fotos do jogador), zera relógio, etapas e contextos de entrada e descarrega a cena (aditiva, seguida de `Resources.UnloadUnusedAssets`).
- `SessionClock` (`IActiveTimeSource`, `DefaultExecutionOrder(-1000)`): só corre de `ChapterStarted` até a conclusão; dossiê, carregamento, conclusão e pausa não contam. Delta por quadro limitado a 0,1 s (volta de aba suspensa). Passe-o ao `InteractionService` e ao `SequenceDirector` em `Initialize`.
- `EvidenceStore`: idempotente por ID, até 3 fotos do jogador por capítulo; `Clear` destrói texturas/sprites criados em runtime (nunca assets).
- `SettingsStore` + `SettingsData`: grava só preferências (idioma, volumes, texto, sensibilidade, qualidade, modo do hold, movimento reduzido, assistência) e tutorial visto, em JSON versionado nos PlayerPrefs. Dado ausente/corrompido/fora de faixa volta ao padrão; progresso de partida nunca é gravado.
