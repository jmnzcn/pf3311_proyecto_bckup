using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Uploads the local session CSV folder to Google Drive via a deployed Apps Script web app.
/// Drive layout: {parentFolder}/CSVs/{ParticipantCode}_{SessionID}/*.csv
/// </summary>
public static class SessionCsvDriveUpload
{
    const int MaxAttempts = 2;
    const float RetryDelaySeconds = 4f;
    const int MaxTotalBytes = 8 * 1024 * 1024;

    [Serializable]
    sealed class UploadPayload
    {
        public string secret;
        public string sessionFolderName;
        public UploadFileEntry[] files;
    }

    [Serializable]
    sealed class UploadFileEntry
    {
        public string name;
        public string contentBase64;
    }

    [Serializable]
    sealed class UploadResponse
    {
        public bool ok;
        public string error;
        public int fileCount;
    }

    public static IEnumerator UploadCoroutine(
        string endpointUrl,
        string uploadSecret,
        string participantCode,
        string sessionId,
        Action<bool, string> onComplete)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl) || string.IsNullOrWhiteSpace(uploadSecret))
        {
            onComplete?.Invoke(false, "Drive upload not configured.");
            yield break;
        }

        string sessionFolderName = SessionCsvPaths.BuildSessionFolderName(participantCode, sessionId);
        string sessionDirectory = SessionCsvPaths.BuildSessionDirectory(participantCode, sessionId);
        if (!Directory.Exists(sessionDirectory))
        {
            onComplete?.Invoke(false, "Session CSV folder not found.");
            yield break;
        }

        string[] csvPaths = Directory.GetFiles(sessionDirectory, "*.csv");
        if (csvPaths.Length == 0)
        {
            onComplete?.Invoke(false, "No CSV files to upload.");
            yield break;
        }

        var files = new UploadFileEntry[csvPaths.Length];
        int totalBytes = 0;

        for (int i = 0; i < csvPaths.Length; i++)
        {
            string path = csvPaths[i];
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(false, "Could not read " + Path.GetFileName(path) + ": " + ex.Message);
                yield break;
            }

            totalBytes += bytes.Length;
            if (totalBytes > MaxTotalBytes)
            {
                onComplete?.Invoke(false, "Session CSV payload exceeds upload limit.");
                yield break;
            }

            files[i] = new UploadFileEntry
            {
                name = Path.GetFileName(path),
                contentBase64 = Convert.ToBase64String(bytes)
            };
        }

        var payload = new UploadPayload
        {
            secret = uploadSecret.Trim(),
            sessionFolderName = sessionFolderName,
            files = files
        };

        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);
        string lastError = "Unknown upload error.";

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using (UnityWebRequest request = new UnityWebRequest(endpointUrl.Trim(), "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 120;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler?.text ?? "";
                    UploadResponse response = null;
                    try
                    {
                        response = JsonUtility.FromJson<UploadResponse>(responseText);
                    }
                    catch
                    {
                        // Apps Script may return plain text on some failures.
                    }

                    if (response != null && response.ok)
                    {
                        onComplete?.Invoke(true, sessionFolderName);
                        yield break;
                    }

                    lastError = response != null && !string.IsNullOrWhiteSpace(response.error)
                        ? response.error
                        : string.IsNullOrWhiteSpace(responseText) ? "Empty Drive upload response." : responseText;
                }
                else
                {
                    lastError = request.error ?? request.result.ToString();
                }
            }

            if (attempt < MaxAttempts)
                yield return new WaitForSecondsRealtime(RetryDelaySeconds);
        }

        onComplete?.Invoke(false, lastError);
    }
}
