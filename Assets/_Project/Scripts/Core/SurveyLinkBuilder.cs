using System;

/// <summary>
/// Builds Google Forms URLs with optional participant-code prefill (entry.XXXX).
/// </summary>
public static class SurveyLinkBuilder
{
    public static string BuildPrefilledUrl(string baseUrl, string entryId, string participantCode)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "";

        string url = NormalizeFormBaseUrl(baseUrl);
        if (string.IsNullOrWhiteSpace(entryId) || string.IsNullOrWhiteSpace(participantCode))
            return url;

        string fieldKey = entryId.StartsWith("entry.", StringComparison.OrdinalIgnoreCase)
            ? entryId.Trim()
            : "entry." + entryId.Trim();
        string encodedCode = Uri.EscapeDataString(participantCode.Trim());
        return url + "?usp=pp_url&" + fieldKey + "=" + encodedCode;
    }

    /// <summary>Strips sharing/prefill query params so prefill is always applied cleanly.</summary>
    public static string NormalizeFormBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        string normalized = url.Trim();
        if (normalized.Contains("/preview", StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Replace("/preview", "/viewform", StringComparison.OrdinalIgnoreCase);

        int queryIndex = normalized.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
            normalized = normalized[..queryIndex];

        return normalized;
    }
}
