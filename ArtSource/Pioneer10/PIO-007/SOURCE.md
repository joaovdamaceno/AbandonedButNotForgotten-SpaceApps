# Fotos históricas da Pioneer 10 (PIO-007)

Registro legível da procedência. O registro completo, com hashes e medidas, está em `Data/Assets/PioneerHistoricalImages.json` (gerado por `Tools/convert_pioneer10_photos.py`).

Baixado em **14/11/2026**, direto das páginas oficiais da NASA, sem intermediários. Crédito de ambas as páginas: **"Credit: NASA"**. Nenhuma das duas traz licença própria; valem as diretrizes de imagens e mídia da NASA (P-S18): reconhecer a NASA como fonte, sem sugerir endosso e sem logotipo.

## Ganimedes (R-PIO-03, P-S09)

| Campo | Valor |
| --- | --- |
| Página | https://science.nasa.gov/resource/pioneer-10-ganymede/ — "Pioneer 10 – Ganymede", 3 de dezembro de 1973, crédito NASA (página atualizada em 27/10/2024; download de 7/9/2023) |
| Arquivo original | `original/Pioneer_10_Ganymede_2679_AC73-9279.jpg` — https://assets.science.nasa.gov/content/dam/science/psd/solar/2023/09/p/i/o/Pioneer_10_Ganymede_2679_AC73-9279.jpg |
| Conferência | 651.345 bytes = **636,08 KB**, o tamanho anunciado na página |
| SHA-256 | `ddee488aadd1007a1162f6d979971add063111260f696d7140eda9c38e1f53fe` |
| Dimensões originais | 2048 × 3072 px (scan de cópia fotográfica, com margens cinza em cima e embaixo) |
| Data | 3/12/1973 é a data exibida na página (encontro mais próximo); o horário exato da imagem não é informado |
| Versão Web | `Assets/_Game/Chapters/Pioneer10/Art/Historical/Pioneer10_Ganymede_1973_web.jpg`, 1024 × 1024 |
| Alterações | recorte quadrado 2048 × 2048 em torno do disco (descarta as margens do scan), redução 2:1 por Lanczos, JPEG q88. **Nada foi ampliado, nitidificado ou "restaurado"** |

A imagem é de baixíssima resolução e fora de foco. É exatamente o que a Pioneer 10 registrou, e a baixa resolução faz parte do fato (roteiro, `PIO.04.08`).

## Júpiter (R-PIO-02, P-S10)

| Campo | Valor |
| --- | --- |
| Página | https://science.nasa.gov/resource/pioneer-10-at-jupiter/ — "Pioneer 10 at Jupiter", crédito NASA (página de 13/2/2018, atualizada em 12/5/2025). O texto da página fala de 4 de dezembro de 1973 |
| Arquivo original | `original/668761main_ac73-9341.jpg` — https://assets.science.nasa.gov/content/dam/science/psd/solar/2023/09/6/668761main_ac73-9341.jpg |
| SHA-256 | `a4d9e5c428849cd4cd956d048e9f25f11f488ec23f98bfb97bd2fb40f8829e0b` |
| Dimensões originais | 2181 × 1693 px, 424.505 bytes |
| Natureza | **Montagem sequencial** de imagens da aproximação (do ponto ao disco grande e, depois do ponto mais próximo, a crescente). Não é uma foto única; o instante de cada quadro não é informado |
| Versão Web | `.../Art/Historical/Pioneer10_Jupiter_1973_web.jpg`, 1024 × 795 |
| Alterações | quadro inteiro, redução de 2181 para 1024 px por Lanczos, JPEG q88. Nada foi ampliado ou filtrado |

A página oferece uma segunda ligação (`668761main_ac73-9341-1.jpg`); ela é **idêntica byte a byte** à primeira (mesmo SHA-256).

## Verificação feita e pendente

- Feito: identifiquei as duas imagens como produtos da Pioneer 10 pelas páginas oficiais, confirmei o tamanho anunciado de Ganimedes, comparei original e versão Web a olho (disco centralizado e sem margens; montagem inteira) e conferi hashes e dimensões (`python Tools/convert_pioneer10_photos.py --check` e o teste `PioneerHistoricalImagesTests`).
- **Pendente (R7):** comparar imagem e fonte lado a lado e conferir o horário e a legenda de cada quadro em fontes primárias do arquivo (por exemplo o relatório da missão). A página da NASA não dá o instante de aquisição.
- A Io perdida **não** tem imagem aqui: `PIO.04.07` só a menciona.
