# PIO-001 — evidência de validação

Resumo **transcrito do corpo do PR #121** (base `develop` e1ea460, Unity 6000.6.7f1). Registrado depois da integração, para seguir a convenção `QA/<ID>/`; os resultados não foram reexecutados nesta data. Os limites e o que não foi feito estão no próprio PR.

| Verificação | Resultado |
| --- | --- |
| `python Tools/validate_pioneer_data.py` | Conforme: 8 estados, 375 s, 78 chaves por idioma, 45 cues, 23 fatos; nenhuma legenda acima de 22 caracteres/s |
| Defeitos plantados no validador | Acusou os quatro (chave sem par, duração errada, sobreposição, data UTC errada) e os dados foram restaurados |
| Suíte EditMode completa em batchmode (Unity fechada) | 490/490 (484 anteriores e 6 novos de `PioneerDataTests`) |
