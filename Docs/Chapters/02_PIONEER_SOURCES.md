# Pioneer 10 — fontes, correções e referências de produção

Pesquisa de planejamento conferida em **14/11/2026**. Nenhum asset desta lista é declarado baixado, importado ou testado apenas por aparecer aqui. O documento separa história, interpretação e visualização de gameplay. O roteiro final está em [02_PIONEER_10.md](02_PIONEER_10.md).

## Catálogo de fontes primárias

| ID | Fonte original | Uso e observação |
| --- | --- | --- |
| P-S01 | [NASA Science — Pioneer 10](https://science.nasa.gov/mission/pioneer-10/) | Resumo da missão, marcos, Saturno/Netuno, último dado e último sinal. A tabela de lançamento contém inconsistência de data UTC; usar P-S03/P-S04 para essa precisão |
| P-S02 | [NASA — 50 Years Ago: Pioneer 10 Launches to Explore Jupiter](https://www.nasa.gov/history/50-years-ago-pioneer-10-launches-to-explore-jupiter/) | Objetivos, 11 horas, risco de poeira, 15/2/1973, imagens e sinal final. Não copiar suas contagens de RTGs sem comparar documentação técnica |
| P-S03 | [NASA NTRS 19730011461 — relatório de lançamento](https://ntrs.nasa.gov/api/citations/19730011461/downloads/19730011461.pdf) | Descreve lançamento em 3/3/1972, 01:49 GMT. Documento contemporâneo, prevalece sobre metadado resumido inconsistente |
| P-S04 | [JPL — Pioneer Mission Support](https://ipnpr.jpl.nasa.gov/progress_report/IX/IXE.PDF) | Confirma a data/hora UTC do lançamento |
| P-S05 | [NASA NTRS 19740018157 — The Pioneer 10 Mission](https://ntrs.nasa.gov/api/citations/19740018157/downloads/19740018157.pdf) | Eixo de rotação paralelo à antena, estabilização por spin, refletor de 2,74 m e trânsito lunar |
| P-S06 | [NASA PDS — Imaging Photopolarimeter for P10](https://pds.nasa.gov/ds-view/pds/viewContext.jsp?identifier=urn%3Anasa%3Apds%3Acontext%3Ainstrument%3Aipp.p10) | Instrumento, faixas estreitas por rotação, medições em vermelho e azul |
| P-S07 | [NASA Ames — Astrogram, dezembro de 1973](https://history.arc.nasa.gov/Astrogram/astrogram_1973_12.pdf) | Montagem de cor com canal verde artificial a partir dos canais recebidos |
| P-S08 | [NASA SP-349 — Pioneer Odyssey: Encounter With a Giant](https://ntrs.nasa.gov/citations/20190002224) | Sequências de recuperação, comandos indevidos no instrumento e perdas de imagem; a seção de processamento explica a reconstrução cromática. PDF completo disponível na ficha |
| P-S09 | [NASA — Pioneer 10: Ganymede](https://science.nasa.gov/resource/pioneer-10-ganymede/) | Fotografia histórica de Ganimedes, data e crédito NASA |
| P-S10 | [NASA — Pioneer 10 at Jupiter](https://science.nasa.gov/resource/pioneer-10-at-jupiter/) | Referência histórica de Júpiter; validar o download exato e a legenda do item, não apenas o título da página |
| P-S11 | [NASA — Pioneer Plaque](https://science.nasa.gov/resource/pioneer-plaque/) | Propósito dos símbolos e referências da gravura; o arquivo de jogo será adaptação educativa identificada |
| P-S12 | [NASA Ames — Astrogram, março de 1972](https://history.arc.nasa.gov/Astrogram/astrogram_1972_3.pdf) | Material e dimensões da placa: alumínio anodizado dourado, não ouro maciço |
| P-S13 | [NASA/JPL — Voyager 1 Now Most Distant Human-Made Object](https://www.jpl.nasa.gov/news/voyager-1-now-most-distant-human-made-object-in-space/) | Marco de 17/2/1998 e direções quase opostas; a “ultrapassagem” é de distância, não encontro entre naves |
| P-S14 | [NASA — Pioneer 10 Sends Last Signal](https://science.nasa.gov/missions/pioneer/pioneer-10-sends-last-signal/) | 27/4/2002 telemetria; 23/1/2003 portadora fraca; 7/2/2003 sem detecção; tentativa de 2006 e queda de potência |
| P-S15 | [NASA Helio Data — Pioneer10](https://helio.data.nasa.gov/mission/Pioneer10) | Aquisições em 3/3/2002 e 27/4/2002, coerentes com calendário UTC |
| P-S16 | [NASA — 10 Facts about the Pioneer Spacecraft](https://science.nasa.gov/missions/pioneer/pioneer-11/10-things-about-pioneer-spacecraft/) | Legado, redução de potência, direção geral de Aldebaran e referência histórica de milhões de anos |
| P-S17 | [NASA 3D Resources — Pioneer 10](https://science.nasa.gov/3d-resources/pioneer/) | Página oficial do modelo GLB, crédito NASA/JPL/Eyes on the Solar System; página anuncia original de cerca de 1,95 MB. Tamanho final importado ainda precisa ser medido |
| P-S18 | [NASA — Images and Media](https://www.nasa.gov/nasa-brand-center/images-and-media/) | Conferir identificação de terceiros, créditos, restrições de marca e ausência de endosso no pacote de assets |
| P-S19 | [JPL TM 33-584, vol. III](https://ntrs.nasa.gov/api/citations/19750020028/downloads/19750020028.pdf) | Luz de rádio: cerca de 46 minutos em um sentido e 92 de ida/volta no encontro de Júpiter |

As fichas técnicas são referências de pesquisa; não é necessário embutir PDFs completos na build. Guardar somente os IDs/URLs e os dados factuais necessários, com créditos em arquivo e no menu.

## Registro de fatos utilizados

| Fato / afirmação | Verificação | Aplicação no capítulo |
| --- | --- | --- |
| Primeira missão NASA aos planetas exteriores, primeira travessia do cinturão e primeiro sobrevoo de Júpiter | P-S01, P-S02 | Identidade do dossiê e conclusão; “primeira” é histórica, não comparação de recorde atual |
| Noite de 2/3/1972 na Flórida corresponde a 3/3/1972 em UTC | P-S03, P-S04 | Cartão duplo de data, sem misturar hora UTC com dia local |
| A sonda cruzou a distância orbital da Lua em menos de 11 horas | P-S02, P-S05 | Elipse com órbita gráfica; não afirmar que voou rente à superfície lunar |
| A rotação estabiliza a atitude, e o eixo da antena se relaciona ao eixo de spin | P-S05 | Rotação visual constante; não usar “sempre de pé” no espaço |
| RTGs fornecem energia por conversão do calor do decaimento radioativo | P-S02, P-S16 | Pequeno cartão calor→eletricidade; sem painéis solares e sem recarga solar |
| O risco importante incluía partículas pequenas desconhecidas, não uma parede de asteroides | P-S02, P-S08 | Contraste entre fantasia identificada e espaço real vazio |
| Entrada no cinturão em julho de 1972; saída no começo de 1973 | P-S01, P-S02 | O filme usa julho→fevereiro, com meses comprimidos |
| Canais vermelho e azul medidos pelo fotopolarímetro; verde reconstruído no processamento | P-S06, P-S07, P-S08 | Minigame não imita câmera RGB; oito faixas representam muitos registros |
| A radiação disparou funções de comando indevidas; sequências de recuperação ajudaram a reconfigurar o instrumento | P-S08 | Botão representa trabalho da equipe, acompanhado de elipse temporal |
| Houve perda da imagem de Io e de partes de outras imagens | P-S02, P-S08 | Não mostrar Io como foto histórica recebida pela Pioneer 10 |
| Pioneer produziu imagens próximas de Ganimedes | P-S09 | Foto histórica real aparece depois da montagem de Júpiter |
| Placa de alumínio anodizado dourado com símbolos da humanidade e origem | P-S11, P-S12 | Adaptação de cinco peças; material não é ouro maciço |
| Cruzar órbitas não equivale a visitar os planetas; ir além de Netuno não equivale a cruzar a heliopausa | P-S01, P-S02 | Diagrama orbital com datas, sem Saturno/Urano junto à sonda e sem uma falsa borda do Sistema Solar |
| Projetada para voar além de Marte; primeira travessia do cinturão; primeira enviada em trajetória para sair do Sistema Solar; ao passar a órbita de Netuno (13/6/1983), primeiro objeto humano a deixar os planetas exteriores para trás | P-S02 (página lida em 15/11/2026; pendente de R7) | Seis estrelas das primeiras vezes na despedida; **não** há "primeira com energia totalmente nuclear" (a página só diz que usava RTGs) |
| Potência dos RTG: ~155 W no lançamento e ~140 W em Júpiter | Wikipedia (Pioneer 10) e espelhos, busca de 15/11/2026; pendente de R7 contra a NASA | Só estas duas marcas no medidor dos anos; o "~65 W" do roteiro vem de fonte que o põe em 2005 e não é usado |
| Em 1998 Voyager 1 superou a distância heliocêntrica da Pioneer 10 | P-S13 | Duas linhas de gráfico, com referência “distância ao Sol”; sem modelar encontro |
| A rotina científica terminou em 31/3/1997 por motivos orçamentários; contatos intermitentes continuaram | P-S01, P-S14 | Ficha opcional do arquivo, separada da redução gradual de potência; não atribuir o marco de 1997 à energia |
| Última telemetria em abril de 2002 é diferente de último sinal fraco em janeiro de 2003 | P-S14, P-S15 | Rodadas do rádio têm resultados distintos, não três downloads de ciência |
| No último contato a distância era cerca de 12,23 bilhões de km e a luz levava 11h20 em um sentido | P-S01, P-S02 | Fato histórico fixo; não reutilizar como distância atual de 2026 |
| A queda de potência explica a incapacidade de continuar transmitindo; não há telemetria de um “instante da bateria zerada” | P-S14 | Dizer energia insuficiente para nos alcançar, sem explosão nem shutdown interno literal |
| Nova busca em 2006 também não detectou sinal | P-S14 | Arquivo histórico opcional; a cena de fevereiro não se anuncia como última tentativa de todas |
| A trajetória geral aponta para a região de Aldebaran; milhões de anos são uma referência de escala histórica | P-S16 | Estrela distante e direção geral. Não prometer interceptação exata da estrela em movimento |

## Correções do roteiro inicial aprovadas para planejamento

| Formulação inicial | Decisão de direção / ciência |
| --- | --- |
| “Ninguém tinha ido além de Marte” | Trocar por “uma pequena sonda partiu para os planetas exteriores”. É uma abertura clara, evita exigir do leigo a distinção entre órbita planetária e viagem a um planeta |
| “Era a máquina mais rápida” | Se aparecer no arquivo, qualificar **na partida da Terra, à época**, segundo P-S02. Não é recorde atual nem velocidade absoluta sem referencial |
| “Assim fica de pé e apontada para a Terra” | Explicar estabilidade por spin; apontamento também envolve manobras, não uma consequência mágica da rotação |
| “Baterias nucleares” | No texto principal usar geradores. Termo popular pode aparecer apenas explicado; não representar um RTG como bateria recarregável |
| Pedras desviadas para sobreviver | Preservar só como fantasia de 15 s, com selo visível e sem dano. A seção factual ensina o contraste e apresenta detecção de partículas |
| “Radiação bagunçou o computador” | Nomear o instrumento e os comandos indevidos. A tecnologia de controle da Pioneer não é a de um computador moderno de navegação |
| Misturar vermelho+azul e revelar foto em cor | Mostrar explicitamente que o processamento estimava verde. Não animar magia de cor como medição de três canais |
| Placa “de ouro” criada em três semanas | Corrigir material. Não foi localizada nesta pesquisa uma fonte NASA primária suficiente para a frase de três semanas; fica fora das legendas P0, como pesquisa opcional P2 |
| “Primeira máquina além de todos os planetas conhecidos” | Descrever cruzamento de distâncias orbitais, com 1983 marcado; não confundir com visita a Netuno ou saída da heliosfera |
| “Energia acabou” | Usar “sem energia suficiente para transmitir”. Não sabemos uma sequência interna completa e observada de falhas após o último dado |
| Aldebaran ilumina a placa | A luz próxima da placa é linguagem de apresentação; ela apaga antes do plano espacial. Aldebaran continua distante e não fornece iluminação fisicamente relevante ali |
| “Vai chegar a Aldebaran em 2 milhões de anos” | Legenda fala em direção geral e escala de milhões de anos. Número histórico exato pode ir ao arquivo, qualificado; não é uma previsão de encontro futuro |

## Divergências e erros que a equipe deve evitar copiar

1. **Data UTC:** P-S01 combina 2 de março com 01:49 UT na tabela, enquanto relatórios contemporâneos P-S03/P-S04 registram 3 de março GMT. Guardar o conflito no registro; a data local da Flórida continua 2 de março.
2. **A saída do cinturão:** fontes NASA usam janeiro de 1973 para saída de uma região definida e fevereiro para conclusão oficial da travessia. O texto principal usa meses e fevereiro como fechamento, sem um cronômetro diário de fronteira física inexistente. P-S02 especifica 15 de fevereiro; não inverter entrada e saída por copiar resumo errado.
3. **Registro PDS de host:** a ficha `instrument_host:spacecraft.p10` encontrada na pesquisa contém referências a painéis solares e dados até 1983, incompatíveis com relatórios e últimos contatos documentados. Ela não é fonte autorizada para esses itens. A ficha **instrument:ipp.p10** P-S06 descreve o instrumento e é usada junto de SP-349.
4. **Contagem de RTGs:** páginas resumidas contam pares/unidades de modo inconsistente. O roteiro não exibe quantidade textual. O artista deve reproduzir o modelo e desenhos técnicos oficiais, sem corrigir a geometria por uma linha de marketing.
5. **Aldebaran e fronteiras:** páginas de divulgação antigas por vezes usam “deixar o Sistema Solar” no sentido de ir além dos planetas. No jogo, a linguagem é precisa e compreensível: além das órbitas planetárias; não afirmar uma heliopausa medida pela Pioneer.

## Referências de asset e status

| Asset | Origem original / instituição | Status e tarefa | Uso/limite |
| --- | --- | --- | --- |
| Modelo Pioneer 10 GLB | P-S17 — NASA/JPL/Eyes | Aguardando arquivo da equipe ou importação autorizada em PIO-002 | Conferir placa, refletor, hastes, texturas, escala e material; derivar LOD sem modificar silhueta de missão |
| Atlas-Centaur de lançamento | Fotos/desenhos em P-S02, NASA | Referência identificada; modelo de produção ainda não feito | Versão leve em silhueta é suficiente para 10 s, sem reaproveitar um foguete comercial sem licença |
| Júpiter histórico de Pioneer | P-S10 / recursos vinculados a P-S02, NASA | Download exato pendente PIO-007 | Confirmar que é produto real da Pioneer, não conceito artístico ou foto Juno |
| Ganimedes de 1973 | P-S09, NASA | Download pendente PIO-007 | Preservar baixa resolução histórica; não gerar detalhe falso com IA e dizer “foto real” |
| Placa em contorno | P-S11/P-S12 | Adaptação artística educativa pendente PIO-009 | Símbolos científicos preservados; humanos em silhueta conforme decisão da equipe; registrar derivação |
| Antena DSN | Fotografias institucionais NASA/JPL a selecionar na tarefa | Referência específica pendente PIO-011 | Silhueta de prato, não cenário de controle inteiro; crédito da foto escolhido antes de importar |
| Música / efeitos | Sistema global, licença verificada por R7 | Nenhuma faixa nova declarada obtida | Quatro cues, arquivos de licença, crossfades; espaço sem vento físico |

Todo recurso externo terá URL da página original, criador/crédito, licença/termos, data de aquisição, SHA-256 do arquivo original e caminho importado em ASSET_CREDITS. Não tratar automaticamente um logo NASA ou crédito de terceiros como domínio público. O jogo não usa a marca para sugerir endosso.

## Escalas e simplificações registradas uma vez

As telas instrumentais mostram uma síntese acessível: cinco eventos de poeira, oito conjuntos de linhas, cinco peças da placa e quatro janelas de escuta. Essas quantidades são de gameplay. A proximidade visual de planetas e veículo favorece composição; valores apresentados como históricos permanecem fiéis. Contadores relativos sem unidade física são chamados de progresso, não telemetria original. O dossiê/arquivo esclarece condensação de tempo e comandos, sem espalhar avisos ilustrativos por toda ação.
