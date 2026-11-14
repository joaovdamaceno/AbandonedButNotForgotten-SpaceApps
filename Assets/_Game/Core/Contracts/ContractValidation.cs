using System.Collections.Generic;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Regras comuns de validação dos contratos (CORE-002). Toda validação devolve mensagens legíveis em vez de
    /// lançar NullReference: builders, testes e o menu do Editor mostram a lista antes da execução.
    /// </summary>
    public static class ContractValidation
    {
        public const int MaxIdLength = 64;
        public const float TimeToleranceSeconds = 0.001f;

        /// <summary>
        /// ID estável: começa com letra; depois letras ASCII, dígitos, '.', '_' ou '-'; até 64 caracteres, sem espaço.
        /// Vale para IDs de capítulo, etapa, evidência, plano, cue e chaves de localização (ex.: "MAR.C12").
        /// </summary>
        public static bool IsValidId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaxIdLength || !IsAsciiLetter(value[0]))
                return false;

            for (var i = 1; i < value.Length; i++)
            {
                var c = value[i];
                if (!IsAsciiLetter(c) && !(c >= '0' && c <= '9') && c != '.' && c != '_' && c != '-')
                    return false;
            }

            return true;
        }

        public static void Report(ICollection<string> errors, string owner, string message) =>
            errors.Add($"{owner}: {message}");

        public static void RequireId(ICollection<string> errors, string owner, string field, string value)
        {
            if (!IsValidId(value))
            {
                Report(errors, owner,
                    $"'{field}' vazio ou inválido ('{value ?? "<nulo>"}'). Use letra inicial e só letras, dígitos, '.', '_' ou '-' (máx. {MaxIdLength}).");
            }
        }

        public static void RequireText(ICollection<string> errors, string owner, string field, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                Report(errors, owner, $"'{field}' é obrigatório.");
        }

        public static void RequirePositive(ICollection<string> errors, string owner, string field, float value)
        {
            if (!(value > 0f) || float.IsInfinity(value))
                Report(errors, owner, $"'{field}' deve ser maior que zero (valor {value}).");
        }

        public static void RequireNonNegative(ICollection<string> errors, string owner, string field, float value)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
                Report(errors, owner, $"'{field}' não pode ser negativo (valor {value}).");
        }

        /// <summary>Texto único para log ou exceção: uma divergência por linha.</summary>
        public static string Describe(IReadOnlyCollection<string> errors) =>
            errors.Count == 0 ? "sem divergências" : "\n- " + string.Join("\n- ", errors);

        static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }
}
