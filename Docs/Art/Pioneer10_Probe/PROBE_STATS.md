# PIO-002 · Sonda Pioneer 10 — medições do prefab

Gerado por `PioneerProbeCapture` a partir de `Art/PioneerActors.prefab`. Medidas de caixa (AABB) em metros, no espaço do `Spin_Root`.

- 8199 triângulos · 2 material(is) · refletor 2.740 m
- Meta do LOD0: até 60 mil triângulos; materiais: até 3.
- Lod1: 2303 triângulos (28 % do LOD0); caixa difere do LOD0 em 0.000/0.006/0.017 m
- Dish: distância até o corpo/refletor 0.001 m
- Feed: distância até o corpo/refletor 0.001 m
- RtgBoomA: distância até o corpo/refletor 0.006 m
- RtgBoomB: distância até o corpo/refletor 0.027 m
- MagnetometerBoom: distância até o corpo/refletor 0.005 m
- Plaque: 8 de 8 cantos a ≤ 0.030 m dos suportes; menor distância 0.009 m

## Custo Web estimado (não medido no navegador)

- Atlas 2048×2048 em DXT1 (override WebGL ligado), mipmaps ligados: ≈ 2.7 MiB de textura residente (orçamento total ≈ 96 MiB).
- Malhas (LOD0 + LOD1 + placa): 10375 vértices ≈ 386 KiB sem compressão de malha (a compressão média do build reduz isso).
- Sem download em runtime: atlas, malhas e materiais estão em `Assets/` e entram no pacote da cena; o original (`ArtSource/`) não vai para o build.

Verificações: **conformes**.
