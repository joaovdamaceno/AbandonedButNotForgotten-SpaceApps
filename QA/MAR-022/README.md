# MAR-022 · música na abertura da Opportunity

Base: `origin/develop` em `4c3ba6d` (PR #211, integração das 3 cenas).

## Problema
`MissionAudio.Enter` só pedia música a partir de `HeatShieldExploration`; as etapas `Intro`, `Landing` e `Activation` ficavam sem faixa,
embora a cena já tivesse os cues `orbit`, `descent` e `arrival` (faixas de `Audio/WebV13`, já licenciadas e no projeto).

## Correção (2ª rodada): a abertura estava realmente muda
Retorno da pessoa usuária: "ainda sem som no começo". Causa: no fluxo integrado, o começo do capítulo (M01 Marte, M02 laboratório, M03 globo) roda na cena
`Mars`, que não tem nenhuma `AudioSource`; a cena `Opportunity` (onde está o `MissionAudio`) só entra no pouso. Os cues da 1ª rodada só valiam depois do handoff.

Correção: `MarsOpeningMusic` (novo, em `Opportunity/Bridge`) é criado por `MarsFieldHandoff` assim que o fluxo está em M01. Toca `a-kind-of-hope` em loop,
entrada de 4 s, volume = master × música do aplicativo × 0,25, pausa junto com o capítulo e sai com fade de 2 s quando a cena `Opportunity` assume
(o `MissionAudio` segue com a faixa do pouso). A faixa foi movida (git mv, GUID preservado) de `Audio/WebV13` para `Resources/Audio` para ser carregada pelo handoff.

## Mudança (1ª rodada, mantida)
`MissionAudio.Enter`: `Intro` → `orbit`, `Landing` → `descent`, `Activation` → `arrival`. Nenhum asset novo.

## Evidência
- Play Mode na cena `Opportunity` (Editor, Unity em foco): `CurrentCue=descent`, `musicA` tocando `the-things-that-keep-us-here`, volume 0,18 (música 0,65 × 0,5 × ganho 0,55), etapa `Landing`, sem pausa.
- Play Mode pela `Boot` (capítulo `mars` iniciado por `ChapterDirector.Begin`), etapa M01, Unity em foco: `MarsOpeningMusic` presente, nível 1,00 e volume 0,200 depois do fade; ao perder o foco a pausa do capítulo pausa a faixa.
- EditMode (2ª rodada): `MarsOpeningTrackTests` (4) passam; no total 58 testes (`MarsOpeningTrackTests`, `MarsOpeningMusicTests`, `MarsChapterTests`, `MarsFlowTests`, `MarsLandingTests`) passam.
- EditMode (1ª rodada): `MarsOpeningMusicTests` (2 testes) passam; `MarsChapterTests`, `MarsFlowTests`, `MarsLandingTests` (52 testes) passam.

## Limitações
- Não testei em build Web nem com fones; mixagem fina (ganho/fade dos cues) segue para a revisão humana de MAR-022.
- Não observei a transição de saída (fade na troca para `Opportunity`) em Play, só o código e o teste do fade; a troca real fica para a revisão com fones.
- Na verificação em Play a cena já estava em `Landing` quando consultei; o cue `orbit` da etapa `Intro` está coberto só pelo teste.
