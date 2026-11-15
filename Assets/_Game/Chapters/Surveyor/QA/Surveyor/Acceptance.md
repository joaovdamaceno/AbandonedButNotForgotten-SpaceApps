# Aceitação Surveyor — SUR-015

## Escopo e evidências

Entrada pelo dossiê próprio `SurveyorArchive.unity`, carregamento aditivo real de `SurveyorChapter.unity` e retorno ao arquivo. Não substitui a integração dos três capítulos. Fontes históricas: `Docs/Chapters/03_SURVEYOR_SOURCES.md`. Não há vídeo pré-renderizado, narração de IA, Game Over ou atividade por reflexo.

- `QA/SUR-015/tests.xml`: 8 testes aprovados no Unity 6000.6.7f1, D3D11: duas regressões de integração e seis de solo.
- `technical-journey.txt`: todas as 12 etapas acionadas pelas operações reais. Referência automatizada: aproximadamente 311 s de tempo ativo; não é mediana humana.
- `Chapter/Tests/SurveyorChapterTests.cs`: entrada pelo botão do dossiê, Core compartilhado, três registros, escavação, carga, impacto, imagens antes/depois, eclipse, noite, visita, foto NASA, memorial e descarte.
- Reinício real com unload/reload durante ordem pendente e primeira varredura; pausa de foco e retorno durante contato da pá. Relógio congelado na pausa, P não retoma pausa de foco, evidências antigas limpas e hosts anteriores destruídos. PT/EN e fonte ampliada aplicados na pausa e preferências restauradas.
- Todos os textos solicitados na jornada automatizada resolvem sem problemas de localização. Fotos de arquivo têm crédito separado da fotografia produzida na partida. O quadro de reconstituição imaginada antecede a visita de 1969.
- Capturas GPU do Editor revisadas: launch, first-picture, soil-comparison, eclipse, archive-photograph, memorial. Foram corrigidos textos vazios do formato legado, baixo contraste das legendas e uma pose externa indevida em FirstImage.

## Web

Relatórios, carregamento, memória disponível e capturas por navegador ficam em `QA/SUR-015/*-chapter.json`, `build-result.txt` e `*-*.png`. O driver usa teclado real no navegador, sem completar estados por API. E é mantido através do fim e exige nova pressão para retornar. Contextos de teste são isolados, sem perfil do usuário. Servidor local usa Brotli nativo e caminhos relativos.

## Revisão humana e dispositivos ainda pendentes

- Cinco primeiras partidas de novatos: duração e compreensão das perguntas do protocolo global.
- Notebook com GPU integrada; não inferir FPS a partir da RTX 3050 Laptop usada aqui.
- Memória total da aba/GPU, draw calls de cada estágio e estabilidade prolongada em máquinas de entrada. Métricas do heap WebAssembly não representam toda a memória da aba.
- Upload real itch.io e revisão em iframe/fullscreen com teclado e foco nativos de janela. Testes headless locais não certificam esses comportamentos.
- Revisão artística e musical por pessoas. Atores próximos são uma reconstrução leve; a foto NASA continua sendo a evidência documental.

A ausência desses dispositivos/pessoas é registrada como pendência, não como aprovação simulada. Falhas de fluxo conhecidas devem ser corrigidas antes da integração; polimento P1 fica em SUR-016 e SUR-017.
