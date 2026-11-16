# Surveyor 3 — documentário jogável

SUR-020, revisão dos controles, pouso, eclipse e reencontro após jogar SUR-019. Encurta a operação, torna o monitor evidente sem explicá-lo na tela e corrige a marcha. O histórico anterior e seus previews permanecem no Git; não são a edição publicada.

## História

Mostrar por que a máquina foi à Lua, o que investigou e por que astronautas voltaram até ela. O filme funciona sem ler o dossiê. Em 1967, câmera e pá ajudaram a estudar resistência e coesão do solo; os testes locais não demonstram água nem certificam todo terreno lunar. Eclipse em 24 de abril, último contato em 4 de maio. Em novembro de 1969, Apollo 12 recuperou câmera, pá e outras peças para estudar a exposição ao ambiente lunar. A estrutura permaneceu. Fontes: 03_SURVEYOR_SOURCES.md.

| Etapa | Referência | Função |
|---|---:|---|
| Prologue | 23 s | Motivo da investigação antes das pessoas |
| Arrival | 34 s | Descida, descarte do retroestágio e contatos |
| Activation | 16 s | Máquina inteira → câmera usada pela equipe |
| Science | Sem espera mínima | Corte curto, pressão de 1,6 s e fotografia |
| Findings | 18 s | Registro do jogador e uso dos ensaios |
| Night | 38 s | Eclipse, elipse de dias, noite e último contato |
| Reunion | 44 s | Aproximação orientada, inspeção e recuperação |
| Legacy | 13 s | Fotografia histórica e estrutura que ficou |
| End | Fora do relógio | Retorno voluntário ao arquivo |

A montagem soma 186 s, contra 215 s da SUR-018. O pedido de reduzir a demora permite terminar abaixo dos cinco minutos com assistência; não preencher a meta anterior de 5–7 com espera. Estimativa do dossiê: 270 s. Medir duração/compreensão em primeiras partidas humanas. Sem cronômetro de falha.

## Operação e apresentação

A primeira etapa mostra apenas **Clique e arraste**. O mouse aponta e o arrasto move a pá; soltar levanta. Exigir aproximadamente uma passada e parte da volta, com geometria alterada pelo contato real. Quando a vala estiver pronta, soltar avança para **Segure E**. A pá encontra apoio na borda e pressiona por 1,6 s de contato. Depois recolhe e registra automaticamente a geometria desta partida. E não opera o corte; o mouse não opera a pressão.

Seta na área alcançável, progresso da atividade, solo exposto mais escuro, grãos, cursor de contato e som mostram o resultado. Progresso é andamento do jogo, não dado da missão. Pausa, perda de foco e troca de contexto cancelam contato; cliques na interface não escavam.

É controle direto assistido, simplificação aprovada da operação real. O atraso representativo de rádio pode ser explicado no arquivo; não atrasar mouse, interface ou pausa. Entrada por InputRouter, tempo por SessionClock, progresso por ChapterDirector.

Planos externos mostram a sonda e corpos completos. A câmera de instrumento permanece no conjunto óptico. A visita externa de 1969 é reconstrução documental, distinta da fotografia NASA; não sugerir que a câmera inativa gravou a recuperação.

Filme externo em cor contida e granulação leve. Instrumento 4:3 com moldura de TV, curvatura/overscan, vidro, varredura e brilho monocromático suave. Sem rótulo de efeito; intensidade reduzida nas configurações. Legendas e comandos nítidos após o filtro. Sem VHS, vento lunar ou voz de IA. Sons externos são linguagem cinematográfica.

Astronautas usam a malha inteira existente e pose neutra, com marcha procedural. Identificar a frente pelos ossos do pé, aplicar rumo após amostrar a pose e fixar o apoio no mundo enquanto o corpo avança. Braços oscilam suavemente; transições para inspeção e carga são graduais. Peças começam a retirada no próprio suporte. Nenhum membro isolado. Os alvos dos braços usam o lado anatômico importado e ficam limitados ao alcance; a posição de trabalho fica perto da peça. A câmera é transportada pelo ponto de pega, preservando sua orientação. Testar rumo/apoio e revisar movimento contínuo.

## Produção e aceitação

SurveyorChapterBuilder continua dono da cena e delega a montagem a DocumentaryBuilder. Composição, filme, elenco, pá, superfície e apresentação são componentes separados. Entrada pública: SurveyorArchive.unity; capítulo: SurveyorChapter.unity. Modelos/conversão e créditos em SourceAssets/Surveyor3/Documentary; validação atual em QA/SUR-020.

Avaliar se novatos conseguem começar com uma instrução, reconhecer progresso e explicar missão, ensaios, diferença 1967/1969 e recuperação das peças. Registrar tempos sem preencher duração por espera. Automação prova fluxo, pausa/foco, descarte e regressões. Arte, compreensão e GPU integrada precisam de avaliação humana identificada. Orçamento isolado: 25 MB; medir o conjunto na integração.

## Pouso e eclipse na SUR-020

O retrofoguete é descartado e três verniers continuam a frenagem final. Jatos estreitos, sem fumaça atmosférica; grãos deslocados seguem arcos balísticos. O plano aberto inclui os dois ressaltos históricos, de aproximadamente 10 m e 3 m, e três contatos cada vez menores. Arcos usam 1,62 m/s²; tempos, impulso visual e vibração são encenação, sem reproduzir telemetria ou integrar a dinâmica real dos verniers. A sonda também desliza encosta abaixo (cerca de 20 m e 11 m entre os toques, relato secundário; ver 03_SURVEYOR_SOURCES.md) e a câmera a acompanha com atraso de operador. Fontes S3-01/03/04.

O eclipse mostra ingresso, ocultação e recuperação do Sol, com arco refratado irregular mais forte ao norte. A superfície escurece temporariamente e volta a clarear antes da elipse de dias para a noite lunar. Datas mudam na própria imagem; o eclipse de 24/04 não é apresentado como o fim da missão em 04/05. Fontes S3-05/06.
