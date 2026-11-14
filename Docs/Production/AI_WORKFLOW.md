# Como usar IA sem sete pessoas reescreverem o mesmo jogo

## Primeiro chat de cada pessoa

1. Clonar o repositório novo com Git LFS e abrir sua branch de tarefa.
2. Colar `Docs/01_MASTER_CONTEXT.md` uma vez; apontar para AGENTS.md e para a arquitetura.
3. Escolher tarefa **Pronta**, conferir bloqueadores e colar o corpo integral de `Docs/Tasks/ID.md` ou da issue. Informar papel, branch, arquivos que outras pessoas estão editando e resultado esperado.
4. Pedir que a IA leia os arquivos reais antes de assumir APIs. Quando a tarefa for de cena, pedir montagem via Editor/builder, prefabs e referências reais; quando for de arte, comparar com a imagem-fonte citada.
5. Conferir a entrega e abrir PR com evidência. Resposta convincente da IA não é prova de que compilou ou executou.

## Agentes e divisão recomendada

| Agente | Pessoa responsável | Escopo e limite |
| --- | --- | --- |
| Integração/núcleo | R1 | Contratos, fluxo, câmera compartilhada e build; único integrador dos arquivos globais |
| Interface/orientação | R2 | Dossiê, E, legendas, tutorial e pausa; entrega prefab/componentes sem mudar diretores de capítulo |
| Gameplay Marte | R3 | Rover, instrumentos e estado de missão; não altera Environment/Timeline de R4 em paralelo |
| Arte/cinema Marte | R4 | Assets, terreno, rigs e takes; não escreve outro InputRouter nem altera física de R3 |
| Capítulo Pioneer | R5 | Estados, placa, IPP, rádio e composição espacial, consumindo contratos |
| Capítulo Surveyor | R6 | POV, fila de comando, TV, pá e visita imaginada; nenhum sistema paralelo de pausa |
| Revisão/ciência/áudio/QA | R7 | Checagem de evidências, importação de áudio e testes; não declara teste humano realizado por automação |

Uma pessoa dirige seu agente e revisa seus arquivos; agente não é um oitavo dono sem responsabilidade. Pode haver uma IA revisora temporária em um PR, com acesso de leitura, para questionar fatos, estados e tratamento de erro. Não delegar o mesmo YAML a dois agentes. Usar o modelo já disponível; o plano não depende de compra de serviço ou de modelo específico.

Na elaboração deste planejamento, três frentes especializadas revisaram **gêmeas, Pioneer e Surveyor**; a integração reuniu contratos, UX, Web e Kanban. O trabalho de implementação continua aberto nas issues. Reutilizar esse planejamento não dispensa leitura do código real criado pela equipe.

## Prompt curto para revisão de um PR

> Leia AGENTS.md, contexto mestre e issue ID. Revise o diff para os critérios desta tarefa, incluindo as dependências realmente integradas. Aponte defeitos reproduzíveis, dados científicos sem fonte e falhas de input/pausa/câmera/reinício. Não reescreva arquivos nem mude contratos. Diferencie bug, pendência e preferência estética. Diga quais verificações realmente executou e quais exigem Editor/navegador/pessoa.

## Quando uma IA deve parar de editar e alinhar

Se precisa mudar assinatura consumida por outra pessoa, editar cena de outro dono, baixar recurso sem licença específica, remover trabalho existente ou alegar dado histórico sem fonte. Continuar trabalho independente permitido, mas registrar a dependência. Não pedir autorização para cada detalhe de iluminação; usar o roteiro e seus critérios. Não inventar que o modelo enviado depois pela equipe já existe.

Ao compactar ou trocar de chat, salvar um handoff com branch/commit, tarefa atual, dependências, arquivos, testes, pendências e próximo passo. Não carregar todo o histórico do projeto anterior como requisito para entender o projeto.
