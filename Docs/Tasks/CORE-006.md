# CORE-006 — Automatizar montagem de cenas e builds sem sobrescrever colegas

**Função:** R1 — Integração, núcleo e distribuição · **Prioridade:** P0 · **Estimativa:** 0.75 h-pessoa (revisável).

## Prompt para execução

Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e os contratos aprovados antes de editar arquivos. R1 fornece a automação compartilhada. Cada capítulo terá seu próprio builder e ownership, e a troca de plataforma precisa terminar antes de compilar para evitar remoção incorreta de shaders.

Leia primeiro o [contexto mestre](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/01_MASTER_CONTEXT.md) e o [AGENTS.md](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/AGENTS.md). O novo jogo ainda precisa ser implementado; não presumir sistemas da edição anterior.

## Dependências e disponibilidade

[#1 — CORE-001](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/issues/1), [#2 — CORE-002](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/issues/2)
Dependência só libera após integração e fechamento com evidência. Cartão movido manualmente não substitui PR integrado. Um responsável, uma tarefa em andamento por vez.

## Entrega concreta

Menus de reconstrução por capítulo e build reproduzível, com alvo ativo correto e relatório dos arquivos gerados.

## Arquivos sob sua responsabilidade

- `Assets/_Game/Editor/SpaceAppsSceneBuilder.cs`
- `Assets/_Game/Editor/SpaceAppsBuild.cs`
- `Tools/build_release.py`

Não editar fora desse escopo sem alinhar com o dono. Se uma pasta contiver arquivo de outro responsável, aplicar o contrato e entregar por prefab/componente próprio.

## Passos de implementação

1. Dar a cada builder uma raiz e um responsável. Criar objetos, prefabs, materiais e referências reais sem limpar indiscriminadamente a cena ou reescrever arquivos de outro capítulo.
2. Trocar o alvo de compilação, aguardar importação e recarga do domínio e só então executar BuildPlayer. Não presumir que BuildPlayerOptions.target substitui a troca de plataforma.
3. Validar chaves, materiais e referências antes de gerar o player. Registrar resultado, arquivos e hashes para identificar a build testada.
4. Manter GUIDs estáveis em reconstruções repetidas e garantir que a cena resultante possa ser aberta e jogada sem montagem manual.

## Critérios de aceite

- [ ] Reconstruir não altera os arquivos do colega nem perde GUIDs.
- [ ] A build Web usa seu perfil e preserva os shaders de cor do mundo.
- [ ] A cena reconstruída possui componentes e referências válidos.

## Como verificar

- Executar o mesmo builder duas vezes e comparar referências.
- Gerar Web após Windows e Windows após Web, sempre em operações sequenciais.

## Fontes e contexto específico

- [Arquitetura](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/03_ARCHITECTURE.md)
- [Web e QA](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/04_WEB_AND_QA.md)

## Limite e alternativa de escopo

Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir ornamentação antes de alterar contratos.

## Ao concluir

Abra PR para `develop`, cite esta issue e anexe evidências. Informe comportamento, arquivos, testes realmente executados e limitações. Peça revisão de outra função. A automação considera concluída a dependência somente após integração; não feche tarefa apenas por gerar código.
