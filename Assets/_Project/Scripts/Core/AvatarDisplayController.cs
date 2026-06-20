using System.Collections;

using UnityEngine;

using UnityEngine.UI;

#if UNITY_RENDER_PIPELINE_URP

using UnityEngine.Rendering.Universal;

#endif



/// <summary>

/// Shows the 3D avatar in the right UI panel (condition C only; scenario index 2).

/// Uses a dedicated camera + RenderTexture; tuned for NICE DCV / CPU-only VMs.

/// </summary>

[DefaultExecutionOrder(500)]

public class AvatarDisplayController : MonoBehaviour

{

    const int AvatarLayer = 6;



    [SerializeField] GameObject characterModel;

    [SerializeField] RectTransform displayPanel;

    [SerializeField] Camera mainCamera;

    [SerializeField] Vector3 displayAnchorPosition = Vector3.zero;



    [Header("Remote streaming (DCV)")]

    [Tooltip("RenderTexture edge size. 512 is enough for the side panel and much lighter on VM.")]

    [SerializeField] int renderTextureSize = 768;

    [Tooltip("Avatar RT refresh rate. Lower = less GPU/CPU load over DCV; motion stays smooth enough for talk.")]

    [SerializeField] float avatarRenderFps = 24f;

    [Tooltip("Recalculate camera framing only when the avatar is shown, not every frame.")]

    [SerializeField] bool cacheFraming = true;



    Camera avatarCamera;

    Animator characterAnimator;

    int mainCameraOriginalMask = -1;

    bool originalApplyRootMotion;

    bool hasStoredAnimatorState;

    RenderTexture renderTexture;

    RawImage rawImage;

    bool isShowing;

    bool isBuilt;

    bool isTearingDown;

    bool framingDirty = true;

    float nextAvatarRenderTime;

    Vector3 lastMainCameraPosition;

    Coroutine refreshCoroutine;



    /// <summary>Call before application exit; skips avatar layer/animator restore during teardown.</summary>

    public void ShutdownForExit()

    {

        isTearingDown = true;



        if (refreshCoroutine != null)

        {

            StopCoroutine(refreshCoroutine);

            refreshCoroutine = null;

        }



        isShowing = false;



        if (avatarCamera != null)

            avatarCamera.enabled = false;

        if (rawImage != null)

            rawImage.enabled = false;



        RestoreMainCameraMaskOnly();

    }



    void RestoreMainCameraMaskOnly()

    {

        if (mainCamera != null && mainCameraOriginalMask >= 0)

            mainCamera.cullingMask = mainCameraOriginalMask;

    }



    static bool IsAlive(Object unityObject) => unityObject != null;



    static bool IsAliveGameObject(GameObject obj) => obj != null;



    void Awake()

    {

        ResolveReferences();

        DetachFromUiCanvas();

    }



    void Start()

    {

        ResolveReferences();

        BuildDisplayIfNeeded();

        SyncDisplay(false);

    }



    void LateUpdate()

    {

        if (isTearingDown || !IsAliveGameObject(characterModel))

            return;



        bool shouldShow = characterModel.activeInHierarchy;

        if (shouldShow != isShowing)

            SyncDisplay(shouldShow);



        if (!isShowing || avatarCamera == null || !IsAliveGameObject(characterModel))

            return;



        if (mainCamera != null)

        {

            Vector3 camPos = mainCamera.transform.position;

            if ((camPos - lastMainCameraPosition).sqrMagnitude > 0.0001f)

            {

                lastMainCameraPosition = camPos;

                framingDirty = true;

            }

        }



        FaceMainCamera();



        if (!cacheFraming || framingDirty)

        {

            FrameCamera();

            framingDirty = false;

        }



        RenderAvatarIfDue();

    }



    public void RefreshDisplay()

    {

        ResolveReferences();

        BuildDisplayIfNeeded();



        if (refreshCoroutine != null)

            StopCoroutine(refreshCoroutine);

        refreshCoroutine = StartCoroutine(RefreshDisplayRoutine());

    }



    IEnumerator RefreshDisplayRoutine()

    {

        yield return null;

        yield return new WaitForEndOfFrame();



        if (isTearingDown)

        {

            refreshCoroutine = null;

            yield break;

        }



        bool shouldShow = IsAliveGameObject(characterModel) && characterModel.activeInHierarchy;

        SyncDisplay(shouldShow);



        if (shouldShow)

        {

            framingDirty = true;

            nextAvatarRenderTime = 0f;

            yield return null;

            FaceMainCamera();

            FrameCamera();

            framingDirty = false;

            RenderAvatarIfDue();

        }



        refreshCoroutine = null;

    }



    void ResolveReferences()

    {

        if (characterModel == null)

        {

            var questionManager = GetComponent<MyProject.QuestionManager>();

            if (questionManager != null)

                characterModel = questionManager.characterModel;

        }



        if (displayPanel == null)

            Debug.LogWarning("AvatarDisplayController: assign displayPanel in the Inspector (RightPanel RectTransform).");



        if (mainCamera == null)

            Debug.LogWarning("AvatarDisplayController: assign mainCamera in the Inspector (Main Camera).");



        if (characterAnimator == null && characterModel != null)

            characterAnimator = characterModel.GetComponent<Animator>();

    }



    void DetachFromUiCanvas()

    {

        if (characterModel == null)

            return;



        var parentCanvas = characterModel.GetComponentInParent<Canvas>();

        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace)

            characterModel.transform.SetParent(null, true);

    }



    void BuildDisplayIfNeeded()

    {

        if (isBuilt || displayPanel == null || characterModel == null)

            return;



        int rtSize = Mathf.Clamp(renderTextureSize, 256, 1024);

        renderTexture = new RenderTexture(rtSize, rtSize, 16, RenderTextureFormat.ARGB32);

        renderTexture.antiAliasing = 4;

        renderTexture.Create();



        var overlayCanvas = displayPanel.GetComponentInParent<Canvas>();

        if (overlayCanvas == null)

            return;



        var viewObject = new GameObject("AvatarRenderView", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));

        var viewRect = viewObject.GetComponent<RectTransform>();

        viewRect.SetParent(displayPanel, false);

        viewRect.anchorMin = Vector2.zero;

        viewRect.anchorMax = Vector2.one;

        viewRect.offsetMin = Vector2.zero;

        viewRect.offsetMax = Vector2.zero;

        viewRect.localScale = Vector3.one;

        viewRect.SetAsLastSibling();



        rawImage = viewObject.GetComponent<RawImage>();

        rawImage.texture = renderTexture;

        rawImage.raycastTarget = false;

        rawImage.color = Color.white;

        rawImage.enabled = false;



        var rig = new GameObject("AvatarCameraRig");

        rig.transform.SetParent(transform, false);



        avatarCamera = rig.AddComponent<Camera>();

        avatarCamera.clearFlags = CameraClearFlags.SolidColor;

        avatarCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);

        avatarCamera.targetTexture = renderTexture;

        avatarCamera.cullingMask = 1 << AvatarLayer;

        avatarCamera.fieldOfView = 26f;

        avatarCamera.nearClipPlane = 0.1f;

        avatarCamera.farClipPlane = 15f;

        avatarCamera.depth = 20f;

        avatarCamera.allowHDR = false;

        avatarCamera.allowMSAA = false;

        avatarCamera.enabled = false;

        ConfigureUrpCamera(avatarCamera);



        var keyLightObject = new GameObject("AvatarKeyLight");

        keyLightObject.transform.SetParent(rig.transform, false);

        keyLightObject.transform.localRotation = Quaternion.Euler(35f, -25f, 0f);

        var keyLight = keyLightObject.AddComponent<Light>();

        keyLight.type = LightType.Directional;

        keyLight.intensity = 1.2f;

        keyLight.shadows = LightShadows.None;

        keyLight.cullingMask = 1 << AvatarLayer;



        var fillLightObject = new GameObject("AvatarFillLight");

        fillLightObject.transform.SetParent(rig.transform, false);

        fillLightObject.transform.localRotation = Quaternion.Euler(12f, 145f, 0f);

        var fillLight = fillLightObject.AddComponent<Light>();

        fillLight.type = LightType.Directional;

        fillLight.intensity = 0.65f;

        fillLight.shadows = LightShadows.None;

        fillLight.cullingMask = 1 << AvatarLayer;



        isBuilt = true;

    }



    static void ConfigureUrpCamera(Camera camera)

    {

#if UNITY_RENDER_PIPELINE_URP

        var data = camera.gameObject.GetComponent<UniversalAdditionalCameraData>();

        if (data == null)

            data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

        data.renderPostProcessing = false;

        data.requiresColorOption = CameraOverrideOption.Off;

        data.requiresDepthOption = CameraOverrideOption.Off;

        data.renderShadows = false;

#endif

    }



    void SyncDisplay(bool shouldShow)

    {

        if (isTearingDown)

            return;



        BuildDisplayIfNeeded();

        if (!isBuilt)

            return;



        if (shouldShow)

            PrepareForDisplay();

        else

            RestoreAfterDisplay();



        isShowing = shouldShow;



        if (rawImage != null)

            rawImage.enabled = shouldShow;



        if (avatarCamera != null)

        {

            avatarCamera.enabled = false;

            if (shouldShow)

            {

                framingDirty = true;

                nextAvatarRenderTime = 0f;

            }

        }

    }



    void PrepareForDisplay()

    {

        if (characterModel == null)

            return;



        DetachFromUiCanvas();

        SnapToDisplayAnchor();

        SetLayerRecursively(characterModel, AvatarLayer);

        EnsureRenderersUpdateOffscreen();

        ConfigureAnimatorForDisplay(true);



        if (mainCamera == null)

            return;



        if (mainCameraOriginalMask < 0)

            mainCameraOriginalMask = mainCamera.cullingMask;

        mainCamera.cullingMask &= ~(1 << AvatarLayer);



        lastMainCameraPosition = mainCamera.transform.position;

        framingDirty = true;

        FaceMainCamera();

        FrameCamera();

        framingDirty = false;

    }



    void RestoreAfterDisplay()

    {

        if (isTearingDown)

        {

            RestoreMainCameraMaskOnly();

            return;

        }



        ConfigureAnimatorForDisplay(false);



        if (IsAliveGameObject(characterModel))

            SetLayerRecursively(characterModel, 0);



        RestoreMainCameraMaskOnly();

    }



    void ConfigureAnimatorForDisplay(bool showing)

    {

        if (characterAnimator == null || characterModel == null)

            return;



        if (!characterAnimator.gameObject.activeInHierarchy)

            return;



        if (showing)

        {

            if (!hasStoredAnimatorState)

            {

                originalApplyRootMotion = characterAnimator.applyRootMotion;

                hasStoredAnimatorState = true;

            }



            characterAnimator.applyRootMotion = false;

            return;

        }



        if (hasStoredAnimatorState)

            characterAnimator.applyRootMotion = originalApplyRootMotion;

    }



    void RenderAvatarIfDue()

    {

        if (avatarCamera == null || !isShowing)

            return;



        if (avatarRenderFps <= 0f)

        {

            if (!avatarCamera.enabled)

                avatarCamera.enabled = true;

            return;

        }



        avatarCamera.enabled = false;



        if (Time.unscaledTime < nextAvatarRenderTime)

            return;



        nextAvatarRenderTime = Time.unscaledTime + (1f / avatarRenderFps);

        avatarCamera.Render();

    }



    void SnapToDisplayAnchor()

    {

        characterModel.transform.SetPositionAndRotation(displayAnchorPosition, Quaternion.identity);

    }



    void EnsureRenderersUpdateOffscreen()

    {

        foreach (var skinned in characterModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))

        {

            skinned.updateWhenOffscreen = true;

            skinned.skinnedMotionVectors = false;

            skinned.allowOcclusionWhenDynamic = false;

            skinned.quality = SkinQuality.Bone2;

        }



        foreach (var mesh in characterModel.GetComponentsInChildren<MeshRenderer>(true))

            mesh.allowOcclusionWhenDynamic = false;

    }



    void FaceMainCamera()

    {

        if (mainCamera == null)

            return;



        Vector3 toCamera = mainCamera.transform.position - characterModel.transform.position;

        toCamera.y = 0f;

        if (toCamera.sqrMagnitude < 0.001f)

            return;



        characterModel.transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);

    }



    void FrameCamera()

    {

        if (avatarCamera == null || characterModel == null)

            return;



        Bounds bounds = CalculateBounds();

        if (bounds.size.sqrMagnitude <= 0.0001f)

            return;



        Vector3 focus = bounds.center;



        Vector3 forward = characterModel.transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)

            forward = Vector3.forward;

        forward.Normalize();



        const float bodyPadding = 1.12f;

        const float verticalFill = 0.84f;

        float frameHeight = bounds.size.y * bodyPadding;

        float halfHeight = frameHeight / (2f * verticalFill);

        float halfWidth = bounds.size.x * 0.55f;



        float aspect = renderTexture != null

            ? (float)renderTexture.width / renderTexture.height

            : avatarCamera.aspect;



        float distanceForHeight = halfHeight / Mathf.Tan(14f * Mathf.Deg2Rad);

        float horizontalFovRad = 2f * Mathf.Atan(Mathf.Tan(14f * Mathf.Deg2Rad) * aspect);

        float distanceForWidth = halfWidth / Mathf.Tan(horizontalFovRad * 0.5f);

        float distance = Mathf.Max(distanceForHeight, distanceForWidth) * 0.656f;



        Vector3 cameraPosition = focus + forward * distance;



        avatarCamera.transform.SetPositionAndRotation(

            cameraPosition,

            Quaternion.LookRotation(focus - cameraPosition, Vector3.up));



        float verticalFov = 2f * Mathf.Atan(halfHeight / distance) * Mathf.Rad2Deg;

        avatarCamera.fieldOfView = Mathf.Clamp(verticalFov, 22f, 34f);

    }



    Bounds CalculateBounds()

    {

        var renderers = characterModel.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)

            return new Bounds(characterModel.transform.position, Vector3.one);



        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)

            bounds.Encapsulate(renderers[i].bounds);

        return bounds;

    }



    static void SetLayerRecursively(GameObject obj, int layer)

    {

        if (!IsAliveGameObject(obj))

            return;



        obj.layer = layer;



        Transform root = obj.transform;

        if (!IsAlive(root))

            return;



        int childCount = root.childCount;

        for (int i = 0; i < childCount; i++)

        {

            Transform child = root.GetChild(i);

            if (IsAlive(child))

                SetLayerRecursively(child.gameObject, layer);

        }

    }



    void OnDestroy()

    {

        isTearingDown = true;



        if (refreshCoroutine != null)

            StopCoroutine(refreshCoroutine);



        isShowing = false;

        RestoreMainCameraMaskOnly();



        if (renderTexture != null)

        {

            renderTexture.Release();

            Destroy(renderTexture);

        }

    }

}


