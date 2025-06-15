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

    [Header("Audio Reference")]
    [Tooltip("Script AudioManager untuk mengontrol audio.")]
    public AudioManager audioManager;  // Drag GameObject yang punya AudioManager di Inspector

    [Header("UI Panels")]
    public GameObject mainMenuUI;
    public GameObject selectModeUI;
    public GameObject settingsUI;
    public GameObject startGameUI;

    [Header("Prolog Reference")]
    public PrologController prologController; 

         

   

    [Header("Transition Settings")]
    public float transitionDuration = 1f;

    public static event System.Action OnGameReady;


    private Camera mainCamera;                
    private Vector3 cameraLocalOffset;        
    private Transform cameraOriginalParent;   

    void Awake()
    {
        // 1) Matikan footstep di AudioManager selama di Main Menu/Prolog
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.EnableFootstep(false);
            Debug.Log("[MainMenuNavigation] Footstep dinonaktifkan di Main Menu/Prolog.");
        }
        else if (audioManager != null)
        {
            audioManager.EnableFootstep(false);
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



        // 5) Posisikan XR-Rig di mainMenuPoint & freeze physics
        if (playerRig != null && mainMenuPoint != null)
        {
            // Nonaktifkan CharacterController untuk mencegah jatuh di menu.
            // Ini adalah perbaikan utama karena rig menggunakan CharacterController, bukan Rigidbody.
            CharacterController cc = playerRig.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                Debug.Log("[MainMenuNavigation] CharacterController dinonaktifkan untuk mencegah jatuh di menu.");
            }

            // Logika Rigidbody dipertahankan untuk fleksibilitas jika suatu saat rig diubah.
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



        // 8) Start main menu music through AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnMainMenuEntered();
            Debug.Log("[MainMenuNavigation] Main menu music started via AudioManager.");
        }
        else if (audioManager != null)
        {
            audioManager.OnMainMenuEntered();
            Debug.Log("[MainMenuNavigation] Main menu music started via AudioManager.");
        }

        // 9) Subscribe ke Prolog completion
        if (prologController != null)
        {
            prologController.OnPrologComplete += OnPrologFinished;
            // Berikan referensi playerRig ke PrologController
            prologController.playerRig = this.playerRig;
            Debug.Log("[MainMenuNavigation] Subscribed to OnPrologComplete() and passed playerRig reference.");
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

    #region Audio Management

    public void PlayClickSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClickSound();
        }
        else if (audioManager != null)
        {
            audioManager.PlayClickSound();
        }
    }

    #endregion

    #region Menu Navigation

    public void GoToSelectMode()
    {
        PlayClickSound();
        Debug.Log("[MainMenuNavigation] GoToSelectMode()");
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

        // Notify AudioManager about prolog start (will stop menu music automatically)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnPrologStarted();
        }
        else if (audioManager != null)
        {
            audioManager.OnPrologStarted();
        }

        // Camera TIDAK LAGI di-detach dari rig. Ini adalah perubahan krusial untuk XR.
        // PrologController sekarang akan menggerakkan seluruh rig, bukan hanya kamera.
        // if (mainCamera != null)
        // {
        //     mainCamera.transform.parent = null;
        //     Debug.Log("[MainMenuNavigation] Camera detached from XR-Rig for prolog.");
        // }

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
            float t = elapsed / transitionDuration; // Linear instead of ease

            mainCamera.transform.position = Vector3.Lerp(camStartPos, camEndPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(camStartRot, camEndRot, t);
            playerRig.position = Vector3.Lerp(rigStartPos, rigEndPos, t);
            playerRig.rotation = Quaternion.Slerp(rigStartRot, rigEndRot, t);

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

        // 6) Aktifkan kembali CharacterController dan/atau fisika untuk gameplay.
        CharacterController cc = playerRig.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = true;
            Debug.Log("[MainMenuNavigation] CharacterController diaktifkan kembali untuk memulai game.");
        }
        
        Rigidbody rb = playerRig.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            Debug.Log("[MainMenuNavigation] Fisika Rigidbody diaktifkan kembali.");
        }

        // 7) Tampilkan UI Start Game & sembunyikan panel lain
        mainMenuUI.SetActive(false);
        selectModeUI.SetActive(false);
        settingsUI.SetActive(false);
        startGameUI.SetActive(true);

        // 8) Notify UIManager to show game UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.TransitionToGameMode();
            Debug.Log("[MainMenuNavigation] Game UI transitioned via UIManager.");
        }

        // 9) Beri tahu MainGameController bahwa game siap dimulai
        OnGameReady?.Invoke();

        // 10) Notify AudioManager that we're entering game mode
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnGameModeEntered();
            Debug.Log("[MainMenuNavigation] Game mode entered, audio switched.");
        }
        else if (audioManager != null)
        {
            audioManager.OnGameModeEntered();
            Debug.Log("[MainMenuNavigation] Game mode entered, audio switched.");
        }
        
        // 11) Notify GameController that we're in game mode
        if (GameController.Instance != null)
        {
            GameController.Instance.TransitionToGameMode();
            Debug.Log("[MainMenuNavigation] GameController notified of game mode transition.");
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
            float t = elapsed / transitionDuration; // Linear instead of ease
            playerRig.position = Vector3.Lerp(startPos, endPos, t);
            playerRig.rotation = Quaternion.Slerp(startRot, endRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        playerRig.position = endPos;
        playerRig.rotation = endRot;
        onComplete?.Invoke();
    }
}