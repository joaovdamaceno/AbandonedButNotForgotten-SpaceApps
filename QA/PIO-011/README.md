# PIO-011 — evidência de validação

Executado em 14/11/2026 sobre `feature/PIO-011-escuta-dsn` (base `develop` 5bcddaa), Unity **6000.6.7f1** / URP 17.6, Windows, Editor aberto (Unity MCP, EditMode). Só as classes de teste da Pioneer, como combinado; a suíte completa **não** foi executada.

| Verificação | Resultado |
| --- | --- |
| `PioneerListeningTests` (novos) | **16/16 passaram** (3 falharam na primeira execução e foram corrigidos: a varredura assistida continuava marcada ao entrar na zona, um erro do teste na busca da silhueta e um termo do texto em inglês) |
| 11 classes de teste da Pioneer (Belt, Data, HistoricalImages, Imaging, Launch, Listening, Narration, Outbound, Probe, Recovery, Stage) | **128/128 passaram**. `PioneerNarrationTests` passou a esperar 105 chaves (3 novas) |
| `python Tools/validate_pioneer_data.py` | Dados conformes (105 chaves por idioma, 45 cues) |
| Builder `pioneer10.stage.listening` reexecutado (junto de `stage`) | 0 mudanças |
| Capturas (`PioneerListeningCapture`) | 20 PNGs em `Docs/Art/Pioneer10_Listening/` (10 estados × 1280×720 e 1024×768) e `00_anchor_antena_1280x720.png` (ponto de vista da antena com a silhueta, renderizado pela câmera do anchor), conferidas visualmente |

Cobertura dos testes: sequência de 52 s (planos 7/9/9/12/8/7) e legendas iguais às dos dados da PIO-001, com os três gatilhos e as leituras mínimas de 5/5/7 s; silhueta no `DishSocket`; zonas de 30%, deslocadas, com o botão a no máximo 15% da borda e o terceiro sinal mais fraco porém legível; setas movem o botão e a proximidade aparece antes de qualquer escuta; **primeiro contato só com as setas e depois E (teclado, sem som)**; varredura assistida que pára na primeira zona válida; três contatos com resultados históricos distintos e a quarta tentativa sem zona e sem resposta; quatro rótulos, quatro cartões e quatro formas de onda diferentes; **E mantido depois de cada contato não confirma o seguinte**; soltar ou sair da zona preserva o progresso; cada marco espera a etapa anterior sem avanço por inatividade; gatilhos que abrem as legendas dos contatos; respiração com controles recolhidos, onda mínima e **2,5 s de silêncio real** que a pausa congela; cancelar em qualquer ponto limpa tudo e repetir começa do zero; pular conclui uma vez sem deixar entrada nem marco esperando; textos nos dois idiomas sem culpar quem escuta; UXML sem frases literais e nenhuma foto ou modelo de terceiros na pasta da silhueta.

**Não executado:** mouse (só teclado), Play Mode, build WebGL aberta no navegador, profiler Web, FPS real, medição de contraste nos pixels, áudio e legendas reais (AUD-002/UI-004 não existem). **Pendente:** escolher e creditar uma **fotografia** NASA/JPL da antena (a referência da auditoria é um modelo 3D sem licença própria na página, que não foi baixado); revisão da R7 (3 textos `derived`, datas e leitura científica em PT/EN).
