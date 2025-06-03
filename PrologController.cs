using UnityEngine;
using TMPro;
using System.Collections;

public class PrologController : MonoBehaviour
{
    [Header("Prolog UI Root")]
    public GameObject prologUIRoot;

    [Header("Prolog Camera Points (only for Camera.main)")]
    public Transform prologPoint1;
    public Transform prologPoint2;
    public Transform prologPoint3;
    public Transform prologPoint4;
    public Transform prologPoint5;
    public Transform prologPoint6;

    [Header("References to UI Elements")]
    public CanvasGroup fadePanel;
    public GameObject prologTitleText;       // “PROLOG” label
    public TextMeshProUGUI subtitleTMP;      // subtitles / VO text

    [Header("Audio for Voice-Over")]
    public AudioSource voiceAudioSource;
    public AudioClip voiceOver1;
    public AudioClip voiceOver2;
    public AudioClip voiceOver3;

    [Header("Audio for Prolog BGM")]
    public AudioSource prologBgmSource;
    public AudioClip prologBgmClip;

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

    private Transform mainCameraTransform;
    private Coroutine prologCoroutine;

    void Awake()
    {
        // 1) Hide the prolog UI root at start
        if (prologUIRoot != null) prologUIRoot.SetActive(false);
        else Debug.LogError("[PrologController] prologUIRoot was not assigned!");

        // 2) Cache Camera.main's Transform (Prolog moves only the camera, not the XR rig)
        Camera mainCam = Camera.main;
        if (mainCam != null) mainCameraTransform = mainCam.transform;
        else Debug.LogError("[PrologController] MainCamera was not found!");

        // 3) Prepare fade panel
        if (fadePanel != null) fadePanel.alpha = 0f;
        else Debug.LogError("[PrologController] fadePanel was not assigned!");

        // 4) Hide the “PROLOG” title text
        if (prologTitleText != null) prologTitleText.SetActive(false);
        else Debug.LogError("[PrologController] prologTitleText was not assigned!");

        // 5) Clear subtitles at start
        if (subtitleTMP != null) subtitleTMP.text = "";
        else Debug.LogError("[PrologController] subtitleTMP was not assigned!");

        // 6) Validate all audio fields
        if (voiceAudioSource == null) Debug.LogError("[PrologController] voiceAudioSource was not assigned!");
        if (voiceOver1 == null || voiceOver2 == null || voiceOver3 == null)
            Debug.LogError("[PrologController] One (or more) voiceOver clips were not assigned!");
        if (prologBgmSource == null) Debug.LogError("[PrologController] prologBgmSource was not assigned!");
        if (prologBgmClip == null) Debug.LogError("[PrologController] prologBgmClip was not assigned!");

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

    private IEnumerator PrologSequenceCoroutine()
    {
        // ————————————————————————————————
        // STEP 1: Start looping BGM for prolog, if assigned
        if (prologBgmSource != null && prologBgmClip != null)
        {
            prologBgmSource.clip = prologBgmClip;
            prologBgmSource.loop = true;
            prologBgmSource.Play();
            Debug.Log("[PrologController] Prolog BGM started.");
        }

        // ————————————————————————————————
        // STEP 2: Fade-in fadePanel (0 → 1)
        yield return StartCoroutine(FadeCanvas(0f, 1f, fadeDuration));

        // ————————————————————————————————
        // STEP 3: Teleport camera to prologPoint1, show “PROLOG” title briefly
        mainCameraTransform.position = prologPoint1.position;
        mainCameraTransform.rotation = prologPoint1.rotation;
        prologTitleText.SetActive(true);
        yield return new WaitForSeconds(1f);
        prologTitleText.SetActive(false);

        // ————————————————————————————————
        // STEP 4: Fade-out fadePanel (1 → 0)
        yield return StartCoroutine(FadeCanvas(1f, 0f, fadeDuration));

        // ————————————————————————————————
        // STEP 5: Move camera to prologPoint2 while playing voiceOver1 + subtitleLine1
        voiceAudioSource.clip = voiceOver1;
        voiceAudioSource.Play();
        subtitleTMP.text = subtitleLine1;

        yield return StartCoroutine(MoveCamera(prologPoint2, moveDuration));
        yield return new WaitUntil(() => !voiceAudioSource.isPlaying);
        subtitleTMP.text = "";

        // ————————————————————————————————
        // STEP 6: Blink (quick fade in/out)
        yield return StartCoroutine(FadeCanvas(0f, 1f, blinkDuration));
        yield return StartCoroutine(FadeCanvas(1f, 0f, blinkDuration));

        // ————————————————————————————————
        // STEP 7: Teleport → Move camera to prologPoint3, then to prologPoint4 while playing voiceOver2 + subtitleLine2
        mainCameraTransform.position = prologPoint3.position;
        mainCameraTransform.rotation = prologPoint3.rotation;

        voiceAudioSource.clip = voiceOver2;
        voiceAudioSource.Play();
        subtitleTMP.text = subtitleLine2;

        yield return StartCoroutine(MoveCamera(prologPoint4, moveDuration2));
        yield return new WaitUntil(() => !voiceAudioSource.isPlaying);
        subtitleTMP.text = "";

        // ————————————————————————————————
        // STEP 8: Teleport → Move camera to prologPoint5, then to prologPoint6 while playing voiceOver3 + subtitleLine3
        mainCameraTransform.position = prologPoint5.position;
        mainCameraTransform.rotation = prologPoint5.rotation;

        voiceAudioSource.clip = voiceOver3;
        voiceAudioSource.Play();
        subtitleTMP.text = subtitleLine3;

        yield return StartCoroutine(MoveCamera(prologPoint6, moveDuration3));
        yield return new WaitUntil(() => !voiceAudioSource.isPlaying);
        subtitleTMP.text = "";

        // ————————————————————————————————
        // STEP 9: Stop the prolog BGM, then fire the OnPrologComplete event
        if (prologBgmSource.isPlaying) prologBgmSource.Stop();
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

    private IEnumerator MoveCamera(Transform target, float dur)
    {
        Vector3 startPos = mainCameraTransform.position;
        Quaternion startRot = mainCameraTransform.rotation;
        Vector3 endPos = target.position;
        Quaternion endRot = target.rotation;

        float elapsed = 0f;
        while (elapsed < dur)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            mainCameraTransform.position = Vector3.Lerp(startPos, endPos, t);
            mainCameraTransform.rotation = Quaternion.Slerp(startRot, endRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        mainCameraTransform.position = endPos;
        mainCameraTransform.rotation = endRot;
    }
}
