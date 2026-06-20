using System.Collections.Generic;
using MyProject;
using UnityEngine;

/// <summary>
/// Tracks chat metrics for the active question/scenario and persists CSV rows via <see cref="DataLogger"/>.
/// </summary>
public class ChatSessionLogger
{
    struct QuestionExchangeTiming
    {
        public bool CountAsOffTopicTime;
        public string UtilityLevel;
        public float SecondsSinceStart;
    }

    int chatTurnIndex;
    int chatExchangeIndex;
    int questionAnswerRequestCount;
    int questionOffTopicCount;
    int questionModelLeakCount;
    int questionExchangeCount;
    float questionHelpScoreSum;
    float questionEngagementScoreSum;
    int questionSubstantiveQuestionCount;
    readonly List<QuestionExchangeTiming> questionExchangeTimings = new List<QuestionExchangeTiming>();
    readonly List<string> questionUtilityLevels = new List<string>();

    int scenarioQuestionsWithChat;
    int scenarioExchangeCount;
    float scenarioHelpScoreSum;
    float scenarioEngagementScoreSum;
    int scenarioAnswerRequestCount;
    int scenarioOffTopicCount;
    int scenarioModelLeakCount;
    float scenarioOnTopicSeconds;
    float scenarioOffTopicSeconds;
    int scenarioSubstantiveQuestionCount;
    readonly List<string> scenarioUtilityLevels = new List<string>();

    int scenarioTotalTurns;
    bool scenarioSummaryFlushed;
    int scenarioTtsAttempts;
    int scenarioTtsSuccesses;

    public int LastExchangeIndex => chatExchangeIndex;

    public int ScenarioTotalTurns => scenarioTotalTurns;

    public int ScenarioExchangeCount => scenarioExchangeCount;

    public int ScenarioTtsAttempts => scenarioTtsAttempts;

    public bool HasPendingQuestionActivity => questionExchangeCount > 0 || chatTurnIndex > 0;

    public bool HasScenarioActivity =>
        scenarioExchangeCount > 0 || scenarioQuestionsWithChat > 0 || HasPendingQuestionActivity ||
        scenarioTtsAttempts > 0;

    public bool ScenarioSummaryFlushed => scenarioSummaryFlushed;

    public void ResetQuestionMetrics()
    {
        chatTurnIndex = 0;
        chatExchangeIndex = 0;
        questionAnswerRequestCount = 0;
        questionOffTopicCount = 0;
        questionModelLeakCount = 0;
        questionExchangeCount = 0;
        questionHelpScoreSum = 0f;
        questionEngagementScoreSum = 0f;
        questionSubstantiveQuestionCount = 0;
        questionExchangeTimings.Clear();
        questionUtilityLevels.Clear();
    }

    public void ResetScenarioMetrics()
    {
        ResetQuestionMetrics();
        scenarioQuestionsWithChat = 0;
        scenarioExchangeCount = 0;
        scenarioHelpScoreSum = 0f;
        scenarioEngagementScoreSum = 0f;
        scenarioAnswerRequestCount = 0;
        scenarioOffTopicCount = 0;
        scenarioModelLeakCount = 0;
        scenarioOnTopicSeconds = 0f;
        scenarioOffTopicSeconds = 0f;
        scenarioSubstantiveQuestionCount = 0;
        scenarioUtilityLevels.Clear();
        scenarioTotalTurns = 0;
        scenarioSummaryFlushed = false;
        scenarioTtsAttempts = 0;
        scenarioTtsSuccesses = 0;
    }

    public bool RecordTtsOutcome(DataLogger logger, TtsExchangeContext context, bool success, string failureReason)
    {
        if (logger == null || !context.IsValid || context.ConditionCode != "C")
            return true;

        if (!success && (failureReason == "superseded" || failureReason == "cancelled"))
            return true;

        if (!logger.SaveTtsEvent(
                context.ScenarioNumber,
                context.ScenarioName,
                context.ConditionCode,
                context.QuestionNumber,
                context.ExchangeIndex,
                success,
                failureReason))
            return false;

        // TTS may finish after FlushScenarioSummary; keep TtsLog rows but avoid stale aggregates.
        if (scenarioSummaryFlushed)
            return true;

        scenarioTtsAttempts++;
        if (success)
            scenarioTtsSuccesses++;

        return true;
    }

    public bool RecordTurn(DataLogger logger, QuestionManager questionManager, string role, string message)
    {
        if (logger == null || questionManager == null || !questionManager.IsChatAssistanceEnabled)
            return true;

        int nextTurnIndex = chatTurnIndex + 1;
        if (!logger.SaveChatTurn(
                questionManager.ActiveScenarioNumber,
                questionManager.ActiveScenarioName,
                questionManager.ActiveConditionCode,
                questionManager.currentQuestionIndex + 1,
                nextTurnIndex,
                role,
                message))
            return false;

        chatTurnIndex = nextTurnIndex;
        scenarioTotalTurns++;
        return true;
    }

    public int RecordExchange(
        DataLogger logger,
        QuestionManager questionManager,
        string studentMessage,
        string modelMessage,
        string questionContext,
        float secondsSinceQuestionStart,
        float geminiLatencySeconds)
    {
        if (logger == null || questionManager == null || !questionManager.IsChatAssistanceEnabled)
            return 0;

        int nextExchangeIndex = chatExchangeIndex + 1;
        var evaluation = ChatHelpScoring.Evaluate(studentMessage, modelMessage, questionContext);

        if (!logger.SaveChatHelpRating(
                questionManager.ActiveScenarioNumber,
                questionManager.ActiveScenarioName,
                questionManager.ActiveConditionCode,
                questionManager.currentQuestionIndex + 1,
                nextExchangeIndex,
                studentMessage,
                modelMessage,
                evaluation,
                secondsSinceQuestionStart,
                geminiLatencySeconds))
            return 0;

        chatExchangeIndex = nextExchangeIndex;
        questionExchangeCount++;
        questionHelpScoreSum += evaluation.HelpScore;
        questionEngagementScoreSum += evaluation.TaskEngagementScore;

        if (evaluation.StudentRequestedAnswer)
            questionAnswerRequestCount++;

        if (evaluation.StudentOffTopic)
            questionOffTopicCount++;

        if (evaluation.ModelPossibleLeak)
            questionModelLeakCount++;

        if (evaluation.SubstantiveQuestion)
            questionSubstantiveQuestionCount++;

        questionExchangeTimings.Add(new QuestionExchangeTiming
        {
            CountAsOffTopicTime = evaluation.StudentOffTopic ||
                                    evaluation.StudentRequestedAnswer ||
                                    evaluation.StudentGamingAttempt,
            UtilityLevel = evaluation.QuestionUtilityLevel,
            SecondsSinceStart = secondsSinceQuestionStart
        });
        questionUtilityLevels.Add(evaluation.QuestionUtilityLevel);

        return chatExchangeIndex;
    }

    public bool FlushQuestionSummary(
        DataLogger logger,
        QuestionManager questionManager,
        int questionNumber,
        float questionEndSecondsSinceStart,
        bool includeWhenNoActivity = true)
    {
        if (logger == null || questionManager == null || !questionManager.IsChatAssistanceEnabled)
            return true;

        if (!includeWhenNoActivity && !HasPendingQuestionActivity)
            return true;

        float avgHelp = questionExchangeCount > 0 ? questionHelpScoreSum / questionExchangeCount : 0f;
        float avgEngagement = questionExchangeCount > 0 ? questionEngagementScoreSum / questionExchangeCount : 0f;
        string effectiveLevel = ChatHelpScoring.ComputeEffectiveHelpLevel(
            questionExchangeCount,
            avgHelp,
            avgEngagement,
            questionAnswerRequestCount,
            questionOffTopicCount,
            questionModelLeakCount);

        float onTopicSeconds = 0f;
        float offTopicSeconds = 0f;
        if (questionExchangeTimings.Count > 0)
        {
            var startSeconds = new List<float>(questionExchangeTimings.Count);
            var offTopicFlags = new List<bool>(questionExchangeTimings.Count);
            foreach (var timing in questionExchangeTimings)
            {
                startSeconds.Add(timing.SecondsSinceStart);
                offTopicFlags.Add(timing.CountAsOffTopicTime);
            }

            ChatHelpScoring.ComputeTopicTimeSeconds(
                startSeconds,
                offTopicFlags,
                questionEndSecondsSinceStart,
                out onTopicSeconds,
                out offTopicSeconds);
        }

        string dominantUtility = ChatHelpScoring.PickDominantUtilityLevel(questionUtilityLevels);
        string chatUsedInQuestion = questionExchangeCount > 0 ? "1" : "0";

        var flags = BuildQuestionFlags(onTopicSeconds, offTopicSeconds, dominantUtility);

        if (!logger.SaveChatQuestionSummary(
                questionManager.ActiveScenarioNumber,
                questionManager.ActiveScenarioName,
                questionManager.ActiveConditionCode,
                questionNumber,
                questionExchangeCount,
                chatTurnIndex,
                avgHelp,
                avgEngagement,
                questionAnswerRequestCount,
                questionOffTopicCount,
                questionModelLeakCount,
                onTopicSeconds,
                offTopicSeconds,
                questionSubstantiveQuestionCount,
                chatUsedInQuestion,
                dominantUtility,
                effectiveLevel,
                string.Join("|", flags)))
            return false;

        if (questionExchangeCount > 0)
        {
            scenarioQuestionsWithChat++;
            scenarioExchangeCount += questionExchangeCount;
            scenarioHelpScoreSum += questionHelpScoreSum;
            scenarioEngagementScoreSum += questionEngagementScoreSum;
            scenarioAnswerRequestCount += questionAnswerRequestCount;
            scenarioOffTopicCount += questionOffTopicCount;
            scenarioModelLeakCount += questionModelLeakCount;
            scenarioOnTopicSeconds += onTopicSeconds;
            scenarioOffTopicSeconds += offTopicSeconds;
            scenarioSubstantiveQuestionCount += questionSubstantiveQuestionCount;
            scenarioUtilityLevels.AddRange(questionUtilityLevels);
        }

        return true;
    }

    public bool FlushScenarioSummary(DataLogger logger, QuestionManager questionManager, int totalQuestions)
    {
        if (logger == null || questionManager == null || !questionManager.IsChatAssistanceEnabled)
            return true;

        if (scenarioSummaryFlushed)
            return true;

        float avgHelp = scenarioExchangeCount > 0 ? scenarioHelpScoreSum / scenarioExchangeCount : 0f;
        float avgEngagement = scenarioExchangeCount > 0 ? scenarioEngagementScoreSum / scenarioExchangeCount : 0f;
        string effectiveLevel = ChatHelpScoring.ComputeScenarioEffectiveHelpLevel(
            totalQuestions,
            scenarioQuestionsWithChat,
            scenarioExchangeCount,
            avgHelp,
            avgEngagement,
            scenarioAnswerRequestCount,
            scenarioOffTopicCount,
            scenarioModelLeakCount);

        var flags = new List<string>();
        if (scenarioAnswerRequestCount > 0)
            flags.Add("answer_requests=" + scenarioAnswerRequestCount);
        if (scenarioOffTopicCount > 0)
            flags.Add("off_topic=" + scenarioOffTopicCount);
        if (scenarioModelLeakCount > 0)
            flags.Add("model_leaks=" + scenarioModelLeakCount);
        if (scenarioQuestionsWithChat == 0)
            flags.Add("no_chat_used");

        string chatUsedInBlock = scenarioQuestionsWithChat > 0 ? "1" : "0";
        string chatMeetsProtocol = scenarioExchangeCount >= 1 ? "1" : "0";
        string dominantUtility = ChatHelpScoring.PickDominantUtilityLevel(scenarioUtilityLevels);
        if (dominantUtility != "None")
            flags.Add("dominant_utility=" + dominantUtility.ToLowerInvariant());
        if (chatMeetsProtocol == "0")
            flags.Add("chat_protocol_not_met");

        if (scenarioTtsAttempts > 0)
        {
            float ttsRate = 100f * scenarioTtsSuccesses / scenarioTtsAttempts;
            flags.Add("tts_success_rate=" + ttsRate.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            if (ttsRate < 85f)
                flags.Add("tts_below_85pct");
        }

        if (!logger.SaveChatScenarioSummary(
                questionManager.ActiveScenarioNumber,
                questionManager.ActiveScenarioName,
                questionManager.ActiveConditionCode,
                totalQuestions,
                scenarioQuestionsWithChat,
                scenarioExchangeCount,
                scenarioTotalTurns,
                avgHelp,
                avgEngagement,
                scenarioAnswerRequestCount,
                scenarioOffTopicCount,
                scenarioModelLeakCount,
                scenarioOnTopicSeconds,
                scenarioOffTopicSeconds,
                scenarioSubstantiveQuestionCount,
                chatUsedInBlock,
                chatMeetsProtocol,
                dominantUtility,
                scenarioTtsAttempts,
                scenarioTtsSuccesses,
                effectiveLevel,
                string.Join("|", flags)))
            return false;

        scenarioSummaryFlushed = true;
        return true;
    }

    List<string> BuildQuestionFlags(float onTopicSeconds, float offTopicSeconds, string dominantUtility)
    {
        var flags = new List<string>();

        if (questionExchangeCount == 0)
            flags.Add("no_chat_used");

        if (questionAnswerRequestCount > 0)
            flags.Add("answer_requests=" + questionAnswerRequestCount);

        if (questionOffTopicCount > 0)
            flags.Add("off_topic=" + questionOffTopicCount);

        if (questionModelLeakCount > 0)
            flags.Add("model_leaks=" + questionModelLeakCount);

        if (questionSubstantiveQuestionCount > 0)
            flags.Add("substantive=" + questionSubstantiveQuestionCount);

        if (onTopicSeconds > 0f || offTopicSeconds > 0f)
        {
            flags.Add("on_topic_sec=" + onTopicSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            flags.Add("off_topic_sec=" + offTopicSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(dominantUtility) && dominantUtility != "None")
            flags.Add("dominant_utility=" + dominantUtility.ToLowerInvariant());

        return flags;
    }
}
