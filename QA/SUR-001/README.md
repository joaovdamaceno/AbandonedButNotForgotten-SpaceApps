# SUR-001 — evidências de validação

Executado em 14/11/2026, Windows, Unity **6000.6.7f1 (b22767f04f04)**, batchmode/nographics, base `develop` **934a2fd** (CORE-006 integrada). Sem teste visual de gameplay.

| Verificação executada | Resultado | Evidência |
| --- | --- | --- |
| Importação inicial, compilação e geração do ScriptableObject | **PASS**, processo Unity terminou com código 0 | `catalog-validation.txt`; asset tipado e `.meta` versionados |
| EditMode da assembly Surveyor | **16 passaram, 0 falharam, 0 ignorados**, saída 0 | `editmode.xml` gerado pelo Unity Test Framework 1.8.0 |
| `Tools/validate_plan.py` | **PASS**, nenhuma inconsistência no planejamento | Execução local; 84 tarefas, dependências/caminhos válidos |
| `Tools/test_readiness.py` | **21 passaram** | Execução local dos testes da ferramenta de fluxo |
| Auditoria de alterações | Somente arquivos da tarefa, regra de branch autorizada e registro de origem | `git diff --check` e lista do PR |

Os testes EditMode verificam: carregamento tipado e igualdade JSON/asset, cobertura dos 31 IDs do roteiro, GUID e bytes estáveis na reconstrução, bloqueio de conflitos no HUD/cues, identificação obrigatória da reconstrução de 1969, citações ausentes, fonte indisponível, cue duplicado, inventário com importações fictícias, destino fora do capítulo e componentes sem referência.

Catálogo: **15 fontes, 39 decisões (7 conflitos bloqueados), 31 cues, 10 referências visuais e 8 itens de inventário**. Todos os arquivos de arte continuam explicitamente não importados/não construídos. A biblioteca consultada não é enviada ao player; nenhum download ocorre pelo catálogo em runtime.

Logs integrais de importação e testes, PDFs e renders de pesquisa estão em `QA/Local/SUR-001/`, ignorada no Git. Foram inspecionados para erros de compilação e falhas de testes. A Unity reserializou automaticamente `SpaceApps_URP_GlobalSettings.asset` durante o batch; essa alteração não faz parte da tarefa e foi descartada, preservando a configuração da equipe.

O YAML e os `.meta` mantêm o formato nativo da Unity, incluindo espaços após campos vazios. A verificação de whitespace dos arquivos autorais passa; normalizar manualmente o asset quebraria o teste de reconstrução byte a byte.

**Pendente:** revisão científica humana de outra função (R7), principalmente divergências de horário, coordenadas e contagem de ensaios. O passo 3 da issue originalmente pedia 4 carga/13 impacto; os relatórios contemporâneos consultados indicam 8/14. As duas versões estão registradas; o conteúdo liberado usa quatro trincheiras e ensaios qualitativos até reconciliação.

Não executados: cena jogável Surveyor, avaliação com novatos, teste visual, desempenho de navegador ou build Web/Windows. São entregas de tarefas posteriores; testes desta tarefa não certificam esses critérios. A issue deve continuar aberta/em revisão até aprovação e integração por R1.
