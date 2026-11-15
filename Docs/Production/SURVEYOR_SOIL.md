# SUR-010 — Pressionar e soltar

Base develop `8810332`; CORE-006 e SUR-009 integradas. Cena executável: `Assets/_Game/Chapters/Surveyor/Soil/Preview/SurveyorSoilPreview.unity`. Menu **Surveyor → SUR-010 Reconstruir testes de solo**. A prévia começa em Sample e termina na transferência para Eclipse.

O módulo espera a escavação e o recolhimento da SUR-009. Segurar E por 4 s pressiona uma área ao lado do sulco; outro hold de 2 s prepara a soltura da pá. Soltar conserva o avanço. Cada operação exige um novo pressionamento; não há reação por reflexo nem medição de força inventada. O gráfico é qualitativo. A pá usa o mesmo IK da SUR-009 pelo método `PositionForSoilTest`, que só aceita poses depois do recolhimento e mantém o balde acima da superfície. Carga e impacto deixam marcas distintas; o sulco conserva seus quatro passes.

A comparação mostra duas texturas 640×480 capturadas da mesma câmera nesta partida, antes da escavação e depois dos dois testes. Elas pertencem ao módulo, não ocupam os três slots de fotos de observação, e são destruídas ao sair/reiniciar. Depois entra uma fotografia real identificada da **pata e pá**, feita pelos astronautas em 20/11/1969, sem fingir que é a foto do sulco recém-aberto. S16 e S17 têm ao menos 5 s; S18 e S19, 6 s. O registro posterior e a montagem de 6 s separam as ações representativas do jogador dos totais históricos 4/4/13.

## Integração

`SurveyorSoilTests.Configure` recebe os Core existentes, SurveyorScoop, câmera/saída TV, SurveyorSoilResults e as três raízes de marcas. Configurar antes da entrada em `Sample`; `SurveyorScoop.Completed` libera os testes. Progressão explícita `Sample → Eclipse`. `SurveyorSoilPanel` é apenas apresentação; `SurveyorSoilPreview` é um host isolado e não deve ser instalado sobre outro host/Core da campanha.

`SetOpticalOccluders` recebe o renderer do elo proximal que atravessa o raio óptico. Durante a visão técnica ele lança sua sombra, mas não tapa a operação; o elo distal e o balde continuam visíveis. O estado anterior é restaurado ao sair. Isso é uma convenção de câmera de instrumento, sem mover a sonda ou alterar seu modelo original. As malhas/materiais de marcas são próprios; as marcas de contato não simulam uma medição geotécnica.

Reutiliza pausa/foco/preferências/legendas comuns, dados PT/EN da SUR-014 e o som de pá da SUR-009. Não adiciona vento, voz, música, serviços remotos ou vídeos. O som de instrumento continua sujeito ao volume de efeitos.

## Ciência e arquivo

- S3-01/S3-02: [NASA Surveyor 3](https://science.nasa.gov/mission/surveyor-3/), [JPL](https://www.jpl.nasa.gov/missions/surveyor-3/), e matriz do capítulo. Propriedades do solo e engenharia para a Apollo; não água lunar ou certificação de todos os locais.
- [NASA Image Library — AS12-48-7110](https://images.nasa.gov/details/as12-48-7110): fotografia de pata, solo e pá em 20/11/1969. A impressão grande próxima à pata é da aterrissagem, **não** um teste da pá. Não a apontamos como marca de carga ou trincheira da partida. O resumo do catálogo usa 19 de abril para o pouso de 1967; a cronologia do jogo mantém 20 ABR UTC, conforme divergência já documentada na matriz.
- Totais 4 trincheiras / 4 testes de carga / 13 impactos pertencem à missão, não ao contador do jogador. Interface sem kPa, força, curva de sensor ou números históricos fictícios.

Crédito e hash em ASSET_CREDITS.md. Capturas e resultados reais de validação em `QA/SUR-010/`. A ligação de todos os atos e a aceitação Web de campanha continuam com a integração/SUR-015.
