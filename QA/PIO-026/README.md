# PIO-026 — Roteiro v2, fase 4, parte 6: as seis primeiras vezes (cena 9)

Detalhes em `Assets/_Game/Chapters/Pioneer10/README.md` (seção "Roteiro v2, fase 4, parte 6").

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **219 de 219**. Novos (`PioneerFirstsHudTests`): seis estrelas e seis chaves distintas; visíveis de 20 a 30 s (o plano `PIO08.Receding`); cada uma apagada antes da hora e cheia 0,5 s depois, uma a cada 1,6 s e em ordem; a sexta acesa antes do memorial; as estrelas numa elipse ao redor da sonda, fora do centro e afastadas entre si; o capítulo tem o driver e o plano de afastamento começa em 20 s e acaba em 30 s |
| Quadros do HUD (`estrelas_*.png`, UI Toolkit, fundo transparente) | `estrelas_t22` (as duas primeiras acesas) e `estrelas_t29` (as seis, com os rótulos em duas linhas, à esquerda e à direita). Os PNGs têm fundo transparente: num visualizador claro as placas escuras aparecem sobre branco |
| Quadro 3D (`probe-3d-24s.png`, câmera e anchor reais, sem UI) | A sonda pequena no centro, sobre o campo de estrelas |
| `python Tools/validate_pioneer_data.py` e `export_pioneer_texts.py` | Conformes (147 chaves) |

**Fontes dos seis "primeiros" (leitura da página da NASA dos 50 anos do lançamento, 15/11/2026; pendente de R7).** A página diz "the first spacecraft designed to travel beyond the orbit of Mars", "the first crossing of the Asteroid Belt", "the first spacecraft sent on a trajectory to leave the solar system" e que, ao passar a órbita de Netuno em 13/6/1983, "became the first human-made object to leave the outer planets behind". "Primeira missão aos planetas exteriores" e "primeira a sobrevoar Júpiter" já estão no registro de fontes do projeto. A página NÃO diz "primeira a usar energia totalmente nuclear" (só que usava RTGs): esse item do roteiro ficou de fora. "Primeira rumo ao espaço interestelar" do roteiro foi reescrito para "em rota para sair do Sistema Solar", como a NASA, e "além de Netuno" para "além das órbitas dos planetas · 1983" (cruzar a órbita não é sair do Sistema Solar).

**Limites.** As estrelas são discos luminosos simples, não estrelas desenhadas; os rótulos fazem o papel das seis legendas curtas (não são cues de legenda). Falta o plano final em que a placa reflete a luz de Aldebaran e a sonda vira um ponto entre estrelas coloridas, e a constelação de Touro. Contraste do HUD não medido. Não executado: Play Mode, teste humano, WebGL deste PR, suíte completa.
