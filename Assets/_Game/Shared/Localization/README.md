# Localização e legendas (UI-004)

Dono: R2. Código em `Presentation/LocalizationService.cs` e `Presentation/SubtitlePresenter.cs`; dados e ferramentas aqui.

| Arquivo | Papel |
| --- | --- |
| `../../Presentation/LocalizationService.cs` | Serviço PT/EN (classe C# simples, sem singleton). Junta as tabelas JSON do projeto e resolve `Get(key)` no idioma atual |
| `../../Presentation/SubtitlePresenter.cs` | Apresenta os cues do `SequenceDirector`. Entra na coluna da base do HUD ou usa documento próprio |
| `SubtitleLayer.uxml/.uss/.prefab` | Contêiner e estilos da camada de legendas (sem HUD); a placa é `sa-subtitle` do design system |
| `SubtitleReadingCheck.cs` | Caracteres por segundo de cada cue (janela ou leitura mínima do gatilho), por idioma; aponta os rápidos demais (limite 22 cps) |
| `Pioneer10Texts.json` | Textos da Pioneer no formato compartilhado, **exportados** de `Data/Localization` por `Tools/export_pioneer_texts.py` (não editar; `--check` confere) |
| `Editor/SubtitleLayerSetup.cs` | Cria o prefab `SubtitleLayer` (menu UI-004) |
| `Editor/SubtitleCapture.cs` | Prévias em `Docs/Art/Subtitles/` e auditoria de linhas em `QA/UI-004/subtitle-audit.txt` (menu UI-004; **precisa da janela da Unity em primeiro plano de verdade**, senão as imagens saem pretas e a auditoria se declara inválida) |
| `Tests/LocalizationTests.cs` | 16 testes EditMode |

## Formato das tabelas

Todas as tabelas do projeto (Marte, HUD, dossiê, Pioneer) usam `{"texts":[{"key","group","pt","en"}]}`. `group` é opcional (`narrative`, `hud`, `instrument`, `card`). Chave: letra inicial + letras ASCII, dígitos, `.`, `_`, `-`, até 64 caracteres.

```csharp
var loc = new LocalizationService();
loc.ApplySettings(settingsStore.Data);                       // idioma salvo
loc.AddTable(textAsset.text, textAsset.name);                // uma por tabela; devolve os problemas dela
Debug.Assert(loc.Validate().Count == 0);                     // chave sem PT ou EN, conflito de texto
presenter.Initialize(loc, sequenceDirector, missionHud.BottomColumn);   // legenda logo abaixo do prompt de E
panel.Render(state, loc.Resolver);                           // os painéis dos capítulos recebem o resolvedor
// preferências: loc.SetLanguage("en") redesenha o que está na tela, sem tocar etapa nem tempo de leitura
```

## Regras

- **Chave ausente ou texto vazio no idioma atual devolve vazio**, nunca a chave nem o outro idioma; o pedido fica em `MissingRequests` para o QA. A legenda esconde a placa inteira (`sa-subtitle--hidden`): nenhuma caixa vazia, fundo ou linha decorativa.
- **Duplicatas:** mesma chave e mesmo texto é só repetição (vale a primeira); texto diferente é conflito (vale a primeira e vai para `Problems`).
- **Uma ideia por cue**: o tempo é do diretor (janela ou gatilho com leitura mínima); o apresentador só mostra e esconde. Com cues simultâneos vale a maior prioridade e, em empate, o mais recente; ao sair, volta o anterior ainda ativo. Base e topo têm placas independentes.
- **Datas e nomes** (grupo `card`, ou `SetStamp(chave)`) usam o carimbo (Plex Mono, destaque), claramente diferente do corpo (Outfit Medium, peso 450). Um cartão sem corpo vira uma placa só de carimbo.
- **Troca de idioma** é imediata e idempotente; o cue, a etapa e o tempo de leitura continuam do `SequenceDirector`. Quem chama (menu de preferências ou pausa) o faz num estado seguro.
- Cobertura de glifos conferida com fontTools: nenhum caractere das 112 chaves da Pioneer (PT e EN) falta em Outfit Regular, Outfit Medium, Outfit SemiBold ou Plex Mono SemiBold.

## Limitações

- **Só a Pioneer e as tabelas existentes** entram nas verificações. Os textos da Surveyor não estão em JSON no formato do projeto e não foram importados; Marte e o HUD já usam o formato e foram carregados juntos nos testes (sem conflito, todas as chaves em PT e EN).
- O **tempo de leitura** continua sendo uma suposição (22 cps como limite de alerta, 16 cps na estimativa da Pioneer), não um dado medido com pessoas.
- O `PioneerNarration` (PIO-013) tem o próprio caminho de texto (`TextChanged`); na integração (PIO-014) é preciso escolher um só: o `SubtitlePresenter` já cobre a apresentação. O alongamento de leitura da Pioneer (`readingStretch`) **não** foi movido para cá.
- A auditoria de linhas (2 linhas no máximo) cobre as legendas narrativas da Pioneer e as de Marte (63 por idioma); medida no motor de texto do UI Toolkit em 1280 px lógicos (caixa de 880 px). O 4:3 foi capturado, mas a contagem de linhas só foi medida em 16:9.
- Não foi exercitado em Play Mode nem no navegador, e a integração com `MissionHUD.BottomColumn` foi testada com um contêiner simples, não com o prefab do HUD.
