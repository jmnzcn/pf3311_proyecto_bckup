using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        [Tooltip("Google Form to open after completing this block (Realizar Encuesta).")]
        public string surveyUrl = "";
        [Tooltip("Form field id for «Código de participante» (entry.XXXXXXXX from the form HTML).")]
        public string surveyCodeEntryId = "";
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
    [Tooltip("Mouse wheel / trackpad scroll speed for the question text area.")]
    [SerializeField] float questionScrollSensitivity = 45f;
    public Image progressBar_Bg;

    [Header("Final Screens")]
    public GameObject finalOptionsPanel;
    public Button surveyButton;
    public Button anotherScenarioButton;
    public Button finalizeSessionButton;
    [Tooltip("Fallback survey URL when the active scenario has no surveyUrl set.")]
    public string surveyUrl = "";
    [Tooltip("Fallback entry.XXXXXXXX for participant code when using surveyUrl fallback.")]
    public string surveyCodeEntryId = "";
    public GameObject exitPopupPanel;
    public GameObject safeExitPopup;
    public GameObject farewellPanel;
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
    private bool isPracticeMode;
    private int practiceAgentProfileIndex;
    private bool postBlockSurveySubmitted;
    private bool postBlockSurveyAwaitingVerification;
    private TextMeshProUGUI surveyButtonTitle;
    const string SurveyButtonDefaultTitle = "Realizar Encuesta";
    const string SurveyButtonConfirmTitle = "CONFIRMAR ENVÍO";
    public bool IsPracticeMode => isPracticeMode;

    public int ActiveScenarioNumber => isPracticeMode ? 0 : (activeScenarioIndex >= 0 ? activeScenarioIndex + 1 : 0);

    public string ActiveConditionCode
    {
        get
        {
            if (isPracticeMode)
                return "P";

            if (activeScenarioIndex >= 0)
            {
                return activeScenarioIndex switch
                {
                    0 => "A",
                    1 => "B",
                    2 => "C",
                    _ => ""
                };
            }

            if (experimentLogic != null)
            {
                string pending = experimentLogic.GetFirstPendingPostBlockSurveyCondition();
                if (!string.IsNullOrEmpty(pending))
                    return pending;
            }

            return "";
        }
    }

    public static int ConditionCodeToScenarioIndex(string conditionCode) =>
        conditionCode switch
        {
            "A" => 0,
            "B" => 1,
            "C" => 2,
            _ => -1
        };

    public bool ScenarioRequiresPostBlockSurvey(int scenarioIndex)
    {
        if (scenarioIndex < 0)
            return false;

        return !string.IsNullOrWhiteSpace(GetScenarioSurveyUrl(scenarioIndex));
    }

    public string GetScenarioSurveyUrl(int scenarioIndex)
    {
        if (scenarios != null && scenarioIndex >= 0 && scenarioIndex < scenarios.Count)
        {
            var url = scenarios[scenarioIndex].surveyUrl;
            if (!string.IsNullOrWhiteSpace(url))
                return url.Trim();
        }

        return string.IsNullOrWhiteSpace(surveyUrl) ? "" : surveyUrl.Trim();
    }

    public string GetScenarioSurveyCodeEntryId(int scenarioIndex)
    {
        if (scenarios != null && scenarioIndex >= 0 && scenarioIndex < scenarios.Count)
        {
            var entryId = scenarios[scenarioIndex].surveyCodeEntryId;
            if (!string.IsNullOrWhiteSpace(entryId))
                return entryId.Trim();
        }

        return string.IsNullOrWhiteSpace(surveyCodeEntryId) ? "" : surveyCodeEntryId.Trim();
    }

    int ResolveSurveyScenarioIndex()
    {
        if (activeScenarioIndex >= 0)
            return activeScenarioIndex;

        if (experimentLogic != null)
        {
            string pending = experimentLogic.GetFirstPendingPostBlockSurveyCondition();
            int idx = ConditionCodeToScenarioIndex(pending);
            if (idx >= 0)
                return idx;
        }

        return activeScenarioIndex;
    }

    public string ActiveScenarioName
    {
        get
        {
            if (isPracticeMode)
                return "Práctica";

            if (activeScenarioIndex < 0 || scenarios == null || activeScenarioIndex >= scenarios.Count)
                return ActiveScenarioNumber > 0 ? "Escenario " + ActiveScenarioNumber : "";

            var name = scenarios[activeScenarioIndex].scenarioName;
            return string.IsNullOrWhiteSpace(name) ? "Escenario " + ActiveScenarioNumber : name.Trim();
        }
    }

    public int ActiveQuestionCount => activeQuestions != null ? activeQuestions.Count : 0;

    /// <summary>Scenarios B and C (indices 1 and 2) include chat assistance; A does not.</summary>
    public bool IsChatAssistanceEnabled =>
        isPracticeMode ? practiceAgentProfileIndex >= 1 : activeScenarioIndex >= 1;

    [Header("Star Rating System")]
    public GameObject confidencePanel;
    public Image[] starIcons;
    public Color glowColor = new Color(0.35f, 1f, 1f, 1f);
    public Color dullColor = new Color(0.22f, 0.28f, 0.30f, 0.72f);
    public Color starLabelSelectedColor = new Color(0.04f, 0.07f, 0.09f, 1f);
    public Color starLabelDullColor = new Color(0.55f, 0.68f, 0.72f, 0.85f);
    private int currentConfidenceScore = 0;
    public Button confirmRatingButton;

    void Start()
    {
        if (experimentLogic == null) experimentLogic = UnityEngine.Object.FindFirstObjectByType<ExperimentLogic>();
        if (dataLogger == null) dataLogger = UnityEngine.Object.FindFirstObjectByType<DataLogger>();

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
        WireFinalOptionsButtons();
        ConfigureQuestionScroll();
    }

    void ConfigureQuestionScroll()
    {
        if (myScrollRect == null)
            return;

        myScrollRect.horizontal = false;
        myScrollRect.vertical = true;
        myScrollRect.scrollSensitivity = questionScrollSensitivity;
        myScrollRect.movementType = ScrollRect.MovementType.Clamped;

        if (myScrollRect.horizontalScrollbar != null)
            myScrollRect.horizontalScrollbar.gameObject.SetActive(false);

        if (myScrollRect.viewport != null)
        {
            var viewport = myScrollRect.viewport;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0f, 1f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-18f, 0f);
        }

        ConfigureQuestionScrollContentLayout();

        if (questionTextDisplay != null)
            questionTextDisplay.isTextObjectScaleStatic = true;
    }

    void ConfigureQuestionScrollContentLayout()
    {
        if (myScrollRect == null || myScrollRect.content == null)
            return;

        var content = myScrollRect.content;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var contentFitter = content.GetComponent<ContentSizeFitter>();
        if (contentFitter != null)
        {
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        var contentLayout = content.GetComponent<VerticalLayoutGroup>();
        if (contentLayout != null)
        {
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.spacing = 0;
            contentLayout.padding.left = 12;
            contentLayout.padding.right = 8;
            contentLayout.padding.top = 8;
            contentLayout.padding.bottom = 12;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
        }

        ConfigureQuestionTextLayout();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        myScrollRect.verticalNormalizedPosition = 1f;
    }

    void ConfigureQuestionTextLayout()
    {
        if (questionTextDisplay == null)
            return;

        var textRect = questionTextDisplay.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        questionTextDisplay.margin = Vector4.zero;
        questionTextDisplay.textWrappingMode = TextWrappingModes.Normal;
        questionTextDisplay.horizontalAlignment = HorizontalAlignmentOptions.Left;
        questionTextDisplay.verticalAlignment = VerticalAlignmentOptions.Top;

        var fitter = questionTextDisplay.GetComponent<ContentSizeFitter>() ??
                     questionTextDisplay.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layoutElement = questionTextDisplay.GetComponent<LayoutElement>() ??
                            questionTextDisplay.gameObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = 0f;
        layoutElement.preferredWidth = -1f;
        layoutElement.flexibleWidth = 1f;
    }

    void WireFinalOptionsButtons()
    {
        if (anotherScenarioButton != null)
        {
            anotherScenarioButton.onClick.RemoveAllListeners();
            anotherScenarioButton.onClick.AddListener(OnAnotherScenarioClicked);
        }

        if (finalizeSessionButton != null)
        {
            finalizeSessionButton.onClick.RemoveAllListeners();
            finalizeSessionButton.onClick.AddListener(OnFinalizeSessionClicked);
        }
    }

    void WireSurveyButton()
    {
        if (surveyButton == null) return;

        surveyButton.onClick.RemoveAllListeners();
        surveyButton.onClick.AddListener(OnSurveyButtonClicked);
        RefreshFinalOptionsPanelState();
    }

    void OnSurveyButtonClicked()
    {
        if (postBlockSurveyAwaitingVerification && !postBlockSurveySubmitted)
        {
            if (experimentLogic != null && !experimentLogic.HasFormResponseVerification)
                ConfirmPostBlockSurveySubmissionManually();
            return;
        }

        OpenSurvey();
    }

    string GetActiveScenarioSurveyCodeEntryId() =>
        GetScenarioSurveyCodeEntryId(ResolveSurveyScenarioIndex());

    string GetActiveScenarioSurveyUrl() =>
        GetScenarioSurveyUrl(ResolveSurveyScenarioIndex());

    string GetSurveyVerificationConditionCode()
    {
        int scenarioIndex = ResolveSurveyScenarioIndex();
        return scenarioIndex switch
        {
            0 => "A",
            1 => "B",
            2 => "C",
            _ => ActiveConditionCode
        };
    }

    bool RequiresPostBlockSurvey() =>
        !isPracticeMode && ScenarioRequiresPostBlockSurvey(ResolveSurveyScenarioIndex());

    string BuildSurveyOpenUrl()
    {
        var url = GetActiveScenarioSurveyUrl();
        if (string.IsNullOrWhiteSpace(url))
            return "";

        string participantCode = experimentLogic != null ? experimentLogic.GetParticipantCode() : "";
        return SurveyLinkBuilder.BuildPrefilledUrl(
            url,
            GetActiveScenarioSurveyCodeEntryId(),
            participantCode);
    }

    public bool CanLeaveBlockCompletionScreen()
    {
        if (!RequiresPostBlockSurvey())
            return true;

        string conditionCode = GetSurveyVerificationConditionCode();
        if (experimentLogic != null && !string.IsNullOrEmpty(conditionCode)
            && experimentLogic.IsPostBlockSurveySubmitted(conditionCode))
            return true;

        return postBlockSurveySubmitted;
    }

    public bool IsAwaitingPostBlockSurveyVerification => postBlockSurveyAwaitingVerification;

    public bool CanFinalizeSession()
    {
        if (experimentLogic == null || !experimentLogic.AreAllConditionsCompleted)
            return false;

        return experimentLogic.AreAllRequiredPostBlockSurveysSubmitted();
    }

    void RefreshSurveyButtonLabel(bool manualConfirmMode, bool completed, bool awaitingVerification)
    {
        if (surveyButtonTitle == null && surveyButton != null)
            surveyButtonTitle = surveyButton.GetComponentInChildren<TextMeshProUGUI>();

        if (surveyButtonTitle == null)
            return;

        surveyButtonTitle.text = manualConfirmMode
            ? SurveyButtonConfirmTitle
            : completed
                ? "ENCUESTA COMPLETADA"
                : SurveyButtonDefaultTitle;
    }

    void RefreshFinalOptionsPanelState()
    {
        bool requiresSurvey = RequiresPostBlockSurvey();
        bool allBlocksDone = experimentLogic != null && experimentLogic.AreAllConditionsCompleted;
        string conditionCode = GetSurveyVerificationConditionCode();
        bool completedForCondition = postBlockSurveySubmitted
                                     || (experimentLogic != null
                                         && !string.IsNullOrEmpty(conditionCode)
                                         && experimentLogic.IsPostBlockSurveySubmitted(conditionCode));
        bool manualConfirmMode = postBlockSurveyAwaitingVerification
                                 && !completedForCondition
                                 && experimentLogic != null
                                 && !experimentLogic.HasFormResponseVerification;

        if (surveyButton != null)
        {
            bool showSurveyAction = requiresSurvey
                                    || (allBlocksDone
                                        && experimentLogic != null
                                        && !experimentLogic.AreAllRequiredPostBlockSurveysSubmitted());
            surveyButton.gameObject.SetActive(showSurveyAction);
            surveyButton.interactable = showSurveyAction
                                          && !completedForCondition
                                          && (!postBlockSurveyAwaitingVerification || manualConfirmMode);
            RefreshSurveyButtonLabel(manualConfirmMode, completedForCondition, postBlockSurveyAwaitingVerification);
        }

        if (anotherScenarioButton != null)
        {
            anotherScenarioButton.gameObject.SetActive(!allBlocksDone);
            anotherScenarioButton.interactable = !allBlocksDone && CanLeaveBlockCompletionScreen();
        }

        if (finalizeSessionButton != null)
            finalizeSessionButton.interactable = IsFarewellScreenVisible() || CanFinalizeSession();
    }

    bool IsFarewellScreenVisible() =>
        farewellPanel != null && farewellPanel.activeInHierarchy;

    public void ShowFarewellScreen()
    {
        if (experimentLogic != null)
            experimentLogic.DeactivateScenarioSelectionOverlay();

        if (exitPopupPanel != null)
            exitPopupPanel.SetActive(false);

        if (safeExitPopup != null)
            safeExitPopup.SetActive(false);

        if (finalOptionsPanel != null)
            finalOptionsPanel.SetActive(false);

        if (farewellPanel != null)
        {
            farewellPanel.SetActive(true);
            BringPanelAboveBlockingOverlays(farewellPanel.transform);
        }

        if (finalizeSessionButton != null)
            finalizeSessionButton.interactable = true;
    }

    static void BringPanelAboveBlockingOverlays(Transform panel)
    {
        if (panel == null || panel.parent == null)
            return;

        Transform canvas = panel.parent;
        Transform exitButton = null;
        for (int i = 0; i < canvas.childCount; i++)
        {
            Transform child = canvas.GetChild(i);
            if (child.name == "Exit_Button")
            {
                exitButton = child;
                break;
            }
        }

        int targetIndex = exitButton != null ? exitButton.GetSiblingIndex() : canvas.childCount - 1;
        panel.SetSiblingIndex(Mathf.Clamp(targetIndex, 0, canvas.childCount - 1));
    }

    public void OpenSurvey()
    {
        var url = BuildSurveyOpenUrl();
        if (string.IsNullOrWhiteSpace(url))
            return;

        postBlockSurveyAwaitingVerification = true;
        postBlockSurveySubmitted = false;
        RefreshFinalOptionsPanelState();
        AppendPostBlockSurveyStatusToCompletionText();

        if (experimentLogic != null)
            experimentLogic.OpenExternalSurveyUrl(url);
        else
            Application.OpenURL(url);

        if (experimentLogic != null && experimentLogic.HasFormResponseVerification)
        {
            experimentLogic.BeginPostBlockSurveyVerification(
                GetSurveyVerificationConditionCode(),
                OnPostBlockSurveyVerificationFinished);
        }
    }

    public void ConfirmPostBlockSurveySubmissionManually()
    {
        if (!postBlockSurveyAwaitingVerification || postBlockSurveySubmitted)
            return;

        MarkPostBlockSurveySubmitted();
    }

    public void CheckPendingPostBlockSurveyOnce()
    {
        if (!postBlockSurveyAwaitingVerification || postBlockSurveySubmitted)
            return;

        if (experimentLogic == null || !experimentLogic.HasFormResponseVerification)
            return;

        StartCoroutine(CheckPostBlockSurveyOnceCoroutine());
    }

    IEnumerator CheckPostBlockSurveyOnceCoroutine()
    {
        bool finished = false;
        bool submitted = false;

        yield return FormResponseVerifier.CheckOnceCoroutine(
            experimentLogic.csvDriveUploadUrl,
            experimentLogic.csvDriveUploadSecret,
            GetSurveyVerificationConditionCode(),
            experimentLogic.GetParticipantCode(),
            experimentLogic.SessionStartedUtcMs,
            (_, isSubmitted, __) =>
            {
                finished = true;
                submitted = isSubmitted;
            });

        while (!finished)
            yield return null;

        if (submitted)
            MarkPostBlockSurveySubmitted();
    }

    void OnPostBlockSurveyVerificationFinished(bool submitted)
    {
        if (submitted)
            MarkPostBlockSurveySubmitted();
    }

    void MarkPostBlockSurveySubmitted()
    {
        postBlockSurveySubmitted = true;
        postBlockSurveyAwaitingVerification = false;

        string conditionCode = GetSurveyVerificationConditionCode();
        if (experimentLogic != null && !string.IsNullOrEmpty(conditionCode))
            experimentLogic.MarkPostBlockSurveySubmitted(conditionCode);

        RefreshFinalOptionsPanelState();
        AppendPostBlockSurveyStatusToCompletionText();
    }

    void AppendPostBlockSurveyStatusToCompletionText()
    {
        if (questionTextDisplay == null || !RequiresPostBlockSurvey())
            return;

        bool allBlocksDone = experimentLogic != null && experimentLogic.AreAllConditionsCompleted;
        string conditionCode = GetSurveyVerificationConditionCode();
        bool completedForCondition = postBlockSurveySubmitted
                                     || (experimentLogic != null
                                         && !string.IsNullOrEmpty(conditionCode)
                                         && experimentLogic.IsPostBlockSurveySubmitted(conditionCode));
        string surveyStatus = completedForCondition
            ? "\n<size=26><color=#6EEDC8>Encuesta recibida correctamente.</color></size>"
            : postBlockSurveyAwaitingVerification
                ? experimentLogic != null && experimentLogic.HasFormResponseVerification
                    ? "\n<size=26><color=#9EBFC2>Completá y enviá la encuesta en el navegador. La app verificará el envío al volver.</color></size>"
                    : "\n<size=26><color=#9EBFC2>Completá y enviá la encuesta. Luego pulsá «Confirmar envío» en Realizar Encuesta.</color></size>"
                : allBlocksDone && experimentLogic != null && !experimentLogic.AreAllRequiredPostBlockSurveysSubmitted()
                    ? "\n<size=26><color=#9EBFC2>Falta la encuesta del bloque "
                      + experimentLogic.GetFirstPendingPostBlockSurveyCondition()
                      + ". Pulsá «Realizar Encuesta».</color></size>"
                    : "\n<size=26><color=#9EBFC2>Pulsá «Realizar Encuesta», completala y enviala en el navegador.</color></size>";

        questionTextDisplay.text =
            "\u00a1Listo! Ya completaste este escenario.\n" +
            "Muchas gracias por participar. Nos ayudas un mont\u00f3n." +
            surveyStatus +
            (allBlocksDone
                ? "\n<size=26><color=#6EEDC8>Completaste los tres bloques.</color></size>" +
                  (experimentLogic != null && experimentLogic.AreAllRequiredPostBlockSurveysSubmitted()
                      ? "\n<size=26><color=#9EBFC2>Puls\u00e1 \u00abFinalizar\u00bb para cerrar la sesi\u00f3n.</color></size>"
                      : "")
                : completedForCondition
                    ? "\n<size=26><color=#9EBFC2>Pod\u00e9s continuar con \u00abOtro Escenario\u00bb.</color></size>"
                    : "");
        questionTextDisplay.alignment = TextAlignmentOptions.Center;
    }

    public void OnAnotherScenarioClicked()
    {
        if (experimentLogic != null && experimentLogic.AreAllConditionsCompleted)
            return;

        if (!CanLeaveBlockCompletionScreen())
            return;

        if (experimentLogic != null)
            experimentLogic.ResetToScenarioSelection();
    }

    public void OnFinalizeSessionClicked()
    {
        if (experimentLogic == null)
            return;

        if (!IsFarewellScreenVisible() && !CanFinalizeSession())
            return;

        experimentLogic.FinalizeAndResetSession();
    }

    /// <summary>Shown when all three blocks are done but the participant left the completion screen (recovery path).</summary>
    public void PresentSessionCompleteFinalScreen()
    {
        if (experimentLogic == null || !experimentLogic.AreAllConditionsCompleted)
            return;

        if (mainGameUiForCompleteScreen != null && experimentLogic != null)
            experimentLogic.SetMainGamePresentation(true);
        else if (mainGameUiForCompleteScreen != null)
            mainGameUiForCompleteScreen.SetActive(true);

        bool allSurveysDone = experimentLogic.AreAllRequiredPostBlockSurveysSubmitted();
        string pendingCondition = experimentLogic.GetFirstPendingPostBlockSurveyCondition();

        if (questionTextDisplay != null)
        {
            if (allSurveysDone)
            {
                questionTextDisplay.text =
                    "\u00a1Completaste los tres bloques!\n" +
                    "Muchas gracias por participar. Nos ayudas un mont\u00f3n.\n\n" +
                    "<size=26><color=#9EBFC2>Puls\u00e1 \u00abFinalizar\u00bb para cerrar la sesi\u00f3n.</color></size>";
            }
            else
            {
                questionTextDisplay.text =
                    "\u00a1Completaste los tres bloques!\n" +
                    "Muchas gracias por participar. Nos ayudas un mont\u00f3n.\n\n" +
                    "<size=26><color=#9EBFC2>Falta la encuesta del bloque "
                    + pendingCondition
                    + ". Puls\u00e1 \u00abRealizar Encuesta\u00bb, completala y enviala en el navegador.</color></size>";
            }

            questionTextDisplay.alignment = TextAlignmentOptions.Center;
        }

        postBlockSurveySubmitted = string.IsNullOrEmpty(pendingCondition);
        postBlockSurveyAwaitingVerification = false;

        if (questionCounterText != null) questionCounterText.gameObject.SetActive(false);
        if (askForHelpButton != null) askForHelpButton.gameObject.SetActive(false);
        if (inputField != null) inputField.gameObject.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (optionButtonGroup != null) optionButtonGroup.SetActive(false);
        if (progressFill != null) progressFill.gameObject.SetActive(false);
        if (progressBar_Bg != null) progressBar_Bg.gameObject.SetActive(false);

        if (myScrollRect != null)
        {
            myScrollRect.enabled = false;
            if (myScrollRect.verticalScrollbar != null)
                myScrollRect.verticalScrollbar.gameObject.SetActive(false);
        }

        if (finalOptionsPanel != null)
        {
            finalOptionsPanel.SetActive(true);
            var rt = finalOptionsPanel.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = Vector2.zero;
        }

        if (characterModel != null) characterModel.SetActive(false);

        RefreshFinalOptionsPanelState();
    }

    GameObject mainGameUiForCompleteScreen =>
        experimentLogic != null ? experimentLogic.mainGameUI : null;

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

            button.transition = Selectable.Transition.None;
        }

        RefreshAllStarVisuals();
    }

    void RefreshAllStarVisuals()
    {
        if (starIcons == null)
            return;

        for (int i = 0; i < starIcons.Length; i++)
            ApplyConfidenceStarVisual(i, i < currentConfidenceScore);
    }

    void ApplyConfidenceStarVisual(int index, bool selected)
    {
        if (starIcons == null || index < 0 || index >= starIcons.Length)
            return;

        Image icon = starIcons[index];
        if (icon == null)
            return;

        icon.color = selected ? glowColor : dullColor;
        icon.rectTransform.localScale = selected
            ? Vector3.one * 0.54f
            : Vector3.one * 0.5f;

        var outline = icon.GetComponent<Outline>();
        if (selected)
        {
            if (outline == null)
                outline = icon.gameObject.AddComponent<Outline>();

            outline.effectColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0.95f);
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = true;
            outline.enabled = true;
        }
        else if (outline != null)
        {
            outline.enabled = false;
        }

        foreach (var label in icon.GetComponentsInChildren<TextMeshProUGUI>(true))
            label.color = selected ? starLabelSelectedColor : starLabelDullColor;
    }

    public void BeginScenario(int scenarioIndex)
    {
        isPracticeMode = false;
        practiceAgentProfileIndex = -1;
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
            dataLogger = UnityEngine.Object.FindFirstObjectByType<DataLogger>();
        if (dataLogger != null)
            dataLogger.ResetQuestionTimer();

        SetOptionButtonsInteractable(true);
        HideQuestionFlowOverlays();

        if (activeQuestions.Count > 0)
            DisplayQuestion(activeQuestions[0]);

        UpdateProgressBar();
    }

    /// <summary>
    /// One-item tutorial block before the first real condition. Agent profile matches the participant's first assigned block (A=0, B=1, C=2).
    /// </summary>
    public void BeginPracticeBlock(int agentProfileScenarioIndex)
    {
        isPracticeMode = true;
        practiceAgentProfileIndex = Mathf.Clamp(agentProfileScenarioIndex, 0, 2);
        activeScenarioIndex = -1;
        activeQuestions = PracticeScenarioContent.CreateQuestionList();

        currentQuestionIndex = 0;
        totalQuestions = activeQuestions.Count;
        temporaryChoice = "";
        temporaryChoiceLetter = "";
        answersSubmittedInCurrentScenario = 0;
        isCurrentScenarioComplete = false;

        if (characterModel != null)
            characterModel.SetActive(practiceAgentProfileIndex == 2);

        ApplyChatAssistanceVisibility(practiceAgentProfileIndex);

        if (dataLogger == null)
            dataLogger = UnityEngine.Object.FindFirstObjectByType<DataLogger>();
        if (dataLogger != null)
            dataLogger.ResetQuestionTimer();

        SetOptionButtonsInteractable(true);
        HideQuestionFlowOverlays();

        if (activeQuestions.Count > 0)
            DisplayQuestion(activeQuestions[0]);

        UpdateProgressBar();
    }

    public void ClearPracticeMode()
    {
        isPracticeMode = false;
        practiceAgentProfileIndex = -1;
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
            experimentLogic.SetChatPanelVisible(showChat, flushChatSummaryOnHide: showChat);
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
            : "<color=#ffaa00>" + message + "</color>\n\n" + FormatSituationDisplayText(situation);
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
        if (experimentLogic != null && experimentLogic.IsGeminiInFlight) return;

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
        RefreshAllStarVisuals();

        if (confirmRatingButton != null) confirmRatingButton.interactable = (currentConfidenceScore > 0);
    }

    private void ResetStars()
    {
        currentConfidenceScore = 0;
        RefreshAllStarVisuals();
        if (confirmRatingButton != null) confirmRatingButton.interactable = false;
    }

    public void OnEntregarClicked()
    {
        if (isSubmittingAnswer) return;
        if (experimentLogic != null && experimentLogic.IsGeminiInFlight) return;
        if (string.IsNullOrEmpty(temporaryChoice) || string.IsNullOrEmpty(temporaryChoiceLetter) || currentConfidenceScore <= 0) return;

        isSubmittingAnswer = true;
        if (confirmRatingButton != null) confirmRatingButton.interactable = false;

        try
        {
            if (isPracticeMode)
            {
                if (confidencePanel != null) confidencePanel.SetActive(false);

                temporaryChoice = "";
                temporaryChoiceLetter = "";
                if (nextButton != null) nextButton.interactable = false;
                UpdateBtnVisuals("");
                SetOptionButtonsInteractable(true);

                ShowNextQuestion();
                return;
            }

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
            if (isPracticeMode)
                FinishPracticeBlock();
            else
                FinishExperiment();
        }
    }

    void FinishPracticeBlock()
    {
        ClearPracticeMode();

        if (questionCounterText != null) questionCounterText.gameObject.SetActive(false);
        if (askForHelpButton != null) askForHelpButton.gameObject.SetActive(false);
        if (inputField != null) inputField.gameObject.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);

        if (experimentLogic != null)
        {
            experimentLogic.SetChatPanelVisible(false, flushChatSummaryOnHide: false);
            experimentLogic.ReturnToScenarioSelectionAfterPractice();
        }

        if (optionButtonGroup != null) optionButtonGroup.SetActive(false);
        if (progressFill != null) progressFill.gameObject.SetActive(false);
        if (progressBar_Bg != null) progressBar_Bg.gameObject.SetActive(false);

        if (myScrollRect != null && myScrollRect.verticalScrollbar != null)
        {
            myScrollRect.verticalScrollbar.gameObject.SetActive(false);
            myScrollRect.enabled = false;
        }

        if (characterModel != null) characterModel.SetActive(false);
    }

    static readonly Regex QuestionParagraphPattern = new(
        @"(\r?\n\r?\n)(?=¿|\u00BF|\?)",
        RegexOptions.Compiled | RegexOptions.RightToLeft);

    static string FormatSituationDisplayText(string situation)
    {
        if (string.IsNullOrWhiteSpace(situation) || ContainsPreguntaSection(situation))
            return situation;

        var match = QuestionParagraphPattern.Match(situation);
        if (!match.Success)
            return situation;

        return situation.Insert(match.Groups[1].Index + match.Groups[1].Length, "PREGUNTA\r\n");
    }

    static bool ContainsPreguntaSection(string text)
    {
        foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (trimmed.Equals("PREGUNTA", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("PREGUNTA:", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("PREGUNTA ", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    void DisplayQuestion(ExperimentQuestion q)
    {
        if (q == null) return;

        HideQuestionFlowOverlays();

        if (dataLogger != null)
            dataLogger.ResetQuestionTimer();

        if (experimentLogic != null)
            experimentLogic.ResetQuestionTimer();

        if (questionTextDisplay != null) questionTextDisplay.text = FormatSituationDisplayText(q.situation);

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
        {
            questionCounterText.text = isPracticeMode
                ? "Práctica · Pregunta 1 de 1"
                : "Pregunta " + (currentQuestionIndex + 1) + " de " + totalQuestions;
        }

        ConfigureQuestionScrollContentLayout();
    }

    private void FinishExperiment()
    {
        isCurrentScenarioComplete = true;
        completedScenariosInSession++;
        string conditionCode = ActiveConditionCode;
        postBlockSurveySubmitted = experimentLogic != null
                                   && !string.IsNullOrEmpty(conditionCode)
                                   && experimentLogic.IsPostBlockSurveySubmitted(conditionCode);
        postBlockSurveyAwaitingVerification = false;

        // Hide interactive controls once all questions are complete.
        if (questionCounterText != null) questionCounterText.gameObject.SetActive(false);
        if (askForHelpButton != null) askForHelpButton.gameObject.SetActive(false);
        if (inputField != null) inputField.gameObject.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);

        if (experimentLogic != null)
        {
            experimentLogic.MarkConditionCompleted(ActiveConditionCode);
            experimentLogic.FlushPendingChatLogs();
            experimentLogic.FinishScenarioChatLogging();
            experimentLogic.SetChatPanelVisible(false, flushChatSummaryOnHide: false);
            experimentLogic.QueueSessionCsvDriveUpload();
        }
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

        RefreshFinalOptionsPanelState();
        AppendPostBlockSurveyStatusToCompletionText();

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

            ConfigureQuestionScroll();
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

        HideQuestionFlowOverlays();

        temporaryChoice = "";
        temporaryChoiceLetter = "";
        currentQuestionIndex = 0;
        activeScenarioIndex = -1;
        activeQuestions = null;
        totalQuestions = 0;
        isPracticeMode = false;
        practiceAgentProfileIndex = -1;

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

    public void HideQuestionFlowOverlays()
    {
        if (confidencePanel != null)
            confidencePanel.SetActive(false);

        if (finalOptionsPanel != null)
            finalOptionsPanel.SetActive(false);

        ResetStars();
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
        if (isPracticeMode)
            return false;

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
        if (experimentLogic != null)
            experimentLogic.PrepareForApplicationExit();
        else
        {
            var avatarDisplay = FindFirstObjectByType<AvatarDisplayController>(FindObjectsInactive.Include);
            if (avatarDisplay != null)
                avatarDisplay.ShutdownForExit();
        }

        if (characterModel != null)
            characterModel.SetActive(false);

        if (ShouldDeleteSessionDataOnExit())
        {
            DataLogger logger = dataLogger != null ? dataLogger : FindFirstObjectByType<DataLogger>();
            if (experimentLogic != null)
                experimentLogic.NotifySessionDiscarded();
            if (logger != null)
                logger.DeleteIncompleteFile();
        }
        else if (experimentLogic != null)
        {
            if (IsChatAssistanceEnabled && activeScenarioIndex >= 0)
                experimentLogic.FinishScenarioChatLogging();
            else
                experimentLogic.FlushPendingChatLogs();

            experimentLogic.FinalizeSessionDataLogging();
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

        var avatarDisplay = FindFirstObjectByType<AvatarDisplayController>(FindObjectsInactive.Include);
        if (avatarDisplay != null)
            avatarDisplay.ShutdownForExit();

        if (characterModel != null)
            characterModel.SetActive(false);
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