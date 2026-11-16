# Pioneer 10 — roteiro v2: balanço (o que foi feito, o que não foi e o que melhorar)

Base: `develop` com o PR 194 mesclado (lote de qualidade visual do teste em Play Mode), 15/11/2026. Este documento reúne tudo o que o roteiro v2 pedia, o que foi entregue nas fases 1 a 5, o que **não** foi feito e o que vale melhorar. Leia junto com [`FASES.md`](FASES.md) (mapa cena a cena e trocas de frase) e os READMEs de QA (`QA/PIO-019` a `QA/PIO-034`, uma pasta por parte, com a evidência e os limites de cada uma).

## 1. Em uma página

**Entregue (em `develop`):** as nove cenas existem como estados do capítulo, com as legendas novas em PT-BR e EN-UK; sete das nove são só cinema; as duas interações do roteiro (a foto em três passos e a sintonia de rádio em uma rodada) foram simplificadas; cada cena de cinema ganhou imagens novas (cinturão com dolly zoom, contador de dias e 307 Nike; Marte e a Lua; Júpiter nascendo de um ponto com a Terra pequena; luas galileanas, radiação, comparação de distâncias e a trajetória; os anos com linha do tempo e medidor de energia; as seis estrelas); e a fase 5 pôs barras de cinema e luz quente na Terra e fria no espaço. Depois do primeiro teste do responsável em Play Mode (15/11/2026) veio um lote de qualidade visual (PIO-031 a PIO-034, abaixo): céu e Aldebaran, a Terra da NASA, o cinturão como ele é, a placa de ouro como cutscene, câmeras mais abertas e a Terra com nome.

| Fase / parte | PR | O que entregou |
| --- | --- | --- |
| Antes do roteiro v2 | 152, 156, 160 | Enquadramento 3D do recuo final e do close do instrumento (152), modelo definitivo da sonda no palco (156), antena DSN e Júpiter com os recursos da NASA (160) |
| 1 | 164 | Legendas novas, estado `PIO.Closest`, 9 etapas |
| 2 | 166 | Lançamento, cinturão, placa e anos viram cinema |
| 3 | 167 | Foto em três passos e sintonia em uma rodada |
| 4 · parte 1 | 168 | Cinturão: parede de pedras, dolly zoom, contador de dias, 307 Nike |
| 4 · parte 2 | 169 | Marte e a Lua em 3D |
| 4 · parte 3 | 170 | Júpiter nasce de um ponto, Terra pequena para comparar |
| 4 · parte 4 | 172 | Luas galileanas, HUD de radiação e distâncias, trajetória curva |
| 4 · parte 5 | 173 | Os anos: HUD com linha do tempo, instrumentos e energia |
| 4 · parte 6 | 175 | As seis estrelas das primeiras vezes |
| 5 | 176 | Barras de cinema e temperatura da luz por grupo |
| Retemporização (passos 1 a 8) | 179 a 186 | Cinturão 50 s, placa 33 s, foto 64 s, escuta 40 s, despedida 40 s, Júpiter 30 s, o mais perto 65 s e os anos 55 s; o capítulo vai de 411 s para **417 s** (histórico em `QA/PIO-029`) |
| Qualidade visual · céu e Terra | 189 (PIO-031) | Céu mais escuro com Via Láctea (faixas de poeira, núcleo quente) e estrelas redondas; Aldebaran com miolo liso e halo, bem mais visível; Terra do mapa Blue Marble da NASA; teto de aprovação do capítulo nos dados de 420 s para 600 s, sem contar as duas interações |
| Qualidade visual · cinturão | 190 (PIO-032) | 70 pedras pequenas e bem separadas, zoom na sonda até 70 m, nota de que a pedra mais próxima ficou a milhões de km; o contador de dias e o rótulo de 307 Nike passam a viver no HUD de cinema (renderizado e conferido em imagem) |
| Qualidade visual · placa | 193 (PIO-033) | Placa dourada 3D com as cinco gravuras, plano geral e zoom moderado em cada peça no ritmo das legendas; sem painel nem barra de E |
| Qualidade visual · câmeras e Terra | 194 (PIO-034) | Planos livres da abertura a ~11–12,5 m da sonda (sem colar no modelo); rótulo "Terra" na cena de Júpiter |
| Os dois jogos refeitos | PIO-036 | Foto em ponto de vista (câmera girando, toque no timing, cinco fotos vermelho+azul juntas, junção automática com o ruído caindo) e sintonia do rádio sem E (quatro rodadas, zona cada vez menor, sinal cada vez mais fraco, onda de osciloscópio, chão escondido); Aldebaran menor (`QA/PIO-036`) |

**Verificação, em números (15/11/2026):**

| O quê | Resultado |
| --- | --- |
| Testes do capítulo (filtro `Pioneer`, EditMode), depois do lote visual | **234 de 234** (a suíte completa e o WebGL foram rodados antes da retemporização e do lote visual e não foram repetidos) |
| Suíte completa de EditMode (`-nographics`) | 1025 testes, **1019 passam e 6 falham**, os seis do capítulo Surveyor e todos de GPU. Rodados de novo com gráfico, os 213 testes do Surveyor passam: as seis falhas eram só do modo sem gráfico. Somados, **todos os testes passam quando há GPU**. |
| Build WebGL da cena da Pioneer (`Tools/build_release.py`) | **Sucesso**: 17 arquivos, 17,58 MiB, shaders 3/3, hashes conferidos |
| Play Mode com a interface, teste humano de leitura e controle | **Não feitos** |
| Contraste e layout dos HUDs novos (cinema) | **Não medidos** (só o painel da foto tem auditoria) |

## 2. Cena a cena

Duração: a atual (nominal do estado, depois da retemporização) contra o alvo do roteiro. Total hoje **417 s (6:57)**; alvo do roteiro **400 s (6:40)**. O teto de aprovação nos dados passou a **600 s (10 min)**, sem contar o tempo das duas interações, por pedido do responsável; o total nominal não mudou com o lote visual.

| Cena | Hoje / alvo | Feito | **Não feito** |
| --- | --- | --- | --- |
| 1 Pioneira · `PIO.Launch` | 40 s / 55 s | Legendas novas; geradores demonstrados sozinhos; Lua do gráfico Terra–Lua com mares e crateras; Terra do gráfico com o mapa da NASA; planos livres da sonda a ~11–12,5 m | Chegar a 55 s (com o teto de 600 s agora é possível, mas ainda não foi feito); contador "11 horas depois"; sobrevoo da Lua em 3D; montagem de RTGs de outras sondas; brilho dos RTGs ainda são esferas aditivas simples |
| 2 Cinturão · `PIO.Belt` | 50 s / 50 s | Cinturão como ele é (70 pedras pequenas e separadas, visão larga e zoom na sonda até 70 m, nota da pedra mais próxima a milhões de km), contador de dias (15 JUL 1972 a 15 FEV 1973) e rótulo de 307 Nike renderizados no HUD de cinema, Marte na abertura | Linha tracejada até a pedra mais próxima (só a nota de texto); lente de 5° (o contrato de planos aceita no mínimo 10°); música tensa; escala e distâncias entre pedras são de composição |
| 3 O rei dos planetas · `PIO.JupiterApproach` | 30 s / 30 s | Júpiter cresce de um ponto até ~73% da altura do quadro; Terra (mapa da NASA) 11,2 vezes menor entra ao lado, com o rótulo "Terra" | Grande Mancha Vermelha cruzando; brilho no limbo; terminador marcado; bandas em movimento |
| 4 Foto em três passos · `PIO.Imaging` | 64 s / 45 s | Três passos (vermelho, azul, juntar), sem radiação nem recuperação | Chegar a 45 s: só ler os textos exige ~45 s e as três legendas dos passos precisam de leitura em sequência (mínimo ~56 s sem cortar texto); barras de cinema (é interação) |
| 5 O mais perto possível · `PIO.Closest` | 65 s / 65 s | Três luas em 3D; medidor de radiação com estática; barras Terra–Lua e sonda–nuvens; "mais de 120.000 km/h"; trajetória curva em plano alto | Fotos reais das luas com crossfade; Io com o "X"; comandos falsos num painel; mergulho rente às nuvens com paralaxe forte; campo gravitacional pulsando; seta de Júpiter ao redor do Sol; velocímetro subindo; linhas Terra–Lua em 3D |
| 6 Uma carta para as estrelas · `PIO.Plaque` | 33 s / 20 s | Cutscene pura: placa dourada 3D com as cinco gravuras, plano geral e zoom em cada peça (sem E, sem painel) | Gravuras são os desenhos simplificados do projeto, não a reprodução da placa real; chegar a 20 s (só ler os sete textos exige ~29 s; 33 s é o mínimo com folgas) |
| 7 Os anos · `PIO.Outbound` | 55 s / 55 s | HUD: ano de 1974 a 1998 parando nos marcos reais, linha do tempo, cinco ícones que apagam em turnos, medidor de energia com 155 W e ~140 W | Desligamentos com data (não verificadas); "[ano] — [nome] desligado" por 3 s; som de clique e de "tique"; ano de 2001; ~65 W e a linha de ~100 W |
| 8 Trinta anos depois · `PIO.Listening` | 40 s / 40 s | Uma única rodada de sintonia | Barras de cinema nos trechos de cinema |
| 9 Rumo a Aldebaran · `PIO.Farewell` | 40 s / 40 s | Seis estrelas acendem uma a uma com rótulos, todos com fonte; Aldebaran com halo, visível no afastamento e no memorial inteiros, câmera a ~16 m | Plano final com a placa refletindo Aldebaran e a sonda virando um ponto entre estrelas coloridas; constelação de Touro |

**Transversal:** retemporização feita onde cabe (as quatro exceções, `PIO.Launch`, `PIO.Plaque`, `PIO.Imaging` e o total de 417 s contra 400 s, estão na tabela acima e no item 2 da seção 6); áudio novo (nenhum som, trilha, clique ou estática foi criado; os eventos de plano seguem disponíveis para o áudio); correção de cor por cena e pós-processamento (fora do perfil Web leve, por decisão); easing nas barras de cinema; o modelo do foguete Atlas-Centaur continua só uma silhueta; PIO-016 (acabamento visual da sonda) adiada para o fim e ainda aberta.

## 3. Fatos, fontes e textos

- **Textos:** os 149 textos do capítulo entram como `derived`; precisam de revisão da R7, inclusive a versão EN-UK. O validador rejeita as formulações do registro que o roteiro contradizia (lista em `FASES.md`).
- **Conferido na web em 15/11/2026 (pendente de R7 contra a NASA):**
  - 155 W no lançamento e ~140 W em Júpiter (Wikipedia e espelhos; algumas fontes dão 165 W nominais).
  - Quatro "primeiros" na página da NASA dos 50 anos do lançamento: projetada para voar além de Marte; primeira travessia do cinturão; primeira em trajetória para sair do Sistema Solar; primeiro objeto a deixar os planetas exteriores para trás (13/6/1983). Datas da página: saída do cinturão em 15/2/1973, 307 Nike em 2/12/1972, Netuno em 13/6/1983, fim do contato de rotina em 31/3/1997, último sinal em 23/1/2003.
- **Não confirmado e portanto fora do jogo:** "~65 W em 2001" (a fonte que o cita põe 65 W em 2005); as datas de desligamento do roteiro (1980, 1986, 1989, 1993, 1995), que contradizem o que as fontes dizem (divisão de energia entre instrumentos desde fevereiro de 1985; fim da ciência de rotina em 30/9/1995); "primeira a usar energia totalmente nuclear" (a NASA só diz que usava RTGs); a duração de 21 meses de projeto e "mais de 30 anos" seguem como já estavam no registro.
- **Mapa da Terra da NASA (Blue Marble, NASA SVS 2915, 2048×1024):** entrou por pedido do responsável; a licença e a atribuição estão em `ArtSource/Pioneer10/PIO-031/SOURCE.md` e **pendem de conferência da R7**.
- **Curvas e desenhos estilizados (e ditos assim no painel):** a queda de energia depois de Júpiter; a subida do medidor de radiação; a curva da trajetória (38°), as superfícies de Marte, da Lua e das luas galileanas (procedurais, não mapas reais) e as gravuras da placa dourada (desenhos do projeto); as escalas (luas e Marte maiores que a razão real com Júpiter, para aparecerem).

## 4. Verificações que nunca foram feitas

- **Play Mode** com a interface de ponta a ponta e **teste humano** de leitura (a regra de 16 caracteres por segundo vale para o texto, não foi medida com pessoas) e de controle.
- **Contraste e layout** dos HUDs de cinema (`CinemaHud`), do contador do cinturão, do rótulo de 307 Nike e do rótulo da Terra. A estática foi vista em quadros isolados, não em movimento.
- **Desempenho em WebGL rodando**: o build passa e tem 17,58 MiB, mas ninguém mediu quadros por segundo. Pontos de atenção: o campo de pedras (70 pedras), os 3.000 discos de estrelas em geometria, as texturas de 1024×512 e a estática do medidor.
- **Revisão R7** dos textos `derived` e dos fatos pendentes acima.

## 5. Dívida técnica e limpeza

- Quadros de `Docs/Art/Pioneer10_Belt` (cinturão interativo) e alguns outros ficaram com a luz antiga; só os do lançamento, da escuta e do palco foram regenerados na fase 5.
- A atividade interativa do cinturão (`BeltInvestigation`, `PIO_Belt`), a recuperação da foto (`ImagingRecovery`) e o atlas do `Outbound` continuam no código e nos testes, mas o capítulo não os inicia (ficaram como "modo antigo" por propriedade).
- Execuções de teste em lote deixam alterações locais em `ProjectSettings/GraphicsSettings.asset`, `QualitySettings.asset` e `QA/SUR-*` que não entram em commit; vale decidir se são esperadas.
- A pasta `QA/PIO-024/hud` versionada tem quadros com nomes antigos (da retemporização); a captura (`PioneerHudCapture`) já escreve os nomes novos e apaga a pasta ao começar, então vale regenerar e versionar a pasta inteira.
- Quadros de `Docs/Art/Pioneer10_Launch` (`PioneerLaunchCapture`) não refletem os planos novos da abertura nem os atores (a captura não roda os eventos); a evidência das câmeras novas está em `QA/PIO-034`.
- Arquivos de log e de captura soltos (`QA/PIO-0*/builder*.log`, `editmode.log`) estão sem versionar na pasta de trabalho.
- O `develop` local ainda não foi alinhado ao `origin/develop` (a história remota foi reescrita antes; o conteúdo é o mesmo), e a branch remota `feature/PIO-020-roteiro-v2-fase3` está obsoleta. Nada disso foi feito sem autorização.

## 6. O que melhorar (em ordem sugerida)

1. **Play Mode e teste humano.** É o maior risco que sobrou: tudo foi conferido por testes de editor e quadros isolados. Um passeio completo de 6:51, medindo leitura das legendas, sobreposição de HUD e legenda, e se alguém consegue completar as duas interações.
2. **Fechar a retemporização** (feita nos passos 1 a 8; sobram o lançamento, a placa e a foto). Com o teto de 600 s, o lançamento (40 s, alvo 55 s) já pode ganhar os 15 s que faltam (a cena de abertura ainda tem planos livres da sonda a 11–12,5 m que aguentariam mais tempo); a placa (33 s, alvo 20 s) e a foto (64 s, alvo 45 s) só chegam ao roteiro cortando ou encurtando texto, o que muda a narrativa e exige a R7. Ainda vale rever, em Play Mode, se o ritmo de cada cena respira bem em segundos reais.
3. **Acessibilidade da estática de radiação:** o tremor a 24 quadros por segundo merece uma opção de movimento reduzido (e deixar a opacidade fixa), por sensibilidade à luz; as barras de cinema e o crescimento de Júpiter também podem respeitar o mesmo ajuste.
4. **Áudio.** Trilha e efeitos do roteiro (música tensa no cinturão, silêncio de 1 s antes da máxima aproximação, estática no som, cliques de desligamento, tique dos anos) ainda não existem; os eventos de plano já marcam os instantes.
5. **Imagens reais de domínio público da NASA:** fotos da Pioneer 10 das luas e de Júpiter (crossfade da cena 5), mapas reais da Lua e de Marte no lugar das texturas procedurais (a Terra já usa o Blue Marble), uma foto de Io perdida só como "X" e, na cena 6, a gravura real da placa no lugar dos desenhos simplificados.
6. **Cena 3:** a Grande Mancha Vermelha cruzando, brilho no limbo e terminador marcado (o planeta ainda é uma textura estática).
7. **Cena 9:** o plano final (placa refletindo Aldebaran, sonda como ponto entre estrelas coloridas) e a constelação de Touro; estrelas desenhadas no lugar dos discos.
8. **Cena 2:** auditar contraste do contador e do rótulo de 307 Nike (já renderizados); uma linha tracejada até a pedra mais próxima; a teleobjetiva de 5° se o contrato de planos (`ShotDefinition`) aceitar valores abaixo de 10° (mudança de contrato, exige acordo com a R1).
9. **Cena 7:** ano de 2001 e os desligamentos com data, assim que a R7 confirmar as datas e o ~65 W; "[ano] — [nome] desligado" por 3 s.
10. **Barras de cinema:** easing no deslize, e decidir se os trechos de cinema das cenas 4 e 8 ganham barras.
11. **Cor por cena:** correção de cor por cena e pós-processamento só se o perfil Web aguentar (hoje fora do escopo).
12. **Acabamento visual que sobrou do teste do responsável:** o modelo da sonda ainda tem poucos polígonos (PIO-016), o brilho dos RTGs é de esferas simples, ainda sobram estrelas fracas da textura do céu (800) que viram quadrados no zoom mais fechado, e o Atlas-Centaur segue uma silhueta.
13. **Limpeza:** os itens da seção 5.
