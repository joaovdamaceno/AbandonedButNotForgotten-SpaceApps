# PIO-003 · Palco espacial da Pioneer 10 — medições da prévia

Gerado por `PioneerStageCapture` (Editor, batchmode). **Estimativas** por renderer cujo AABB intersecta o frustum,
sem batching: não são números do profiler nem da build Web. Metas do roteiro: < 120 draw calls de conteúdo e
≤ 150 mil triângulos visíveis nos planos de Júpiter; uma luz principal sem sombra + um fill sem sombra.

| Anchor | Estado | Região | FOV | Renderers | Triângulos | Draw calls (est.) | Luzes ativas | Com sombra | Folga da sonda |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `PIO.Launch.Reveal` | PIO.Launch | DeepSpace | 34° | 23 | 5032 | 23 | 2 | 0 | 5.50 m |
| `PIO.Launch.Pad` | PIO.Launch | LaunchSite | 50° | 3 | 3448 | 3 | 2 | 0 | — |
| `PIO.Launch.Ellipse` | PIO.Launch | DeepSpace | 42° | 24 | 5044 | 24 | 2 | 0 | 501.57 m |
| `PIO.Launch` | PIO.Launch | DeepSpace | 40° | 23 | 5032 | 23 | 2 | 0 | 4.57 m |
| `PIO.Belt` | PIO.Belt | DeepSpace | 30° | 24 | 5044 | 24 | 2 | 0 | 74.70 m |
| `PIO.JupiterApproach` | PIO.JupiterApproach | DeepSpace | 40° | 24 | 9000 | 24 | 2 | 0 | 6.10 m |
| `PIO.JupiterApproach.Instrument` | PIO.JupiterApproach | DeepSpace | 38° | 20 | 4944 | 20 | 2 | 0 | 0.75 m |
| `PIO.Imaging` | PIO.Imaging | DeepSpace | 46° | 5 | 8520 | 5 | 2 | 0 | 1.13 m |
| `PIO.Plaque` | PIO.Plaque | DeepSpace | 36° | 13 | 8716 | 13 | 2 | 0 | 0.57 m |
| `PIO.Outbound` | PIO.Outbound | DeepSpace | 40° | 24 | 5044 | 24 | 2 | 0 | 16.41 m |
| `PIO.Listening` | PIO.Listening | GroundStation | 55° | 3 | 3448 | 3 | 2 | 0 | — |
| `PIO.Farewell` | PIO.Farewell | DeepSpace | 45° | 24 | 5152 | 24 | 2 | 0 | 8.23 m |

Maior coordenada de anchor: 536 m (limite 1000 m).
Rotação visual de cruzeiro: 24°/s (valor de direção, não telemetria).

## Continuidade entre planos consecutivos

- PIO.Launch.Reveal → PIO.Launch.Pad: Δângulo 102°, Δposição 745.4 m, ΔFOV 16°, região muda
- PIO.Launch.Pad → PIO.Launch.Ellipse: Δângulo 51°, Δposição 523.5 m, ΔFOV 8°, região muda
- PIO.Launch.Ellipse → PIO.Launch: Δângulo 52°, Δposição 497.1 m, ΔFOV 2°, região igual
- PIO.Launch → PIO.Belt: Δângulo 149°, Δposição 86.7 m, ΔFOV 10°, região igual
- PIO.Belt → PIO.JupiterApproach: Δângulo 168°, Δposição 89.9 m, ΔFOV 10°, região igual
- PIO.JupiterApproach → PIO.JupiterApproach.Instrument: Δângulo 101°, Δposição 12.9 m, ΔFOV 2°, região igual
- PIO.JupiterApproach.Instrument → PIO.Imaging: Δângulo 74°, Δposição 4.4 m, ΔFOV 8°, região igual
- PIO.Imaging → PIO.Plaque: Δângulo 15°, Δposição 1.8 m, ΔFOV 10°, região igual
- PIO.Plaque → PIO.Outbound: Δângulo 167°, Δposição 18.9 m, ΔFOV 4°, região igual
- PIO.Outbound → PIO.Listening: Δângulo 151°, Δposição 742.5 m, ΔFOV 15°, região muda
- PIO.Listening → PIO.Farewell: Δângulo 156°, Δposição 735.5 m, ΔFOV 10°, região muda

Verificações geométricas: **conformes**.
