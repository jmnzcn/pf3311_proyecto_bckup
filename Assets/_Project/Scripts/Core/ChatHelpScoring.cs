using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MyProject;

/// <summary>
/// Heuristic scoring for chat exchanges (no extra API calls). Supports CSV export of help quality.
/// </summary>
public static class ChatHelpScoring
{
    public struct ChatExchangeEvaluation
    {
        public float HelpScore;
        public float GuidanceScore;
        public float TaskEngagementScore;
        public bool StudentRequestedAnswer;
        public bool StudentOffTopic;
        public bool StudentGamingAttempt;
        public bool ModelPossibleLeak;
        public float ScenarioRelevanceScore;
        public bool SubstantiveQuestion;
        public string QuestionUtilityLevel;
        public string HelpLevel;
        public string Flags;
    }

    public static readonly Dictionary<string, int> UtilityLevelRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        { "Wasted", 0 },
        { "Minimal", 1 },
        { "Productive", 2 },
        { "HighValue", 3 },
    };

    static readonly Regex[] StudentAnswerRequestPatterns =
    {
        new Regex(@"\b(dame|decime|dime|decirme|indicame|indícame)\b.{0,40}\b(respuesta|opcion|opción|letra|correcta)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(cu[aá]l\s+es\s+la\s+(?:correcta|opcion|opción|letra))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(cu[aá]l\s+es\s+la\s+respuesta)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(opci[oó]n\s+correcta|la\s+correcta\s+es|solo\s+dime|s[oó]lo\s+dime)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(responde\s+por\s+m[ií]|hac[eé]lo\s+por\s+m[ií])\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(dime\s+la\s+[abcd]|marca\s+la\s+[abcd]|elige\s+[abcd])\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    static readonly Regex[] StudentGamingPatterns =
    {
        new Regex(@"\b(ignora|olvida|salteate|salta)\b.{0,30}\b(instrucciones|reglas|prompt)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(cu[eé]ntame|dime)\s+(un\s+)?(chiste|historia|broma)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(eres|sos)\s+(un\s+)?(bot|ia|inteligencia\s+artificial|chatgpt|gemini)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(hackear|jailbreak|modo\s+desarrollador|dan\s+mode)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(no\s+importa|da\s+igual)\s+(el\s+)?(escenario|pregunta|caso)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    static readonly Regex[] ModelLeakPatterns =
    {
        new Regex(@"\b(la\s+)?(opci[oó]n|alternativa)\s+(correcta\s+)?(es\s+)?[abcd]\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(la\s+)?(correcta|mejor\s+opci[oó]n)\s+es\s+[abcd]\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(elige|selecciona|marca|escoge)\s+(la\s+)?opci[oó]n\s+[abcd]\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(la\s+)?(letra|opcion|opción)\s+[abcd]\s+(es\s+)?(la\s+)?(correcta|adecuada|indicada)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(la\s+)?(?:respuesta|soluci[oó]n)\s+(?:correcta\s+)?(?:es|ser[ií]a)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\bdebes?\s+(elegir|seleccionar|marcar)\s+(la\s+)?opci[oó]n\s+[abcd]\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    static readonly Regex[] ModelAffirmationLeakPatterns =
    {
        new Regex(@"\b(excelente|perfecto|correcto|acertaste|bien\s+hecho|muy\s+bien|exacto|efectivamente|as[ií]\s+es)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\blograste\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\bconectaste\s+todos\s+los\s+puntos\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\b(esa|esa\s+es)\s+(la\s+)?(respuesta|opción|opci[oó]n)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"\bparece\s+que\s+.{0,60}\b(correct|acert|bien|claro|resolv)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    static readonly Regex StudentStatingOptionPattern =
        new Regex(@"^\s*(s[ií]|no)\s*[—\-–:]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static readonly Regex GreetingOnlyPattern =
        new Regex(@"^\s*(hola|buenos\s+d[ií]as|buenas\s+tardes|buenas\s+noches|hey|jaja+|lol|prueba|test|qu[eé]\s+tal)\s*[!?.]*\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "para", "como", "esta", "este", "está", "que", "con", "por", "son", "del", "las", "los", "una", "uno", "sus", "hay", "más", "mas", "muy", "sobre", "desde", "hacia", "entre", "cada", "todo", "toda", "todos", "todas", "debe", "deber", "puede", "pueden", "sería", "seria", "qué", "cual", "cuál", "agente", "profesor", "estudiante", "pregunta", "opcion", "opción"
    };

    public static string BuildQuestionContext(QuestionManager.ExperimentQuestion question)
    {
        if (question == null)
            return "";

        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(question.situation))
            builder.Append(question.situation.Trim());

        AppendOptionContext(builder, question.optA);
        AppendOptionContext(builder, question.optB);
        AppendOptionContext(builder, question.optC);
        AppendOptionContext(builder, question.optD);

        return builder.ToString();
    }

    static void AppendOptionContext(StringBuilder builder, string optionText)
    {
        if (string.IsNullOrWhiteSpace(optionText))
            return;

        if (builder.Length > 0)
            builder.Append(' ');

        builder.Append(optionText.Trim());
    }

    public static ChatExchangeEvaluation Evaluate(string studentMessage, string modelMessage, string questionContext)
    {
        var evaluation = new ChatExchangeEvaluation
        {
            HelpScore = 0f,
            GuidanceScore = 0f,
            TaskEngagementScore = 50f,
            HelpLevel = "Low",
            Flags = ""
        };

        string student = Normalize(studentMessage);
        string model = Normalize(modelMessage);

        evaluation.StudentRequestedAnswer = MatchesAny(student, StudentAnswerRequestPatterns);
        evaluation.StudentGamingAttempt = MatchesAny(student, StudentGamingPatterns);
        evaluation.StudentOffTopic = IsStudentOffTopic(student, questionContext, evaluation.StudentRequestedAnswer, evaluation.StudentGamingAttempt);
        evaluation.ModelPossibleLeak = MatchesAny(model, ModelLeakPatterns) ||
                                       IsModelAnswerAffirmation(student, model);
        evaluation.ScenarioRelevanceScore = ComputeScenarioRelevanceScore(student, questionContext);
        evaluation.SubstantiveQuestion = IsSubstantiveQuestion(
            student,
            evaluation.StudentOffTopic,
            evaluation.StudentGamingAttempt,
            evaluation.StudentRequestedAnswer);

        evaluation.TaskEngagementScore = ScoreTaskEngagement(
            student,
            evaluation.StudentRequestedAnswer,
            evaluation.StudentOffTopic,
            evaluation.StudentGamingAttempt);
        evaluation.GuidanceScore = ScoreGuidance(model, evaluation.ModelPossibleLeak);
        evaluation.HelpScore = ScoreHelp(evaluation.GuidanceScore, evaluation.ModelPossibleLeak, evaluation.TaskEngagementScore);
        evaluation.QuestionUtilityLevel = ClassifyQuestionUtilityLevel(
            student,
            evaluation.SubstantiveQuestion,
            evaluation.StudentOffTopic,
            evaluation.StudentGamingAttempt,
            evaluation.StudentRequestedAnswer,
            evaluation.ScenarioRelevanceScore,
            evaluation.HelpScore);

        evaluation.HelpLevel = ClassifyHelpLevel(evaluation.HelpScore, evaluation.ModelPossibleLeak);
        evaluation.Flags = BuildFlags(evaluation);

        return evaluation;
    }

    public static void ComputeTopicTimeSeconds(
        IList<float> exchangeStartSeconds,
        IList<bool> exchangeOffTopic,
        float questionEndSeconds,
        out float onTopicSeconds,
        out float offTopicSeconds)
    {
        onTopicSeconds = 0f;
        offTopicSeconds = 0f;

        if (exchangeStartSeconds == null || exchangeStartSeconds.Count == 0)
            return;

        for (int i = 0; i < exchangeStartSeconds.Count; i++)
        {
            float start = exchangeStartSeconds[i];
            float end = i + 1 < exchangeStartSeconds.Count
                ? exchangeStartSeconds[i + 1]
                : Math.Max(questionEndSeconds, start + 30f);
            float duration = Math.Max(0f, end - start);

            if (exchangeOffTopic != null && i < exchangeOffTopic.Count && exchangeOffTopic[i])
                offTopicSeconds += duration;
            else
                onTopicSeconds += duration;
        }
    }

    public static string PickDominantUtilityLevel(IEnumerable<string> utilityLevels)
    {
        if (utilityLevels == null)
            return "None";

        string best = "None";
        int bestRank = -1;

        foreach (string level in utilityLevels)
        {
            if (string.IsNullOrWhiteSpace(level))
                continue;

            if (!UtilityLevelRank.TryGetValue(level.Trim(), out int rank))
                continue;

            if (rank > bestRank)
            {
                bestRank = rank;
                best = level.Trim();
            }
        }

        return bestRank >= 0 ? best : "None";
    }

    public static string ComputeEffectiveHelpLevel(
        int exchangeCount,
        float avgHelpScore,
        float avgTaskEngagement,
        int answerRequestCount,
        int offTopicCount,
        int modelLeakCount)
    {
        if (exchangeCount <= 0)
            return "None";

        int disruptiveCount = answerRequestCount + offTopicCount;
        float disruptiveRatio = disruptiveCount / (float)exchangeCount;

        if (disruptiveRatio >= 0.6f && avgTaskEngagement < 45f)
            return "LowEngagement";

        if (modelLeakCount > 0)
            return "Compromised";

        if (avgHelpScore >= 65f && avgTaskEngagement >= 50f)
            return "High";

        if (avgHelpScore >= 40f)
            return "Medium";

        return "Low";
    }

    public static string ComputeScenarioEffectiveHelpLevel(
        int totalQuestions,
        int questionsWithChat,
        int totalExchanges,
        float avgHelpScore,
        float avgTaskEngagement,
        int answerRequestCount,
        int offTopicCount,
        int modelLeakCount)
    {
        if (totalExchanges <= 0)
            return questionsWithChat > 0 ? "Low" : "None";

        if (modelLeakCount > 0)
            return "Compromised";

        int disruptiveCount = answerRequestCount + offTopicCount;
        float disruptiveRatio = disruptiveCount / (float)totalExchanges;

        if (disruptiveRatio >= 0.55f && avgTaskEngagement < 45f)
            return "LowEngagement";

        float usageRatio = totalQuestions > 0 ? questionsWithChat / (float)totalQuestions : 0f;
        if (usageRatio < 0.15f && totalExchanges <= 2)
            return "MinimalUse";

        if (avgHelpScore >= 65f && avgTaskEngagement >= 50f)
            return "High";

        if (avgHelpScore >= 40f)
            return "Medium";

        return "Low";
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }

    static bool MatchesAny(string text, Regex[] patterns)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (var pattern in patterns)
        {
            if (pattern.IsMatch(text))
                return true;
        }

        return false;
    }

    static bool IsStudentOffTopic(string studentMessage, string questionContext, bool requestedAnswer, bool gamingAttempt)
    {
        if (string.IsNullOrEmpty(studentMessage))
            return true;

        if (gamingAttempt)
            return true;

        if (GreetingOnlyPattern.IsMatch(studentMessage))
            return true;

        if (requestedAnswer)
            return false;

        if (studentMessage.Length < 8 && !studentMessage.Contains("?"))
            return true;

        var questionTokens = ExtractSignificantTokens(questionContext);
        if (questionTokens.Count == 0)
            return false;

        var studentTokens = ExtractSignificantTokens(studentMessage);
        if (studentTokens.Count == 0)
            return true;

        foreach (var token in studentTokens)
        {
            if (questionTokens.Contains(token))
                return false;
        }

        return studentMessage.Length >= 12;
    }

    static HashSet<string> ExtractSignificantTokens(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
            return tokens;

        foreach (Match match in Regex.Matches(text.ToLowerInvariant(), @"[\p{L}]{4,}"))
        {
            string word = match.Value;
            if (!StopWords.Contains(word))
                tokens.Add(word);
        }

        return tokens;
    }

    static float ScoreTaskEngagement(string studentMessage, bool requestedAnswer, bool offTopic, bool gamingAttempt)
    {
        if (gamingAttempt)
            return 10f;

        if (offTopic)
            return requestedAnswer ? 35f : 15f;

        if (requestedAnswer)
            return 45f;

        if (string.IsNullOrEmpty(studentMessage))
            return 0f;

        if (studentMessage.Length >= 20)
            return 85f;

        return 70f;
    }

    static float ScoreGuidance(string modelMessage, bool modelLeak)
    {
        if (string.IsNullOrEmpty(modelMessage))
            return 0f;

        float score = 45f;

        if (modelMessage.Contains("?"))
            score += 25f;

        int length = modelMessage.Length;
        if (length >= 30 && length <= 350)
            score += 15f;
        else if (length < 15)
            score -= 15f;

        if (modelLeak)
            score -= 50f;

        return Clamp(score);
    }

    static float ScoreHelp(float guidanceScore, bool modelLeak, float taskEngagementScore)
    {
        if (modelLeak)
            return Clamp(guidanceScore * 0.4f);

        float blended = guidanceScore * 0.75f + taskEngagementScore * 0.25f;
        return Clamp(blended);
    }

    static string ClassifyHelpLevel(float helpScore, bool modelLeak)
    {
        if (modelLeak)
            return "Compromised";

        if (helpScore >= 65f)
            return "High";

        if (helpScore >= 40f)
            return "Medium";

        return "Low";
    }

    static string BuildFlags(ChatExchangeEvaluation evaluation)
    {
        var flags = new List<string>();

        if (evaluation.StudentRequestedAnswer)
            flags.Add("student_answer_request");

        if (evaluation.StudentGamingAttempt)
            flags.Add("student_gaming_attempt");

        if (evaluation.StudentOffTopic)
            flags.Add("student_off_topic");

        if (evaluation.ModelPossibleLeak)
            flags.Add("model_possible_leak");

        if (evaluation.SubstantiveQuestion)
            flags.Add("substantive_question");

        if (!string.IsNullOrWhiteSpace(evaluation.QuestionUtilityLevel))
            flags.Add("utility=" + evaluation.QuestionUtilityLevel.ToLowerInvariant());

        if (evaluation.GuidanceScore >= 65f && !evaluation.ModelPossibleLeak)
            flags.Add("guidance_present");

        return string.Join("|", flags);
    }

    static float ComputeScenarioRelevanceScore(string studentMessage, string questionContext)
    {
        var questionTokens = ExtractSignificantTokens(questionContext);
        if (questionTokens.Count == 0)
            return 50f;

        var studentTokens = ExtractSignificantTokens(studentMessage);
        if (studentTokens.Count == 0)
            return 0f;

        int overlap = 0;
        foreach (var token in studentTokens)
        {
            if (questionTokens.Contains(token))
                overlap++;
        }

        float ratio = overlap / (float)Math.Max(1, studentTokens.Count);
        return Clamp(ratio * 100f);
    }

    static bool IsSubstantiveQuestion(
        string studentMessage,
        bool offTopic,
        bool gamingAttempt,
        bool requestedAnswer)
    {
        if (gamingAttempt || offTopic || requestedAnswer)
            return false;

        if (string.IsNullOrWhiteSpace(studentMessage))
            return false;

        if (GreetingOnlyPattern.IsMatch(studentMessage))
            return false;

        if (studentMessage.Contains("?"))
            return studentMessage.Length >= 12;

        return studentMessage.Length >= 25;
    }

    static string ClassifyQuestionUtilityLevel(
        string studentMessage,
        bool substantiveQuestion,
        bool offTopic,
        bool gamingAttempt,
        bool requestedAnswer,
        float relevanceScore,
        float helpScore)
    {
        if (gamingAttempt)
            return "Wasted";

        if (GreetingOnlyPattern.IsMatch(studentMessage))
            return "Wasted";

        if (offTopic && !requestedAnswer)
            return "Wasted";

        if (requestedAnswer)
            return relevanceScore >= 30f ? "Minimal" : "Wasted";

        if (!substantiveQuestion)
            return "Minimal";

        if (relevanceScore >= 60f && helpScore >= 50f)
            return "HighValue";

        if (relevanceScore >= 35f)
            return "Productive";

        return "Minimal";
    }

    public static bool IsModelAnswerAffirmation(string studentMessage, string modelMessage)
    {
        if (string.IsNullOrWhiteSpace(modelMessage) || !StudentSeeksAnswerValidation(studentMessage))
            return false;

        return MatchesAny(Normalize(modelMessage), ModelAffirmationLeakPatterns);
    }

    public static bool StudentSeeksAnswerValidation(string studentMessage)
    {
        string student = Normalize(studentMessage);
        if (string.IsNullOrEmpty(student))
            return false;

        if (MatchesAny(student, StudentAnswerRequestPatterns))
            return true;

        if (StudentStatingOptionPattern.IsMatch(student))
            return true;

        return student.Contains('?') &&
               Regex.IsMatch(student,
                   @"\b(correcta|opcion|opción|respuesta|ser[ií]a|entonces|seria)\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public const string AnswerAffirmationSafeReply =
        "¡Buena pregunta! Entiendo que querés saber si vas por buen camino. " +
        "En este ejercicio no puedo confirmarte si esa opción es la correcta: mi papel es acompañarte a interpretar las reglas para que llegues a tu propia conclusión, no decirte qué marcar. " +
        "Si repasás el caso, fijate qué condiciones se cumplen y cuáles no. ¿Hay alguna regla que todavía no te quede clara?";

    public const string AnswerDirectRequestSafeReply =
        "Te entiendo, es normal querer cerrar la duda de una vez. " +
        "Acá mi rol no es darte la respuesta ni la letra correcta, sino ayudarte a pensar el caso con las reglas del enunciado — así el razonamiento queda de vos. " +
        "Contame qué parte del caso te confunde y lo vemos juntos, sin spoilear la opción final.";

    public const string ModelLeakSafeReply =
        "Perdón si sonó como que te iba a dar la respuesta — no es la idea. " +
        "En este chat puedo aclararte las reglas y hacerte preguntas guía, pero no puedo indicarte cuál opción elegir. " +
        "¿Qué regla o condición del caso querés que revisemos primero?";

    public static string GetWarmDeclineReply(string studentMessage)
    {
        string student = Normalize(studentMessage);
        if (MatchesAny(student, StudentAnswerRequestPatterns))
            return AnswerDirectRequestSafeReply;

        if (StudentStatingOptionPattern.IsMatch(student) ||
            (student.Contains('?') &&
             Regex.IsMatch(student,
                 @"\b(correcta|opcion|opción|respuesta|ser[ií]a|entonces|seria)\b",
                 RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
            return AnswerAffirmationSafeReply;

        return ModelLeakSafeReply;
    }

    public static bool TryGetSafeReplyForAnswerAffirmation(string studentMessage, string modelMessage, out string safeReply)
    {
        safeReply = null;
        if (!IsModelAnswerAffirmation(studentMessage, modelMessage))
            return false;

        safeReply = GetWarmDeclineReply(studentMessage);
        return true;
    }

    public static bool TryGetSafeReplyForModelLeak(string studentMessage, string modelMessage, out string safeReply)
    {
        safeReply = null;
        if (string.IsNullOrWhiteSpace(modelMessage))
            return false;

        if (!MatchesAny(Normalize(modelMessage), ModelLeakPatterns))
            return false;

        safeReply = GetWarmDeclineReply(studentMessage);
        return true;
    }

    static float Clamp(float value)
    {
        if (value < 0f) return 0f;
        if (value > 100f) return 100f;
        return value;
    }
}
