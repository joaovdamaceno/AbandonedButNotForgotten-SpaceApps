# Revisão e integração — 14/11/2026

Por autorização explícita do proprietário, o assistente revisa, valida e integra sem aguardar aprovação de outra pessoa. Não há aprovação humana independente simulada.

- Incorporada `develop` em `b337cd1` (MAR-001 já integrada), sem conflitos nem alterações no capítulo de Marte.
- Corrigido o pivô de `Scoop_Arm_1` para o lado positivo de Z do modelo importado. A restauração da pose canônica antes do parenting evita acumular deslocamentos em reconstruções.
- O teste de reconstrução confirma que a geometria do braço permanece na mesma posição (tolerância <1 mm), que o pivô está no lado correto e que os IDs persistem.
- **143 testes EditMode passaram, zero falhas/ignorados**, Unity 6000.6.7f1, Windows/D3D11, saída 0. Evidência: `integration-editmode.xml`.
- Capturas anteriores continuam documentando a pose de repouso: a correção muda o ponto de articulação e conserva a posição da malha. Não foi feita nova avaliação humana ou medição Web.

Sem outra falha impeditiva encontrada na revisão técnica desta entrega. Continuam válidos os limites de `README.md`: cena/rig, sem capítulo completo, build Web ou garantia de FPS.
