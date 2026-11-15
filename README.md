# Abandoned, But Not Forgotten — novo projeto

Planejamento de produção para **14–15 de novembro de 2026**, com sete pessoas e três capítulos independentes de **5–7 minutos cada**:

1. Spirit & Opportunity — duas geólogas em Marte.
2. Pioneer 10 — o caminho para os planetas exteriores.
3. Surveyor 3 — ver, tocar e preparar o caminho para pessoas.

**Este repositório começa com documentação, contratos e tarefas. O novo jogo ainda precisa ser implementado.** O projeto Unity anterior permanece preservado no repositório privado AbandonedButNotForgotten. Não transferimos seus resultados de teste para esta edição.

## Por onde começar

- **Repositório público:** https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps
- **Kanban privado:** https://github.com/users/joaovdamaceno/projects/1
- **Para revisar o jogo sem ler tarefas:** [gameplay completa](Docs/02_GAMEPLAY_REVIEW.md).
- **Para abrir um chat com IA:** copie [contexto mestre](Docs/01_MASTER_CONTEXT.md) e o corpo da tarefa escolhida.
- **Para programar:** [arquitetura e contratos](Docs/03_ARCHITECTURE.md) e [AGENTS.md](AGENTS.md).
- **Para organizar as sete pessoas:** [produção do fim de semana](Docs/Production/WEEKEND.md), [Kanban](Docs/Production/KANBAN.md) e [backlog](Docs/Production/BACKLOG.md).
- **Para cenas, legendas e ciência:** [capítulos](Docs/Chapters/), com arquivos de fontes separados.
- **Para arte e navegador:** [direção de arte](Docs/Art/DIRECTION.md) e [orçamento Web](Docs/04_WEB_AND_QA.md).

O dossiê apresenta uma pergunta, uma imagem e **Iniciar**. Cada capítulo começa e termina sozinho. A leitura do arquivo, o carregamento e as pausas ficam fora dos 5–7 minutos. Os três capítulos juntos duram cerca de 18 minutos; um avaliador pode escolher qualquer um sem jogar os anteriores.

## Estado e ferramentas

Unity prevista: **6000.6.7f1**, URP **17.6.0**, desktop WebGL 2, teclado/mouse, itch.io. A inicialização da Unity é a tarefa CORE-001 (seção abaixo). Ainda não existe build nova executável. Nenhum asset NASA novo foi baixado ou importado para este repositório; os modelos fornecidos pela equipe serão auditados nas tarefas de arte.

Valide a documentação com `python Tools/validate_plan.py`. O backlog em `Data/Backlog/` é a fonte dos prompts e do grafo de dependências. Os vínculos remotos e o resultado da sincronização ficam em `Data/github-map.json` e `QA/Planning/` quando disponíveis.

Os textos históricos são verificados nas fontes oficiais e distinguem observações, hipóteses e simplificações. Esta é uma produção educativa independente, sem afiliação ou endosso da NASA. Créditos de assets serão registrados em `ASSET_CREDITS.md` antes de cada importação.

## Projeto Unity (CORE-001)

O projeto Unity fica na raiz deste repositório (`Assets/`, `Packages/`, `ProjectSettings/`), ao lado de `Docs/`, `Data/` e `Tools/`.

| Item | Valor fixado | Onde conferir |
| --- | --- | --- |
| Editor | 6000.6.7f1 (b22767f04f04) | `ProjectSettings/ProjectVersion.txt` |
| Pipeline | URP 17.6.0, renderer Forward, asset `Assets/_Game/Settings/URP/SpaceApps_URP.asset` | `GraphicsSettings`, validador |
| Entrada | Input System 1.20.1, *Active Input Handling* = somente Input System (consumido pelo `InputRouter` da CORE-003) | `ProjectSettings.asset` |
| Testes | Test Framework 1.8.0 | `Assets/_Game/Core/Tests/EditMode/` |
| Cor / Web | Linear; WebGL 2 (OpenGLES3) apenas; threads desligadas; exceções explícitas | `PlayerSettings` |
| Serialização | Force Text, Visible Meta Files | `EditorSettings`, `VersionControlSettings` |

Dependências diretas aprovadas estão em `Packages/manifest.json` e em `ProjectSetupSpec.ManifestDependencies`; versões resolvidas em `Packages/packages-lock.json`. **Timeline e glTFast não foram adicionados**: entram só com PR que mostre o consumidor real (ver `Docs/Art/IMPORT_RULES.md`). A primeira abertura em batchmode reinseriu pacotes padrão da Unity no manifesto; eles foram removidos e o validador agora rejeita qualquer dependência direta fora da lista.

Assemblies: `Abandoned.SpaceApps` (runtime, `Assets/_Game/`), `Abandoned.SpaceApps.Editor` (`Assets/_Game/Editor/`) e `Abandoned.SpaceApps.Core.Tests`. **Toda pasta `Editor` dentro de `Assets/_Game` precisa de um `.asmref` para `Abandoned.SpaceApps.Editor`**; o validador e um teste acusam a falta.

### Abrir e verificar

1. `git lfs install` uma vez por máquina e clone com LFS (fontes e binários estão em LFS).
2. Abrir a pasta raiz no Unity Hub com o Editor 6000.6.7f1. Restaurar pacotes exige rede **só na primeira importação**; o jogo não usa serviços externos em execução.
3. Menu **Abandoned Space Apps → Validar configuração do projeto** (ou **Aplicar…** para reaplicar de forma idempotente, sem trocar GUIDs).
4. Linha de comando, a partir da raiz:

```text
"<Unity 6000.6.7f1>/Editor/Unity.exe" -batchmode -projectPath . -executeMethod Abandoned.SpaceApps.EditorTools.ProjectSetupValidator.ValidateFromCommandLine -logFile -
"<Unity 6000.6.7f1>/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults QA/Local/editmode.xml
```

Código de saída 0 = conforme. Mudar qualquer valor exige alterar `Assets/_Game/Core/Editor/ProjectSetupSpec.cs`, esta tabela e pedir revisão de R1 no mesmo PR.

**Aviso de ambiente:** clones dentro de pastas sincronizadas (OneDrive, Dropbox) sincronizam `Library/` e podem travar a importação; prefira um diretório local fora da sincronização.

## Colaboração

`main` guarda o planejamento/entrega; `develop` recebe os PRs. Use branch por tarefa e não edite a mesma cena junto de outra pessoa. As 84 issues possuem função, prioridade, estimativa, prompt, arquivos sob responsabilidade, critérios, testes e bloqueadores nativos. Os nomes das sete pessoas serão atribuídos depois. Na verificação de 14/11/2026, as 84 issues estavam abertas: 7 Prontas, 69 Bloqueadas e 8 no Backlog. Nenhuma implementação foi marcada como concluída.

O sincronizador local pode atualizar labels e colunas. O workflow do repositório atualiza integração/readiness das issues; acesso adicional ao Project no Actions depende de segredo configurado pelo titular e não foi criado automaticamente. Consulte o procedimento de claim e revisão em [KANBAN.md](Docs/Production/KANBAN.md).


### Capítulo Surveyor 3 integrado

Abra `Assets/_Game/Chapters/Surveyor/Scenes/SurveyorArchive.unity` e pressione Play; a pasta Surveyor inicia o capítulo completo. A cena `SurveyorChapter.unity` permite inspeção isolada. Controles, montagem, build Web próprio e limites de validação: [SURVEYOR_CHAPTER](Docs/Production/SURVEYOR_CHAPTER.md). Este arquivo de teste não substitui a integração global dos outros capítulos.
