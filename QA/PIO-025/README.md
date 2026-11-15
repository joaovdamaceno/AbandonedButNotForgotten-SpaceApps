# PIO-025 — Roteiro v2, fase 4, parte 5: os anos (cena 7)

Detalhes em `Assets/_Game/Chapters/Pioneer10/README.md` (seção "Roteiro v2, fase 4, parte 5").

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **216 de 216**. Novos (`PioneerYearsHudTests`): o ano corre de 1974 a 1998 sem voltar e para nos marcos reais (Saturno 1976, Urano 1979, Netuno 13 jun 1983, fim do acompanhamento 31 mar 1997, Voyager 1 1998), cada marco dentro da janela da legenda que fala dele; o medidor tem só as marcas reais 155 W e ~140 W e cai numa curva estilizada; os instrumentos apagam em turnos e o magnetômetro é o último; o estado do HUD tem 5 nomes, as chaves e a posição na linha do tempo; o capítulo tem o driver e o atlas só some no modo dos anos ligado pelo controlador |
| Quadros do HUD (`anos_*.png`, UI Toolkit com o tema da UI-001, fundo transparente) | `anos_saturno_t3.5`, `anos_netuno_t13` (com o aviso de que passar as órbitas não é sair do Sistema Solar), `anos_energia_t20`, `anos_voyager_t30`. Os PNGs têm fundo transparente: num visualizador claro as placas escuras aparecem sobre branco |
| Quadro 3D (`probe-3d-13s.png`, câmera e anchor reais, sem UI) | A sonda no centro do quadro sobre o campo de estrelas, livre das placas do HUD |
| `python Tools/validate_pioneer_data.py` e `export_pioneer_texts.py` | Conformes (141 chaves) |

**Verificação dos números (web, 15/11/2026, pendente de R7).** 155 W no lançamento e ~140 W em Júpiter aparecem em mais de uma fonte (Wikipedia e espelhos; algumas listam 165 W nominais). O "~65 W em 2001" do roteiro NÃO foi confirmado: a fonte que o cita o põe em 2005. As datas de desligamento do roteiro (1980, 1986, 1989, 1993, 1995) não foram confirmadas; as fontes dizem que a divisão de energia entre instrumentos começou em fevereiro de 1985 e que a ciência de rotina cessou em 30 de setembro de 1995. Por isso o painel NÃO mostra 65 W, a linha de ~100 W nem datas de desligamento por instrumento; os ícones apagam conforme a curva estilizada.

**Limites.** A curva de energia depois de Júpiter é estilizada (e o painel diz isso). Os ícones são formas geométricas, não ícones desenhados; não têm som de "clique grave". O ano em destaque vai de 1974 a 1998 (o roteiro vai a 2001). Falta o texto "[ano] — [nome] desligado" por 3 s, o plano com "câmera/detector desligado", "plano de dividir energia" e "computador de comandos desligado" (sem confirmação das datas). O contraste do HUD não foi medido. Não executado: Play Mode, teste humano, WebGL deste PR, suíte completa.
