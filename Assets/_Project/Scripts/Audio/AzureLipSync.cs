using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.CognitiveServices.Speech;

/// <summary>
/// Synthesizes Spanish speech with Azure and drives mouth blend shapes from viseme events.
/// TTS, PCM-to-AudioClip conversion, and viseme mapping follow Microsoft Azure Speech SDK docs and samples.
/// </summary>
[DefaultExecutionOrder(1000)]
public class AzureLipSync : MonoBehaviour
{
    [Header("Azure Credentials")]
    public string subscriptionKey = "YOUR_API_KEY_HERE";
    public string region = "eastus";

    [Header("References")]
    public SkinnedMeshRenderer meshRenderer;
    public Animator characterAnimator;
    public AudioSource voiceAudioSource;

    [Header("BlendShape Indices")]
    public int indexA = 9;
    public int indexE = 11;
    public int indexU = 12;
    public int indexJaw = 15;

    [Header("Animation Settings")]
    public float smoothness = 25f;

    [Header("Animation State Names (MUST MATCH ANIMATOR EXACTLY)")]
    public string idleStateName = "TTB_idle1";
    public string talkStateName = "TTB_talk2";

    private float targetA, targetE, targetU, targetJaw;
    private float weightA, weightE, weightU, weightJaw;

    private bool isMoving = false;
    private float elapsedTime = 0f;
    private Coroutine activeRoutine;
    private bool speechActive;
    private bool pausedByFocus;
    private float speechEndTime;

    private SpeechConfig speechConfig;
    private SpeechSynthesizer synthesizer;
    private ulong speechGenerationId;
    private Coroutine activeSpeakRoutine;
    private bool isShuttingDown;

    private struct VisemeData { public float time; public int visemeId; }
    private List<VisemeData> visemeList = new List<VisemeData>();

    void Start()
    {
        if (voiceAudioSource == null) voiceAudioSource = GetComponent<AudioSource>();
        if (voiceAudioSource != null)
        {
            voiceAudioSource.playOnAwake = false;
            voiceAudioSource.spatialBlend = 0f;
        }

        speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
        if (string.IsNullOrWhiteSpace(subscriptionKey) || subscriptionKey.Contains("YOUR_API"))
            Debug.LogWarning("Azure TTS: configure a valid subscription key on AzureLipSync before the pilot.");
        speechConfig.SpeechSynthesisVoiceName = "es-MX-JorgeNeural";
        speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw16Khz16BitMonoPcm);

        synthesizer = new SpeechSynthesizer(speechConfig, null);

        synthesizer.VisemeReceived += (s, e) =>
        {
            lock (visemeList)
            {
                visemeList.Add(new VisemeData { time = e.AudioOffset / 10000000f, visemeId = (int)e.VisemeId });
            }
        };
    }

    /// <summary>Invalidates in-flight synthesis and stops playback (e.g. on question change).</summary>
    public void CancelPendingSpeech()
    {
        speechGenerationId++;
        if (activeSpeakRoutine != null)
        {
            StopCoroutine(activeSpeakRoutine);
            activeSpeakRoutine = null;
        }
        StopTalking();
    }

    public void SpeakText(string text, TtsExchangeContext context)
    {
        if (string.IsNullOrWhiteSpace(text) || isShuttingDown)
            return;

        if (activeSpeakRoutine != null)
        {
            StopCoroutine(activeSpeakRoutine);
            activeSpeakRoutine = null;
        }

        activeSpeakRoutine = StartCoroutine(SpeakTextRoutine(text, context));
    }

    IEnumerator SpeakTextRoutine(string text, TtsExchangeContext context)
    {
        try
        {
            yield return SpeakTextRoutineCore(text, context);
        }
        finally
        {
            activeSpeakRoutine = null;
        }
    }

    IEnumerator SpeakTextRoutineCore(string text, TtsExchangeContext context)
    {
        if (synthesizer == null)
        {
            Debug.LogError("Azure TTS: synthesizer not initialized.");
            TtsOutcomeReporter.Report(context, false, "synthesizer_not_initialized");
            NotifyTtsFailureToChat();
            yield break;
        }

        ulong generation = ++speechGenerationId;

        lock (visemeList) { visemeList.Clear(); }
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        StopTalking();

        Task<SpeechSynthesisResult> speakTask;
        try
        {
            speakTask = synthesizer.SpeakTextAsync(text);
        }
        catch (System.Exception ex)
        {
            if (generation != speechGenerationId || isShuttingDown)
                yield break;

            Debug.LogError("Azure TTS exception: " + ex.Message);
            TtsOutcomeReporter.Report(context, false, "exception:" + ex.GetType().Name);
            NotifyTtsFailureToChat();
            yield break;
        }

        while (!speakTask.IsCompleted)
            yield return null;

        if (generation != speechGenerationId || isShuttingDown)
            yield break;

        SpeechSynthesisResult result;
        try
        {
            result = speakTask.Result;
        }
        catch (System.Exception ex)
        {
            if (generation != speechGenerationId || isShuttingDown)
                yield break;

            Debug.LogError("Azure TTS exception: " + ex.Message);
            TtsOutcomeReporter.Report(context, false, "exception:" + ex.GetType().Name);
            NotifyTtsFailureToChat();
            yield break;
        }

        using (result)
        {
            if (generation != speechGenerationId || isShuttingDown)
                yield break;

            if (result.Reason == ResultReason.SynthesizingAudioCompleted &&
                result.AudioData != null && result.AudioData.Length >= 2)
            {
                var sampleCount = result.AudioData.Length / 2;
                if (sampleCount <= 0)
                {
                    TtsOutcomeReporter.Report(context, false, "empty_audio");
                    NotifyTtsFailureToChat();
                    yield break;
                }

                var audioClip = AudioClip.Create("AzureAudio", sampleCount, 1, 16000, false);
                float[] audioFloats = new float[sampleCount];

                for (int i = 0; i < sampleCount; i++)
                {
                    audioFloats[i] = (short)(result.AudioData[i * 2] | (result.AudioData[i * 2 + 1] << 8)) / 32768.0f;
                }

                audioClip.SetData(audioFloats, 0);

                if (generation != speechGenerationId || isShuttingDown)
                    yield break;

                if (voiceAudioSource == null)
                {
                    TtsOutcomeReporter.Report(context, false, "audio_source_missing");
                    NotifyTtsFailureToChat();
                    yield break;
                }

                voiceAudioSource.clip = audioClip;

                float exactEndTime = audioClip.length;
                lock (visemeList)
                {
                    if (visemeList.Count > 0)
                        exactEndTime = Mathf.Min(visemeList[visemeList.Count - 1].time + 0.05f, audioClip.length);
                }

                if (exactEndTime <= 0.05f) exactEndTime = audioClip.length;

                activeRoutine = StartCoroutine(MouthRoutine(exactEndTime, generation));
                TtsOutcomeReporter.Report(context, true, "");
            }
            else
            {
                string failureReason = result.Reason.ToString();
                if (result.Reason == ResultReason.Canceled)
                {
                    var details = SpeechSynthesisCancellationDetails.FromResult(result);
                    failureReason = details.ErrorDetails;
                    Debug.LogError("Azure TTS canceled: " + details.ErrorDetails);
                }
                else
                {
                    Debug.LogError("Azure TTS failed: " + result.Reason);
                }

                TtsOutcomeReporter.Report(context, false, failureReason);
                NotifyTtsFailureToChat();
            }
        }
    }

    IEnumerator MouthRoutine(float exactEndTime, ulong generation)
    {
        if (generation != speechGenerationId)
            yield break;

        speechEndTime = exactEndTime;
        speechActive = true;
        pausedByFocus = false;

        if (voiceAudioSource == null)
            yield break;

        voiceAudioSource.Play();
        isMoving = true;
        elapsedTime = 0f;
        float speechWallClockStart = Time.unscaledTime;

        while (speechActive && generation == speechGenerationId &&
               voiceAudioSource != null && voiceAudioSource.clip != null)
        {
            if (!pausedByFocus)
                elapsedTime = voiceAudioSource.time;

            if (!pausedByFocus && !voiceAudioSource.isPlaying)
                break;

            if ((!pausedByFocus && voiceAudioSource.time >= exactEndTime) ||
                Time.unscaledTime - speechWallClockStart >= exactEndTime + 5f)
                break;

            yield return null;
        }

        if (generation != speechGenerationId)
            yield break;

        speechActive = false;
        pausedByFocus = false;
        activeRoutine = null;
        StopTalking();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        HandleSpeechPause(!hasFocus);
    }

    void OnApplicationPause(bool pauseStatus)
    {
        HandleSpeechPause(pauseStatus);
    }

    void HandleSpeechPause(bool shouldPause)
    {
        if (voiceAudioSource == null || !speechActive)
            return;

        if (shouldPause)
        {
            if (voiceAudioSource.isPlaying)
            {
                pausedByFocus = true;
                voiceAudioSource.Pause();
            }
            return;
        }

        if (!pausedByFocus)
            return;

        pausedByFocus = false;
        if (voiceAudioSource.clip == null || voiceAudioSource.time >= speechEndTime)
            return;

        voiceAudioSource.UnPause();
        isMoving = true;
    }

    void LateUpdate()
    {
        if (isShuttingDown || meshRenderer == null || !isMoving)
            return;

        int activeVisemeId = 0;
        lock (visemeList)
        {
            for (int i = 0; i < visemeList.Count; i++)
            {
                if (elapsedTime >= visemeList[i].time) activeVisemeId = visemeList[i].visemeId;
                else break;
            }
        }

        MapVisemeToBlendshapes(activeVisemeId);

        if (IsAnimatorUsable())
        {
            if (activeVisemeId == 0 && characterAnimator.GetBool("isTalking"))
                SetAnimatorTalking(false);
            else if (activeVisemeId != 0 && !characterAnimator.GetBool("isTalking"))
                SetAnimatorTalking(true);
        }

        if (activeVisemeId == 0)
        {
            weightA = 0f; weightE = 0f; weightU = 0f; weightJaw = 0f;
        }
        else
        {
            float lerpFactor = Time.deltaTime * smoothness;
            weightA = Mathf.Lerp(weightA, targetA, lerpFactor);
            weightE = Mathf.Lerp(weightE, targetE, lerpFactor);
            weightU = Mathf.Lerp(weightU, targetU, lerpFactor);
            weightJaw = Mathf.Lerp(weightJaw, targetJaw, lerpFactor);
        }

        meshRenderer.SetBlendShapeWeight(indexA, weightA);
        meshRenderer.SetBlendShapeWeight(indexE, weightE);
        meshRenderer.SetBlendShapeWeight(indexU, weightU);
        meshRenderer.SetBlendShapeWeight(indexJaw, weightJaw);
    }

    bool IsAnimatorUsable()
    {
        return characterAnimator != null &&
               characterAnimator.gameObject.activeInHierarchy &&
               characterAnimator.isActiveAndEnabled &&
               !isShuttingDown;
    }

    void SetAnimatorTalking(bool talking)
    {
        if (!IsAnimatorUsable())
            return;

        characterAnimator.SetBool("isTalking", talking);
        characterAnimator.CrossFade(talking ? talkStateName : idleStateName, talking ? 0.1f : 0.15f);
    }

    public void StopTalking()
    {
        speechActive = false;
        pausedByFocus = false;
        isMoving = false;

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (voiceAudioSource != null && voiceAudioSource.isPlaying)
            voiceAudioSource.Stop();

        if (IsAnimatorUsable())
        {
            characterAnimator.SetBool("isTalking", false);
            characterAnimator.CrossFade(idleStateName, 0.15f);
        }

        ResetMouth();
    }

    void ResetMouth()
    {
        targetA = 0; targetE = 0; targetU = 0; targetJaw = 0;
        weightA = 0; weightE = 0; weightU = 0; weightJaw = 0;

        if (meshRenderer != null)
        {
            meshRenderer.SetBlendShapeWeight(indexA, 0);
            meshRenderer.SetBlendShapeWeight(indexE, 0);
            meshRenderer.SetBlendShapeWeight(indexU, 0);
            meshRenderer.SetBlendShapeWeight(indexJaw, 0);
        }
    }

    private void MapVisemeToBlendshapes(int visemeId)
    {
        targetA = 0f; targetE = 0f; targetU = 0f; targetJaw = 0f;

        switch (visemeId)
        {
            case 0: break;
            case 1: case 2: case 3: targetA = 80f; targetJaw = 20f; break;
            case 4: case 5: targetE = 80f; targetJaw = 15f; break;
            case 6: case 7: case 8: case 9: targetU = 90f; targetJaw = 30f; break;
            case 10: case 11: case 12: case 13: case 14: targetJaw = 40f; targetE = 30f; break;
            case 15: case 16: case 17: targetJaw = 20f; break;
            case 18: targetJaw = 0f; break;
            case 19: case 20: case 21: targetJaw = 80f; targetA = 50f; break;
        }
    }

    void NotifyTtsFailureToChat()
    {
        var experiment = Object.FindFirstObjectByType<ExperimentLogic>(FindObjectsInactive.Include);
        if (experiment != null)
        {
            experiment.AppendChatNotice(
                "No pude reproducir la voz del agente. Puedes seguir leyendo la respuesta en el chat.");
        }
    }

    void OnDestroy()
    {
        isShuttingDown = true;
        speechGenerationId++;

        if (activeSpeakRoutine != null)
        {
            StopCoroutine(activeSpeakRoutine);
            activeSpeakRoutine = null;
        }

        speechActive = false;
        pausedByFocus = false;
        isMoving = false;

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (voiceAudioSource != null && voiceAudioSource.isPlaying)
            voiceAudioSource.Stop();

        if (synthesizer != null)
        {
            synthesizer.Dispose();
            synthesizer = null;
        }
    }
}