# SUR-017 — ficha da câmera e pacote final

Base: SUR-016 integrada em 18c5fb0. A ficha fica em Detalhes → Objeto recuperado, recolhida por padrão. Reutiliza foto NASA já incluída e cria apenas um sprite leve referenciando a textura; sem imagem de museu ou alegação de exposição atual da pá. A pasta passa a mostrar LUA · 1967–1969, o período narrado pelo capítulo.

Teste no Unity 6000.6.7f1: identidade/URLs/dados, entrada pelos Detalhes do dossiê, abertura manual, PT/EN, clock ainda zero e nenhuma evidência de partida criada. Capturas GPU em card-pt.png e card-en.png. A suíte tem um caso de integração; a primeira tentativa falhou no harness de teste por closure atravessando o reload de Play Mode, corrigido com callbacks estáticos e restauração de preferências. Não se ocultou falha de produto com salto de estado.

O build final e o percurso Web ficam nos arquivos deste diretório. Avaliação de novatos, GPU integrada, iframe/itch.io e foco de janela nativo continuam pendentes, conforme aceitação do capítulo. A revisão do assistente não substitui revisão humana R7.


## Pacote verificado

Build release IL2CPP/Brotli final: zero erros. Firefox 159, 1280×720, percurso por teclado de todas as etapas, 314,97 s ativos, E mantido através do fim e nova pressão para voltar ao arquivo, zero erros de runtime. Entrada localhost sem limitação de rede: 2,949 s; não é medida de download em internet lenta. WASM usado ao final 171.848.696 bytes, capacidade 186.580.992; não é pico da aba/GPU.

Chrome 159: cliques reais em Detalhes, expansão/rolagem da ficha e Escape ao arquivo; captura revisada e nenhuma etapa de capítulo iniciada. O setup usa contexto explícito, escala de dispositivo 1 e movimento do ponteiro antes do primeiro clique, como o driver do capítulo. Capturas finais do enquadramento da visita, memorial, eclipse, capa e retorno foram revisadas.

ZIP local: Builds/Surveyor3-itch.zip, 18.474.650 bytes, index.html na raiz, cinco arquivos, CRC verificado. Hash e inventário em package.json. O ZIP permanece fora do Git conforme AGENTS.md; os builders e todos os dados/fontes necessários estão versionados. As bibliotecas Python/Playwright servem somente à QA e não são necessárias para jogar.
