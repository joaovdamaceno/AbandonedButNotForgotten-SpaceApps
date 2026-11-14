# MAR-018 — Edição cinematográfica e entrega de câmeras de Marte

Entrega: `Timelines/MarsChapterSequences.asset` (catálogo das 14 etapas cinemáticas), `Timelines/MarsEdit_M14/M15/M20/M23/M27.asset` (as cinco sequências que só a edição possui), `Prefabs/MarsCameraAnchors.prefab` (âncoras de câmera e `MarsSequenceConductor`) e `Art/MarsLightingBindings.asset` (exposição por região). Código em `Art/Cinema/`.

## Pontos de corte, folgas e câmeras por etapa

Folga = duração da sequência − segundos de referência do `MarsChapterFlow`; verificada por teste (todas 0). Câmeras = pontos de vista renderizados ao mesmo tempo.

| Etapa | Região | Rig da câmera principal | Duração | Folga | Cortes (s locais) | Câmeras |
| --- | --- | --- | ---: | ---: | --- | ---: |
| M01 | origin | `mars.origin` | 8 | 0 | 4 | 1 |
| M02 | origin | `mars.origin` | 12 | 0 | 4, 8 | 1 |
| M03 | origin | `mars.origin` | 5 | 0 | 2,5 | 1 |
| M04 | landing | `mars.landing.opportunity` | 18 | 0 | 6, 12 | 2 (split) |
| M05 | landing | `mars.landing.opportunity` | 4 | 0 | — (contínuo, a metade inferior cresce) | 2 → 1 |
| M06 | landing | `mars.landing.opportunity` | 5 | 0 | 3,2 | 1 |
| M14 | eagle | `mars.cin.M14` | 14 | 0 | 7 | 1 |
| M15 | eagle | `mars.cin.M15` | 5 | 0 | — | 1 |
| M20 | shield | `mars.cin.M20` | 8 | 0 | 4 | 1 |
| M22 | spirit | `mars.spirit.opportunity` | 45 | 0 | — na câmera principal; 5, 11, 17, 22, 27, 32, 39 na câmera do Spirit | 2 → 1 |
| M23 | valley | `mars.cin.M23` | 9 | 0 | 4,5 | 1 |
| M27 | valley | `mars.cin.M27` | 6 | 0 | — | 1 |
| M31 | memorial | `mars.memorial` | 30 | 0 | 6, 12, 18, 24 | 1 |
| M32 | memorial | `mars.memorial` | 4 | 0 | — (díptico imóvel) | 1 |

Somente M04, M05 e M22 renderizam dois pontos de vista (teste de capítulo inteiro). As legendas (28 de relógio de sequência) vêm das próprias sequências via `MarsChapterFlow.BuildSequenceCues`, sem duplicar texto e sem cue vazio.

## Como o condutor liga tudo (para a MAR-009)
`MarsSequenceConductor.Initialize(flow, sequenceDirector, cameraDirector, sunLight)`; a cena registra os objetos de cada região com `RegisterRegion(regionId, GameObject)` (`mars.origin`, `mars.landing`, `mars.eagle`, `mars.shield`, `mars.valley`, `mars.spirit`, `mars.memorial`) e pousa cada rig `mars.cin.*` no sujeito real com `MarsCameraAnchors.Bind(rigId, subject)` (cavidade da RAT, rover, meteorito, mirante). Ao entrar numa etapa cinemática ele ativa só a região da etapa, aplica a exposição, toca a sequência e devolve o término ao fluxo; em gameplay, reinício ou desligamento cancela a sequência, devolve a câmera e restaura a exposição original. Sequência ausente vira salto com o evento `PlaybackFailed` (o capítulo não trava).

## Limitações
- A região `mars.eagle` (M14, M15) não tem ambiente real: a MAR-012 (Eagle) ainda não foi integrada. Os planos usam âncoras relativas ao sujeito; "não cortar para dentro da rocha" é garantido por distância mínima ao sujeito (2,5 m) e altura mínima, não pela geometria real da rocha.
- Os planos autorais (M14, M15, M20, M23, M27) não têm captura visual com cenário real; revisão visual por outra função pendente, junto com a conferência da exposição na cena final.
- A atmosfera dinâmica (poeira, névoa) continua no `MarsAtmosphereController` (MAR-016); os perfis daqui são a exposição de base.
- Contagem de câmeras/objetos medida por teste de EditMode; falta medir custo no navegador.
- Sem teste automatizado de reconstrução de cena.
