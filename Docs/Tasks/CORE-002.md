# CORE-002 — Implementar e congelar os tipos e contratos globais

**Função:** R1 — Integração, núcleo e distribuição · **Prioridade:** P0 · **Estimativa:** 0.75 h-pessoa (revisável).

## Prompt para execução

Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e os contratos aprovados antes de editar arquivos. R1 entrega as assinaturas que os três capítulos e a interface consumirão. O objetivo é permitir trabalho paralelo com tipos concretos e estáveis, sem construir um framework genérico.

Leia primeiro o [contexto mestre](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/01_MASTER_CONTEXT.md) e o [AGENTS.md](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/AGENTS.md). O novo jogo ainda precisa ser implementado; não presumir sistemas da edição anterior.

## Dependências e disponibilidade

[#1 — CORE-001](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/issues/1)
Dependência só libera após integração e fechamento com evidência. Cartão movido manualmente não substitui PR integrado. Um responsável, uma tarefa em andamento por vez.

## Entrega concreta

Tipos mínimos compiláveis para ChapterDefinition, InteractionCandidate, EvidenceRecord e ShotDefinition, com eventos e exemplo de consumo documentados.

## Arquivos sob sua responsabilidade

- `Assets/_Game/Core/Contracts/`
- `Docs/03_ARCHITECTURE.md`

Não editar fora desse escopo sem alinhar com o dono. Se uma pasta contiver arquivo de outro responsável, aplicar o contrato e entregar por prefab/componente próprio.

## Passos de implementação

1. Converter os contratos propostos em tipos reais com identificadores estáveis, unidades explícitas e campos obrigatórios reconhecíveis no Inspector.
2. Publicar um exemplo mínimo de capítulo e de instrumento em uma cena de teste. O exemplo deve mostrar como solicitar interação, apresentar uma evidência e concluir uma etapa.
3. Documentar as assinaturas reais no mesmo PR. Os diretores dos capítulos devem depender de IDs e transições explícitas, sem somar valores numéricos de enum para avançar.
4. Avisar os consumidores sobre alterações de contrato no PR e atualizar o exemplo junto com a API; evitar que cada capítulo crie um tipo equivalente incompatível.

## Critérios de aceite

- [ ] Os três diretores podem referenciar os mesmos tipos sem implementar uma interface própria.
- [ ] Dados inválidos geram validação clara antes da execução.
- [ ] Exemplo compilável e documentação descrevem a mesma API.

## Como verificar

- Compilar no Editor e no alvo Web.
- Validar dados sem ID e referências obrigatórias ausentes; não aceitar NullReference silenciosa como resultado.

## Fontes e contexto específico

- [Arquitetura](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/03_ARCHITECTURE.md)
- [Web e QA](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/04_WEB_AND_QA.md)

## Limite e alternativa de escopo

Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir ornamentação antes de alterar contratos.

## Ao concluir

Abra PR para `develop`, cite esta issue e anexe evidências. Informe comportamento, arquivos, testes realmente executados e limitações. Peça revisão de outra função. A automação considera concluída a dependência somente após integração; não feche tarefa apenas por gerar código.
