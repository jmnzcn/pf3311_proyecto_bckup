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

        string url = baseUrl.Trim();
        if (url.Contains("/preview", StringComparison.OrdinalIgnoreCase))
            url = url.Replace("/preview", "/viewform", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(entryId) || string.IsNullOrWhiteSpace(participantCode))
            return url;

        string fieldKey = entryId.StartsWith("entry.", StringComparison.OrdinalIgnoreCase)
            ? entryId.Trim()
            : "entry." + entryId.Trim();
        string query = "usp=pp_url&" + fieldKey + "=" + Uri.EscapeDataString(participantCode.Trim());
        return url.Contains("?") ? url + "&" + query : url + "?" + query;
    }
}
