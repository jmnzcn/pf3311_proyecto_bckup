using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
    [Header("Chat UI")]
    public ScrollRect chatScrollRect;
    public TextMeshProUGUI hintText;
    [Tooltip("Mouse wheel / trackpad scroll speed for chat history and chat input.")]
    [SerializeField] float chatScrollSensitivity = 45f;

    [Header("Session UI")]
    public GameObject mainGameUI;
    public Toggle ageConsentToggle;
    public Image ageConsentBackground;
    public Toggle consentToggle;
    public Image consentBackground;
    public Button continueButton;
    public TMP_InputField participantCodeInput;
    public TextMeshProUGUI userIdText;
    public GameObject scenarioSelectionPanel;

    GameObject scenarioSelectionPopup;

    const string PopupObjectName = "Popup";
    static readonly Color ScenarioSelectionPopupTint = new(0.41568628f, 0.41568628f, 0.41568628f, 0f);
    static readonly Color BookshelfBackgroundTint = new(0.41509432f, 0.41509432f, 0.41509432f, 1f);
    static readonly string[] CanvasFlatBackdropNames =
    {
        "bg",
        "left_panel_bg",
        "left_panel_bg (1)",
    };
    static readonly string[] MainGameOverlayPanelNames =
    {
        "ConfidencePopup",
        "FinalMenuPanel",
    };

    Transform savedBookshelfBgParent;
    int savedBookshelfBgSiblingIndex = -1;
    bool savedBookshelfBgActive;
    static readonly Vector2 RightPanelGameplayAnchorMin = new(1f, 0f);
    static readonly Vector2 RightPanelGameplayAnchorMax = new(1f, 1f);
    static readonly Vector2 RightPanelGameplayPivot = new(1f, 0.5f);
    static readonly Vector2 RightPanelGameplaySize = new(550f, 0f);
    const string ScenarioSelectionBackgroundName = "bg_image";
    static readonly Vector2 BookshelfBgOriginalAnchorMin = new(0f, 1f);
    static readonly Vector2 BookshelfBgOriginalAnchorMax = new(0f, 1f);
    static readonly Vector2 BookshelfBgOriginalPivot = new(0.5f, 0.5f);
    static readonly Vector2 BookshelfBgOriginalAnchoredPosition = new(960f, -540f);
    static readonly Vector2 BookshelfBgOriginalSizeDelta = new(1940f, 1080f);

    const int MinParticipantNumber = 1;

    /// <summary>Formats a validated participant number: 1→P01, 20→P20.</summary>
    public static string FormatParticipantCode(int number)
    {
        return number < 10 ? "P" + number.ToString("D2") : "P" + number.ToString();
    }

    [Header("Integration")]
    public QuestionManager questionManager;

    [Header("Profile survey (Form 0 — before first block)")]
    [Tooltip("Google Form de perfil del participante. Se abre en el navegador desde la pantalla de selección.")]
    public string profileSurveyUrl =
        "https://docs.google.com/forms/d/1XjG9GBr71tyhfF2sFWn7RcKfHkBAjCnufZJKZXoP6zA/viewform";
    [Tooltip("Campo entry.XXXXXXXX del código de participante en Form 0 (vacío = sin prellenado).")]
    public string profileSurveyCodeEntryId = "entry.1810287026";

    [Header("CSV backup — Google Drive (Apps Script)")]
    [Tooltip("URL /exec del Apps Script desplegado. Vacío = no sube a Drive.")]
    public string csvDriveUploadUrl = "";
    [Tooltip("Secreto compartido con UPLOAD_SECRET en Apps Script. Vacío = no sube.")]
    public string csvDriveUploadSecret = "";

    [Header("API Config")]
    public string apiKey = "YOUR_GEMINI_API_KEY_HERE";
    public string endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=";

    private string currentUserID;
    private string participantCode;
    private bool consentRecordSaved;
    private bool consentWrittenToDisk;
    private bool sessionSkeletonCreated;
    private bool sessionCsvFinalized;
    private bool sessionDataDiscarded;
    private bool sessionRanConditionA;
    private bool sessionRanConditionB;
    private bool sessionRanConditionC;
    private bool sessionCompletedConditionA;
    private bool sessionCompletedConditionB;
    private bool sessionCompletedConditionC;
    private bool sessionProfileSurveySubmitted;
    private bool sessionProfileSurveyAwaitingVerification;
    private bool sessionPostBlockSurveySubmittedA;
    private bool sessionPostBlockSurveySubmittedB;
    private bool sessionPostBlockSurveySubmittedC;
    private long sessionStartedUtcMs;
    private bool sessionHadChatExchanges;
    private string chatHistory = "";
    private float questionStartTime = 0f;
    private bool geminiInFlight;
    private int geminiRequestGeneration;
    private Coroutine activeGeminiCoroutine;
    private Coroutine scrollCoroutine;
    private Coroutine chatFinalizeCoroutine;
    private Coroutine csvDriveUploadCoroutine;
    private Coroutine profileVerifyCoroutine;
    private Coroutine postBlockVerifyCoroutine;
    private float geminiExchangeStartTime;
    private bool chatHistoryLayoutConfigured;
    private readonly List<ApiChatTurn> apiTurns = new List<ApiChatTurn>();
    private readonly ChatSessionLogger chatSessionLogger = new ChatSessionLogger();
    private string lastCommittedStudentMessage = "";
    private bool lastInputHadText;
    private static float lastSaveFailureNoticeTime = -999f;
    private ChatInputUiController chatInputUi;

    /// <summary>
    /// Sliding window of prior user/model turns sent to Gemini per question (not a participant-visible cap).
    /// CSV and on-screen chat history remain complete; only API context is trimmed.
    /// </summary>
    const int MaxApiTurnsPerQuestion = 30;
    const string SaveFailureParticipantMessage =
        "No se pudo guardar tu respuesta en el registro de la sesión. Cerrá Excel u otros programas que puedan tener abierto el archivo de datos e intentá entregar de nuevo. Si el problema continúa, contactá al investigador del estudio.";
    const string ChatSaveFailureParticipantMessage =
        "No se pudo guardar el registro del chat en el archivo de datos. Cerrá Excel u otros programas que puedan tener abierto la carpeta CSV e intentá de nuevo. Si el problema continúa, contactá al investigador del estudio.";
    const string ConsentSaveFailureParticipantMessage =
        "No se pudo guardar el registro de consentimiento. Cerrá Excel u otros programas que puedan tener abierto la carpeta CSV e intentá Continuar de nuevo. Si el problema continúa, contactá al investigador del estudio.";

    /// <summary>Standalone: windowed 1920×1080 (fullscreen + external browser corrupts UI on Windows).</summary>
    static void ApplyStandaloneDisplayProfile()
    {
#if UNITY_EDITOR
        return;
#else
        QualitySettings.vSyncCount = 1;
        Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
#endif
    }

    TMP_InputField ChatInput =>
        questionManager != null ? questionManager.inputField : null;

    Button SendChatButton =>
        questionManager != null ? questionManager.askForHelpButton : null;

    public bool IsGeminiInFlight => geminiInFlight;

    public bool IsConsentComplete()
    {
        if (ageConsentToggle == null || consentToggle == null)
            return false;

        if (!ageConsentToggle.isOn || !consentToggle.isOn)
            return false;

        return TryGetParticipantCodeFromInput(out _);
    }

    /// <summary>Normalizes raw input (P1, p020, 01…) to canonical form for CSV (P01, P20, …).</summary>
    public static bool TryNormalizeParticipantCode(string raw, out string normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        raw = raw.Trim().ToUpperInvariant().Replace(" ", "");
        if (raw.StartsWith("P", StringComparison.Ordinal))
            raw = raw[1..];

        if (raw.Length == 0 || !int.TryParse(raw, out int number))
            return false;

        if (number < MinParticipantNumber)
            return false;

        normalized = FormatParticipantCode(number);
        return true;
    }

    public void BindParticipantCodeInput(TMP_InputField input)
    {
        if (participantCodeInput != null)
            participantCodeInput.onEndEdit.RemoveListener(OnParticipantCodeEndEdit);

        participantCodeInput = input;

        if (participantCodeInput != null)
            participantCodeInput.onEndEdit.AddListener(OnParticipantCodeEndEdit);
    }

    void OnParticipantCodeEndEdit(string _) => FormatParticipantCodeInputDisplay();

    /// <summary>Shows canonical code in the field when input is valid (e.g. p1 → P01, p020 → P20).</summary>
    void FormatParticipantCodeInputDisplay()
    {
        if (participantCodeInput == null)
            return;

        if (TryNormalizeParticipantCode(GetParticipantCodeInputRawText(), out string normalized))
            participantCodeInput.SetTextWithoutNotify(normalized);
    }

    /// <summary>Persists normalized code (always uppercase P##) for CSV and session ID.</summary>
    void CommitParticipantCodeFromInput()
    {
        if (!TryGetParticipantCodeFromInput(out string normalized))
            return;

        participantCode = normalized;

        if (participantCodeInput != null && participantCodeInput.text != normalized)
            participantCodeInput.SetTextWithoutNotify(normalized);
    }

    bool TryGetParticipantCodeFromInput(out string normalized)
    {
        normalized = null;

        if (participantCodeInput == null)
            return false;

        return TryNormalizeParticipantCode(GetParticipantCodeInputRawText(), out normalized);
    }

    string GetParticipantCodeInputRawText()
    {
        if (participantCodeInput == null)
            return string.Empty;

        return participantCodeInput.text?.Replace("\u200B", string.Empty).Trim() ?? string.Empty;
    }

    public string GetParticipantCode()
    {
        if (string.IsNullOrEmpty(participantCode) && TryGetParticipantCodeFromInput(out string fromInput))
            participantCode = fromInput;

        if (!string.IsNullOrEmpty(participantCode))
            return participantCode;

        return "Unknown";
    }

    /// <summary>Participant code for Google Forms prefill; empty when not available (never sends "Unknown").</summary>
    public string GetParticipantCodeForSurveyPrefill()
    {
        if (string.IsNullOrEmpty(participantCode))
            CommitParticipantCodeFromInput();

        if (TryNormalizeParticipantCode(participantCode, out string committed))
            return committed;

        if (TryGetParticipantCodeFromInput(out string fromInput)
            && TryNormalizeParticipantCode(fromInput, out string fromField))
            return fromField;

        string fromUi = TryParseParticipantCodeFromUserIdLabel();
        if (!string.IsNullOrEmpty(fromUi))
            return fromUi;

        return "";
    }

    string TryParseParticipantCodeFromUserIdLabel()
    {
        if (userIdText == null || string.IsNullOrWhiteSpace(userIdText.text))
            return "";

        var match = Regex.Match(userIdText.text, @"\bP\d{2,}\b", RegexOptions.IgnoreCase);
        if (!match.Success)
            return "";

        return TryNormalizeParticipantCode(match.Value, out string normalized) ? normalized : "";
    }

    public bool HasParticipantProfileSurvey =>
        !string.IsNullOrWhiteSpace(profileSurveyUrl);

    public bool HasFormResponseVerification => HasCsvDriveUpload;

    public bool IsProfileSurveyCompleted => sessionProfileSurveySubmitted;

    public bool IsProfileSurveyAwaitingVerification => sessionProfileSurveyAwaitingVerification;

    public bool IsPostBlockSurveySubmitted(string conditionCode) =>
        conditionCode switch
        {
            "A" => sessionPostBlockSurveySubmittedA,
            "B" => sessionPostBlockSurveySubmittedB,
            "C" => sessionPostBlockSurveySubmittedC,
            _ => true
        };

    public void MarkPostBlockSurveySubmitted(string conditionCode)
    {
        switch (conditionCode)
        {
            case "A":
                sessionPostBlockSurveySubmittedA = true;
                break;
            case "B":
                sessionPostBlockSurveySubmittedB = true;
                break;
            case "C":
                sessionPostBlockSurveySubmittedC = true;
                break;
        }

        if (postBlockVerifyCoroutine != null)
        {
            StopCoroutine(postBlockVerifyCoroutine);
            postBlockVerifyCoroutine = null;
        }
    }

    /// <summary>True when every completed block that has a survey URL also has a verified submission.</summary>
    public bool AreAllRequiredPostBlockSurveysSubmitted()
    {
        if (questionManager == null)
            return true;

        for (int scenarioIndex = 0; scenarioIndex < 3; scenarioIndex++)
        {
            if (!IsConditionCompleted(scenarioIndex))
                continue;

            if (!questionManager.ScenarioRequiresPostBlockSurvey(scenarioIndex))
                continue;

            string conditionCode = ScenarioIndexToConditionCode(scenarioIndex);
            if (!IsPostBlockSurveySubmitted(conditionCode))
                return false;
        }

        return true;
    }

    /// <summary>First completed block (A→B→C) still missing a verified post-block survey, or empty.</summary>
    public string GetFirstPendingPostBlockSurveyCondition()
    {
        if (questionManager == null)
            return "";

        for (int scenarioIndex = 0; scenarioIndex < 3; scenarioIndex++)
        {
            if (!IsConditionCompleted(scenarioIndex))
                continue;

            if (!questionManager.ScenarioRequiresPostBlockSurvey(scenarioIndex))
                continue;

            string conditionCode = ScenarioIndexToConditionCode(scenarioIndex);
            if (!IsPostBlockSurveySubmitted(conditionCode))
                return conditionCode;
        }

        return "";
    }

    static string ScenarioIndexToConditionCode(int scenarioIndex) =>
        scenarioIndex switch
        {
            0 => "A",
            1 => "B",
            2 => "C",
            _ => ""
        };

    public bool CanOpenProfileSurvey() =>
        HasParticipantProfileSurvey && !sessionProfileSurveySubmitted && !sessionProfileSurveyAwaitingVerification;

    public long SessionStartedUtcMs => sessionStartedUtcMs;

    public bool HasCsvDriveUpload =>
        !string.IsNullOrWhiteSpace(csvDriveUploadUrl) &&
        !string.IsNullOrWhiteSpace(csvDriveUploadSecret);

    public void OpenParticipantProfileSurvey()
    {
        OpenProfileSurveyInBrowser();
    }

    public void OpenProfileSurveyInBrowser()
    {
        if (!CanOpenProfileSurvey())
            return;

        EnsureScenarioSelectionVisible();

        string url = BuildParticipantProfileSurveyUrl();
        if (string.IsNullOrWhiteSpace(url))
            return;

        sessionProfileSurveyAwaitingVerification = true;
        OpenExternalSurveyUrl(url, GetParticipantCodeForSurveyPrefill());
        NotifyProfileSurveyOpened();
        BeginProfileSurveyVerification();
    }

    public void ConfirmProfileSurveySubmissionManually()
    {
        if (!sessionProfileSurveyAwaitingVerification || sessionProfileSurveySubmitted)
            return;

        MarkProfileSurveySubmitted();
    }

    void MarkProfileSurveySubmitted()
    {
        sessionProfileSurveySubmitted = true;
        sessionProfileSurveyAwaitingVerification = false;

        if (profileVerifyCoroutine != null)
        {
            StopCoroutine(profileVerifyCoroutine);
            profileVerifyCoroutine = null;
        }

        NotifyProfileSurveyOpened();
    }

    void BeginProfileSurveyVerification()
    {
        if (profileVerifyCoroutine != null)
            StopCoroutine(profileVerifyCoroutine);

        if (!HasFormResponseVerification)
            return;

        profileVerifyCoroutine = StartCoroutine(ProfileSurveyVerificationCoroutine());
    }

    IEnumerator ProfileSurveyVerificationCoroutine()
    {
        bool submitted = false;
        yield return FormResponseVerifier.PollUntilSubmittedCoroutine(
            csvDriveUploadUrl,
            csvDriveUploadSecret,
            "profile",
            GetParticipantCode(),
            sessionStartedUtcMs,
            4f,
            900f,
            (ok, _) => submitted = ok);

        if (submitted)
            MarkProfileSurveySubmitted();
    }

    public void BeginPostBlockSurveyVerification(string conditionCode, Action<bool> onFinished)
    {
        if (postBlockVerifyCoroutine != null)
            StopCoroutine(postBlockVerifyCoroutine);

        if (!HasFormResponseVerification)
        {
            onFinished?.Invoke(false);
            return;
        }

        postBlockVerifyCoroutine = StartCoroutine(PostBlockSurveyVerificationCoroutine(conditionCode, onFinished));
    }

    IEnumerator PostBlockSurveyVerificationCoroutine(string conditionCode, Action<bool> onFinished)
    {
        bool submitted = false;
        yield return FormResponseVerifier.PollUntilSubmittedCoroutine(
            csvDriveUploadUrl,
            csvDriveUploadSecret,
            conditionCode,
            GetParticipantCode(),
            sessionStartedUtcMs,
            4f,
            900f,
            (ok, _) => submitted = ok);

        postBlockVerifyCoroutine = null;
        onFinished?.Invoke(submitted);
    }

    public void CheckPendingSurveySubmissionsOnce()
    {
        if (sessionProfileSurveyAwaitingVerification && HasFormResponseVerification)
            StartCoroutine(CheckProfileSurveyOnceCoroutine());

        if (questionManager != null)
            questionManager.CheckPendingPostBlockSurveyOnce();
    }

    IEnumerator CheckProfileSurveyOnceCoroutine()
    {
        bool finished = false;
        bool submitted = false;

        yield return FormResponseVerifier.CheckOnceCoroutine(
            csvDriveUploadUrl,
            csvDriveUploadSecret,
            "profile",
            GetParticipantCode(),
            sessionStartedUtcMs,
            (_, isSubmitted, __) =>
            {
                finished = true;
                submitted = isSubmitted;
            });

        while (!finished)
            yield return null;

        if (submitted)
            MarkProfileSurveySubmitted();
    }

    public void EnsureScenarioSelectionVisible()
    {
        SetScenarioSelectionPresentation(true);
    }

    void SetScenarioSelectionPresentation(bool visible)
    {
        ResolveScenarioSelectionPanelReference();

        if (visible)
        {
            Transform consentOverlay = FindCanvasChild("Consent_Overlay");
            if (consentOverlay != null)
                consentOverlay.gameObject.SetActive(false);

            if (mainGameUI != null)
                mainGameUI.SetActive(false);

            SetCanvasFlatBackdropVisible(false);
            SetScenarioSelectionBackground(true);

            ActivateScenarioSelectionHierarchy();
            SetScenarioSelectionVisible(true);
            RefreshScenarioSelectionUi();
            return;
        }

        DeactivateScenarioSelectionOverlay();
        SetScenarioSelectionBackground(false);
        SetCanvasFlatBackdropVisible(true);

        if (mainGameUI != null)
            mainGameUI.SetActive(true);
    }

    void SetCanvasFlatBackdropVisible(bool visible)
    {
        foreach (string childName in CanvasFlatBackdropNames)
        {
            Transform child = FindCanvasChild(childName);
            if (child != null)
                child.gameObject.SetActive(visible);
        }
    }

    static Transform FindCanvasChild(string childName)
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
            return null;

        foreach (Transform child in canvas.transform)
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    static Transform FindCanvasObject(string objectName)
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
            return null;

        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                return child;
        }

        return null;
    }

    void SetScenarioSelectionBackground(bool showBookshelfPhoto)
    {
        Transform bookshelfBg = FindCanvasObject(ScenarioSelectionBackgroundName);
        Transform rightPanel = FindCanvasChild("RightPanel");
        var canvas = GameObject.Find("Canvas")?.transform;

        if (showBookshelfPhoto)
        {
            if (bookshelfBg is RectTransform bgRect && canvas != null)
            {
                if (savedBookshelfBgParent == null)
                {
                    savedBookshelfBgParent = bgRect.parent;
                    savedBookshelfBgSiblingIndex = bgRect.GetSiblingIndex();
                    savedBookshelfBgActive = bgRect.gameObject.activeSelf;
                }

                bgRect.SetParent(canvas, false);
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.pivot = new Vector2(0.5f, 0.5f);
                bgRect.anchoredPosition = Vector2.zero;
                bgRect.sizeDelta = Vector2.zero;
                bgRect.localScale = Vector3.one;
                bgRect.SetAsFirstSibling();
                bgRect.gameObject.SetActive(true);

                if (bgRect.TryGetComponent(out Image bgImage))
                {
                    bgImage.raycastTarget = false;
                    bgImage.color = BookshelfBackgroundTint;
                }
            }

            if (rightPanel != null)
                rightPanel.gameObject.SetActive(false);

            if (questionManager != null && questionManager.characterModel != null)
                questionManager.characterModel.SetActive(false);

            return;
        }

        if (bookshelfBg is RectTransform restoreRect)
        {
            restoreRect.gameObject.SetActive(savedBookshelfBgActive);

            if (savedBookshelfBgParent != null)
            {
                restoreRect.SetParent(savedBookshelfBgParent, false);
                restoreRect.SetSiblingIndex(Mathf.Max(0, savedBookshelfBgSiblingIndex));
            }

            restoreRect.anchorMin = BookshelfBgOriginalAnchorMin;
            restoreRect.anchorMax = BookshelfBgOriginalAnchorMax;
            restoreRect.pivot = BookshelfBgOriginalPivot;
            restoreRect.anchoredPosition = BookshelfBgOriginalAnchoredPosition;
            restoreRect.sizeDelta = BookshelfBgOriginalSizeDelta;
            restoreRect.localScale = Vector3.one;

            savedBookshelfBgParent = null;
            savedBookshelfBgSiblingIndex = -1;
        }

        if (rightPanel is RectTransform rect)
        {
            rect.anchorMin = RightPanelGameplayAnchorMin;
            rect.anchorMax = RightPanelGameplayAnchorMax;
            rect.pivot = RightPanelGameplayPivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = RightPanelGameplaySize;
            rect.localScale = Vector3.one;
            rightPanel.gameObject.SetActive(true);
        }
    }

    public void SetMainGamePresentation(bool questionMode)
    {
        if (mainGameUI == null)
            return;

        mainGameUI.SetActive(questionMode);

        if (!questionMode)
            return;

        if (mainGameUI.TryGetComponent(out Image panelImage))
            panelImage.raycastTarget = true;

        foreach (Transform child in mainGameUI.transform)
        {
            if (ShouldKeepMainGameOverlayHidden(child.name))
                continue;

            child.gameObject.SetActive(true);
        }

        if (questionManager != null)
            questionManager.HideQuestionFlowOverlays();
    }

    static bool ShouldKeepMainGameOverlayHidden(string childName)
    {
        if (string.IsNullOrEmpty(childName))
            return false;

        foreach (string overlayName in MainGameOverlayPanelNames)
        {
            if (childName == overlayName)
                return true;
        }

        return false;
    }

    public void DeactivateScenarioSelectionOverlay()
    {
        if (scenarioSelectionPanel != null)
            scenarioSelectionPanel.SetActive(false);

        Transform popup = scenarioSelectionPopup != null
            ? scenarioSelectionPopup.transform
            : FindCanvasChild(PopupObjectName);

        if (popup == null)
            return;

        scenarioSelectionPopup = popup.gameObject;

        if (popup.TryGetComponent(out Image popupImage))
        {
            popupImage.raycastTarget = false;
            popupImage.color = ScenarioSelectionPopupTint;
        }

        scenarioSelectionPopup.SetActive(false);
    }

    void ActivateScenarioSelectionHierarchy()
    {
        ResolveScenarioSelectionPanelReference();

        Transform popup = FindCanvasChild(PopupObjectName);
        if (popup != null)
        {
            scenarioSelectionPopup = popup.gameObject;
            scenarioSelectionPopup.SetActive(true);

            if (popup.TryGetComponent(out Image popupImage))
            {
                popupImage.raycastTarget = false;
                popupImage.color = ScenarioSelectionPopupTint;
            }

            Transform canvas = popup.parent;
            if (canvas != null)
            {
                Transform exitButton = FindCanvasChild("Exit_Button");
                int targetIndex = exitButton != null ? exitButton.GetSiblingIndex() : canvas.childCount - 1;
                popup.SetSiblingIndex(Mathf.Clamp(targetIndex, 0, canvas.childCount - 1));
            }
        }

        if (scenarioSelectionPanel != null && popup != null
            && scenarioSelectionPanel.transform.parent != popup)
        {
            scenarioSelectionPanel.transform.SetParent(popup, false);
        }
    }

    void ResolveScenarioSelectionPanelReference()
    {
        if (scenarioSelectionPanel != null)
            return;

        Transform popup = FindCanvasChild(PopupObjectName);
        if (popup != null)
        {
            foreach (Transform child in popup)
            {
                if (child.name == "Scenario_Selection")
                {
                    scenarioSelectionPanel = child.gameObject;
                    return;
                }
            }
        }

        var found = GameObject.Find("Scenario_Selection");
        if (found != null)
            scenarioSelectionPanel = found;
    }

    void SetScenarioSelectionVisible(bool visible)
    {
        ResolveScenarioSelectionPanelReference();
        if (scenarioSelectionPanel == null)
            return;

        if (visible)
        {
            var selector = scenarioSelectionPanel.GetComponent<ScenarioSelectionController>();
            if (selector == null)
                selector = scenarioSelectionPanel.AddComponent<ScenarioSelectionController>();
            else
                selector.PrepareForDisplay();
        }

        scenarioSelectionPanel.SetActive(visible);
        if (!visible)
            return;

        var activeSelector = scenarioSelectionPanel.GetComponent<ScenarioSelectionController>();
        if (activeSelector == null)
            activeSelector = scenarioSelectionPanel.AddComponent<ScenarioSelectionController>();
    }

    void NotifyProfileSurveyOpened()
    {
        ResolveScenarioSelectionPanelReference();

        if (scenarioSelectionPanel == null)
            return;

        var selector = scenarioSelectionPanel.GetComponent<ScenarioSelectionController>();
        if (selector == null)
            selector = scenarioSelectionPanel.AddComponent<ScenarioSelectionController>();

        selector.NotifyProfileSurveyOpened();
    }

    string BuildParticipantProfileSurveyUrl()
    {
        return SurveyLinkBuilder.BuildPrefilledUrl(
            profileSurveyUrl,
            profileSurveyCodeEntryId,
            GetParticipantCodeForSurveyPrefill());
    }

    public void OpenExternalSurveyUrl(string url, string participantCodeForClipboard = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        // Full prefilled URL on clipboard: paste in the browser bar if auto-open drops query params.
        TryCopyParticipantCodeToClipboard(url);

        if (IsWindowsRuntime() && TryOpenUrlInPrivateBrowser(url))
        {
            UnityEngine.Debug.Log("Survey opened in private browser: " + url);
            return;
        }

        UnityEngine.Debug.Log("Survey opened via Application.OpenURL: " + url);
        Application.OpenURL(url);
    }

    static bool IsWindowsRuntime() =>
        Application.platform == RuntimePlatform.WindowsEditor
        || Application.platform == RuntimePlatform.WindowsPlayer;

    static void TryCopyParticipantCodeToClipboard(string participantCode)
    {
        if (string.IsNullOrWhiteSpace(participantCode))
            return;

        try
        {
            GUIUtility.systemCopyBuffer = participantCode.Trim();
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("Could not copy participant code to clipboard: " + ex.Message);
        }
    }

    /// <summary>
    /// Private/incognito avoids Google Forms draft popups that override URL prefill when signed in.
    /// </summary>
    static bool TryOpenUrlInPrivateBrowser(string url)
    {
        string[] browserPaths =
        {
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        };

        string[] privateFlags = { "--inprivate", "--incognito", "--inprivate", "--incognito" };

        for (int i = 0; i < browserPaths.Length; i++)
        {
            string path = browserPaths[i];
            if (!File.Exists(path))
                continue;

            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = false,
                };
                startInfo.ArgumentList.Add(privateFlags[i]);
                startInfo.ArgumentList.Add(url);
                System.Diagnostics.Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("Private browser launch failed for " + path + ": " + ex.Message);
            }
        }

        return false;
    }

    public void RefreshDisplayAfterExternalApp()
    {
        StartCoroutine(RefreshDisplayAfterFocusCoroutine());
    }

    System.Collections.IEnumerator RefreshDisplayAfterFocusCoroutine()
    {
        yield return null;
        yield return null;
#if !UNITY_EDITOR
        ApplyStandaloneDisplayProfile();
#endif
        Canvas.ForceUpdateCanvases();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            return;

        CheckPendingSurveySubmissionsOnce();

#if !UNITY_EDITOR
        StartCoroutine(RefreshDisplayAfterFocusCoroutine());
#endif
    }

    void Awake()
    {
        ApplyStandaloneDisplayProfile();
    }

    static void DestroyProfileSurveyOverlayIfPresent()
    {
        var overlay = GameObject.Find("ProfileSurveyOverlayCanvas");
        if (overlay != null)
            Destroy(overlay);
    }

    void Start()
    {
        DestroyProfileSurveyOverlayIfPresent();
        Application.wantsToQuit += HandleWantsToQuit;

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnConsentContinueClicked);
        }

        if (questionManager == null)
            questionManager = UnityEngine.Object.FindFirstObjectByType<QuestionManager>();

        if (mainGameUI != null)
            mainGameUI.SetActive(false);

        DeactivateScenarioSelectionOverlay();

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

        ConfigureChatScrollUi();
        EnsureChatInputUi();
    }

    void EnsureChatInputUi()
    {
        if (ChatInput == null || SendChatButton == null)
            return;

        chatInputUi ??= GetComponent<ChatInputUiController>();
        if (chatInputUi == null)
            chatInputUi = gameObject.AddComponent<ChatInputUiController>();

        chatInputUi.Bind(ChatInput, SendChatButton, AskForHelp);
    }

    void ConfigureChatScrollUi()
    {
        ConfigureChatHistoryScroll();
        ConfigureChatInputScroll();
    }

    void ConfigureChatHistoryScroll()
    {
        if (chatScrollRect == null)
            return;

        chatScrollRect.horizontal = false;
        chatScrollRect.vertical = true;
        chatScrollRect.scrollSensitivity = chatScrollSensitivity;
        chatScrollRect.movementType = ScrollRect.MovementType.Clamped;

        if (chatScrollRect.horizontalScrollbar != null)
            chatScrollRect.horizontalScrollbar.gameObject.SetActive(false);

        if (chatScrollRect.viewport != null)
        {
            var viewport = chatScrollRect.viewport;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0f, 1f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-18f, 0f);
        }

        ConfigureChatHistoryScrollContentLayout();
        chatHistoryLayoutConfigured = true;

        if (hintText != null)
            hintText.isTextObjectScaleStatic = true;
    }

    void EnsureChatHistoryScrollContentLayout()
    {
        if (chatHistoryLayoutConfigured)
            return;

        ConfigureChatHistoryScrollContentLayout();
        chatHistoryLayoutConfigured = true;
    }

    void ConfigureChatHistoryScrollContentLayout()
    {
        if (chatScrollRect == null || chatScrollRect.content == null)
            return;

        var content = chatScrollRect.content;
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

        ConfigureChatHistoryTextLayout();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    void ConfigureChatHistoryTextLayout()
    {
        if (hintText == null)
            return;

        var textRect = hintText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        hintText.margin = Vector4.zero;
        hintText.textWrappingMode = TextWrappingModes.Normal;
        hintText.horizontalAlignment = HorizontalAlignmentOptions.Left;
        hintText.verticalAlignment = VerticalAlignmentOptions.Top;

        var fitter = hintText.GetComponent<ContentSizeFitter>() ??
                     hintText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layoutElement = hintText.GetComponent<LayoutElement>() ??
                            hintText.gameObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = 0f;
        layoutElement.preferredWidth = -1f;
        layoutElement.flexibleWidth = 1f;
    }

    void ConfigureChatInputScroll()
    {
        var chatInput = ChatInput;
        if (chatInput == null)
            return;

        chatInput.scrollSensitivity = chatScrollSensitivity;

        if (chatInput.textViewport != null)
        {
            var viewport = chatInput.textViewport;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0f, 1f);
            float scrollbarWidth = 20f;
            if (chatInput.verticalScrollbar != null)
            {
                var scrollbarRect = chatInput.verticalScrollbar.transform as RectTransform;
                if (scrollbarRect != null)
                {
                    float width = scrollbarRect.sizeDelta.x;
                    if (width <= 0f)
                        width = scrollbarRect.rect.width;
                    if (width > 0f)
                        scrollbarWidth = width;
                }
            }

            viewport.offsetMin = new Vector2(12f, 8f);
            viewport.offsetMax = new Vector2(-(scrollbarWidth + 2f), -28f);

            var mask = viewport.GetComponent<RectMask2D>();
            if (mask != null)
                mask.padding = Vector4.zero;
        }

        ConfigureChatInputTextLayout(chatInput.textComponent);
        if (chatInput.placeholder is TMP_Text placeholder)
            ConfigureChatInputTextLayout(placeholder);

        chatInput.SetTextWithoutNotify(chatInput.text);
    }

    static void ConfigureChatInputTextLayout(TMP_Text text)
    {
        if (text == null)
            return;

        var textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        text.margin = Vector4.zero;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        text.verticalAlignment = VerticalAlignmentOptions.Top;
        TrySetEnableExtraPadding(text, false);
        text.isTextObjectScaleStatic = true;
    }

    static void TrySetEnableExtraPadding(TMP_Text tmp, bool value)
    {
        if (tmp == null)
            return;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var property = typeof(TMP_Text).GetProperty("enableExtraPadding", flags);
        if (property != null && property.CanWrite)
        {
            property.SetValue(tmp, value);
            return;
        }

        var field = typeof(TMP_Text).GetField("m_enableExtraPadding", flags);
        field?.SetValue(tmp, value);
    }

    void OnConsentContinueClicked()
    {
        if (!IsConsentComplete())
        {
            var consentUi = UnityEngine.Object.FindFirstObjectByType<ConsentUIController>(FindObjectsInactive.Include);
            consentUi?.RevealValidationHints();
            return;
        }

        if (!PrepareSessionForDataCapture())
            return;

        ApplyUserIdToUI();
        ShowScenarioSelectionAfterConsent();
        QueueSessionCsvDriveUpload();
    }

    void ShowScenarioSelectionAfterConsent()
    {
        if (questionManager != null && questionManager.Consent_Overlay != null)
            questionManager.Consent_Overlay.SetActive(false);

        SetScenarioSelectionPresentation(true);
    }

    void RefreshScenarioSelectionUi()
    {
        if (AreAllConditionsCompleted && questionManager != null)
        {
            if (scenarioSelectionPanel != null)
                scenarioSelectionPanel.SetActive(false);

            questionManager.PresentSessionCompleteFinalScreen();
            return;
        }

        if (scenarioSelectionPanel == null)
            return;

        var selector = scenarioSelectionPanel.GetComponent<ScenarioSelectionController>();
        if (selector == null)
            selector = scenarioSelectionPanel.AddComponent<ScenarioSelectionController>();
        selector.RefreshAllButtonStates();
    }

    /// <summary>Commits participant code, session id, and consent audit row before any CSV writes.</summary>
    bool PrepareSessionForDataCapture()
    {
        CommitParticipantCodeFromInput();
        EnsureSessionUserId();
        return TryLogInformedConsent();
    }

    bool TryLogInformedConsent()
    {
        if (consentRecordSaved)
            return true;

        DataLogger logger = ResolveDataLogger();
        if (logger == null)
        {
            Debug.LogError("ExperimentLogic: DataLogger not found; consent was not saved.");
            NotifyConsentSaveFailure();
            return false;
        }

        if (!consentWrittenToDisk)
        {
            bool saved = logger.SaveConsentRecord(
                GetParticipantCode(),
                GetSessionUserId(),
                ageConsentToggle != null && ageConsentToggle.isOn,
                consentToggle != null && consentToggle.isOn,
                SessionCsvPaths.ConsentFormVersion);

            if (!saved)
            {
                NotifyConsentSaveFailure();
                return false;
            }

            consentWrittenToDisk = true;
        }

        if (!sessionSkeletonCreated && !logger.EnsureSessionCsvSkeleton())
        {
            NotifyConsentSaveFailure();
            return false;
        }

        sessionSkeletonCreated = true;
        consentRecordSaved = true;
        return true;
    }

    void OnApplicationQuit()
    {
        if (!sessionCsvFinalized)
            FinalizeSessionDataLogging(abrupt: true);
    }

    void OnDestroy()
    {
        Application.wantsToQuit -= HandleWantsToQuit;

        if (!sessionCsvFinalized && consentRecordSaved && !sessionDataDiscarded)
            FinalizeSessionDataLogging(abrupt: true);
    }

    bool HandleWantsToQuit()
    {
        PrepareForApplicationExit();
        FinalizeSessionDataLogging(abrupt: false);
        return true;
    }

    public void NotifySessionDiscarded()
    {
        sessionDataDiscarded = true;
    }

    /// <summary>Stops in-flight chat/TTS before application exit to avoid teardown errors.</summary>
    public void PrepareForApplicationExit()
    {
        CancelActiveGeminiRequest();
        CancelActiveSpeech();

        var avatarDisplay = UnityEngine.Object.FindFirstObjectByType<AvatarDisplayController>(FindObjectsInactive.Include);
        if (avatarDisplay != null)
            avatarDisplay.ShutdownForExit();
    }

    /// <summary>Writes [SIN DATOS] rows for CSV files that never received real data.</summary>
    public void FinalizeSessionDataLogging(bool abrupt = false)
    {
        if (sessionCsvFinalized || sessionDataDiscarded || !consentRecordSaved)
            return;

        if (questionManager != null && questionManager.IsChatAssistanceEnabled && questionManager.ActiveScenarioNumber > 0)
            FinishScenarioChatLogging();
        else
            FlushPendingChatLogs();

        DataLogger logger = ResolveDataLogger();
        if (logger == null)
            return;

        if (logger.FinalizeSessionCsvPlaceholders(
                sessionRanConditionA,
                sessionRanConditionB,
                sessionRanConditionC,
                sessionHadChatExchanges,
                abrupt))
            sessionCsvFinalized = true;

        QueueSessionCsvDriveUpload();
    }

    /// <summary>Background upload of CSV data/{P##}_{SessionID}/ to Google Drive (CSVs/…).</summary>
    public void QueueSessionCsvDriveUpload()
    {
        if (!HasCsvDriveUpload || sessionDataDiscarded || !consentRecordSaved)
            return;

        if (csvDriveUploadCoroutine != null)
            StopCoroutine(csvDriveUploadCoroutine);

        csvDriveUploadCoroutine = StartCoroutine(SessionCsvDriveUpload.UploadCoroutine(
            csvDriveUploadUrl,
            csvDriveUploadSecret,
            GetParticipantCode(),
            GetSessionUserId(),
            OnSessionCsvDriveUploadFinished));
    }

    void OnSessionCsvDriveUploadFinished(bool success, string message)
    {
        csvDriveUploadCoroutine = null;

        if (success)
        {
#if UNITY_EDITOR
            Debug.Log($"<color=cyan>CSV DRIVE UPLOAD OK:</color> CSVs/{message}");
#endif
            return;
        }

        Debug.LogWarning("CSV Drive upload failed: " + message);
    }

    void MarkConditionRun(string conditionCode)
    {
        switch (conditionCode)
        {
            case "A": sessionRanConditionA = true; break;
            case "B": sessionRanConditionB = true; break;
            case "C": sessionRanConditionC = true; break;
        }
    }

    public static string ConditionCodeFromScenarioIndex(int scenarioIndex)
    {
        return scenarioIndex switch
        {
            0 => "A",
            1 => "B",
            2 => "C",
            _ => ""
        };
    }

    public void MarkConditionCompleted(string conditionCode)
    {
        switch (conditionCode)
        {
            case "A": sessionCompletedConditionA = true; break;
            case "B": sessionCompletedConditionB = true; break;
            case "C": sessionCompletedConditionC = true; break;
        }
    }

    public bool IsConditionCompleted(int scenarioIndex)
    {
        return scenarioIndex switch
        {
            0 => sessionCompletedConditionA,
            1 => sessionCompletedConditionB,
            2 => sessionCompletedConditionC,
            _ => false
        };
    }

    public bool AreAllConditionsCompleted =>
        sessionCompletedConditionA && sessionCompletedConditionB && sessionCompletedConditionC;

    /// <summary>First incomplete block in the participant's assigned order, or -1 if all three are done.</summary>
    public int GetNextAllowedScenarioIndex()
    {
        if (!ParticipantConditionOrder.TryGetOrderForParticipant(GetParticipantCode(), out int[] order))
            return 0;

        foreach (int scenarioIndex in order)
        {
            if (!IsConditionCompleted(scenarioIndex))
                return scenarioIndex;
        }

        return -1;
    }

    public bool IsScenarioAllowedNow(int scenarioIndex)
    {
        if (IsConditionCompleted(scenarioIndex))
            return false;

        return GetNextAllowedScenarioIndex() == scenarioIndex;
    }

    void NotifyConsentSaveFailure()
    {
        var consentUi = UnityEngine.Object.FindFirstObjectByType<ConsentUIController>(FindObjectsInactive.Include);
        if (consentUi != null)
            consentUi.ShowConsentError(ConsentSaveFailureParticipantMessage);
        else
            Debug.LogError(ConsentSaveFailureParticipantMessage);
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
        chatInputUi?.RefreshCharacterCounter(inputText);

        if (SendChatButton == null)
            return;

        bool hasText = !string.IsNullOrWhiteSpace(inputText);
        if (hasText == lastInputHadText)
            return;

        lastInputHadText = hasText;
        SendChatButton.interactable = !geminiInFlight && hasText;
    }

    void SetGeminiInFlight(bool inFlight)
    {
        geminiInFlight = inFlight;
        chatInputUi?.SetGeminiWaiting(inFlight);

        if (ChatInput != null)
            ChatInput.interactable = !inFlight;

        if (SendChatButton != null)
            SendChatButton.interactable = !inFlight &&
                ChatInput != null && !string.IsNullOrWhiteSpace(ChatInput.text);
    }

    /// <summary>Starts scenario 0 (A), 1 (B), or 2 (C). Wire each INICIAR button with the matching int in the Inspector.</summary>
    public void OnScenarioSelected(int scenarioIndex)
    {
        if (!IsConsentComplete() || !PrepareSessionForDataCapture())
            return;

        if (!IsScenarioAllowedNow(scenarioIndex))
            return;

        if (questionManager != null)
            questionManager.BeginScenario(scenarioIndex);

        if (questionManager != null)
            MarkConditionRun(questionManager.ActiveConditionCode);

        EnterMainExperimentUi();
        ResetQuestionTimer();
    }

    /// <summary>Single tutorial item before the first real block. Agent UI matches the participant's first assigned condition.</summary>
    public void OnPracticeSelected()
    {
        if (!IsConsentComplete() || !PrepareSessionForDataCapture())
            return;

        if (questionManager != null)
            questionManager.BeginPracticeBlock(GetFirstBlockScenarioIndexForParticipant());

        EnterMainExperimentUi();
        ResetQuestionTimer();
    }

    public bool CanStartPracticeBlock() => questionManager != null;

    public static int GetFirstBlockScenarioIndexForParticipant(string participantCode)
    {
        return ParticipantConditionOrder.GetFirstBlockScenarioIndex(participantCode);
    }

    int GetFirstBlockScenarioIndexForParticipant() =>
        GetFirstBlockScenarioIndexForParticipant(GetParticipantCode());

    void EnterMainExperimentUi()
    {
        DeactivateScenarioSelectionOverlay();
        SetScenarioSelectionBackground(false);
        SetCanvasFlatBackdropVisible(true);
        SetMainGamePresentation(true);

        if (questionManager != null && questionManager.finalOptionsPanel != null)
            questionManager.finalOptionsPanel.SetActive(false);

        if (questionManager != null && questionManager.Consent_Overlay != null)
            questionManager.Consent_Overlay.SetActive(false);

        if (questionManager != null && questionManager.IsChatAssistanceEnabled)
            EnsureChatInputUi();

        var avatarDisplay = UnityEngine.Object.FindFirstObjectByType<AvatarDisplayController>();
        if (avatarDisplay != null)
            StartCoroutine(RefreshAvatarDisplayNextFrame(avatarDisplay));
    }

    public void ReturnToScenarioSelectionAfterPractice()
    {
        CancelActiveGeminiRequest();
        CancelActiveSpeech();

        if (questionManager != null)
            questionManager.ResetUIForNewScenario();

        SetScenarioSelectionPresentation(true);
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
        lastInputHadText = false;
        chatInputUi?.RefreshCharacterCounter("");
        if (SendChatButton != null)
            SendChatButton.interactable = false;

        CancelActiveGeminiRequest();
        activeGeminiCoroutine = StartCoroutine(CallGemini(studentText));
    }

    public void CancelActiveGeminiRequest()
    {
        geminiRequestGeneration++;

        if (activeGeminiCoroutine != null)
        {
            StopCoroutine(activeGeminiCoroutine);
            activeGeminiCoroutine = null;
        }

        if (geminiInFlight)
        {
            RevertPendingStudentMessage();
            SetGeminiInFlight(false);
        }
    }

    bool IsGeminiResponseStillValid(int generation, int questionIndex)
    {
        if (generation != geminiRequestGeneration)
            return false;

        if (questionManager == null)
            return false;

        return questionManager.currentQuestionIndex == questionIndex;
    }

    static string BuildSystemPreamble(string scenarioText)
    {
        return
            "CONTEXTO DEL CASO (ficticio; solo para que conozcas las reglas y la situación):\n" +
            scenarioText +
            "\n\nROL: Sos un tutor que ayuda a un participante adulto a resolver el caso. " +
            "El participante que escribe en el chat NO es ningún nombre del enunciado " +
            "(Carlos, Laura, Valentina, etc. son personajes ficticios del caso). " +
            "NUNCA le digas al participante que es ese personaje ni lo llames por nombres del caso. " +
            "Tratalo con 'vos' de forma neutra, sin asumir género ni identidad.\n\n" +
            "REGLAS ESTRICTAS:\n" +
            "1) NUNCA des la respuesta final ni indiques qué opción (A, B, C o D) es correcta.\n" +
            "2) Solo ayudá con las reglas o el razonamiento cuando el participante lo pida o pregunte algo concreto sobre el caso.\n" +
            "3) Si solo te saluda o hace charla social (por ejemplo «hola», «qué tal», «hey»), respondé breve y natural al saludo únicamente. " +
            "NO des pistas, NO analices el caso y NO menciones reglas hasta que pregunte algo sobre la tarea.\n" +
            "4) Si saluda y además pregunta algo del caso, podés saludar brevemente y después responder solo lo que preguntó.\n" +
            "5) Si pide la respuesta directa o pregunta si una opción concreta es la correcta (por ejemplo «¿es la opción B?», «¿No — Lista de Espera?»), " +
            "NO confirmes ni niegues. Explicale con amabilidad que en este ejercicio no podés validar opciones ni dar la respuesta final — tu rol es ayudarle a razonar con las reglas — " +
            "y ofrecé revisar juntos la parte que le genere duda. Nunca suenes frío ni evasivo.\n" +
            "6) NUNCA felicites ni valides la elección del participante (prohibido: «excelente», «correcto», «acertaste», «lograste conectar los puntos», " +
            "«esa es la respuesta», «muy bien elegido», etc.), aunque repita el texto de una opción.\n" +
            "7) Si insiste en la respuesta directa, reconocé su esfuerzo o la duda, explicá brevemente por qué no podés decirle la opción correcta y redirigí con una pregunta guía sobre las reglas.\n" +
            "8) Respuestas breves (máximo 2 o 3 oraciones), salvo que pida más detalle o estés explicando con amabilidad por qué no podés validar una opción (ahí podés usar hasta 4 oraciones).\n" +
            "9) Español costarricense con «vos». Tono profesional, cálido, empático y conciso.";
    }

    IEnumerator CallGemini(string userMessage)
    {
        int generation = geminiRequestGeneration;
        int questionIndex = questionManager != null ? questionManager.currentQuestionIndex : -1;

        SetGeminiInFlight(true);
        geminiExchangeStartTime = Time.unscaledTime;
        RefreshPendingStudentMessage(userMessage);

        yield return null;

        if (!IsGeminiResponseStillValid(generation, questionIndex))
        {
            RevertPendingStudentMessage();
            SetGeminiInFlight(false);
            activeGeminiCoroutine = null;
            yield break;
        }

        string url = endpoint + apiKey;
        string json = BuildGeminiRequestJson(BuildSystemPreamble(GetCurrentScenarioText()), userMessage, apiTurns);

        try
        {
            int maxRetries = 3;
            int retryDelay = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                if (!IsGeminiResponseStillValid(generation, questionIndex))
                {
                    RevertPendingStudentMessage();
                    yield break;
                }

                using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
                {
                    byte[] body = Encoding.UTF8.GetBytes(json);
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");

                    yield return request.SendWebRequest();

                    if (!IsGeminiResponseStillValid(generation, questionIndex))
                    {
                        RevertPendingStudentMessage();
                        yield break;
                    }

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
                            if (string.IsNullOrWhiteSpace(answer))
                            {
                                Debug.LogWarning("Gemini: Empty text in response.");
                                RecordChatApiFailure(userMessage, "empty_response", 200, "empty_response_text");
                                RevertPendingStudentMessage();
                                AppendChatNotice("No recibí respuesta del agente. Intenta enviar tu mensaje de nuevo.");
                                yield break;
                            }

                            float geminiLatencySeconds = Time.unscaledTime - geminiExchangeStartTime;

                            if (!IsGeminiResponseStillValid(generation, questionIndex))
                            {
                                RevertPendingStudentMessage();
                                yield break;
                            }

                            RecordApiTurn("user", userMessage);
                            CommitStudentMessage(userMessage);
                            ProcessResponse(answer, geminiLatencySeconds);
                            yield break;
                        }

                        Debug.LogWarning("Gemini: Empty response.");
                        RecordChatApiFailure(userMessage, "empty_response", 200, "empty_response_body");
                        RevertPendingStudentMessage();
                        AppendChatNotice("No recibí respuesta del agente. Intenta enviar tu mensaje de nuevo.");
                        yield break;
                    }

                    long code = request.responseCode;

                    if (code == 429)
                    {
                        Debug.LogWarning("Gemini API 429 Rate Limit hit.");
                        RecordChatApiFailure(userMessage, "rate_limit", code, "http_429");
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
                        Debug.LogError($"Gemini API Error: {code} ({request.result})");
                        string eventType = request.result == UnityWebRequest.Result.ConnectionError
                            ? "network_error"
                            : code == 503 ? "service_unavailable" : "http_error";
                        RecordChatApiFailure(userMessage, eventType, code, request.result + "_http_" + code);
                        RevertPendingStudentMessage();
                        AppendChatNotice("No pude conectar con el agente en este momento. Revisa tu conexión e inténtalo de nuevo.");
                        yield break;
                    }
                }
            }

            RecordChatApiFailure(userMessage, "exhausted_retries", 0, "max_retries_reached");
            RevertPendingStudentMessage();
        }
        finally
        {
            activeGeminiCoroutine = null;
            SetGeminiInFlight(false);
        }
    }

    void RefreshPendingStudentMessage(string userMessage, string statusLine = null)
    {
        string pending = chatHistory + "\n<b>Estudiante:</b> " + userMessage + "\n";
        if (!string.IsNullOrEmpty(statusLine))
            pending += statusLine + "\n";
        else
            pending += "<i><color=#9EBFC2>Esperando respuesta del agente…</color></i>\n";

        RefreshChatDisplay(pending);
    }

    void RevertPendingStudentMessage()
    {
        RefreshChatDisplay(chatHistory);
    }

    void CommitStudentMessage(string userMessage)
    {
        lastCommittedStudentMessage = userMessage;
        chatHistory += "\n<b>Estudiante:</b> " + userMessage + "\n";
    }

    void RecordApiTurn(string role, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        string trimmed = text.Trim();
        apiTurns.Add(new ApiChatTurn { role = role, text = trimmed });

        while (apiTurns.Count > MaxApiTurnsPerQuestion)
            apiTurns.RemoveAt(0);

        if (questionManager != null && questionManager.IsPracticeMode)
            return;

        if (!chatSessionLogger.RecordTurn(ResolveDataLogger(), questionManager, role, trimmed))
            NotifyDataSaveFailure(chatContext: true);
    }

    int ScoreAndLogChatExchange(string studentMessage, string modelMessage, float geminiLatencySeconds)
    {
        if (questionManager != null && questionManager.IsPracticeMode)
            return 1;
        int exchangeIndex = chatSessionLogger.RecordExchange(
            ResolveDataLogger(),
            questionManager,
            studentMessage,
            modelMessage,
            GetQuestionContext(),
            Time.time - questionStartTime,
            geminiLatencySeconds);

        if (exchangeIndex <= 0)
        {
            NotifyDataSaveFailure(chatContext: true);
            return 0;
        }

        sessionHadChatExchanges = true;
        return exchangeIndex;
    }

    void RecordChatApiFailure(string userMessage, string eventType, long httpStatusCode, string failureReason)
    {
        if (questionManager == null || !questionManager.IsChatAssistanceEnabled)
            return;

        if (questionManager.IsPracticeMode)
            return;

        var logger = ResolveDataLogger();
        if (logger == null)
            return;

        float latency = Time.unscaledTime - geminiExchangeStartTime;
        if (!logger.SaveChatApiEvent(
                questionManager.ActiveScenarioNumber,
                questionManager.ActiveScenarioName,
                questionManager.ActiveConditionCode,
                questionManager.currentQuestionIndex + 1,
                eventType,
                httpStatusCode,
                failureReason,
                userMessage,
                latency))
            NotifyDataSaveFailure(chatContext: true);
    }

    bool FlushQuestionChatSummary(int questionNumber, float questionEndSecondsSinceStart, bool includeWhenNoActivity = true)
    {
        if (questionManager != null && questionManager.IsPracticeMode)
            return true;

        if (!chatSessionLogger.FlushQuestionSummary(
                ResolveDataLogger(),
                questionManager,
                questionNumber,
                questionEndSecondsSinceStart,
                includeWhenNoActivity))
        {
            NotifyDataSaveFailure(chatContext: true);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Persists in-progress chat metrics when the session is kept (e.g. safe exit mid-scenario).
    /// </summary>
    public void FlushPendingChatLogs()
    {
        if (questionManager == null || !questionManager.IsChatAssistanceEnabled)
            return;

        if (questionManager.IsPracticeMode)
            return;

        int questionNumber = questionManager.currentQuestionIndex + 1;
        float questionEndSeconds = Time.time - questionStartTime;

        if (chatSessionLogger.HasPendingQuestionActivity)
        {
            FlushQuestionChatSummary(questionNumber, questionEndSeconds, includeWhenNoActivity: false);
            chatSessionLogger.ResetQuestionMetrics();
        }

        if (chatSessionLogger.HasScenarioActivity && !chatSessionLogger.ScenarioSummaryFlushed)
        {
            if (!chatSessionLogger.FlushScenarioSummary(
                    ResolveDataLogger(),
                    questionManager,
                    questionManager.totalQuestions))
                NotifyDataSaveFailure(chatContext: true);
        }
    }

    void CancelActiveSpeech()
    {
        AgentSpeechController speech = ResolveAgentSpeech();
        if (speech != null)
            speech.CancelSpeech();
    }

    AgentSpeechController ResolveAgentSpeech()
    {
        AgentSpeechController speech = GetComponent<AgentSpeechController>();
        return speech != null ? speech : UnityEngine.Object.FindFirstObjectByType<AgentSpeechController>();
    }

    DataLogger ResolveDataLogger()
    {
        if (questionManager != null && questionManager.dataLogger != null)
            return questionManager.dataLogger;

        return UnityEngine.Object.FindFirstObjectByType<DataLogger>();
    }

    string GetQuestionContext()
    {
        if (questionManager == null)
            return "";

        return ChatHelpScoring.BuildQuestionContext(questionManager.GetCurrentQuestion());
    }

    public bool FinishScenarioChatLogging()
    {
        if (questionManager == null || !questionManager.IsChatAssistanceEnabled)
            return true;

        if (questionManager.IsPracticeMode)
            return true;

        CancelActiveGeminiRequest();
        CancelActiveSpeech();
        FlushPendingChatLogs();

        if (!chatSessionLogger.FlushScenarioSummary(
                ResolveDataLogger(),
                questionManager,
                questionManager.totalQuestions))
        {
            NotifyDataSaveFailure(chatContext: true);
            return false;
        }

        return true;
    }

    TtsExchangeContext BuildTtsContext(int exchangeIndex)
    {
        if (questionManager == null)
            return default;

        return new TtsExchangeContext
        {
            ScenarioNumber = questionManager.ActiveScenarioNumber,
            ScenarioName = questionManager.ActiveScenarioName,
            ConditionCode = questionManager.ActiveConditionCode,
            QuestionNumber = questionManager.currentQuestionIndex + 1,
            ExchangeIndex = exchangeIndex
        };
    }

    public bool RecordTtsOutcome(TtsExchangeContext context, bool success, string failureReason)
    {
        if (questionManager != null && questionManager.IsPracticeMode)
            return true;

        return chatSessionLogger.RecordTtsOutcome(ResolveDataLogger(), context, success, failureReason ?? "");
    }

    void ProcessResponse(string rawText, float geminiLatencySeconds)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return;

        string cleanText = rawText.Replace("*", "").Replace("#", "").Replace("\"", "").Trim();
        if (string.IsNullOrEmpty(cleanText))
            return;

        if (ChatHelpScoring.TryGetSafeReplyForAnswerAffirmation(lastCommittedStudentMessage, cleanText, out string safeReply) ||
            ChatHelpScoring.TryGetSafeReplyForModelLeak(lastCommittedStudentMessage, cleanText, out safeReply))
            cleanText = safeReply;

        chatHistory += "\nProfesor: " + cleanText + "\n";
        RefreshChatDisplay(chatHistory);

        int expectedExchangeIndex = chatSessionLogger.LastExchangeIndex + 1;
        AgentSpeechController speech = ResolveAgentSpeech();
        if (speech != null && questionManager != null && questionManager.characterModel != null &&
            questionManager.characterModel.activeInHierarchy)
        {
            speech.Speak(cleanText, BuildTtsContext(expectedExchangeIndex));
        }

        if (chatFinalizeCoroutine != null)
            StopCoroutine(chatFinalizeCoroutine);
        chatFinalizeCoroutine = StartCoroutine(FinalizeChatResponse(cleanText, geminiLatencySeconds));
    }

    IEnumerator FinalizeChatResponse(string cleanText, float geminiLatencySeconds)
    {
        yield return null;

        RecordApiTurn("model", cleanText);
        ScoreAndLogChatExchange(lastCommittedStudentMessage, cleanText, geminiLatencySeconds);
        chatFinalizeCoroutine = null;
    }

    /// <summary>Secondary CSV export to Logs/ (legacy schema). Primary analysis uses <see cref="DataLogger"/>.</summary>
    public void SaveDataToCSV(string actionType, string actionDetail, string correctAnswer = "", int scenarioNumber = 0, string scenarioName = "", int questionNumber = 0)
    {
        if (questionManager != null && questionManager.IsPracticeMode)
            return;

        try
        {
            string directory = Path.Combine(Application.dataPath, "..", "Logs");
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string participant = GetParticipantCode();
            string sessionId = currentUserID ?? "unknown";
            string filePath = Path.Combine(directory, SessionCsvPaths.BuildLegacyLogsFileName(participant, sessionId));
            bool isNewFile = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;

            if (questionNumber <= 0 && questionManager != null)
                questionNumber = questionManager.currentQuestionIndex + 1;

            float timeSpent = Time.time - questionStartTime;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            const string header = "ParticipantCode,SessionID,ScenarioNumber,ScenarioName,QuestionNumber,ActionType,ActionDetail,CorrectAnswer,TimeSpent(s),Timestamp";
            string row = string.Join(",",
                CsvFileWriter.Escape(participant),
                CsvFileWriter.Escape(sessionId),
                scenarioNumber.ToString(),
                CsvFileWriter.Escape(scenarioName),
                questionNumber.ToString(),
                CsvFileWriter.Escape(actionType),
                CsvFileWriter.Escape(actionDetail),
                CsvFileWriter.Escape(correctAnswer),
                timeSpent.ToString("F2"),
                CsvFileWriter.Escape(timestamp));

            if (isNewFile)
                CsvFileWriter.WriteAllText(filePath, header + "\n" + row + "\n", new UTF8Encoding(true));
            else
                CsvFileWriter.AppendLine(filePath, row + "\n", utf8Bom: false);
        }
        catch (Exception ex)
        {
            Debug.LogError("SaveDataToCSV failed: " + ex.Message);
            NotifyDataSaveFailure();
        }
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
        sessionStartedUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ApplyUserIdToUI();
    }

    void ApplyUserIdToUI()
    {
        if (userIdText == null)
            return;

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(participantCode))
            parts.Add("Participante: " + participantCode);
        if (!string.IsNullOrEmpty(currentUserID))
            parts.Add("Sesión: " + currentUserID);

        userIdText.text = parts.Count > 0 ? string.Join(" · ", parts) : "";
    }

    public string GetSessionUserId()
    {
        if (string.IsNullOrEmpty(currentUserID))
            return "Unknown";

        return currentUserID;
    }

    public void ResetForNewScenario()
    {
        CancelActiveGeminiRequest();
        CancelActiveSpeech();
        FlushPendingChatLogs();

        chatHistory = "";
        apiTurns.Clear();
        chatSessionLogger.ResetScenarioMetrics();
        lastCommittedStudentMessage = "";
        if (hintText != null) hintText.text = "Aquí verás tu conversación con el agente…";
        if (ChatInput != null) ChatInput.text = "";
        if (questionManager != null) questionManager.ResetUIForNewScenario();
        SetMainGamePresentation(true);
        if (questionManager != null && questionManager.finalOptionsPanel != null)
            questionManager.finalOptionsPanel.SetActive(false);

        if (questionManager != null && questionManager.characterModel != null)
            questionManager.characterModel.SetActive(false);
    }

    public void ResetToScenarioSelection()
    {
        if (questionManager != null && !questionManager.CanLeaveBlockCompletionScreen())
            return;

        CancelActiveGeminiRequest();
        CancelActiveSpeech();

        if (AreAllConditionsCompleted)
        {
            if (scenarioSelectionPanel != null)
                scenarioSelectionPanel.SetActive(false);

            if (questionManager != null)
                questionManager.PresentSessionCompleteFinalScreen();
            else
                SetMainGamePresentation(true);

            return;
        }

        if (questionManager != null)
            questionManager.ResetUIForNewScenario();

        SetScenarioSelectionPresentation(true);
    }

    public void FinalizeAndResetSession()
    {
        DeactivateScenarioSelectionOverlay();
        FinalizeSessionDataLogging(abrupt: false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void SetChatPanelVisible(bool visible, bool flushChatSummaryOnHide = true)
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

                ConfigureChatScrollUi();
                EnsureChatPanelDrawOrder();
            }
        }

        if (!visible)
        {
            CancelActiveGeminiRequest();
            CancelActiveSpeech();

            if (ChatInput != null)
                ChatInput.text = string.Empty;

            if (SendChatButton != null)
                SendChatButton.interactable = false;

            if (flushChatSummaryOnHide)
                ClearChatHistory();
            else
                ResetChatDisplayOnly();
        }
        else
        {
            CancelActiveGeminiRequest();
            chatHistory = "";
            apiTurns.Clear();
            lastCommittedStudentMessage = "";
            chatSessionLogger.ResetQuestionMetrics();
            RefreshChatDisplay("Aquí verás tu conversación con el agente…", new Color(0.7f, 0.7f, 0.7f, 1f));
        }
    }

    void ResetChatDisplayOnly()
    {
        chatHistory = "";
        apiTurns.Clear();
        lastCommittedStudentMessage = "";
        chatSessionLogger.ResetQuestionMetrics();
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

        EnsureChatHistoryScrollContentLayout();

        hintText.gameObject.SetActive(true);
        if (color.HasValue)
            hintText.color = color.Value;

        if (hintText.text == text)
        {
            RequestChatScrollToBottom();
            return;
        }

        hintText.text = text;
        hintText.ForceMeshUpdate(false, false);

        if (chatScrollRect != null && chatScrollRect.content != null)
            LayoutRebuilder.MarkLayoutForRebuild(chatScrollRect.content);

        RequestChatScrollToBottom();
    }

    void RequestChatScrollToBottom()
    {
        if (chatScrollRect == null || !chatScrollRect.gameObject.activeInHierarchy)
            return;

        if (scrollCoroutine != null)
            StopCoroutine(scrollCoroutine);
        scrollCoroutine = StartCoroutine(ScrollToBottomRoutine());
    }

    public void ClearChatHistory()
    {
        CancelActiveGeminiRequest();
        CancelActiveSpeech();

        int questionNumber = questionManager != null ? questionManager.currentQuestionIndex + 1 : 0;
        float questionEndSeconds = questionManager != null ? Time.time - questionStartTime : 0f;

        chatHistory = "";
        apiTurns.Clear();
        lastCommittedStudentMessage = "";

        FlushQuestionChatSummary(questionNumber, questionEndSeconds);
        chatSessionLogger.ResetQuestionMetrics();

        if (hintText != null && chatScrollRect != null && chatScrollRect.gameObject.activeInHierarchy)
            RefreshChatDisplay("Aquí verás tu conversación con el agente…", new Color(0.7f, 0.7f, 0.7f, 1f));
    }

    public void AppendChatNotice(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        chatHistory += "\n<color=#ffaa00>" + message + "</color>\n";
        RefreshChatDisplay(chatHistory);
    }

    public void NotifyDataSaveFailure(bool chatContext = false)
    {
        if (Time.unscaledTime - lastSaveFailureNoticeTime < 2f)
            return;

        lastSaveFailureNoticeTime = Time.unscaledTime;

        string message = chatContext ? ChatSaveFailureParticipantMessage : SaveFailureParticipantMessage;

        if (questionManager != null && !questionManager.IsChatAssistanceEnabled)
        {
            questionManager.ShowPrimarySaveFailureNotice(message);
            return;
        }

        if (hintText != null)
            hintText.gameObject.SetActive(true);

        AppendChatNotice(message);
    }

    IEnumerator ScrollToBottomRoutine()
    {
        yield return null;

        if (chatScrollRect?.content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(chatScrollRect.content);

        if (chatScrollRect != null)
            chatScrollRect.verticalNormalizedPosition = 0f;

        scrollCoroutine = null;
    }

    #region GeminiApiTypes

    static string BuildGeminiRequestJson(string systemPreamble, string newUserMessage, List<ApiChatTurn> priorTurns)
    {
        var request = new GeminiGenerateRequest
        {
            contents = BuildConversationContents(systemPreamble, newUserMessage, priorTurns),
            generationConfig = new GeminiGenerationConfig
            {
                maxOutputTokens = 1024,
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
