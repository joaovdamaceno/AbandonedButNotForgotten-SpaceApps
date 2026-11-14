# MAR-009 — Integração do capítulo de Marte (cena `Mars.unity`)

Entrega: `Scenes/Mars.unity` (montada por `Editor/MarsChapterAssembly.cs`), `Data/MarsSceneBindings.asset` (referências serializadas a prefabs e dados, tabela etapa → região das 32 etapas) e `Runtime/MarsChapterBindings.cs` (liga fluxo, atividades, regiões e câmeras). Menu: **Abandoned Space Apps → Marte → MAR-009 Montar a cena do capítulo** (idempotente).

## Montagem
- **Regiões** (contêineres sob `Regions`): `origin` (M01–M03), `landing` (M04–M06), `eagle` (M07–M15), `shield` (M16–M20), `victoria` (M21), `spirit` (M22), `valley` (M23–M30), `memorial` (M31–M32). Só a de abertura começa ativa; o `MarsChapterBindings`/`MarsSequenceConductor` ativam uma por etapa, então não há luzes, colisores ou câmeras duplicados. Todas partilham a origem do mundo (nunca ativas juntas), o que evita deslocamentos distantes e perda de precisão.
- **Rover:** `MarsRoverGameplay` com o modelo `OpportunityVisual` (MAR-011) no `VisualRoot`; só fica ativo nas regiões de gameplay e é posto nas estações (M07 Eagle, M16 campo do escudo, M21 Victoria, M24 vale).
- **Serviços da cena:** câmera principal + `CameraDirector`, `SequenceDirector`, Sol único com sombra e `MarsChapterFlow`. A R1 pode mover a câmera e os diretores para o Boot sem mudar nada aqui: tudo entra por `Initialize`.
- **Reconstrução:** reaproveita instâncias existentes; pose, apoio e overrides dos objetos são preservados (só instâncias novas nascem na origem). Testado.

## Passos para a R1 (INT-001)
1. Registrar a cena `Mars` nas Build Settings (só a R1 edita).
2. Depois de carregar a cena (aditiva), obter o `MarsChapterBindings` e chamar `Initialize(chapterDirector, sessionClock, inputRouter, interactionService, evidenceStore, settingsStore)`.
3. Ouvir `ArchiveControlsRequested` (disparado uma vez, depois dos 4 s do título) para expor VOLTAR AO ARQUIVO / REPETIR CAPÍTULO; pausa, reinício, dossiê e conclusão continuam do `ChapterDirector`.
4. Ao descarregar, chamar `Teardown()` (também feito em `OnDisable`).
5. A memória escolhida (M30) vai ao memorial sozinha, via `MarsTransmission.SelectedRecord`.

## Orçamento de 360 s
As 32 etapas somam 360 s de referência (validado). Cinemáticas: 146 s; gameplay: 214 s de referência editorial, sem cronômetro que conclua etapa. A meta de 5–7 min depende de teste com pessoas.

## Limitações
- **Eagle (M07–M15) usa um piso provisório identificado** (`Eagle · PLACEHOLDER (MAR-012 pendente)`): a MAR-012 não foi integrada. Quando for, basta atribuir o prefab em `MarsSceneBindings.eagleEnvironment` e reconstruir. Sem o terreno real, a distância entre a estação e os alvos da RAT/blueberries não foi conferida em cena.
- Os testes percorrem o capítulo com eventos do fluxo nas etapas de gameplay (as atividades têm suítes próprias); **não houve execução com teclado/mouse nem QA humano** (MAR-010). Reinício testado em M12 (RAT), M14, M25 (2018) e M31.
- Sem validação em WebGL/perfil leve. Câmera e Sol da cena, Boot e Build Settings aguardam a R1.
