# Instruções para pessoas e agentes

Leia `Docs/01_MASTER_CONTEXT.md`, o roteiro do capítulo e a tarefa inteira antes de editar. Este é um **novo projeto**; não suponha que scripts, testes ou prefabs da edição anterior existem aqui.

## Escopo e conclusão

- Implemente somente a tarefa ativa e os arquivos que ela possui. Dependência aberta significa trabalho bloqueado; prepare material fora dos arquivos alheios ou escolha uma tarefa Pronta.
- Não declare uma funcionalidade entregue porque escreveu código. Crie/configure cena, prefab e referências quando a tarefa pedir, execute a validação pertinente e registre evidência.
- Congele contratos antes da implementação paralela. Mudança de assinatura exige acordo com R1 e atualização do contrato e dos consumidores no mesmo PR.
- Não invente dados, mensagens transmitidas, descobertas, datas ou garantia de FPS. Consulte a matriz de fontes do capítulo.
- Não sobrescreva arquivos de outra pessoa, não faça force-push e não resolva conflito de cena descartando a versão de alguém.
- Preserve `.meta`. Não copie Library, Temp, Builds, tokens ou credenciais.

## C# e Unity

- Namespace `Abandoned.SpaceApps`; um componente público por arquivo; nomes PascalCase para tipos/métodos, camelCase para campos, constantes PascalCase. Campos privados serializados com `[SerializeField]`.
- Use APIs da versão prevista. Dependências novas precisam de utilização concreta; não atualizar Editor/pipeline durante o fim de semana.
- Referências via Inspector/builder e validação no Editor; evitar `Find` por frame, singletons distribuídos, reflexão, callbacks vazios, alocações por frame e dependência em internet durante a partida.
- Entrada pelo `InputRouter`; tempo ativo pelo `SessionClock`; avanço de missão pelo `ChapterDirector`; legendas por IDs localizados. Não crie outros leitores de E nem escreva Time.timeScale em scripts de capítulo.
- Use metros, segundos e graus nos dados com unidade no nome. Tempo narrativo não é relógio histórico.
- Use prefabs e cenas por dono. Somente R1 edita Boot/Dossier/BuildSettings e a integração; R3/R4 compartilham Marte por prefabs distintos, nunca o mesmo YAML simultaneamente.
- Testes devem provar comportamento: cancelamento/reentrada, entrada mantida entre etapas, pausa/foco, conclusão, descarte de memória e reconstrução de cena.

## Fluxo Git e IA

1. Escolha item **Pronta**, confira os bloqueadores nativos e links; atribua-se e mova para Em andamento. Uma tarefa ativa por pessoa.
2. Branch pelo ID da tarefa, como `SUR-001`, a partir de `develop` atualizada. Se ela já existir, preserve seu trabalho e integre a base sem force-push. Registre no PR o commit de base e a última CORE integrada; não parta de uma CORE antiga ignorando outras entregas já presentes em develop. Commit pequeno que mencione o ID.
3. Cole contexto mestre + prompt da tarefa na IA. Informe arquivos com dono e restrições; peça montagem real quando aplicável.
4. PR para develop com `Closes #numero`, comportamento, arquivos, teste e limitações. Revisor de função diferente. Não fechar tarefa sem evidência.
5. R1 integra, smoke Web verifica, depois o PR é aprovado. A liberação de dependentes acontece após merge e fechamento, não apenas ao mover um cartão.

As fontes anteriores podem orientar a solução. Registre origem, licença e o que foi reescrito/adaptado em `Docs/Production/REUSE.md`; não altere datas nem invente autoria ou execução de testes.
