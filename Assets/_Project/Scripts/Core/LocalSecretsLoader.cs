using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads API keys from _config/LocalSecrets.json (gitignored). Never commit that file.
/// </summary>
public static class LocalSecretsLoader
{
    [Serializable]
    public class Payload
    {
        public string geminiApiKey;
        public string azureSpeechKey;
        public string azureSpeechRegion;
        public string csvDriveUploadSecret;
    }

    public static Payload Load()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "_config", "LocalSecrets.json"));
        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<Payload>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("No se pudo leer _config/LocalSecrets.json: " + ex.Message);
            return null;
        }
    }

    public static bool IsPlaceholder(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        value = value.Trim();
        return value.StartsWith("PEGAR_", StringComparison.OrdinalIgnoreCase)
               || value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
               || value.Contains("AQUI", StringComparison.OrdinalIgnoreCase);
    }
}
