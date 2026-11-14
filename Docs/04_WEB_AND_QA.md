# Web, performance e validação

Metas de projeto, não resultados medidos. A build nova ainda não existe. Plataforma: desktop, teclado/mouse, WebGL 2; celular fora do MVP. Unidade de sucesso: iniciar qualquer capítulo, compreender o objetivo e concluir em 5–7 min, inclusive para uma pessoa que não joga normalmente.

## Orçamentos iniciais e medição

| Medida | Meta inicial | Limite de ação / evidência |
| --- | --- | --- |
| Download comprimido dos três capítulos | até 45 MB | acima de 60 MB exige corte de assets antes de release; medir arquivos Brotli, não BuildReport.totalSize |
| Rede fria 10 Mbps | runtime pronto em até 45 s | registrar cache vazio, rede, navegador, máquina e instante de primeiro clique possível |
| Desenho interno | até 1280×720, escala leve 0,7 | CSS pode preencher o iframe sem renderizar em 4K |
| Hardware de referência | GPU integrada, 8 GB RAM, navegador atual | escolher dispositivo real e registrar; não inferir pelo notebook com GPU dedicada |
| Gameplay | 30 FPS sustentados na referência | medir frametimes, P95 e picos; média de FPS sozinha não aprova |
| Memória Unity em uso | buscar pico <384 MiB | também medir capacidade WASM, processos da aba e texturas; são grandezas diferentes |
| Recomeçar/trocar capítulo | 2 reinícios e 3 trocas | ausência de crescimento progressivo; liberar RTs, fotos, áudios e cenas |
| Geometria visível | referência 250 mil triângulos em gameplay / 400 mil em close | ajustar ao hardware; culling, LOD e draw calls antes de depender do número bruto |
| Texturas | cenário de 512–1024 px; modelo principal até 2048 px | orçamento de residência de aproximadamente 96 MiB; evitar texturas internas de GLB sem override |
| Luzes | 1 principal com sombra | fills sem sombras; sombras de 1024 px, com duas cascatas, alcance pequeno; perfil leve sem SSAO/DOF |
| Fotos/RT | 960×540, até 3 por capítulo; tela dividida até 640×180 por metade | nada de screenshot intermediário 4K, mipmaps quando necessários |
| Partículas | inicial 200–500 visíveis | testar overdraw da tempestade; reduzir antes de apagar leitura da poeira |

Os três cenários não ficam carregados juntos. Um único build inclui todo o conteúdo inicial; carregar cena assincronamente não elimina bytes desse download. Se a meta não couber, cortar história repetida, áudio longo, texturas e modelos; carregamento remoto por capítulo só numa segunda etapa com CORS/cache/erros testados.

## Perfil e builds

Fixar Unity/pacotes; WebGL 2 / IL2CPP / Release / OptimizeSize, threads desabilitadas no MVP. Comprimir texturas para desktop conforme suporte do navegador e registrar fallback em GPU que não aceite o formato. Brotli somente quando host entregar Content-Encoding correto; alternativa de descompressão precisa ser escolhida após teste real, não por suposição.

**Aprendizado da edição anterior:** trocar a plataforma ativa do Editor para WebGL, esperar importação/compilação e só então BuildPlayer. Validar que URP inclui o perfil Web e retém shaders de cor. BuildWindows com Editor em WebGL também exige troca concluída. Não concluir sucesso porque os shaders compilaram: capturar mundo opaco, rodas, solo e poeira no navegador.

glTF: validar materiais de normal, metal/roughness e escala. A convenção do shader pode exigir normal RGB, diferente do normal map repack da Unity; conferir visual antes de aplicar um importador global. Exportar modelo otimizado próprio com origem/alterações registradas; preservar original fora da dependência runtime. Sem mp4 ou câmera de laboratório detalhada carregada por engano.

Áudio inicia após clique; buses com limites e crossfades. Pausar ao perder foco, P/Esc e botão visível. Pointer lock/fullscreen são opcionais, dependem de gesto; retornar à aba exige clique explícito e limpa teclas retidas. Não iniciar som enquanto pessoa apenas lê a página itch.io.

## Sequência de aprovação

1. Primeiro build Web mínimo no sábado: dossiê + cena simples + E/hold + áudio + pausa. Provar pipeline antes dos modelos finais.
2. Cada PR de capítulo: fluxo de estados, interações, tempo, pausa/cancelamento, montagem e memória local.
3. Build integrada: três capítulos selecionáveis, reinício/menu e troca; nenhuma variável de evidência cruzando capítulos.
4. Chrome/Edge e Firefox: boot, conteúdo visível, teclado/mouse, áudio após clique, fullscreen, Esc/P, desfoco e retorno. Registrar quando o ambiente de automação não consegue produzir foco real; não declarar aprovado nesse caso.
5. 5 primeiras sessões por capítulo, preferencialmente pessoas que não jogam; quatro de cinco entre 300 e 420 s. Não impor cronômetro que conclua a ciência sozinho. Registrar pausas e tempo parado por confusão separadamente.
6. GPU integrada e rede fria; duas reinicializações completas e troca de todos os capítulos; comparar uso depois de estabilizar. Não confundir buffer WASM reservado com heap efetivamente usado.
7. Upload itch.io restrito para revisão; validar headers e iframe na URL real. ZIP com index.html na raiz, caminhos relativos e case-sensitive, sem serviços externos. Só então release final.

## Compreensão por capítulo

- Gêmeas: por que dois destinos; o que esmerilhar revelou; por que as evidências apontam para água; por que escudo/meteorito importam; diferença entre 2007/2018 e entre Spirit/Opportunity.
- Pioneer: cinturão real pouco denso; imagem por rotação e duas bandas; RTG e enfraquecimento de sinais; placa como mensagem; rota distante não é voo ao lado de Aldebaran.
- Surveyor: visão só depois do pouso; atraso de comando; teste do solo preparou Apollo; visita posterior recuperou partes; epílogo da câmera é imaginado.

Critério de bloqueio: qualquer erro de progressão, dado científico falso, câmera subterrânea, peça essencial flutuante, ação sem indicador ou áudio incapaz de ser reduzido. Qualidade estética/emoção exige revisão humana; teste automatizado não certifica filme ou compreensão.

## Referências técnicas oficiais

- https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/develop/memory
- https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/develop/audio
- https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/develop/texture-compression
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/buildplayeroptions/target
- https://itch.io/docs/creators/html5

Links consultados/previstos devem ser conferidos com a versão instalada em QA-001. Limites reais da plataforma precisam ser medidos no pacote final, sem prometer que cenas separadas significam download separado.
