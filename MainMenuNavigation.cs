using UnityEngine;
using System.Collections;

public class MainMenuNavigation : MonoBehaviour
{
    [Header("References")]
    public Transform playerRig;               // Root Transform of the XR-Rig (not Main Camera!)
    public Transform mainMenuPoint;           // Where the rig should be positioned in the Main Menu
    public Transform selectModePoint;         // Where the rig moves when “Select Mode” is chosen
    public Transform settingsPoint;           // Where the rig moves when “Settings” is chosen

    [Tooltip("This should be a Transform placed at the *ground-level* where you want the XR-Rig to land when the game starts.")]
    public Transform startGamePoint;          // STILL drag your “StartGame_CameraPoint” here—but we’ll treat it as a *camera target*, not a rig target

    [Header("UI Panels")]
    public GameObject mainMenuUI;
    public GameObject selectModeUI;
    public GameObject settingsUI;
    public GameObject startGameUI;

    [Header("Prolog Reference")]
    public PrologController prologController; // Drag in your PrologManager GameObject here

    [Header("Transition Settings")]
    public float transitionDuration = 1f;

    [Header("Audio Settings")]
    public AudioClip[] bgClips;               // At least two BG clips for the main menu
    public AudioClip clickClip;               // SFX for button clicks
    public AudioSource bgAudioSource;         // AudioSource that plays BG music & SFX

    private int lastBgIndex = -1;
    private Coroutine bgMusicCoroutine;

    // === new fields for camera management ===
    private Camera mainCamera;                // reference to Camera.main
    private Vector3 cameraLocalOffset;        // the (rig-relative) offset of the camera before prolog begins
    private Transform cameraOriginalParent;   // to remember where the camera belonged before detaching
    // ==========================================

    void Awake()
    {
        // 1) Validate that we indeed have a Camera.main at start
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[MainMenuNavigation] No Camera.main found!");
        }
        else
        {
            // Record the camera’s parent (which should be under playerRig)
            cameraOriginalParent = mainCamera.transform.parent;
            // Record how far “up” the camera sits, relative to the rig’s origin
            cameraLocalOffset = mainCamera.transform.localPosition;
        }

        // 2) Validate AudioListener (just a sanity check)
        AudioListener listener = FindObjectOfType<AudioListener>();
        if (listener == null) Debug.LogError("[AudioDebug] AudioListener not found in scene!");
        else Debug.Log("[AudioDebug] AudioListener: " + listener.gameObject.name);

        // 3) Validate bgAudioSource, bgClips, clickClip
        if (bgAudioSource == null) Debug.LogError("[AudioDebug] bgAudioSource has not been assigned!");
        else
        {
            if (!bgAudioSource.enabled) Debug.LogWarning("[AudioDebug] bgAudioSource is disabled!");
            if (bgAudioSource.mute) Debug.LogWarning("[AudioDebug] bgAudioSource is muted!");
            Debug.Log("[AudioDebug] bgAudioSource: " + bgAudioSource.gameObject.name);
        }

        if (bgClips == null || bgClips.Length < 2)
            Debug.LogError("[AudioDebug] bgClips must have at least two clips assigned!");
        else
            for (int i = 0; i < bgClips.Length; i++)
                if (bgClips[i] == null)
                    Debug.LogError($"[AudioDebug] bgClips[{i}] has not been assigned!");
                else
                    Debug.Log($"[AudioDebug] bgClips[{i}] = {bgClips[i].name}");

        if (clickClip == null)
            Debug.LogWarning("[AudioDebug] clickClip has not been assigned!");
        else
            Debug.Log("[AudioDebug] clickClip = " + clickClip.name);

        // 4) Position the XR-Rig at the mainMenuPoint and freeze physics
        if (playerRig != null && mainMenuPoint != null)
        {
            Rigidbody rb = playerRig.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            playerRig.position = mainMenuPoint.position;
            playerRig.rotation = mainMenuPoint.rotation;
        }
        else
        {
            Debug.LogError("[MainMenuNavigation] playerRig or mainMenuPoint is null!");
        }

        // 5) Initialize which UI panels are active
        mainMenuUI.SetActive(true);
        selectModeUI.SetActive(false);
        settingsUI.SetActive(false);
        startGameUI.SetActive(false);

        // 6) Start looping BG music
        if (bgAudioSource != null && bgClips != null && bgClips.Length >= 2)
        {
            bgMusicCoroutine = StartCoroutine(PlayBgMusicLoop());
            Debug.Log("[AudioDebug] PlayBgMusicLoop() started.");
        }

        // 7) Subscribe to prolog completion
        if (prologController != null)
        {
            prologController.OnPrologComplete += OnPrologFinished;
            Debug.Log("[MainMenuNavigation] Subscribed to OnPrologComplete().");
        }
        else
        {
            Debug.LogWarning("[MainMenuNavigation] prologController was not assigned in Inspector!");
        }
    }

    void Start()
    {
        // (Nothing else needed here; Awake already did subscriptions.)
    }

    void OnDestroy()
    {
        if (prologController != null)
            prologController.OnPrologComplete -= OnPrologFinished;
    }

    IEnumerator PlayBgMusicLoop()
    {
        while (true)
        {
            int nextIndex = Random.Range(0, bgClips.Length);
            if (bgClips.Length > 1)
                while (nextIndex == lastBgIndex)
                    nextIndex = Random.Range(0, bgClips.Length);

            lastBgIndex = nextIndex;
            bgAudioSource.clip = bgClips[nextIndex];
            Debug.Log($"[AudioDebug] Playing BG main menu: {bgClips[nextIndex].name}");
            bgAudioSource.Play();
            yield return new WaitForSeconds(bgAudioSource.clip.length);
        }
    }

    public void PlayClickSound()
    {
        if (bgAudioSource == null)
        {
            Debug.LogWarning("[AudioDebug] bgAudioSource is null, cannot PlayOneShot()");
            return;
        }
        if (clickClip == null)
        {
            Debug.LogWarning("[AudioDebug] clickClip is null, no SFX assigned for button.");
            return;
        }
        Debug.Log("[AudioDebug] Play SFX click: " + clickClip.name);
        bgAudioSource.PlayOneShot(clickClip);
    }

    public void GoToSelectMode()
    {
        PlayClickSound();
        Debug.Log("[MainMenuNavigation] GoToSelectMode()");
        StopAllCoroutines();
        StartCoroutine(SmoothTransition(selectModePoint, () =>
        {
            mainMenuUI.SetActive(false);
            selectModeUI.SetActive(true);
            settingsUI.SetActive(false);
        }));
    }

    public void GoToMainMenu()
    {
        PlayClickSound();
        Debug.Log("[MainMenuNavigation] GoToMainMenu()");
        StopAllCoroutines();
        StartCoroutine(SmoothTransition(mainMenuPoint, () =>
        {
            mainMenuUI.SetActive(true);
            selectModeUI.SetActive(false);
            settingsUI.SetActive(false);
        }));
    }

    public void GoToSettings()
    {
        PlayClickSound();
        Debug.Log("[MainMenuNavigation] GoToSettings()");
        StopAllCoroutines();
        StartCoroutine(SmoothTransition(settingsPoint, () =>
        {
            mainMenuUI.SetActive(false);
            selectModeUI.SetActive(false);
            settingsUI.SetActive(true);
        }));
    }

    public void GoToCareerMode()
    {
        PlayClickSound();

        // 1) Stop only the main‐menu BG music (leave other sounds alone)
        if (bgMusicCoroutine != null)
        {
            StopCoroutine(bgMusicCoroutine);
            Debug.Log("[AudioDebug] Stopped PlayBgMusicLoop()");
        }
        if (bgAudioSource != null)
        {
            bgAudioSource.Stop();
            Debug.Log("[AudioDebug] bgAudioSource.Stop()");
        }

        // 2) Detach camera from rig so PrologController can move it independently
        if (mainCamera != null)
        {
            // Detach the camera from the XR Rig hierarchy
            mainCamera.transform.parent = null;
            Debug.Log("[MainMenuNavigation] Camera detached from XR-Rig for prolog.");
        }

        // 3) Trigger Prolog sequence
        if (prologController != null)
        {
            Debug.Log("[MainMenuNavigation] Calling StartPrologSequence()");
            prologController.StartPrologSequence();
        }
        else
        {
            Debug.LogWarning("[MainMenuNavigation] prologController is still null!");
        }
    }

    void OnPrologFinished()
    {
        // 1) Compute where the XR-Rig needs to go so that the camera ends up exactly at startGamePoint
        if (startGamePoint == null)
        {
            Debug.LogError("[MainMenuNavigation] startGamePoint has not been assigned!");
            return;
        }
        if (mainCamera == null)
        {
            Debug.LogError("[MainMenuNavigation] Camera.main was not found in OnPrologFinished!");
            return;
        }

        Debug.Log($"[MainMenuNavigation] OnPrologFinished() → moving XR-Rig so camera lands at {startGamePoint.name}");

        // Re-parent and reposition the camera *after* the transition:
        StartCoroutine(SmoothTransitionToStartGame());
    }

    private IEnumerator SmoothTransitionToStartGame()
    {
        // We still want a smooth motion for the rig → so we’ll interpolate the *camera* from wherever it is
        //    to the “startGamePoint” and at the same time slide the rig to keep the camera at the correct offset.

        // 1) Record the camera’s current position and rotation in world space:
        Vector3 camStartPos = mainCamera.transform.position;
        Quaternion camStartRot = mainCamera.transform.rotation;

        // 2) The “world target” for the camera is simply startGamePoint.position / rotation:
        Vector3 camEndPos = startGamePoint.position;
        Quaternion camEndRot = startGamePoint.rotation;

        // 3) Meanwhile, we know how far “up” the camera sits relative to the rig:
        Vector3 rigOffset = cameraLocalOffset; 
        //    (this was recorded in Awake: cameraLocalOffset = mainCamera.transform.localPosition)

        // 4) We’ll lerp the camera itself from camStartPos→camEndPos, and lerp the rig from its *current* to *(camEndPos – rigOffset)*:
        Vector3 rigStartPos = playerRig.position;
        Quaternion rigStartRot = playerRig.rotation;

        // Calculate the rig’s end position so that, when the camera (child) is re-parented, 
        // the camera’s world position equals camEndPos. Because cameraLocalOffset is the local 
        // offset from rig’s origin to camera, we want:
        //     ( rigEndPos + cameraLocalOffset_inWorld ) == camEndPos  
        // Since cameraLocalOffset is a *local* Vector3, we must transform it by the rig’s final rotation 
        // to know exactly how to subtract. Because we also want the rig to match startGamePoint.rotation:
        Quaternion rigEndRot = startGamePoint.rotation;
        Vector3 cameraOffsetWorld = rigEndRot * cameraLocalOffset; 
        Vector3 rigEndPos = camEndPos - cameraOffsetWorld;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            float tEase = EaseInOutQuad(elapsed / transitionDuration);

            // 1) Lerp the camera itself
            mainCamera.transform.position = Vector3.Lerp(camStartPos, camEndPos, tEase);
            mainCamera.transform.rotation = Quaternion.Slerp(camStartRot, camEndRot, tEase);

            // 2) Lerp the rig toward rigEndPos / rigEndRot
            playerRig.position = Vector3.Lerp(rigStartPos, rigEndPos, tEase);
            playerRig.rotation = Quaternion.Slerp(rigStartRot, rigEndRot, tEase);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap both exactly at the end
        mainCamera.transform.position = camEndPos;
        mainCamera.transform.rotation = camEndRot;
        playerRig.position = rigEndPos;
        playerRig.rotation = rigEndRot;

        // 5) Now that both camera & rig are in the final, correct place:
        //    • Re-parent the camera under the XR-Rig
        mainCamera.transform.parent = playerRig;
        mainCamera.transform.localPosition = cameraLocalOffset;
        mainCamera.transform.localRotation = Quaternion.identity;
        Debug.Log("[MainMenuNavigation] Camera re-parented to XR-Rig; localOffset restored.");

        // 6) Re-enable physics on the rig so it “lands” on the ground if above it:
        Rigidbody rb = playerRig.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            Debug.Log("[AudioDebug] playerRig physics re-enabled so it can settle on the ground.");
        }

        // 7) Finally, show the “Start Game” UI and hide everything else
        mainMenuUI.SetActive(false);
        selectModeUI.SetActive(false);
        settingsUI.SetActive(false);
        startGameUI.SetActive(true);

        Debug.Log($"[MainMenuNavigation] startGameUI is now active. XR-Rig at {playerRig.position}, camera at {mainCamera.transform.position}");
    }

    private float EaseInOutQuad(float t)
    {
        if (t < 0.5f) return 2f * t * t;
        return -1f + (4f - 2f * t) * t;
    }

    IEnumerator SmoothTransition(Transform target, System.Action onComplete)
    {
        if (playerRig == null || target == null)
        {
            Debug.LogError("[MainMenuNavigation] playerRig or target is null in SmoothTransition!");
            yield break;
        }

        Vector3 startPos = playerRig.position;
        Quaternion startRot = playerRig.rotation;
        Vector3 endPos = target.position;
        Quaternion endRot = target.rotation;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            float tEase = EaseInOutQuad(elapsed / transitionDuration);
            playerRig.position = Vector3.Lerp(startPos, endPos, tEase);
            playerRig.rotation = Quaternion.Slerp(startRot, endRot, tEase);
            elapsed += Time.deltaTime;
            yield return null;
        }

        playerRig.position = endPos;
        playerRig.rotation = endRot;

        onComplete?.Invoke();
    }
}
