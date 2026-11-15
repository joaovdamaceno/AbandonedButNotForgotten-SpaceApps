# SUR-012 — Último contato e 31 meses

Base: develop `55bb487`, CORE-006 e UI-006 integradas. Cena: `Assets/_Game/Chapters/Surveyor/Sequences/Night/Preview/SurveyorNightPreview.unity`; Play e Iniciar. Reconstruir pelo menu **Surveyor → SUR-012 Reconstruir noite e passagem**.

## Entrega e integração

`SurveyorNight.asset` + `SurveyorTimePassage.asset`, executadas por `SurveyorNight.Configure` com os Core, câmera, luz e ponto óptico existentes. Progressão explícita `LunarNight → TimePassage → ImaginedVisit`. O integrador reutiliza esses componentes, sem instalar os Core adicionais do host de prévia. A visita da SUR-013 não foi implementada aqui.

A mesma câmera e enquadramento permanecem durante todo o trecho. A luz e o ambiente evoluem pelo tempo do SequenceDirector, alimentado pelo SessionClock; não há outro relógio nem troca de posição da sonda. Pausa/foco/preferências reutilizam UI-006. O menu de pausa global continua acessível; o HUD de missão desaparece até o segundo 12.

| Tempo do trecho | Conteúdo |
| --- | --- |
| 0–5 s | S22: continuidade das observações; início da sombra. |
| 5–11 s | S23: noite e último contato; cartão 4 MAI 1967 e total histórico 6.326 imagens. |
| 11–17 s | S24: dados já chegaram àTerra; instrumentos encerrados e HUD oculto. |
| 17–30 s | Calendário 1967→1968→NOV1969 e quatro mudanças editoriais de luz; S25 em 18–23 s. |

A divisão 17 + 13 s acomoda S22–S25 com leitura integral dentro dos 30 s; muda em 1 s a sugestão 16 + 14 do roteiro. O consumidor de legendas usa estas sequências, evitando repetir S22 na transição anterior. A passagem de anos dura 13 s; entendimento por novatos ainda requer avaliação humana.

Em TimePassage, miniaturas PlayerCapture são liberadas pelo EvidenceStore, com seus IDs, resultados, fontes e autoria preservados. Arquivos históricos permanecem. SUR-013 usa a fotografia histórica; não depende dessas três texturas do jogador. Fontes de áudio de instrumento podem ser fornecidas explicitamente a Configure e são interrompidas; a música global continua sob o AudioDirector. A prévia não adiciona trilha ou efeitos novos.

## Fatos e simplificações

- S3-01: [NASA Surveyor 3](https://science.nasa.gov/mission/surveyor-3/): último contato em 4 de maio de 1967 e 6.326 imagens. Não há última mensagem inventada nem uma falha interna específica atribuída à sonda.
- S3-07: [NASA Apollo 12](https://www.nasa.gov/history/55-years-ago-apollo-12-makes-a-pinpoint-landing-on-the-moon/): exposição por 31 meses antes da recuperação de peças em novembro de 1969.
- S3-04: o PDF NTRS 19680009189 indicado pela tarefa respondeu 403 nesta consulta; os fatos exibidos foram conferidos nas páginas NASA acima.

Temperatura é uma tendência qualitativa. As mudanças de luz condensam muitas manhãs/noites; não simulam ciclos exatos. A passagem é identificada como reconstituição, preparando o cartão explicativo da visita imaginada na SUR-013. O cenário aponta à área SUR.Trench; a escavação jogável continua com o responsável pela SUR-009.

Validação: `QA/SUR-012/README.md`. Integração completa e teste amplo Web permanecem em INT-001/SUR-015. Não foram alterados Boot, Dossier, BuildSettings, modelos ou shaders.

A prévia já inicia com a pá em repouso, fora da linha óptica; esse ajuste está apenas na instância local e não altera o prefab/modelo da SUR-002 nem implementa a escavação da SUR-009.
