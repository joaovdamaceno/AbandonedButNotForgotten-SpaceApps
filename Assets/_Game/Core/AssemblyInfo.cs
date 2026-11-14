using System.Runtime.CompilerServices;

// Testes de comportamento (CORE-001 e QA-002) podem verificar membros internos sem torná-los públicos.
[assembly: InternalsVisibleTo("Abandoned.SpaceApps.Core.Tests")]
[assembly: InternalsVisibleTo("Abandoned.SpaceApps.Tests.EditMode")]
[assembly: InternalsVisibleTo("Abandoned.SpaceApps.Tests.PlayMode")]
