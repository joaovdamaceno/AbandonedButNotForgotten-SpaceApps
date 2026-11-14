# Produção com sete pessoas — 14 e 15 de novembro de 2026

Este é um plano de execução, não relato de trabalho concluído. T0 é o início real da equipe, evitando agendar retroativamente uma manhã já passada. O último candidato deve estar congelado no domingo, com pelo menos duas horas disponíveis para pacote, upload e verificação hospedada.

## Pessoas, responsabilidade e capacidade

Os usuários do GitHub serão atribuídos depois. O campo **Função** e a label `role:Rn` são responsabilidade prevista, não assignee real. Cada pessoa escolhe uma função e assume apenas um item em andamento. Pode ajudar outra função em uma tarefa Pronta, desde que o ownership seja transferido antes de editar.

| Função | Responsabilidade principal | P0 estimado | Primeira ação útil |
| --- | --- | ---: | --- |
| R1 | Núcleo, contratos, montagem e distribuição | 9,25 h | CORE-001; depois CORE-002 e contrato publicado |
| R2 | Dossiê, HUD, legendas, tutorial e testes de orientação | 10,75 h | UI-001, composição estática independente de código |
| R3 | Condução, instrumentos e fluxo de Marte | 12,5 h | Revisar roteiro/contratos com R1; depois primeira tarefa MAR liberada |
| R4 | Modelos, terreno, pouso e cinema de Marte | 13 h | ART-001; preparar referências e módulos de arte sem editar cena de R3 |
| R5 | Pioneer, instrumentos e encenação | 10,75 h | PIO-007 e preparação independente; aguardar só contratos realmente usados |
| R6 | Surveyor, telemetria, pá e epílogo | 12,75 h | SUR-001, revisão das fontes e referências da câmera |
| R7 | Auditoria científica, áudio, Web e QA | 10,25 h | AUD-001/PIO-001; QA-001 assim que CORE-001 integrar |

P0 total **79,25 horas-pessoa**, distribuídas em 76 tarefas. Existem 84 tarefas, incluindo 8 opcionais. Uma hipótese de 14 h produtivas por pessoa oferece 98 horas-pessoa, aproximadamente 18,75 h de margem. Isso **não** garante prazo: integrações, problemas de hardware e dependências alongam o caminho. Se a disponibilidade real somada for menor, cortar complexidade de arte e encaixar fallbacks antes de iniciar P1. Não atribuir 14 h sem conferir a disponibilidade real de cada pessoa.

## Ondas e portões

**T0–T + 2 h — prova técnica e contrato.** R1 inicializa e publica tipos; R2 desenha UI; R4 audita assets; R5 prepara placa e referências; R6 trava ciência/POV; R7 cuida dos sons/fontes e primeira build Web. R3 participa da revisão do contrato e prepara dados/corredor em branch própria, sem fingir que dependência já está integrada. Ao fim: Editor fixado, assinaturas compiláveis e Web mínimo começando a carregar.

**T + 2 h–T + 6 h — blocos jogáveis.** Input/hold, fluxo, sequências e UI entram em commits pequenos. Cada capítulo implementa primeiro uma interação e a sequência anterior/próxima com o modelo provisório reconhecível. Não esperar hero final para descobrir que E ou pause não funciona. R7 confere mundo opaco, áudio e foco no Web cedo.

**T + 6 h–T + 10 h — ponta a ponta por capítulo.** Cada dono monta início→atividade→descoberta→fim em sua cena/harness, com dados finais e arte simplificada. R3/R4 integram Marte por prefabs distintos. Modelo pendente da equipe usa o fallback documentado; trocar referência deve ser barato. No fim do sábado ou primeiro portão equivalente: três capítulos devem possuir fluxo terminal, mesmo sem ornamentação. Se algum não tiver, todos os P1 ficam suspensos e ajuda vai ao caminho crítico.

**Domingo, primeira metade / T + 10 h–T + 14 h — integração e corte.** INT-001 liga dossiê e camadas globais; QA começa enquanto defeitos são corrigidos por seus donos. Revisar legendas e fotos na build real. Executar primeiras partidas em paralelo com máquinas de QA, sem orientar o novato. Nenhum ajuste de cronômetro substitui compreensão.

**Último bloco — congelar, empacotar, testar endereço.** REL-001/002 somente após portões. Não trocar pipeline, adicionar pacotes ou importar asset enorme no último momento. Confirmar hashes após runtime congelado, hospedar candidato e conferir iframe/headers reais. Manter versão anterior do candidato recuperável; não sobrescrever o único ZIP aprovado.

O grafo, e não a ordem numérica dos IDs, define o início. `Data/backlog.json` contém dependências verificáveis e `Docs/Tasks/ID.md` é o prompt completo. Estimativas devem ser revisadas após a primeira hora de execução, com o tempo disponível real até domingo.

## Acordos de integração

- PR para `develop`, revisão cruzada curta. Um PR deve funcionar sem incorporar metade da branch de outra pessoa.
- Só o integrador edita Boot/Dossier/BuildSettings. Capítulos entregam builders/prefabs; não mesclar o mesmo YAML em paralelo.
- Contrato novo passa por R1 e consumidores; não consertar API duplicando InputRouter, pausa ou UI.
- Dependência fecha após merge e evidência; mover cartão sozinho não libera trabalho. PR em `develop` não fecha issue automaticamente por padrão do GitHub: o sincronizador verifica o merge e fecha explicitamente os IDs válidos.
- Commit de documento/planejamento de hoje é real; adaptação de solução anterior registra origem. Não inventar datas, autores ou testes.

## Corte de escopo em ordem

1. Todos os P1/P2 saem da execução até os três capítulos completos.
2. Reduzir laboratório para duas silhuetas/modelos sob recorte, foguete para plano breve baseado em referência e cenários distantes para imagem/diagrama legítimo.
3. Reduzir malhas, texturas, partículas, cenas de fundo e planos redundantes; manter o hardware reconhecível.
4. Preservar todas as descobertas centrais e eventos do roteiro em montagem curta; ciência secundária pode ficar no arquivo opcional, com decisão editorial explícita.
5. Se ainda não couber, registrar a redução negociada do escopo antes de prometer os três capítulos completos. Não publicar uma pasta “disponível” que abre cena vazia.

## Riscos reais

O capítulo conjunto de Marte é o maior esforço. Modelos Pioneer/Surveyor ainda podem não chegar prontos. O fluxo de 1,3 s da Surveyor precisa de um teste dedicado de pausa/rearme. Split-screen pode custar caro. Faixas de música e modelos NASA podem ter créditos específicos. Acesso de Projects/itch.io pode requerer ação do titular. A disponibilidade de cinco novatos por capítulo e GPU integrada não foi confirmada. Essas são pendências de produção, não resultados.
