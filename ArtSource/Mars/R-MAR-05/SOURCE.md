# R-MAR-05 — Marte, mapas orbitais e globo da abertura (MAR-013, revisada)

Consulta: 14/11/2026. Política de imagens do JPL (https://www.jpl.nasa.gov/jpl-image-use-policy/): uso com crédito, sem insígnia/logotipo da NASA ou do JPL e sem sugerir endosso.

| Arquivo original | Página / arquivo de origem | Crédito | SHA-256 do original |
| --- | --- | --- | --- |
| `original/Mars_Planet_24881.glb` (4,0 MB, 3.072 triângulos, 2 texturas 2048×1536) | https://science.nasa.gov/resource/planet-mars-3d-model/ — arquivo https://assets.science.nasa.gov/content/dam/science/psd/mars/resources/gltf_files/24881_Mars_1_6792.glb. A página não traz nota de licença própria além do crédito. | NASA/JPL-Caltech | `0d27fc65fe3e6a5847cf0cd3a5c951538afdd49bd0513607d8a70e8ab1d8d7ac` |
| `original/36899_20_Meridiani_Planum.jpg` (2589×1932) | https://science.nasa.gov/resource/meridiani-planum-2/ — arquivo https://assets.science.nasa.gov/content/dam/science/psd/mars/downloadable_items/3/36899_20_Meridiani_Planum.jpg | NASA/JPL-Caltech/Arizona State University | `78b2a6f55a449fa083607f6307d3c21297c8292bca0ef69130547447d8d4cb16` |
| `original/jpegPIA05346.jpg` (1387×3223) | PIA05346 "Gusev Crater" (THEMIS, Mars Odyssey; divulgada em 6/1/2004) — arquivo https://d2pn8kiwq2w21t.cloudfront.net/original_images/jpegPIA05346.jpg | NASA/JPL/Arizona State University | `8e658c43eae007a55d95df0d7f03e1ca155567410788dffbb0762b4844c691eb` |

Os dois primeiros arquivos já estavam na edição anterior do projeto; foram baixados de novo da NASA e os hashes conferem.

Derivados em `Assets/_Game/Chapters/Mars/Art/Origin/Textures/`:
- `Mars_Equirect.jpg` (2048×1024, q90): textura difusa do modelo reamostrada para projeção equirretangular por `Tools/mars_planet_equirect.py` (reprodutível byte a byte). Calibrada a olho (Hellas, Syrtis Major, Valles Marineris, Olympus Mons; erro de poucos graus). É ilustrativa: não serve para medir posições.
- `OrbitalMap_Meridiani.jpg` (1024 px de largura) e `OrbitalMap_Gusev.jpg` (1024 px de altura): LANCZOS, JPEG q88, sem recorte nem retoque.
