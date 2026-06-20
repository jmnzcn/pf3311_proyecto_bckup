using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chat input character counter and send-button waiting feedback (spinner + label).
/// </summary>
[DisallowMultipleComponent]
public class ChatInputUiController : MonoBehaviour
{
    public const int CharacterLimit = 1000;
    public const int CharacterWarningThreshold = 800;
    public const int CharacterCriticalThreshold = 950;

    const string CharCounterName = "ChatCharCounter";
    const string SpinnerName = "ChatSendSpinner";
    const string WaitingButtonLabel = "Esperando…";
    const float SpinnerRotateSpeed = 420f;
    const float SpinnerSize = 22f;
    const float CounterEdgeGap = 8f;
    const float CounterTopInset = 8f;
    const float CounterFallbackScrollbarWidth = 20f;

    static readonly Color CounterNormal = new(0.62f, 0.74f, 0.76f, 0.88f);
    static readonly Color CounterWarning = new(0.95f, 0.78f, 0.35f, 0.95f);
    static readonly Color CounterCritical = new(0.95f, 0.45f, 0.4f, 0.95f);
    static readonly Color SpinnerColor = new(0.18f, 0.9f, 0.9f, 1f);

    TMP_InputField inputField;
    Button sendButton;
    TextMeshProUGUI sendButtonLabel;
    TextMeshProUGUI charCounter;
    RectTransform spinnerRect;
    TextMeshProUGUI spinnerLabel;
    string sendButtonDefaultLabel = "Enviar consulta";
    bool spinnerActive;
    Action onSubmit;

    public void Bind(TMP_InputField input, Button send, Action submit)
    {
        UnwireSubmitHandlers();

        inputField = input;
        sendButton = send;
        onSubmit = submit;

        if (sendButton != null)
        {
            sendButtonLabel = sendButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (sendButtonLabel != null && !string.IsNullOrWhiteSpace(sendButtonLabel.text))
                sendButtonDefaultLabel = sendButtonLabel.text;

            EnsureSpinner();
        }

        if (inputField != null)
        {
            inputField.characterLimit = CharacterLimit;
            EnsureCharCounter();
            WireSubmitHandlers();
        }

        if (charCounter != null)
            RefreshCharacterCounter(inputField != null ? inputField.text : "");
        SetGeminiWaiting(false);
    }

    void WireSubmitHandlers()
    {
        if (inputField == null)
            return;

        inputField.onSubmit.AddListener(OnInputSubmit);
        inputField.onValidateInput += OnValidateInput;
    }

    void UnwireSubmitHandlers()
    {
        if (inputField == null)
            return;

        inputField.onSubmit.RemoveListener(OnInputSubmit);
        inputField.onValidateInput -= OnValidateInput;
    }

    void OnInputSubmit(string _)
    {
        TrySubmit();
    }

    char OnValidateInput(string text, int charIndex, char addedChar)
    {
        if (addedChar != '\n' && addedChar != '\r')
            return addedChar;

        if (IsShiftHeldForNewline())
            return addedChar;

        TrySubmit();
        return '\0';
    }

    static bool IsShiftHeldForNewline() =>
        Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    void TrySubmit()
    {
        if (spinnerActive || onSubmit == null)
            return;

        onSubmit.Invoke();
    }

    void OnDestroy()
    {
        UnwireSubmitHandlers();
    }

    public void RefreshCharacterCounter(string text)
    {
        if (charCounter == null)
            return;

        int length = string.IsNullOrEmpty(text) ? 0 : text.Length;
        string counterText = charCounter.text ?? string.Empty;
        int previousLength = counterText.Length > 0 && counterText.Contains('/')
            ? ParseCounterLength(counterText)
            : -1;

        bool crossedThreshold =
            (previousLength < CharacterWarningThreshold && length >= CharacterWarningThreshold) ||
            (previousLength < CharacterCriticalThreshold && length >= CharacterCriticalThreshold) ||
            (previousLength >= CharacterWarningThreshold && length < CharacterWarningThreshold) ||
            (previousLength >= CharacterCriticalThreshold && length < CharacterCriticalThreshold);

        if (!crossedThreshold && previousLength >= 0 && length / 5 == previousLength / 5)
            return;

        charCounter.text = $"{length}/{CharacterLimit}";

        if (length >= CharacterCriticalThreshold)
            charCounter.color = CounterCritical;
        else if (length >= CharacterWarningThreshold)
            charCounter.color = CounterWarning;
        else
            charCounter.color = CounterNormal;
    }

    static int ParseCounterLength(string counterText)
    {
        int slash = counterText.IndexOf('/');
        if (slash <= 0)
            return 0;

        return int.TryParse(counterText.Substring(0, slash), out int value) ? value : 0;
    }

    public void SetGeminiWaiting(bool waiting)
    {
        spinnerActive = waiting;

        if (spinnerRect != null)
            spinnerRect.gameObject.SetActive(waiting);

        if (sendButtonLabel != null)
        {
            sendButtonLabel.text = waiting ? WaitingButtonLabel : sendButtonDefaultLabel;
            sendButtonLabel.color = waiting
                ? new Color(0.35f, 0.95f, 0.95f, 1f)
                : new Color(0f, 1f, 1f, 1f);
        }
    }

    void Update()
    {
        if (!spinnerActive || spinnerRect == null)
            return;

        spinnerRect.Rotate(0f, 0f, -SpinnerRotateSpeed * Time.unscaledDeltaTime);
    }

    void EnsureCharCounter()
    {
        if (inputField == null)
            return;

        var existing = inputField.transform.Find(CharCounterName)?.GetComponent<TextMeshProUGUI>();
        if (existing != null)
        {
            charCounter = existing;
            if (string.IsNullOrEmpty(charCounter.text))
                charCounter.text = $"0/{CharacterLimit}";
            ApplyCharCounterLayout();
            return;
        }

        var go = new GameObject(CharCounterName, typeof(RectTransform));
        go.transform.SetParent(inputField.transform, false);

        charCounter = go.AddComponent<TextMeshProUGUI>();
        charCounter.raycastTarget = false;
        charCounter.textWrappingMode = TextWrappingModes.NoWrap;
        charCounter.text = $"0/{CharacterLimit}";

        if (inputField.textComponent != null)
        {
            charCounter.font = inputField.textComponent.font;
            charCounter.fontSharedMaterial = inputField.textComponent.fontSharedMaterial;
        }

        ApplyCharCounterLayout();
    }

    void ApplyCharCounterLayout()
    {
        if (charCounter == null)
            return;

        float scrollbarWidth = GetVerticalScrollbarWidth();
        float rightInset = scrollbarWidth + CounterEdgeGap;

        var rect = charCounter.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-rightInset, -CounterTopInset);
        rect.sizeDelta = new Vector2(112f, 22f);

        charCounter.alignment = TextAlignmentOptions.TopRight;
        charCounter.verticalAlignment = VerticalAlignmentOptions.Top;
        charCounter.fontSize = 16f;

        charCounter.transform.SetAsLastSibling();
        ReserveViewportForCounter(scrollbarWidth);
    }

    float GetVerticalScrollbarWidth()
    {
        if (inputField?.verticalScrollbar == null)
            return CounterFallbackScrollbarWidth;

        var scrollbarRect = inputField.verticalScrollbar.transform as RectTransform;
        if (scrollbarRect == null)
            return CounterFallbackScrollbarWidth;

        float width = scrollbarRect.sizeDelta.x;
        if (width <= 0f)
            width = scrollbarRect.rect.width;

        return width > 0f ? width : CounterFallbackScrollbarWidth;
    }

    void ReserveViewportForCounter(float scrollbarWidth)
    {
        if (inputField?.textViewport == null)
            return;

        var viewport = inputField.textViewport;
        var max = viewport.offsetMax;
        max.x = -(scrollbarWidth + 2f);
        max.y = -28f;
        viewport.offsetMax = max;
    }

    void EnsureSpinner()
    {
        if (sendButton == null)
            return;

        var existing = sendButton.transform.Find(SpinnerName);
        if (existing != null)
        {
            spinnerLabel = existing.GetComponent<TextMeshProUGUI>();
            if (spinnerLabel != null)
            {
                spinnerRect = existing as RectTransform;
                return;
            }

            Destroy(existing.gameObject);
        }

        var go = new GameObject(SpinnerName, typeof(RectTransform));
        go.transform.SetParent(sendButton.transform, false);

        spinnerRect = go.GetComponent<RectTransform>();
        spinnerRect.anchorMin = new Vector2(0f, 0.5f);
        spinnerRect.anchorMax = new Vector2(0f, 0.5f);
        spinnerRect.pivot = new Vector2(0.5f, 0.5f);
        spinnerRect.anchoredPosition = new Vector2(28f, 0f);
        spinnerRect.sizeDelta = new Vector2(SpinnerSize, SpinnerSize);

        spinnerLabel = go.AddComponent<TextMeshProUGUI>();
        spinnerLabel.text = "|";
        spinnerLabel.fontSize = 22f;
        spinnerLabel.color = SpinnerColor;
        spinnerLabel.alignment = TextAlignmentOptions.Center;
        spinnerLabel.verticalAlignment = VerticalAlignmentOptions.Middle;
        spinnerLabel.raycastTarget = false;
        spinnerLabel.textWrappingMode = TextWrappingModes.NoWrap;

        if (sendButtonLabel != null)
        {
            spinnerLabel.font = sendButtonLabel.font;
            spinnerLabel.fontSharedMaterial = sendButtonLabel.fontSharedMaterial;
        }

        go.SetActive(false);
    }
}
