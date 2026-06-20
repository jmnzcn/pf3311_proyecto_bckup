/// <summary>
/// Links a TTS synthesis attempt to the chat exchange that triggered it (condition C).
/// </summary>
public struct TtsExchangeContext
{
    public int ScenarioNumber;
    public string ScenarioName;
    public string ConditionCode;
    public int QuestionNumber;
    public int ExchangeIndex;

    public bool IsValid =>
        ScenarioNumber > 0 && QuestionNumber > 0 && ExchangeIndex > 0 && !string.IsNullOrEmpty(ConditionCode);
}
