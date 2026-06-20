using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Polls a deployed Apps Script endpoint that checks Google Form responses by participant code.
/// </summary>
public static class FormResponseVerifier
{
    const int MaxAttemptsPerPoll = 2;
    const float RetryDelaySeconds = 2f;

    [Serializable]
    sealed class VerifyResponse
    {
        public bool ok;
        public bool submitted;
        public string error;
    }

    public static IEnumerator PollUntilSubmittedCoroutine(
        string endpointUrl,
        string secret,
        string formKey,
        string participantCode,
        long sessionStartedUtcMs,
        float pollIntervalSeconds,
        float maxWaitSeconds,
        Action<bool, string> onComplete)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl) || string.IsNullOrWhiteSpace(secret))
        {
            onComplete?.Invoke(false, "Form verification not configured.");
            yield break;
        }

        float elapsed = 0f;
        string message = "";
        while (elapsed <= maxWaitSeconds)
        {
            bool done = false;
            bool submitted = false;
            message = "";

            yield return CheckOnceCoroutine(
                endpointUrl,
                secret,
                formKey,
                participantCode,
                sessionStartedUtcMs,
                (ok, isSubmitted, error) =>
                {
                    done = true;
                    submitted = ok && isSubmitted;
                    message = error ?? "";
                });

            while (!done)
                yield return null;

            if (submitted)
            {
                onComplete?.Invoke(true, "");
                yield break;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(1f, pollIntervalSeconds));
            elapsed += pollIntervalSeconds;
        }

        onComplete?.Invoke(false, string.IsNullOrWhiteSpace(message) ? "Timed out waiting for form submission." : message);
    }

    public static IEnumerator CheckOnceCoroutine(
        string endpointUrl,
        string secret,
        string formKey,
        string participantCode,
        long sessionStartedUtcMs,
        Action<bool, bool, string> onComplete)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl) || string.IsNullOrWhiteSpace(secret))
        {
            onComplete?.Invoke(false, false, "Form verification not configured.");
            yield break;
        }

        string url = BuildRequestUrl(endpointUrl, secret, formKey, participantCode, sessionStartedUtcMs);
        bool finished = false;
        bool requestOk = false;
        bool submitted = false;
        string error = "";

        for (int attempt = 0; attempt < MaxAttemptsPerPoll && !finished; attempt++)
        {
            if (attempt > 0)
                yield return new WaitForSecondsRealtime(RetryDelaySeconds);

            using var request = UnityWebRequest.Get(url);
            request.timeout = 20;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                error = request.error;
                continue;
            }

            try
            {
                var response = JsonUtility.FromJson<VerifyResponse>(request.downloadHandler.text);
                if (response == null)
                {
                    error = "Invalid verification response.";
                    continue;
                }

                if (!response.ok)
                {
                    error = string.IsNullOrWhiteSpace(response.error) ? "Verification failed." : response.error;
                    continue;
                }

                requestOk = true;
                submitted = response.submitted;
                finished = true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        onComplete?.Invoke(requestOk, submitted, error);
    }

    static string BuildRequestUrl(
        string endpointUrl,
        string secret,
        string formKey,
        string participantCode,
        long sessionStartedUtcMs)
    {
        string baseUrl = endpointUrl.Trim();
        char join = baseUrl.Contains("?") ? '&' : '?';
        return baseUrl
               + join
               + "secret=" + UnityWebRequest.EscapeURL(secret)
               + "&form=" + UnityWebRequest.EscapeURL(formKey ?? "")
               + "&participant=" + UnityWebRequest.EscapeURL(participantCode ?? "")
               + "&sinceMs=" + sessionStartedUtcMs.ToString(CultureInfo.InvariantCulture);
    }
}
