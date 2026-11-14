# Registro de recursos — novo projeto

Fonte única de procedência e direitos. Regras: [Docs/Art/IMPORT_RULES.md](Docs/Art/IMPORT_RULES.md). Inventário e estado de recebimento: [Docs/Art/ASSET_AUDIT.md](Docs/Art/ASSET_AUDIT.md). Áudio: [Docs/Art/AUDIO_CUE_SHEET.md](Docs/Art/AUDIO_CUE_SHEET.md). Links de referência não representam assets baixados ou aprovados. Não há licença aberta global atribuída ao código original.

## Importados (no repositório)

| ID | Recurso | Autor / instituição | Origem (URL) | Acesso | Licença | Atribuição | Caminho local | SHA-256 do original | Alterações | Resp. |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| F-01 | Outfit Regular, SemiBold, Bold (TTF estáticos) | The Outfit Project Authors | https://github.com/Outfitio/Outfit-Fonts (`fonts/ttf/`, branch `main`) | 14/11/2026 | SIL OFL 1.1 (`OFL.txt` incluído ao lado) | “Outfit — Copyright 2021 The Outfit Project Authors, SIL Open Font License 1.1” | `Assets/_Game/Shared/Fonts/Outfit/` | Regular `3b64ac4f6ab6a8eebddd4b0bc03c811c43602e11e176382ab0ee6be615ab861b` · SemiBold `bf2e1d2a6ec2a67952e8b36edd2b2bb9f340c0cdd10b0ad5145b4dbbc1339608` · Bold `f620b69582e06d7e1b3bbde74ed8c5876eadabb038390780db2a3414a1490197` · OFL.txt `c676351bf8576b9aba743cd5eaa8c0e7ee0d51f805d720447b4df4ddb6a2e416` | Nenhuma (arquivos sem modificação) | R2 · UI-001 |
| F-02 | IBM Plex Mono SemiBold (TTF) | IBM Corp. (Reserved Font Name “Plex”) | https://github.com/google/fonts/tree/main/ofl/ibmplexmono | 14/11/2026 | SIL OFL 1.1 (`OFL.txt` incluído ao lado) | “IBM Plex Mono — Copyright © 2017 IBM Corp., SIL Open Font License 1.1” | `Assets/_Game/Shared/Fonts/IBMPlexMono/` | SemiBold `d3c38e55c78f5b0f28009fddba4834ec503278936a5986032424c9bd2d23aa46` · OFL.txt `7e6b2818edbd8f6a01ae80641cc8f16a51080d08fb4e532be3a0b6f74adb07da` | Nenhuma; nome reservado “Plex” preservado (sem versão modificada). `OFL.txt` tinha fim de linha CRLF e é normalizado para LF pelo `.gitattributes` (texto idêntico; o hash registrado é do arquivo baixado) | R2 · UI-001 |

As fontes são distribuídas dentro do build Web; a OFL permite incorporação em software, exige manter o aviso de copyright/licença (arquivos `OFL.txt`) e proíbe vender a fonte isoladamente. Os créditos do jogo listam as duas famílias.

## Planejados (não baixados nem importados)

| ID | Recurso | Origem verificada | Crédito na página | Estado | Verificação antes de importar | Resp. |
| --- | --- | --- | --- | --- | --- | --- |
| A-MER | Spirit/Opportunity 3D | https://science.nasa.gov/resource/spirit-and-opportunity-rover-3d-model/ — GLB `24883_MER_static.glb` 11,36 MB; USDZ 3,01 MB | NASA/JPL-Caltech | Página verificada 14/11/2026 · arquivo da equipe pendente | Texturas, escala (~1,5 m do mastro), peças móveis, decimação, marcas de terceiros nas texturas | R4 · MAR-011 |
| A-PIO | Pioneer 10 3D | https://science.nasa.gov/3d-resources/pioneer/ — `Pioneer 10.glb` 1,95 MB; espelho GitHub nasa/NASA-3D-Resources | NASA/JPL/Eyes on the Solar System | Página verificada 14/11/2026 · arquivo da equipe pendente | RTGs, antena 2,74 m, placa, eixo de spin | R5 · PIO-002 |
| A-SUR | Surveyor 3 | Sem modelo oficial localizado (1ª página do catálogo NASA 3D) | — | Não localizado; fallback é reconstrução própria | Modelo derivado de Surveyor 1 precisa de correções explícitas; não rotular como 3 sem revisão | R6 · SUR-001/002 |
| A-DSN | Antena 70 m | https://science.nasa.gov/3d-resources/70-meter-dish/ (listada no catálogo) | não lida | Candidato | Ler página do item | R5 · PIO-011 |
| A-A12 | Apollo 12 – Landing Site (catálogo NASA 3D) | https://science.nasa.gov/3d-resources/ | não lida | Candidato | Conteúdo e escala do item | R6 · SUR-002 |
| P-* | Fotos históricas (escudo, Ganimedes, Júpiter, Apollo 12 + Surveyor) | Ver ASSET_AUDIT “Referências” e matrizes de fontes dos capítulos | conforme página | Pendentes (PIO-007, MAR-015, SUR-013) | Crédito/data exatos; terceiros; resolução | R4/R6/R7 |
| S-KEN | Pacotes de som Kenney (Interface, Sci-fi, UI Audio, Impact, Digital Audio) | https://kenney.nl/assets/… | CC0 (atribuição não exigida) | Candidato | Licença incluída no download; arquivos escolhidos | R7 · AUD-002 |
| M-SB | Faixas Scott Buckley | https://www.scottbuckley.com.au/library/ | CC BY 4.0: “[Título] by Scott Buckley – released under CC-BY 4.0. www.scottbuckley.com.au” | Candidato | Página da faixa, escuta, cortes/loops registrados | R7 · AUD-002 |

Para cada importação registrar: nome, autor/instituição, URL específica, data de acesso, licença e atribuição, caminho local, hash original, transformações/otimizações e responsável. NASA: conferir a página de cada item e https://www.nasa.gov/nasa-brand-center/images-and-media/ ; não usar logotipo como endosso — insígnia, logotipo e identificadores NASA não são de domínio público. Fotos de hardware, dados científicos e capturas do jogo têm funções distintas. Reconstruções precisam de rótulo quando confundíveis com registros reais.

Áudio: somente original ou licenciado para distribuição/streaming previstos. Registrar alterações de duração, mix e loop; incluir atribuição no jogo e no pacote. Licença da gravação e composição precisam ser verificadas separadamente. A equipe pode reutilizar trechos já licenciados do projeto anterior após revisar seus créditos.
