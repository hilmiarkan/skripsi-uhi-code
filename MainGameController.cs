// MainGameController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class MainGameController : MonoBehaviour
{
    [Header("In-Game BGM Settings")]
    [Tooltip("AudioSource untuk memutar in-game background music.")]
    public AudioSource gameBgAudioSource;

    [Tooltip("5 AudioClips untuk in-game background music.")]
    public AudioClip[] gameBgClips;  // Harus tepat 5

    [Header("Footstep Settings")]
    [Tooltip("Transform XR-Rig (player).")]
    public Transform playerRig;

    [Tooltip("AudioSource yang akan memutar suara footstep.")]
    public AudioSource footstepSource;

    [Tooltip("List AudioClip footstep (boleh satu atau lebih).")]
    public AudioClip[] footstepClips;

    [Tooltip("Terrain yang valid untuk memicu footstep.")]
    public Terrain footstepTerrain;

    [Tooltip("Jarak maksimal (meter) untuk raycast ke bawah.")]
    public float groundCheckDistance = 1.1f;

    [Tooltip("Kecepatan minimal (m/s) agar dianggap berjalan.")]
    public float moveThreshold = 0.1f;

    // Apakah sesi game di‐enable?
    private bool isGameStarted = false;

    // Untuk shuffle dan loop BGM
    private List<int> trackIndices = new List<int> { 0, 1, 2, 3, 4 };

    // Untuk deteksi movement
    private Vector3 lastPosition;
    private bool wasMoving = false;

    // Untuk enable/disable footstep dari luar (MainMenuNavigation)
    private bool footstepEnabled = false;

    void OnEnable()
    {
        MainMenuNavigation.OnGameReady += BeginGame;
    }

    void OnDisable()
    {
        MainMenuNavigation.OnGameReady -= BeginGame;
    }

    void Start()
    {
        // Validasi BGM source dan clips
        if (gameBgAudioSource == null)
            Debug.LogError("[MainGameController] gameBgAudioSource not assigned!");
        if (gameBgClips == null || gameBgClips.Length != 5)
            Debug.LogError("[MainGameController] Please assign exactly 5 gameBgClips!");

        // Validasi playerRig dan footstepSource
        if (playerRig == null)
            Debug.LogError("[MainGameController] playerRig not assigned!");
        if (footstepSource == null)
            Debug.LogError("[MainGameController] footstepSource not assigned!");
        if (footstepClips == null || footstepClips.Length == 0)
            Debug.LogWarning("[MainGameController] No footstepClips assigned. Footstep akan silent.");

        if (footstepTerrain == null)
            Debug.LogWarning("[MainGameController] footstepTerrain not assigned. Footstep tidak akan trigger.");

        // Pastikan footstepSource tidak playOnAwake, dan looping false
        if (footstepSource != null)
        {
            footstepSource.playOnAwake = false;
            footstepSource.loop = false;
        }
    }

    /// <summary>
    /// Method publik yang dipanggil MainMenuNavigation untuk meng‐enable atau disable footstep.
    /// </summary>
    public void EnableFootstep(bool enable)
    {
        footstepEnabled = enable;
        if (!enable && footstepSource != null)
        {
            // Jika disable, pastikan clip footstep berhenti
            footstepSource.loop = false;
            footstepSource.Stop();
        }
    }

    private void BeginGame()
    {
        // Dipanggil saat Prolog selesai dan transisi ke StartGame sudah done
        isGameStarted = true;
        lastPosition = playerRig.position;
        wasMoving = false;

        // Mulai BGM loop
        if (gameBgAudioSource != null && gameBgClips != null && gameBgClips.Length == 5)
        {
            StartCoroutine(PlayGameBgMusicLoop());
        }
        else
        {
            Debug.LogWarning("[MainGameController] Unable to start BGM loop: check assignments.");
        }

        // Pastikan footstepSource siap dan tidak play di awal
        if (footstepSource != null)
        {
            footstepSource.Stop();
            footstepSource.loop = false;
        }
    }

    void Update()
    {
        if (!isGameStarted || !footstepEnabled)
            return;

        // 1) Raycast ke bawah untuk cek apakah di atas footstepTerrain
        bool isOnAssignedTerrain = false;
        RaycastHit hit;
        Vector3 origin = playerRig.position + Vector3.up * 0.1f;

        if (Physics.Raycast(origin, Vector3.down, out hit, groundCheckDistance))
        {
            Terrain hitTerrain = hit.collider.GetComponent<Terrain>();
            if (hitTerrain == footstepTerrain)
            {
                isOnAssignedTerrain = true;
            }
        }

        if (!isOnAssignedTerrain)
        {
            // Jika tidak di terrain → hentikan footstep (jika sedang berjalan)
            if (wasMoving)
            {
                StopFootstepLoop();
                wasMoving = false;
            }
            lastPosition = playerRig.position;
            return;
        }

        // 2) Hitung kecepatan (m/s)
        float deltaDist = Vector3.Distance(playerRig.position, lastPosition);
        float deltaTime = Time.deltaTime;
        float speed = (deltaTime > 0f) ? (deltaDist / deltaTime) : 0f;

        // 3) Jika di terrain & speed >= threshold → mulai berjalan
        if (speed >= moveThreshold)
        {
            if (!wasMoving)
            {
                StartFootstepLoop();
                wasMoving = true;
            }
            // Jika sudah wasMoving = true, biarkan footstep looping
        }
        else
        {
            // Jika speed < threshold → hentikan footstep
            if (wasMoving)
            {
                StopFootstepLoop();
                wasMoving = false;
            }
        }

        lastPosition = playerRig.position;
    }

    private void StartFootstepLoop()
    {
        if (footstepSource == null || footstepClips == null || footstepClips.Length == 0)
            return;

        if (!footstepSource.isActiveAndEnabled)
            return;

        int idx = (footstepClips.Length == 1) ? 0 : Random.Range(0, footstepClips.Length);
        footstepSource.clip = footstepClips[idx];
        footstepSource.loop = true;
        footstepSource.Play();
    }

    private void StopFootstepLoop()
    {
        if (footstepSource == null)
            return;

        footstepSource.loop = false;
        footstepSource.Stop();
    }

    private IEnumerator PlayGameBgMusicLoop()
    {
        while (true)
        {
            // Fisher–Yates shuffle
            for (int i = trackIndices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = trackIndices[i];
                trackIndices[i] = trackIndices[j];
                trackIndices[j] = tmp;
            }

            foreach (int idx in trackIndices)
            {
                gameBgAudioSource.clip = gameBgClips[idx];
                gameBgAudioSource.Play();
                yield return new WaitForSeconds(gameBgAudioSource.clip.length);
            }
            // Setelah semua lima dimainkan, loop lagi
        }
    }
}
