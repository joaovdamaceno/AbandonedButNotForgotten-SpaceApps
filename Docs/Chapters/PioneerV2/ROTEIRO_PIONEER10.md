# Pioneer 10 — Roteiro, diretrizes, falas e linha do tempo

Jogo educativo para o NASA Space Apps Challenge (Ensino Fundamental 1 e 2). As falas são legendas de cinema na tela, sem voz.

---

## Diretrizes gerais

- **Duração total: 5 a 7 minutos, alvo de ~6:40, teto de 7:00**, para uma criança jogando sem pressa, minigame incluído.
- **Prioridade máxima: enredo, história e cinematografia.** A fase deve parecer um curta-metragem de ficção científica real, com **uma única interação** no meio (o minigame da Cena 4) e **uma única interação** perto do fim (a sintonia de rádio da Cena 8). Todo o resto é cinema.
- **Visual: 3D realista e cinematográfico, de alta qualidade.** Nada de estilo desenho, quadrinhos, lousa, molduras decorativas ou selos de "imaginação". O que aparece na tela é o que a sonda viu e viveu. Use:
  - câmera física (Cinemachine) com lentes escolhidas de propósito: teleobjetiva para comprimir distâncias, grande-angular para revelar espaço;
  - barras de cinema (letterbox 2.39:1) nas cenas de cinema, que somem durante o minigame e a sintonia de rádio;
  - pós-processamento (bloom, vinheta suave, grão de filme leve, profundidade de campo, color grading quente nas cenas da Terra e frio nas do espaço, flare da luz do Sol);
  - movimentos de câmera lentos e intencionais (dolly, órbita, travelling), planos abertos e fechados, pausas contemplativas.
- **Linear e historicamente preciso.** A fase segue a sonda passo a passo, na ordem cronológica real: lançamento → Lua → Marte → cinturão de asteroides → Júpiter → placa → Saturno, Urano e Netuno → anos de energia caindo → silêncio → rumo a Aldebaran. Não existem cenas hipotéticas. Todo fato, data e número na tela vem da linha do tempo ou dos dados técnicos abaixo. Reconstruções visuais (modelos 3D, escala artística) são permitidas, mas nunca devem inventar um evento.
- **Público: 6 a 14 anos.** Frases curtas (até ~20 palavras por legenda) e vocabulário simples. Toda palavra técnica vem com uma analogia.
- **Sem narração por voz.** A história é contada por **legendas de cinema na tela, sincronizadas com a imagem** (texto aparece enquanto o fato acontece), mais música e som. Use o sistema de texto do módulo global, mas com aparência de legenda de filme (terço inferior da tela, sem balão de quadrinho).
- **Interações fáceis e bem explicadas:**
  - instrução curta e visual antes de começar, com ícone ou animação da mão mostrando o gesto;
  - tolerância generosa, sem game over e com tentativas ilimitadas;
  - dica automática depois de 2 erros ou 8 s parado;
  - feedback positivo ao acertar.
- Texto avançável por clique, com tempo mínimo na tela (siga o padrão do módulo global). Nas cenas de cinema, a legenda avança sozinha pelo tempo, mas pode ser acelerada por clique.
- **Final em tom esperançoso**, mesmo com a sonda em silêncio.
- **Escala artística**, não real. Quando as distâncias forem enormes, diga isso no texto.

---

## Linha do tempo factual (fonte do conteúdo da fase)

| Data | Evento |
|---|---|
| 2 mar 1972, 20h49 (hora da Flórida) / 3 mar 1972, 01h49 UTC | Lançamento em Cabo Kennedy, num foguete Atlas-Centaur com um terceiro estágio extra. A sonda pesava 258 kg e tinha o tamanho de um carro pequeno. Primeira espaçonave a usar energia elétrica totalmente nuclear. |
| ~11 h após o lançamento | Cruza a órbita da Lua. Era o objeto mais rápido já feito pela humanidade até então (~51.700 km/h ao deixar a Terra). |
| ~12 semanas após o lançamento | Cruza a órbita de Marte. Primeira sonda a ir além de Marte. |
| 15 jul 1972 | Entra no cinturão de asteroides, a primeira sonda a fazer isso. O cinturão tem ~434 milhões de km de largura. Alguns cientistas temiam que ela fosse destruída. A rota foi planejada para passar a ~8,8 milhões de km do asteroide conhecido mais próximo. |
| ago 1972 | Registra detalhes de uma das tempestades solares mais violentas já medidas até então, junto com outra sonda Pioneer. |
| 2 dez 1972 (dia 140 da travessia) | Passagem mais próxima de um asteroide conhecido nessa travessia: 307 Nike. |
| 15 fev 1973 (dia 215 da travessia) | Sai do cinturão sem danos sérios, depois de 215 dias (cerca de 7 meses). Os detectores registraram só poeira fina. Provou que dava para atravessar o cinturão. |
| 6 nov 1973 | Começa a fotografar Júpiter, a ~25 milhões de km. |
| 1º dez 1973 | Já manda imagens de Júpiter melhores do que as dos telescópios da Terra. Um comando e sua resposta levavam até 92 minutos para ir e voltar. |
| 3 dez 1973 (EUA) / 4 dez 1973, 02h26 UTC | Máxima aproximação de Júpiter: ~130.000 km acima das nuvens (~200.000 km do centro do planeta, ~2,8 raios de Júpiter), a mais de 120.000 km/h. Isso é cerca de 3 vezes mais perto do que a Lua está da Terra (384.400 km). Primeira sonda a visitar Júpiter. Mostre a data como **4 de dezembro de 1973** (padrão da NASA, em UTC). |
| Encontro com Júpiter (nov 1973 – 2 jan 1974) | Os cinturões de radiação de Júpiter são milhares de vezes mais intensos que os da Terra. A sonda absorveu mais de mil vezes a dose que seria mortal para uma pessoa. A rota foi escolhida de propósito para medir essa radiação, mesmo arriscando a sonda. A partir de 3 dez, a radiação gerou comandos falsos; comandos de reserva corrigiram quase todos, mas a foto de Io e algumas imagens de perto se perderam. Ela fotografou Ganimedes (primeiras imagens de perto), Europa e Calisto. Mediu o campo magnético, a atmosfera, a radiação e a massa de Júpiter e das grandes luas. Foram cerca de 500 imagens de Júpiter e das luas. Os efeitos da radiação na sonda passaram nos meses seguintes. A radiação medida por ela mostrou que as próximas sondas precisariam de muito mais proteção, e isso mudou o projeto da **Voyager**, que viria depois. |
| Após a máxima aproximação | A gravidade de Júpiter curvou a trajetória da sonda e deu o impulso que a colocou numa rota para escapar do Sistema Solar. Primeira espaçonave colocada nessa trajetória rumo ao espaço interestelar. |
| fev 1976 | Cruza a órbita de Saturno. Descobre que a "cauda" magnética de Júpiter (~800 milhões de km) chega até lá. |
| 1979 | Cruza a órbita de Urano (mostre só o ano, não o dia). |
| out 1980 | Detector de meteoroides desligado (sensores inativos no frio). |
| 13 jun 1983 | Cruza a órbita de Netuno, na época o planeta mais distante. Primeiro objeto feito pelo homem além da órbita de Netuno. Ainda funcionando normalmente. |
| mai 1986 | Os sensores solares da sonda param de funcionar. A câmera (fotopolarímetro) passa a ajudar a sonda a saber como está girando. |
| set 1989 | Começa o "plano de dividir energia": instrumentos passam a ser ligados e desligados em turnos porque os RTGs estão mais fracos. O computador de comandos programados (PSE) é desligado. |
| out 1993 / nov 1993 | A câmera (fotopolarímetro) é desligada em out 1993 e o detector de radiação em nov 1993, para economizar energia. |
| set 1995 | O analisador de plasma é desligado para economizar energia. |
| jan 1996 | Plano final: só 4 instrumentos funcionam, em turnos (partículas carregadas, raios cósmicos, contador Geiger e ultravioleta). |
| 31 mar 1997 | Fim do acompanhamento de rotina, por falta de verba. Contatos ocasionais continuam. |
| 17 fev 1998 | A **Voyager 1** ultrapassa a Pioneer 10 e vira o objeto humano mais distante. Até ali, o título era da Pioneer 10 (~69 UA). |
| 2001 | Os RTGs geram só ~65 W. A sonda precisaria de ~100 W para ligar tudo. |
| 3 mar 2002 | Contato com sucesso no 30º aniversário do lançamento. |
| 27 abr 2002 | Última vez que dados foram recebidos com sucesso (33 minutos de dados limpos, a ~80 UA). |
| 23 jan 2003 | Último sinal detectado, a ~12,23 bilhões de km (~80 UA). O sinal levou 11 h 20 min para chegar à Terra. Naquele contato, a equipe tentou enviar o comando para desligar o último instrumento que ainda funcionava, mas a sonda não respondeu como esperado. |
| 7 fev 2003 | Última tentativa: nenhum sinal. A NASA conclui que a energia dos RTGs não é mais suficiente para o transmissor. |
| Futuro | Segue na direção da estrela vermelha **Aldebaran**, o "olho" da constelação de Touro, a ~68 anos-luz. A viagem levaria cerca de 2 milhões de anos. |

Dados técnicos que podem aparecer (simplificados):
- **Duração:** foi projetada para uma missão de **21 meses** (até Júpiter) e funcionou por **mais de 30 anos**. Esse é um dos fatos principais da fase.
- **Baterias nucleares (RTG):** 4 geradores SNAP-19 de plutônio, em dois braços, cada um com ~40 W. Somavam ~155 W no lançamento e ~140 W ao chegar a Júpiter. É menos do que muitas lâmpadas de casa somadas. O calor do plutônio vira eletricidade, sem peças que se mexam e sem depender do Sol. Foi a primeira espaçonave a usar energia elétrica totalmente nuclear. Depois dela, esse tipo de bateria foi usado por muitas outras sondas e robôs, como a Voyager 1 e 2, Galileo, Cassini, New Horizons e os jipes de Marte Curiosity e Perseverance.
- **Tamanho de Júpiter:** o diâmetro de Júpiter é cerca de 11 vezes o da Terra.
- **Comunicação:** antena de 2,74 m e transmissor de 8 W (como uma lâmpada pequena) falando com a Deep Space Network. A velocidade de envio de dados caiu de 2.048 bits/s perto de Júpiter para 16 bits/s no fim da missão.
- **A câmera (fotopolarímetro):** um telescópio de 2,5 cm que enxergava só **uma linha fininha por volta**, em **duas cores: vermelho e azul**. O giro da sonda varria a linha, e a imagem era montada linha por linha. **O verde era calculado pelos computadores na Terra**, a partir do vermelho e do azul, para a foto ficar com cores naturais.
- **A placa da Pioneer**, de alumínio anodizado em ouro, foi presa nos suportes da antena. Foi criada por Carl Sagan e Frank Drake, com desenho de Linda Salzman Sagan, em cerca de 3 semanas. Mostra:
  - o átomo de hidrogênio (a "régua" usada para medir o resto da placa);
  - um mapa de pulsares que indica onde o Sol fica na galáxia;
  - o Sistema Solar, com a sonda saindo do 3º planeta;
  - a silhueta da sonda;
  - duas figuras humanas, uma acenando.

**Primeiras vezes** (usadas na Cena 9; todas confirmadas pela NASA):
1. Primeira espaçonave a usar energia elétrica totalmente nuclear.
2. Primeira espaçonave a voar além de Marte.
3. Primeira espaçonave a atravessar o cinturão principal de asteroides.
4. Primeira espaçonave a sobrevoar Júpiter.
5. Primeira espaçonave colocada em trajetória para escapar do Sistema Solar e entrar no espaço interestelar.
6. Primeiro objeto feito pelo homem a ir além da órbita de Netuno.

---

## Roteiro (cena a cena, com orçamento de tempo)

Os textos abaixo são a base. Pode reescrever para soar mais natural, mas mantenha o conteúdo, os fatos e o tom. As legendas aparecem **no momento em que o evento acontece na imagem**.

### Cena 1 — "Pioneira" (0:00–0:55)
- Abre em preto. Título da fase no padrão de títulos do módulo global: **Pioneer 10**. Legenda: "Pioneer quer dizer **pioneira**: quem vai primeiro. E o nome não é por acaso."
- Noite de 2 de março de 1972, Cabo Kennedy. Close na sonda pequena dentro do foguete, iluminada por refletores. **Plano de 2 s** em que a câmera passa sobre a **placa dourada** presa à antena e brilha (plantando o que vem na Cena 6). Legenda: "1972. Nenhuma máquina humana tinha ido além de Marte. Esta sonda, do tamanho de um carro pequeno, ia tentar."
- Lançamento noturno: chama iluminando a plataforma, câmera tremendo, corte seco para o silêncio do espaço.
- Contador estilizado: "11 horas depois, ela já tinha passado a Lua." Legenda: "Era a máquina mais rápida que a humanidade já tinha construído!"
- A sonda se abre e começa a girar devagar, num plano bonito com o Sol atrás da antena. Legenda: "A Pioneer gira como um pião. Assim ela fica sempre de pé e apontada para a Terra."
- **Baterias nucleares (RTG), com tempo para respirar (~15 s):** a câmera faz um travelling até um dos braços e mostra um RTG em close, brilhando de calor.
  - Legenda: "Longe do Sol, a luz é fraca demais para painéis solares."
  - Legenda: "Por isso ela leva baterias nucleares, os RTG. O calor do plutônio vira eletricidade."
  - Legenda: "A Pioneer foi uma das primeiras sondas a usar essa tecnologia: sem nenhuma peça que se mexa e sem precisar do Sol."
  - Montagem rápida e elegante com os RTG de outras sondas que vieram depois (apenas ícones/silhuetas minimalistas em linha, ou só texto, sem copiar designs): Voyager, Galileo, Cassini, New Horizons, Curiosity. Legenda: "Muitas outras sondas e até robôs de Marte usaram RTG depois dela."

### Cena 2 — "O cinturão de asteroides" (0:55–1:45) — SOMENTE CINEMA, sem interação
Experiência puramente visual e narrada por legendas. Mostra, ao mesmo tempo, **o medo dos cientistas** e **a realidade**, usando só a câmera.

- **Abertura (~10 s):** a sonda cruza a órbita de Marte (12 semanas depois do lançamento). Legenda: "Depois de Marte, havia um lugar onde ninguém tinha passado: o cinturão de asteroides." Contador discreto no canto inferior: **15 jul 1972**.
- **O medo (~12 s):** plano **com teleobjetiva longa (~400 a 600 mm equivalente)**. A sonda aparece pequena no primeiro plano, e atrás dela, muito longe, **uma parede cheia de asteroides que parece estar bem colada**. A teleobjetiva comprime a profundidade, então eles parecem pertinho uns dos outros, quase sem espaço entre eles. Trilha tensa.
  - Legenda: "Sabia-se que o cinturão tinha muitíssimas pedras. E ninguém sabia se dava para atravessar."
  - Legenda: "Alguns cientistas temiam que a Pioneer batesse numa delas e fosse destruída."
- **O tempo acelera (~15 s):** aparece o **contador de dias** ("Dia 0 / 215", com a data embaixo). A câmera avança em dolly lento atrás da sonda e o contador começa lento (1 dia por segundo) e **acelera cada vez mais** (5, 20, 60 dias por segundo), com um zumbido crescente e a poeira estelar passando em riscos. Legenda: "Os dias passam. Uma semana. Um mês. Meses."
  - Ao longo do avanço, **a distância focal diminui aos poucos** (de teleobjetiva para grande-angular, estilo "dolly zoom"), e a parede de asteroides se **abre** diante da câmera: a profundidade volta, e as pedras se revelam **muito afastadas umas das outras**.
- **A revelação (~10 s):** o contador desacelera e para por um instante no **Dia 140 (2 dez 1972)**. Um único asteroide aparece bem longe, um ponto em meio ao vazio (**307 Nike**), com uma linha fina e um rótulo discreto "o asteroide conhecido mais próximo". Legenda: "Na vida real, o espaço entre as pedras é gigantesco. A rota foi planejada para passar a milhões de quilômetros do asteroide conhecido mais próximo."
  - Trilha muda de tensa para calma e maravilhada, com um corte limpo.
  - Mostre por um instante os detectores da sonda piscando de leve: poeira fina passando. Legenda: "Só poeira fina bateu na sonda."
- **Saída (~3 s):** contador chega ao **Dia 215 (15 fev 1973)**. Legenda: "Sete meses depois, ela saiu do outro lado, inteira. Agora sabíamos: **dá para atravessar!**" Plano de câmera: a sonda sai de um campo de pedras para o escuro limpo à frente.

### Cena 3 — "O rei dos planetas" (1:45–2:15) — cinema
- Viagem comprimida: o fundo de estrelas passa e **Júpiter vai crescendo de um pontinho de luz até ocupar a tela**, com um movimento de câmera lento e majestoso (a sonda girando em silhueta no primeiro plano).
- Legenda: "Novembro de 1973. Pela primeira vez, uma máquina humana chegava perto de Júpiter, o maior planeta do Sistema Solar."
- **Plano grandioso:** Júpiter enorme, ocupando mais de 70% do quadro, com as bandas de nuvens em movimento lento, a Grande Mancha Vermelha cruzando, o brilho atmosférico no limbo e a luz do Sol de lado (terminador). A sonda pequenininha, em silhueta, girando na frente. Pausa contemplativa.
- **Comparação de tamanho:** uma Terra pequenina entra em quadro ao lado. Legenda: "Júpiter é cerca de 11 vezes mais largo que a Terra."
- Legenda: "Ela começou a fotografar de longe, a 25 milhões de km. As mensagens para a Terra levavam muito tempo: um comando e sua resposta podiam levar 1 hora e meia."

### Cena 4 — INTERAÇÃO 1: "Uma foto, três passos" (2:15–3:00)
Redesenhado para ser **simples**: uma única tela, **um único verbo** (segurar o botão), e três passos numerados com o indicador **"Passo 1/3", "2/3", "3/3"** sempre visível no topo. Nunca há mais de um botão grande na tela ao mesmo tempo. Sem tempo, sem erro possível.

- **Introdução (~8 s), fato real:** "A Pioneer não tinha uma câmera como a do celular. Ela enxergava só uma linha fininha a cada volta. A foto era montada linha por linha, em vermelho e em azul!"
- **Layout:** à esquerda, a sonda girando, com um feixe de luz que varre Júpiter a cada volta. À direita, um "quadro" vazio onde a imagem vai aparecendo (sem moldura decorativa, só uma área de imagem limpa).
- **Passo 1/3 — Luz vermelha (~10 s):**
  - Um único botão grande vermelho: **GIRAR** (segure com mouse/dedo ou barra de espaço). A mão-fantasma mostra o gesto.
  - Enquanto o botão está apertado, a sonda gira e **uma linha nova aparece no quadro a cada volta**, formando uma imagem de Júpiter **toda em tons de vermelho**. Se a criança soltar, tudo pausa. Quando a imagem completa, soa um "plim" e o botão vira um ✓.
  - Legenda: "Vermelho: mostra muitos detalhes das nuvens."
- **Passo 2/3 — Luz azul (~10 s):**
  - O botão muda para um único botão grande azul: **TROCAR PARA AZUL**. Um anel de filtro gira na sonda e a cor do feixe muda para azul.
  - Aparece de novo o botão **GIRAR**, agora azul. Mesmo gesto. Uma segunda imagem de Júpiter vai se montando, **toda em tons de azul**, ao lado da vermelha.
  - Legenda: "Azul: mostra outras partes da atmosfera."
- **Passo 3/3 — Juntar as cores (~10 s):**
  - Um único botão grande brilhante: **JUNTAR**. As duas imagens deslizam uma sobre a outra.
  - Aparece um ícone de computador na Terra. Legenda: "A Pioneer só via vermelho e azul. O computador na Terra **calculou o verde**."
  - A imagem ganha cor, num efeito bonito, e vira o Júpiter colorido.
- **Revelação (~7 s):** a imagem montada pela criança faz crossfade para a **foto real de Júpiter tirada pela Pioneer 10** (NASA, domínio público), com a legenda de crédito "Foto real, 1973". Legenda: "Eram imagens melhores que qualquer telescópio da Terra."
- Duração alvo: 45 s. Dica automática se a criança ficar parada 8 s (a mão-fantasma reaparece).

### Cena 5 — "O mais perto possível" (3:00–4:05) — cinema, o coração da fase
A cena mais cinematográfica. Câmera colada na sonda e em Júpiter, ritmo que acelera e depois relaxa. Em ordem cronológica:

- **5a. Chegada e as luas (~12 s):**
  - Legenda: "Ela passou pelas grandes luas de Júpiter."
  - Cortes elegantes mostrando as luas em 3D (Calisto, Ganimedes, Europa) cruzando o quadro e, em cada uma, um crossfade rápido para a **foto real da Pioneer** (Ganimedes, Calisto, Europa). Legenda: "Ela tirou as primeiras fotos de perto de Ganimedes, a maior lua do Sistema Solar."
  - Aparece um plano de **Io**, com um "X" suave de falha: "A foto de Io se perdeu" (explicação vem na 5b).
- **5b. A radiação (~15 s):**
  - O ambiente fica mais intenso: um medidor de radiação sobe e a imagem chuvisca de leve, com grão e estática no som. Legenda: "Júpiter tem uma radiação milhares de vezes mais forte que a da Terra."
  - Legenda: "A sonda absorveu mais de mil vezes a dose que seria mortal para uma pessoa!"
  - Comandos falsos aparecem rapidamente num painel de controle (sem texto técnico, só ícones piscando) e são corrigidos por "comandos de reserva". Legenda: "A radiação bagunçou o computador, mas a equipe tinha comandos de reserva prontos. A Pioneer aguentou."
  - Legenda honesta: "Algumas fotos se perderam, inclusive a de Io."
- **5c. O ponto mais próximo (~14 s) — ultra cinematográfico:**
  - Silêncio na música por 1 s. Depois a trilha sobe e a câmera **mergulha**: a sonda passa rente às nuvens de Júpiter, cobrindo o quadro inteiro. Câmera baixa, atrás/ao lado da sonda, com a antena girando contra o enorme mar de nuvens e a Grande Mancha Vermelha ao longe. Efeito de velocidade nas nuvens (paralaxe forte, camadas se mexendo).
  - Legenda grande, solta no centro: **"4 de dezembro de 1973"**.
  - Legenda: "A Pioneer 10 foi a **primeira sonda a visitar Júpiter**."
  - Quando a sonda chega ao ponto mais próximo, mostre a **comparação de distância**: uma linha traçada da Terra até a Lua (384.400 km) e outra, **3 vezes menor**, da sonda até as nuvens (130.000 km). Legenda: "Ela chegou a cerca de 130.000 km das nuvens. Isso é 3 vezes mais perto do que a Lua está da Terra!"
  - Legenda: "Naquele momento, a mais de 120.000 km por hora."
- **5d. O empurrão gravitacional (~14 s):**
  - A câmera se afasta e sobe, até um **plano alto, quase de cima**, com Júpiter e a trajetória da sonda desenhada como uma linha de luz. A linha **faz uma curva** em volta de Júpiter. Um velocímetro discreto mostra a **velocidade subindo** na chegada. Legenda: "A gravidade de Júpiter é gigante. Ela **empurrou** e **curvou** o caminho da Pioneer."
  - Linhas de campo gravitacional pulsando em torno do planeta (visual elegante, nada cartunesco).
  - Mostre a seta do movimento de Júpiter em volta do Sol. Legenda: "Júpiter também corre ao redor do Sol, e a sonda pegou carona nesse movimento."
  - Legenda: "Esse empurrão deu a ela velocidade para **escapar do Sistema Solar**."
- **5e. A lição (~10 s):**
  - Plano de volta na sonda saindo, com Júpiter ficando para trás, iluminada por trás. Legenda: "A Pioneer descobriu como a radiação de Júpiter é forte."
  - Legenda: "Isso ensinou os cientistas a proteger melhor as sondas seguintes. A radiação medida por ela mudou o projeto da **Voyager**, que viria depois."
  - Legenda: "Ela também mediu o campo magnético, a atmosfera e o 'peso' de Júpiter e das suas luas."

### Cena 6 — "Uma carta para as estrelas" (4:05–4:25) — cinema, sem interação
- Corte para o espaço profundo, silencioso. Legenda: "Depois de Júpiter, a Pioneer 10 nunca mais voltaria. Agora ela ia rumo às estrelas."
- Close lento na **placa dourada** presa à sonda, brilhando à luz de Júpiter que fica para trás. A câmera desliza sobre a gravura e cada elemento **acende em sequência**, com uma legenda curta e sem interação:
  - Hidrogênio: "O átomo mais comum do Universo: a régua."
  - Pulsares: "Um mapa de estrelas que piscam: onde fica o Sol."
  - Sistema Solar: "A sonda saiu do terceiro planeta: a Terra."
  - Silhueta: "O desenho da própria Pioneer."
  - Pessoas: "Quem mandou a mensagem. Uma pessoa acenando: olá!"
- Legenda final: "É uma carta para quem a encontrar daqui a milhões de anos. Foi criada em só 3 semanas!"
- **Cuidado com o público infantil:** as figuras humanas da placa original estão sem roupa. Desenhe uma versão simplificada da placa em traço dourado, com as pessoas como silhuetas lisas e sem detalhes anatômicos. Nada de usar a gravura original em alta resolução.

### Cena 7 — "Os anos" (4:25–5:20) — cinema com medidor de energia
Uma montagem linear, ano a ano, sobre uma linha do tempo horizontal elegante na parte de baixo da tela, enquanto a sonda segue girando, cada vez menor e mais longe, num fundo de estrelas.

- **Estrutura da tela:**
  - uma linha do tempo com o **ano em destaque** correndo de 1974 até 2001;
  - à direita, um **medidor de energia dos RTG** (barra ou arco). Marcas reais: **155 W** no lançamento, **~140 W** em Júpiter e **~65 W** em 2001. Uma linha fina marca "~100 W: o necessário para ligar tudo". A curva entre esses pontos é **estilizada** (suave, descendo ao longo dos anos);
  - à esquerda, **ícones dos instrumentos** (câmera, detector de radiação, analisador de plasma, detector de meteoroides, etc.), cada um com o nome simples embaixo. Quando um instrumento é desligado, o ícone **apaga com um clique grave** e o rótulo do ano mostra "**[ano] — [nome] desligado**" por ~3 s.
- **Sequência (cada passo com sua legenda e seu som):**
  - Introdução. Legenda: "A Pioneer tinha sido feita para durar 21 meses. Ela continuou. E continuou."
  - **1976** — "Órbita de Saturno." Legenda: "Ela descobriu que a 'cauda' magnética de Júpiter chega até aqui!"
  - **1979** — "Órbita de Urano."
  - **1980** — "Detector de meteoroides desligado." Legenda: "Os sensores dele pararam de funcionar no frio."
  - **13 de junho de 1983 — Órbita de Netuno.** Pausa longa, música sobe. Legenda: "Pela primeira vez, uma máquina humana estava **além de Netuno**, na época o último planeta!"
  - **1986** — "Sensores solares param." Legenda: "A câmera passou a ajudar a sonda a saber como estava girando."
  - **1989** — "Plano de dividir energia." Legenda: "Os RTG ficaram mais fracos. Os instrumentos passaram a trabalhar em turnos." Também: "Computador de comandos desligado."
  - **1993** — "Câmera desligada" e depois "Detector de radiação desligado". Legenda: "Para economizar energia, a câmera que fotografou Júpiter foi desligada."
  - **1995** — "Analisador de plasma desligado."
  - **1996** — Legenda: "Só 4 instrumentos ainda funcionavam, em turnos."
  - **1997** — "31 de março: fim do acompanhamento de rotina." Legenda: "A equipe ouvia a Pioneer só de vez em quando."
  - **1998 (momento de passar o bastão)** — só texto e um indicador simples de distância subindo na tela, sem desenhar outra sonda. Legenda: "A Voyager 1 passou à frente e virou a máquina humana mais distante. A Pioneer 10 tinha mostrado o caminho." Não diga que elas estavam perto uma da outra: seguem direções diferentes do espaço.
  - **2001** — o medidor chega perto de ~65 W, abaixo da linha de 100 W. Legenda: "Ela já tinha menos energia do que precisava."
- Anos sem evento passam rápido (≈0,5 s cada) com um "tique" suave. Anos com evento ganham pausa.

### Cena 8 — INTERAÇÃO 2: "Trinta anos depois" (5:20–6:00)
Um momento de cinema com uma única interação curta no meio.

- **Abertura (~10 s):** a antena de 70 m da Deep Space Network à noite, gigante contra a Via Láctea. Câmera sobe devagar. Legenda: "3 de março de 2002. A Pioneer 10 fazia 30 anos de viagem."
- Legenda: "Ela estava tão longe que o sinal era mais fraco que um sussurro."
- **Interação (~15 s, uma única rodada):**
  - A criança gira um botão de sintonia (arrastar em círculo ou setas) enquanto uma onda chiada vai ficando limpa. Ao achar a frequência certa, aparece um "bip" e uma luz verde. A zona correta é grande e o áudio fica mais limpo quanto mais perto ela chega.
  - Ao acertar: o bip é recebido, a câmera abre num plano épico da antena apontada para o céu e a sonda aparece pequena no céu, como uma estrela. Legenda: "A Pioneer respondeu! **30 anos depois**, ela ainda falava com a Terra."
- **Cinema depois da interação (~15 s), tudo linear:**
  - Legenda: "27 de abril de 2002: chegaram os últimos dados."
  - Legenda: "23 de janeiro de 2003: um último sinal, de 12 bilhões de km. Ele levou mais de 11 horas para chegar."
  - Legenda: "7 de fevereiro de 2003: a Terra escutou... e não ouviu nada."
  - A tela mostra com calma "Nenhum sinal." A criança não participa. A antena continua apontada para o céu.
  - Legenda: "A energia acabou e a Pioneer 10 ficou em silêncio. Mas ela não parou."
- **Silêncio total de 2–3 s.** É o momento emocional da fase.

### Cena 9 — "Rumo a Aldebaran" — final (6:00–6:40)
- A câmera sai da antena na Terra e volta para a sonda, ainda girando devagar no escuro.
- Uma estrela vermelha brilhante ao fundo se destaca: **Aldebaran**, o "olho do Touro". Pode desenhar sutilmente a constelação de Touro.
- Legenda: "Ela segue em direção à estrela vermelha Aldebaran. A viagem vai levar cerca de 2 milhões de anos."
- **Destaque ao tempo:** Legenda grande: "Foi feita para **21 meses**. Durou **mais de 30 anos**."
- **As seis primeiras vezes (~15 s):** ao redor da sonda, seis pequenas estrelas acendem uma a uma (em ordem cronológica), cada uma com uma legenda curta:
  1. "Primeira a usar energia totalmente nuclear."
  2. "Primeira a voar além de Marte."
  3. "Primeira a atravessar o cinturão de asteroides."
  4. "Primeira a sobrevoar Júpiter."
  5. "Primeira rumo ao espaço interestelar."
  6. "Primeira a ir além de Netuno."
- Legenda: "Pioneer quer dizer pioneira. O nome não é por acaso."
- Plano final: a placa dourada reflete a luz de Aldebaran. A câmera se afasta devagar e a sonda vira um ponto entre estrelas vivas e coloridas (não um vazio triste).
- Texto final, esperançoso: "Toda grande viagem começa com alguém que vai primeiro. A Pioneer 10 foi primeiro, por todos nós."
- Ao terminar, use o fluxo de fim de fase do módulo global (tela de conclusão e volta ao menu ou à seleção de sonda).
