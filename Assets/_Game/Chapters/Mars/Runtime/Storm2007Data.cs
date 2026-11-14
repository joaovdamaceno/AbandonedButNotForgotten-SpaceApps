using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// MAR-007: parâmetros da tempestade de 2007 (M21). Os dois únicos números científicos mostrados são marcos PUBLICADOS
    /// (MS17: mais de 700 Wh/sol antes da tempestade e o mínimo reportado de 128 Wh/sol em julho de 2007); eles são validados
    /// aqui para ninguém inventar outro valor. Geração solar em Wh/sol não é potência, nem carga de bateria, nem curva completa.
    /// Os valores do ponteiro (0–1) são puramente esquemáticos. Nenhuma frase de jogo: só chaves.
    /// </summary>
    [CreateAssetMenu(fileName = "Storm2007", menuName = "Abandoned/Marte/Storm 2007")]
    public sealed class Storm2007Data : ScriptableObject
    {
        public const int PublishedBeforeWhPerSol = 700;
        public const int PublishedLowWhPerSol = 128;

        [Header("Tempo ATIVO, em segundos (roteiro M21: 6 + 8 + 6 + 5 = 25)")]
        [Min(0f), Tooltip("0–6 s: o horizonte apaga. Sem E nesta fase.")] public float dimSeconds = 6f;
        [Min(0.1f), Tooltip("6–14 s: E sustentado desliga os grupos não essenciais.")] public float conserveHoldSeconds = 8f;
        [Min(0f), Tooltip("14–20 s: elipse de semanas, comunicações reduzidas.")] public float weeksSeconds = 6f;
        [Min(0f), Tooltip("20–25 s: o céu clareia e a máquina retoma.")] public float clearSeconds = 5f;

        [Header("Poeira (0 = céu limpo, 1 = escuro total) · só visual; quem faz a arte lê DustLevel")]
        [Range(0f, 1f)] public float dustPeak = 0.85f;
        [Range(0f, 1f), Tooltip("Resíduo de névoa depois que o céu clareia.")] public float dustAfterClearing = 0.1f;

        [Header("Ponteiro do medidor (0–1) · ESQUEMÁTICO: não é uma curva nem tem valores intermediários")]
        [Range(0f, 1f)] public float needleBefore = 1f;
        [Range(0f, 1f)] public float needleLow = 0.18f;
        [Range(0f, 1f)] public float needleRecovered = 0.55f;

        [Header("Grupos desligados pelo E (fração do trabalho em que cada um cai)")]
        [Range(0.05f, 1f)] public float wheelsOffAt = 0.34f;
        [Range(0.05f, 1f)] public float armOffAt = 0.67f;
        [Range(0.05f, 1f)] public float instrumentsOffAt = 1f;

        [Header("Valores publicados (MS17) · não alterar sem nova fonte")]
        public int beforeWhPerSol = PublishedBeforeWhPerSol;
        public int lowWhPerSol = PublishedLowWhPerSol;

        [Header("Círculo E (verbo + objeto)")]
        public string conserveVerbKey = "MAR.VERB.conserve";
        public string conserveObjectKey = "MAR.OBJ.machine";

        [Header("Textos do medidor e dos grupos (chaves)")]
        public string meterTitleKey = "MAR.POWER";
        public string meterBeforeKey = "MAR.STORM.gen.before";
        public string meterLowKey = "MAR.STORM.gen.low";
        public string meterRecoveringKey = "MAR.STORM.gen.recovering";
        public string meterNoteKey = "MAR.STORM.gen.note";
        public string commsReducedKey = "MAR.COMMS";
        public string wheelsKey = "MAR.STORM.group.wheels";
        public string armKey = "MAR.STORM.group.arm";
        public string instrumentsKey = "MAR.STORM.group.instruments";

        public float TotalSeconds => dimSeconds + conserveHoldSeconds + weeksSeconds + clearSeconds;

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"Storm2007Data '{name}'";
            ContractValidation.RequireNonNegative(errors, owner, nameof(dimSeconds), dimSeconds);
            ContractValidation.RequirePositive(errors, owner, nameof(conserveHoldSeconds), conserveHoldSeconds);
            ContractValidation.RequireNonNegative(errors, owner, nameof(weeksSeconds), weeksSeconds);
            ContractValidation.RequireNonNegative(errors, owner, nameof(clearSeconds), clearSeconds);
            if (Mathf.Abs(TotalSeconds - 25f) > 0.01f)
                ContractValidation.Report(errors, owner, $"M21 soma {TotalSeconds} s; o roteiro pede 25 s (6 + 8 + 6 + 5).");

            if (beforeWhPerSol != PublishedBeforeWhPerSol || lowWhPerSol != PublishedLowWhPerSol)
                ContractValidation.Report(errors, owner, $"só os marcos publicados valem: >{PublishedBeforeWhPerSol} e {PublishedLowWhPerSol} Wh/sol (MS17).");

            if (!(wheelsOffAt < armOffAt && armOffAt < instrumentsOffAt) || !Mathf.Approximately(instrumentsOffAt, 1f))
                ContractValidation.Report(errors, owner, "os grupos devem cair em ordem (rodas, braço, instrumentos) e o último só ao completar o E.");

            if (dustAfterClearing >= dustPeak)
                ContractValidation.Report(errors, owner, "o céu precisa clarear: a poeira final deve ser menor que o pico.");

            if (!(needleLow < needleRecovered && needleRecovered < needleBefore))
                ContractValidation.Report(errors, owner, "ponteiro: baixo < recuperado < antes (a recuperação é parcial e cautelosa).");

            foreach (var (field, value) in new[]
            {
                (nameof(conserveVerbKey), conserveVerbKey), (nameof(conserveObjectKey), conserveObjectKey), (nameof(meterTitleKey), meterTitleKey),
                (nameof(meterBeforeKey), meterBeforeKey), (nameof(meterLowKey), meterLowKey), (nameof(meterRecoveringKey), meterRecoveringKey),
                (nameof(meterNoteKey), meterNoteKey), (nameof(commsReducedKey), commsReducedKey), (nameof(wheelsKey), wheelsKey),
                (nameof(armKey), armKey), (nameof(instrumentsKey), instrumentsKey),
            })
            {
                ContractValidation.RequireId(errors, owner, field, value);
            }

            return errors;
        }
    }
}
