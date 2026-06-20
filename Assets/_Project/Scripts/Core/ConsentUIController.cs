using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Layout y estilo visual del formulario de consentimiento informado.
/// </summary>
[DefaultExecutionOrder(100)]
public class ConsentUIController : MonoBehaviour
{
    static readonly Color Accent = new(0.12f, 0.82f, 0.82f, 1f);
    static readonly Color AccentSoft = new(0.55f, 0.9f, 0.92f, 1f);
    static readonly Color PanelBg = new(0.05f, 0.08f, 0.1f, 0.98f);
    static readonly Color Surface = new(0.09f, 0.13f, 0.15f, 0.92f);
    static readonly Color SurfaceBorder = new(0.12f, 0.55f, 0.58f, 0.45f);
    static readonly Color TextPrimary = new(0.95f, 0.98f, 0.99f, 1f);
    static readonly Color TextSecondary = new(0.68f, 0.78f, 0.82f, 1f);
    static readonly Color ToggleOffFill = new(0.08f, 0.14f, 0.16f, 1f);
    static readonly Color ToggleOnFill = new(0.1f, 0.72f, 0.72f, 1f);
    static readonly Color InputFocusedFill = new(0.1f, 0.2f, 0.22f, 1f);
    static readonly Color InputFocusedBorder = new(0.12f, 0.82f, 0.82f, 0.95f);
    static readonly Color ButtonEnabledBg = new(0.1f, 0.74f, 0.74f, 1f);
    static readonly Color ButtonEnabledText = new(0.03f, 0.08f, 0.1f, 1f);
    static readonly Color ButtonDisabledBg = new(0.12f, 0.16f, 0.18f, 0.55f);
    static readonly Color ButtonDisabledText = new(0.45f, 0.52f, 0.55f, 0.9f);
    static readonly Color HintError = new(0.95f, 0.32f, 0.32f, 1f);
    static readonly Color HintSuccess = new(0.35f, 0.88f, 0.55f, 1f);
    static readonly Color HintHidden = new(0.95f, 0.32f, 0.32f, 0f);
    static readonly Color FieldErrorBorder = new(0.95f, 0.32f, 0.32f, 0.95f);
    static readonly Color FieldErrorFill = new(0.18f, 0.1f, 0.1f, 1f);

    const float FormTopPadding = 14f;
    const float FormBottomPadding = 28f;
    const float FormVerticalPadding = FormTopPadding + FormBottomPadding;
    const float FormSectionSpacing = 10f;
    const float OverlayVerticalMargin = 64f;

    const float StepIndicatorHeight = 32f;
    const float ParticipantCodeRowHeight = 76f;
    const float ToggleRowHeight = 42f;
    const float RequirementHintReservedHeight = 66f;
    const float ContinueButtonHeight = 52f;
    const float ContinueActionBlockSpacing = 6f;
    const float ContinueActionBlockInnerBottom = 4f;
    const float ContinueActionBlockHeight = RequirementHintReservedHeight + ContinueActionBlockSpacing + ContinueButtonHeight + ContinueActionBlockInnerBottom;
    const float FooterSurfacePadding = 14f;
    const float FooterSurfaceExtraHeight = FooterSurfacePadding * 2f;

    bool formLayoutFinalized;
    bool validationHintsVisible;

    [Header("Referencias (opcional)")]
    public ScrollRect consentScrollRect;
    public TextMeshProUGUI consentBodyText;
    public Button continueButton;
    public Toggle ageToggle;
    public Toggle consentToggle;
    public Image ageToggleBackground;
    public Image consentToggleBackground;
    public TextMeshProUGUI continueLabel;
    public TextMeshProUGUI stepIndicator;

    TMP_InputField participantCodeInput;
    TextMeshProUGUI participantCodeHint;
    TextMeshProUGUI continueRequirementHint;
    Image participantCodeInputBackground;
    Outline participantCodeInputOutline;
    bool participantCodeFocusHooksAttached;

    ExperimentLogic experimentLogic;
    Transform scenarioTitle;
    Transform scenarioDesc;
    Transform scenarioRow;

    void Awake()
    {
        experimentLogic = FindFirstObjectByType<ExperimentLogic>();
        AutoFindReferences();
    }

    void Start()
    {
        validationHintsVisible = false;
        CleanupVisualV1Artifacts();
        CleanupLegacyButtons();
        EnsureStepIndicator();
        AdjustFormWidth();
        CenterFormInOverlay();
        ConsolidateIntoMainPanel();
        FixFormLayout();
        ApplyModernVisualDesign();
        ApplyScrollFixes();
        ResizeToggleBoxes();
        HookToggleListeners();
        RefreshToggleVisuals();
        ApplyCopyFixes();
        ApplyScenarioCopyFixes();
        RefreshContinueVisual();
        EnsureContinueDisabledClickHandler();
        FinalizeFormLayoutOnce();
        StartCoroutine(RefreshContinueVisualAfterLayout());
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    void FinalizeFormLayoutOnce()
    {
        formLayoutFinalized = false;
        FitFormDimensions();
    }

    System.Collections.IEnumerator RefreshContinueVisualAfterLayout()
    {
        yield return null;
        RefreshContinueVisual();
    }

    void AutoFindReferences()
    {
        if (consentScrollRect == null)
            consentScrollRect = GetComponentInChildren<ScrollRect>(true);

        if (consentBodyText == null && consentScrollRect != null)
            consentBodyText = consentScrollRect.GetComponentInChildren<TextMeshProUGUI>(true);

        if (experimentLogic != null)
        {
            if (continueButton == null) continueButton = experimentLogic.continueButton;
            if (ageToggle == null) ageToggle = experimentLogic.ageConsentToggle;
            if (consentToggle == null) consentToggle = experimentLogic.consentToggle;
            if (ageToggleBackground == null) ageToggleBackground = experimentLogic.ageConsentBackground;
            if (consentToggleBackground == null) consentToggleBackground = experimentLogic.consentBackground;
        }

        if (continueLabel == null && continueButton != null)
            continueLabel = continueButton.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    void CleanupLegacyButtons()
    {
        var staleRow = transform.Find("ConsentActionsRow");
        if (staleRow != null)
        {
            if (continueButton != null && continueButton.transform.IsChildOf(staleRow))
                continueButton.transform.SetParent(transform, false);
            Destroy(staleRow.gameObject);
        }

        foreach (var name in new[] { "NoparticiparButton", "No participarButton" })
        {
            var decline = transform.Find(name);
            if (decline != null)
                Destroy(decline.gameObject);
        }
    }

    void ConsolidateIntoMainPanel()
    {
        if (consentScrollRect == null || consentScrollRect.content == null)
            return;

        var content = consentScrollRect.content;

        scenarioTitle = transform.Find("ESCENARIO TITLE");
        scenarioDesc = transform.Find("ESCENARIO Desc");
        scenarioRow = transform.Find("ScenarioRow");

        if (consentBodyText != null)
            consentBodyText.transform.SetParent(content, false);
        if (scenarioTitle != null)
            scenarioTitle.SetParent(content, false);
        if (scenarioDesc != null)
            scenarioDesc.SetParent(content, false);
        if (scenarioRow != null)
            scenarioRow.SetParent(content, false);

        int order = 0;
        if (consentBodyText != null)
            consentBodyText.transform.SetSiblingIndex(order++);
        if (scenarioTitle != null)
            scenarioTitle.SetSiblingIndex(order++);
        if (scenarioDesc != null)
            scenarioDesc.SetSiblingIndex(order++);
        if (scenarioRow != null)
            scenarioRow.SetSiblingIndex(order++);

        if (scenarioTitle != null)
            scenarioTitle.gameObject.SetActive(false);
        if (scenarioDesc != null)
            scenarioDesc.gameObject.SetActive(false);

        var contentLayout = content.GetComponent<VerticalLayoutGroup>();
        if (contentLayout != null)
        {
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.spacing = 24;
            contentLayout.padding.left = 20;
            contentLayout.padding.right = 18;
            contentLayout.padding.top = 28;
            contentLayout.padding.bottom = 28;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
        }

        if (scenarioRow != null)
        {
            SetPreferredHeight(scenarioRow, 188f);
            var rowLayout = scenarioRow.GetComponent<HorizontalLayoutGroup>();
            if (rowLayout != null)
            {
                rowLayout.spacing = 16;
                rowLayout.padding.left = 0;
                rowLayout.padding.right = 0;
                rowLayout.padding.top = 4;
                rowLayout.padding.bottom = 0;
                rowLayout.childForceExpandWidth = true;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
            }
        }

        ConfigureScrollContentLayout();
    }

    void ConfigureScrollContentLayout()
    {
        if (consentScrollRect == null || consentScrollRect.content == null)
            return;

        var content = consentScrollRect.content as RectTransform;
        if (content == null)
            return;

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        var contentFitter = content.GetComponent<ContentSizeFitter>() ??
                            content.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var contentLayout = content.GetComponent<VerticalLayoutGroup>();
        if (contentLayout != null)
        {
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.spacing = 16;
            contentLayout.padding.left = 18;
            contentLayout.padding.right = 14;
            contentLayout.padding.top = 12;
            contentLayout.padding.bottom = 12;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
        }

        ConfigureTextAutoHeight(consentBodyText);
        ConfigureTextAutoHeight(scenarioTitle != null ? scenarioTitle.GetComponent<TextMeshProUGUI>() : null);
        ConfigureTextAutoHeight(scenarioDesc != null ? scenarioDesc.GetComponent<TextMeshProUGUI>() : null);

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        consentScrollRect.verticalNormalizedPosition = 1f;
    }

    static void ConfigureTextAutoHeight(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.textWrappingMode = TextWrappingModes.Normal;

        var fitter = text.GetComponent<ContentSizeFitter>() ??
                     text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = text.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredHeight = -1f;
            layout.minHeight = -1f;
            layout.flexibleHeight = 0f;
        }
    }

    void AdjustFormWidth()
    {
        var rect = GetComponent<RectTransform>();
        if (rect == null || rect.parent is not RectTransform parent)
            return;

        const float sideMargin = 72f;
        const float widthScale = 0.78f;

        float maxWidth = parent.rect.width - sideMargin * 2f;
        float targetWidth = Mathf.Clamp(maxWidth * widthScale, 880f, maxWidth);
        rect.sizeDelta = new Vector2(targetWidth, rect.sizeDelta.y);
    }

    void CenterFormInOverlay()
    {
        var formRect = GetComponent<RectTransform>();
        if (formRect == null)
            return;

        formRect.anchorMin = new Vector2(0.5f, 0.5f);
        formRect.anchorMax = new Vector2(0.5f, 0.5f);
        formRect.pivot = new Vector2(0.5f, 0.5f);
        formRect.anchoredPosition = Vector2.zero;
    }

    void FixFormLayout()
    {
        CleanupFormRootChildren();

        var vlg = GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = FormSectionSpacing;
            vlg.padding.left = 16;
            vlg.padding.right = 16;
            vlg.padding.top = (int)FormTopPadding;
            vlg.padding.bottom = (int)FormBottomPadding;
        }

        foreach (var childName in new[] { "DataTagsRow", "Spacer", "Title", "ScenarioSectionSpacer" })
        {
            var child = transform.Find(childName);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        SetPreferredHeight(stepIndicator != null ? stepIndicator.transform : transform.Find("StepIndicator"), StepIndicatorHeight);

        BuildConsentFooter();
        NormalizeFormChildOrder();
    }

    void CleanupVisualV1Artifacts()
    {
        var track = transform.Find("ConsentProgressTrack");
        if (track != null)
            Destroy(track.gameObject);
    }

    void CleanupFormRootChildren()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (IsAllowedFormChild(child))
                continue;

            child.gameObject.SetActive(false);
        }
    }

    static bool IsFooterChildName(string name) =>
        name == "ParticipantCodeRow" || name == "ContinueActionBlock";

    bool IsAllowedFormChild(Transform child)
    {
        if (stepIndicator != null && child == stepIndicator.transform)
            return true;
        if (child.name == "ConsentFormFooter")
            return true;
        if (consentScrollRect != null && child == consentScrollRect.transform)
            return true;
        if (IsFooterChildName(child.name))
            return true;
        if (ageToggle != null && child == ageToggle.transform)
            return true;
        if (consentToggle != null && child == consentToggle.transform)
            return true;
        if (continueButton != null && child == continueButton.transform)
            return true;
        return false;
    }

    void FitFormDimensions()
    {
        if (formLayoutFinalized)
            return;

        ApplyStableFooterHeights();
        EnsureFooterLayoutElementsNoFlex();
        ConfigureScrollContentLayout();

        var formRect = GetComponent<RectTransform>();
        if (formRect == null)
            return;

        float maxFormHeight = 980f;
        if (formRect.parent is RectTransform overlayRect)
            maxFormHeight = overlayRect.rect.height - OverlayVerticalMargin;

        float fixedSections = MeasureStableFooterHeight();
        float scrollHeight = Mathf.Max(maxFormHeight - fixedSections, 320f);

        if (consentScrollRect != null)
        {
            var scrollLayout = consentScrollRect.GetComponent<LayoutElement>() ??
                               consentScrollRect.gameObject.AddComponent<LayoutElement>();
            scrollLayout.minHeight = scrollHeight;
            scrollLayout.preferredHeight = scrollHeight;
            scrollLayout.flexibleHeight = 0f;
        }

        formRect.sizeDelta = new Vector2(formRect.sizeDelta.x, maxFormHeight);

        CenterFormInOverlay();
        LayoutRebuilder.ForceRebuildLayoutImmediate(formRect);

        if (consentScrollRect != null)
            consentScrollRect.verticalNormalizedPosition = 1f;

        formLayoutFinalized = true;
    }

    void ApplyStableFooterHeights()
    {
        SetFixedLayoutHeight(stepIndicator != null ? stepIndicator.transform : transform.Find("StepIndicator"), StepIndicatorHeight);

        var footer = FindConsentFormFooter();
        SetFixedLayoutHeight(footer, MeasureFooterSurfaceContentHeight());

        var codeRow = footer != null ? footer.Find("ParticipantCodeRow") : transform.Find("ParticipantCodeRow");
        var actionBlock = FindContinueActionBlock();

        SetFixedLayoutHeight(codeRow, ParticipantCodeRowHeight);
        SetFixedLayoutHeight(ageToggle != null ? ageToggle.transform : null, ToggleRowHeight);
        SetFixedLayoutHeight(consentToggle != null ? consentToggle.transform : null, ToggleRowHeight);
        SetFixedLayoutHeight(actionBlock, ContinueActionBlockHeight);

        ApplyRequirementHintSlot();
        ConfigureParticipantCodeRow(codeRow);
    }

    float MeasureFooterSurfaceContentHeight()
    {
        return FooterSurfaceExtraHeight
               + ParticipantCodeRowHeight + FormSectionSpacing
               + ToggleRowHeight + FormSectionSpacing
               + ToggleRowHeight + FormSectionSpacing
               + ContinueActionBlockHeight;
    }

    float MeasureStableFooterHeight()
    {
        var formLayout = GetComponent<VerticalLayoutGroup>();
        float padding = formLayout != null ? formLayout.padding.top + formLayout.padding.bottom : FormVerticalPadding;
        float spacing = formLayout != null ? formLayout.spacing : FormSectionSpacing;

        return padding
               + StepIndicatorHeight + spacing
               + spacing
               + MeasureFooterSurfaceContentHeight();
    }

    void ApplyRequirementHintSlot()
    {
        ResolveContinueRequirementHint();
        if (continueRequirementHint == null)
            return;

        continueRequirementHint.gameObject.SetActive(true);
        SetFixedLayoutHeight(continueRequirementHint.transform, RequirementHintReservedHeight);
    }

    static void SetFixedLayoutHeight(Transform target, float height)
    {
        if (target == null)
            return;

        var layout = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
    }

    void EnsureFooterLayoutElementsNoFlex()
    {
        SetLayoutFlexibleHeight(stepIndicator != null ? stepIndicator.transform : transform.Find("StepIndicator"), 0f);

        var footer = FindConsentFormFooter();
        SetLayoutFlexibleHeight(footer, 0f);

        if (footer != null)
        {
            SetLayoutFlexibleHeight(footer.Find("ParticipantCodeRow"), 0f);
            SetLayoutFlexibleHeight(FindContinueActionBlock(), 0f);
        }
        else
        {
            SetLayoutFlexibleHeight(transform.Find("ParticipantCodeRow"), 0f);
            SetLayoutFlexibleHeight(transform.Find("ContinueActionBlock"), 0f);
        }

        SetLayoutFlexibleHeight(ageToggle != null ? ageToggle.transform : null, 0f);
        SetLayoutFlexibleHeight(consentToggle != null ? consentToggle.transform : null, 0f);
    }

    static void SetLayoutFlexibleHeight(Transform target, float flexibleHeight)
    {
        if (target == null)
            return;

        var layout = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
        layout.flexibleHeight = flexibleHeight;
    }

    void SyncContinueActionBlockHeight()
    {
        ApplyStableFooterHeights();
    }

    Transform FindConsentFormFooter() => transform.Find("ConsentFormFooter");

    Transform FindContinueActionBlock()
    {
        var footer = FindConsentFormFooter();
        if (footer != null)
        {
            var block = footer.Find("ContinueActionBlock");
            if (block != null)
                return block;
        }

        return transform.Find("ContinueActionBlock");
    }

    Transform GetFormActionParent()
    {
        var footer = FindConsentFormFooter();
        return footer != null ? footer : transform;
    }

    void CleanupOrphanedFooterElements()
    {
        var footer = FindConsentFormFooter();
        if (footer == null)
            return;

        foreach (Transform child in transform)
        {
            if (child == footer || child == stepIndicator?.transform || child == consentScrollRect?.transform)
                continue;

            if (child.name == "ContinueRequirementHint"
                || child.name == "ContinueActionBlock"
                || child.name == "ParticipantCodeRow")
            {
                Destroy(child.gameObject);
                continue;
            }

            if (ageToggle != null && child == ageToggle.transform)
                child.SetParent(footer, false);
            else if (consentToggle != null && child == consentToggle.transform)
                child.SetParent(footer, false);
        }
    }

    void BuildConsentFooter()
    {
        ApplyToggleRowSpacing(ageToggle);
        ApplyToggleRowSpacing(consentToggle);

        var spacer = transform.Find("ContinueTopSpacer");
        if (spacer != null)
            Destroy(spacer.gameObject);

        UnwrapLegacyConsentPanel();

        if (ageToggle == null || consentToggle == null)
            return;

        EnsureConsentFormFooterShell();
        CleanupOrphanedFooterElements();

        var footer = GetFormActionParent();
        EnsureParticipantCodeField(footer);
        ageToggle.transform.SetParent(footer, false);
        consentToggle.transform.SetParent(footer, false);
        EnsureContinueActionBlock(footer);

        ApplyStableFooterHeights();
        NormalizeFormChildOrder();
    }

    void EnsureConsentFormFooterShell()
    {
        if (FindConsentFormFooter() != null)
            return;

        var footerGo = new GameObject(
            "ConsentFormFooter",
                typeof(RectTransform),
                typeof(Image),
                typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        var footer = footerGo.transform;
        footer.SetParent(transform, false);

        var footerLayout = footer.GetComponent<VerticalLayoutGroup>();
        footerLayout.spacing = FormSectionSpacing;
        footerLayout.padding = new RectOffset(
            (int)FooterSurfacePadding,
            (int)FooterSurfacePadding,
            (int)FooterSurfacePadding,
            (int)FooterSurfacePadding);
        footerLayout.childAlignment = TextAnchor.UpperLeft;
        footerLayout.childControlWidth = true;
        footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = true;
        footerLayout.childForceExpandHeight = false;
    }

    void UnwrapLegacyConsentPanel()
    {
        var legacyPanel = transform.Find("ConsentChecksPanel");
        if (legacyPanel == null)
            return;

        var children = new List<Transform>();
        foreach (Transform child in legacyPanel)
            children.Add(child);

        foreach (Transform child in children)
            child.SetParent(transform, false);

        Destroy(legacyPanel.gameObject);
    }

    void EnsureContinueActionBlock(Transform panel)
    {
        if (continueButton == null)
            return;

        RemoveStrayContinueHints();

        var block = FindContinueActionBlock();
        if (block == null)
        {
            var go = new GameObject(
                "ContinueActionBlock",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(LayoutElement));
            block = go.transform;
            block.SetParent(panel, false);

            var blockLayout = go.GetComponent<VerticalLayoutGroup>();
            blockLayout.spacing = ContinueActionBlockSpacing;
            blockLayout.padding = new RectOffset(0, 0, 0, 4);
            blockLayout.childAlignment = TextAnchor.UpperLeft;
            blockLayout.childControlWidth = true;
            blockLayout.childControlHeight = true;
            blockLayout.childForceExpandWidth = true;
            blockLayout.childForceExpandHeight = false;

            var blockElement = go.GetComponent<LayoutElement>();
            blockElement.minHeight = ContinueActionBlockHeight;
            blockElement.preferredHeight = ContinueActionBlockHeight;
            blockElement.flexibleHeight = 0f;
        }
        else
        {
            SetFixedLayoutHeight(block, ContinueActionBlockHeight);
            var blockLayout = block.GetComponent<VerticalLayoutGroup>();
            if (blockLayout != null)
                blockLayout.padding = new RectOffset(0, 0, 0, (int)ContinueActionBlockInnerBottom);
        }

        block.SetAsLastSibling();

        EnsureContinueRequirementHint(block);
        if (continueRequirementHint != null)
            continueRequirementHint.transform.SetAsFirstSibling();

        continueButton.transform.SetParent(block, false);
            continueButton.transform.SetAsLastSibling();

            var buttonLayout = continueButton.GetComponent<LayoutElement>() ??
                               continueButton.gameObject.AddComponent<LayoutElement>();
        buttonLayout.preferredHeight = ContinueButtonHeight;
        buttonLayout.minHeight = ContinueButtonHeight;
            buttonLayout.flexibleWidth = 1f;
            buttonLayout.flexibleHeight = 0f;

            var buttonRect = continueButton.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(0f, ContinueButtonHeight);
    }

    void RemoveStrayContinueHints()
    {
        var actionBlock = FindContinueActionBlock();

        foreach (Transform child in transform)
        {
            if (child.name != "ContinueRequirementHint")
                continue;

            if (actionBlock == null || !child.IsChildOf(actionBlock))
                Destroy(child.gameObject);
        }

        if (actionBlock == null)
            return;

        bool keptHint = false;
        foreach (Transform child in actionBlock)
        {
            if (child.name != "ContinueRequirementHint")
                continue;

            if (!keptHint)
            {
                keptHint = true;
                continueRequirementHint = child.GetComponent<TextMeshProUGUI>();
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    Transform GetContinueHintParent()
    {
        var block = FindContinueActionBlock();
        return block != null ? block : transform;
    }

    void ResolveContinueRequirementHint()
    {
        var parent = GetContinueHintParent();
        continueRequirementHint = parent.Find("ContinueRequirementHint")?.GetComponent<TextMeshProUGUI>();

        if (continueRequirementHint == null && continueButton != null)
            EnsureContinueRequirementHint(parent);
    }

    void EnsureContinueRequirementHint(Transform parent)
    {
        RemoveStrayContinueHints();

        var existing = parent.Find("ContinueRequirementHint");
        if (existing != null)
        {
            continueRequirementHint = existing.GetComponent<TextMeshProUGUI>();
            ApplyHintFont(continueRequirementHint);
            ApplyRequirementHintSlot();
            if (continueButton != null && continueButton.transform.parent == parent)
                continueRequirementHint.transform.SetAsFirstSibling();
            return;
        }

        var hintGo = new GameObject(
            "ContinueRequirementHint",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        hintGo.transform.SetParent(parent, false);
        hintGo.transform.SetAsFirstSibling();

        continueRequirementHint = hintGo.GetComponent<TextMeshProUGUI>();
        continueRequirementHint.fontSize = 16f;
        continueRequirementHint.color = HintError;
        continueRequirementHint.alignment = TextAlignmentOptions.Left;
        continueRequirementHint.textWrappingMode = TextWrappingModes.Normal;
        continueRequirementHint.richText = false;
        continueRequirementHint.raycastTarget = false;
        continueRequirementHint.text = "";
        continueRequirementHint.color = HintHidden;

        ApplyRequirementHintSlot();
        ApplyHintFont(continueRequirementHint);
    }

    void ApplyHintFont(TextMeshProUGUI hint)
    {
        if (hint == null)
            return;

        if (consentBodyText != null && consentBodyText.font != null)
            hint.font = consentBodyText.font;
        else if (hint.font == null && TMP_Settings.defaultFontAsset != null)
            hint.font = TMP_Settings.defaultFontAsset;
    }

    void EnsureParticipantCodeField(Transform panel)
    {
        var row = panel.Find("ParticipantCodeRow");
        if (row == null)
        {
            var rowGo = new GameObject(
                "ParticipantCodeRow",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(LayoutElement));
            row = rowGo.transform;
            row.SetParent(panel, false);
            row.SetAsFirstSibling();

            var rowLayout = row.GetComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 6;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            var rowElement = row.GetComponent<LayoutElement>();
            rowElement.preferredHeight = ParticipantCodeRowHeight;
            rowElement.minHeight = ParticipantCodeRowHeight;
            rowElement.flexibleHeight = 0f;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelGo.transform.SetParent(row, false);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "Código de participante (ej.: P01, P20):";
            label.fontSize = 18f;
            label.color = TextPrimary;
            label.alignment = TextAlignmentOptions.Left;
            var labelLayout = labelGo.GetComponent<LayoutElement>();
            labelLayout.preferredHeight = 26f;
            labelLayout.minHeight = 26f;

            if (consentBodyText != null)
                label.font = consentBodyText.font;

            participantCodeInput = CreateParticipantCodeInputManual(row);
        }
        else
        {
            participantCodeInput = row.GetComponentInChildren<TMP_InputField>(true);
            participantCodeHint = row.Find("Hint")?.GetComponent<TextMeshProUGUI>();
            if (participantCodeHint != null)
                participantCodeHint.gameObject.SetActive(false);
        }

        ConfigureParticipantCodeRow(row);

        if (experimentLogic != null && participantCodeInput != null)
            experimentLogic.BindParticipantCodeInput(participantCodeInput);

        ApplyParticipantCodeInputStyle(participantCodeInput);
    }

    TMP_InputField CreateParticipantCodeInputManual(Transform row)
    {
        var inputGo = new GameObject(
            "Input",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        inputGo.SetActive(false);
        inputGo.transform.SetParent(row, false);
        StyleSurface(inputGo, ToggleOffFill, SurfaceBorder, 0);
        ConfigureParticipantCodeInputRect(inputGo);

        var inputBackground = inputGo.GetComponent<Image>();
        if (inputBackground != null)
            inputBackground.raycastTarget = true;

        var textAreaGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textAreaGo.transform.SetParent(inputGo.transform, false);
        var textAreaRect = textAreaGo.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(14f, 8f);
        textAreaRect.offsetMax = new Vector2(-14f, -8f);

        var textAreaMask = textAreaGo.GetComponent<RectMask2D>();
        textAreaMask.padding = new Vector4(-8f, 0f, -8f, 0f);

        var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderGo.transform.SetParent(textAreaGo.transform, false);
        var placeholder = placeholderGo.GetComponent<TextMeshProUGUI>();
        placeholder.text = "Ej.: P01";
        placeholder.fontSize = 20f;
        placeholder.color = new Color(TextSecondary.r, TextSecondary.g, TextSecondary.b, 0.75f);
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.richText = false;
        placeholder.raycastTarget = false;
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        StretchRectToParent(placeholderGo.GetComponent<RectTransform>());

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(textAreaGo.transform, false);
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.fontSize = 20f;
        text.color = TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.richText = false;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.parseCtrlCharacters = true;
        StretchRectToParent(textGo.GetComponent<RectTransform>());

        // TMP crea el mesh del caret en OnEnable solo si textComponent ya está asignado.
        var inputField = inputGo.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRect;
        inputField.placeholder = placeholder;
        inputField.textComponent = text;
        if (inputBackground != null)
            inputField.targetGraphic = inputBackground;

        ConfigureParticipantCodeInputBehavior(inputField);
        ApplyParticipantCodeInputVisuals(inputField);
        inputField.text = string.Empty;

        inputGo.SetActive(true);
        return inputField;
    }

    static void ConfigureParticipantCodeInputBehavior(TMP_InputField inputField)
    {
        if (inputField == null)
            return;

        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = 10;
        inputField.contentType = TMP_InputField.ContentType.Standard;
        inputField.shouldActivateOnSelect = true;
        inputField.onFocusSelectAll = false;
        inputField.resetOnDeActivation = true;
        inputField.readOnly = false;
        inputField.interactable = true;
        inputField.caretBlinkRate = 0.85f;
        inputField.caretWidth = 2;
        inputField.customCaretColor = true;
        inputField.caretColor = TextPrimary;
    }

    static void EnsureParticipantCodeInputCaretRenderer(TMP_InputField inputField)
    {
        if (inputField == null || !inputField.gameObject.activeInHierarchy)
            return;

        // Si textComponent se asignó después del primer OnEnable, el caret nunca se creó.
        inputField.enabled = false;
        inputField.enabled = true;
    }

    static void ConfigureParticipantCodeInputRect(GameObject inputGo)
    {
        var inputRect = inputGo.GetComponent<RectTransform>();
        inputRect.localScale = Vector3.one;
        inputRect.anchorMin = new Vector2(0f, 0.5f);
        inputRect.anchorMax = new Vector2(1f, 0.5f);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = Vector2.zero;
        inputRect.sizeDelta = new Vector2(0f, 44f);

        var inputLayout = inputGo.GetComponent<LayoutElement>() ?? inputGo.AddComponent<LayoutElement>();
        inputLayout.preferredHeight = 44f;
        inputLayout.minHeight = 44f;
        inputLayout.flexibleHeight = 0f;
        inputLayout.flexibleWidth = 1f;
    }

    static void StretchRectToParent(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void ApplyParticipantCodeInputVisuals(TMP_InputField inputField)
    {
        if (inputField == null)
            return;

        var bg = inputField.GetComponent<Image>();
        if (bg != null)
        {
            bg.raycastTarget = true;
            inputField.targetGraphic = bg;
        }

        if (inputField.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "Ej.: P01";
            placeholder.fontSize = 20f;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(TextSecondary.r, TextSecondary.g, TextSecondary.b, 0.75f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.richText = false;
            placeholder.raycastTarget = false;
        }

        if (inputField.textComponent != null)
        {
            inputField.textComponent.fontSize = 20f;
            inputField.textComponent.color = TextPrimary;
            inputField.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            inputField.textComponent.richText = false;
            inputField.textComponent.raycastTarget = false;
            inputField.textComponent.parseCtrlCharacters = true;
            TrySetEnableExtraPadding(inputField.textComponent, true);
        }

        TMP_FontAsset font = consentBodyText != null ? consentBodyText.font : TMP_Settings.defaultFontAsset;
        if (font == null)
            return;

        if (inputField.textComponent != null)
            inputField.textComponent.font = font;
        if (inputField.placeholder is TMP_Text placeholderText)
            placeholderText.font = font;
    }

    void ConfigureParticipantCodeRow(Transform row)
    {
        if (row == null)
            return;

        SetFixedLayoutHeight(row, ParticipantCodeRowHeight);
    }

    void SetParticipantCodeInlineHint(string message)
    {
        // La validación se muestra en ContinueRequirementHint para no mover el layout.
    }

    void ApplyParticipantCodeInputStyle(TMP_InputField inputField)
    {
        if (inputField == null)
            return;

        participantCodeInputBackground = inputField.GetComponent<Image>();
        participantCodeInputOutline = inputField.GetComponent<Outline>();
        if (participantCodeInputOutline == null)
            participantCodeInputOutline = inputField.gameObject.AddComponent<Outline>();

        if (participantCodeInputBackground != null)
        {
            inputField.targetGraphic = participantCodeInputBackground;
            participantCodeInputBackground.raycastTarget = true;
        }

        ApplyParticipantCodeInputVisuals(inputField);
        ConfigureParticipantCodeInputBehavior(inputField);
        EnsureParticipantCodeInputCaretRenderer(inputField);

        SetParticipantCodeInputFocused(inputField.isFocused);

        if (participantCodeFocusHooksAttached)
            return;

        participantCodeFocusHooksAttached = true;
        inputField.onSelect.AddListener(_ => OnParticipantCodeSelected());
        inputField.onDeselect.AddListener(_ => OnParticipantCodeDeselected());
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

    void OnParticipantCodeSelected()
    {
        MarkConsentFormTouched();
        SetParticipantCodeInputFocused(true);
        RefreshContinueVisual();
    }

    void OnParticipantCodeDeselected()
    {
        SetParticipantCodeInputFocused(false);
        RefreshToggleVisuals();
    }

    static string GetParticipantCodeRawText(TMP_InputField inputField)
    {
        if (inputField == null)
            return string.Empty;

        return inputField.text?.Replace("\u200B", string.Empty).Trim() ?? string.Empty;
    }

    void SetParticipantCodeInputFocused(bool focused)
    {
        if (participantCodeInputBackground != null)
            participantCodeInputBackground.color = focused ? InputFocusedFill : ToggleOffFill;

        if (participantCodeInputOutline != null)
        {
            participantCodeInputOutline.effectColor = focused ? InputFocusedBorder : SurfaceBorder;
            participantCodeInputOutline.effectDistance = focused ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            participantCodeInputOutline.useGraphicAlpha = true;
        }
    }

    void NormalizeFormChildOrder()
    {
        int order = 0;

        if (stepIndicator != null)
            stepIndicator.transform.SetSiblingIndex(order++);
        if (consentScrollRect != null)
            consentScrollRect.transform.SetSiblingIndex(order++);

        var footer = FindConsentFormFooter();
        if (footer != null)
            footer.SetSiblingIndex(order);
    }

    void ApplyModernVisualDesign()
    {
        var panelImage = GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = PanelBg;

        EnsureOutline(gameObject, SurfaceBorder, new Vector2(1.5f, -1.5f));

        if (stepIndicator != null)
        {
            stepIndicator.fontSize = 17f;
            stepIndicator.color = AccentSoft;
            stepIndicator.fontStyle = FontStyles.Normal;
            stepIndicator.characterSpacing = 0.6f;
            stepIndicator.alignment = TextAlignmentOptions.Center;
        }

        StyleSectionHeader(scenarioTitle);
        StyleSectionSubtitle(scenarioDesc);

        if (consentScrollRect != null)
            StyleSurface(consentScrollRect.gameObject, Surface, SurfaceBorder, 0);

        var footer = FindConsentFormFooter();
        if (footer != null)
            StyleSurface(footer.gameObject, Surface, SurfaceBorder, 0);

        if (consentBodyText != null)
        {
            consentBodyText.fontSize = 18.5f;
            consentBodyText.color = TextSecondary;
            consentBodyText.lineSpacing = 8f;
            consentBodyText.paragraphSpacing = 14f;
            consentBodyText.margin = new Vector4(0f, 0f, 0f, 8f);
        }

        foreach (var card in GetComponentsInChildren<Transform>(true))
        {
            if (!card.name.StartsWith("Card"))
                continue;
            StyleScenarioCard(card.gameObject);
        }

        StyleToggleLabels();
        StyleContinueButton();
    }

    void StyleSectionHeader(Transform target)
    {
        if (target == null)
            return;

        var tmp = target.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            return;

        tmp.fontSize = 22f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = TextPrimary;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.margin = new Vector4(0f, 18f, 0f, 0f);
    }

    void StyleSectionSubtitle(Transform target)
    {
        if (target == null)
            return;

        var tmp = target.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            return;

        tmp.fontSize = 17f;
        tmp.color = TextSecondary;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.lineSpacing = 6f;
        tmp.margin = new Vector4(0f, 0f, 0f, 8f);
    }

    void StyleScenarioCard(GameObject card)
    {
        var image = card.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.11f, 0.16f, 0.18f, 0.88f);

        EnsureOutline(card, SurfaceBorder, new Vector2(1.2f, -1.2f));

        var layout = card.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding.left = 18;
            layout.padding.right = 18;
            layout.padding.top = 16;
            layout.padding.bottom = 16;
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = false;
        }

        var element = card.GetComponent<LayoutElement>() ?? card.AddComponent<LayoutElement>();
        element.minHeight = 148f;
        element.preferredHeight = 168f;
        element.flexibleWidth = 1f;
        element.minWidth = 150f;

        foreach (var tmp in card.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp == null)
                continue;

            string tmpText = tmp.text ?? string.Empty;
            if (tmpText.StartsWith("ESCENARIO"))
            {
                tmp.fontSize = 16f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Accent;
                tmp.alignment = TextAlignmentOptions.Left;
            }
            else
            {
                tmp.fontSize = 15.5f;
                tmp.color = TextSecondary;
                tmp.lineSpacing = 5f;
                tmp.alignment = TextAlignmentOptions.TopLeft;
            }
        }

        var hover = card.GetComponent<ScenarioCardHover>() ?? card.AddComponent<ScenarioCardHover>();
        hover.Configure(
            new Color(0.11f, 0.16f, 0.18f, 0.88f),
            new Color(0.13f, 0.3f, 0.34f, 0.98f),
            SurfaceBorder,
            new Color(Accent.r, Accent.g, Accent.b, 0.95f));
    }

    void StyleToggleLabels()
    {
        foreach (var toggle in new[] { ageToggle, consentToggle })
        {
            if (toggle == null)
                continue;

            var label = toggle.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.fontSize = 20;
                label.color = TextPrimary;
                label.supportRichText = false;
            }
        }
    }

    void StyleContinueButton()
    {
        if (continueButton == null)
            return;

        continueButton.transition = Selectable.Transition.ColorTint;
        var colors = continueButton.colors;
        colors.fadeDuration = 0.12f;
        colors.highlightedColor = new Color(0.85f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.65f, 0.9f, 0.9f, 1f);
        continueButton.colors = colors;

        if (continueLabel != null)
        {
            continueLabel.text = "Continuar";
            continueLabel.fontSize = 22f;
            continueLabel.fontStyle = FontStyles.Bold;
            continueLabel.alignment = TextAlignmentOptions.Center;
        }

        EnsureOutline(continueButton.gameObject, new Color(Accent.r, Accent.g, Accent.b, 0.55f), new Vector2(1.5f, -1.5f));
    }

    void EnsureContinueDisabledClickHandler()
    {
        if (continueButton == null)
            return;

        var handler = continueButton.GetComponent<ConsentContinueDisabledClick>();
        if (handler == null)
            handler = continueButton.gameObject.AddComponent<ConsentContinueDisabledClick>();

        handler.Bind(this, continueButton);
    }

    static void StyleSurface(GameObject target, Color fill, Color border, int paddingHint)
    {
        var image = target.GetComponent<Image>();
        if (image != null)
            image.color = fill;

        EnsureOutline(target, border, new Vector2(1f, -1f));

        var layout = target.GetComponent<VerticalLayoutGroup>();
        if (layout != null && paddingHint > 0)
        {
            layout.padding.left = paddingHint;
            layout.padding.right = paddingHint;
        }
    }

    static void EnsureOutline(GameObject target, Color color, Vector2 distance)
    {
        var outline = target.GetComponent<Outline>();
        if (outline == null)
            outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    static void SetPreferredHeight(Transform target, float height)
    {
        if (target == null)
            return;

        var layout = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
    }

    static void ApplyToggleRowSpacing(Toggle toggle)
    {
        if (toggle == null)
            return;

        var layout = toggle.GetComponent<LayoutElement>() ?? toggle.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = ToggleRowHeight;
        layout.minHeight = ToggleRowHeight;
        layout.flexibleHeight = 0f;

        var rect = toggle.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, ToggleRowHeight);
    }

    void ApplyScrollFixes()
    {
        if (consentScrollRect == null)
            return;

        consentScrollRect.horizontal = false;
        consentScrollRect.vertical = true;
        consentScrollRect.scrollSensitivity = 28f;
        consentScrollRect.movementType = ScrollRect.MovementType.Clamped;
        consentScrollRect.verticalNormalizedPosition = 1f;

        ConfigureScrollContentLayout();

        if (consentScrollRect.viewport != null)
        {
            var viewport = consentScrollRect.viewport;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-18f, 0f);
        }

        if (consentScrollRect.verticalScrollbar != null)
        {
            var scrollbarRect = consentScrollRect.verticalScrollbar.GetComponent<RectTransform>();
            if (scrollbarRect != null)
                scrollbarRect.sizeDelta = new Vector2(13.94f, scrollbarRect.sizeDelta.y);

            var track = consentScrollRect.verticalScrollbar.GetComponent<Image>();
            if (track != null)
                track.color = new Color(0.04f, 0.08f, 0.1f, 0.7f);

            if (consentScrollRect.verticalScrollbar.handleRect != null)
            {
                var handle = consentScrollRect.verticalScrollbar.handleRect.GetComponent<Image>();
                if (handle != null)
                    handle.color = new Color(0.15f, 0.62f, 0.64f, 0.9f);
            }
        }
    }

    void ApplyCopyFixes()
    {
        if (consentBodyText == null)
            return;

        string text = consentBodyText.text ?? string.Empty;
        text = text.TrimStart('\n', '\r', ' ');
        text = text.Replace("NeyFred Jimenez Campos", "Ney Fred Jiménez Campos");
        text = text.Replace(
            "Si tiene alguna pregunta puede comunicarse con:\r\nNey Fred Jiménez Campos\r\nney.jimenez@ucr.ac.cr",
            "Si tiene alguna pregunta, comuníquese con:\r\nNey Fred Jiménez Campos\r\nCorreo: ney.jimenez@ucr.ac.cr");

        if (!text.Contains("Duración estimada"))
        {
            const string marker = "utilizados exclusivamente con fines académicos.\r\n";
            text = text.Replace(marker, marker + "Duración estimada: 15–20 minutos.\r\n");
        }

        consentBodyText.text = text;
    }

    void ApplyScenarioCopyFixes()
    {
        foreach (var label in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label == null || label == consentBodyText)
                continue;

            string labelText = label.text ?? string.Empty;

            if (labelText.StartsWith("Sin asistencia."))
                label.text = "Sin asistencia. Usted responderá de forma independiente.";
            else if (labelText.StartsWith("Con asistencia IA."))
                label.text = "Con asistencia IA. Un tutor virtual le guiará en el razonamiento.";
            else if (labelText.Contains("consiste el experimento"))
            {
                label.text = "¿En qué consiste el experimento?";
                label.fontStyle = FontStyles.Bold;
            }
            else if (labelText == "CONTINUAR")
                label.text = "Continuar";
        }
    }

    void ResizeToggleBoxes()
    {
        ResizeToggleBox(ageToggleBackground);
        ResizeToggleBox(consentToggleBackground);
    }

    static void ResizeToggleBox(Image background)
    {
        if (background == null)
            return;

        var rect = background.rectTransform;
        rect.sizeDelta = new Vector2(26f, 26f);
        rect.anchoredPosition = new Vector2(13f, -13f);
        EnsureOutline(background.gameObject, AccentSoft, new Vector2(1.2f, -1.2f));
    }

    void EnsureStepIndicator()
    {
        if (stepIndicator == null)
        {
            var title = transform.Find("Title");
            if (title == null)
                return;

            var go = new GameObject("StepIndicator", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(transform, false);
            go.transform.SetSiblingIndex(title.GetSiblingIndex() + 1);
            stepIndicator = go.GetComponent<TextMeshProUGUI>();
            stepIndicator.font = title.GetComponent<TextMeshProUGUI>()?.font;
        }

        stepIndicator.text = "Paso 1 de 2 · Consentimiento informado";
        var layout = stepIndicator.GetComponent<LayoutElement>() ?? stepIndicator.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = StepIndicatorHeight;
        layout.flexibleHeight = 0f;
    }

    void OnEnable()
    {
        RefreshContinueVisual();
    }

    void HookToggleListeners()
    {
        if (ageToggle != null)
            ageToggle.onValueChanged.AddListener(_ => OnToggleChanged());
        if (consentToggle != null)
            consentToggle.onValueChanged.AddListener(_ => OnToggleChanged());
        if (participantCodeInput != null)
            participantCodeInput.onValueChanged.AddListener(_ => OnToggleChanged());
    }

    void OnToggleChanged()
    {
        MarkConsentFormTouched();
        RefreshContinueVisual();
    }

    void MarkConsentFormTouched()
    {
        if (validationHintsVisible)
            return;

        validationHintsVisible = true;
    }

    public void RevealValidationHints()
    {
        validationHintsVisible = true;
        RefreshContinueVisual();
    }

    bool IsConsentFormComplete()
    {
        if (experimentLogic != null)
            return experimentLogic.IsConsentComplete();

        return ageToggle != null && consentToggle != null &&
               ageToggle.isOn && consentToggle.isOn &&
               IsParticipantCodeValid();
    }

    bool IsParticipantCodeValid()
    {
        if (participantCodeInput == null)
            return false;

        return ExperimentLogic.TryNormalizeParticipantCode(GetParticipantCodeRawText(participantCodeInput), out _);
    }

    bool IsParticipantCodeMissing()
    {
        return participantCodeInput == null || string.IsNullOrWhiteSpace(GetParticipantCodeRawText(participantCodeInput));
    }

    void RefreshToggleVisuals()
    {
        bool showFieldErrors = validationHintsVisible && !IsConsentFormComplete();
        bool codeMissingOrInvalid = IsParticipantCodeMissing() || !IsParticipantCodeValid();

        ApplyToggleVisual(ageToggle, ageToggleBackground, showFieldErrors && (ageToggle == null || !ageToggle.isOn));
        ApplyToggleVisual(consentToggle, consentToggleBackground, showFieldErrors && (consentToggle == null || !consentToggle.isOn));
        ApplyParticipantCodeValidationHighlight(showFieldErrors && codeMissingOrInvalid);
    }

    static void ApplyToggleVisual(Toggle toggle, Image background, bool showError)
    {
        if (toggle == null || background == null)
            return;

        background.color = toggle.isOn ? ToggleOnFill : ToggleOffFill;

        var outline = background.GetComponent<Outline>();
        if (outline == null)
            return;

        if (showError && !toggle.isOn)
        {
            outline.effectColor = FieldErrorBorder;
            outline.effectDistance = new Vector2(2f, -2f);
            return;
        }

        outline.effectColor = AccentSoft;
        outline.effectDistance = new Vector2(1.2f, -1.2f);
    }

    void ApplyParticipantCodeValidationHighlight(bool showError)
    {
        if (participantCodeInput != null && participantCodeInput.isFocused)
        {
            SetParticipantCodeInputFocused(true);
            return;
        }

        if (showError)
        {
            if (participantCodeInputBackground != null)
                participantCodeInputBackground.color = FieldErrorFill;

            if (participantCodeInputOutline != null)
            {
                participantCodeInputOutline.effectColor = FieldErrorBorder;
                participantCodeInputOutline.effectDistance = new Vector2(2f, -2f);
            }

            return;
        }

        SetParticipantCodeInputFocused(false);
    }

    void RefreshContinueVisual()
    {
        if (continueButton == null)
            return;

        bool enabled = IsConsentFormComplete();

        UpdateParticipantCodeHint();
        UpdateContinueRequirementHint(enabled);
        RefreshToggleVisuals();

        continueButton.interactable = enabled;

        if (continueLabel != null)
            continueLabel.color = enabled ? ButtonEnabledText : ButtonDisabledText;

        var buttonImage = continueButton.targetGraphic as Image;
        if (buttonImage != null)
            buttonImage.color = enabled ? ButtonEnabledBg : ButtonDisabledBg;
    }

    void UpdateParticipantCodeHint()
    {
        if (participantCodeInput == null)
            return;

        string raw = GetParticipantCodeRawText(participantCodeInput);
        if (string.IsNullOrWhiteSpace(raw))
        {
            SetParticipantCodeInlineHint("");
            return;
        }

        SetParticipantCodeInlineHint(
            ExperimentLogic.TryNormalizeParticipantCode(raw, out _)
                ? ""
                : "Use solo la letra P seguida de un número (ej.: P01, P20).");
    }

    void UpdateContinueRequirementHint(bool consentComplete)
    {
        ResolveContinueRequirementHint();
        if (continueRequirementHint == null)
            return;

        ApplyRequirementHintSlot();

        if (consentComplete)
        {
            continueRequirementHint.text = "Listo para continuar.";
            continueRequirementHint.color = HintSuccess;
            ApplyHintFont(continueRequirementHint);
            return;
        }

        if (!validationHintsVisible)
        {
            continueRequirementHint.text = "";
            continueRequirementHint.color = HintHidden;
            return;
        }

        var missing = new List<string>(3);
        string raw = participantCodeInput != null ? GetParticipantCodeRawText(participantCodeInput) : "";

        if (string.IsNullOrWhiteSpace(raw))
            missing.Add("Ingrese su código de participante (ej.: P01, P20).");
        else if (!ExperimentLogic.TryNormalizeParticipantCode(raw, out _))
            missing.Add("Use solo la letra P seguida de un número (ej.: P01, P20).");

        if (ageToggle == null || !ageToggle.isOn)
            missing.Add("Marque que tiene 18 años o más.");

        if (consentToggle == null || !consentToggle.isOn)
            missing.Add("Marque que acepta participar en el estudio.");

        if (missing.Count == 0)
        {
            continueRequirementHint.text = "";
            continueRequirementHint.color = HintHidden;
            return;
        }

        continueRequirementHint.text = string.Join("\n", missing);
        continueRequirementHint.color = HintError;
        ApplyHintFont(continueRequirementHint);
    }

    public void ShowConsentError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        validationHintsVisible = true;
        ResolveContinueRequirementHint();
        if (continueRequirementHint == null)
            return;

        ApplyRequirementHintSlot();
        continueRequirementHint.text = message;
        continueRequirementHint.color = HintError;
        RefreshToggleVisuals();
    }
}
