# Contexto mestre — cole uma vez por chat

Você trabalha em Abandoned, But Not Forgotten, jogo educativo e cinematográfico independente para o NASA Space Apps. O novo projeto será produzido por sete pessoas em 14–15/11/2026. Seu trabalho é executar UMA tarefa do Kanban. Leia o AGENTS.md e os arquivos citados pela tarefa, identifique suas dependências e respeite donos de arquivos. Se uma dependência não estiver integrada, não simule que existe nem duplique seu sistema: escolha trabalho independente ou prepare o asset isolado.

## Experiência definida

Três capítulos independentes de 5–7 minutos **cada**, cronometrados depois de Iniciar, sem contar arquivo, carregamento e pausa. Menu em dossiês 2D leves: Spirit & Opportunity, Pioneer 10, Surveyor 3. Sem progressão obrigatória entre capítulos. A pessoa pode nunca ter jogado: objetivo explícito, verbo junto de E, confirmação imediata, tarefas fáceis e nenhuma procura aleatória de pixel. Se soltar E, a ferramenta pausa com feedback. Não há game over, punição por lentidão ou atividade essencial baseada em reflexo.

**Marte:** Opportunity é controlada em terceira pessoa; Spirit aparece em planos próprios, com local/data explícitos. Descoberta por evidências combinadas, hardware descartado, sobrevivência de 2007, despedida da Spirit e perda de energia em 2018.

**Pioneer:** capítulos de viagem e instrumentos; asteroides exagerados aparecem só em uma visualização de imaginação explicitamente identificada. Imagens reais e varredura por rotação, placa simbólica, sinais progressivamente fracos. A sonda não desvia de um arcade histórico real nem recebe luz de Aldebaran como se estivesse ao lado dela.

**Surveyor (SUR-019):** câmera híbrida, viagem/pouso em cinema externo, ciência pela câmera e pá estacionária, corte curto com mouse ou assistência ao segurar E e deformação por contato. Não é rover. Visita de 1969 com corpos completos e marcha apoiada, distinta da fotografia histórica; a câmera real já estava inativa. TV sem rótulo de efeito, legendas concretas e montagem mais curta. Esta revisão pode terminar abaixo do antigo mínimo de cinco minutos; não preencher tempo com espera. Sem voz de IA ou sugestão de água lunar. Ver roteiro revisado.

## Apresentação

Legendas são o canal narrativo padrão, sem voz de IA. PT e EN têm dados próprios e tempo de leitura; nada de texto embutido nos scripts. Uma ideia por legenda, normalmente até duas linhas. Texto de objetivo permanece legível durante a ação; legendas vazias não desenham fundo. Datas e localizações só quando mudam o entendimento. Arte dirigida por imagens NASA, iluminação e composição; filme em tempo real, sem vídeos pré-renderizados. Música com licença documentada, temas e transições claros, vento discreto e sons de máquina afetivos.

## Stack e orçamento

Unity 6000.6.7f1 / URP17.6.0 / WebGL2 desktop / itch.io. InputSystem, Timeline e glTFast apenas se usados na implementação. Editor e pacotes serão fixados pelo CORE-001. A versão anterior é referência opcional; sua build aprovada não prova o novo projeto.

Comece com 720p internos, meta de 30 FPS em GPU integrada a medir. Sem SSAO ou DOF na gameplay leve, uma luz principal com sombra, partículas e transparências limitadas. Sem serviços externos de runtime. Ver `Docs/04_WEB_AND_QA.md` para limites mensuráveis e ordem de cortes. Não adicionar Addressables, shader caro ou outra arquitetura para resolver uma preferência estética antes da primeira build Web.

## Contratos que não podem divergir

`InputRouter`, `ChapterDirector`, `SessionClock`, `InteractionService`/`HoldAction`, `SequenceDirector`, `SubtitlePresenter`, `LocalizationService`, `AudioDirector`, `ChapterDefinition`, `EvidenceRecord`. Assinaturas e estados estão em `Docs/03_ARCHITECTURE.md`. Scripts de capítulos não alteram estado global diretamente. Uma fonte de duração sincroniza câmera, atores, legendas e término.

## Entregue como colega

Execute o prompt da tarefa no escopo autorizado, monte a cena/prefab por automação quando necessário e explique como verificar. Cite fatos com IDs da matriz científica. Registre assets com URL, autor, licença, arquivo e alterações. Se faltar um modelo da equipe, use o fallback explicitado e marque a pendência; não baixe conteúdo sem procedência. Não copie música comercial sob a justificativa de não comercializar.

Ao terminar, informe arquivos alterados, critérios atendidos, testes realmente executados, limitações e dependentes que ficam liberados após integração. Não invente resultado humano, build, teste visual ou qualidade cinematográfica. Não mande mensagens a pessoas nem publique em conta externa fora do pedido da tarefa.

**Agora leia o prompt da tarefa abaixo e trabalhe somente nela.**
