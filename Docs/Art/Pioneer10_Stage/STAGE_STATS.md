# PIO-003 · Palco espacial da Pioneer 10 — medições da prévia

Gerado por `PioneerStageCapture` (Editor, batchmode). **Estimativas** por renderer cujo AABB intersecta o frustum,
sem batching: não são números do profiler nem da build Web. Metas do roteiro: < 120 draw calls de conteúdo e
≤ 150 mil triângulos visíveis nos planos de Júpiter; uma luz principal sem sombra + um fill sem sombra.

| Anchor | Estado | Região | FOV | Renderers | Triângulos | Draw calls (est.) | Luzes ativas | Com sombra | Folga da sonda |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `PIO.Launch.Reveal` | PIO.Launch | DeepSpace | 34° | 10 | 13750 | 10 | 2 | 0 | 5.06 m |
| `PIO.Launch.Pad` | PIO.Launch | LaunchSite | 50° | 11 | 3776 | 11 | 2 | 0 | — |
| `PIO.Launch.Ellipse` | PIO.Launch | DeepSpace | 42° | 10 | 13750 | 10 | 2 | 0 | 501.32 m |
| `PIO.Launch` | PIO.Launch | DeepSpace | 40° | 10 | 13750 | 10 | 2 | 0 | 4.84 m |
| `PIO.Belt` | PIO.Belt | DeepSpace | 30° | 10 | 13750 | 10 | 2 | 0 | 74.51 m |
| `PIO.Belt.Sensor` | PIO.Belt | DeepSpace | 40° | 10 | 13750 | 10 | 2 | 0 | 4.75 m |
| `PIO.JupiterApproach` | PIO.JupiterApproach | DeepSpace | 40° | 11 | 17718 | 11 | 2 | 0 | 6.33 m |
| `PIO.JupiterApproach.Instrument` | PIO.JupiterApproach | DeepSpace | 38° | 10 | 13750 | 10 | 2 | 0 | 0.97 m |
| `PIO.Imaging` | PIO.Imaging | DeepSpace | 46° | 7 | 15722 | 7 | 2 | 0 | 1.38 m |
| `PIO.Closest` | PIO.Closest | DeepSpace | 55° | 3 | 7216 | 3 | 2 | 0 | 204.66 m |
| `PIO.Plaque` | PIO.Plaque | DeepSpace | 36° | 9 | 16654 | 9 | 2 | 0 | 0.76 m |
| `PIO.Outbound` | PIO.Outbound | DeepSpace | 40° | 10 | 13750 | 10 | 2 | 0 | 15.91 m |
| `PIO.Listening` | PIO.Listening | GroundStation | 55° | 16 | 36560 | 16 | 2 | 0 | — |
| `PIO.Farewell` | PIO.Farewell | DeepSpace | 45° | 11 | 13870 | 11 | 2 | 0 | 7.98 m |

Maior coordenada de anchor: 536 m (limite 1000 m).
Rotação visual de cruzeiro: 24°/s (valor de direção, não telemetria).

## Continuidade entre planos consecutivos

- PIO.Launch.Reveal → PIO.Launch.Pad: Δângulo 102°, Δposição 745.4 m, ΔFOV 16°, região muda
- PIO.Launch.Pad → PIO.Launch.Ellipse: Δângulo 51°, Δposição 523.5 m, ΔFOV 8°, região muda
- PIO.Launch.Ellipse → PIO.Launch: Δângulo 52°, Δposição 497.1 m, ΔFOV 2°, região igual
- PIO.Launch → PIO.Belt: Δângulo 149°, Δposição 86.7 m, ΔFOV 10°, região igual
- PIO.Belt → PIO.Belt.Sensor: Δângulo 78°, Δposição 80.2 m, ΔFOV 10°, região igual
- PIO.Belt.Sensor → PIO.JupiterApproach: Δângulo 105°, Δposição 13.6 m, ΔFOV 0°, região igual
- PIO.JupiterApproach → PIO.JupiterApproach.Instrument: Δângulo 101°, Δposição 12.9 m, ΔFOV 2°, região igual
- PIO.JupiterApproach.Instrument → PIO.Imaging: Δângulo 74°, Δposição 4.4 m, ΔFOV 8°, região igual
- PIO.Imaging → PIO.Closest: Δângulo 34°, Δposição 211.1 m, ΔFOV 9°, região igual
- PIO.Closest → PIO.Plaque: Δângulo 39°, Δposição 210.3 m, ΔFOV 19°, região igual
- PIO.Plaque → PIO.Outbound: Δângulo 174°, Δposição 19.1 m, ΔFOV 4°, região igual
- PIO.Outbound → PIO.Listening: Δângulo 151°, Δposição 742.5 m, ΔFOV 15°, região muda
- PIO.Listening → PIO.Farewell: Δângulo 156°, Δposição 735.5 m, ΔFOV 10°, região muda

Verificações geométricas: **conformes**.
