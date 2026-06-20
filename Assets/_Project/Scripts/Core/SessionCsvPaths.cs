using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Paths and file names for per-session CSV output under CSV data/.
/// </summary>
public static class SessionCsvPaths
{
    public const string ConsentFormVersion = "PF3311-v1";

    public static class Files
    {
        public const string ExperimentData = "ExperimentData.csv";
        public const string ConsentLog = "ConsentLog.csv";
        public const string ChatLog = "ChatLog.csv";
        public const string ChatHelpRating = "ChatHelpRating.csv";
        public const string ChatQuestionSummary = "ChatQuestionSummary.csv";
        public const string ChatScenarioSummary = "ChatScenarioSummary.csv";
        public const string TtsLog = "TtsLog.csv";
        public const string ChatApiEvent = "ChatApiEvent.csv";
    }

    public static string RootDirectory =>
        Path.Combine(Application.dataPath, "..", "CSV data");

    public static string BuildSessionFolderName(string participantCode, string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || sessionId == "Unknown")
            sessionId = "UnknownSession";

        if (!string.IsNullOrEmpty(participantCode) && participantCode != "Unknown")
            return participantCode + "_" + sessionId;

        return sessionId;
    }

    public static string BuildSessionDirectory(string participantCode, string sessionId)
    {
        if (!Directory.Exists(RootDirectory))
            Directory.CreateDirectory(RootDirectory);

        string directory = Path.Combine(RootDirectory, BuildSessionFolderName(participantCode, sessionId));
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        return directory;
    }

    public static string BuildSessionFilePath(string participantCode, string sessionId, string fileName) =>
        Path.Combine(BuildSessionDirectory(participantCode, sessionId), fileName);

    public static string BuildLegacyLogsFileName(string participantCode, string sessionId)
    {
        if (!string.IsNullOrEmpty(participantCode) && participantCode != "Unknown")
            return "ExperimentData_" + participantCode + "_" + sessionId + ".csv";

        return "ExperimentData_" + sessionId + ".csv";
    }

    public static IEnumerable<string> LegacyFlatFileNames(string participantCode, string sessionId)
    {
        string[] stems =
        {
            "ExperimentData", "ConsentLog", "ChatLog", "ChatHelpRating",
            "ChatQuestionSummary", "ChatScenarioSummary", "TtsLog", "ChatApiEvent",
        };

        foreach (string stem in stems)
        {
            if (!string.IsNullOrEmpty(participantCode) && participantCode != "Unknown")
                yield return stem + "_" + participantCode + "_" + sessionId + ".csv";

            yield return stem + "_" + sessionId + ".csv";
        }
    }

    public static void DeleteSessionDirectory(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;

        Directory.Delete(directory, true);
    }

    public static void DeleteLegacyFlatFiles(string participantCode, string sessionId)
    {
        foreach (string fileName in LegacyFlatFileNames(participantCode, sessionId))
        {
            string path = Path.Combine(RootDirectory, fileName);
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    public static string LegacyLogsDirectory =>
        Path.Combine(Application.dataPath, "..", "Logs");

    public static void DeleteLegacyLogsExportFile(string participantCode, string sessionId)
    {
        string path = Path.Combine(LegacyLogsDirectory, BuildLegacyLogsFileName(participantCode, sessionId));
        if (File.Exists(path))
            File.Delete(path);
    }
}
