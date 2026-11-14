# CORE-004 — Implementar o diretor de sequências com um único relógio

**Função:** R1 — Integração, núcleo e distribuição · **Prioridade:** P0 · **Estimativa:** 1.5 h-pessoa (revisável).

## Prompt para execução

Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e os contratos aprovados antes de editar arquivos. R1 entrega a infraestrutura de apresentação consumida pelos roteiros dos três capítulos. Câmera, atores, texto e áudio precisam permanecer sincronizados mesmo após pausa, cancelamento e reinício.

Leia primeiro o [contexto mestre](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/01_MASTER_CONTEXT.md) e o [AGENTS.md](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/AGENTS.md). O novo jogo ainda precisa ser implementado; não presumir sistemas da edição anterior.

## Dependências e disponibilidade

[#2 — CORE-002](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/issues/2)
Dependência só libera após integração e fechamento com evidência. Cartão movido manualmente não substitui PR integrado. Um responsável, uma tarefa em andamento por vez.

## Entrega concreta

Sequências configuradas por dados, com tomadas reais, eventos nomeados, controle de câmera e saídas seguras.

## Arquivos sob sua responsabilidade

- `Assets/_Game/Presentation/SequenceDirector.cs`
- `Assets/_Game/Presentation/CameraDirector.cs`
- `Assets/_Game/Presentation/SequenceClock.cs`

Não editar fora desse escopo sem alinhar com o dono. Se uma pasta contiver arquivo de outro responsável, aplicar o contrato e entregar por prefab/componente próprio.

## Passos de implementação

1. Implementar keyframes com campo de visão, curvas de suavização, alvo de enquadramento e eventos nomeados, todos subordinados a um único relógio de sequência.
2. Adicionar proteção para a câmera permanecer acima do terreno e garantir um único AudioListener ativo. Uma transição deve preservar pose e campo de visão quando essa continuidade estiver prevista.
3. Expor eventos para legendas e áudio; permitir testar estados anteriores e seguintes. As tomadas devem mover câmera e atores de fato, sem métodos ou referências vazias.
4. Tratar término, salto permitido e cancelamento por um único caminho de saída. A devolução do controle acontece uma vez, e não em callbacks concorrentes.

## Critérios de aceite

- [ ] Duas tomadas conectadas reproduzem sem salto involuntário de posição ou relógios divergentes.
- [ ] Pausa congela câmera, atores e texto juntos.
- [ ] Término, salto permitido e cancelamento liberam o controle uma única vez.

## Como verificar

- Reproduzir, pausar e reiniciar no meio de uma tomada.
- Comparar eventos a 30 e 60 FPS e conferir os limites inicial e final de cada clip.

## Fontes e contexto específico

- [Arquitetura](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/03_ARCHITECTURE.md)
- [Web e QA](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/04_WEB_AND_QA.md)

## Limite e alternativa de escopo

Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir ornamentação antes de alterar contratos.

## Ao concluir

Abra PR para `develop`, cite esta issue e anexe evidências. Informe comportamento, arquivos, testes realmente executados e limitações. Peça revisão de outra função. A automação considera concluída a dependência somente após integração; não feche tarefa apenas por gerar código.
