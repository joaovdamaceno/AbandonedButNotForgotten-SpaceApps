# CORE-003 — Criar InputRouter e interação única com E mantido e rearme

**Função:** R1 — Integração, núcleo e distribuição · **Prioridade:** P0 · **Estimativa:** 1.75 h-pessoa (revisável).

## Prompt para execução

Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e os contratos aprovados antes de editar arquivos. Este pacote de R1 é a única origem dos comandos compartilhados. A UI e o instrumento precisam concordar sobre disponibilidade e progresso; um E não pode atravessar duas telas ou atividades.

Leia primeiro o [contexto mestre](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/01_MASTER_CONTEXT.md) e o [AGENTS.md](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/AGENTS.md). O novo jogo ainda precisa ser implementado; não presumir sistemas da edição anterior.

## Dependências e disponibilidade

[#2 — CORE-002](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/issues/2)
Dependência só libera após integração e fechamento com evidência. Cartão movido manualmente não substitui PR integrado. Um responsável, uma tarefa em andamento por vez.

## Entrega concreta

Entrada compartilhada para WASD/setas, mouse opcional e E, com início, pausa, retomada e conclusão sem vazamento de comandos.

## Arquivos sob sua responsabilidade

- `Assets/_Game/Core/InputRouter.cs`
- `Assets/_Game/Core/InteractionService.cs`
- `Assets/_Game/Core/HoldAction.cs`

Não editar fora desse escopo sem alinhar com o dono. Se uma pasta contiver arquivo de outro responsável, aplicar o contrato e entregar por prefab/componente próprio.

## Passos de implementação

1. Separar os eventos de pressionar, manter e soltar. O candidato apresentado no HUD e o comando executado devem consultar a mesma condição de disponibilidade.
2. Exigir a soltura da tecla entre abrir um painel e iniciar a etapa seguinte. Ao pausar ou perder foco, limpar o estado de entrada para impedir movimento ou ferramenta ativa na retomada.
3. Emitir eventos idempotentes de progresso, cancelamento e conclusão para a UI. O instrumento decide, por seus dados, qual progresso físico deve ser conservado quando E é solto.
4. Desabilitar a entrada de gameplay durante cinematics e restaurá-la apenas na saída válida do estado. Não criar leitores paralelos de E nos capítulos.

## Critérios de aceite

- [ ] Um pressionamento de E nunca abre duas atividades.
- [ ] Soltar E pausa a operação; continuar segurando não atravessa etapas.
- [ ] Pausa, troca de foco e saída de cinematic deixam a entrada em estado seguro.

## Como verificar

- Manter E ao entrar e fechar um painel; pressionar durante uma transição.
- Pausar, desfocar e reiniciar enquanto a ferramenta opera; confirmar que ela não permanece ligada.

## Fontes e contexto específico

- [Arquitetura](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/03_ARCHITECTURE.md)
- [Web e QA](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/04_WEB_AND_QA.md)

## Limite e alternativa de escopo

Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir ornamentação antes de alterar contratos.

## Ao concluir

Abra PR para `develop`, cite esta issue e anexe evidências. Informe comportamento, arquivos, testes realmente executados e limitações. Peça revisão de outra função. A automação considera concluída a dependência somente após integração; não feche tarefa apenas por gerar código.
