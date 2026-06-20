using UnityEngine;

/// <summary>
/// Centralizes TTS outcome reporting to <see cref="ExperimentLogic"/> and CSV persistence.
/// </summary>
public static class TtsOutcomeReporter
{
    public static void Report(TtsExchangeContext context, bool success, string failureReason)
    {
        if (!context.IsValid)
            return;

        var experiment = Object.FindFirstObjectByType<ExperimentLogic>(FindObjectsInactive.Include);
        if (experiment == null)
            return;

        if (!experiment.RecordTtsOutcome(context, success, failureReason ?? ""))
            experiment.NotifyDataSaveFailure(chatContext: true);
    }
}
