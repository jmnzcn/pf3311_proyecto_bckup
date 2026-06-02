using UnityEngine;

/// <summary>
/// Entry point for spoken agent replies; delegates synthesis to AzureLipSync when present.
/// </summary>
public class AgentSpeechController : MonoBehaviour
{
    AzureLipSync azureLipSync;

    void Awake()
    {
        azureLipSync = GetComponent<AzureLipSync>();
        if (azureLipSync == null)
            azureLipSync = Object.FindFirstObjectByType<AzureLipSync>();
    }

    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (azureLipSync == null)
            azureLipSync = Object.FindFirstObjectByType<AzureLipSync>();

        if (azureLipSync != null)
        {
            azureLipSync.SpeakText(text);
        }
        else
        {
            Debug.LogError("Could not find AzureLipSync anywhere in the scene!");
        }
    }
}
