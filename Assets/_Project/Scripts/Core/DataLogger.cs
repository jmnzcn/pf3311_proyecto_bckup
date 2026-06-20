using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Persists participant responses and chat analytics to CSV files for offline analysis.
/// Each session writes to CSV data/{ParticipantCode}_{SessionID}/.
/// </summary>
public class DataLogger : MonoBehaviour
{
    float lastQuestionTime;

    void Awake()
    {
        ResetQuestionTimer();
    }

    public void ResetQuestionTimer()
    {
        lastQuestionTime = Time.time;
    }

    public bool SaveConsentRecord(
        string participantCode,
        string sessionId,
        bool ageConsent,
        bool studyConsent,
        string consentFormVersion)
    {
        return TryAppendSessionCsv(
            participantCode,
            sessionId,
            SessionCsvPaths.Files.ConsentLog,
            "ParticipantCode,SessionID,AgeConsent,StudyConsent,ConsentFormVersion,Timestamp\n",
            string.Join(",",
                CsvFileWriter.Escape(participantCode),
                CsvFileWriter.Escape(sessionId),
                ageConsent ? "1" : "0",
                studyConsent ? "1" : "0",
                CsvFileWriter.Escape(consentFormVersion ?? ""),
                CsvFileWriter.Escape(TimestampNow())) + "\n",
            "CONSENT RECORD SAVED");
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
            SessionContext session = GetSessionContext();
            string path = SessionFilePath(session, SessionCsvPaths.Files.ExperimentData);
            CsvFileWriter.EnsureHeader(path, ExperimentDataHeader);

            CsvFileWriter.RemoveProvisionalRows(path, ExperimentDataHeader);

            float timeSpent = Time.time - lastQuestionTime;
            lastQuestionTime = Time.time;

            var line = string.Join(",",
                CsvFileWriter.Escape(session.ParticipantCode),
                CsvFileWriter.Escape(session.SessionId),
                scenarioNumber.ToString(),
                CsvFileWriter.Escape(scenarioName),
                questionNumber.ToString(),
                CsvFileWriter.Escape(answerLetter),
                CsvFileWriter.Escape(answerText),
                CsvFileWriter.Escape(correctAnswerLetter),
                CsvFileWriter.Escape(correctAnswerText),
                confidence.ToString(),
                timeSpent.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                CsvFileWriter.Escape(TimestampNow())) + "\n";

            CsvFileWriter.AppendLine(path, line);
            LogSaved("DATA SAVED TO CSV", path);
            return true;
        }
        catch (Exception ex)
        {
            LogSaveError("SaveAnswer", ex);
            return false;
        }
    }

    public bool SaveChatTurn(
        int scenarioNumber,
        string scenarioName,
        string conditionCode,
        int questionNumber,
        int turnIndex,
        string role,
        string message)
    {
        SessionContext session = GetSessionContext();
        return TryAppendSessionCsv(
            session,
            SessionCsvPaths.Files.ChatLog,
            "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,TurnIndex,Role,Message,Timestamp\n",
            BuildChatTurnLine(session, scenarioNumber, scenarioName, conditionCode, questionNumber, turnIndex, role, message),
            "CHAT TURN SAVED TO CSV");
    }

    public bool SaveChatHelpRating(
        int scenarioNumber,
        string scenarioName,
        string conditionCode,
        int questionNumber,
        int exchangeIndex,
        string studentMessage,
        string modelMessage,
        ChatHelpScoring.ChatExchangeEvaluation evaluation,
        float secondsSinceQuestionStart,
        float geminiLatencySeconds)
    {
        SessionContext session = GetSessionContext();
        var line = string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            CsvFileWriter.Escape(conditionCode),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            questionNumber.ToString(),
            exchangeIndex.ToString(),
            CsvFileWriter.Escape(studentMessage),
            CsvFileWriter.Escape(modelMessage),
            FormatScore(evaluation.HelpScore),
            FormatScore(evaluation.GuidanceScore),
            FormatScore(evaluation.TaskEngagementScore),
            evaluation.StudentRequestedAnswer ? "1" : "0",
            evaluation.StudentOffTopic ? "1" : "0",
            evaluation.StudentGamingAttempt ? "1" : "0",
            evaluation.ModelPossibleLeak ? "1" : "0",
            FormatScore(evaluation.ScenarioRelevanceScore),
            evaluation.SubstantiveQuestion ? "1" : "0",
            CsvFileWriter.Escape(evaluation.QuestionUtilityLevel ?? ""),
            CsvFileWriter.Escape(evaluation.HelpLevel),
            CsvFileWriter.Escape(evaluation.Flags),
            secondsSinceQuestionStart.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            geminiLatencySeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            CsvFileWriter.Escape(TimestampNow())) + "\n";

        return TryAppendSessionCsv(
            session,
            SessionCsvPaths.Files.ChatHelpRating,
            "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,ExchangeIndex,StudentMessage,ModelMessage,HelpScore,GuidanceScore,TaskEngagementScore,StudentRequestedAnswer,StudentOffTopic,StudentGamingAttempt,ModelPossibleLeak,ScenarioRelevanceScore,SubstantiveQuestion,QuestionUtilityLevel,HelpLevel,Flags,SecondsSinceQuestionStart,GeminiLatencySeconds,Timestamp\n",
            line,
            "CHAT HELP RATING SAVED");
    }

    public bool SaveChatApiEvent(
        int scenarioNumber,
        string scenarioName,
        string conditionCode,
        int questionNumber,
        string eventType,
        long httpStatusCode,
        string failureReason,
        string studentMessage,
        float geminiLatencySeconds)
    {
        SessionContext session = GetSessionContext();
        var line = string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            CsvFileWriter.Escape(conditionCode),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            questionNumber.ToString(),
            CsvFileWriter.Escape(eventType ?? ""),
            httpStatusCode.ToString(),
            CsvFileWriter.Escape(failureReason ?? ""),
            CsvFileWriter.Escape(studentMessage ?? ""),
            geminiLatencySeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            CsvFileWriter.Escape(TimestampNow())) + "\n";

        return TryAppendSessionCsv(
            session,
            SessionCsvPaths.Files.ChatApiEvent,
            "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,EventType,HttpStatusCode,FailureReason,StudentMessage,GeminiLatencySeconds,Timestamp\n",
            line,
            "CHAT API EVENT SAVED");
    }

    public bool SaveTtsEvent(
        int scenarioNumber,
        string scenarioName,
        string conditionCode,
        int questionNumber,
        int exchangeIndex,
        bool success,
        string failureReason)
    {
        SessionContext session = GetSessionContext();
        var line = string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            CsvFileWriter.Escape(conditionCode),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            questionNumber.ToString(),
            exchangeIndex.ToString(),
            success ? "1" : "0",
            CsvFileWriter.Escape(failureReason ?? ""),
            CsvFileWriter.Escape(TimestampNow())) + "\n";

        return TryAppendSessionCsv(
            session,
            SessionCsvPaths.Files.TtsLog,
            "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,ExchangeIndex,TtsSuccess,FailureReason,Timestamp\n",
            line,
            "TTS EVENT SAVED");
    }

    public bool SaveChatQuestionSummary(
        int scenarioNumber,
        string scenarioName,
        string conditionCode,
        int questionNumber,
        int totalExchanges,
        int totalTurns,
        float avgHelpScore,
        float avgTaskEngagementScore,
        int answerRequestCount,
        int offTopicCount,
        int modelLeakCount,
        float onTopicSeconds,
        float offTopicSeconds,
        int substantiveQuestionCount,
        string chatUsedInQuestion,
        string dominantUtilityLevel,
        string effectiveHelpLevel,
        string flags)
    {
        SessionContext session = GetSessionContext();
        var line = string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            CsvFileWriter.Escape(conditionCode),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            questionNumber.ToString(),
            totalExchanges.ToString(),
            totalTurns.ToString(),
            FormatScore(avgHelpScore),
            FormatScore(avgTaskEngagementScore),
            answerRequestCount.ToString(),
            offTopicCount.ToString(),
            modelLeakCount.ToString(),
            onTopicSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            offTopicSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            substantiveQuestionCount.ToString(),
            CsvFileWriter.Escape(chatUsedInQuestion ?? "0"),
            CsvFileWriter.Escape(dominantUtilityLevel ?? "None"),
            CsvFileWriter.Escape(effectiveHelpLevel),
            CsvFileWriter.Escape(flags),
            CsvFileWriter.Escape(TimestampNow())) + "\n";

        return TryAppendSessionCsv(
            session,
            SessionCsvPaths.Files.ChatQuestionSummary,
            "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,TotalExchanges,TotalTurns,AvgHelpScore,AvgTaskEngagementScore,AnswerRequestCount,OffTopicCount,ModelLeakCount,OnTopicSeconds,OffTopicSeconds,SubstantiveQuestionCount,ChatUsedInQuestion,DominantUtilityLevel,EffectiveHelpLevel,Flags,Timestamp\n",
            line,
            "CHAT QUESTION SUMMARY SAVED");
    }

    public bool SaveChatScenarioSummary(
        int scenarioNumber,
        string scenarioName,
        string conditionCode,
        int totalQuestions,
        int questionsWithChat,
        int totalExchanges,
        int totalTurns,
        float avgHelpScore,
        float avgTaskEngagementScore,
        int answerRequestCount,
        int offTopicCount,
        int modelLeakCount,
        float onTopicSeconds,
        float offTopicSeconds,
        int substantiveQuestionCount,
        string chatUsedInBlock,
        string chatMeetsProtocol,
        string dominantUtilityLevel,
        int ttsAttempts,
        int ttsSuccessCount,
        string effectiveHelpLevel,
        string flags)
    {
        SessionContext session = GetSessionContext();
        string ttsRate = ttsAttempts > 0
            ? (100f * ttsSuccessCount / ttsAttempts).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            : "";

        var line = string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            CsvFileWriter.Escape(conditionCode),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            totalQuestions.ToString(),
            questionsWithChat.ToString(),
            totalExchanges.ToString(),
            totalTurns.ToString(),
            FormatScore(avgHelpScore),
            FormatScore(avgTaskEngagementScore),
            answerRequestCount.ToString(),
            offTopicCount.ToString(),
            modelLeakCount.ToString(),
            onTopicSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            offTopicSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            substantiveQuestionCount.ToString(),
            CsvFileWriter.Escape(chatUsedInBlock ?? "0"),
            CsvFileWriter.Escape(chatMeetsProtocol ?? "0"),
            CsvFileWriter.Escape(dominantUtilityLevel ?? "None"),
            ttsAttempts.ToString(),
            ttsSuccessCount.ToString(),
            ttsRate,
            CsvFileWriter.Escape(effectiveHelpLevel),
            CsvFileWriter.Escape(flags),
            CsvFileWriter.Escape(TimestampNow())) + "\n";

        return TryAppendSessionCsv(
            session,
            SessionCsvPaths.Files.ChatScenarioSummary,
            "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,TotalQuestions,QuestionsWithChat,TotalExchanges,TotalTurns,AvgHelpScore,AvgTaskEngagementScore,AnswerRequestCount,OffTopicCount,ModelLeakCount,OnTopicSeconds,OffTopicSeconds,SubstantiveQuestionCount,ChatUsedInBlock,ChatMeetsProtocol,DominantUtilityLevel,TtsAttempts,TtsSuccessCount,TtsSuccessRate,EffectiveHelpLevel,Flags,Timestamp\n",
            line,
            "CHAT SCENARIO SUMMARY SAVED");
    }

    const string ExperimentDataHeader =
        "ParticipantCode,SessionID,ScenarioNumber,ScenarioName,QuestionNumber,User_answer_Letter,User_answer,CorrectAnswerLetter,CorrectAnswer,Confidence,TimeSpent(Seconds),Timestamp\n";

    const string ChatLogHeader =
        "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,TurnIndex,Role,Message,Timestamp\n";

    const string ChatHelpRatingHeader =
        "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,ExchangeIndex,StudentMessage,ModelMessage,HelpScore,GuidanceScore,TaskEngagementScore,StudentRequestedAnswer,StudentOffTopic,StudentGamingAttempt,ModelPossibleLeak,ScenarioRelevanceScore,SubstantiveQuestion,QuestionUtilityLevel,HelpLevel,Flags,SecondsSinceQuestionStart,GeminiLatencySeconds,Timestamp\n";

    const string ChatApiEventHeader =
        "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,EventType,HttpStatusCode,FailureReason,StudentMessage,GeminiLatencySeconds,Timestamp\n";

    const string TtsLogHeader =
        "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,ExchangeIndex,TtsSuccess,FailureReason,Timestamp\n";

    const string ChatQuestionSummaryHeader =
        "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,TotalExchanges,TotalTurns,AvgHelpScore,AvgTaskEngagementScore,AnswerRequestCount,OffTopicCount,ModelLeakCount,OnTopicSeconds,OffTopicSeconds,SubstantiveQuestionCount,ChatUsedInQuestion,DominantUtilityLevel,EffectiveHelpLevel,Flags,Timestamp\n";

    const string ChatScenarioSummaryHeader =
        "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,TotalQuestions,QuestionsWithChat,TotalExchanges,TotalTurns,AvgHelpScore,AvgTaskEngagementScore,AnswerRequestCount,OffTopicCount,ModelLeakCount,OnTopicSeconds,OffTopicSeconds,SubstantiveQuestionCount,ChatUsedInBlock,ChatMeetsProtocol,DominantUtilityLevel,TtsAttempts,TtsSuccessCount,TtsSuccessRate,EffectiveHelpLevel,Flags,Timestamp\n";

    const string PendingSessionNote =
        "Sesión en curso. Si esta es la única fila de contenido, la sesión no finalizó correctamente (cierre forzado, cuelgue o apagado del equipo).";

    /// <summary>Creates the session folder and CSV files with header + provisional row at session start.</summary>
    public bool EnsureSessionCsvSkeleton()
    {
        try
        {
            SessionContext session = GetSessionContext();
            string pendingNote = Pending(PendingSessionNote);

            EnsureSkeletonWithPending(session, SessionCsvPaths.Files.ExperimentData, ExperimentDataHeader,
                explanation => BuildExperimentDataPlaceholder(session, 0, "Sesión", "—", explanation));
            EnsureSkeletonWithPending(session, SessionCsvPaths.Files.ChatLog, ChatLogHeader,
                explanation => BuildChatLogPlaceholder(session, "—", 0, "Sesión", 0, explanation));
            EnsureSkeletonWithPending(session, SessionCsvPaths.Files.ChatHelpRating, ChatHelpRatingHeader,
                explanation => BuildChatHelpRatingPlaceholder(session, "—", 0, "Sesión", 0, explanation));
            EnsureSkeletonWithPending(session, SessionCsvPaths.Files.ChatApiEvent, ChatApiEventHeader,
                explanation => BuildChatApiEventPlaceholder(session, "—", 0, "Sesión", explanation));
            EnsureSkeletonWithPending(session, SessionCsvPaths.Files.TtsLog, TtsLogHeader,
                explanation => BuildTtsLogPlaceholder(session, "—", 0, "Sesión", 0, explanation));
            EnsureSkeletonWithPending(session, SessionCsvPaths.Files.ChatQuestionSummary, ChatQuestionSummaryHeader,
                explanation => BuildChatQuestionSummaryPlaceholder(session, "—", 0, "Sesión", 0, explanation));
            EnsureSkeletonWithPending(session, SessionCsvPaths.Files.ChatScenarioSummary, ChatScenarioSummaryHeader,
                explanation => BuildChatScenarioSummaryPlaceholder(session, "—", 0, "Sesión", explanation));
#if UNITY_EDITOR
            Debug.Log($"<color=cyan>SESSION CSV SKELETON:</color> {session.Directory}");
#endif
            return true;
        }
        catch (Exception ex)
        {
            LogSaveError("EnsureSessionCsvSkeleton", ex);
            return false;
        }
    }

    void EnsureSkeletonWithPending(
        SessionContext session,
        string fileName,
        string header,
        System.Func<string, string> buildRow)
    {
        string path = SessionFilePath(session, fileName);
        if (CsvFileWriter.HasRealDataRows(path))
            return;

        CsvFileWriter.EnsureHeader(path, header);
        if (!CsvFileWriter.HasDataRows(path))
            CsvFileWriter.AppendLine(path, buildRow(Pending(PendingSessionNote)));
    }

    /// <summary>
    /// At session end, replaces provisional rows and adds [SIN DATOS] when no real rows were recorded.
    /// </summary>
    public bool FinalizeSessionCsvPlaceholders(
        bool ranConditionA,
        bool ranConditionB,
        bool ranConditionC,
        bool hadChatExchanges,
        bool abrupt)
    {
        try
        {
            SessionContext session = GetSessionContext();
            string conditionsLabel = BuildConditionsLabel(ranConditionA, ranConditionB, ranConditionC);
            bool ranChatConditions = ranConditionB || ranConditionC;

            EnsurePlaceholderIfEmpty(session, SessionCsvPaths.Files.ExperimentData, ExperimentDataHeader,
                BuildExperimentDataPlaceholder(session, 0, "Sesión", conditionsLabel,
                    NoData(WrapFinalizeReason("El participante no entregó ninguna respuesta registrada en esta sesión.", abrupt))));

            string chatUnavailableReason =
                WrapFinalizeReason(
                    "La sesión no incluyó escenarios con agente (condiciones B/C); el chat no está disponible.",
                    abrupt);
            string chatUnusedReason =
                WrapFinalizeReason("El participante no envió mensajes al chat durante esta sesión.", abrupt);
            string exchangesUnusedReason =
                WrapFinalizeReason(
                    "No hubo intercambios estudiante-agente en esta sesión; no se calcularon métricas de ayuda.",
                    abrupt);
            string apiUnusedReason =
                WrapFinalizeReason(
                    "No se registraron fallos ni eventos de la API Gemini durante esta sesión.",
                    abrupt);
            string ttsUnavailableReason =
                WrapFinalizeReason(
                    "La sesión no incluyó la condición C (agente virtual con voz); Azure TTS no aplica.",
                    abrupt);
            string ttsUnusedReason =
                WrapFinalizeReason(
                    hadChatExchanges
                        ? "Hubo chat en condición C pero no se registraron intentos de síntesis de voz."
                        : "No hubo chat ni intentos de síntesis de voz Azure en la condición C.",
                    abrupt);
            string summaryUnavailableReason =
                WrapFinalizeReason(
                    "La sesión no incluyó escenarios con agente (condiciones B/C); no hay resúmenes de chat.",
                    abrupt);
            string summaryUnusedReason =
                WrapFinalizeReason(
                    "La sesión incluyó escenarios B/C pero no se generaron resúmenes (sesión incompleta o sin actividad registrada).",
                    abrupt);

            EnsurePlaceholderIfEmpty(session, SessionCsvPaths.Files.ChatLog, ChatLogHeader,
                BuildChatLogPlaceholder(session, conditionsLabel, 0, "Sesión", 0,
                    NoData(ranChatConditions ? chatUnusedReason : chatUnavailableReason)));

            EnsurePlaceholderIfEmpty(session, SessionCsvPaths.Files.ChatHelpRating, ChatHelpRatingHeader,
                BuildChatHelpRatingPlaceholder(session, conditionsLabel, 0, "Sesión", 0,
                    NoData(ranChatConditions ? exchangesUnusedReason : chatUnavailableReason)));

            EnsurePlaceholderIfEmpty(session, SessionCsvPaths.Files.ChatApiEvent, ChatApiEventHeader,
                BuildChatApiEventPlaceholder(session, conditionsLabel, 0, "Sesión",
                    NoData(apiUnusedReason)));

            EnsurePlaceholderIfEmpty(session, SessionCsvPaths.Files.TtsLog, TtsLogHeader,
                BuildTtsLogPlaceholder(session, conditionsLabel, 0, "Sesión", 0,
                    NoData(ranConditionC ? ttsUnusedReason : ttsUnavailableReason)));

            EnsurePlaceholderIfEmpty(session, SessionCsvPaths.Files.ChatQuestionSummary, ChatQuestionSummaryHeader,
                BuildChatQuestionSummaryPlaceholder(session, conditionsLabel, 0, "Sesión", 0,
                    NoData(ranChatConditions ? summaryUnusedReason : summaryUnavailableReason)));

            EnsurePlaceholderIfEmpty(session, SessionCsvPaths.Files.ChatScenarioSummary, ChatScenarioSummaryHeader,
                BuildChatScenarioSummaryPlaceholder(session, conditionsLabel, 0, "Sesión",
                    NoData(ranChatConditions ? summaryUnusedReason : summaryUnavailableReason)));

            return true;
        }
        catch (Exception ex)
        {
            LogSaveError("FinalizeSessionCsvPlaceholders", ex);
            return false;
        }
    }

    static string WrapFinalizeReason(string reason, bool abrupt)
    {
        if (!abrupt)
            return reason;

        return "Sesión interrumpida abruptamente (cierre forzado, cuelgue o apagado del sistema). " + reason;
    }

    void EnsurePlaceholderIfEmpty(SessionContext session, string fileName, string header, string line)
    {
        string path = SessionFilePath(session, fileName);
        if (CsvFileWriter.HasRealDataRows(path) || CsvFileWriter.HasNoDataPlaceholder(path))
            return;

        CsvFileWriter.EnsureHeader(path, header);
        CsvFileWriter.RemoveProvisionalRows(path, header);
        CsvFileWriter.AppendLine(path, line);
#if UNITY_EDITOR
        Debug.Log($"<color=yellow>CSV PLACEHOLDER:</color> {Path.GetFullPath(path)}");
#endif
    }

    static string Pending(string explanation) => CsvFileWriter.PendingMarker + " " + explanation;

    static string NoData(string explanation) => CsvFileWriter.NoDataMarker + " " + explanation;

    static string BuildConditionsLabel(bool ranConditionA, bool ranConditionB, bool ranConditionC)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (ranConditionA) parts.Add("A");
        if (ranConditionB) parts.Add("B");
        if (ranConditionC) parts.Add("C");
        return parts.Count > 0 ? string.Join("|", parts) : "—";
    }

    static string BuildExperimentDataPlaceholder(
        SessionContext session,
        int scenarioNumber,
        string scenarioName,
        string conditionCode,
        string explanation)
    {
        return string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            "0",
            CsvFileWriter.Escape("—"),
            CsvFileWriter.Escape(explanation),
            CsvFileWriter.Escape("—"),
            CsvFileWriter.Escape("—"),
            "0",
            "0.00",
            CsvFileWriter.Escape(TimestampNow())) + "\n";
    }

    static string BuildChatLogPlaceholder(
        SessionContext session,
        string conditionCode,
        int scenarioNumber,
        string scenarioName,
        int questionNumber,
        string explanation)
    {
        return string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            CsvFileWriter.Escape(conditionCode),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            questionNumber.ToString(),
            "0",
            CsvFileWriter.Escape("system"),
            CsvFileWriter.Escape(explanation),
            CsvFileWriter.Escape(TimestampNow())) + "\n";
    }

    static string BuildChatHelpRatingPlaceholder(
        SessionContext session,
        string conditionCode,
        int scenarioNumber,
        string scenarioName,
        int questionNumber,
        string explanation)
    {
        return string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            CsvFileWriter.Escape(conditionCode),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            questionNumber.ToString(),
            "0",
            CsvFileWriter.Escape(explanation),
            CsvFileWriter.Escape("—"),
            "0.0", "0.0", "0.0",
            "0", "0", "0", "0",
            "0.0", "0",
            CsvFileWriter.Escape("None"),
            CsvFileWriter.Escape("None"),
            CsvFileWriter.Escape("None"),
            CsvFileWriter.Escape("no_data"),
            "0.00", "0.00",
            CsvFileWriter.Escape(TimestampNow())) + "\n";
    }

    static string BuildChatApiEventPlaceholder(
        SessionContext session,
        string conditionCode,
        int scenarioNumber,
        string scenarioName,
        string message)
    {
        string eventType = message.StartsWith(CsvFileWriter.PendingMarker, StringComparison.Ordinal)
            ? "PENDING"
            : "NO_DATA";

        return string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            CsvFileWriter.Escape(conditionCode),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            "0",
            CsvFileWriter.Escape(eventType),
            "0",
            CsvFileWriter.Escape(message),
            CsvFileWriter.Escape("—"),
            "0.00",
            CsvFileWriter.Escape(TimestampNow())) + "\n";
    }

    static string BuildTtsLogPlaceholder(
        SessionContext session,
        string conditionCode,
        int scenarioNumber,
        string scenarioName,
        int questionNumber,
        string explanation)
    {
        return string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            CsvFileWriter.Escape(conditionCode),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            questionNumber.ToString(),
            "0",
            "",
            CsvFileWriter.Escape(explanation),
            CsvFileWriter.Escape(TimestampNow())) + "\n";
    }

    static string BuildChatQuestionSummaryPlaceholder(
        SessionContext session,
        string conditionCode,
        int scenarioNumber,
        string scenarioName,
        int questionNumber,
        string explanation)
    {
        return string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            CsvFileWriter.Escape(conditionCode),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            questionNumber.ToString(),
            "0", "0", "0.0", "0.0", "0", "0", "0",
            "0.00", "0.00", "0", "0",
            CsvFileWriter.Escape("None"),
            CsvFileWriter.Escape("None"),
            CsvFileWriter.Escape("no_data|" + explanation),
            CsvFileWriter.Escape(TimestampNow())) + "\n";
    }

    static string BuildChatScenarioSummaryPlaceholder(
        SessionContext session,
        string conditionCode,
        int scenarioNumber,
        string scenarioName,
        string explanation)
    {
        return string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            CsvFileWriter.Escape(conditionCode),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            "0", "0", "0", "0", "0.0", "0.0", "0", "0", "0",
            "0.00", "0.00", "0", "0", "0",
            CsvFileWriter.Escape("None"),
            "0", "0", "",
            CsvFileWriter.Escape("None"),
            CsvFileWriter.Escape("no_data|" + explanation),
            CsvFileWriter.Escape(TimestampNow())) + "\n";
    }

    public void DeleteIncompleteFile()
    {
        try
        {
            SessionContext session = GetSessionContext();
            SessionCsvPaths.DeleteSessionDirectory(session.Directory);
            SessionCsvPaths.DeleteLegacyFlatFiles(session.ParticipantCode, session.SessionId);
            SessionCsvPaths.DeleteLegacyLogsExportFile(session.ParticipantCode, session.SessionId);
#if UNITY_EDITOR
            Debug.Log("Incomplete session CSV folder deleted.");
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"DataLogger.DeleteIncompleteFile() failed safely: {ex.Message}");
        }
    }

    struct SessionContext
    {
        public string ParticipantCode;
        public string SessionId;
        public string Directory;
    }

    SessionContext GetSessionContext()
    {
        string participantCode = "Unknown";
        string sessionId = "Unknown";
        ExperimentLogic expLogic = UnityEngine.Object.FindFirstObjectByType<ExperimentLogic>(FindObjectsInactive.Include);
        if (expLogic != null)
        {
            participantCode = expLogic.GetParticipantCode();
            sessionId = expLogic.GetSessionUserId();
        }

        if (string.IsNullOrEmpty(participantCode))
            participantCode = "Unknown";
        if (string.IsNullOrEmpty(sessionId))
            sessionId = "Unknown";

        return new SessionContext
        {
            ParticipantCode = participantCode,
            SessionId = sessionId,
            Directory = SessionCsvPaths.BuildSessionDirectory(participantCode, sessionId)
        };
    }

    static string SessionFilePath(SessionContext session, string fileName) =>
        Path.Combine(session.Directory, fileName);

    bool TryAppendSessionCsv(
        string participantCode,
        string sessionId,
        string fileName,
        string header,
        string line,
        string logLabel)
    {
        try
        {
            SessionContext session = new SessionContext
            {
                ParticipantCode = participantCode,
                SessionId = sessionId,
                Directory = SessionCsvPaths.BuildSessionDirectory(participantCode, sessionId)
            };
            return TryAppendSessionCsv(session, fileName, header, line, logLabel);
        }
        catch (Exception ex)
        {
            LogSaveError(logLabel, ex);
            return false;
        }
    }

    bool TryAppendSessionCsv(SessionContext session, string fileName, string header, string line, string logLabel)
    {
        try
        {
            string path = SessionFilePath(session, fileName);
            CsvFileWriter.EnsureHeader(path, header);
            CsvFileWriter.RemoveProvisionalRows(path, header);
            CsvFileWriter.AppendLine(path, line);
            LogSaved(logLabel, path);
            return true;
        }
        catch (Exception ex)
        {
            LogSaveError(logLabel, ex);
            return false;
        }
    }

    string BuildChatTurnLine(
        SessionContext session,
        int scenarioNumber,
        string scenarioName,
        string conditionCode,
        int questionNumber,
        int turnIndex,
        string role,
        string message)
    {
        return string.Join(",",
            CsvFileWriter.Escape(session.ParticipantCode),
            CsvFileWriter.Escape(session.SessionId),
            CsvFileWriter.Escape(conditionCode),
            scenarioNumber.ToString(),
            CsvFileWriter.Escape(scenarioName),
            questionNumber.ToString(),
            turnIndex.ToString(),
            CsvFileWriter.Escape(role),
            CsvFileWriter.Escape(message),
            CsvFileWriter.Escape(TimestampNow())) + "\n";
    }

    static string TimestampNow() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    static string FormatScore(float value) =>
        value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

    static void LogSaved(string label, string path)
    {
#if UNITY_EDITOR
        Debug.Log($"<color=cyan>{label}:</color> {Path.GetFullPath(path)}");
#endif
    }

    static void LogSaveError(string operation, Exception ex)
    {
        Debug.LogError($"DataLogger.{operation} failed safely: {ex.Message}\nStack trace: {ex.StackTrace}");
    }
}
