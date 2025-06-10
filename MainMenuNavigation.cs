// MainMenuNavigation.cs
using UnityEngine;
using System.Collections;

public class MainMenuNavigation : MonoBehaviour
{
    [Header("References")]
    public Transform playerRig;               
    public Transform mainMenuPoint;           
    public Transform selectModePoint;         
    public Transform settingsPoint;           

    [Tooltip("Transform ground‐level untuk XR-Rig saat game mulai.")]
    public Transform startGamePoint;          

    [Header("GameController Reference")]
    [Tooltip("Script MainGameController pada GameController.")]
    public MainGameController gameController;  // Drag GameObject yang punya MainGameController di Inspector

    [Header("UI Panels")]
    public GameObject mainMenuUI;
    public GameObject selectModeUI;
    public GameObject settingsUI;
    public GameObject startGameUI;

    [Header("Prolog Reference")]
    public PrologController prologController; 

    [Header("Audio Settings for Main Menu")]
    public AudioClip[] bgClips;               
    public AudioClip clickClip;               
    public AudioSource bgAudioSource;         

    [Header("TextMeshPro UI to Hide/Show")]
    public GameObject uhiIntensityText;       
    public GameObject hotspotRemainingText;   

    [Header("Transition Settings")]
    public float transitionDuration = 1f;

    public static event System.Action OnGameReady;

    private int lastBgIndex = -1;
    private Coroutine bgMusicCoroutine;
    private Camera mainCamera;                
    private Vector3 cameraLocalOffset;        
    private Transform cameraOriginalParent;   

    void Awake()
    {
        // 1) Matikan logo footstep di MainGameController selama di Main Menu/Prolog
        if (gameController != null)
        {
            gameController.EnableFootstep(false);
            Debug.Log("[MainMenuNavigation] Footstep dinonaktifkan di Main Menu/Prolog.");
        }

        // 2) Cache Camera.main dan local offset
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[MainMenuNavigation] No Camera.main found!");
        }
        else
        {
            cameraOriginalParent = mainCamera.transform.parent;
            cameraLocalOffset = mainCamera.transform.localPosition;
        }

        // 3) AudioListener sanity check
        AudioListener listener = FindObjectOfType<AudioListener>();
        if (listener == null) Debug.LogError("[AudioDebug] AudioListener not found in scene!");
        else Debug.Log("[AudioDebug] AudioListener: " + listener.gameObject.name);

        // 4) Validasi bgAudioSource, bgClips, clickClip
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

        // 5) Posisikan XR-Rig di mainMenuPoint & freeze physics
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

        // 6) Inisialisasi UI Panels
        mainMenuUI.SetActive(true);
        selectModeUI.SetActive(false);
        settingsUI.SetActive(false);
        startGameUI.SetActive(false);

        // 7) Sembunyikan TextMeshPro UI
        if (uhiIntensityText != null) uhiIntensityText.SetActive(false);
        if (hotspotRemainingText != null) hotspotRemainingText.SetActive(false);

        // 8) Mulai looping Main Menu BG music
        if (bgAudioSource != null && bgClips != null && bgClips.Length >= 2)
        {
            bgMusicCoroutine = StartCoroutine(PlayBgMusicLoop());
            Debug.Log("[AudioDebug] PlayBgMusicLoop() started.");
        }

        // 9) Subscribe ke Prolog completion
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

    void OnDestroy()
    {
        if (prologController != null)
            prologController.OnPrologComplete -= OnPrologFinished;
    }

    #region Main Menu Music & SFX

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

    #endregion

    #region Menu Navigation

    public void GoToSelectMode()
    {
        PlayClickSound();
        Debug.Log("[MainMenuNavigation] GoToSelectMode()");
        StopAllCoroutines(); // stop menu-music loop
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
        StopAllCoroutines(); // stop any music coroutines
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
        StopAllCoroutines(); // stop menu-music loop
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

        // Stop main-menu BG music
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

        // Detach camera dari rig agar PrologController bisa gerakin kamera
        if (mainCamera != null)
        {
            mainCamera.transform.parent = null;
            Debug.Log("[MainMenuNavigation] Camera detached from XR-Rig for prolog.");
        }

        // Trigger Prolog sequence
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

    #endregion

    void OnPrologFinished()
    {
        // Dipanggil ketika PrologController invoke OnPrologComplete
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

        Debug.Log($"[MainMenuNavigation] OnPrologFinished() → moving XR-Rig ke {startGamePoint.name}");
        StartCoroutine(SmoothTransitionToStartGame());
    }

    private IEnumerator SmoothTransitionToStartGame()
    {
        // 1) Record posisi/rotasi kamera sekarang
        Vector3 camStartPos = mainCamera.transform.position;
        Quaternion camStartRot = mainCamera.transform.rotation;

        // 2) Target posisi/rotasi kamera
        Vector3 camEndPos = startGamePoint.position;
        Quaternion camEndRot = startGamePoint.rotation;

        // 3) Hitung rigEndPos agar kamera posisinya sesuai offset
        Vector3 rigOffset = cameraLocalOffset;
        Quaternion rigEndRot = startGamePoint.rotation;
        Vector3 cameraOffsetWorld = rigEndRot * cameraLocalOffset;
        Vector3 rigEndPos = camEndPos - cameraOffsetWorld;

        // 4) Record rig posisi/rotasi awal
        Vector3 rigStartPos = playerRig.position;
        Quaternion rigStartRot = playerRig.rotation;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            float tEase = EaseInOutQuad(elapsed / transitionDuration);

            mainCamera.transform.position = Vector3.Lerp(camStartPos, camEndPos, tEase);
            mainCamera.transform.rotation = Quaternion.Slerp(camStartRot, camEndRot, tEase);
            playerRig.position = Vector3.Lerp(rigStartPos, rigEndPos, tEase);
            playerRig.rotation = Quaternion.Slerp(rigStartRot, rigEndRot, tEase);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap ke final
        mainCamera.transform.position = camEndPos;
        mainCamera.transform.rotation = camEndRot;
        playerRig.position = rigEndPos;
        playerRig.rotation = rigEndRot;

        // 5) Reparent kamera ke rig & restore offset
        mainCamera.transform.parent = playerRig;
        mainCamera.transform.localPosition = cameraLocalOffset;
        mainCamera.transform.localRotation = Quaternion.identity;
        Debug.Log("[MainMenuNavigation] Camera re-parented to XR-Rig; localOffset restored.");

        // 6) Re-enable physics agar rig “turun” ke tanah
        Rigidbody rb = playerRig.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            Debug.Log("[AudioDebug] playerRig physics re-enabled.");
        }

        // 7) Tampilkan UI Start Game & sembunyikan panel lain
        mainMenuUI.SetActive(false);
        selectModeUI.SetActive(false);
        settingsUI.SetActive(false);
        startGameUI.SetActive(true);

        // 8) Tampilkan TextMeshPro UHI & Hotspot
        if (uhiIntensityText != null)       uhiIntensityText.SetActive(true);
        if (hotspotRemainingText != null)   hotspotRemainingText.SetActive(true);
        Debug.Log("[MainMenuNavigation] startGameUI aktif. Menampilkan UHI & Hotspot teks.");

        // 9) Beri tahu MainGameController bahwa game siap dimulai
        OnGameReady?.Invoke();

        // 10) Aktifkan footstep di MainGameController
        if (gameController != null)
        {
            gameController.EnableFootstep(true);
            Debug.Log("[MainMenuNavigation] Footstep diaktifkan dari MainMenuNavigation.");
        }
    }

    private float EaseInOutQuad(float t)
    {
        if (t < 0.5f) return 2f * t * t;
        return -1f + (4f - 2f * t) * t;
    }

    private IEnumerator SmoothTransition(Transform target, System.Action onComplete)
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