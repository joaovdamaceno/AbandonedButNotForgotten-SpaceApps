# Direção de arte, cinema e som

Princípio: a câmera apresenta uma ideia e muda a compreensão do hardware. Não somar planos apenas para ocupar segundos. Estética de animação documental estilizada, iluminação convincente e modelos cientificamente reconhecíveis, compatível com Web. Não prometer fotorealismo ou custo de longa-metragem num fim de semana.

## Linguagem comum

Arquivo 2D: papel quente, tinta escura, foto grande, blueprint discreto. Cada pasta tem nome, pergunta, destino/ano e botão; leitura essencial em menos de 10 s, detalhes em aba opcional. Sem logotipo NASA como marca própria. Tipos legíveis, família Outfit para UI e uma família de título avaliada em nomes/datas, com licenças OFL registradas. Não converter páginas em blocos de texto.

Cinema: establishing de localização → detalhe que introduz ação → consequência. Match cuts apenas quando conectam máquinas/anos; mostrar data/local ao trocar Spirit/Opportunity. Sem duas tomadas quase iguais, snap de zoom/FOV, câmera abaixo da malha, repetição de gameplay como falso novo evento. Cada plano possui propósito, sujeito, eixo, iluminação, entrada/saída e alternativa de baixo custo no documento de capítulo.

Usar espaço negativo para escala e isolamento, recortes de luz para componentes, contraste suficiente para encontrar alvo. Não esconder uma instrução essencial dentro de legenda que some. Na ação, o céu e o solo conduzem o olhar para o objetivo, marcador discreto confirma.

## Marte

Meridiani: bacia Eagle reconhecível, afloramento claro estratificado em faixa e pequenas concreções; evitar grandes seixos brancos espalhados sem padrão. Cores ferrugem/ocre, sombras frias moderadas, céu caramelo. Taludes e areia encostam nas bases; escala baseada em imagens. Victoria/Perseverance/Gusev são regiões e épocas diferentes: trocar cenário com elipse, não fingir adjacência.

Rover: modelo principal da NASA preservado, rodas/esterço com eixos locais corretos, suspensão sutil, pinos conectados, braço que recua pelo trajeto livre da rocha. Rastro usa âncoras independentes da animação, raycast no terreno exclusivamente, normal e pequeno offset; limpeza do rastro em teleporte, não cruza por cima do chassi. Pedras grandes verificadas em múltiplos pontos de apoio e em ângulo baixo, não somente pelo centro.

Pouso: escudo térmico/carenagem, paraquedas, módulo envolto em airbags e rover dobrado formam um encadeamento. Inflação não revela partes atravessando o tecido; módulo fechado cabe dentro; airbags murcham suavemente, sem sumir por triângulos. Abertura tem elipse de verificações. VFX de impacto com alpha/luminância limitados; poeira ocre não vira mancha branca por bloom.

Tempestades: poeira atmosférica e perda de luz solar; 2007 tem recuperação, 2018 termina em silêncio. Atmosfera afeta solo **e rover**, sem silhueta preta impossível atravessando neblina. Evitar tornado/destruição inventada. Dramaticidade vem de visibilidade, som, energia e ritmo, sem tremor que impeça ler. Fallback leve mantém fog de profundidade e reduz partículas/overdraw.

## Pioneer

Direção do spin coerente com eixo da antena e RTGs. Júpiter grandioso por escala/enquadramento e textura NASA; não fazer a sonda orbitar como se tivesse sido capturada. Uma key solar conforme posição, fills artísticos discretos e separação do fundo. Estrelas não piscam por atmosfera inexistente; podem ter brilho artístico estável.

Imaginação do cinturão: moldura gráfica clara, curta, lúdica; virada revela vazio real. Imagens IPP e placa usam UI/2D de qualidade e partes 3D só onde ajudam. Placa é alumínio anodizado dourado, figuras simplificadas identificadas como adaptação. Aldebaran distante é direção simbólica, sem reflexo que sugira uma estrela próxima à placa. Sala DSN pode ser painel e silhueta 2D de antena com foto de referência, sem laboratório inteiro.

## Surveyor

HUD escuro de telemetria legível não significa seis telas pretas sem acontecimentos. Velocidade/altitude, barra de comando e vibração moderada narram descida; as primeiras varreduras de TV revelam encosta, patas e pá. Imagem preta e branca com contraste suave, sem ruído que esconda ferramentas. Limitar efeitos a um material de TV e máscara de scan, sem renderizar 600 linhas reais a cada frame.

Regolito lunar seco e cinza, relevo/cratera conferidos nas imagens da missão; luz solar baixa com sombras bem definidas. Lente e espelho movem o enquadramento no local do equipamento. Botas/luva/sombra no final, sem astronauta completo se não couber. Identificar a partir da transição que a visita é o plano que a câmera nunca registrou. Não acrescentar poeira em suspensão longa ou vento numa Lua sem atmosfera.

## Áudio e feedback

Três famílias musicais licenciadas: curiosidade/descoberta, expansão/viagem e legado. Cues associados aos estados, crossfades de 0,8–2 s e ducking suave quando texto científico entra. Não reutilizar tensão de suspense em todo plano. Sem locução de IA; som mecânico simpático cria afeto sem transformar a máquina em personagem que fala mensagens inventadas.

Hold E: clique de início, motor/scan crescente, progressão visual material e confirmação curta. Cancelar tem desaceleração; zero loop contínuo no arquivo/pausa. Vento baixo e variável, sem pulsação de helicóptero; deve poder baixar SFX sem perder sinais visuais. Rádio Pioneer com faixa de ruído limitada e volume calibrado, silêncio final intencional. Surveyor usa sonificação eletrônica, não passos propagados pelo vácuo.

Revisão obrigatória em tela pequena: objeto científico legível, E/resultado relacionados ao mesmo alvo, cores de alerta acompanhadas de texto/forma, câmera assistida e movimento reduzido. Modelos e assets principais pendentes têm fallbacks definidos nas tarefas, sem fingir que foram entregues.
