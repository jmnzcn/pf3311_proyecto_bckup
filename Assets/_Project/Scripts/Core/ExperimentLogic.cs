using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using MyProject;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Consent, session ID, Gemini chat agent, and secondary CSV export.
/// Question flow UI is owned by <see cref="QuestionManager"/>.
/// </summary>
public class ExperimentLogic : MonoBehaviour
{
    static readonly object LogsFileLock = new object();

    [Header("Chat UI")]
    public ScrollRect chatScrollRect;
    public TextMeshProUGUI hintText;

    [Header("Session UI")]
    public GameObject mainGameUI;
    public Toggle consentToggle;
    public Image consentBackground;
    public Button continueButton;
    public TextMeshProUGUI userIdText;

    [Header("Integration")]
    public QuestionManager questionManager;

    [Header("API Config")]
    public string apiKey = "YOUR_GEMINI_API_KEY_HERE";
    public string endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=";

    private string currentUserID;
    private string chatHistory = "";
    private float questionStartTime = 0f;
    private bool geminiInFlight;
    private readonly List<ApiChatTurn> apiTurns = new List<ApiChatTurn>();
    private static float lastSaveFailureNoticeTime = -999f;

    const int MaxApiTurnsPerQuestion = 20;
    const string SaveFailureParticipantMessage =
        "No se pudo guardar tu respuesta en el registro de la sesión. Cerrá Excel u otros programas que puedan tener abierto el archivo de datos e intentá entregar de nuevo. Si el problema continúa, contactá al investigador del estudio.";

    TMP_InputField ChatInput =>
        questionManager != null ? questionManager.inputField : null;

    Button SendChatButton =>
        questionManager != null ? questionManager.askForHelpButton : null;

    void UpdateConsentBackground(bool isSelected)
    {
        if (consentBackground != null)
            consentBackground.color = isSelected ? Color.cyan : new Color(0.15f, 0.15f, 0.15f);
    }

    void Start()
    {
        if (consentToggle != null)
            consentToggle.onValueChanged.AddListener(UpdateConsentBackground);

        if (questionManager == null)
            questionManager = UnityEngine.Object.FindFirstObjectByType<QuestionManager>();

        if (mainGameUI != null)
            mainGameUI.SetActive(false);

        if (SendChatButton != null)
            SendChatButton.interactable = false;

        if (ChatInput != null)
            ChatInput.onValueChanged.AddListener(OnUserInputValueChanged);

        questionStartTime = Time.time;

        if (hintText != null)
        {
            hintText.text = "Aquí verás tu conversación con el agente…";
            hintText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        }
    }

    string GetCurrentScenarioText()
    {
        if (questionManager == null)
        {
            Debug.LogWarning("QuestionManager not found!");
            return "";
        }

        if (questionManager.ActiveScenarioNumber <= 0)
            return "";

        var question = questionManager.GetCurrentQuestion();
        if (question == null)
        {
            Debug.LogWarning("No active question for the current scenario.");
            return "";
        }

        return question.situation;
    }

    void OnUserInputValueChanged(string inputText)
    {
        if (SendChatButton == null)
            return;

        SendChatButton.interactable = !geminiInFlight && !string.IsNullOrWhiteSpace(inputText);
    }

    void SetGeminiInFlight(bool inFlight)
    {
        geminiInFlight = inFlight;

        if (ChatInput != null)
            ChatInput.interactable = !inFlight;

        if (SendChatButton != null)
            SendChatButton.interactable = !inFlight &&
                ChatInput != null && !string.IsNullOrWhiteSpace(ChatInput.text);
    }

    void Update()
    {
        if (continueButton != null && consentToggle != null)
        {
            continueButton.interactable = consentToggle.isOn;
            UpdateConsentBackground(consentToggle.isOn);
        }
    }

    /// <summary>Starts scenario 0 (A), 1 (B), or 2 (C). Wire each INICIAR button with the matching int in the Inspector.</summary>
    public void OnScenarioSelected(int scenarioIndex)
    {
        if (consentToggle != null && !consentToggle.isOn)
            return;

        EnsureSessionUserId();

        if (questionManager != null)
            questionManager.BeginScenario(scenarioIndex);

        if (mainGameUI != null)
            mainGameUI.SetActive(true);

        if (questionManager != null && questionManager.finalOptionsPanel != null)
            questionManager.finalOptionsPanel.SetActive(false);

        if (questionManager != null && questionManager.Consent_Overlay != null)
            questionManager.Consent_Overlay.SetActive(false);

        var avatarDisplay = UnityEngine.Object.FindFirstObjectByType<AvatarDisplayController>();
        if (avatarDisplay != null)
            StartCoroutine(RefreshAvatarDisplayNextFrame(avatarDisplay));

        ResetQuestionTimer();
    }

    static IEnumerator RefreshAvatarDisplayNextFrame(AvatarDisplayController avatarDisplay)
    {
        yield return null;
        if (avatarDisplay != null)
            avatarDisplay.RefreshDisplay();
    }

    public void AskForHelp()
    {
        if (questionManager != null && !questionManager.IsChatAssistanceEnabled)
            return;

        if (geminiInFlight)
            return;

        if (ChatInput == null || string.IsNullOrWhiteSpace(ChatInput.text))
            return;

        string studentText = ChatInput.text.Trim();
        ChatInput.text = "";

        StartCoroutine(CallGemini(studentText));
    }

    static string BuildSystemPreamble(string scenarioText)
    {
        return "Contexto del Escenario Actual: " + scenarioText + ". " +
            "INSTRUCCIÓN: Eres un profesor humano y empático. El estudiante te está hablando en un chat. " +
            "REGLA ABSOLUTA Y ESTRICTA: BAJO NINGUNA CIRCUNSTANCIA debes darle la respuesta final o la opción correcta al estudiante, " +
            "incluso si te lo pide directamente o te ruega. Tu único objetivo es GUIARLO para que deduzca la respuesta por sí mismo. " +
            "Si te pide la respuesta directa, dile amablemente que tu trabajo es ayudarle a pensar, y hazle una pregunta guía sobre la situación. " +
            "REGLA DE SALUDOS: NO uses saludos (como 'Hola', 'Buenos días', etc.) al inicio de tu respuesta, A MENOS que el estudiante te haya saludado primero en su mensaje. Si el estudiante no te saluda, ve directo al grano. " +
            "Responde de forma natural y dándole una pequeña pista. Mantén la respuesta breve (máximo 2 o 3 oraciones).";
    }

    IEnumerator CallGemini(string userMessage)
    {
        SetGeminiInFlight(true);

        try
        {
            RefreshPendingStudentMessage(userMessage);

            string url = endpoint + apiKey;
            string json = BuildGeminiRequestJson(BuildSystemPreamble(GetCurrentScenarioText()), userMessage, apiTurns);

            int maxRetries = 3;
            int retryDelay = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
                {
                    byte[] body = Encoding.UTF8.GetBytes(json);
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string rawJson = request.downloadHandler?.text ?? "";
                        GeminiResponse response = null;
                        try { response = JsonUtility.FromJson<GeminiResponse>(rawJson); } catch { }

                        if (response != null && response.candidates != null && response.candidates.Length > 0 &&
                            response.candidates[0].content != null &&
                            response.candidates[0].content.parts != null &&
                            response.candidates[0].content.parts.Length > 0)
                        {
                            string answer = response.candidates[0].content.parts[0].text;
                            RecordApiTurn("user", userMessage);
                            CommitStudentMessage(userMessage);
                            ProcessResponse(answer);
                            yield break;
                        }

                        Debug.LogWarning("Gemini: Empty response.");
                        RevertPendingStudentMessage();
                        AppendChatNotice("No recibí respuesta del agente. Intenta enviar tu mensaje de nuevo.");
                        yield break;
                    }

                    long code = request.responseCode;

                    if (code == 429)
                    {
                        Debug.LogWarning("Gemini API 429 Rate Limit hit.");
                        RevertPendingStudentMessage();
                        AppendChatNotice("El agente recibió demasiadas consultas seguidas. Espera unos segundos e inténtalo de nuevo.");
                        yield break;
                    }

                    if (code == 503 && attempt < maxRetries)
                    {
                        RefreshPendingStudentMessage(userMessage,
                            $"<color=yellow>Reintentando ({attempt}/{maxRetries})...</color>");
                        yield return new WaitForSeconds(retryDelay);
                    }
                    else
                    {
                        Debug.LogError($"Gemini API Error: {code}");
                        RevertPendingStudentMessage();
                        AppendChatNotice("No pude conectar con el agente en este momento. Revisa tu conexión e inténtalo de nuevo.");
                        yield break;
                    }
                }
            }

            RevertPendingStudentMessage();
        }
        finally
        {
            SetGeminiInFlight(false);
        }
    }

    void RefreshPendingStudentMessage(string userMessage, string statusLine = null)
    {
        string pending = chatHistory + "\n<b>Estudiante:</b> " + userMessage + "\n";
        if (!string.IsNullOrEmpty(statusLine))
            pending += statusLine + "\n";
        else
            pending += "<i>Enviando...</i>\n";

        RefreshChatDisplay(pending);
    }

    void RevertPendingStudentMessage()
    {
        RefreshChatDisplay(chatHistory);
    }

    void CommitStudentMessage(string userMessage)
    {
        chatHistory += "\n<b>Estudiante:</b> " + userMessage + "\n";
    }

    void RecordApiTurn(string role, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        apiTurns.Add(new ApiChatTurn { role = role, text = text.Trim() });

        while (apiTurns.Count > MaxApiTurnsPerQuestion)
            apiTurns.RemoveAt(0);
    }

    void ProcessResponse(string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return;

        string cleanText = rawText.Replace("*", "").Replace("#", "").Replace("\"", "").Trim();
        chatHistory += "\nProfesor: " + cleanText + "\n";
        RefreshChatDisplay(chatHistory);
        RecordApiTurn("model", cleanText);

        AgentSpeechController speech = GetComponent<AgentSpeechController>();
        if (speech == null)
            speech = UnityEngine.Object.FindFirstObjectByType<AgentSpeechController>();

        if (speech != null && questionManager != null && questionManager.characterModel != null &&
            questionManager.characterModel.activeInHierarchy)
        {
            speech.Speak(cleanText);
        }
    }

    /// <summary>Secondary CSV export to Logs/ (legacy schema). Primary analysis uses <see cref="DataLogger"/>.</summary>
    public void SaveDataToCSV(string actionType, string actionDetail, string correctAnswer = "", int scenarioNumber = 0, string scenarioName = "", int questionNumber = 0)
    {
        try
        {
            string directory = Path.Combine(Application.dataPath, "..", "Logs");
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string userId = currentUserID ?? "unknown";
            string filePath = Path.Combine(directory, "ExperimentData_" + userId + ".csv");
            bool isNewFile = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;

            if (questionNumber <= 0 && questionManager != null)
                questionNumber = questionManager.currentQuestionIndex + 1;

            float timeSpent = Time.time - questionStartTime;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            const string header = "UserID,ScenarioNumber,ScenarioName,QuestionNumber,ActionType,ActionDetail,CorrectAnswer,TimeSpent(s),Timestamp";
            string row = string.Join(",",
                EscapeCsvField(userId),
                scenarioNumber.ToString(),
                EscapeCsvField(scenarioName),
                questionNumber.ToString(),
                EscapeCsvField(actionType),
                EscapeCsvField(actionDetail),
                EscapeCsvField(correctAnswer),
                timeSpent.ToString("F2"),
                EscapeCsvField(timestamp));

            if (isNewFile)
                WriteLogsCsvText(filePath, header + "\n" + row + "\n", new UTF8Encoding(true));
            else
                AppendLogsCsvLine(filePath, row + "\n", new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Debug.LogError("SaveDataToCSV failed: " + ex.Message);
            NotifyDataSaveFailure();
        }
    }

    static void AppendLogsCsvLine(string path, string line, Encoding encoding)
    {
        ExecuteLogsWriteWithRetry(() =>
        {
            lock (LogsFileLock)
            {
                using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, encoding))
                {
                    writer.Write(line);
                }
            }
        });
    }

    static void WriteLogsCsvText(string path, string text, Encoding encoding)
    {
        ExecuteLogsWriteWithRetry(() =>
        {
            lock (LogsFileLock)
            {
                File.WriteAllText(path, text, encoding);
            }
        });
    }

    static void ExecuteLogsWriteWithRetry(Action action)
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

    static string EscapeCsvField(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    public void ResetQuestionTimer() { questionStartTime = Time.time; }

    void EnsureSessionUserId()
    {
        if (!string.IsNullOrEmpty(currentUserID))
        {
            ApplyUserIdToUI();
            return;
        }

        currentUserID = "ID-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + UnityEngine.Random.Range(0, 10000).ToString("D4");
        ApplyUserIdToUI();
    }

    void ApplyUserIdToUI()
    {
        if (userIdText != null && !string.IsNullOrEmpty(currentUserID))
            userIdText.text = "ID de sesión: " + currentUserID;
    }

    public string GetSessionUserId()
    {
        if (string.IsNullOrEmpty(currentUserID))
            return "Unknown";

        return currentUserID;
    }

    public void ResetForNewScenario()
    {
        chatHistory = "";
        apiTurns.Clear();
        if (hintText != null) hintText.text = "Aquí verás tu conversación con el agente…";
        if (ChatInput != null) ChatInput.text = "";
        if (questionManager != null) questionManager.ResetUIForNewScenario();
        if (mainGameUI != null) mainGameUI.SetActive(true);
        if (questionManager != null && questionManager.finalOptionsPanel != null)
            questionManager.finalOptionsPanel.SetActive(false);

        if (questionManager != null && questionManager.characterModel != null)
            questionManager.characterModel.SetActive(false);
    }

    public void ResetToScenarioSelection()
    {
        ResetForNewScenario();
    }

    public void FinalizeAndResetSession() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }

    public void SetChatPanelVisible(bool visible)
    {
        if (chatScrollRect != null)
        {
            chatScrollRect.gameObject.SetActive(visible);
            if (visible)
            {
                chatScrollRect.enabled = true;
                if (chatScrollRect.verticalScrollbar != null)
                    chatScrollRect.verticalScrollbar.gameObject.SetActive(true);

                var chatBackground = chatScrollRect.GetComponent<Image>();
                if (chatBackground != null)
                    chatBackground.raycastTarget = false;

                EnsureChatPanelDrawOrder();
            }
        }

        if (!visible)
        {
            if (ChatInput != null)
                ChatInput.text = string.Empty;

            if (SendChatButton != null)
                SendChatButton.interactable = false;

            ClearChatHistory();
        }
        else
        {
            chatHistory = "";
            apiTurns.Clear();
            RefreshChatDisplay("Aquí verás tu conversación con el agente…", new Color(0.7f, 0.7f, 0.7f, 1f));
        }
    }

    void EnsureChatPanelDrawOrder()
    {
        if (chatScrollRect == null || questionManager == null || questionManager.optionButtonGroup == null)
            return;

        Transform chatTransform = chatScrollRect.transform;
        Transform optionsRoot = questionManager.optionButtonGroup.transform.parent;
        if (optionsRoot == null || chatTransform.parent != optionsRoot.parent)
            return;

        chatTransform.SetSiblingIndex(optionsRoot.GetSiblingIndex() + 1);
    }

    void RefreshChatDisplay(string text, Color? color = null)
    {
        if (hintText == null)
            return;

        hintText.gameObject.SetActive(true);
        hintText.text = text;
        if (color.HasValue)
            hintText.color = color.Value;

        hintText.ForceMeshUpdate(true, true);
        Canvas.ForceUpdateCanvases();

        if (chatScrollRect != null && chatScrollRect.gameObject.activeInHierarchy)
            StartCoroutine(ScrollToBottomRoutine());
    }

    public void ClearChatHistory()
    {
        chatHistory = "";
        apiTurns.Clear();
        if (hintText == null || chatScrollRect == null || !chatScrollRect.gameObject.activeInHierarchy)
            return;

        RefreshChatDisplay("Aquí verás tu conversación con el agente…", new Color(0.7f, 0.7f, 0.7f, 1f));
    }

    public void AppendChatNotice(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        chatHistory += "\n<color=#ffaa00>" + message + "</color>\n";
        RefreshChatDisplay(chatHistory);
    }

    public void NotifyDataSaveFailure()
    {
        if (Time.unscaledTime - lastSaveFailureNoticeTime < 2f)
            return;

        lastSaveFailureNoticeTime = Time.unscaledTime;

        if (questionManager != null && !questionManager.IsChatAssistanceEnabled)
        {
            questionManager.ShowPrimarySaveFailureNotice(SaveFailureParticipantMessage);
            return;
        }

        if (hintText != null)
            hintText.gameObject.SetActive(true);

        AppendChatNotice(SaveFailureParticipantMessage);
    }

    IEnumerator ScrollToBottomRoutine()
    {
        yield return new WaitForEndOfFrame();

        if (chatScrollRect != null)
            chatScrollRect.verticalNormalizedPosition = 0f;
    }

    #region GeminiApiTypes

    static string BuildGeminiRequestJson(string systemPreamble, string newUserMessage, List<ApiChatTurn> priorTurns)
    {
        var request = new GeminiGenerateRequest
        {
            contents = BuildConversationContents(systemPreamble, newUserMessage, priorTurns),
            generationConfig = new GeminiGenerationConfig
            {
                maxOutputTokens = 2000,
                temperature = 0.7f
            }
        };

        return JsonUtility.ToJson(request);
    }

    static GeminiContent[] BuildConversationContents(string systemPreamble, string newUserMessage, List<ApiChatTurn> priorTurns)
    {
        var contents = new List<GeminiContent>();

        if (priorTurns == null || priorTurns.Count == 0)
        {
            contents.Add(MakeGeminiContent("user",
                systemPreamble + "\n\nMensaje del estudiante: " + (newUserMessage ?? "")));
            return contents.ToArray();
        }

        for (int i = 0; i < priorTurns.Count; i++)
        {
            var turn = priorTurns[i];
            if (i == 0 && turn.role == "user")
            {
                contents.Add(MakeGeminiContent("user",
                    systemPreamble + "\n\nMensaje del estudiante: " + turn.text));
            }
            else
            {
                contents.Add(MakeGeminiContent(turn.role, turn.text));
            }
        }

        contents.Add(MakeGeminiContent("user", newUserMessage ?? ""));
        return contents.ToArray();
    }

    static GeminiContent MakeGeminiContent(string role, string text)
    {
        return new GeminiContent
        {
            role = role,
            parts = new[] { new GeminiPart { text = text ?? "" } }
        };
    }

    [Serializable]
    class ApiChatTurn
    {
        public string role;
        public string text;
    }

    [Serializable]
    class GeminiGenerateRequest
    {
        public GeminiContent[] contents;
        public GeminiGenerationConfig generationConfig;
    }

    [Serializable]
    class GeminiGenerationConfig
    {
        public int maxOutputTokens;
        public float temperature;
    }

    [Serializable] public class GeminiResponse { public Candidate[] candidates; }
    [Serializable] public class Candidate { public Content content; }
    [Serializable] public class Content { public Part[] parts; }
    [Serializable] public class Part { public string text; }
    [Serializable] class GeminiContent { public string role; public GeminiPart[] parts; }
    [Serializable] class GeminiPart { public string text; }

    #endregion
}
