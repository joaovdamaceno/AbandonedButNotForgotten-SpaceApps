"""Gera os 19 pacotes de trabalho compartilhados do novo projeto.

As tarefas dos capítulos são produzidas separadamente. Esta lista preserva IDs,
dependências, papéis, estimativas e ownership acordados no planejamento.
"""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]

TASKS = [{'id': 'CORE-001',
  'title': 'Inicializar o projeto Unity e fixar Editor, URP, pacotes e pastas',
  'role': 'R1',
  'priority': 'P0',
  'hours': 0.75,
  'depends_on': [],
  'files': ['Assets/_Game/Core/',
            'Packages/manifest.json',
            'Packages/packages-lock.json',
            'ProjectSettings/'],
  'owned_paths': ['Assets/_Game/Core/',
                  'Packages/manifest.json',
                  'Packages/packages-lock.json',
                  'ProjectSettings/'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. Este é o primeiro pacote técnico de R1. Os '
             'demais capítulos precisam de uma base reproduzível para começar. Preserve os documentos de '
             'planejamento e o histórico do novo repositório.',
  'deliverable': 'Projeto novo que abre na versão acordada, com estrutura organizada, assets serializados em '
                 'texto e dependências registradas.',
  'instructions': ['Inicializar Unity 6000.6.7f1 com URP 17.6.0 na nova raiz, preservando Docs, Data, Tools '
                   'e .git. Não copiar Assets ou ProjectSettings do projeto anterior por inteiro.',
                   'Ativar Visible Meta Files e Force Text. Registrar as versões efetivamente usadas no '
                   'manifesto e no lockfile; adicionar Input System, Timeline e glTFast apenas quando a '
                   'arquitetura realmente consumir esses pacotes.',
                   'Criar a árvore compartilhada e as pastas isoladas dos três capítulos conforme o '
                   'documento de arquitetura. Configurar .gitignore e Git LFS sem incluir Library, Temp, '
                   'Builds ou credenciais.',
                   'Restaurar os pacotes, abrir uma cena vazia e registrar a configuração no README. Não '
                   'atualizar versões nem comprar extensões para contornar uma incompatibilidade sem '
                   'diagnóstico.'],
  'acceptance': ['O Editor abre sem erro de compilação.',
                 'Todos os arquivos .meta, manifesto e lockfile necessários estão versionados.',
                 'A configuração documentada corresponde à cópia integrada em develop.'],
  'tests': ['Fechar e reabrir o Editor, aguardando a importação terminar.',
            'Quando viável, restaurar em um clone limpo; distinguir download de pacotes da execução do jogo '
            'sem serviços externos.'],
  'sources': ['https://docs.unity.com/en-us/engine/6000.6/manual/urp/urp-introduction'],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'CORE-002',
  'title': 'Implementar e congelar os tipos e contratos globais',
  'role': 'R1',
  'priority': 'P0',
  'hours': 0.75,
  'depends_on': ['CORE-001'],
  'files': ['Assets/_Game/Core/Contracts/', 'Docs/03_ARCHITECTURE.md'],
  'owned_paths': ['Assets/_Game/Core/Contracts/', 'Docs/03_ARCHITECTURE.md'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R1 entrega as assinaturas que os três '
             'capítulos e a interface consumirão. O objetivo é permitir trabalho paralelo com tipos '
             'concretos e estáveis, sem construir um framework genérico.',
  'deliverable': 'Tipos mínimos compiláveis para ChapterDefinition, InteractionCandidate, EvidenceRecord e '
                 'ShotDefinition, com eventos e exemplo de consumo documentados.',
  'instructions': ['Converter os contratos propostos em tipos reais com identificadores estáveis, unidades '
                   'explícitas e campos obrigatórios reconhecíveis no Inspector.',
                   'Publicar um exemplo mínimo de capítulo e de instrumento em uma cena de teste. O exemplo '
                   'deve mostrar como solicitar interação, apresentar uma evidência e concluir uma etapa.',
                   'Documentar as assinaturas reais no mesmo PR. Os diretores dos capítulos devem depender '
                   'de IDs e transições explícitas, sem somar valores numéricos de enum para avançar.',
                   'Avisar os consumidores sobre alterações de contrato no PR e atualizar o exemplo junto '
                   'com a API; evitar que cada capítulo crie um tipo equivalente incompatível.'],
  'acceptance': ['Os três diretores podem referenciar os mesmos tipos sem implementar uma interface própria.',
                 'Dados inválidos geram validação clara antes da execução.',
                 'Exemplo compilável e documentação descrevem a mesma API.'],
  'tests': ['Compilar no Editor e no alvo Web.',
            'Validar dados sem ID e referências obrigatórias ausentes; não aceitar NullReference silenciosa '
            'como resultado.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'CORE-003',
  'title': 'Criar InputRouter e interação única com E mantido e rearme',
  'role': 'R1',
  'priority': 'P0',
  'hours': 1.75,
  'depends_on': ['CORE-002'],
  'files': ['Assets/_Game/Core/InputRouter.cs',
            'Assets/_Game/Core/InteractionService.cs',
            'Assets/_Game/Core/HoldAction.cs'],
  'owned_paths': ['Assets/_Game/Core/InputRouter.cs',
                  'Assets/_Game/Core/InteractionService.cs',
                  'Assets/_Game/Core/HoldAction.cs'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. Este pacote de R1 é a única origem dos '
             'comandos compartilhados. A UI e o instrumento precisam concordar sobre disponibilidade e '
             'progresso; um E não pode atravessar duas telas ou atividades.',
  'deliverable': 'Entrada compartilhada para WASD/setas, mouse opcional e E, com início, pausa, retomada e '
                 'conclusão sem vazamento de comandos.',
  'instructions': ['Separar os eventos de pressionar, manter e soltar. O candidato apresentado no HUD e o '
                   'comando executado devem consultar a mesma condição de disponibilidade.',
                   'Exigir a soltura da tecla entre abrir um painel e iniciar a etapa seguinte. Ao pausar ou '
                   'perder foco, limpar o estado de entrada para impedir movimento ou ferramenta ativa na '
                   'retomada.',
                   'Emitir eventos idempotentes de progresso, cancelamento e conclusão para a UI. O '
                   'instrumento decide, por seus dados, qual progresso físico deve ser conservado quando E é '
                   'solto.',
                   'Desabilitar a entrada de gameplay durante cinematics e restaurá-la apenas na saída '
                   'válida do estado. Não criar leitores paralelos de E nos capítulos.'],
  'acceptance': ['Um pressionamento de E nunca abre duas atividades.',
                 'Soltar E pausa a operação; continuar segurando não atravessa etapas.',
                 'Pausa, troca de foco e saída de cinematic deixam a entrada em estado seguro.'],
  'tests': ['Manter E ao entrar e fechar um painel; pressionar durante uma transição.',
            'Pausar, desfocar e reiniciar enquanto a ferramenta opera; confirmar que ela não permanece '
            'ligada.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'CORE-004',
  'title': 'Implementar o diretor de sequências com um único relógio',
  'role': 'R1',
  'priority': 'P0',
  'hours': 1.5,
  'depends_on': ['CORE-002'],
  'files': ['Assets/_Game/Presentation/SequenceDirector.cs',
            'Assets/_Game/Presentation/CameraDirector.cs',
            'Assets/_Game/Presentation/SequenceClock.cs'],
  'owned_paths': ['Assets/_Game/Presentation/SequenceDirector.cs',
                  'Assets/_Game/Presentation/CameraDirector.cs',
                  'Assets/_Game/Presentation/SequenceClock.cs'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R1 entrega a infraestrutura de apresentação '
             'consumida pelos roteiros dos três capítulos. Câmera, atores, texto e áudio precisam permanecer '
             'sincronizados mesmo após pausa, cancelamento e reinício.',
  'deliverable': 'Sequências configuradas por dados, com tomadas reais, eventos nomeados, controle de câmera '
                 'e saídas seguras.',
  'instructions': ['Implementar keyframes com campo de visão, curvas de suavização, alvo de enquadramento e '
                   'eventos nomeados, todos subordinados a um único relógio de sequência.',
                   'Adicionar proteção para a câmera permanecer acima do terreno e garantir um único '
                   'AudioListener ativo. Uma transição deve preservar pose e campo de visão quando essa '
                   'continuidade estiver prevista.',
                   'Expor eventos para legendas e áudio; permitir testar estados anteriores e seguintes. As '
                   'tomadas devem mover câmera e atores de fato, sem métodos ou referências vazias.',
                   'Tratar término, salto permitido e cancelamento por um único caminho de saída. A '
                   'devolução do controle acontece uma vez, e não em callbacks concorrentes.'],
  'acceptance': ['Duas tomadas conectadas reproduzem sem salto involuntário de posição ou relógios '
                 'divergentes.',
                 'Pausa congela câmera, atores e texto juntos.',
                 'Término, salto permitido e cancelamento liberam o controle uma única vez.'],
  'tests': ['Reproduzir, pausar e reiniciar no meio de uma tomada.',
            'Comparar eventos a 30 e 60 FPS e conferir os limites inicial e final de cada clip.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'CORE-005',
  'title': 'Criar o fluxo do aplicativo, o relógio ativo e o ciclo dos capítulos',
  'role': 'R1',
  'priority': 'P0',
  'hours': 1.25,
  'depends_on': ['CORE-002'],
  'files': ['Assets/_Game/Core/ChapterDirector.cs',
            'Assets/_Game/Core/SessionClock.cs',
            'Assets/_Game/Core/EvidenceStore.cs',
            'Assets/_Game/Core/SettingsStore.cs'],
  'owned_paths': ['Assets/_Game/Core/ChapterDirector.cs',
                  'Assets/_Game/Core/SessionClock.cs',
                  'Assets/_Game/Core/EvidenceStore.cs',
                  'Assets/_Game/Core/SettingsStore.cs'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R1 define o ciclo de vida da sessão. Cada '
             'missão é independente: o avaliador deve iniciar qualquer capítulo sem jogar os demais, e a '
             'duração medida precisa excluir leitura do arquivo, carregamento e pausa.',
  'deliverable': 'Fluxo dossiê → carregamento → capítulo → conclusão → dossiê, com reinício, evidências e '
                 'preferências controlados.',
  'instructions': ['Iniciar o relógio ativo somente na entrada efetiva do capítulo; excluir dossiê, '
                   'carregamento e pausa. Ações e minigames fazem parte do tempo da missão.',
                   'Descarregar cena, RenderTextures, fotografias e áudio antes de trocar de capítulo. '
                   'Registrar evidências por ID com conclusão idempotente.',
                   'Persistir apenas preferências e tutorial visto. Uma partida nova deve continuar completa '
                   'e jogável mesmo se a preferência de tutorial estiver ausente ou já marcada.',
                   'Definir transições explícitas e limites dos percursos; não somar enums nem acessar um '
                   'ponto depois do último índice. Reiniciar limpa relógio e evidências da sessão anterior.'],
  'acceptance': ['Qualquer capítulo pode iniciar sem outro concluído.',
                 'Reiniciar limpa evidências e tempo ativo.',
                 'Troca de capítulo não deixa cena, foto ou áudio anterior em uso.'],
  'tests': ['Abrir os capítulos na ordem 2 → 1 → 3 e repetir a sequência.',
            'Manter uma pausa longa e confirmar que ActiveSeconds não aumenta.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'CORE-006',
  'title': 'Automatizar montagem de cenas e builds sem sobrescrever colegas',
  'role': 'R1',
  'priority': 'P0',
  'hours': 0.75,
  'depends_on': ['CORE-001', 'CORE-002'],
  'files': ['Assets/_Game/Editor/SpaceAppsSceneBuilder.cs',
            'Assets/_Game/Editor/SpaceAppsBuild.cs',
            'Tools/build_release.py'],
  'owned_paths': ['Assets/_Game/Editor/SpaceAppsSceneBuilder.cs',
                  'Assets/_Game/Editor/SpaceAppsBuild.cs',
                  'Tools/build_release.py'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R1 fornece a automação compartilhada. Cada '
             'capítulo terá seu próprio builder e ownership, e a troca de plataforma precisa terminar antes '
             'de compilar para evitar remoção incorreta de shaders.',
  'deliverable': 'Menus de reconstrução por capítulo e build reproduzível, com alvo ativo correto e '
                 'relatório dos arquivos gerados.',
  'instructions': ['Dar a cada builder uma raiz e um responsável. Criar objetos, prefabs, materiais e '
                   'referências reais sem limpar indiscriminadamente a cena ou reescrever arquivos de outro '
                   'capítulo.',
                   'Trocar o alvo de compilação, aguardar importação e recarga do domínio e só então '
                   'executar BuildPlayer. Não presumir que BuildPlayerOptions.target substitui a troca de '
                   'plataforma.',
                   'Validar chaves, materiais e referências antes de gerar o player. Registrar resultado, '
                   'arquivos e hashes para identificar a build testada.',
                   'Manter GUIDs estáveis em reconstruções repetidas e garantir que a cena resultante possa '
                   'ser aberta e jogada sem montagem manual.'],
  'acceptance': ['Reconstruir não altera os arquivos do colega nem perde GUIDs.',
                 'A build Web usa seu perfil e preserva os shaders de cor do mundo.',
                 'A cena reconstruída possui componentes e referências válidos.'],
  'tests': ['Executar o mesmo builder duas vezes e comparar referências.',
            'Gerar Web após Windows e Windows após Web, sempre em operações sequenciais.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'ART-001',
  'title': 'Auditar modelos e referências e definir o pipeline de assets',
  'role': 'R4',
  'priority': 'P0',
  'hours': 0.75,
  'depends_on': [],
  'files': ['Docs/Art/ASSET_AUDIT.md', 'ASSET_CREDITS.md', 'Docs/Art/IMPORT_RULES.md'],
  'owned_paths': ['Docs/Art/ASSET_AUDIT.md', 'ASSET_CREDITS.md', 'Docs/Art/IMPORT_RULES.md'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R4 define como os assets entram no projeto. '
             'Os modelos NASA fornecidos depois não podem bloquear o código agora; toda alternativa '
             'provisória precisa continuar reconhecível e ter caminho claro de substituição.',
  'deliverable': 'Inventário de modelos principais e referências, com origem, direitos, escala, componentes '
                 'e alternativa de produção; a importação pertence às tarefas de cada capítulo.',
  'instructions': ['Conferir as páginas NASA indicadas e registrar arquivos da equipe como pendentes até '
                   'serem recebidos. Não declarar download ou importação sem evidência.',
                   'Definir eixos, escala em metros, orçamento de polígonos, materiais e texturas. '
                   'Documentar o tratamento de normais RGB e a separação de rodas, braço, câmera e antena '
                   'quando forem animados.',
                   'Para cada modelo pendente, registrar uma alternativa leve baseada em fotografias, com as '
                   'peças reconhecíveis e o custo de substituí-la depois.',
                   'Exigir URL, instituição ou autor, licença, atribuição e destino para cada recurso. Estar '
                   'disponível na internet não estabelece permissão de redistribuição.'],
  'acceptance': ['Spirit/Opportunity, Pioneer 10 e Surveyor 3 possuem uma lista de componentes '
                 'reconhecíveis.',
                 'Os direitos não são presumidos pela origem da página.',
                 'Cada recurso planejado tem responsável, prioridade e estado de recebimento.'],
  'tests': ['Comparar a silhueta e os componentes com fotos da missão correspondente.',
            'R1 e R7 revisam o inventário e distinguem recurso planejado de importação concluída.'],
  'sources': ['https://science.nasa.gov/3d-resources/pioneer/',
              'https://science.nasa.gov/resource/spirit-and-opportunity-rover-3d-model/',
              'https://www.nasa.gov/nasa-brand-center/images-and-media/'],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'UI-001',
  'title': 'Definir o sistema visual do dossiê e do HUD em layouts estáticos',
  'role': 'R2',
  'priority': 'P0',
  'hours': 1.25,
  'depends_on': [],
  'files': ['Docs/Art/UI_SPEC.md', 'Assets/_Game/UI/Design/'],
  'owned_paths': ['Docs/Art/UI_SPEC.md', 'Assets/_Game/UI/Design/'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R2 estabelece a linguagem visual '
             'compartilhada antes das telas funcionais. O avaliador não deve perder tempo lendo uma pasta '
             'extensa nem procurar o botão de início.',
  'deliverable': 'Composição simples de três pastas, HUD e painel de instrumento, com hierarquia, escalas e '
                 'variantes de resolução definidas.',
  'instructions': ['Cada pasta destaca nome, fotografia, uma pergunta e Iniciar. Informações detalhadas são '
                   'opcionais; o botão de início aparece sem rolagem.',
                   'Definir fonte com licença registrada, margens, cores e componentes de objetivo, E e '
                   'progresso de tecla mantida. A hierarquia deve funcionar sem depender só da cor.',
                   'Desenhar variantes de 960×600, 1280×720 e 4:3. Testar texto mais longo em inglês e '
                   'manter o botão de pausa alinhado.',
                   'Reutilizar a mesma linguagem nos três capítulos, com identidades locais por imagem e '
                   'cor, sem construir uma sala 3D pesada para o menu.'],
  'acceptance': ['A página principal é compreensível em até 10 s e Iniciar não exige rolagem.',
                 'Os três capítulos parecem parte do mesmo jogo.',
                 'A informação essencial não depende apenas de cor ou ícone.'],
  'tests': ['Pedir revisão a uma pessoa que não leu o briefing.',
            'Conferir fonte, contraste e hierarquia no tamanho real de um iframe pequeno.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'UI-002',
  'title': 'Implementar seleção por dossiês e carregamento explícito',
  'role': 'R2',
  'priority': 'P0',
  'hours': 1.5,
  'depends_on': ['CORE-005', 'UI-001'],
  'files': ['Assets/_Game/UI/Dossier/', 'Assets/_Game/Core/ChapterCatalog.asset'],
  'owned_paths': ['Assets/_Game/UI/Dossier/', 'Assets/_Game/Core/ChapterCatalog.asset'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R2 transforma o layout aprovado em entrada '
             'funcional para os três capítulos. Consumir ChapterDefinition e o fluxo global; não criar uma '
             'campanha linear obrigatória.',
  'deliverable': 'Três capítulos selecionáveis, com contexto opcional, configurações e preparação da cena '
                 'claramente apresentada.',
  'instructions': ['Consumir o catálogo de capítulos e manter os assets 3D pesados fora do menu. A pasta '
                   'mostra apenas o conteúdo necessário para decidir e iniciar.',
                   'Usar o clique em Iniciar para liberar áudio e, quando aplicável, captura do cursor. '
                   'Exibir carregamento antes de ativar o relógio da missão.',
                   'Disponibilizar retorno, configurações, créditos, PT/EN e duração por capítulo. A leitura '
                   'detalhada não é condição para avançar.',
                   'Impedir entrada duplicada por cliques rápidos ou troca de idioma durante o carregamento. '
                   'O dossiê não herda vento marciano nem mantém outra câmera de gameplay ativa.'],
  'acceptance': ['Todos os capítulos iniciam sem leitura obrigatória ou pré-requisito de campanha.',
                 'O carregamento tem indicação visível e a interface responde.',
                 'Menu e gameplay não duplicam câmera ou áudio.'],
  'tests': ['Entrar e sair de cada capítulo duas vezes.',
            'Clicar rapidamente e trocar idioma durante o carregamento, verificando que a cena abre uma '
            'única vez.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'UI-003',
  'title': 'Implementar HUD, orientação e prompt E compartilhado',
  'role': 'R2',
  'priority': 'P0',
  'hours': 1.25,
  'depends_on': ['CORE-003', 'UI-001'],
  'files': ['Assets/_Game/UI/MissionHUD.cs',
            'Assets/_Game/UI/InteractionPrompt.cs',
            'Assets/_Game/UI/ObjectiveIndicator.cs'],
  'owned_paths': ['Assets/_Game/UI/MissionHUD.cs',
                  'Assets/_Game/UI/InteractionPrompt.cs',
                  'Assets/_Game/UI/ObjectiveIndicator.cs'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R2 apresenta o candidato fornecido pelo '
             'InteractionService. A orientação precisa funcionar para pessoas que nunca jogam, inclusive '
             'quando o alvo sai do enquadramento.',
  'deliverable': 'Prompt E + verbo + objeto, distância, direção e progresso consistentes nas atividades dos '
                 'três capítulos.',
  'instructions': ['Consumir o candidato e a disponibilidade do InteractionService; não recalcular um '
                   'alcance diferente no HUD.',
                   'Apresentar âncora no mundo e seta de borda para o objetivo fora do quadro. Esconder a '
                   'orientação durante pausa e cinematic.',
                   'Mostrar progresso de E mantido e feedback distinto para início, soltura e conclusão. Dar '
                   'suporte ao estado de comando em trânsito da Surveyor.',
                   'Manter um objetivo principal legível. A energia aparece quando ajuda a entender a ação, '
                   'em vez de ocupar o dossiê ou todos os estados do HUD.'],
  'acceptance': ['E aparece disponível apenas quando a ação pode executar.',
                 'O objetivo é reconhecível sem procurar um pixel específico.',
                 'Energia, distância e progresso não disputam o mesmo espaço visual.'],
  'tests': ['Trocar candidato e enquadramento com o veículo em movimento.',
            'Testar PT/EN em 960×600 e 16:9, sem cortes ou sobreposição.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'UI-004',
  'title': 'Implementar PT/EN e legendas sem caixas vazias',
  'role': 'R2',
  'priority': 'P0',
  'hours': 1.25,
  'depends_on': ['CORE-004', 'UI-001'],
  'files': ['Assets/_Game/Presentation/LocalizationService.cs',
            'Assets/_Game/Presentation/SubtitlePresenter.cs',
            'Assets/_Game/Shared/Localization/'],
  'owned_paths': ['Assets/_Game/Presentation/LocalizationService.cs',
                  'Assets/_Game/Presentation/SubtitlePresenter.cs',
                  'Assets/_Game/Shared/Localization/'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R2 recebe os textos aprovados nos roteiros de '
             'cada capítulo. A implementação deve respeitar o conteúdo, o evento e o tempo de leitura, '
             'inclusive para pessoas que jogam sem áudio.',
  'deliverable': 'Cues identificados por ID, textos PT/EN separados do código e apresentação de legendas '
                 'legível.',
  'instructions': ['Importar o texto aprovado com seus IDs; não recriar a narrativa de memória ou trocar uma '
                   'frase factual por outra genérica.',
                   'Exibir uma ideia por cue e respeitar seu tempo mínimo. Quando não houver texto, esconder '
                   'também o fundo e qualquer linha decorativa da legenda.',
                   'Trocar o idioma em um estado seguro, preservando a etapa e o tempo de leitura. Incluir '
                   'diacríticos e os arquivos das fontes usadas.',
                   'Dar a datas e nomes uma hierarquia própria, diferente das legendas. Testar também a '
                   'extensão das traduções em inglês.'],
  'acceptance': ['Nenhuma legenda vazia deixa uma caixa visível.',
                 'Todas as chaves existem em PT e EN, sem duplicatas.',
                 'Datas e nomes são legíveis e visualmente distintos do corpo das legendas.'],
  'tests': ['Validar IDs ausentes ou duplicados e textos em inglês maiores.',
            'Assistir às sequências a 1×, sem pausa, e registrar cues rápidos demais.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'UI-005',
  'title': 'Criar tutorial por ações e assistência para quem não joga',
  'role': 'R2',
  'priority': 'P0',
  'hours': 1.25,
  'depends_on': ['CORE-003', 'UI-003'],
  'files': ['Assets/_Game/UI/GuidanceController.cs', 'Assets/_Game/UI/TutorialPresenter.cs'],
  'owned_paths': ['Assets/_Game/UI/GuidanceController.cs', 'Assets/_Game/UI/TutorialPresenter.cs'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R2 torna os controles compreensíveis na '
             'primeira partida. Instruções devem desaparecer por uma ação realmente observada e a ajuda não '
             'pode completar a descoberta sem participação.',
  'deliverable': 'Tutorial integrado ao primeiro movimento e à primeira interação, com orientação adicional '
                 'quando não há progresso.',
  'instructions': ['Apresentar Mover com WASD/setas e Interagir com E. Concluir cada instrução somente após '
                   'detectar a ação correspondente.',
                   'Depois de 12 s sem progresso, reforçar o alvo; após 30 s, oferecer H, enquadramento ou '
                   'retorno ao corredor previsto. Não teleportar ou resolver automaticamente.',
                   'Aplicar alvos grandes e alternativa de teclado à placa, sintonia e varredura. Consumir '
                   'os contratos dos capítulos em vez de criar uma segunda lógica de conclusão.',
                   'Permitir repetir o tutorial independentemente da preferência salva. Evitar mensagens em '
                   'loop e não capturar a entrada destinada à atividade.'],
  'acceptance': ['Tutorial pode ser repetido mesmo depois de marcado como visto.',
                 'O jogo é concluível sem pontaria precisa.',
                 'A assistência não entra em loop nem rouba a entrada do instrumento.'],
  'tests': ['Fazer uma sessão nova com teclado, usando o mouse apenas onde houver UI alternativa.',
            'Desorientar a câmera e testar a ajuda, sem teleporte automático.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'UI-006',
  'title': 'Centralizar pausa e configurar preferências e foco no navegador',
  'role': 'R2',
  'priority': 'P0',
  'hours': 1.25,
  'depends_on': ['CORE-005', 'UI-003'],
  'files': ['Assets/_Game/UI/PauseMenu.cs',
            'Assets/_Game/Presentation/FocusPause.cs',
            'Assets/_Game/UI/SettingsPanel.cs'],
  'owned_paths': ['Assets/_Game/UI/PauseMenu.cs',
                  'Assets/_Game/Presentation/FocusPause.cs',
                  'Assets/_Game/UI/SettingsPanel.cs'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R2 integra pausa, acessibilidade e '
             'comportamento de foco. Esc pode ser consumido pelo navegador; por isso o jogo também oferece P '
             'e botão próprio.',
  'deliverable': 'Pausa por P, Esc ou botão, configurações acessíveis e retomada por ação deliberada.',
  'instructions': ['Centralizar o painel por âncoras e testar proporções diferentes. Incluir volumes de '
                   'música, efeitos e UI, idioma, sensibilidade, movimento reduzido e qualidade.',
                   'Ao perder foco, pausar e limpar E/WASD. O clique em Continuar retoma a missão e o áudio '
                   'sem reaplicar teclas antigas.',
                   'Manter fullscreen e captura do cursor opcionais. Não depender exclusivamente de Esc para '
                   'abrir ou fechar a pausa.',
                   'Persistir preferências pelo serviço global e garantir reinício sem estado residual do '
                   'capítulo anterior.'],
  'acceptance': ['O painel permanece centralizado em quatro proporções testadas.',
                 'Retomar não move o veículo nem liga uma ferramenta sozinho.',
                 'Preferências persistem e reinício funciona sem estado antigo.'],
  'tests': ['Testar iframe, fullscreen, redimensionamento e troca real de aba.',
            'Manter W/E ao desfocar e conferir a retomada deliberada.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'UI-007',
  'title': 'Apresentar evidências, limitar fotografias e permitir a escolha final',
  'role': 'R2',
  'priority': 'P0',
  'hours': 1.0,
  'depends_on': ['CORE-005', 'UI-003'],
  'files': ['Assets/_Game/UI/EvidencePanel.cs',
            'Assets/_Game/UI/PhotoChoice.cs',
            'Assets/_Game/Presentation/PhotoCapture.cs'],
  'owned_paths': ['Assets/_Game/UI/EvidencePanel.cs',
                  'Assets/_Game/UI/PhotoChoice.cs',
                  'Assets/_Game/Presentation/PhotoCapture.cs'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R2 implementa a apresentação visual dos '
             'resultados e da lembrança do jogador. A captura precisa respeitar memória Web, e a foto do '
             'memorial deve ser a escolhida na sessão.',
  'deliverable': 'Painéis científicos com imagem apropriada e proveniência, galeria limitada e seleção por '
                 'teclado ou mouse.',
  'instructions': ['Capturar diretamente para RenderTexture de até 960×540, manter no máximo três fotos e '
                   'liberar recursos ao sair ou reiniciar. Não capturar uma tela 4K temporária.',
                   'Identificar data e crédito nas imagens de arquivo. Fotografias do jogador devem vir da '
                   'sessão atual e não ser trocadas por imagens históricas sem aviso.',
                   'Permitir selecionar com setas/E ou clique em miniaturas visíveis. Mostrar uma prévia '
                   'legível e usar a seleção confirmada no memorial.',
                   'Conectar imagens de resultado aos minigames previstos, inclusive meteorito e Sol. Não '
                   'deixar esses painéis sem foto quando o roteiro a exigir.'],
  'acceptance': ['Todo resultado que exige evidência visual recebe uma imagem válida.',
                 'O memorial usa a fotografia selecionada.',
                 'O limite de três imagens e o descarte são respeitados.'],
  'tests': ['Capturar, descartar e reiniciar, conferindo referências e liberação.',
            'Selecionar sem mouse e verificar que a foto permanece válida até o fim.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'AUD-001',
  'title': 'Selecionar música e efeitos licenciados por intenção narrativa',
  'role': 'R7',
  'priority': 'P0',
  'hours': 0.75,
  'depends_on': [],
  'files': ['Docs/Art/AUDIO_CUE_SHEET.md', 'ASSET_CREDITS.md'],
  'owned_paths': ['Docs/Art/AUDIO_CUE_SHEET.md', 'ASSET_CREDITS.md'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R7 prepara a direção sonora dos três '
             'capítulos. A música deve apoiar descoberta, viagem e legado, e o som das ações deve comunicar '
             'resposta sem cansar em poucos minutos.',
  'deliverable': 'Planilha de cues por capítulo, com fontes reutilizáveis, direitos e transições, sem '
                 'gravação comercial não autorizada.',
  'instructions': ['Organizar três famílias de intenção: curiosidade, viagem e legado. Vincular cada estado '
                   'dos roteiros a um cue, uma transição ou silêncio deliberado.',
                   'Conferir direitos da composição e da gravação, licença e atribuição. Preferir material '
                   'original ou CC0/CC BY; não presumir que distribuição gratuita libera uma gravação '
                   'protegida.',
                   'Prever motores que criem afeto pela máquina, varredura, confirmação, rádio e tempestade. '
                   'Evitar efeito de helicóptero na poeira e vento forte contínuo.',
                   'Registrar URL, autor, licença, adaptações e destino de cada arquivo. Não incluir '
                   'narração de IA.'],
  'acceptance': ['Os momentos importantes têm cue, transição ou silêncio definido.',
                 'Origem e licença de cada arquivo estão documentadas.',
                 'Feedback sonoro essencial possui equivalente visual.'],
  'tests': ['Ouvir cada loop por pelo menos 30 s e suas transições por mais 30 s.',
            'Comparar os avisos e confirmações com os sinais visuais correspondentes.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'AUD-002',
  'title': 'Implementar canais, crossfades e sonificação das ações',
  'role': 'R7',
  'priority': 'P0',
  'hours': 1.25,
  'depends_on': ['CORE-004', 'AUD-001'],
  'files': ['Assets/_Game/Presentation/AudioDirector.cs', 'Assets/_Game/Shared/Audio/'],
  'owned_paths': ['Assets/_Game/Presentation/AudioDirector.cs', 'Assets/_Game/Shared/Audio/'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R7 transforma a lista de cues em áudio '
             'funcional. Música, efeitos e UI têm volumes independentes, e o som de uma ferramenta acompanha '
             'o estado real do comando.',
  'deliverable': 'Diretor de áudio orientado por dados, com variação por cena e resposta imediata das ações '
                 'de E mantido.',
  'instructions': ['Consumir cues por dados e executar crossfades de 0,8–2 s, impedindo que duas rotinas '
                   'disputem o volume da mesma fonte.',
                   'Ligar, modular e parar o loop do instrumento pelos eventos de HoldAction. Não deixar o '
                   'som tocando na pausa ou no menu.',
                   'Usar vento baixo e variável quando apropriado, rádio com duração limitada e silêncio '
                   'intencional. Sons externos no espaço e na Lua são sonificação cinematográfica, não '
                   'simulação de propagação pelo vácuo.',
                   'Conectar volumes dos três canais às preferências e restaurar o áudio corretamente após '
                   'reinício ou troca de capítulo.'],
  'acceptance': ['Volumes são ajustáveis sem tornar a ação incompreensível.',
                 'Nenhum clique ou loop fica agressivo depois de 30 s.',
                 'O último silêncio é intencional, e reiniciar restaura o estado sonoro.'],
  'tests': ['Verificar o AudioContext após o clique inicial no Web.',
            'Alternar capítulo, pausa e menu sem fontes duplicadas ou loops esquecidos.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'QA-001',
  'title': 'Gerar o primeiro build Web e provar o perfil Leve antes do polimento',
  'role': 'R7',
  'priority': 'P0',
  'hours': 1.25,
  'depends_on': ['CORE-001'],
  'files': ['Assets/_Game/Settings/Web/', 'Assets/WebGLTemplates/SpaceApps/', 'QA/WebBaseline/'],
  'owned_paths': ['Assets/_Game/Settings/Web/', 'Assets/WebGLTemplates/SpaceApps/', 'QA/WebBaseline/'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R7 valida a distribuição cedo para que os '
             'capítulos não sejam produzidos sobre shaders ou recursos incompatíveis. Configurações de '
             'orçamento não equivalem a desempenho medido.',
  'deliverable': 'Pipeline Web demonstrado numa cena mínima, com perfil Leve, medidas de carregamento e '
                 'evidências visuais reais.',
  'instructions': ['Limitar a resolução interna inicial a 1280×720, usar uma luz principal com sombras e '
                   'desabilitar SSAO e profundidade de campo no perfil Leve.',
                   'Compilar Release com IL2CPP e Brotli. Registrar tamanho real dos arquivos e conferir '
                   'MIME e Content-Encoding no servidor local.',
                   'Provar material opaco, solo, áudio após gesto do usuário e pausa. Guardar capturas reais '
                   'para detectar mundo invisível ou coberto por uma cor uniforme.',
                   'Verificar a plataforma ativa e os shaders incluídos depois de alternar Windows → Web. '
                   'Distinguir claramente limite configurado, medida neste dispositivo e desempenho ainda '
                   'não homologado.'],
  'acceptance': ['O Web abre em Chrome e Firefox sem erros observados.',
                 'Os shaders de cor necessários ao mundo não foram removidos.',
                 'O relatório identifica dispositivo e separa orçamento configurado de FPS medido.'],
  'tests': ['Compilar depois de trocar o alvo de Windows para Web.',
            'Testar cache vazio e tela física 4K, mantendo o teto interno de 1280×720.'],
  'sources': ['https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/buildplayeroptions/target',
              'https://itch.io/docs/creators/html5'],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'QA-002',
  'title': 'Automatizar progressão, tecla mantida, pausa, cancelamento e descarte',
  'role': 'R7',
  'priority': 'P0',
  'hours': 1.5,
  'depends_on': ['CORE-003', 'CORE-004', 'CORE-005'],
  'files': ['Assets/_Game/Tests/', 'Tools/run_qa.py', 'QA/Contracts/'],
  'owned_paths': ['Assets/_Game/Tests/', 'Tools/run_qa.py', 'QA/Contracts/'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R7 protege os contratos comuns antes de os '
             'capítulos se integrarem. Testes de estados isolados e operadores automáticos são úteis, mas '
             'não demonstram compreensão de novatos.',
  'deliverable': 'Testes comportamentais do núcleo e um harness para os capítulos, com falhas visíveis e '
                 'identificação da build testada.',
  'instructions': ['Provar idempotência, entrada mantida entre etapas, dependências de progresso e reinício. '
                   'Conferir que um hold não conclui duas ações.',
                   'Abortar a jornada após um bloqueio fatal. Não gerar PASS para sistemas que não chegaram '
                   'a executar.',
                   'Separar testes de estado isolado, percurso com operador automático e partidas de '
                   'novatos. Documentar preparação de estado, mocks e qualquer ajuda aplicada pelo harness.',
                   'Incluir hash da build e escopo real nos logs. O relógio do capítulo não pode concluir '
                   'automaticamente objetivos para fazer o teste passar.'],
  'acceptance': ['Os testes detectam um hold que atravessa duas etapas.',
                 'Não há erros conhecidos de referência nos caminhos aprovados.',
                 'Logs identificam a build e o que foi efetivamente exercitado.'],
  'tests': ['Executar Edit Mode e Play Mode e registrar uma falha controlada.',
            'Conferir que nenhum teste completa objetivos apenas pelo relógio do capítulo.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'},
 {'id': 'UI-008',
  'title': 'Refinar ornamentos, transições do arquivo e fotografia editorial',
  'role': 'R2',
  'priority': 'P1',
  'hours': 1.5,
  'depends_on': ['UI-002', 'UI-004'],
  'files': ['Assets/_Game/UI/Dossier/', 'Assets/_Game/UI/Styles/'],
  'owned_paths': ['Assets/_Game/UI/Dossier/', 'Assets/_Game/UI/Styles/'],
  'context': 'Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete '
             'pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis '
             'para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa '
             'infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e '
             'os contratos aprovados antes de editar arquivos. R2 executa este polimento P1 somente depois '
             'da seleção funcional e das legendas. A apresentação pode ganhar cuidado editorial sem aumentar '
             'o tempo até Iniciar.',
  'deliverable': 'Refinamento visual do dossiê após o MVP, preservando entrada rápida, legibilidade e '
                 'acessibilidade.',
  'instructions': ['Adicionar microanimações e revelação de folha com duração inferior a 250 ms, respeitando '
                   'a opção de movimento reduzido.',
                   'Usar fotografias, recortes e linhas técnicas discretas. Não construir uma sala 3D para '
                   'apresentar o menu.',
                   'Preservar o foco de teclado, a interação existente e todas as atribuições dos recursos.',
                   'Comparar com o layout anterior e reverter qualquer ornamento que esconda o botão ou '
                   'atrase a decisão do avaliador.'],
  'acceptance': ['Iniciar continua disponível com um clique.',
                 'Não há regressão de leitura nas resoluções previstas.',
                 'Créditos e condições de licença permanecem preservados.'],
  'tests': ['Comparar antes e depois dentro do iframe.',
            'Testar tanto com movimento reduzido ativado quanto desativado.'],
  'sources': [],
  'cutline': 'Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir '
             'ornamentação antes de alterar contratos.'}]

if __name__ == '__main__':
    out = ROOT / 'Data/Backlog/global.json'
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(TASKS, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'{len(TASKS)} tarefas compartilhadas geradas.')
