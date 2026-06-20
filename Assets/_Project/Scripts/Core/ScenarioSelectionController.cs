using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Practice entry and scenario selection buttons (A/B/C). Disables completed blocks and locks out-of-order selection.
/// </summary>
[DisallowMultipleComponent]
public class ScenarioSelectionController : MonoBehaviour
{
    const string PracticeButtonName = "Btn_Practice";
    const string ProfileSurveyButtonName = "Btn_ProfileSurvey";
    const string ProfileSurveySubtitleName = "ProfileSurveySubtitle";
    const string OrderHintName = "ConditionOrderHint";
    static readonly Color ScenarioSelectionPanelTint = new(0.41568628f, 0.41568628f, 0.41568628f, 1f);
    const string ConditionLetterBadgeName = "ConditionLetterBadge";
    const string StatusSubtitleName = "StatusSubtitle";
    static readonly Color LetterBadgeFill = new(0.08f, 0.18f, 0.2f, 0.95f);
    static readonly Color LetterBadgeBorder = new(0.12f, 0.82f, 0.82f, 0.85f);
    static readonly Color LetterBadgeText = new(0.2f, 0.92f, 0.92f, 1f);
    const string CompletedStatusText = "COMPLETADO";
    const string ProfileSurveyPendingSubtitle =
        "Abre en el navegador · tu código P## ya va completado · ~2 min";
    const string ProfileSurveyAwaitingSubtitle =
        "Completá y enviá el formulario en el navegador";
    const string ProfileSurveyConfirmSubtitle =
        "Si ya enviaste, pulsá de nuevo para confirmar";
    const float HintTopInset = 28f;
    const float HintBlockHeight = 150f;
    const float HintToButtonsGap = 20f;
    const float ButtonHeight = 108f;
    const float ButtonSpacing = 18f;
    const float ButtonWidth = 920f;
    const float HintWidth = 920f;

    static readonly (string keyword, int scenarioIndex)[] ScenarioLabelKeywords =
    {
        ("SIN ASISTENCIA", 0),
        ("AGENTE DE TEXTO", 1),
        ("AGENTE VIRTUAL", 2),
    };

    static readonly string[] DefaultScenarioTitles =
    {
        "INICIAR: SIN ASISTENCIA",
        "INICIAR: AGENTE DE TEXTO",
        "INICIAR: AGENTE VIRTUAL",
    };

    sealed class ScenarioButtonUi
    {
        public Button Button;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI StatusSubtitle;
        public string OriginalTitle;
    }

    ExperimentLogic experimentLogic;
    Button practiceButton;
    Button profileSurveyButton;
    TextMeshProUGUI profileSurveyTitle;
    TextMeshProUGUI profileSurveyStatusSubtitle;
    TextMeshProUGUI orderHintText;
    TextMeshProUGUI practiceTitle;
    TextMeshProUGUI practiceStatusSubtitle;
    string practiceOriginalTitle = "INICIAR: PRÁCTICA";
    readonly Dictionary<int, ScenarioButtonUi> scenarioButtonUi = new Dictionary<int, ScenarioButtonUi>();
    bool scenarioButtonsCached;
    bool ensureProfileSurveyInProgress;

    void Awake()
    {
        experimentLogic = FindFirstObjectByType<ExperimentLogic>();
        EnsureFullScreenContainer();
        DisableConflictingAutoLayout();
        EnsureProfileSurveyButton();
        EnsurePracticeButton();
        CacheScenarioButtons();
        LayoutSelectionPanel();
    }

    void OnEnable()
    {
        if (experimentLogic == null)
            experimentLogic = FindFirstObjectByType<ExperimentLogic>();

        EnsureFullScreenContainer();
        DisableConflictingAutoLayout();
        EnsureProfileSurveyButton();
        EnsurePracticeButton();
        CacheScenarioButtons();
        RefreshAllButtonStates();
        LayoutSelectionPanel();
    }

    public void PrepareForDisplay()
    {
        EnsureFullScreenContainer();
        LayoutSelectionPanel();
    }

    void EnsureFullScreenContainer()
    {
        var panel = transform as RectTransform;
        if (panel == null)
            return;

        Transform popup = FindCanvasChild("Popup");
        if (popup != null)
        {
            if (panel.parent != popup)
                panel.SetParent(popup, false);

            if (popup is RectTransform popupRect)
                StretchRectToParent(popupRect);

            popup.gameObject.SetActive(true);

            if (popup.TryGetComponent(out Image popupImage))
            {
                popupImage.raycastTarget = false;
                popupImage.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        StretchRectToParent(panel);

        if (TryGetComponent(out Image panelImage))
        {
            panelImage.raycastTarget = false;
            panelImage.color = ScenarioSelectionPanelTint;
        }
    }

    const float SelectionButtonCenterX = 960f;

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

    static void StretchRectToParent(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    void DisableConflictingAutoLayout()
    {
        if (transform.TryGetComponent(out VerticalLayoutGroup verticalLayout))
            verticalLayout.enabled = false;

        if (transform.TryGetComponent(out ContentSizeFitter sizeFitter))
            sizeFitter.enabled = false;
    }

    static void IgnoreParentLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        if (!rect.TryGetComponent(out LayoutElement layoutElement))
            layoutElement = rect.gameObject.AddComponent<LayoutElement>();

        layoutElement.ignoreLayout = true;
    }

    float FirstButtonCenterY()
    {
        return -370f;
    }

    float ButtonLayoutStep()
    {
        return ButtonHeight + ButtonSpacing;
    }

    void LayoutSelectionPanel()
    {
        LayoutOrderHint();
        LayoutScenarioButtons();
    }

    void LayoutOrderHint()
    {
        EnsureOrderHint();
        if (orderHintText == null)
            return;

        var rect = orderHintText.rectTransform;
        IgnoreParentLayout(rect);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -HintTopInset);
        rect.sizeDelta = new Vector2(HintWidth, HintBlockHeight);

        orderHintText.lineSpacing = 6f;
        orderHintText.margin = new Vector4(8f, 4f, 8f, 4f);
        orderHintText.overflowMode = TextOverflowModes.Overflow;

        rect.SetAsLastSibling();
    }

    public void RefreshAllButtonStates()
    {
        EnsureProfileSurveyButton();
        RefreshConditionOrderHint();
        RefreshProfileSurveyButtonState();
        RefreshPracticeButtonState();
        RefreshScenarioButtonStates();
        LayoutScenarioButtons();
    }

    public void RefreshConditionOrderHint()
    {
        if (experimentLogic == null)
            experimentLogic = FindFirstObjectByType<ExperimentLogic>();

        EnsureOrderHint();
        if (orderHintText == null)
            return;

        string code = experimentLogic != null ? experimentLogic.GetParticipantCode() : "";
        string hint = ParticipantConditionOrder.FormatOrderHintRichText(code);
        orderHintText.text = string.IsNullOrEmpty(hint)
            ? BuildEmptyOrderHintText()
            : AppendProfileSurveyHint(hint);

        LayoutOrderHint();
    }

    string AppendProfileSurveyHint(string baseHint)
    {
        if (experimentLogic == null || !experimentLogic.HasParticipantProfileSurvey)
            return baseHint;

        if (experimentLogic.IsProfileSurveyCompleted)
        {
            return baseHint
                   + "\n<size=20><color=#6EEDC8>Perfil del participante: COMPLETADO.</color></size>";
        }

        if (experimentLogic.IsProfileSurveyAwaitingVerification)
        {
            return baseHint
                   + "\n<size=20><color=#9EBFC2>Perfil: completá y enviá el formulario en el navegador. "
                   + "La app verificará el envío al volver.</color></size>";
        }

        return baseHint
               + "\n<size=20><color=#9EBFC2>Opcional: pulsá «Perfil del participante» para abrir el formulario en el navegador (~2 min). "
               + "Tu código P## ya va completado; volvé aquí al terminar.</color></size>";
    }

    public void NotifyProfileSurveyOpened()
    {
        if (experimentLogic == null)
            experimentLogic = FindFirstObjectByType<ExperimentLogic>();

        EnsureProfileSurveyButton();
        RefreshProfileSurveyButtonState();
        RefreshConditionOrderHint();
    }

    public void RefreshProfileSurveyButtonState()
    {
        if (profileSurveyButton == null)
            return;

        if (experimentLogic == null)
            experimentLogic = FindFirstObjectByType<ExperimentLogic>();

        if (experimentLogic == null)
            return;

        if (profileSurveyTitle == null)
            profileSurveyTitle = GetTitleText(profileSurveyButton);

        if (profileSurveyStatusSubtitle == null)
        {
            var subtitleTransform = profileSurveyButton.transform.Find(ProfileSurveySubtitleName);
            if (subtitleTransform != null)
                profileSurveyStatusSubtitle = subtitleTransform.GetComponent<TextMeshProUGUI>();
        }

        bool completed = experimentLogic.IsProfileSurveyCompleted;
        bool awaiting = experimentLogic.IsProfileSurveyAwaitingVerification;
        bool manualConfirmMode = awaiting
                                 && !completed
                                 && !experimentLogic.HasFormResponseVerification;

        profileSurveyButton.interactable = !completed && (!awaiting || manualConfirmMode);

        if (profileSurveyTitle != null)
        {
            profileSurveyTitle.text = manualConfirmMode
                ? "CONFIRMAR ENVÍO"
                : "PERFIL DEL PARTICIPANTE";
        }

        if (profileSurveyStatusSubtitle != null)
        {
            profileSurveyStatusSubtitle.text = completed
                ? CompletedStatusText
                : awaiting
                    ? experimentLogic.HasFormResponseVerification
                        ? ProfileSurveyAwaitingSubtitle
                        : ProfileSurveyConfirmSubtitle
                    : ProfileSurveyPendingSubtitle;
            profileSurveyStatusSubtitle.gameObject.SetActive(true);
            profileSurveyStatusSubtitle.color = completed
                ? new Color(0.62f, 0.88f, 0.9f, 0.95f)
                : new Color(0.75f, 0.95f, 0.95f, 0.92f);
        }
    }

    static string BuildEmptyOrderHintText()
    {
        return ParticipantConditionOrder.FormatConditionLegendRichText()
               + "\n<size=22><color=#9EBFC2>Tu orden de bloques se mostrará aquí cuando ingreses tu código de participante.</color></size>";
    }

    public void RefreshPracticeButtonState()
    {
        if (practiceButton == null)
            practiceButton = transform.Find(PracticeButtonName)?.GetComponent<Button>();

        if (practiceButton == null || experimentLogic == null)
            return;

        if (practiceTitle == null)
            practiceTitle = GetTitleText(practiceButton);

        if (practiceTitle != null && !string.IsNullOrWhiteSpace(practiceTitle.text) &&
            practiceTitle.text.IndexOf("INICIAR", System.StringComparison.OrdinalIgnoreCase) >= 0)
            practiceOriginalTitle = practiceTitle.text;

        practiceButton.interactable = experimentLogic.CanStartPracticeBlock();

        if (practiceTitle != null)
            practiceTitle.text = practiceOriginalTitle;

        if (practiceStatusSubtitle != null)
            practiceStatusSubtitle.gameObject.SetActive(false);
    }

    void RefreshScenarioButtonStates()
    {
        if (experimentLogic == null)
            return;

        CacheScenarioButtons();

        int nextAllowed = experimentLogic.GetNextAllowedScenarioIndex();

        foreach (var pair in scenarioButtonUi)
        {
            var ui = pair.Value;
            if (ui?.Button == null || ui.Title == null)
                continue;

            bool completed = experimentLogic.IsConditionCompleted(pair.Key);
            bool allowedNow = !completed && pair.Key == nextAllowed;
            ui.Button.interactable = allowedNow;
            ui.Title.text = ui.OriginalTitle;

            ui.StatusSubtitle ??= EnsureStatusSubtitle(ui.Button, ui.Title);
            if (ui.StatusSubtitle != null)
            {
                ui.StatusSubtitle.text = CompletedStatusText;
                ui.StatusSubtitle.gameObject.SetActive(completed);
            }
        }
    }

    void CacheScenarioButtons()
    {
        if (scenarioButtonsCached && scenarioButtonUi.Count >= DefaultScenarioTitles.Length)
            return;

        scenarioButtonUi.Clear();
        int fallbackIndex = 0;

        foreach (Transform child in transform)
        {
            if (child.name == PracticeButtonName || child.name == ProfileSurveyButtonName)
                continue;

            var button = child.GetComponent<Button>();
            if (button == null)
                continue;

            var title = GetTitleText(button);
            if (title == null)
                continue;

            int scenarioIndex = ResolveScenarioIndex(title.text) ?? fallbackIndex;
            fallbackIndex++;

            if (scenarioIndex < 0 || scenarioIndex >= DefaultScenarioTitles.Length)
                continue;

            string originalTitle = title.text;
            if (string.IsNullOrWhiteSpace(originalTitle) ||
                originalTitle.Equals(CompletedStatusText, System.StringComparison.OrdinalIgnoreCase) ||
                ResolveScenarioIndex(originalTitle) == null)
                originalTitle = DefaultScenarioTitles[scenarioIndex];

            var ui = new ScenarioButtonUi
            {
                Button = button,
                Title = title,
                OriginalTitle = originalTitle,
                StatusSubtitle = child.Find(StatusSubtitleName)?.GetComponent<TextMeshProUGUI>()
            };

            title.text = originalTitle;
            EnsureConditionLetterBadge(button, scenarioIndex, title);
            scenarioButtonUi[scenarioIndex] = ui;
        }

        scenarioButtonsCached = scenarioButtonUi.Count >= DefaultScenarioTitles.Length;
    }

    static TextMeshProUGUI GetTitleText(Button button)
    {
        if (button == null)
            return null;

        foreach (var tmp in button.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject.name == StatusSubtitleName)
                continue;

            return tmp;
        }

        return null;
    }

    static TextMeshProUGUI EnsureStatusSubtitle(Button button, TextMeshProUGUI title)
    {
        if (button == null)
            return null;

        var existing = button.transform.Find(StatusSubtitleName)?.GetComponent<TextMeshProUGUI>();
        if (existing != null)
            return existing;

        if (title != null)
        {
            bool leaveRoomForBadge = button.transform.Find(ConditionLetterBadgeName) != null;
            ConfigureTitleLayout(title.rectTransform, leaveRoomForBadge);
        }

        var go = new GameObject(StatusSubtitleName, typeof(RectTransform));
        go.transform.SetParent(button.transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0.38f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        var subtitle = go.AddComponent<TextMeshProUGUI>();
        subtitle.text = CompletedStatusText;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.verticalAlignment = VerticalAlignmentOptions.Middle;
        subtitle.raycastTarget = false;
        subtitle.textWrappingMode = TextWrappingModes.NoWrap;

        if (title != null)
        {
            subtitle.font = title.font;
            subtitle.fontSharedMaterial = title.fontSharedMaterial;
            subtitle.fontSize = Mathf.Clamp(title.fontSize * 0.52f, 16f, 24f);
            subtitle.color = new Color(0f, 0.92f, 0.92f, 0.9f);
        }

        go.SetActive(false);
        return subtitle;
    }

    static void EnsureConditionLetterBadge(Button button, int scenarioIndex, TextMeshProUGUI title)
    {
        if (button == null)
            return;

        var existing = button.transform.Find(ConditionLetterBadgeName);
        if (existing != null)
        {
            var existingLabel = existing.GetComponentInChildren<TextMeshProUGUI>(true);
            if (existingLabel != null)
                existingLabel.text = ParticipantConditionOrder.ConditionLetter(scenarioIndex);
            return;
        }

        if (title != null)
            ConfigureTitleLayout(title.rectTransform, true);

        var go = new GameObject(ConditionLetterBadgeName, typeof(RectTransform));
        go.transform.SetParent(button.transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(28f, 0f);
        rect.sizeDelta = new Vector2(56f, 56f);

        var bg = go.AddComponent<Image>();
        bg.color = LetterBadgeFill;
        bg.raycastTarget = false;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = LetterBadgeBorder;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        var labelGo = new GameObject("Letter", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = ParticipantConditionOrder.ConditionLetter(scenarioIndex);
        label.alignment = TextAlignmentOptions.Center;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = LetterBadgeText;

        if (title != null)
        {
            label.font = title.font;
            label.fontSharedMaterial = title.fontSharedMaterial;
            label.fontSize = Mathf.Clamp(title.fontSize * 0.72f, 28f, 36f);
        }
        else
        {
            label.fontSize = 32f;
        }
    }

    static void ConfigureTitleLayout(RectTransform titleRect, bool leaveRoomForBadge = false)
    {
        if (titleRect == null)
            return;

        float leftInset = leaveRoomForBadge ? 0.11f : 0f;
        titleRect.anchorMin = new Vector2(leftInset, 0.38f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = Vector2.zero;

        var title = titleRect.GetComponent<TextMeshProUGUI>();
        if (title != null)
        {
            title.alignment = TextAlignmentOptions.Center;
            title.verticalAlignment = VerticalAlignmentOptions.Middle;
        }
    }

    static int? ResolveScenarioIndex(string labelText)
    {
        if (string.IsNullOrWhiteSpace(labelText))
            return null;

        string upper = labelText.ToUpperInvariant();
        foreach (var (keyword, scenarioIndex) in ScenarioLabelKeywords)
        {
            if (upper.Contains(keyword))
                return scenarioIndex;
        }

        return null;
    }

    void EnsureOrderHint()
    {
        if (orderHintText == null)
        {
            var existing = transform.Find(OrderHintName)?.GetComponent<TextMeshProUGUI>();
            if (existing != null)
                orderHintText = existing;
        }

        if (orderHintText != null)
            return;

        var referenceText = GetReferenceTmp();
        var go = new GameObject(OrderHintName, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        orderHintText = go.AddComponent<TextMeshProUGUI>();
        orderHintText.alignment = TextAlignmentOptions.Center;
        orderHintText.verticalAlignment = VerticalAlignmentOptions.Top;
        orderHintText.textWrappingMode = TextWrappingModes.Normal;
        orderHintText.raycastTarget = false;
        orderHintText.richText = true;

        if (referenceText != null)
        {
            orderHintText.font = referenceText.font;
            orderHintText.fontSharedMaterial = referenceText.fontSharedMaterial;
            orderHintText.fontSize = Mathf.Clamp(referenceText.fontSize * 0.74f, 22f, 28f);
        }
        else
        {
            orderHintText.fontSize = 26f;
        }

        orderHintText.color = new Color(0.82f, 1f, 1f, 1f);
    }

    TextMeshProUGUI GetReferenceTmp()
    {
        if (practiceTitle != null)
            return practiceTitle;

        foreach (Transform child in transform)
        {
            var tmp = child.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && tmp.gameObject.name != StatusSubtitleName)
                return tmp;
        }

        return null;
    }

    void EnsurePracticeButton()
    {
        if (transform.Find(PracticeButtonName) != null)
        {
            practiceButton = transform.Find(PracticeButtonName).GetComponent<Button>();
            practiceTitle = GetTitleText(practiceButton);
            WirePracticeButton();
            return;
        }

        Button template = profileSurveyButton;
        if (template == null)
        {
            foreach (Transform child in transform)
            {
                template = child.GetComponent<Button>();
                if (template != null && child.name != ProfileSurveyButtonName)
                    break;
                template = null;
            }
        }

        if (template == null)
            return;

        var clone = Instantiate(template.gameObject, transform);
        clone.name = PracticeButtonName;

        var subtitle = clone.transform.Find(StatusSubtitleName);
        if (subtitle != null)
            Destroy(subtitle.gameObject);

        var profileSubtitle = clone.transform.Find(ProfileSurveySubtitleName);
        if (profileSubtitle != null)
            Destroy(profileSubtitle.gameObject);

        practiceTitle = GetTitleText(clone.GetComponent<Button>());
        if (practiceTitle != null)
        {
            practiceTitle.text = practiceOriginalTitle;
            practiceTitle.fontSize = Mathf.Min(practiceTitle.fontSize, 34f);
        }

        practiceButton = clone.GetComponent<Button>();
        ResetButtonListeners(practiceButton);
        WirePracticeButton();
    }

    void EnsureProfileSurveyButton()
    {
        if (ensureProfileSurveyInProgress)
            return;

        ensureProfileSurveyInProgress = true;
        try
        {
            EnsureProfileSurveyButtonCore();
        }
        finally
        {
            ensureProfileSurveyInProgress = false;
        }
    }

    void EnsureProfileSurveyButtonCore()
    {
        if (experimentLogic == null)
            experimentLogic = FindFirstObjectByType<ExperimentLogic>();

        if (experimentLogic == null || !experimentLogic.HasParticipantProfileSurvey)
        {
            var existing = transform.Find(ProfileSurveyButtonName);
            if (existing != null)
                existing.gameObject.SetActive(false);

            profileSurveyButton = null;
            return;
        }

        Transform existingButton = transform.Find(ProfileSurveyButtonName);
        if (existingButton != null)
        {
            profileSurveyButton = existingButton.GetComponent<Button>();
            if (profileSurveyButton != null)
            {
                profileSurveyButton.gameObject.SetActive(true);
                WireProfileSurveyButton();
                StyleProfileSurveyButton();
            }

            return;
        }

        Button template = null;
        foreach (Transform child in transform)
        {
            if (child.name == OrderHintName)
                continue;

            template = child.GetComponent<Button>();
            if (template != null)
                break;
        }

        if (template == null)
            return;

        var clone = Instantiate(template.gameObject, transform);
        clone.name = ProfileSurveyButtonName;

        var statusSubtitle = clone.transform.Find(StatusSubtitleName);
        if (statusSubtitle != null)
            Destroy(statusSubtitle.gameObject);

        var badge = clone.transform.Find(ConditionLetterBadgeName);
        if (badge != null)
            Destroy(badge.gameObject);

        var title = GetTitleText(clone.GetComponent<Button>());
        if (title != null)
        {
            title.text = "PERFIL DEL PARTICIPANTE";
            title.fontSize = Mathf.Min(title.fontSize, 30f);
        }

        var subtitleGo = new GameObject(ProfileSurveySubtitleName, typeof(RectTransform));
        subtitleGo.transform.SetParent(clone.transform, false);
        var subtitleRect = subtitleGo.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0f, 0f);
        subtitleRect.anchorMax = new Vector2(1f, 0.38f);
        subtitleRect.offsetMin = Vector2.zero;
        subtitleRect.offsetMax = Vector2.zero;
        var subtitle = subtitleGo.AddComponent<TextMeshProUGUI>();
        subtitle.text = ProfileSurveyPendingSubtitle;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.verticalAlignment = VerticalAlignmentOptions.Middle;
        subtitle.raycastTarget = false;
        if (title != null)
        {
            subtitle.font = title.font;
            subtitle.fontSharedMaterial = title.fontSharedMaterial;
            subtitle.fontSize = Mathf.Clamp(title.fontSize * 0.48f, 14f, 20f);
            subtitle.color = new Color(0.75f, 0.95f, 0.95f, 0.92f);
            ConfigureTitleLayout(title.rectTransform, false);
        }

        profileSurveyButton = clone.GetComponent<Button>();
        ResetButtonListeners(profileSurveyButton);
        WireProfileSurveyButton();
        StyleProfileSurveyButton();
    }

    void StyleProfileSurveyButton()
    {
        if (profileSurveyButton == null)
            return;

        if (profileSurveyButton.TryGetComponent(out Image image))
            image.color = new Color(0.12f, 0.28f, 0.3f, 0.98f);
    }

    static void ResetButtonListeners(Button button)
    {
        if (button == null)
            return;

        // Cloned scenario buttons copy Inspector onClick (hide Scenario_Selection + start block).
        button.onClick = new Button.ButtonClickedEvent();
    }

    void WireProfileSurveyButton()
    {
        if (profileSurveyButton == null)
            return;

        ResetButtonListeners(profileSurveyButton);
        profileSurveyButton.onClick.AddListener(OnProfileSurveyClicked);
    }

    void OnProfileSurveyClicked()
    {
        if (experimentLogic == null)
            experimentLogic = FindFirstObjectByType<ExperimentLogic>();

        if (experimentLogic == null)
            return;

        if (experimentLogic.IsProfileSurveyAwaitingVerification && !experimentLogic.IsProfileSurveyCompleted)
        {
            if (!experimentLogic.HasFormResponseVerification)
                experimentLogic.ConfirmProfileSurveySubmissionManually();
            return;
        }

        if (!experimentLogic.CanOpenProfileSurvey())
            return;

        experimentLogic.OpenProfileSurveyInBrowser();
    }

    void WirePracticeButton()
    {
        if (practiceButton == null)
            return;

        ResetButtonListeners(practiceButton);
        practiceButton.onClick.AddListener(OnPracticeClicked);
    }

    void OnPracticeClicked()
    {
        if (experimentLogic == null)
            experimentLogic = FindFirstObjectByType<ExperimentLogic>();

        experimentLogic?.OnPracticeSelected();
    }

    void LayoutScenarioButtons()
    {
        CacheScenarioButtons();

        var orderedButtons = new List<Transform>();

        Transform profile = transform.Find(ProfileSurveyButtonName);
        if (profile != null && profile.gameObject.activeInHierarchy && profile.GetComponent<Button>() != null)
            orderedButtons.Add(profile);

        Transform practice = transform.Find(PracticeButtonName);
        if (practice != null && practice.gameObject.activeInHierarchy && practice.GetComponent<Button>() != null)
            orderedButtons.Add(practice);

        for (int scenarioIndex = 0; scenarioIndex < DefaultScenarioTitles.Length; scenarioIndex++)
        {
            if (!scenarioButtonUi.TryGetValue(scenarioIndex, out ScenarioButtonUi ui) || ui?.Button == null)
                continue;

            orderedButtons.Add(ui.Button.transform);
        }

        Transform hint = transform.Find(OrderHintName);
        int siblingIndex = 0;
        if (hint != null)
            hint.SetSiblingIndex(siblingIndex++);

        float firstCenterY = FirstButtonCenterY();
        float step = ButtonLayoutStep();
        float buttonCenterX = SelectionButtonCenterX;

        for (int i = 0; i < orderedButtons.Count; i++)
        {
            Transform child = orderedButtons[i];
            child.SetSiblingIndex(siblingIndex++);

            var rect = child as RectTransform;
            if (rect == null)
                continue;

            IgnoreParentLayout(rect);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(buttonCenterX, firstCenterY - (i * step));
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            rect.localScale = Vector3.one;
        }
    }
}
