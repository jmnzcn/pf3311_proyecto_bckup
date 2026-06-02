using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MyProject
{
/// <summary>
/// Drives question flow, answer selection, confidence rating, and experiment progression UI.
/// </summary>
public class QuestionManager : MonoBehaviour
{
    [System.Serializable]
    public class ExperimentQuestion
    {
        [TextArea(5, 15)]
        public string situation;
        public string optA;
        public string optB;
        public string optC;
        public string optD;
        [Tooltip("Letter of the correct option: A, B, C, or D.")]
        public string correctOption;
    }

    [System.Serializable]
    public class ScenarioDefinition
    {
        public string scenarioName;
        public List<ExperimentQuestion> questions = new List<ExperimentQuestion>();
    }

    public static string GetCorrectAnswerText(ExperimentQuestion question)
    {
        if (question == null || string.IsNullOrWhiteSpace(question.correctOption))
            return "";

        switch (question.correctOption.Trim().ToUpperInvariant())
        {
            case "A": return question.optA ?? "";
            case "B": return question.optB ?? "";
            case "C": return question.optC ?? "";
            case "D": return question.optD ?? "";
            default: return "";
        }
    }

    public static string NormalizeOptionLetter(string letter)
    {
        if (string.IsNullOrWhiteSpace(letter))
            return "";

        return letter.Trim().ToUpperInvariant();
    }

    public static string GetCorrectAnswerLetter(ExperimentQuestion question)
    {
        return question == null ? "" : NormalizeOptionLetter(question.correctOption);
    }

    [Header("UI Controls")]
    public TextMeshProUGUI questionTextDisplay;
    public GameObject optionButtonGroup;
    public Button btnA_Button;
    public Button btnB_Button;
    public Button btnC_Button;
    public Button btnD_Button;
    public TextMeshProUGUI btnA_Text;
    public TextMeshProUGUI btnB_Text;
    public TextMeshProUGUI btnC_Text;
    public TextMeshProUGUI btnD_Text;
    public GameObject characterModel;

    [Header("Navigation")]
    public Button nextButton;
    public Button askForHelpButton;
    public TMP_InputField inputField;
    public TextMeshProUGUI questionCounterText;
    public ScrollRect myScrollRect;
    public Image progressBar_Bg;

    [Header("Final Screens")]
    public GameObject finalOptionsPanel;
    public Button surveyButton;
    [Tooltip("When set, Realizar Encuesta opens this URL. Leave empty to keep the button visible but disabled.")]
    public string surveyUrl = "";
    public GameObject exitPopupPanel;
    public GameObject safeExitPopup;
    public GameObject Consent_Overlay;

    [Header("Progress")]
    public Image progressFill;
    public int totalQuestions = 12;

    [Header("Integration")]
    public ExperimentLogic experimentLogic;
    public DataLogger dataLogger;

    [Header("Scenarios")]
    public List<ScenarioDefinition> scenarios = new List<ScenarioDefinition>();

    [Header("Runtime State")]
    public int currentQuestionIndex = 0;
    private string temporaryChoice = "";
    private string temporaryChoiceLetter = "";
    private bool isSubmittingAnswer;
    private List<ExperimentQuestion> activeQuestions;
    private int activeScenarioIndex = -1;
    private int answersSubmittedInCurrentScenario;
    private int completedScenariosInSession;
    private bool isCurrentScenarioComplete;

    public int ActiveScenarioNumber => activeScenarioIndex >= 0 ? activeScenarioIndex + 1 : 0;

    public string ActiveScenarioName
    {
        get
        {
            if (activeScenarioIndex < 0 || scenarios == null || activeScenarioIndex >= scenarios.Count)
                return ActiveScenarioNumber > 0 ? "Escenario " + ActiveScenarioNumber : "";

            var name = scenarios[activeScenarioIndex].scenarioName;
            return string.IsNullOrWhiteSpace(name) ? "Escenario " + ActiveScenarioNumber : name.Trim();
        }
    }

    public int ActiveQuestionCount => activeQuestions != null ? activeQuestions.Count : 0;

    /// <summary>Scenarios B and C (indices 1 and 2) include chat assistance; A does not.</summary>
    public bool IsChatAssistanceEnabled => activeScenarioIndex >= 1;

    [Header("Star Rating System")]
    public GameObject confidencePanel;
    public Image[] starIcons;
    public Color glowColor = Color.cyan;
    public Color dullColor = new Color(0.1f, 0.1f, 0.1f);
    private int currentConfidenceScore = 0;
    public Button confirmRatingButton;

    void Start()
    {
        if (experimentLogic == null) experimentLogic = Object.FindFirstObjectByType<ExperimentLogic>();
        if (dataLogger == null) dataLogger = Object.FindFirstObjectByType<DataLogger>();

        if (nextButton != null) nextButton.interactable = false;
        if (confidencePanel != null) confidencePanel.SetActive(false);

        if (btnA_Button != null)
        {
            btnA_Button.onClick.RemoveAllListeners();
            btnA_Button.onClick.AddListener(() => OnOptionSelected("A", btnA_Text != null ? btnA_Text.text : ""));
        }
        if (btnB_Button != null)
        {
            btnB_Button.onClick.RemoveAllListeners();
            btnB_Button.onClick.AddListener(() => OnOptionSelected("B", btnB_Text != null ? btnB_Text.text : ""));
        }
        if (btnC_Button != null)
        {
            btnC_Button.onClick.RemoveAllListeners();
            btnC_Button.onClick.AddListener(() => OnOptionSelected("C", btnC_Text != null ? btnC_Text.text : ""));
        }
        if (btnD_Button != null)
        {
            btnD_Button.onClick.RemoveAllListeners();
            btnD_Button.onClick.AddListener(() => OnOptionSelected("D", btnD_Text != null ? btnD_Text.text : ""));
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnSiguienteClicked);
        }

        if (confirmRatingButton != null)
        {
            confirmRatingButton.onClick.RemoveAllListeners();
            confirmRatingButton.onClick.AddListener(OnEntregarClicked);
        }

        WireConfidenceStarButtons();
        WireSurveyButton();
    }

    void WireSurveyButton()
    {
        if (surveyButton == null) return;

        surveyButton.onClick.RemoveAllListeners();
        surveyButton.onClick.AddListener(OpenSurvey);
        RefreshSurveyButtonState();
    }

    void RefreshSurveyButtonState()
    {
        if (surveyButton == null) return;

        surveyButton.gameObject.SetActive(true);
        surveyButton.interactable = !string.IsNullOrWhiteSpace(surveyUrl);
    }

    public void OpenSurvey()
    {
        if (string.IsNullOrWhiteSpace(surveyUrl)) return;
        Application.OpenURL(surveyUrl);
    }

    void WireConfidenceStarButtons()
    {
        if (starIcons == null) return;

        for (int i = 0; i < starIcons.Length; i++)
        {
            Image icon = starIcons[i];
            if (icon == null) continue;

            foreach (var label in icon.GetComponentsInChildren<TextMeshProUGUI>(true))
                label.raycastTarget = false;

            Button button = icon.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning("Confidence star " + (i + 1) + " has no Button component.");
                continue;
            }

            int rating = i + 1;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnStarClicked(rating));
        }
    }

    public void BeginScenario(int scenarioIndex)
    {
        activeScenarioIndex = scenarioIndex;
        activeQuestions = null;

        if (scenarios != null && scenarioIndex >= 0 && scenarioIndex < scenarios.Count)
            activeQuestions = scenarios[scenarioIndex].questions;

        if (activeQuestions == null)
            activeQuestions = new List<ExperimentQuestion>();

        if (activeQuestions.Count == 0)
            Debug.LogWarning("Scenario " + (scenarioIndex + 1) + " has no questions configured.");

        currentQuestionIndex = 0;
        totalQuestions = activeQuestions.Count;
        temporaryChoice = "";
        temporaryChoiceLetter = "";
        answersSubmittedInCurrentScenario = 0;
        isCurrentScenarioComplete = false;

        if (characterModel != null)
            characterModel.SetActive(scenarioIndex == 2);

        ApplyChatAssistanceVisibility(scenarioIndex);

        if (dataLogger == null)
            dataLogger = Object.FindFirstObjectByType<DataLogger>();
        if (dataLogger != null)
            dataLogger.ResetQuestionTimer();

        SetOptionButtonsInteractable(true);

        if (activeQuestions.Count > 0)
            DisplayQuestion(activeQuestions[0]);

        UpdateProgressBar();
    }

    void ApplyChatAssistanceVisibility(int scenarioIndex)
    {
        bool showChat = scenarioIndex >= 1;

        if (askForHelpButton != null)
            askForHelpButton.gameObject.SetActive(showChat);

        if (inputField != null)
        {
            inputField.gameObject.SetActive(showChat);
            if (!showChat)
                inputField.text = string.Empty;
        }

        if (experimentLogic != null)
            experimentLogic.SetChatPanelVisible(showChat);
    }

    public ExperimentQuestion GetCurrentQuestion()
    {
        if (activeQuestions == null || currentQuestionIndex < 0 || currentQuestionIndex >= activeQuestions.Count)
            return null;

        return activeQuestions[currentQuestionIndex];
    }

    public void ShowPrimarySaveFailureNotice(string message)
    {
        if (questionTextDisplay == null || string.IsNullOrWhiteSpace(message))
            return;

        var question = GetCurrentQuestion();
        string situation = question != null ? question.situation : string.Empty;

        questionTextDisplay.alignment = TextAlignmentOptions.TopLeft;
        questionTextDisplay.text = string.IsNullOrEmpty(situation)
            ? "<color=#ffaa00>" + message + "</color>"
            : "<color=#ffaa00>" + message + "</color>\n\n" + situation;
    }

    public void OnOptionSelected(string letter, string choiceText)
    {
        if (IsConfidencePanelOpen) return;
        if (string.IsNullOrEmpty(choiceText)) return;

        temporaryChoiceLetter = NormalizeOptionLetter(letter);
        temporaryChoice = choiceText;
        UpdateBtnVisuals(choiceText);
        if (nextButton != null) nextButton.interactable = true;
    }

    private void UpdateBtnVisuals(string selectedChoice)
    {
        Color unselected = new Color(0.15f, 0.15f, 0.15f);
        Color selectedColor = Color.cyan;

        SetButtonColor(btnA_Button, (btnA_Text != null && btnA_Text.text == selectedChoice) ? selectedColor : unselected);
        SetButtonColor(btnB_Button, (btnB_Text != null && btnB_Text.text == selectedChoice) ? selectedColor : unselected);
        SetButtonColor(btnC_Button, (btnC_Text != null && btnC_Text.text == selectedChoice) ? selectedColor : unselected);
        SetButtonColor(btnD_Button, (btnD_Text != null && btnD_Text.text == selectedChoice) ? selectedColor : unselected);
    }

    private void SetButtonColor(Button b, Color c)
    {
        if (b == null) return;
        Image img = b.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    public void OnSiguienteClicked()
    {
        if (string.IsNullOrEmpty(temporaryChoice)) return;

        if (confidencePanel != null)
        {
            confidencePanel.SetActive(true);
            ResetStars();
            SetOptionButtonsInteractable(false);
            if (nextButton != null) nextButton.interactable = false;
        }
    }

    public void OnStarClicked(int rating)
    {
        int maxStars = (starIcons != null) ? starIcons.Length : 0;
        currentConfidenceScore = Mathf.Clamp(rating, 0, Mathf.Max(0, maxStars));

        if (starIcons != null)
        {
            for (int i = 0; i < starIcons.Length; i++)
            {
                if (starIcons[i] == null) continue;
                starIcons[i].color = (i < currentConfidenceScore) ? glowColor : dullColor;
            }
        }

        if (confirmRatingButton != null) confirmRatingButton.interactable = (currentConfidenceScore > 0);
    }

    private void ResetStars()
    {
        currentConfidenceScore = 0;
        if (starIcons != null)
        {
            for (int i = 0; i < starIcons.Length; i++)
            {
                if (starIcons[i] != null) starIcons[i].color = dullColor;
            }
        }
        if (confirmRatingButton != null) confirmRatingButton.interactable = false;
    }

    public void OnEntregarClicked()
    {
        if (isSubmittingAnswer) return;
        if (string.IsNullOrEmpty(temporaryChoice) || string.IsNullOrEmpty(temporaryChoiceLetter) || currentConfidenceScore <= 0) return;

        isSubmittingAnswer = true;
        if (confirmRatingButton != null) confirmRatingButton.interactable = false;

        try
        {
            var question = GetCurrentQuestion();
            string correctAnswer = GetCorrectAnswerText(question);
            string correctAnswerLetter = GetCorrectAnswerLetter(question);

            bool saved = dataLogger != null && dataLogger.SaveAnswer(
                ActiveScenarioNumber,
                ActiveScenarioName,
                currentQuestionIndex + 1,
                temporaryChoiceLetter,
                temporaryChoice,
                currentConfidenceScore,
                correctAnswerLetter,
                correctAnswer);

            if (!saved)
            {
                if (dataLogger == null)
                    Debug.LogError("QuestionManager: DataLogger is not assigned.");

                if (experimentLogic != null)
                    experimentLogic.NotifyDataSaveFailure();

                return;
            }

            answersSubmittedInCurrentScenario++;

            if (experimentLogic != null)
            {
                experimentLogic.SaveDataToCSV(
                    "S" + ActiveScenarioNumber + "_Quest_" + (currentQuestionIndex + 1),
                    temporaryChoice + " | Confianza: " + currentConfidenceScore,
                    correctAnswer,
                    ActiveScenarioNumber,
                    ActiveScenarioName,
                    currentQuestionIndex + 1);
            }

            if (confidencePanel != null) confidencePanel.SetActive(false);

            temporaryChoice = "";
            temporaryChoiceLetter = "";
            if (nextButton != null) nextButton.interactable = false;
            UpdateBtnVisuals("");
            SetOptionButtonsInteractable(true);

            ShowNextQuestion();
        }
        finally
        {
            isSubmittingAnswer = false;

            if (confirmRatingButton != null && confidencePanel != null && confidencePanel.activeInHierarchy)
                confirmRatingButton.interactable = currentConfidenceScore > 0;
        }
    }

    /// <summary>Advances to the next question or ends the experiment when the list is exhausted.</summary>
    public void ShowNextQuestion()
    {
        SetOptionButtonsInteractable(true);

        if (inputField != null) inputField.text = string.Empty;
        if (myScrollRect != null) myScrollRect.verticalNormalizedPosition = 1f;

        if (experimentLogic != null && IsChatAssistanceEnabled)
            experimentLogic.ClearChatHistory();

        if (activeQuestions != null && currentQuestionIndex < activeQuestions.Count - 1)
        {
            currentQuestionIndex++;
            DisplayQuestion(activeQuestions[currentQuestionIndex]);
            UpdateProgressBar();
        }
        else
        {
            FinishExperiment();
        }
    }

    void DisplayQuestion(ExperimentQuestion q)
    {
        if (q == null) return;

        if (questionTextDisplay != null) questionTextDisplay.text = q.situation;

        if (btnA_Text != null) btnA_Text.text = q.optA;
        if (btnB_Text != null) btnB_Text.text = q.optB;

        if (string.IsNullOrEmpty(q.optC))
        {
            if (btnC_Button != null) btnC_Button.gameObject.SetActive(false);
        }
        else
        {
            if (btnC_Button != null) btnC_Button.gameObject.SetActive(true);
            if (btnC_Text != null) btnC_Text.text = q.optC;
        }

        if (string.IsNullOrEmpty(q.optD))
        {
            if (btnD_Button != null) btnD_Button.gameObject.SetActive(false);
        }
        else
        {
            if (btnD_Button != null) btnD_Button.gameObject.SetActive(true);
            if (btnD_Text != null) btnD_Text.text = q.optD;
        }

        if (questionCounterText != null)
            questionCounterText.text = "Pregunta " + (currentQuestionIndex + 1) + " de " + totalQuestions;
    }

    private void FinishExperiment()
    {
        isCurrentScenarioComplete = true;
        completedScenariosInSession++;

        if (questionTextDisplay != null)
        {
            questionTextDisplay.text =
                "\u00a1Listo! Ya completaste este escenario.\n" +
                "Muchas gracias por participar. Nos ayudas un mont\u00f3n.";
            questionTextDisplay.alignment = TextAlignmentOptions.Center;
        }

        // Hide interactive controls once all questions are complete.
        if (questionCounterText != null) questionCounterText.gameObject.SetActive(false);
        if (askForHelpButton != null) askForHelpButton.gameObject.SetActive(false);
        if (inputField != null) inputField.gameObject.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);

        if (experimentLogic != null)
            experimentLogic.SetChatPanelVisible(false);
        if (optionButtonGroup != null) optionButtonGroup.SetActive(false);
        if (progressFill != null) progressFill.gameObject.SetActive(false);
        if (progressBar_Bg != null) progressBar_Bg.gameObject.SetActive(false);

        if (myScrollRect != null && myScrollRect.verticalScrollbar != null)
        {
            myScrollRect.verticalScrollbar.gameObject.SetActive(false);
            myScrollRect.enabled = false;
        }

        if (finalOptionsPanel != null)
        {
            finalOptionsPanel.SetActive(true);
            var rt = finalOptionsPanel.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = Vector2.zero;
        }

        RefreshSurveyButtonState();

        if (characterModel != null) characterModel.SetActive(false);
    }

    public void HandleNoOnExitPopup()
    {
        if (exitPopupPanel != null) exitPopupPanel.SetActive(false);

        if (currentQuestionIndex >= totalQuestions - 1 && totalQuestions > 0)
        {
            if (finalOptionsPanel != null) finalOptionsPanel.SetActive(true);
        }
    }

    public void UpdateProgressBar()
    {
        if (progressFill != null && totalQuestions > 0)
        {
            float progress = (float)(currentQuestionIndex + 1) / totalQuestions;
            progressFill.fillAmount = Mathf.Clamp01(progress);
        }
    }

    public void ResetUIForNewScenario()
    {
        if (finalOptionsPanel != null) finalOptionsPanel.SetActive(false);

        if (questionTextDisplay != null)
        {
            questionTextDisplay.alignment = TextAlignmentOptions.TopLeft;
            questionTextDisplay.text = string.Empty;
        }

        if (myScrollRect != null)
        {
            myScrollRect.enabled = true;
            if (myScrollRect.verticalScrollbar != null)
                myScrollRect.verticalScrollbar.gameObject.SetActive(true);

            myScrollRect.verticalNormalizedPosition = 1f;
        }
        if (progressFill != null) progressFill.gameObject.SetActive(true);
        if (progressBar_Bg != null) progressBar_Bg.gameObject.SetActive(true);

        if (questionCounterText != null) questionCounterText.gameObject.SetActive(true);

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(true);
            nextButton.interactable = false;
        }
        if (optionButtonGroup != null) optionButtonGroup.SetActive(true);

        temporaryChoice = "";
        temporaryChoiceLetter = "";
        currentQuestionIndex = 0;
        activeScenarioIndex = -1;
        activeQuestions = null;
        totalQuestions = 0;

        ApplyChatAssistanceVisibility(-1);
    }

    public void CloseConfidencePopup()
    {
        if (confidencePanel != null) confidencePanel.SetActive(false);

        ResetStars();
        SetOptionButtonsInteractable(true);

        if (nextButton != null)
            nextButton.interactable = !string.IsNullOrEmpty(temporaryChoice);
    }

    bool IsConfidencePanelOpen =>
        confidencePanel != null && confidencePanel.activeInHierarchy;

    void SetOptionButtonsInteractable(bool interactable)
    {
        if (btnA_Button != null) btnA_Button.interactable = interactable;
        if (btnB_Button != null) btnB_Button.interactable = interactable;
        if (btnC_Button != null) btnC_Button.interactable = interactable;
        if (btnD_Button != null) btnD_Button.interactable = interactable;
    }

    /// <summary>
    /// True when safe exit should discard the session CSV (first scenario never fully completed).
    /// </summary>
    bool ShouldDeleteSessionDataOnExit()
    {
        if (isCurrentScenarioComplete)
            return false;

        if (finalOptionsPanel != null && finalOptionsPanel.activeSelf)
            return false;

        // Scenario selection after "Otro escenario"; prior completed rows stay valid.
        if (activeScenarioIndex < 0)
            return false;

        if (totalQuestions <= 0)
            return false;

        if (answersSubmittedInCurrentScenario >= totalQuestions)
            return false;

        // Mid-session after a completed scenario: keep CSV; partial rows remain for filtering.
        if (completedScenariosInSession > 0)
            return false;

        return true;
    }

    public void SafeExit()
    {
        if (ShouldDeleteSessionDataOnExit())
        {
            DataLogger logger = dataLogger != null ? dataLogger : FindFirstObjectByType<DataLogger>();
            if (logger != null)
                logger.DeleteIncompleteFile();
        }

#if UNITY_EDITOR
        Debug.Log("Exiting Safely...");
#endif
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenExitPopup()
    {
        if (safeExitPopup != null) safeExitPopup.SetActive(true);
        if (characterModel != null) characterModel.SetActive(false);
    }

    public void CloseExitPopup()
    {
        if (safeExitPopup != null) safeExitPopup.SetActive(false);

        if (Consent_Overlay != null && !Consent_Overlay.activeInHierarchy)
        {
            if (characterModel != null)
                characterModel.SetActive(activeScenarioIndex == 2);
        }
    }
}
}