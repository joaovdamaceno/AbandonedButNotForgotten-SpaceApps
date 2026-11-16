# Capítulo II — Pioneer 10: quem vai primeiro

> **Roteiro v2 (15/11/2026), em fases.** Um roteiro novo de 9 cenas e só 2 interações foi recebido; as legendas PT-BR e EN-UK e o estado `PIO.Closest` (cena 5) já o seguem. Este documento continua sendo o registro de estados, interações e contratos; a tabela abaixo ainda descreve **oito** estados com 375 s, e o capítulo agora tem **nove**, com 415 s. O mapeamento, as trocas de frase e as fases estão em [`PioneerV2/FASES.md`](PioneerV2/FASES.md).

**Documento de implementação, não funcionalidade pronta.** Planejado em 14/11/2026. Proprietário de código/cena: R5. Revisão científica, áudio e QA: R7. Integração dos módulos comuns: R1. Leia o contexto mestre do repositório antes de executar uma tarefa. Fontes e decisões científicas: [02_PIONEER_SOURCES.md](02_PIONEER_SOURCES.md).

## Intenção, duração e percurso exato

Uma máquina pequena abre um caminho imenso. O capítulo troca o medo de um espaço cheio de obstáculos pela descoberta de como ele realmente é. Em seguida, transforma luz em imagem, apresenta uma mensagem humana e deixa o jogador escutar os últimos sinais. O último gesto é de escuta, não de derrota.

O jogador começa no dossiê, escolhe Pioneer 10 e clica Iniciar. A partir daí, o tempo ativo nominal é **375 s / 6min15s**, incluindo legendas e interações; arquivo, carregamento e pausa ficam fora. O intervalo de aprovação é 300–420 s na primeira experiência humana. Não encerrar atividade pelo relógio nem inferir desempenho humano de uma automação. O registro de tempo é global, e o roteiro de referência é uma única sequência de estados, não oito vídeos.

| Estado estável | Janela nominal | Duração | O que o jogador faz / aprende | Saída obrigatória |
| --- | --- | ---: | --- | --- |
| PIO.Launch | 00:00–00:40 | 40 s | Reconhece sonda, orientação e RTGs; segura E no cartão do gerador | Introdução lida e demonstração de energia concluída |
| PIO.Belt | 00:40–01:38 | 58 s | Experimenta 15 s de imaginação, depois registra cinco sinais de poeira | Cinco registros reais estilizados, sem dano |
| PIO.JupiterApproach | 01:38–02:05 | 27 s | Vê a escala de Júpiter e a demora do rádio | Revelação e explicação terminadas |
| PIO.Imaging | 02:05–03:19 | 74 s | Monta oito faixas, aplica comando de recuperação e vê fotos históricas | 8/8 faixas, comando confirmado e duas imagens reveladas |
| PIO.Plaque | 03:19–04:06 | 47 s | Encaixa cinco símbolos de uma mensagem | Cinco partes identificadas, sem pontuação |
| PIO.Outbound | 04:06–04:45 | 39 s | Acompanha distância e redução de potência; prioriza transmissão | Economia confirmada, sem desligamento fatal por erro |
| PIO.Listening | 04:45–05:37 | 52 s | Sintoniza três contatos e presencia uma quarta tentativa sem resposta | Três contatos + janela final de silêncio concluídos |
| PIO.Farewell | 05:37–06:15 | 38 s | Entende que perder comunicação não interrompe a trajetória | Memorial e tela de conclusão |
| **Total** | | **375 s** | Um capítulo completo e independente dos outros | Voltar ao arquivo / Repetir |

No orçamento existem aproximadamente 160–180 s de participação direta e 195–215 s de introduções, leitura, demonstrações e contemplação. Uma legenda pode atravessar um gesto simples, mas não disputar a atenção com uma instrução nova. A compreensão vale mais que aumentar artificialmente o número de cliques.

## Controles, assistência e contratos

- **E:** toque para abrir uma atividade; solte e então segure para executar. A atividade não começa com o mesmo key-down que a abriu. Soltar pausa o progresso sem retirar resultado já adquirido.
- **Setas ou WASD:** deslocamento bidimensional somente na fantasia do cinturão; seleção de peça/encaixe na placa; ajuste horizontal de sintonia na escuta. Mouse e botões grandes são equivalentes, nunca obrigatórios.
- **Enter/E:** confirmar a escolha focada, conforme o cartão. Esc/P/botão de pausa pertencem ao shell global, não a scripts do capítulo.
- **Ajuda automática:** após 6 s sem uma ação compreendida, pulsar o controle e mostrar uma frase literal; após 12 s, oferecer no mesmo foco “Ajuda de alinhamento”. A ajuda aproxima a seleção da solução e amplia a tolerância, mas o jogador ainda segura/confirma. Não passar cenas automaticamente por inatividade.
- Inputs são capturados pelo contexto ativo. Uma confirmação não avança também a próxima tela; mudar estado cancela holds, tweens, ruído e botões antigos. Controle de rádio só existe em PIO.Listening, sem simular um comando físico recebendo resposta instantânea da sonda.
- Objetivos mostram verbo + objeto + progresso, em fonte de leitura normal. O menu nunca exige que a pessoa decifre telemetria ou descubra um hotspot invisível.

## Direção comum aos oito estados

Paleta: metal marfim, dourado opaco, preto azulado e Júpiter em ocre/creme. A fantasia tem moldura editorial ciano e selo permanente **IMAGINAÇÃO / IMAGINATION**; o restante usa o mesmo sistema gráfico dos demais capítulos. A estética será de uma ilustração científica cinematográfica, com materiais plausíveis e composição deliberada.

Uma câmera 3D principal; o quadro de imagem é UI, não uma segunda câmera 3D. Cinematics em tempo real. Cortes mudam informação: escala, instrumento ou consequência. Evitar consecutivos planos quase iguais. Plano de 3–8 s com ponto focal claro; movimento reduzido substitui órbitas por dolly curto e retira vibração. Não piscar o ecrã por radiação; ruído localizado de baixa amplitude, sem estrobo.

Música em quatro camadas/cues licenciados: **Partida** (pulso e crescimento), **Curiosidade** (timbres leves para ciência), **Distância** (sustentação delicada para placa/energia) e **Escuta** (piano/cordas com silêncio estruturado). Crossfade de 1,5–2 s entre estados. Espaço não tem som propagado no vácuo: bipes são interface e a música é linguagem narrativa. Sem vento contínuo, helicóptero, locução de IA ou gravação histórica sem licença.

## Cena 1 — Partir do conhecido · 40 s

**Objetivo de direção:** sair do peso e do ruído da Terra para a rotação silenciosa de uma sonda. O lançamento é um único impulso de energia, não uma longa introdução antes de interagir.

| Plano | Tempo local | Enquadramento / ação / luz | Função e continuidade |
| --- | --- | --- | --- |
| PIO01.A | 0–7 s | Preto revela contorno real do refletor. Contraluz fria; título Pioneer 10 discreto. Não construir outra sala de laboratório | Identificar a protagonista |
| PIO01.B | 7–17 s | Plano baixo lateral do Atlas-Centaur real, noite; chama quente ilumina a estrutura. Pequena vibração opcional; fumaça cinza com limite de alpha | Mostrar o instrumento sendo lançado, sem esconder toda a imagem por bloom |
| PIO01.C | 17–26 s | Corte por movimento para foguete como traço, elipse de 11 horas. A Lua aparece pequena ao lado de órbita gráfica, sem fingir um encontro próximo | Comunicar rapidez e tempo comprimido |
| PIO01.D | 26–40 s | Sonda livre no espaço; eixo de rotação estável. Linhas discretas apontam antena e RTGs. Cartão “Segure E para acompanhar a geração de energia” | Primeiro hold guiado, 3 s, mostra calor→eletricidade; legenda explica função |

**Fidelidade:** cartão de data “2 MAR 1972 · FLÓRIDA / 3 MAR · UTC”; não misturar data local com hora UTC. RTGs não abrem como painéis solares. Desdobramento de hastes só onde a referência de lançamento o comprovar; se a documentação não sustentar a animação, cortar da carenagem para configuração de cruzeiro com elipse temporal. Antena não se mantém apontada para a Terra apenas pela rotação: correções de atitude continuam existindo.

**Fallback do fim de semana:** Atlas em silhueta baseado em desenho oficial, plataforma sugerida por quatro peças e quads de emissão; nenhuma construção detalhada. A câmera próxima da sonda usa o GLB NASA quando disponível. Se o arquivo ainda não chegou, usar malha simplificada por medidas e fotografia oficial, explicitamente marcada como asset provisório na tarefa; não bloquear todo o estado.

## Cena 2 — O cinturão não era um labirinto · 58 s

**0–12 s:** entra o mapa simples Sol–Marte–Júpiter, data de entrada e pergunta sobre partículas desconhecidas. Não inserir asteroides em primeiro plano antes de estabelecer a fantasia.

**12–27 s, fantasia:** moldura de caderno se fecha, selo IMAGINAÇÃO fica visível em PT/EN. Setas grandes ensinam uma única direção por vez; três pedras grandes caminham devagar. O jogador pode deslocar um ícone da sonda ou ficar parado. Contato causa um “toc” e leve deformação gráfica, não explosão, perda de energia ou repetição. Esse trecho dura 15 s, mesmo sem habilidade; sua conclusão é narrativa e não exige escapar.

**27–40 s, correção:** a moldura se recolhe, não estilhaça cobrindo a tela. Corte a um plano longo quase vazio com a sonda pequena; somente pontos distantes representam asteroides. O texto informa o contraste com a brincadeira anterior. Não retratar a passagem real como pilotagem de videogame, nem afirmar que não existe poeira perigosa.

**40–58 s, registro:** painel de instrumento mostra cinco eventos grandes em fila, todos visíveis, não grãos escondidos no cenário. O ativo pulsa com número 1–5. Clique/toque de E registra o evento focado; segurar E mantém a aquisição, um evento por 2 s, total mínimo de 10 s. Um pequeno brilho na posição do sensor indica evento estilizado. Não exigir acertar um objeto em movimento. Após cinco registros, calendário avança para fevereiro de 1973 e a sonda continua inteira. Número cinco é amostragem de gameplay, não quantidade histórica de impactos.

## Cena 3 — Um mundo que enche o quadro · 27 s

**PIO03.A, 0–9 s:** Júpiter cresce de ponto a disco, sem zoom contínuo por uma distância astronomicamente literal. Data “NOV–DEZ 1973”. A câmera cruza para o mesmo lado da antena e trava o eixo de luz, impedindo um salto de orientação.

**PIO03.B, 9–19 s:** plano aberto com o planeta ocupando dois terços do quadro e a Pioneer num terço inferior. A massa de Júpiter aparece pelo enquadramento, não por planetas gigantes colados ao veículo. O texto explica atraso de dezenas de minutos. O indicador Terra→sonda→Terra é uma animação editorial de 3 s; não um relógio operacional falso.

**PIO03.C, 19–27 s:** corte ao fotopolarímetro, feixe em traço fino aponta para o planeta e se transforma na borda do quadro do minigame. A câmera para antes que apareça a primeira instrução. A sonda gira; Júpiter não gira a velocidade absurda para simular a varredura.

## Cena 4 — Construir uma imagem · 74 s

**0–16 s, aprender:** esquerda com sonda/linha de visada em movimento lento; direita com a fotografia de referência ocultada por oito faixas. O cursor de fase tem uma zona larga e contorno que não depende só de cor. Prompts: “Segure E quando a faixa iluminada atravessar Júpiter”. Um hold contínuo é aceito: o sistema registra na próxima janela válida. Não exigir toques sincronizados nem penalizar um hold antecipado.

**16–48 s, oito registros:** quatro faixas formam o canal vermelho, quatro o azul; cada faixa leva aproximadamente 3 s e os tempos de explicação ocupam as lacunas. O controlador representa um conjunto de linhas por faixa, não oito linhas históricas. Rótulos permanentes “Canal vermelho / Canal azul”. Na composição, canal verde é **estimado** a partir do processamento; não alegar que vermelho+azul produz cor verdadeira sozinho.

**Interrupção após a faixa 6:** somente o quadro do instrumento apresenta ruído leve; o menu principal continua legível. Uma notificação explica “Radiação: comando indevido no instrumento”. Pausa a aquisição; o foco vai ao botão “Enviar sequência de recuperação”. Um toque de E confirma. Animação mostra um pacote enviado e elipse temporal — o botão não faz um sinal real atravessar dezenas de minutos em um segundo. Retoma no mesmo progresso, com as últimas duas faixas. Não trocar erro de instrumento por um computador moderno inventado.

**48–74 s, ciência e foto:** composição aparece em 2 s; crossfade de 1 s para **imagem real da Pioneer 10**, crédito e ano no rodapé. Em seguida, foto real de Ganimedes, na resolução modesta histórica. Não usar imagem de Voyager/Juno e carimbar 1973. A legenda distingue a imagem perdida de Io dos resultados recebidos. Um cartão curto menciona campos magnéticos e radiação; a arte do campo fica no dossiê, para não sobrecarregar a revelação.

**Sem falha:** perder uma janela preserva o preenchimento e oferece outra volta; com E segurado todas as janelas seguintes entram automaticamente. Após 12 s de dificuldade, modo assistido alarga a janela a toda a passagem sobre o planeta. Nunca simular dano permanente, Game Over ou perda do resultado por errar.

## Cena 5 — Uma apresentação da humanidade · 47 s

**0–9 s:** Jupiter sai do fundo em continuidade com a trajetória. A câmera chega à placa **presa aos suportes reais da antena**. Metal dourado opaco, reflexo controlado; não uma placa de ouro maciço solta no espaço.

**9–38 s:** placa simplificada em traço dourado com cinco contornos visíveis. Toda peça já vem associada a um símbolo e um nome; não é um teste de ler código alienígena. Cinco partes: hidrogênio, mapa de pulsares, Sistema Solar, escala da Pioneer, pessoas. Desenhos humanos com silhueta lisa e sem detalhes anatômicos, conforme a direção da equipe; crédito “adaptação educativa da placa” no registro, sem fingir ser reprodução fiel da gravura. As informações científicas dos símbolos são preservadas.

Com mouse, arrastar até a região ou clicar peça→contorno. Com teclado, setas percorrem peças restantes; E seleciona, setas percorrem contornos, E encaixa. Snap de 96 px a 720p; contorno correto pulsa após seleção; contorno errado devolve a peça com mensagem neutra. Alternativa assistida: segurar E encaixa a peça focada no contorno já indicado, em 2 s. Não repetir uma longa explicação depois de cada erro.

Cada sucesso revela uma frase de 3–4 s, enquanto o próximo item já está pronto. Cinco peças não podem exigir mais de 30 s de execução assistida. A UI “3/5” serve para progresso, sem estrelas, combo ou nota. A placa completa continua presa à sonda ao sair da atividade.

**38–47 s:** revelar placa inteira, devolver escala humana ao refletor. A música retira o pulso e fica delicada. A frase sobre fazer uma apresentação se refere ao propósito da placa, não a um encontro já ocorrido.

## Cena 6 — O caminho continua · 39 s

Três composições, não seis planetas modelados. **0–12 s:** diagrama orbital com arco de saída e cartão 1976. O cruzamento da distância orbital de Saturno não é um sobrevoo do planeta; não colocar Saturno enorme ao lado. Uma anotação sobre a extensa cauda magnética de Júpiter pode aparecer no arquivo opcional.

**12–24 s:** cartão 13 JUN 1983, cruzamento da distância orbital de Netuno; explique “além das órbitas dos planetas”, não “fora de todo o Sistema Solar” nem “atravessou a heliopausa”. Urano/1979 é contexto opcional no dossiê para não congestionar o filme.

**24–39 s:** o calendário avança até 1998. O HUD de geração mostra barras sem números inventados; o jogador segura E por 3 s para priorizar comunicação, iluminando o símbolo do rádio e apagando instrumentos secundários. É uma síntese dos comandos da equipe ao longo dos anos, não uma lista exata de desligamentos daquele dia. Um gráfico de distâncias com duas linhas separadas apresenta Voyager 1 ultrapassando **a distância ao Sol**, não encontrando a Pioneer; nenhuma segunda sonda 3D é necessária. Mudar a referência espacial sem rótulo é proibido. O encerramento da rotina científica em 1997 por questões orçamentárias pertence ao arquivo, sem sugerir que ocorreu por uma falha elétrica naquele ano.

## Cena 7 — Escutar até o silêncio · 52 s

A janela passa do diagrama ao disco de uma antena terrestre, céu noturno. Ponto de vista: equipe na Terra. Não reproduzir uma sala de controle complexa; uma silhueta de antena referenciada em fotografia e painel com a forma da onda bastam.

| Trecho | Tempo local | Ação | Retorno |
| --- | --- | --- | --- |
| Ensinar | 0–7 s | Cartão “← → para sintonizar · E para escutar”; knob grande e foco visível | O indicador de proximidade aparece antes do primeiro ajuste |
| Contato 1 | 7–16 s | Setas/mouse movem o cursor para zona de 30% da escala; segure E 2 s | 3 MAR 2002 · sinal recebido, aniversário de 30 anos |
| Contato 2 | 16–25 s | Zona ligeiramente deslocada, mesma largura; segure E 2 s | 27 ABR 2002 · últimos dados de telemetria |
| Contato 3 | 25–37 s | Volume menor e sinal visual mais fraco; zona ainda legível | 23 JAN 2003 · sinal de rádio muito fraco; não declarar dados novos |
| Tentativa 4 | 37–45 s | Comando “Segure E para ouvir”; não há zona escondida nem frequência correta | 7 FEV 2003 · nenhum sinal detectado, sem erro/alarme de falha do jogador |
| Respirar | 45–52 s | Controles recolhem; 2,5 s de silêncio real, sem hiss em loop | A legenda explica o limite da energia e a trajetória que continua |

Nas três primeiras rodadas, o knob começa a no máximo 15% da borda da zona. Uma seta e um medidor de proximidade orientam; visão e áudio fornecem feedback equivalente. Segurar E pode executar uma varredura assistida lenta, parando na primeira zona válida. Não exigir audição para localizar a frequência. A quarta tentativa é inevitável e honesta: “Nenhum sinal detectado”, sem culpar jogador ou instruir a continuar procurando.

Datas são **recepção na Terra em UTC**. O aniversário usa 3 MAR para coerência UTC. O atraso final de 11h20 refere-se a um sentido Terra–sonda no último sinal de janeiro, não ao tempo do minigame. O arquivo registra a tentativa posterior de 2006; a cena não afirma que fevereiro de 2003 foi a última tentativa de toda a história.

## Cena 8 — Ainda em viagem · 38 s

**0–8 s:** círculo da antena faz match cut com refletor da Pioneer; contorno emerge em preto. A câmera e a sonda mantêm o eixo de rotação de cruzeiro, embora os instrumentos estejam apagados.

**8–20 s:** placa em close com luz de apresentação muito suave e ausência de partículas próximas; a transição para plano aberto elimina a luz antes de revelar a direção de Aldebaran. A estrela é um ponto avermelhado distante, não um Sol próximo, e não ilumina a nave. Um guia discreto “direção de Aldebaran” evita fingir que a sonda já chegou. Não modelar uma constelação como desenho físico no espaço; uma linha de atlas pode aparecer como gráfico por 4 s.

**20–30 s:** a Pioneer diminui no campo de estrelas. Texto fala em milhões de anos como ordem de grandeza histórica, não em uma chegada programada à posição futura da estrela. Uma direção geral não é um rendez-vous calculado. O plano deve permitir reconhecer a antena antes que vire ponto; não um zoom repetido da cena anterior.

**30–38 s:** preto com PIONEER 10 · 1972–2003 / Em viagem. A linha de datas refere-se ao intervalo de comunicação, não à existência física. “Quem vai primeiro abre caminho para muitos.” Últimos 2 s oferecem Voltar ao arquivo e Repetir. Não iniciar outro capítulo automaticamente e não contabilizar o tempo parado nessa tela.

## Legendas finais PT/EN e marcações

Relógio **local do estado**, em segundos. Intervalos sem legenda são intencionais. Não sobrepor duas legendas narrativas; prompts pertencem ao HUD e suspendem novas informações quando uma instrução exige aprendizagem. `hold` significa que o relógio do cue espera a ação indicada antes de iniciar o próximo; não força o jogador a cumprir a ação até o segundo indicado. Cada idioma recebe quebra de linha própria, máximo de duas linhas e leitura revista em 720p. Se o texto não couber, dividir dentro da janela em vez de diminuir a fonte. As janelas abaixo reservam leitura; elas não são diálogos gravados.

| ID | Estado / início–fim | PT-BR | EN |
| --- | --- | --- | --- |
| PIO.01.01 | Launch / 0–7 | Em 1972, uma pequena sonda partiu para os planetas exteriores. | In 1972, a small spacecraft set out for the outer planets. |
| PIO.01.02 | Launch / 8–16 | Seu destino era Júpiter. Ninguém havia explorado aquele mundo de perto. | Its destination was Jupiter. No one had explored that world up close. |
| PIO.01.03 | Launch / 18–25 | Em menos de 11 horas, já havia cruzado a órbita da Lua. | In less than 11 hours, it had crossed the Moon’s orbit. |
| PIO.01.04 | Launch / 26–32 | Girar ajudava a manter a sonda estável durante a viagem. | Spinning helped keep the spacecraft stable during its journey. |
| PIO.01.05 | Launch / 33–40 | Longe do Sol, seus geradores transformavam calor radioativo em eletricidade. | Far from the Sun, its generators turned radioactive heat into electricity. |
| PIO.02.01 | Belt / 0–6 | Entre Marte e Júpiter, o cinturão de asteroides guardava uma dúvida. | Between Mars and Jupiter, the asteroid belt held an unanswered question. |
| PIO.02.02 | Belt / 6–12 | Haveria partículas suficientes para danificar a sonda? | Would there be enough particles to damage the spacecraft? |
| PIO.02.03 | Belt / 13–20 | Imagine o medo de encontrar um caminho cheio de pedras... | Imagine the fear of finding a path crowded with rocks... |
| PIO.02.04 | Belt / 27–34 | Mas a travessia real não foi um labirinto. Os asteroides estavam muito distantes. | But the real crossing was not a maze. The asteroids were very far apart. |
| PIO.02.05 | Belt / 34–40 | Os instrumentos procuravam algo menor: partículas de poeira. | The instruments searched for something smaller: dust particles. |
| PIO.02.06 | Belt / 51–58, hold 5/5 | Após meses de medições, a Pioneer saiu do cinturão sem danos. | After months of measurements, Pioneer emerged from the belt undamaged. |
| PIO.03.01 | JupiterApproach / 0–8 | No fim de 1973, Júpiter começou a encher o quadro. | By late 1973, Jupiter began to fill the frame. |
| PIO.03.02 | JupiterApproach / 9–17 | Cada mensagem levava dezenas de minutos. A equipe precisava planejar antes. | Each message took tens of minutes. The team had to plan ahead. |
| PIO.03.03 | JupiterApproach / 18–26 | Para ver esse mundo, uma imagem teria de nascer aos poucos. | To see this world, an image would have to emerge a little at a time. |
| PIO.04.01 | Imaging / 0–8 | A Pioneer não tinha uma câmera como a de um celular. | Pioneer did not have a camera like the one in a phone. |
| PIO.04.02 | Imaging / 8–16 | A rotação varria o planeta. Faixas estreitas formavam a imagem. | Its spin scanned the planet. Narrow strips built the image. |
| PIO.04.03 | Imaging / após faixa 4, 6 s | O instrumento media luz vermelha e azul. | The instrument measured red and blue light. |
| PIO.04.04 | Imaging / após faixa 6, 6 s | A radiação de Júpiter provocou comandos indevidos no instrumento. | Jupiter’s radiation triggered unwanted commands in the instrument. |
| PIO.04.05 | Imaging / após recuperação, 6 s | A equipe enviou sequências de recuperação. A observação continuou. | The team sent recovery sequences. The observations continued. |
| PIO.04.06 | Imaging / 48–55, hold 8/8 | Na Terra, o processamento estimava também o verde e compunha a imagem. | On Earth, processing also estimated green and assembled the image. |
| PIO.04.07 | Imaging / 55–63 | Algumas imagens se perderam, inclusive a de Io. Mas muito chegou até nós. | Some images were lost, including Io’s. But so much reached us. |
| PIO.04.08 | Imaging / 63–69 | Esta é Ganimedes, vista pela Pioneer 10 em 1973. | This is Ganymede, seen by Pioneer 10 in 1973. |
| PIO.04.09 | Imaging / 69–74 | Luz, radiação e magnetismo: um mundo novo para estudar. | Light, radiation and magnetism: a new world to study. |
| PIO.05.01 | Plaque / 0–8 | A sonda levava outra coisa: uma apresentação de quem a construiu. | The spacecraft carried something else: an introduction to its makers. |
| PIO.05.02 | Plaque / peça hidrogênio, 4 s | Hidrogênio: uma referência para medir tempo e tamanho. | Hydrogen: a reference for measuring time and size. |
| PIO.05.03 | Plaque / peça pulsares, 4 s | Pulsares: estrelas que ajudam a localizar o nosso Sol. | Pulsars: stars that help locate our Sun. |
| PIO.05.04 | Plaque / peça sistema, 4 s | O terceiro planeta é a origem da viagem: a Terra. | The third planet is where the journey began: Earth. |
| PIO.05.05 | Plaque / peça silhueta, 4 s | A própria Pioneer dá uma escala para os desenhos humanos. | Pioneer itself provides a scale for the human figures. |
| PIO.05.06 | Plaque / peça pessoas, 4 s | Pessoas: uma delas ergue a mão, como quem diz olá. | People: one raises a hand, as if to say hello. |
| PIO.05.07 | Plaque / 38–46, hold 5/5 | Uma placa de alumínio dourado. Uma mensagem para um encontro que talvez nunca aconteça. | A gold-colored aluminum plaque. A message for a meeting that may never happen. |
| PIO.06.01 | Outbound / 0–8 | A viagem não terminou em Júpiter. Sua gravidade mudou o caminho da Pioneer. | The journey did not end at Jupiter. Its gravity changed Pioneer’s path. |
| PIO.06.02 | Outbound / 8–16 | Em 1983, ela já seguia além das órbitas dos planetas. | By 1983, it was traveling beyond the planets’ orbits. |
| PIO.06.03 | Outbound / 17–24 | Com os anos, os geradores forneciam menos energia. Era preciso escolher o que manter ligado. | As years passed, the generators supplied less power. The team had to choose what stayed on. |
| PIO.06.04 | Outbound / 25–33 | Em 1998, a Voyager 1 se tornou a mais distante do Sol, seguindo outra direção. | In 1998, Voyager 1 became the farthest from the Sun, traveling in another direction. |
| PIO.06.05 | Outbound / 33–39 | A Pioneer havia mostrado o caminho. Agora, a Terra escutava cada vez mais longe. | Pioneer had shown the way. Now Earth was listening farther and farther away. |
| PIO.07.01 | Listening / 0–7 | A distância aumentava. As antenas da Terra buscavam um sinal cada vez mais fraco. | The distance grew. Earth’s antennas searched for an ever weaker signal. |
| PIO.07.02 | Listening / sucesso 1, 5 s | Março de 2002. Trinta anos depois do lançamento, ela ainda respondia. | March 2002. Thirty years after launch, it was still responding. |
| PIO.07.03 | Listening / sucesso 2, 5 s | Abril de 2002. Os últimos dados de seus instrumentos chegaram à Terra. | April 2002. The last data from its instruments reached Earth. |
| PIO.07.04 | Listening / sucesso 3, 7 s | Janeiro de 2003. Um último sinal levou 11 horas e 20 minutos para chegar. | January 2003. A last signal took 11 hours and 20 minutes to arrive. |
| PIO.07.05 | Listening / 39–45 | Fevereiro de 2003. Nenhum sinal detectado. | February 2003. No signal detected. |
| PIO.07.06 | Listening / 48–52 | Sem energia suficiente para nos alcançar, a Pioneer ficou em silêncio. | Without enough power to reach us, Pioneer fell silent. |
| PIO.08.01 | Farewell / 0–8 | O silêncio não interrompeu a viagem. | Silence did not stop the journey. |
| PIO.08.02 | Farewell / 9–17 | A Pioneer segue na direção de Aldebaran, uma estrela distante. | Pioneer continues in the direction of Aldebaran, a distant star. |
| PIO.08.03 | Farewell / 18–26 | Estamos falando de milhões de anos. Um futuro que ninguém aqui verá. | We are talking about millions of years. A future none of us here will see. |
| PIO.08.04 | Farewell / 27–35 | Quem vai primeiro abre caminho para muitos. | Those who go first open a path for many. |

**Ajuste de leitura obrigatório:** PIO.07.06 tem apenas 4 s no relógio nominal; sua animação pode alongar até 7 s sem cortar a legenda. O mesmo vale para PIO.04.09 se o idioma precisar de mais tempo. O total poderá chegar a 381 s. Nunca acelerar a leitura para preservar um número exato de segundos.

### Texto exato de HUD e cartões

| Chave | PT-BR | EN |
| --- | --- | --- |
| PIO.UI.start | Pioneer 10 · Abrir caminho | Pioneer 10 · Going first |
| PIO.UI.dossier | A primeira visita a Júpiter. Transforme sinais em imagens e escute uma viagem de trinta anos. | The first visit to Jupiter. Turn signals into images and listen to a thirty-year journey. |
| PIO.UI.rtg | Segure E · acompanhar a energia | Hold E · follow the power |
| PIO.UI.imagination | IMAGINAÇÃO | IMAGINATION |
| PIO.UI.move | Setas ou WASD · mover | Arrow keys or WASD · move |
| PIO.UI.dust | Segure E · registrar poeira {count}/5 | Hold E · record dust {count}/5 |
| PIO.UI.scan | Segure E · registrar a passagem {count}/8 | Hold E · record this pass {count}/8 |
| PIO.UI.red | Canal vermelho | Red channel |
| PIO.UI.blue | Canal azul | Blue channel |
| PIO.UI.recover | E · enviar sequência de recuperação | E · send recovery sequence |
| PIO.UI.real | Imagem real · Pioneer 10 · 1973 · NASA | Actual image · Pioneer 10 · 1973 · NASA |
| PIO.UI.plaque | Escolha uma peça e seu contorno · {count}/5 | Choose a piece and its outline · {count}/5 |
| PIO.UI.snap | E · encaixar / Segure E · ajuda | E · place / Hold E · help |
| PIO.UI.power | Segure E · priorizar a comunicação | Hold E · prioritize communication |
| PIO.UI.tune | ← → · sintonizar / Segure E · escutar | ← → · tune / Hold E · listen |
| PIO.UI.nearer | Mais perto do sinal | Closer to the signal |
| PIO.UI.farther | Mais longe do sinal | Farther from the signal |
| PIO.UI.received | Sinal recebido | Signal received |
| PIO.UI.no_signal | Nenhum sinal detectado | No signal detected |
| PIO.UI.no_fault | Sua escuta funcionou. Desta vez, não houve resposta. | You listened correctly. This time, there was no response. |
| PIO.UI.help | Ajuda de alinhamento | Alignment help |
| PIO.UI.ending | PIONEER 10 · 1972–2003 / Em viagem | PIONEER 10 · 1972–2003 / Still traveling |
| PIO.UI.return | Voltar ao arquivo | Return to the archive |
| PIO.UI.replay | Repetir capítulo | Replay chapter |

## Módulos locais e arquivos de responsabilidade de R5

| Arquivo proposto | Responsabilidade única | Entrada / saída |
| --- | --- | --- |
| `Assets/_Game/Chapters/Pioneer10/PioneerChapterController.cs` | Ordem explícita dos oito estados; cancelamento e reset locais | Consome contrato global de capítulo; emite conclusão |
| `PioneerPresentation.cs` | Rotação visual, atlas orbital, planos e eixo de luz | Recebe cue/estado; não aprova ciência nem altera objetivo |
| `BeltInvestigation.cs` | Separar fantasia temporizada de aquisição de cinco registros | `HoldProgress`, foco e conclusão; sem colisão fatal |
| `PioneerImaging.cs` | Oito faixas, canais, pausa por recuperação e revelação | Estado serializável; sem outro sistema de fotos completo |
| `PioneerPlaqueActivity.cs` | Cinco pares, seleção, snap e alternativa de teclado | Coleção de IDs; sem depender da posição 3D do refletor |
| `PioneerListening.cs` | Três zonas válidas e uma janela intencional sem resposta | Sintonia visual/áudio desacoplada; emite resultado histórico |
| `PioneerData.asset` / JSON localizado | Datas, cue IDs, ordem e durações | Fonte única de textos, sem literais narrativos nos scripts |
| `Pioneer10.unity` + `PioneerActors.prefab` | Geometria e referências da apresentação | Cena aditiva inicializável por menu e por atalho de desenvolvimento |

Reutilizar `HoldInteraction`, `SubtitleTrack`, `ChapterClock`, `InputContext`, `AudioBus`, `ChapterResult` e assistência globais; não desenvolver versões locais de pausa, localização, volume ou carregar cena. Os nomes concretos devem seguir o contrato aprovado em CORE-002; a tabela define responsabilidades, não uma API já implementada.

## Assets previstos e orçamento Web

- GLB oficial NASA da Pioneer; arquivo será fornecido pela equipe ou baixado da página oficial com licença e hash registrados. **Ainda não consta como recebido/importado neste planejamento.** Conferir escala do refletor (~2,74 m), orientação, UVs, RTGs e placa. Meta do prefab LOD0: até 60 mil triângulos; 1 atlas 2K principal, resto 1K; LOD1/2 para planos distantes. Se exceder, decimar e comparar silhueta antes de integrar.
- Fotos NASA Pioneer/Júpiter e Pioneer/Ganimedes verificadas como resultado da missão, não fotografia moderna emprestada. Até 1024 px de largura para resultado; uma textura de Júpiter 2K para esfera artística, com origem e data explicitadas no crédito.
- Campo de estrelas em uma textura de céu compartilhada, sem milhares de GameObjects. Planetas em um atlas/diagrama 2D quando não são o foco. Uma esfera de Júpiter, uma malha de sonda e uma silhueta de antena; nenhum terreno gigante.
- No perfil leve: uma luz principal sem sombras no espaço, materiais simples e um fill sem sombra para apresentação. Bloom discreto opcional; zero SSAO, motion blur, DOF ou luzes volumétricas. Radiação e varredura por UI/máscaras de textura, não pós-processamento full-screen caro.
- Meta de conteúdo incremental do capítulo: **até 12 MB comprimidos**, a confirmar no primeiro build. Máximo uma captura/resultado de 960×540; fotos históricas são assets reutilizados, não screenshots acumuladas. Nenhuma dependência online durante a partida.
- Meta da cena: menos de 120 draw calls de conteúdo, até 150 mil triângulos visíveis nos planos de Júpiter, pico de 80 partículas no lançamento e zero partículas permanentes em cruzeiro. Metas são limites iniciais de produção, não benchmarks medidos.

## Gates de aceitação do capítulo

1. Dossiê→oito estados→memorial→arquivo, com PT/EN, pausa, foco perdido e reinício em todos os minigames, sem callbacks antigos.
2. Todos os objetivos são alcançáveis só com teclado; nenhuma tarefa depende de ouvir um som, mirar algo pequeno ou executar um timing estreito.
3. E mantido antes de abrir a tela não resolve a atividade nem pula a legenda seguinte. Soltar/retomar preserva progresso válido.
4. Fantasia tem selo em todos os seus frames e some antes de mostrar o espaço real. A criança consegue dizer que o cinturão real não era uma pista de obstáculos.
5. Canais vermelho/azul e verde processado, foto real correta e Io perdida não são apresentados como uma câmera RGB contemporânea.
6. Últimos dados (2002) e último sinal (2003) são diferenciados; a quarta tentativa não parece falha do jogador. A sonda continua em movimento, sem explosão.
7. Datas UTC/local, placa de alumínio dourado, energia RTG e direção geral de Aldebaran conferidas. Nenhum planeta aparece como encontro durante mero cruzamento de distância orbital.
8. Teste automatizado confirma ordem, idempotência e reset; primeira experiência humana confirma 5–7 min. Navegador em perfil leve avaliado no hardware de referência real da equipe, com medidas de FPS, memória e download.

## Corte de escopo ordenado

P0 preserva os oito estados, cinco registros, oito faixas, cinco peças e três contatos mais silêncio. Se o tempo de produção cair: reduzir modelagem do lançamento a silhuetas; reduzir câmera orbital a dois planos; fazer DSN com silhueta 2D; usar só dois cartões no percurso. Não remover controles de teclado, legendas, identidade das imagens, fato sobre energia ou o silêncio final. P1: acabamento do refletor/antena, mais detalhe do lançamento e texto opcional da cauda magnética. P2: datas extras de Urano, história de três semanas da placa e panorama orbital tridimensional completo. Não são pendências bloqueantes para a versão de domingo.
