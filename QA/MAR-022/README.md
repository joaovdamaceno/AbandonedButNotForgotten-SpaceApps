# MAR-022 · música na abertura da Opportunity

Base: `origin/develop` em `4c3ba6d` (PR #211, integração das 3 cenas).

## Problema
`MissionAudio.Enter` só pedia música a partir de `HeatShieldExploration`; as etapas `Intro`, `Landing` e `Activation` ficavam sem faixa,
embora a cena já tivesse os cues `orbit`, `descent` e `arrival` (faixas de `Audio/WebV13`, já licenciadas e no projeto).

## Mudança
`MissionAudio.Enter`: `Intro` → `orbit`, `Landing` → `descent`, `Activation` → `arrival`. Nenhum asset novo.

## Evidência
- Play Mode na cena `Opportunity` (Editor, Unity em foco): `CurrentCue=descent`, `musicA` tocando `the-things-that-keep-us-here`, volume 0,18 (música 0,65 × 0,5 × ganho 0,55), etapa `Landing`, sem pausa.
- EditMode: `MarsOpeningMusicTests` (2 testes) passam; `MarsChapterTests`, `MarsFlowTests`, `MarsLandingTests` (52 testes) passam.

## Limitações
- Não testei em build Web nem com fones; mixagem fina (ganho/fade dos cues) segue para a revisão humana de MAR-022.
- Na verificação em Play a cena já estava em `Landing` quando consultei; o cue `orbit` da etapa `Intro` está coberto só pelo teste.
