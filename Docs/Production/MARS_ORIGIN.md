# MAR-013 — Laboratório, mapa e lançamentos (M01–M03, 25 s)

`Prefabs/MarsPreparationStage.prefab` (laboratório, globo, lançamentos e `MarsOriginDirector`), `Timelines/MarsOrigin_M01/M02/M03.asset` (8/12/5 s) e `Art/Origin/`. Tudo procedural; nenhum asset externo.

## Decisões e desvios
- **Sem `.playable`:** o projeto não tem o pacote Timeline; as sequências são `SequenceDefinition` (CORE-004) com keyframes de câmera densos. Câmera, globo, lançamentos e letreiros leem o mesmo tempo.
- **Mapa:** globo estilizado com os dois destinos em lat/lon aproximadas (Gusev ≈ 14,6°S 175,5°L; Meridiani ≈ 2°S 354,5°L, fontes: NASA, Mars24). **Não é o mapa de hematita do TES**: nenhuma imagem externa foi importada por falta de origem/licença verificadas. A mancha de hematita é um círculo de 7° em torno de Meridiani, ilustrativo.
- **Lançamentos:** duas silhuetas genéricas (não afirmam o modelo exato do Delta II) e uma única chama reaproveitada; rótulos só com mês e ano (JUN/JUL 2003).
- **Texto:** `Art/Origin/MarsOriginTexts.json`, chaves `MAR.ORI.*`; `MAR.T01` (título de M01) continua em `MarsSubtitles.json`.
- Luz lateral e contraste: a luz com sombra é da cena; o prefab não tem luzes, câmeras, partículas nem colisores.

## Limitações
Sem validação em WebGL nem em perfil baixo real; Unity 6000.6.8f1 (o projeto fixa 6.7f1). Julgamento visual (laboratório, globo) pendente de revisão por outra função. Integração na cena do capítulo fica para a MAR-018/MAR-009.
