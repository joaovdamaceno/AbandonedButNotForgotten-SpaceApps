# Surveyor 3 — registro científico e decisões editoriais

Revisado em 14/11/2026 para o planejamento do novo projeto. Referências abaixo foram consultadas; isso não significa que assets foram baixados, autorizados ou importados. Uma página oficial também pode conter erros: divergências são registradas em vez de combinadas numa falsa certeza.

## Fontes primárias / institucionais

| ID | Fonte e URL | Uso neste capítulo |
| --- | --- | --- |
| S3-01 | [NASA Science — Surveyor 3](https://science.nasa.gov/mission/surveyor-3/) | Visão geral, Centaur, contato, amostragem, número de imagens e anomalia do pouso; não usar isoladamente para cronologia Apollo. |
| S3-02 | [JPL — Surveyor 3 mission](https://www.jpl.nasa.gov/missions/surveyor-3/) | Contexto: engenharia, imagens e propriedades físicas do solo antes das missões humanas. |
| S3-03 | [JPL Technical Report 32-1177 — Surveyor III Mission Report](https://www.nasa.gov/wp-content/uploads/static/history/alsj/a12/Surveyor-III-MIssionRpt1967028267.pdf) | Relatório contemporâneo: introdução, voo, pouso e experimentos; base das dimensões/poses e do cenário. |
| S3-04 | [NTRS 19680009189 — System performance analysis](https://ntrs.nasa.gov/api/citations/19680009189/downloads/19680009189.pdf) | Seção 4.1: lançamento, motores comandados pelo solo e encerramento das operações. |
| S3-05 | [NASA SP-184 — Surveyor Program Results](https://www.nasa.gov/wp-content/uploads/static/history/alsj/NASA-SP-184.pdf) | Seção de televisão, p.119 em diante: eclipse da Terra/Sol e enquadramento limitado pelo espelho. |
| S3-06 | [NASA — Eclipses Near and Far](https://www.nasa.gov/history/eclipses-near-and-far/) | Eclipse visto da Lua em 24/04/1967 e distinção em relação a um eclipse observado da Terra. |
| S3-07 | [NASA — 55 Years Ago: Apollo 12 Makes a Pinpoint Landing](https://www.nasa.gov/history/55-years-ago-apollo-12-makes-a-pinpoint-landing-on-the-moon/) | Pouso Apollo 12, segunda EVA, retirada das peças e 31 meses na Lua. |
| S3-08 | [NASA — Apollo 12 and Surveyor 3](https://science.nasa.gov/resource/apollo-12-and-surveyor-3/) | Fotografia real datada de 20/11/1969; relação espacial entre os dois veículos. A legenda usa data local para o pouso de 1967. |
| S3-09 | [NASA/JPL — Surveyor 3 Camera Returned from the Moon](https://www.jpl.nasa.gov/blog/2012/1/surveyor-3-camera-returned-from-the-moon) | Retorno da câmera, quarentena e investigação em Terra. |
| S3-10 | [NTRS 19710024418 — Test and Evaluation of the Surveyor 3 Television Camera Returned from the Moon](https://ntrs.nasa.gov/citations/19710024418) | Identificação da câmera que voou; título e resumo confirmam estudo após retorno. PDF integral excedeu o limite do navegador, portanto detalhes não lidos não são afirmados. |
| S3-11 | [NASA SP-284 — Analysis of Surveyor 3 Material and Photographs](https://www.nasa.gov/wp-content/uploads/static/history/alsj/a12/AnalysSurvIIIMtrial.pdf) | Capítulo sobre solo deslocado pela Apollo e mudança de cor; distinguir poeira de alteração da tinta por exposição. |
| S3-12 | [Smithsonian — Camera, Television, Surveyor, A19761264000](https://airandspace.si.edu/collection-objects/camera-television-surveyor/nasm_A19761264000) | Foco 25/100 mm, espelho e modos 200/600 linhas. **Este objeto é artigo de teste**, não a peça retornada pela Apollo 12. |
| S3-13 | [Smithsonian — Surveyor III Television Camera, image 2006-1139](https://airandspace.si.edu/multimedia-gallery/image/2006-1139hjpg) | Identifica a câmera retornada. Fotografia com copyright Smithsonian: não presumir reutilização livre. |
| S3-14 | [NSSDCA — Surveyor program](https://nssdc.gsfc.nasa.gov/planetary/lunar/surveyor.html) | Segunda referência de tempo/local: informa 00:04:53 UT, diferença em relação ao primeiro toque do relatório. |

## Matriz de afirmações, confiança e tratamento

| Afirmação | Fonte | Decisão publicável / contexto |
| --- | --- | --- |
| Lançamento em 17/04/1967, 07:05:01 UTC, Cape Kennedy, Atlas-Centaur AC-12, plataforma 36B. | S3-03, S3-04 | Verificado. Em tela usar precisão de minuto; segundos/plataforma ficam no arquivo. Não converter para hora brasileira sem mudar data/contexto. |
| O Centaur fez partida a partir de órbita de estacionamento. | S3-01, S3-03 | Verificado. A confirmação do jogador é representação de ordens da equipe, não joystick original da sonda. |
| Massa de lançamento 997,9 kg. | NASA Beyond Earth, tabela Surveyor III; S3-03 para configuração | Apenas arquivo, identificada como **massa de lançamento**. Não atribuir ao veículo já assentado depois de perder propelente/retroestágio. |
| Comando demora aproximadamente 1,3 s da Terra à Lua. | Distância média Terra–Lua / velocidade da luz; regra didática | Valor representativo de ida. Distância real varia. Confirmação por rádio exige ida e volta; UI local não recebe atraso. Não apresentar fila do jogo como reprodução de logs de 1967. |
| A correção teve cerca de 4,19 m/s. | S3-03, seção de operação de voo | O trecho indexado informa 4,19 m/s. Valor opcional só no arquivo após confirmar página no PDF; minigame usa indicador qualitativo. |
| Radar e motores: corte esperado perto de 4,3 m não ocorreu; houve recontatos com elevação aproximada de 10 m e 3 m. | S3-01, S3-03, S3-04 | Verificado na síntese e no relatório. Não atribuir falha a impulso do jogador. Não chamar o motor vernier de motor principal/retro motor por simplificação confusa. |
| O desligamento final foi comandado da Terra. | S3-04 | Verificado. A janela de interação é alongada para acessibilidade; sem alegar momento exato de comando do operador. |
| Hora do pouso: 00:04:17 ou 00:04:53 UTC. | S3-03 e S3-14 | Relatório usa 17; catálogo usa 53. Compatível com primeiro contato versus assentamento final, mas esta interpretação exige confirmação de evento no relatório. Em tela **00:04 UTC**, não resolver a divergência por suposição. |
| Local: 2,94° S / 23,34° O ou aproximadamente 3° S / 23,41° O. | S3-03 e S3-01 | Diferenças de referência/refino. Publicar região e cratera; não misturar precisão de catálogos distintos. Cenário usa o contexto fotografado, não uma coordenada montada de duas fontes. |
| Cratera de aproximadamente 200 m e veículo inclinado na encosta. | S3-03, S3-05 | Verificado; este relevo torna possível o campo de visão do eclipse. Não usar piso plano com estrela pintada no alto. |
| A missão fez 6.326 imagens. | S3-01 e NASA Beyond Earth | Número adotado para total da missão. Outros textos distinguem categorias/intervalos e indicam 6.315; não somar categorias. O jogador faz só três registros e não produz o total histórico. |
| Câmera tinha 25/100 mm, espelho móvel e modos 200/600 linhas. | S3-12 | Verificado em descrição técnica de um artigo de teste equivalente. Varredura do jogo é acelerada e monocromática por direção artística; não afirmar que toda imagem histórica tinha esse modo. |
| Pá: alcance até aproximadamente 1,5 m, escavação de até 18 cm. | S3-01, S3-03 | Limites da capacidade, não profundidade atingida por cada passada. Animação mantém dimensões relativas. |
| Quatro trincheiras, quatro testes de carga, treze de impacto. | S3-01 e NASA Beyond Earth | Resumo da missão; montagem editorial depois de três atividades representativas. Sem contador que inventa que o jogador repetiu todos. |
| Solo “semelhante a areia úmida”, cerca de 98 kPa, certifica módulo lunar. | Pedido inicial; sem suporte primário suficiente para essa combinação | **Não publicar assim.** Semelhança mecânica não demonstra água. Pressão depende de ensaio/método; 10 psi ≈ 69 kPa, não 98. Dados locais contribuíram ao planejamento, não garantem todo terreno nem todo módulo. Usar resistência/cohesão qualitativa. |
| Eclipse em 24/04/1967. | S3-05, S3-06 | Verificado. A Terra ocultou o Sol; borda avermelhada pela atmosfera. Não eclipse anular com Terra menor. Câmera no limite de elevação, possibilitada pela inclinação. |
| Último contato em 04/05/1967. | S3-01; S3-04 situa término próximo a 00:00 GMT do dia 124 | Publicar dia sem horário. Não dizer “a bateria morreu exatamente aqui”; não inventar sequência interna de falhas. Tentativas posteriores não recuperaram operação. |
| A visita ocorreu 31 meses depois. | S3-07, S3-11 | Usar `31 meses`, não “mais de três anos”, presente incorretamente no resumo S3-01. Entre maio de 1967 e novembro de 1969 transcorreram aproximadamente 31 ciclos sinódicos, não 32 dias terrestres. Não contar ciclos solares exatos em tela. |
| Intrepid pousou em 19/11/1969 UTC; visita na EVA 2 em 20/11. | S3-07, S3-08 | Adotado. Resumo S3-01 mistura dias anteriores; não usá-lo para esse fato. Não fundir pouso e visita na mesma data. |
| Cerca de 180 m entre LM e Surveyor. | S3-08 descreve aproximadamente 600 pés | Valor aproximado e consistente com escala visual. Não interpretar foto em perspectiva como distância exata. |
| Poeira da Apollo atingiu a Surveyor, mas sua cor também tinha outros mecanismos. | S3-11 | Material e padrões ajudam a estudar deslocamento de partículas; alteração de tinta por UV também foi discutida. Evitar nuvem marciana, barro na lente ou causalidade única “ficou marrom por causa da Apollo”. |
| Câmera, pá e outras peças voltaram com Apollo 12. | S3-07–S3-10 | Verificado. O corpo não voltou. Final informa o que ficou e por que recuperar partes gera ciência. |
| A câmera foi testemunha gravando a visita de 1969. | Contradiz a missão encerrada em 1967 | **Não apresentar assim.** SUR-018 usa planos externos de reconstrução com astronautas completos; nenhum HUD operacional da sonda no reencontro. A fotografia histórica é identificada separadamente. |
| Câmera e pá estão ambas hoje expostas no Smithsonian. | Não verificado como conjunto | Não afirmar local/exposição atual da pá. Museu é opcional; se houver imagem, confirmar inventário da câmera de voo e licença específica. Artigo de teste S3-12 não é a câmera que foi à Lua. |

## Assets e uso permitido: pesquisar não equivale a baixar

1. **Fotografia de Apollo 12 junto à Surveyor:** S3-08, crédito NASA. Conferir arquivo, legenda, agência/digitalização e condições antes de inserir em ASSET_CREDITS. Preferir resolução 1024–1600 para Web e manter original fora da build.
2. **Imagem real da pá / trincheira:** figuras de S3-03 ou arquivo NASA identificado; registrar página, data e crédito. Não recortar fotografia lunar de uma imagem de museu sem verificar direitos.
3. **Eclipse:** referência técnica S3-05 e NASA S3-06. Para o jogo, construir geometria Terra/Sol em um pequeno plano ou textura própria com gradiente, baseada no fenômeno; inserir a foto real opcionalmente, com o crédito correto.
4. **Silhueta/modelo Surveyor:** não foi prometido um modelo oficial recebido. Procurar NASA 3D Resources e, em caso de ausência, reconstruir os componentes visíveis a partir de diagramas públicos e fotos. O fallback P0 é explícito, não um asset de origem desconhecida.
5. **Boot/glove/LM:** composição própria ou assets com licença comprovada; sem copiar modelo de jogo comercial. Fotografias de astronautas são referência de forma, não material para sugerir endosso de pessoas.
6. **Câmera no museu:** S3-13 tem copyright Smithsonian e exige conferir termos. **Não baixar por supor que toda página institucional é domínio público.** O final P0 já funciona com a foto NASA.

## Resolução editorial dos seis atos pedidos

Direção revista pelo proprietário em SUR-018: viagem e pouso são cinema, sem minigames de confirmação/correção; a ciência passa a operação direta assistida da pá, com deformação pelo contato. Noite e último contato precedem o salto de 31 meses. A recuperação é encenada externamente, com corpos completos, e segue para fotografia histórica identificada. Permanecem as restrições a números sem método e à sugestão de água por coesão. A observação é local, não garantia universal de segurança.

O arquivo de roteiro é conteúdo proposto. Fatos conferidos não certificam uma build; nenhum teste de gameplay, duração humana, assets importados ou permissão individual foi realizado nesta entrega de planejamento.
