using UnityEngine;
using TMPro;
using System.Collections;

public class PrologController : MonoBehaviour
{
    [Header("Prolog UI Root")]
    public GameObject prologUIRoot;

    [Header("Core References")]
    [Tooltip("Referensi ke XR Rig atau Player Controller yang akan digerakkan.")]
    public Transform playerRig; // Menggantikan referensi ke kamera

    [Header("Prolog Camera Points (Target untuk Player Rig)")]
    public Transform prologPoint1;
    public Transform prologPoint2;
    public Transform prologPoint3;
    public Transform prologPoint4;
    public Transform prologPoint5;
    public Transform prologPoint6;

    [Header("References to UI Elements")]
    public CanvasGroup fadePanel;
    public GameObject prologTitleText;       // "PROLOG" label
    public TextMeshProUGUI subtitleTMP;      // subtitles / VO text

    [Header("Audio Reference")]
    public AudioManager audioManager;

    [Header("Cam Move Settings")]
    public float moveDuration = 5f;
    public float moveDuration2 = 5f;
    public float moveDuration3 = 5f;
    public float fadeDuration = 1f;
    public float blinkDuration = 0.2f;

    [Header("Subtitle Texts")]
    [TextArea(2, 4)]
    public string subtitleLine1 = "...";
    [TextArea(2, 4)]
    public string subtitleLine2 = "...";
    [TextArea(2, 4)]
    public string subtitleLine3 = "...";

    public event System.Action OnPrologComplete;

    private Coroutine prologCoroutine;
    private Camera mainCamera; // Tambahkan referensi kamera
    private bool isPrologActive = false; // Flag untuk mengontrol UI updates

    void Awake()
    {
        // Cache kamera utama
        mainCamera = Camera.main;

        // 1) Hide the prolog UI root at start
        if (prologUIRoot != null) prologUIRoot.SetActive(false);
        else Debug.LogError("[PrologController] prologUIRoot was not assigned!");

        // 2) Validasi referensi Player Rig. Ini harus di-assign dari MainMenuNavigation.
        if (playerRig == null)
        {
            Debug.LogError("[PrologController] Player Rig reference was not assigned! Prolog cannot move the player.");
        }

        // 3) Prepare fade panel
        if (fadePanel != null) fadePanel.alpha = 0f;
        else Debug.LogError("[PrologController] fadePanel was not assigned!");

        // 4) Hide the "PROLOG" title text
        if (prologTitleText != null) prologTitleText.SetActive(false);
        else Debug.LogError("[PrologController] prologTitleText was not assigned!");

        // 5) Clear subtitles at start
        if (subtitleTMP != null) subtitleTMP.text = "";
        else Debug.LogError("[PrologController] subtitleTMP was not assigned!");

        // 6) Validate AudioManager reference
        if (audioManager == null && AudioManager.Instance == null)
            Debug.LogError("[PrologController] AudioManager reference was not assigned and no Instance found!");

        // 7) Ensure we have all six prolog points
        if (prologPoint1 == null || prologPoint2 == null ||
            prologPoint3 == null || prologPoint4 == null ||
            prologPoint5 == null || prologPoint6 == null)
        {
            Debug.LogError("[PrologController] All prologPoint transforms (1–6) must be assigned!");
        }
    }

    public void StartPrologSequence()
    {
        // 1) Show the prolog UI root
        if (prologUIRoot != null) prologUIRoot.SetActive(true);

        // 2) If there is already a running coroutine, stop it
        if (prologCoroutine != null) StopCoroutine(prologCoroutine);

        // 3) Kick off the prolog sequence
        prologCoroutine = StartCoroutine(PrologSequenceCoroutine());
    }

    private void LateUpdate()
    {
        // Hanya update UI positioning jika prolog aktif dan tidak sedang bergerak
        if (isPrologActive && prologUIRoot != null && prologUIRoot.activeInHierarchy && mainCamera != null)
        {
            // Jarak dan skala bisa disesuaikan sesuai kebutuhan
            float distance = 2.0f; 
            prologUIRoot.transform.position = mainCamera.transform.position + mainCamera.transform.forward * distance;
            prologUIRoot.transform.rotation = Quaternion.LookRotation(prologUIRoot.transform.position - mainCamera.transform.position);
        }
    }

    private IEnumerator PrologSequenceCoroutine()
    {
        isPrologActive = true;
        
        // Notify UIManager bahwa prolog sedang aktif
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetPrologActive(true);
        }
        
        // Hentikan sementara proses auto-start yang berat
        if (GameController.Instance != null)
        {
            GameController.Instance.autoStartGameProcesses = false;
            Debug.Log("[PrologController] Temporarily disabled auto-start processes during prolog");
        }

        // ————————————————————————————————
        // STEP 1: AudioManager already started prolog BGM when MainMenuNavigation called OnPrologStarted()
        Debug.Log("[PrologController] Prolog BGM should already be playing via AudioManager.");

        // ————————————————————————————————
        // STEP 2: Fade-in fadePanel (0 → 1)
        yield return StartCoroutine(FadeCanvas(0f, 1f, fadeDuration));

        // ————————————————————————————————
        // STEP 3: Teleport Player Rig ke prologPoint1, tunggu 1 detik dengan background hitam
        if(playerRig == null) yield break; // Safety check
        
        // Disable CharacterController temporarily untuk movement yang lebih smooth
        CharacterController cc = playerRig.GetComponent<CharacterController>();
        bool wasControllerEnabled = false;
        if (cc != null)
        {
            wasControllerEnabled = cc.enabled;
            cc.enabled = false;
        }
        
        playerRig.position = prologPoint1.position;
        playerRig.rotation = prologPoint1.rotation;
        yield return new WaitForSeconds(1f);

        // ————————————————————————————————
        // STEP 4: Show "PROLOG" title, start voice over, dan langsung mulai animasi camera
        prologTitleText.SetActive(true);
        
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayVoiceOver(0); // VO1
        }
        else if (audioManager != null)
        {
            audioManager.PlayVoiceOver(0); // VO1
        }
        subtitleTMP.text = subtitleLine1;

        // ————————————————————————————————
        // STEP 5: Langsung mulai move Player Rig to prologPoint2 sambil fade out dan voice over
        StartCoroutine(FadeCanvas(1f, 0f, fadeDuration)); // Fade out bersamaan
        yield return StartCoroutine(MoveRigSmooth(prologPoint2, moveDuration)); // Move Player Rig dengan smoothing

        // ————————————————————————————————
        // STEP 6: Hide tulisan "PROLOG" setelah camera movement selesai
        prologTitleText.SetActive(false);
        
        // Wait for voice-over to finish
        if (AudioManager.Instance != null)
        {
            yield return new WaitUntil(() => !AudioManager.Instance.voiceOverSource.isPlaying);
        }
        else if (audioManager != null)
        {
            yield return new WaitUntil(() => !audioManager.voiceOverSource.isPlaying);
        }
        subtitleTMP.text = "";

        // ————————————————————————————————
        // STEP 11: Blink (quick fade in/out)
        yield return StartCoroutine(FadeCanvas(0f, 1f, blinkDuration));
        yield return StartCoroutine(FadeCanvas(1f, 0f, blinkDuration));

        // ————————————————————————————————
        // STEP 12: Teleport → Move Player Rig to prologPoint3, then to prologPoint4
        if(playerRig == null) yield break;
        playerRig.position = prologPoint3.position;
        playerRig.rotation = prologPoint3.rotation;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayVoiceOver(1); // VO2
        }
        else if (audioManager != null)
        {
            audioManager.PlayVoiceOver(1); // VO2
        }
        subtitleTMP.text = subtitleLine2;

        yield return StartCoroutine(MoveRigSmooth(prologPoint4, moveDuration2));
        
        // Wait for voice-over to finish
        if (AudioManager.Instance != null)
        {
            yield return new WaitUntil(() => !AudioManager.Instance.voiceOverSource.isPlaying);
        }
        else if (audioManager != null)
        {
            yield return new WaitUntil(() => !audioManager.voiceOverSource.isPlaying);
        }
        subtitleTMP.text = "";

        // ————————————————————————————————
        // STEP 13: Teleport → Move Player Rig to prologPoint5, then to prologPoint6
        if(playerRig == null) yield break;
        playerRig.position = prologPoint5.position;
        playerRig.rotation = prologPoint5.rotation;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayVoiceOver(2); // VO3
        }
        else if (audioManager != null)
        {
            audioManager.PlayVoiceOver(2); // VO3
        }
        subtitleTMP.text = subtitleLine3;

        yield return StartCoroutine(MoveRigSmooth(prologPoint6, moveDuration3));
        
        // Wait for voice-over to finish
        if (AudioManager.Instance != null)
        {
            yield return new WaitUntil(() => !AudioManager.Instance.voiceOverSource.isPlaying);
        }
        else if (audioManager != null)
        {
            yield return new WaitUntil(() => !audioManager.voiceOverSource.isPlaying);
        }
        subtitleTMP.text = "";

        // Re-enable CharacterController jika sebelumnya aktif
        if (cc != null && wasControllerEnabled)
        {
            cc.enabled = true;
        }

        // ————————————————————————————————
        // STEP 14: Stop prolog audio via AudioManager, then fire the OnPrologComplete event
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnPrologEnded();
        }
        else if (audioManager != null)
        {
            audioManager.OnPrologEnded();
        }
        
        isPrologActive = false;
        
        // Notify UIManager bahwa prolog selesai
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetPrologActive(false);
        }
        
        // Re-enable auto-start processes setelah prolog selesai
        if (GameController.Instance != null)
        {
            GameController.Instance.autoStartGameProcesses = true;
            Debug.Log("[PrologController] Re-enabled auto-start processes after prolog");
        }
        
        Debug.Log("[PrologController] Prolog finished → invoking OnPrologComplete().");
        OnPrologComplete?.Invoke();
    }

    private IEnumerator FadeCanvas(float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            fadePanel.alpha = Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        fadePanel.alpha = toAlpha;
    }

    private IEnumerator MoveRigSmooth(Transform target, float dur)
    {
        if (playerRig == null) yield break;

        Vector3 startPos = playerRig.position;
        Quaternion startRot = playerRig.rotation;
        Vector3 endPos = target.position;
        Quaternion endRot = target.rotation;

        float elapsed = 0f;
        while (elapsed < dur)
        {
            float t = elapsed / dur;
            // Gunakan smoothstep untuk pergerakan yang lebih halus
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            
            playerRig.position = Vector3.Lerp(startPos, endPos, smoothT);
            playerRig.rotation = Quaternion.Slerp(startRot, endRot, smoothT);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        playerRig.position = endPos;
        playerRig.rotation = endRot;
        
        // Tambahan delay kecil untuk stabilitas
        yield return new WaitForSeconds(0.1f);
    }

    // Backward compatibility - keep the old method name
    private IEnumerator MoveRig(Transform target, float dur)
    {
        return MoveRigSmooth(target, dur);
    }
}
