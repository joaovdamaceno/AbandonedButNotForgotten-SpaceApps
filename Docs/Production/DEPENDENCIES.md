# Dependências: visão de integração

O CSV `Data/dependencies.csv` e o backlog contêm todas as 204 arestas. As relações também estão nas issues nativas do GitHub. Este diagrama resume os portões para evitar um grafo ilegível de 84 caixas.

```mermaid
flowchart TD
    C1[CORE-001 · Projeto limpo] --> C2[CORE-002 · Contratos]
    C2 --> INPUT[CORE-003 · Input e hold]
    C2 --> SEQ[CORE-004 · Sequências]
    C2 --> FLOW[CORE-005 · Fluxo e evidências]
    C1 --> WEB[QA-001 · Web mínimo]
    ART[ART-001 · Assets e referências] --> M[Marte · R3 + R4]
    ART --> P[Pioneer · R5]
    ART --> S[Surveyor · R6]
    INPUT --> M
    INPUT --> P
    INPUT --> S
    SEQ --> M
    SEQ --> P
    SEQ --> S
    FLOW --> UI[Dossiê + HUD + legendas + tutorial]
    M --> INT[INT-001 · Integração dos três]
    P --> INT
    S --> INT
    UI --> INT
    INT --> PLAY[QA-003 · Jornadas]
    INT --> NOV[QA-004 · Novatos]
    INT --> PERF[QA-005 · Navegadores e GPU integrada]
    WEB --> PERF
    PLAY --> PKG[REL-001 · Pacotes]
    PERF --> PKG
    SCI[SCI-001 · Ciência] --> PKG
    VIS[ART-002 · Continuidade] --> PKG
    NOV --> HOST[REL-002 · itch.io real]
    PKG --> HOST
```

As setas não dizem que todo trabalho de arte depende de código pronto. Referências, dados, layout e peças isoladas começam antes. O ID de cada issue informa o bloqueio exato. Não iniciar implementações contra uma API inventada apenas porque a caixa deste resumo parece liberada.
