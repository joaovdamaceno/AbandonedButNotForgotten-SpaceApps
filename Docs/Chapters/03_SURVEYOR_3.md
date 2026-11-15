# Surveyor 3 — documentário jogável

SUR-018, direção aprovada pelo proprietário em 15/11/2026. Substitui a telemetria interativa, o centro óptico fixo durante todo o filme e o reencontro representado por botas/luva independentes. O histórico SUR-001…017 e seus previews permanecem no Git; não são a edição publicada.

## História

Mostrar por que a máquina foi à Lua, o que investigou e por que astronautas voltaram até ela. O filme funciona sem ler o dossiê. Em 1967, câmera e pá ajudaram a estudar resistência e coesão do solo; os testes locais não demonstram água nem certificam todo terreno lunar. Eclipse em 24 de abril, último contato em 4 de maio. Em novembro de 1969, Apollo 12 recuperou câmera, pá e outras peças para estudar a exposição ao ambiente lunar. A estrutura permaneceu. Fontes: 03_SURVEYOR_SOURCES.md.

| Etapa | Referência | Função |
|---|---:|---|
| Prologue | 25 s | Conhecer o chão antes dos astronautas |
| Arrival | 35 s | Retroestágio, separação, verniers e contatos |
| Activation | 20 s | Máquina inteira → instrumento |
| Science | 150–180 s de referência | Arrastar, aprofundar, pressionar, fotografar |
| Findings | 20 s | Efeito visível e significado científico |
| Night | 45 s | Eclipse, noite e último contato |
| Reunion | 55 s | 31 meses depois: pessoas e máquina |
| Legacy | 15 s | Fotografia histórica e o que ficou |
| End | Fora do relógio | Retorno voluntário ao arquivo |

Meta humana 5–7 minutos, sem cronômetro de falha ou ciência concluída automaticamente. Não preencher duração com esperas artificiais.

## Operação e apresentação

Mouse aponta; clique e arrasto colocam a pá em contato. Soltar levanta. E com WASD é alternativa. Deformação depende do caminho percorrido; manter a ferramenta parada não cava. Passagens sucessivas aprofundam a mesma região. A retirada sobe antes de recolher. Um teste curto de pressão compara outra resposta. A fotografia registra a geometria desta partida.

É controle direto assistido, simplificação aprovada da operação real. O atraso representativo de rádio pode ser explicado no arquivo; não atrasar mouse, interface ou pausa. Entrada por InputRouter, tempo por SessionClock, progresso por ChapterDirector.

Planos externos mostram a sonda e corpos completos. A câmera de instrumento permanece no conjunto óptico. A visita externa de 1969 é reconstrução documental, distinta da fotografia NASA; não sugerir que a câmera inativa gravou a recuperação.

Filme externo em cor contida e granulação leve. Instrumento 4:3 dominante, monocromático, scanlines/vinheta/ruído discretos e opção reduzida. Legendas e comandos nítidos após o filtro. Sem VHS, vento lunar ou narração de IA. Sons mecânicos externos são linguagem cinematográfica.

Astronautas usam malha inteira, esqueleto, clipes e correção de contatos. Pés apoiados; mãos alcançam peças; objetos passam entre suportes explícitos. Nenhum membro isolado aparece por temporizador. Revisão exige movimento contínuo, não apenas capturas.

## Produção e aceitação

SurveyorChapterBuilder continua dono da cena e delega a montagem a DocumentaryBuilder. Composição, filme, elenco, pá, superfície e apresentação são componentes separados. Entrada pública: SurveyorArchive.unity; capítulo: SurveyorChapter.unity. Modelos/conversão e créditos em SourceAssets/Surveyor3/Documentary; validação em QA/SUR-018.

Quatro de cinco novatos devem concluir em 300–420 s e entender missão, ensaios do solo, diferença 1967/1969 e retorno das peças. Automação prova fluxo, pausa/foco, descarte e regressões. Arte, compreensão e GPU integrada precisam de avaliação própria identificada. Orçamento inicial do capítulo isolado: 25 MB; medir o pacote conjunto na integração.
