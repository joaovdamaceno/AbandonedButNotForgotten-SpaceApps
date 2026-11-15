# PIO-024 — Roteiro v2, fase 4, parte 4: luas em 3D, radiação e trajetória (cena 5)

Detalhes em `Assets/_Game/Chapters/Pioneer10/README.md` (seção "Roteiro v2, fase 4, parte 4").

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **210 de 210**. Novos (`PioneerClosestCinemaTests`): medidor de radiação (visível de 6 a 17 s, só sobe, estática de 0,05 a 0,4 e tremor determinístico), barras de distância (24–29 s, a das nuvens cresce depois da da Lua e vale ~1/3), luas e trajetória só nos seus anchors, cada lua no centro do próprio plano e raios na razão real, a curva de ~38° passando a 95–135 m do centro de Júpiter e visível no plano alto, sequência de 40 s com os três planos de lua primeiro |
| Quadros 3D (`dev-frames`, câmera e anchors reais, sem UI) | `moon-ganymede-0.5s.png`, `moon-europa-4.5s.png`, `bend-trajectory-30s.png` |
| Quadros do HUD (`hud/*.png`, UI Toolkit com o tema da UI-001, fundo transparente) | `radiacao_08s`, `radiacao_14s` (placa com a barra e a estática), `distancias_27s` (velocidade e as duas barras) |
| `python Tools/validate_pioneer_data.py` e `export_pioneer_texts.py` | Conformes (131 chaves) |

**Limites.** Superfícies das luas procedurais e estilizadas (não são mapas reais). A trajetória é uma linha de composição (curva de 38° em volta de Júpiter), não um cálculo orbital. O medidor de radiação sobe numa curva estilizada; a velocidade é o texto fixo "Mais de 120.000 km/h" do ponto mais próximo, não um velocímetro subindo. **Não feito** (do roteiro, cena 5): fotos reais das luas com crossfade, o plano de Io com o "X", comandos falsos piscando num painel, o mergulho rente às nuvens com paralaxe forte, linhas de campo gravitacional pulsando, a seta do movimento de Júpiter ao redor do Sol e o velocímetro subindo. A estática foi vista em quadros isolados (não em movimento) e não foi medido o contraste do HUD. Não executado: Play Mode, teste humano, WebGL deste PR, suíte completa.
