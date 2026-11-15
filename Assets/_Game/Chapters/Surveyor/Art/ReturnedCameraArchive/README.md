# Câmera recuperada — SUR-017

Ficha opcional nos Detalhes de Surveyor 3 no arquivo próprio. Usa o mesmo modal/rolagem do dossiê e começa recolhida. Não é aberta pelo epílogo, não lê controles de missão e não inicia o SessionClock. Os textos PT/EN ficam em ReturnedCameraTexts.json; ReturnedCameraCard.asset guarda referências, imagem e links.

A imagem é a NASA AS12-48-7134 já utilizada no final, referenciada sem duplicar a textura. ReturnedCameraCover.asset é um sprite que aponta para a mesma textura 1024, também usado na pasta da missão. Não se baixou imagem do Smithsonian. O inventário A19761264000 identifica um exemplar de teste equivalente; a ficha o distingue da câmera de voo e não afirma exposição atual da pá.

Fontes consultadas em 15/11/2026:

- https://ntrs.nasa.gov/citations/19710024418 — estudo da câmera de voo retornada.
- https://www.jpl.nasa.gov/blog/2012/1/surveyor-3-camera-returned-from-the-moon — retorno e estudo em Terra; o link antigo da imagem retorna 404, por isso não foi importada.
- https://science.nasa.gov/resource/apollo-12-and-surveyor-3/ — visita, recuperação e foto NASA, já creditada no projeto.
- https://airandspace.si.edu/collection-objects/camera-television-surveyor/nasm_A19761264000 — artigo de teste; consulta direta automatizada respondeu 403, descrição institucional e matriz S3-12 distinguem a identidade.
- https://airandspace.si.edu/multimedia-gallery/image/2006-1139hjpg — foto da câmera retornada com direitos Smithsonian, não redistribuída.

Os links externos só abrem por clique do jogador; a ficha e o capítulo funcionam sem rede. Menu do Editor: Preparar ficha da câmera recuperada, seguido de Reconstruir capítulo completo. Batch reproduzível: Abandoned.SpaceApps.EditorTools.ReturnedCameraBuilder.BuildFromCommandLine. Web final: mesmo namespace, ReturnedCameraBuilder.BuildWeb, com -buildTarget WebGL.
