using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Persists participant responses to CSV files for offline analysis.
/// </summary>
public class DataLogger : MonoBehaviour
{
    static readonly object FileLock = new object();

    private float lastQuestionTime;
    private string currentFilePath;
    void Awake()
    {
        ResetQuestionTimer();
    }

    /// <summary>Starts the per-question timer (call when a scenario question is shown).</summary>
    public void ResetQuestionTimer()
    {
        lastQuestionTime = Time.time;
    }

    public bool SaveAnswer(
        int scenarioNumber,
        string scenarioName,
        int questionNumber,
        string answerLetter,
        string answerText,
        int confidence,
        string correctAnswerLetter,
        string correctAnswerText)
    {
        try
        {
            string userId = "Unknown";

            ExperimentLogic expLogic = UnityEngine.Object.FindFirstObjectByType<ExperimentLogic>(FindObjectsInactive.Include);
            if (expLogic != null)
                userId = expLogic.GetSessionUserId();

            if (string.IsNullOrEmpty(userId)) userId = "Unknown";

            string fileName = $"ExperimentData_{userId}.csv";

            string directory = Path.Combine(Application.dataPath, "..", "CSV data");
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            currentFilePath = Path.Combine(directory, fileName);

            if (!File.Exists(currentFilePath))
            {
                WriteCsvText(currentFilePath,
                    "UserID,ScenarioNumber,ScenarioName,QuestionNumber,AnswerLetter,Answer,CorrectAnswerLetter,CorrectAnswer,Confidence,TimeSpent(Seconds),Timestamp\n",
                    new UTF8Encoding(true));
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            float timeSpent = Time.time - lastQuestionTime;
            lastQuestionTime = Time.time;

            var line = string.Join(",",
                userId,
                scenarioNumber.ToString(),
                EscapeCsv(scenarioName),
                questionNumber.ToString(),
                EscapeCsv(answerLetter),
                EscapeCsv(answerText),
                EscapeCsv(correctAnswerLetter),
                EscapeCsv(correctAnswerText),
                confidence.ToString(),
                timeSpent.ToString("F2"),
                EscapeCsv(timestamp)) + "\n";

            AppendCsvLine(currentFilePath, line, new UTF8Encoding(true));

#if UNITY_EDITOR
            Debug.Log($"<color=cyan>DATA SAVED TO CSV:</color> {currentFilePath}");
#endif
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DataLogger.SaveAnswer() failed safely: {ex.Message}\nStack trace: {ex.StackTrace}");
            return false;
        }
    }

    static void AppendCsvLine(string path, string line, Encoding encoding)
    {
        ExecuteWithRetry(() =>
        {
            lock (FileLock)
            {
                using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, encoding))
                {
                    writer.Write(line);
                }
            }
        });
    }

    static void WriteCsvText(string path, string text, Encoding encoding)
    {
        ExecuteWithRetry(() =>
        {
            lock (FileLock)
            {
                File.WriteAllText(path, text, encoding);
            }
        });
    }

    static void ExecuteWithRetry(Action action)
    {
        const int maxAttempts = 5;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(40 * attempt);
            }
        }

        action();
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    public void DeleteIncompleteFile()
    {
        try
        {
            if (!string.IsNullOrEmpty(currentFilePath) && File.Exists(currentFilePath))
            {
                File.Delete(currentFilePath);
#if UNITY_EDITOR
                Debug.Log("Incomplete session file deleted.");
#endif
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DataLogger.DeleteIncompleteFile() failed safely: {ex.Message}");
        }
    }
}
