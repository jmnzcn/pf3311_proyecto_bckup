using UnityEngine;

/// <summary>
/// Entry point for spoken agent replies; delegates synthesis to AzureLipSync when present.
/// </summary>
public class AgentSpeechController : MonoBehaviour
{
    AzureLipSync azureLipSync;

    void Awake()
    {
        azureLipSync = ResolveAzureLipSync();
    }

    public void Speak(string text, TtsExchangeContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        azureLipSync = ResolveAzureLipSync();
        if (azureLipSync != null)
        {
            azureLipSync.SpeakText(text, context);
            return;
        }

        Debug.LogError("Could not find AzureLipSync anywhere in the scene!");
        TtsOutcomeReporter.Report(context, false, "AzureLipSync_not_found");
    }

    public void CancelSpeech()
    {
        azureLipSync = ResolveAzureLipSync();
        if (azureLipSync != null)
            azureLipSync.CancelPendingSpeech();
    }

    AzureLipSync ResolveAzureLipSync()
    {
        if (azureLipSync != null)
            return azureLipSync;

        azureLipSync = GetComponent<AzureLipSync>();
        return azureLipSync != null ? azureLipSync : Object.FindFirstObjectByType<AzureLipSync>();
    }
}
