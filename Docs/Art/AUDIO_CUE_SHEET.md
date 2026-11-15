# AUD-001 — Planilha de cues de música e efeitos

Dono: R7. Consumidores: AUD-002 (`AudioDirector`, `Shared/Audio/`), PIO-013 e SUR-014 (ligação aos estados), AUD-003 (mixagem final). Direção: `Docs/Art/DIRECTION.md` §Áudio e notas de áudio de cada roteiro.

**Estado em 14/11/2026: nenhum arquivo de áudio foi baixado, importado ou ouvido nesta tarefa.** As faixas e pacotes abaixo são **candidatos** cuja licença foi lida na página da fonte; a escolha final exige a escuta descrita em *Verificação* e uma linha **Importado** em `ASSET_CREDITS.md`. Sem locução (nem humana nem de IA); legendas e sinais visuais bastam com o áudio desligado.

## 1. Famílias de intenção

| Família | Código | Intenção | Caráter | Onde |
| --- | --- | --- | --- | --- |
| Curiosidade / descoberta | `CUR` | Perguntar, observar, revelar evidência | Timbres leves, pulso discreto, harmonia aberta; cresce na revelação sem pico brusco | Ciência de Marte, IPP/Ganimedes, primeira imagem e pá da Surveyor |
| Viagem / expansão | `VIA` | Partir, atravessar, escala e tempo | Sustentação ampla, pulso de partida que se dissolve em espaço | Lançamentos, pousos, cruzeiro, saída da Pioneer, elipses |
| Legado / despedida | `LEG` | Perda de energia, silêncio, o que fica | Piano/cordas, espaço entre notas, termina em repouso | Spirit, 2018, escuta final Pioneer, noite lunar e visita imaginada |

Cada capítulo usa **um motivo de cada família** (Marte: três motivos; Pioneer: os quatro cues do roteiro — Partida=`VIA`, Curiosidade=`CUR`, Distância=`VIA`/`LEG` de transição, Escuta=`LEG`; Surveyor: as três camas do roteiro — intenção=`VIA`, descoberta=`CUR`, retorno=`LEG`). Não reutilizar música de tensão/suspense em todo plano.

## 2. Regras de mixagem e transição

- Buses `Master / Music / SFX / UI` (contrato `AudioDirector.SetCue(cueId, fadeSeconds)`); um único dono do crossfade.
- Crossfades **0,8–2 s** (Pioneer: 1,5–2 s entre estados). Corte seco só onde o roteiro pede silêncio.
- *Ducking* da música: −6 dB, ataque 0,3 s, retorno 0,8 s, quando entra legenda científica ou resultado; legenda comum não aciona.
- Loops de cama ≥ 30 s, sem clique na emenda; loops de instrumento (hold) 2–6 s com variação e desaceleração ao soltar. Nada fica tocando na pausa, no menu ou no arquivo.
- Pausa/perda de foco: Music e SFX pausam; UI responde. Ao reiniciar o capítulo, todas as fontes param e o estado sonoro volta ao inicial.
- Espaço e Lua: **não** há som propagado no vácuo. O que se ouve é música e sonificação de interface/telemetria. Vibração do lançamento da Surveyor é recurso subjetivo, rotulado uma vez no arquivo.
- Vento em Marte: baixo, variável, **sem pulsação de helicóptero** e nunca ocupando a mix inteira. Rádio Pioneer: ruído de banda limitada e duração limitada; silêncio final intencional.
- WebGL: áudio só começa após o clique em **Iniciar** (AudioContext). Orçamento proposto: todo o áudio ≤ 10 MB comprimido (Vorbis), a medir em QA-001.

## 3. Mapa por capítulo

Notação: **MUS** música · **SFX** efeito · **AMB** ambiente · **SIL** silêncio deliberado · `→ 1,5 s` crossfade.

### Capítulo I — Spirit & Opportunity (32 batidas, 360 s de referência)

| Batidas | Cue / transição | Camadas | Equivalente visual do som essencial |
| --- | --- | --- | --- |
| M01–M02 | `MUS_MAR_CUR` entra do silêncio (fade 2 s) | AMB laboratório muito baixo; SFX servo curto ao revelar rodas | Placas SPIRIT/OPPORTUNITY |
| M03 | `MUS_MAR_VIA` → 1 s | SFX estrondo curto de lançamento (Terra, há ar) | Chama contida, carimbo JUN–JUL 2003 |
| M04–M05 | `MUS_MAR_VIA` sustentado | SFX entrada, paraquedas, dois impactos de airbag por metade, abafados | Split com datas UTC; quiques visíveis |
| M06 | Transição: música desce a −12 dB | SFX esvaziar airbag, pétalas, rodas desdobrando | Título DIAS DE VERIFICAÇÃO |
| M07–M08 | Música fora (fade 1,5 s); **AMB vento baixo variável** | SFX servos de rodas (assinatura afetiva) | Cartão de controles, objetivo |
| M09 | sem música | SFX hold: clique de início → zumbido do MI crescente → confirmação curta | Prompt E, barra, toast HEMATITA |
| M10–M11 | sem música | SFX braço se aproximando | Objetivo McKittrick |
| M12 | sem música | SFX RAT: início, **contato** (loop modulado pelo progresso), desaceleração ao soltar, recuo | Disco crescendo, poeira, barra; estado Pausado ao soltar |
| M13 | sem música | SFX APXS/Mössbauer: zumbido de medição + confirmação | Resultados no painel |
| M14 | `MUS_MAR_CUR` **revelação** (fade 2 s, sem aumento brusco) | Ducking durante MAR.C19 | Caderno de evidências |
| M15–M16 | `MUS_MAR_VIA` → 1,5 s | SFX carimbo/transição | ABRIL DE 2004; 2005 |
| M17–M19 | música baixa ou fora | SFX obturador Pancam; medição do meteorito | Toasts de registro |
| M20 | acento suave `MUS_MAR_CUR` | — | Foto real creditada |
| M21 (2007) | Música → drone mínimo; **SIL** relativo nas semanas | AMB vento sobe (sem pulsação); SFX desligamento por grupo (rodas, braço, instrumentos, comunicação) com cliques distintos; **retorno do motor** marca a recuperação | Céu escurece; medidor Wh/sol; cada grupo apaga no HUD |
| M22 (Spirit 45 s) | `MUS_MAR_LEG` motivo da Spirit (piano) | Sem SFX de máquina; termina rarefeito | Datas, metade escurece |
| M23–M26 (2018) | `MUS_MAR_CUR` baixo | AMB vento; SFX Pancam | Objetivo, fotos |
| M27–M28 | música recua | AMB poeira mais densa; vento ainda sem pulsação | Sol enfraquece na imagem |
| M29–M30 | sem música | SFX servo da antena; ticks de seleção | Antena move; foto destacada |
| M31 | **Camadas mecânicas somem primeiro (0–6 s)**, depois `MUS_MAR_LEG` | — | Mecanismos param, data do último sinal |
| M32 | `MUS_MAR_LEG` resolve e termina | SFX UI no botão | Título final, Voltar/Repetir |

### Capítulo II — Pioneer 10 (8 estados, 375 s)

> **Música:** os cues de música desta tabela (`MUS_PIO_PARTIDA/CUR/DISTANCIA/ESCUTA`) foram substituídos pelos sete da **seção 6** (roteiro novo, 15/11/2026). Os efeitos continuam valendo.

| Estado | Cue | Camadas | Equivalente visual |
| --- | --- | --- | --- |
| PIO.Launch | `MUS_PIO_PARTIDA` (`VIA`, pulso e crescimento) | SFX lançamento na Terra; no espaço só música; hold RTG = zumbido de interface calor→eletricidade | Cartão RTG com barra |
| PIO.Belt (fantasia 15 s) | `MUS_PIO_CUR` variação lúdica | SFX “toc” leve no contato (sem explosão) | Selo IMAGINAÇÃO, deformação gráfica |
| PIO.Belt (correção + registros) | Música quase silencia na virada (1,5 s) | SFX tique de detector por registro (UI) | Contador 1–5, brilho no sensor |
| PIO.JupiterApproach | `MUS_PIO_CUR` cresce com a escala | SFX bipe da animação Terra→sonda→Terra | Indicador de atraso de rádio |
| PIO.Imaging | `MUS_PIO_CUR` | SFX varredura (loop modulado), confirmação por faixa; **ruído de radiação curto e de baixa amplitude** + envio do comando de recuperação | Faixas preenchendo; ruído só no quadro do instrumento; notificação escrita |
| PIO.Plaque | `MUS_PIO_DISTANCIA` (delicada; pulso sai em 38–47 s) | SFX encaixe por peça; retorno neutro em erro (sem buzina) | Contorno pulsa; “3/5” |
| PIO.Outbound | `MUS_PIO_DISTANCIA` | SFX relés ao priorizar comunicação; instrumentos apagando | Barras de geração; símbolo do rádio |
| PIO.Listening | `MUS_PIO_ESCUTA` (`LEG`, piano/cordas com silêncio estruturado) | AMB ruído de rádio de banda limitada; tom de proximidade; acorde de “sinal recebido” ×3; tentativa 4 sem resposta → **SIL 2,5 s real, sem hiss em loop** | Medidor de proximidade, textos “Mais perto do sinal”, “Nenhum sinal detectado”, “Sua escuta funcionou…” |
| PIO.Farewell | `MUS_PIO_ESCUTA` resolve | Nenhum SFX externo | Sonda diminui; título |

### Capítulo III — Surveyor 3 (6 atos, 360 s)

| Ato / estado | Cue | Camadas | Equivalente visual |
| --- | --- | --- | --- |
| 1 · LaunchTelemetry | `MUS_SUR_INTENCAO` (`VIA`) baixo | SFX contagem em bipes; **vibração de estrutura subjetiva** (rotulada no arquivo) | Escala âmbar, contagem |
| 1 · CentaurConfirm | idem | SFX fila de comando: **envio** (clique imediato) → **trânsito** (tom 1,3 s) → **execução** (clique diferente) → confirmação | Fila Preparado→…→Confirmado |
| Transição | O grave some antes do título do ato 2 | — | Terra vira origem do mapa |
| 2 · Cruise/Midcourse | `MUS_SUR_INTENCAO` | SFX ticks de alinhamento; fila de comando | Contorno vs. marca sólida; “Correção preparada” |
| 3 · Descent | música cai a pulso mínimo ou fora | SFX tom de alerta curto quando o corte automático não acontece | Linha âmbar + SUR-S07 |
| 3 · Cutoff | sem música | SFX comando; **dois impactos estruturais curtos** (não explosões) | Altímetro sobe ~10 m e ~3 m; pulso no retículo (modo sem tremor) |
| 3 · Settled | **SIL 1,5 s** | — | Altímetro estável, carimbo de data |
| 4 · FirstImage | `MUS_SUR_DESCOBERTA` (`CUR`) entra com a varredura | SFX sonificação suave das linhas (sem glitch de terror) | Imagem linha por linha |
| 4 · Observe | `MUS_SUR_DESCOBERTA` baixo | SFX servo eletrônico do espelho (sonificação), registro | Moldura do alvo, carimbo da imagem |
| 4 · Sample | sem música ou muito baixa | SFX motor da pá no contato, retorno a cada 0,8–1,2 s ao segurar, carga, soltura e impacto curto | Sulco cresce, barra, marca de impacto |
| 4 · Eclipse | `MUS_SUR_DESCOBERTA` sustenta **8 s sem frases** | — | Borda avermelhada |
| 5 · LunarNight | áudio eletrônico **cessa**; só o tema liga as elipses | — | Sombras, contador TOTAL DA MISSÃO |
| 5 · TimePassage | `MUS_SUR_RETORNO` (`LEG`) | — | 1967 → 1968 → NOV 1969 |
| 6 · ImaginedVisit | **SIL** antes do contrato SUR-S26; depois `MUS_SUR_RETORNO` | **Sem passos, vento ou som do LM** (vácuo) | Legenda “A imagem que ela nunca chegou a transmitir” |
| 6 · HistoricalReceipt / End | corte para preto em silêncio; tema resolve na foto real e termina suave | SFX UI no Voltar | Foto real creditada, título |

## 4. Efeitos (SFX) e seu equivalente visual obrigatório

| ID | Função | Nunca sem | Fonte preferida |
| --- | --- | --- | --- |
| `SFX_HOLD_START` / `_LOOP` / `_DONE` / `_RELEASE` | Resposta imediata do E segurado | tecla preenchida, barra, ✓, estado Pausado | Kenney Interface/Sci-fi (CC0) ou síntese própria |
| `SFX_SERVO_*` | Afeto pela máquina (rodas, braço, antena, espelho) | movimento visível da peça | **Foley original da equipe** (servo/motor pequeno gravado) |
| `SFX_RAT_CONTACT` | Esmerilhar | disco crescendo, poeira | Foley original / Kenney Impact (CC0) |
| `SFX_SCAN` | IPP, TV linha a linha | faixa/linha avançando | Síntese própria / Kenney Digital Audio (CC0) |
| `SFX_CMD_SENT` / `_TRANSIT` / `_EXEC` | Fila 1,3 s | etapa da fila em destaque | Kenney Interface (CC0) |
| `SFX_RADIO_*` | Proximidade, recebido, ruído | medidor + texto | Síntese própria (ruído filtrado) |
| `SFX_IMPACT_*` | Airbags, quiques Surveyor, pá solta | movimento/altímetro/marca | Kenney Impact (CC0) |
| `SFX_UI_*` | Clique, foco, encaixe | foco visível | Kenney UI Audio / Interface (CC0) |
| `AMB_WIND_MARS` | Vento variável | fog/céu | Síntese própria (ruído filtrado, modulação lenta, sem pulsação) |

Nenhuma informação essencial existe só no som (critério de UI_SPEC e DIRECTION).

## 5. Fontes candidatas e direitos (lidos nas páginas em 14/11/2026)

| Fonte | Licença declarada na página | Atribuição exigida | Restrições declaradas | Uso proposto | Estado |
| --- | --- | --- | --- | --- | --- |
| **Kenney** — Interface Sounds (100 arq., 2020), Sci-fi Sounds (70, 2020), UI Audio (50, 2012), Impact Sounds (130, 2019), Digital Audio (60, 2012); https://kenney.nl/assets/… | **CC0** (“Creative Commons CC0”); FAQ: “Attribution is not required” | Não (crédito cortesia “Kenney”) | Não usar o logotipo Kenney separadamente | UI, comando, impactos, varredura | Candidato; arquivos e formatos não verificados (página não informa formato) |
| **Scott Buckley** — https://www.scottbuckley.com.au/library/ | **CC BY 4.0** (“All music is licensed under Creative Commons Attribution 4.0”) | Sim: “[Título] by Scott Buckley – released under CC-BY 4.0. www.scottbuckley.com.au” | Não revender isolado, não re-subir a plataformas de streaming de música, deve ser sincronizado com outra mídia; jogos/software aparecem cobertos pela coluna CC-BY da página de licenças | Camas musicais | Candidato; páginas individuais e durações não verificadas |
| **Kevin MacLeod / incompetech** — https://incompetech.com/music/royalty-free/licenses/ | Creative Commons, gratuito, exige crédito (FAQ: CC BY 4.0) | Sim: “Título Kevin MacLeod (incompetech.com) / Licensed under Creative Commons: By Attribution 4.0 / https://creativecommons.org/licenses/by/4.0/” | FAQ pede contato se o jogo for transmitido em stream | Alternativa às camas | Candidato; títulos não listados (catálogo carrega por script) |
| **Freesound** — https://freesound.org | Por arquivo: CC0, CC BY ou CC BY-NC (Sampling+ antigo) | Conforme o arquivo | **Usar só CC0/CC BY**; BY-NC fica fora (preferência da tarefa por CC0/CC BY) | Foley específico | Só com página do som e licença do arquivo |
| **Pixabay** | Licença própria (não é CC); sem atribuição | Não | Proíbe distribuição “standalone”; termos completos **não lidos** | — | **Não usar** até ler os termos completos |
| **FreePD** | — | — | Site **permanentemente fechado** | — | Descartado |
| **Original da equipe** (Foley gravado, síntese) | Autoria da equipe | Créditos do jogo | — | Servos, vento, rádio | Preferido para assinatura das máquinas |

Gravação comercial, trilha de filme/jogo e áudio histórico da NASA **sem página e termos específicos** não entram. A licença da composição e a da gravação são verificadas separadamente (a página Buckley/MacLeod cobre ambas por serem do mesmo autor; Freesound é por arquivo).

### Faixas candidatas (Scott Buckley, CC BY 4.0 — descrição copiada da biblioteca; escuta pendente)

| Família | Faixa | Página | Descrição na biblioteca | Encaixe proposto |
| --- | --- | --- | --- | --- |
| `CUR` | Unraveling | https://www.scottbuckley.com.au/library/unraveling/ | “Ambient, introspective strings and synth…” | Marte ciência; Pioneer IPP |
| `CUR` | With These Hands | https://www.scottbuckley.com.au/library/with-these-hands/ | “dreamy, nostalgic and contemplative… piano, strings and synth” | Surveyor primeira imagem |
| `VIA` | Aphelion | https://www.scottbuckley.com.au/library/aphelion/ | “epic, cinematic hybrid… discovering your purpose in the depths of space” | Partida Pioneer (trecho calmo; evitar picos) |
| `VIA` | Starfire | https://www.scottbuckley.com.au/library/starfire/ | “Hopeful, epic sci-fi orchestra…” | Pousos/lançamentos de Marte (curto) |
| `LEG` | In This Moment | https://www.scottbuckley.com.au/library/in-this-moment/ | “bittersweet meditation for intimate solo piano” | Spirit (M22) |
| `LEG` | Penumbra | https://www.scottbuckley.com.au/library/penumbra/ | “emotional, bittersweet… string orchestra, atmospheric synth” | Escuta Pioneer; memorial 2018 |
| `LEG` | Memories Of Stone / Wildflowers | …/memories-of-stone/ · …/wildflowers/ | “Peaceful, bittersweet strings…” / “contemplative… piano & strings” | Visita imaginada Surveyor |

“Epic” é risco para um jogo calmo: usar trechos editados (registrar corte/loop em ASSET_CREDITS) e manter crossfades longos.

## 6. Registro obrigatório ao importar (AUD-002)

Para cada arquivo: ID do cue, título, autor, URL da página específica, licença e versão, texto de atribuição exato, data de acesso, SHA-256 do original, alterações (corte, loop, fade, mixagem, conversão Vorbis), destino em `Assets/_Game/Shared/Audio/` e responsável — em `ASSET_CREDITS.md`. A tela de créditos do jogo repete a atribuição CC BY.

## 7. Verificação (pendente — R7)

- [ ] Ouvir cada loop por ≥ 30 s e sua transição por mais 30 s, em fone e alto-falante de notebook; anotar cliques na emenda e fadiga.
- [ ] Com SFX e música zerados, completar uma atividade de cada capítulo só pelos sinais visuais.
- [ ] Conferir as páginas individuais das faixas escolhidas (licença por faixa, “unless stated otherwise”).
- [ ] Medir tamanho comprimido no primeiro build Web (QA-001).

Nada desta seção foi executado em AUD-001; os itens ficam para AUD-002/AUD-003 e para quem importar os arquivos.

## 6. Música da Pioneer 10 para o roteiro novo (15/11/2026)

Substitui os quatro cues `MUS_PIO_PARTIDA/CUR/DISTANCIA/ESCUTA` por sete, um por clima do roteiro novo (`ROTEIRO_PIONEER10.md`, 6:40, nove cenas). Todas as faixas são de **Scott Buckley, CC BY 4.0** (a página de cada faixa declara a licença; ver `ASSET_CREDITS.md`, linhas `M-SB-PIO-01…07`). Os trechos são gerados por `Tools/convert_pioneer10_music.py` (recorte, loudnorm de -20 LUFS, fade de 1 s na entrada e 3 s na saída, Ogg Vorbis ~64 kbps, 4,35 MB no total). **Escolhidas pela descrição da página e pelo envelope de volume; nenhuma foi ouvida por pessoa.** Quem for aprovar deve ouvir cada uma no momento da cena.

| Cue | Faixa (trecho) | Cenas e tempo do roteiro | O que a música faz | Clima da faixa (página) |
| --- | --- | --- | --- | --- |
| `MUS_PIO_LANCAMENTO` | Chronicle (0–80 s) | 1 · 0:00–0:55 | Entra do silêncio na noite de lançamento e sobe devagar até a travessia da Lua e as baterias nucleares | começa em tons ambient escuros, sobe para a orquestra e chega a uma melodia de duduk |
| `MUS_PIO_CINTURAO` | Descent (0–60 s) | 2 · 0:55–1:33 | O medo: "ninguém sabia se dava para atravessar"; para no corte do dia 140 | escura, atmosférica, sinistra e misteriosa |
| `MUS_PIO_MARAVILHA` | The Distant Sun (0–120 s) | 2 · 1:33–1:48, 3 · 1:48–2:15 e 4 · 2:15–3:00 | O corte limpo para a calma: os asteroides longe uns dos outros, Júpiter e a foto montada linha por linha (discreta, é a mais baixa de todas) | calma, olhar para as estrelas, cordas e violoncelo solo |
| `MUS_PIO_JUPITER` | Sanctum (0–90 s) | 5 · 3:00–4:05 (`PIO.Closest`) | A radiação, 1 s de silêncio, o mergulho sobre as nuvens e o empurrão gravitacional | orquestral cinematográfica e edificante, flautas, percussão e cordas |
| `MUS_PIO_ANOS` | Extrapolation (0–110 s) | 6 · 4:05–4:25 e 7 · 4:25–5:20 | A placa dourada e os anos que passam, instrumento por instrumento | piano sombrio com cordas, levemente edificante e melancólica |
| `MUS_PIO_RADIO` | Signal to Noise (0–80 s) | 8 · 5:20–6:00 | A antena à noite; termina antes dos 2 a 3 s de silêncio total | neoclássica cinematográfica, misteriosa, melancólica e esperançosa |
| `MUS_PIO_FINAL` | Filaments (245–340 s) | 9 · 6:00–6:40 | Rumo a Aldebaran e as seis primeiras vezes; o clímax cai por volta de 15 a 55 s do trecho | piano quente e sombrio, cordas emotivas e um final dramático por volta de 4:10 |

**Estado atual do jogo (nove estados do roteiro v2):** `PIO.Launch → LANCAMENTO`, `Belt → CINTURAO`, `JupiterApproach → MARAVILHA`, `Imaging → MARAVILHA` (sem reiniciar), `Closest → JUPITER`, `Plaque` e `Outbound → ANOS` (sem reiniciar), `Listening → RADIO`, `Farewell → FINAL`. A revelação do cinturão (cena 2, 1:33) também é `MARAVILHA`: hoje ela só toca a partir de `JupiterApproach`, então o corte limpo "tensão → maravilha" dentro do Belt precisa de um evento de música no roteiro (`AudioDirector.SetCue`). Se os estados mudarem de novo, só a tabela de estados do `PioneerCueMap` (`PioneerDataBuilder`) muda; os sete cues e os clipes continuam os mesmos.

**Pontos de atenção:**
- Os trechos são curtos (60 a 120 s) e **repetem** (loop com emenda) se a cena durar mais, o que é provável no minigame e na sintonia, onde o ritmo é da pessoa; a emenda tem fade de 3 s. Se isso incomodar, trocar por um trecho mais longo ou por uma faixa com loop próprio.
- **Ligados no jogo:** o evento `PIO.Belt.Reality` troca `CINTURAO` por `MARAVILHA` (ação `Music`, crossfade de 0,8 s) na revelação do cinturão (~1:33); `PIO.Closest.Dive` (`ShotEvent` a 14 s do plano `PIO09.Radiation`, na lacuna entre legendas) pede `Silence(1 s)`; `PIO.Listening.Attempt4` pede o silêncio de 2,5 s, encaminhado pelo `PioneerChapterController` (`listening.StateEvent` → `narration.RaiseEvent`). Todos dependem de o controlador receber o `PioneerAudioAdapter`.
- **Orçamento Web:** 4,35 MB só da Pioneer, de 10 MB para todo o áudio do jogo. Reduzir (menos trechos ou bitrate menor) se Marte e a Surveyor precisarem de espaço.
- A **atribuição é obrigatória** nos créditos do jogo: já existe a linha `credits.line.pioneer_music` no dossiê (PT e EN).
