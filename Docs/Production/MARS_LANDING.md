# MAR-014 — Pouso paralelo (M04–M06)

`Prefabs/MerLandingAssembly.prefab` (dois palcos, câmera do Spirit e `MarsDualLandingDirector`), `Timelines/MarsDualLanding_M04/M05/M06.asset` (18/4/5 s) e `Art/Landing/`. Tudo procedural; nenhum asset externo.

## Decisões e desvios
- **Sem `.playable`:** o projeto não tem o pacote Timeline. As sequências são `SequenceDefinition` (CORE-004) com keyframes de câmera densos; câmera, atores, legendas e letreiros leem o mesmo tempo.
- **Coreografia pura** (`MerLandingChoreography`): Spirit e Opportunity usam os mesmos movimentos; só o terreno muda (Eagle: bacia de ~2,9 m, aproximada; o modelo final é da MAR-012).
- **Escala cinematográfica:** o hardware de pouso é maior que o real para o rover (1:1, MAR-011) caber dobrado; o rover guardado usa escala 0,42 e cresce ao se desdobrar (as asas do modelo são fixas). Tempo comprimido a 27 s.
- Datas UTC: NASA GISS Mars24 (4 e 25/1/2004), JPL (desembarque em 31/1/2004). Letreiros por chave em `MarsLandingTexts.json`; `MAR.T02` é reutilizada de `MarsSubtitles.json`.
- MAR-011 (#30) está fechada e integrada, mas sem o label `integrated`.

## Limitações
Sem validação em WebGL nem em GPU fraca. Unity 6000.6.8f1 (o projeto fixa 6.7f1). Julgamento visual (cores, plasma, escala) pendente de revisão da R4/R1. Integração na cena e áudio ficam para a MAR-018/MAR-009.
