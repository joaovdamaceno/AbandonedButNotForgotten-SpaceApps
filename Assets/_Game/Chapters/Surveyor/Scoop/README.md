# SUR-009 — pá, sulco e recolhimento seguros

`Scripts/SurveyorScoop.cs` (runtime), `Art/Trench.prefab` e `Art/ScoopRig.prefab` (gerados por `Editor/SurveyorScoopBuilder.cs`). **Estado: código, prefabs, builder e 27 testes verificados no Unity; falta integrar na cena do capítulo e concluir as capturas WebGL** (ver `QA/SUR-009/README.md`).

## Como gerar os prefabs

Menu **Abandoned Space Apps → Surveyor → SUR-009 → Reconstruir pá e trincheira**, ou `-executeMethod Abandoned.SpaceApps.Chapters.Surveyor.EditorTools.SurveyorScoopBuilder.BuildFromCommandLine`. Lê a âncora `Trench`, o ombro `Scoop_Arm_1`, a pá nativa e a câmera do `SurveyorVisibleRig.prefab` sem editá-lo e grava `Art/Trench.prefab`, `Art/ScoopRig.prefab` e `Art/ScoopGenerated/` (malhas, dois materiais derivados do regolito, laço de motor sintetizado). Reconstruir preserva GUIDs. Os prefabs são assados no espaço do LunarSite: instancie-os sob o mesmo pai (origem) do site e do rig.

## Contrato de uso

```csharp
scoop.Configure(chapterDirector, sessionClock, inputRouter, interactionService,
    trenchInstance, scoopRigInstance, groundRenderersToReplace, nativeScoopRenderers);
```

- `groundRenderersToReplace`: o `MeshRenderer` do chunk `NearGround_40m/Chunk_2_2` do LunarSite. O Trench traz o próprio chunk com um furo e o sulco como `SkinnedMeshRenderer` de quatro blend shapes (`Pass1..4`); sem trocar o chunk, o terreno cobriria a cavidade. `Dispose`/`OnDisable` restaura o original.
- `nativeScoopRenderers`: `Team_ScoopLinkage` e `Team_ScoopTool` do rig da SUR-002. O repouso do ScoopRig coincide com a pose da pá nativa.
- Entra na etapa `Sample`. **Não conclui a etapa**: SUR-010 (carga e impacto) e o `ChapterDirector` decidem a saída. O sulco e o repouso permanecem nas cenas seguintes; reiniciar/descarregar restaura o terreno original.
- `ValidateSetup(anchorTrench)` confere a âncora (a foto do resultado usa o mesmo `TrenchCenter`/`FootprintWorldBounds`) e o alcance do braço.

## Comportamento

| Fase | O que acontece |
| --- | --- |
| Approaching | 2,4 s ativos do repouso até acima do início do sulco |
| Ready | SUR-S14; oferece o hold E de 12 s (mesmo estado do prompt: `CanDig`) |
| Digging | 4 passes de 3 s; doze mordidas de 1 s (até ~0,35 s cada para se formar). Soltar E congela malha e áudio e conserva o avanço; pausa também |
| Retracting | contato → sobe 8 cm (3 pontos fixos em x/z) → recua lateralmente → repouso |

- Profundidade máxima 18 cm (`Shape.MaxDepthMeters`). Três grãos por mordida, no máximo 24 vivos, gravidade lunar 1,62 m/s², pousam e somem em 1,5 s: sem fumaça nem poeira suspensa.
- SUR-S15 entra na primeira mordida; se S14 ainda estiver no tempo mínimo de leitura (5 s), espera. As chaves são os IDs (`SUR-S14`, `SUR-S15`); os textos PT/EN pertencem à SUR-014.
- `Cancel()` e sair da etapa usam a pose segura: subida de 8 cm antes de qualquer movimento lateral; nunca abaixo do relevo deformado. O balde inteiro (não só a ponta) é testado contra o relevo.
- O braço é uma reconstrução leve de dois elos com IK (pantógrafo estilizado), não o CAD da NASA. O arrasto segue o eixo perpendicular à visão da câmera; o sulco não é topografia medida.

## Decisões

- O arrasto usa o hold direto do `InteractionService` (feedback contínuo, soltar pausa), não a fila de 1,3 s da SUR-004, porque o roteiro pede resposta a cada 0,8–1,2 s.
- Áudio: um laço de motor sintético de 1 s (92/184/276 Hz com trinado de 9 Hz), em pausa quando E é solto ou o jogo pausa. Volume em `MotorVolume` para o host ligar às configurações.
