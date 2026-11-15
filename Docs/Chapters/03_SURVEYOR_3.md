# Surveyor 3 — documentário jogável

SUR-019, revisão pedida após jogar SUR-018. Encurta a operação, torna o monitor evidente sem explicá-lo na tela e corrige a marcha. O histórico anterior e seus previews permanecem no Git; não são a edição publicada.

## História

Mostrar por que a máquina foi à Lua, o que investigou e por que astronautas voltaram até ela. O filme funciona sem ler o dossiê. Em 1967, câmera e pá ajudaram a estudar resistência e coesão do solo; os testes locais não demonstram água nem certificam todo terreno lunar. Eclipse em 24 de abril, último contato em 4 de maio. Em novembro de 1969, Apollo 12 recuperou câmera, pá e outras peças para estudar a exposição ao ambiente lunar. A estrutura permaneceu. Fontes: 03_SURVEYOR_SOURCES.md.

| Etapa | Referência | Função |
|---|---:|---|
| Prologue | 23 s | Motivo da investigação antes das pessoas |
| Arrival | 31 s | Descida, descarte do retroestágio e contatos |
| Activation | 16 s | Máquina inteira → câmera usada pela equipe |
| Science | Sem espera mínima | Corte curto, pressão de 1,6 s e fotografia |
| Findings | 18 s | Registro do jogador e uso dos ensaios |
| Night | 38 s | Eclipse, elipse de dias, noite e último contato |
| Reunion | 44 s | Aproximação orientada, inspeção e recuperação |
| Legacy | 13 s | Fotografia histórica e estrutura que ficou |
| End | Fora do relógio | Retorno voluntário ao arquivo |

A montagem soma 183 s, contra 215 s da SUR-018. O pedido de reduzir a demora permite terminar abaixo dos cinco minutos com assistência; não preencher a meta anterior de 5–7 com espera. Estimativa do dossiê: 270 s. Medir duração/compreensão em primeiras partidas humanas. Sem cronômetro de falha.

## Operação e apresentação

Mouse aponta; clique e arrasto colocam a pá em contato. Soltar levanta. Segurar E oferece um corte assistido sem exigir WASD: o braço se move de verdade e o solo muda pelo contato/traçado, nunca por temporizador de conclusão. E + WASD permite ajustar. Exigir aproximadamente uma passada e parte da volta; retirar para cima antes de recolher. Um novo hold pressiona a borda e a foto registra a geometria desta partida.

Seta na área alcançável, progresso da atividade, solo exposto mais escuro, grãos, cursor de contato, estado textual e resposta mecânica dão feedback. Mostrar o botão de pressão apenas com o corte pronto. Percentual é andamento do jogo, não dado da missão. Pressão usa o apoio real da ferramenta, inclusive quando a vala cruza o ponto preferencial.

É controle direto assistido, simplificação aprovada da operação real. O atraso representativo de rádio pode ser explicado no arquivo; não atrasar mouse, interface ou pausa. Entrada por InputRouter, tempo por SessionClock, progresso por ChapterDirector.

Planos externos mostram a sonda e corpos completos. A câmera de instrumento permanece no conjunto óptico. A visita externa de 1969 é reconstrução documental, distinta da fotografia NASA; não sugerir que a câmera inativa gravou a recuperação.

Filme externo em cor contida e granulação leve. Instrumento 4:3 com moldura de TV, curvatura/overscan, vidro, varredura e brilho monocromático suave. Sem rótulo de efeito; intensidade reduzida nas configurações. Legendas e comandos nítidos após o filtro. Sem VHS, vento lunar ou voz de IA. Sons externos são linguagem cinematográfica.

Astronautas usam a malha inteira existente e pose neutra, com marcha procedural. Identificar a frente pelos ossos do pé, aplicar rumo após amostrar a pose e fixar o apoio no mundo enquanto o corpo avança. Braços oscilam suavemente; transições para inspeção e carga são graduais. Peças começam a retirada no próprio suporte. Nenhum membro isolado. Testar rumo/apoio e revisar movimento contínuo.

## Produção e aceitação

SurveyorChapterBuilder continua dono da cena e delega a montagem a DocumentaryBuilder. Composição, filme, elenco, pá, superfície e apresentação são componentes separados. Entrada pública: SurveyorArchive.unity; capítulo: SurveyorChapter.unity. Modelos/conversão e créditos em SourceAssets/Surveyor3/Documentary; validação atual em QA/SUR-019.

Avaliar se novatos conseguem começar com uma instrução, reconhecer progresso e explicar missão, ensaios, diferença 1967/1969 e recuperação das peças. Registrar tempos sem preencher duração por espera. Automação prova fluxo, pausa/foco, descarte e regressões. Arte, compreensão e GPU integrada precisam de avaliação humana identificada. Orçamento isolado: 25 MB; medir o conjunto na integração.
