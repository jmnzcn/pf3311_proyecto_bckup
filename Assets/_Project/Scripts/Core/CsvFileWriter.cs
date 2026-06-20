using System;
using System.IO;
using System.Text;
using System.Threading;

/// <summary>
/// Thread-safe CSV file append/write helpers shared by DataLogger and legacy Logs export.
/// </summary>
public static class CsvFileWriter
{
    static readonly object FileLock = new object();

    public const string PendingMarker = "[PENDIENTE]";
    public const string NoDataMarker = "[SIN DATOS]";

    public static void EnsureHeader(string path, string header)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            WriteAllText(path, header, new UTF8Encoding(true));
    }

    /// <summary>True when the file exists and has at least one non-empty row after the header.</summary>
    public static bool HasDataRows(string path)
    {
        if (!File.Exists(path))
            return false;

        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                return true;
        }

        return false;
    }

    /// <summary>True when at least one row after the header is not a [PENDIENTE] / [SIN DATOS] marker row.</summary>
    public static bool HasRealDataRows(string path)
    {
        if (!File.Exists(path))
            return false;

        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]) && !IsProvisionalLine(lines[i]))
                return true;
        }

        return false;
    }

    public static bool IsProvisionalLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        return line.Contains(PendingMarker, StringComparison.Ordinal) ||
               line.Contains(NoDataMarker, StringComparison.Ordinal);
    }

    public static bool HasNoDataPlaceholder(string path)
    {
        if (!File.Exists(path))
            return false;

        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]) &&
                lines[i].Contains(NoDataMarker, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>Rewrites the file keeping the header and only non-provisional data rows.</summary>
    public static void RemoveProvisionalRows(string path, string header)
    {
        ExecuteWithRetry(() =>
        {
            lock (FileLock)
            {
                if (!File.Exists(path))
                    return;

                string normalizedHeader = header.TrimEnd('\r', '\n');
                var kept = new System.Collections.Generic.List<string> { normalizedHeader };
                string[] lines = File.ReadAllLines(path);

                for (int i = 1; i < lines.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i]) && !IsProvisionalLine(lines[i]))
                        kept.Add(lines[i]);
                }

                File.WriteAllText(path, string.Join("\n", kept) + "\n", new UTF8Encoding(true));
            }
        });
    }

    public static void AppendLine(string path, string line, bool utf8Bom = true)
    {
        ExecuteWithRetry(() =>
        {
            lock (FileLock)
            {
                using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, utf8Bom ? new UTF8Encoding(true) : new UTF8Encoding(false)))
                {
                    writer.Write(line);
                }
            }
        });
    }

    public static void WriteAllText(string path, string text, Encoding encoding)
    {
        ExecuteWithRetry(() =>
        {
            lock (FileLock)
            {
                File.WriteAllText(path, text, encoding);
            }
        });
    }

    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
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
}
