# SUR-013 — A imagem que ela não fez

Base develop `77ff4f6`, CORE-006 e UI-006 integradas. Cena própria: `Assets/_Game/Chapters/Surveyor/Sequences/Visit/Preview/SurveyorVisitPreview.unity`. Play → Iniciar. Reconstrução: menu **Abandoned Space Apps → Surveyor → SUR-013 Reconstruir visita e final**.

## Entrega e integração

`SurveyorVisit.asset`, `ApolloVisitRig.prefab` e `SurveyorEnding.asset` entregam o ato de 50 segundos. `SurveyorVisit.Configure` recebe os Core existentes, as duas orientações ópticas, os atores e os dados. O integrador fornece a câmera já existente e os pontos no seu cenário; não deve instalar os Core adicionais do host de prévia. A sequência ancora as duas orientações na posição atual da câmera. A rotação muda suavemente para o visitante, sem deslocar o ponto óptico.

Progressão explícita: `ImaginedVisit → HistoricalReceipt → End → Completion`. Aos 32 s registra `surveyor.apollo12.archive` como **Archive**, com crédito NASA e sem fingir ser uma captura do jogador. A imagem está no ending, não no buffer de capturas pessoais. A conclusão só ocorre aos 50 s. Voltar usa uma nova pressão de E após soltar a tecla, ou o botão; não reinicia nem abre outro capítulo automaticamente. Saída, cancelamento e reinício liberam controle da câmera e entrada.

| Tempo local | Encenação e leitura |
| --- | --- |
| 0–5 | Preto; S26 integral esclarece a reconstrução antes de qualquer imagem. |
| 5–19 | Data **19 NOV 1969**. Intrepid a 180 m, descendo até 10 s; pequenos grãos balísticos junto ao chão. S27 6–12. |
| 19–32 | **20 NOV 1969** antes da aproximação. Pernas, botas, sombra e luva; S28 20–26. Preto aos 31 s, antes de representar a retirada física. |
| 32–44 | Fotografia real NASA, AS12-48-7134, com data e crédito. S29 32–38; S30 38–43. |
| 44–50 | Preto; SURVEYOR 3 / 1967 · Lua. S31 integral. |

Pausa, foco, legendas, localização PT/EN, movimento reduzido e entrada reutilizam os componentes comuns. A prévia não acrescenta música, vento, passos aéreos, vozes ou vídeos. A trilha permanece com o AudioDirector global. A TV de 1969 não apresenta REC, telemetria de operação ou shader que finja uma transmissão registrada. Render local 640×480; foto com importação limitada a 1024 px, sem mipmaps nem cópia legível na CPU.

## Fontes, autoria e simplificações

- [NASA History — Apollo 12 pinpoint landing](https://www.nasa.gov/history/55-years-ago-apollo-12-makes-a-pinpoint-landing-on-the-moon/): pouso em 19 de novembro e visita em 20 de novembro de 1969; exposição de peças por 31 meses.
- [NASA — Apollo 12 and Surveyor 3](https://science.nasa.gov/resource/apollo-12-and-surveyor-3/): foto de **20 NOV 1969**, crédito NASA; Conrad junto à câmera, Intrepid ao fundo, pouso a aproximadamente 600 pés. Fonte **S3-08** da matriz do capítulo. Original, hash e condições de uso em ASSET_CREDITS.md.
- [NASA SP-284 — análise do material recuperado](https://www.nasa.gov/wp-content/uploads/static/history/alsj/a12/AnalysSurvIIIMtrial.pdf): câmera, pá e outras peças recuperadas para estudo; o restante da sonda ficou na Lua. A recuperação também consta na fonte S3-07, acima.

A câmera já não funcionava. A visita vista pelo seu antigo centro óptico é uma imagem imaginada, informada por S26; a fotografia final é o documento real. Não se afirma que o registro NASA tenha sido feito pela Surveyor. A descida em 5 s, os passos condensados e a luva são elipses artísticas. A silhueta do LM, os fragmentos de traje e a poeira foram modelados em código desta tarefa, com proporções aproximadas; não são modelos oficiais nem uma reconstituição de engenharia. Não há simulação de retirada da câmera. Só uma luz direcional lança sombras; a sombra do corpo não exige um astronauta inteiro em quadro.

## Limites

Esta entrega é um módulo com cena verificável, sem alteração de Boot, Dossier, BuildSettings ou arquivos de outro responsável. Ligação ao capítulo completo, música global, revisão artística de conjunto e testes Web de campanha ficam com a integração/SUR-015. A duração desta sequência não certifica os 5–7 minutos do capítulo. Evidências e procedimentos em `QA/SUR-013/README.md`.
