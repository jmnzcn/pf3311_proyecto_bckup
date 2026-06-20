using System.Collections.Generic;
using System.Text;

/// <summary>
/// Assigns condition block order from participant number.
/// Slot = (participantNumber - 1) % 6; works for P01, P16, P20, P99, etc. without per-code tables.
/// The six base sequences match Protocolo investigador §3 (all permutations of A/B/C).
/// </summary>
public static class ParticipantConditionOrder
{
    public const int ConditionCount = 3;
    public const int OrderCount = 6;

    public const string LetterAccentHex = "#2DD4D4";
    public const string LabelMutedHex = "#9EBFC2";

    public static readonly string[] ConditionDisplayNames =
    {
        "Sin asistencia",
        "Agente de texto",
        "Agente virtual",
    };

    /// <summary>Maps protocol slot (0–5) to lexicographic permutation index.</summary>
    static readonly int[] ProtocolSlotToPermutationIndex = { 0, 3, 4, 1, 2, 5 };

    static readonly int[][] OrdersBySlot = BuildProtocolOrders();

    public static int GetOrderSlot(int participantNumber)
    {
        if (participantNumber < 1)
            return 0;

        return (participantNumber - 1) % OrderCount;
    }

    public static bool TryGetOrderForParticipant(string participantCode, out int[] scenarioIndices)
    {
        scenarioIndices = null;
        if (!ExperimentLogic.TryNormalizeParticipantCode(participantCode, out string normalized))
            return false;

        if (!TryGetParticipantNumber(normalized, out int number))
            return false;

        scenarioIndices = GetOrderForSlot(GetOrderSlot(number));
        return scenarioIndices != null && scenarioIndices.Length > 0;
    }

    public static int[] GetOrderForSlot(int slot)
    {
        slot = ((slot % OrderCount) + OrderCount) % OrderCount;
        return OrdersBySlot[slot];
    }

    public static int GetFirstBlockScenarioIndex(string participantCode)
    {
        if (!TryGetOrderForParticipant(participantCode, out int[] order) || order.Length == 0)
            return 0;

        return order[0];
    }

    public static string FormatOrderHint(string participantCode)
    {
        if (!TryGetOrderForParticipant(participantCode, out int[] order))
            return "";

        return "Tu orden: " + FormatOrderChain(order);
    }

    public static string FormatConditionLegendRichText()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < ConditionCount; i++)
        {
            if (i > 0)
                sb.Append("      ");

            sb.Append(FormatLetterRichText(i));
            sb.Append($"<size=20><color={LabelMutedHex}> {GetConditionDisplayName(i)}</color></size>");
        }

        return sb.ToString();
    }

    public static string FormatOrderHintRichText(string participantCode)
    {
        if (!TryGetOrderForParticipant(participantCode, out int[] order))
            return "";

        var orderLine = new StringBuilder("Tu orden: ");
        for (int i = 0; i < order.Length; i++)
        {
            if (i > 0)
                orderLine.Append("  →  ");

            orderLine.Append(FormatLetterRichText(order[i]));
        }

        return FormatConditionLegendRichText()
               + "\n"
               + orderLine
               + $"\n<size=22><color={LabelMutedHex}>Completá los tres bloques en este orden. Los que ya terminaste muestran COMPLETADO debajo de INICIAR.</color></size>"
               + $"\n<size=22><color={LabelMutedHex}>Solo podés pulsar el bloque que te toca ahora; práctica libre.</color></size>";
    }

    public static string FormatLetterRichText(int scenarioIndex)
    {
        return $"<color={LetterAccentHex}><b>{ConditionLetter(scenarioIndex)}</b></color>";
    }

    public static string GetConditionDisplayName(int scenarioIndex)
    {
        if (scenarioIndex < 0 || scenarioIndex >= ConditionDisplayNames.Length)
            return "";

        return ConditionDisplayNames[scenarioIndex];
    }

    public static string FormatOrderChain(int[] scenarioIndices)
    {
        if (scenarioIndices == null || scenarioIndices.Length == 0)
            return "";

        var parts = new string[scenarioIndices.Length];
        for (int i = 0; i < scenarioIndices.Length; i++)
            parts[i] = ConditionLetter(scenarioIndices[i]);

        return string.Join(" → ", parts);
    }

    public static string ConditionLetter(int scenarioIndex)
    {
        return scenarioIndex switch
        {
            0 => "A",
            1 => "B",
            2 => "C",
            _ => "?"
        };
    }

    static int[][] BuildProtocolOrders()
    {
        var permutations = GeneratePermutations(ConditionCount);
        var orders = new int[OrderCount][];

        for (int slot = 0; slot < OrderCount; slot++)
            orders[slot] = permutations[ProtocolSlotToPermutationIndex[slot]];

        return orders;
    }

    static List<int[]> GeneratePermutations(int length)
    {
        var results = new List<int[]>();
        var values = new int[length];
        for (int i = 0; i < length; i++)
            values[i] = i;

        Permute(values, 0, results);
        return results;
    }

    static void Permute(int[] values, int start, List<int[]> results)
    {
        if (start == values.Length - 1)
        {
            var copy = new int[values.Length];
            values.CopyTo(copy, 0);
            results.Add(copy);
            return;
        }

        for (int i = start; i < values.Length; i++)
        {
            (values[start], values[i]) = (values[i], values[start]);
            Permute(values, start + 1, results);
            (values[start], values[i]) = (values[i], values[start]);
        }
    }

    static bool TryGetParticipantNumber(string normalized, out int number)
    {
        number = 0;
        if (string.IsNullOrEmpty(normalized) || normalized[0] != 'P')
            return false;

        return int.TryParse(normalized.Substring(1), out number) && number >= 1;
    }
}
