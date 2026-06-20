using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover suave para las tarjetas de escenario en el formulario de consentimiento.
/// </summary>
[RequireComponent(typeof(Image))]
public class ScenarioCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    static readonly Color DefaultNormalBg = new(0.11f, 0.16f, 0.18f, 0.88f);
    static readonly Color DefaultHoverBg = new(0.13f, 0.3f, 0.34f, 0.98f);
    static readonly Color DefaultNormalOutline = new(0.12f, 0.55f, 0.58f, 0.45f);
    static readonly Color DefaultHoverOutline = new(0.2f, 0.92f, 0.94f, 0.95f);
    static readonly Color DefaultTitleNormal = new(0.12f, 0.82f, 0.82f, 1f);
    static readonly Color DefaultTitleHover = new(0.55f, 1f, 1f, 1f);
    static readonly Color DefaultBodyNormal = new(0.68f, 0.78f, 0.82f, 1f);
    static readonly Color DefaultBodyHover = new(0.9f, 0.97f, 0.99f, 1f);

    [SerializeField] float hoverScale = 1.045f;
    [SerializeField] float blendSpeed = 10f;
    [SerializeField] Vector2 normalOutlineDistance = new(1.2f, -1.2f);
    [SerializeField] Vector2 hoverOutlineDistance = new(2.4f, -2.4f);

    Image background;
    Outline outline;
    TextMeshProUGUI titleLabel;
    TextMeshProUGUI bodyLabel;

    Color normalBg = DefaultNormalBg;
    Color hoverBg = DefaultHoverBg;
    Color normalOutline = DefaultNormalOutline;
    Color hoverOutline = DefaultHoverOutline;
    Color titleNormal = DefaultTitleNormal;
    Color titleHover = DefaultTitleHover;
    Color bodyNormal = DefaultBodyNormal;
    Color bodyHover = DefaultBodyHover;

    Vector3 baseScale;
    float blend;

    void Awake()
    {
        background = GetComponent<Image>();
        outline = GetComponent<Outline>();
        baseScale = transform.localScale;
        CacheLabels();
        ApplyBlend(0f);
    }

    void CacheLabels()
    {
        titleLabel = null;
        bodyLabel = null;

        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.text.StartsWith("ESCENARIO"))
                titleLabel = tmp;
            else if (bodyLabel == null)
                bodyLabel = tmp;
        }
    }

    public void Configure(
        Color? normalBackground = null,
        Color? hoveredBackground = null,
        Color? normalBorder = null,
        Color? hoveredBorder = null)
    {
        if (normalBackground.HasValue) normalBg = normalBackground.Value;
        if (hoveredBackground.HasValue) hoverBg = hoveredBackground.Value;
        if (normalBorder.HasValue) normalOutline = normalBorder.Value;
        if (hoveredBorder.HasValue) hoverOutline = hoveredBorder.Value;
        ApplyBlend(blend);
    }

    public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);

    public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

    void OnDisable() => SetHovered(false);

    void SetHovered(bool value) => _hovered = value;

    bool _hovered;

    void Update()
    {
        float target = _hovered ? 1f : 0f;
        blend = Mathf.MoveTowards(blend, target, Time.unscaledDeltaTime * blendSpeed);

        if (!_hovered && blend <= 0.001f && target <= 0.001f)
            return;

        ApplyBlend(blend);
    }

    void ApplyBlend(float t)
    {
        if (background != null)
            background.color = Color.Lerp(normalBg, hoverBg, t);

        if (outline != null)
        {
            outline.effectColor = Color.Lerp(normalOutline, hoverOutline, t);
            outline.effectDistance = Vector2.Lerp(normalOutlineDistance, hoverOutlineDistance, t);
        }

        if (titleLabel != null)
            titleLabel.color = Color.Lerp(titleNormal, titleHover, t);

        if (bodyLabel != null)
            bodyLabel.color = Color.Lerp(bodyNormal, bodyHover, t);

        transform.localScale = Vector3.Lerp(baseScale, baseScale * hoverScale, t);
    }
}
