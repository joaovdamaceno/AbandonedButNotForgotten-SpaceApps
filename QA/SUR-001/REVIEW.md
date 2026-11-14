# Revisão técnica antes do merge — 14/11/2026

Revisão executada pelo assistente, a pedido do proprietário do repositório, que autorizou revisar e integrar o PR #94. Não é uma aprovação humana independente nem uma revisão R7 simulada.

- Escopo revisado: tipo serializado, importador, manifesto, asset gerado, 31 vínculos de roteiro, inventário, testes e origem.
- Corrigido: coleção de fontes nula podia causar `NullReferenceException` no validador visual. O validador agora devolve erros claros; novo teste cobre coleção e catálogo ausentes.
- Regressão completa EditMode: **120 passaram, 0 falharam, 0 ignorados**, Unity 6000.6.7f1, Windows, batchmode/nographics, saída 0. Evidência: `review-editmode.xml` (17 testes Surveyor e 103 de infraestrutura).
- Nenhuma outra falha impeditiva encontrada nesta revisão. Sem mudanças em contratos Core, cenas globais, pacotes ou configurações gráficas.
- Os números controversos continuam bloqueados. Para o capítulo usar minuto/região, quatro trincheiras e resistência qualitativa; nenhuma prova de água, 98 kPa, segundos/coordenadas híbridos ou câmera operacional em 1969.
- Fontes primárias e referências visuais selecionadas foram consultadas na preparação da SUR-001; a revisão confirma o tratamento conservador, não resolve por suposição as divergências documentadas.

O proprietário dispensou a espera por revisão de outra pessoa ao autorizar este merge. `crossReviewStatus: pending-R7` é preservado por honestidade: não houve revisão humana R7. Isso não libera os fatos `SUR.C.*`; precisão opcional exige reconciliação posterior.

Resultado: apto à integração como catálogo de dados com as restrições acima. Não constitui aprovação visual, de desempenho Web ou do capítulo jogável completo.
