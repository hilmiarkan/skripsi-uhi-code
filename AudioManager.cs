using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    [Header("BGM Settings")]
    public AudioSource bgAudioSource;
    public AudioClip[] mainMenuBgClips;
    public AudioClip[] gameBgClips;

    [Header("Prolog Audio Settings")]
    public AudioSource prologBgmSource;
    public AudioClip prologBgmClip;
    public AudioSource voiceOverSource;
    public AudioClip[] voiceOverClips; // Array for VO1, VO2, VO3

    [Header("Footstep Settings")]
    public AudioSource footstepSource;
    public AudioClip[] footstepClips;
    public Transform playerRig;
    public Terrain terrain;
    public float groundCheckDistance = 1.1f;
    public float moveThreshold = 0.1f;

    [Header("SFX Settings")]
    public AudioSource sfxSource;
    public AudioClip clickSound;

    public static AudioManager Instance { get; private set; }

    // Footstep tracking
    private Vector3 lastPosition;
    private bool wasMoving = false;
    private bool footstepEnabled = false;

    // BGM tracking
    private bool isPlayingMainMenuMusic = false;
    private bool isPlayingGameMusic = false;
    private Coroutine currentBgmCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudio();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeAudio()
    {
        // Setup audio sources
        if (footstepSource != null)
        {
            footstepSource.playOnAwake = false;
            footstepSource.loop = false;
        }

        if (bgAudioSource != null)
        {
            bgAudioSource.playOnAwake = false;
            bgAudioSource.loop = false;
        }

        if (prologBgmSource != null)
        {
            prologBgmSource.playOnAwake = false;
            prologBgmSource.loop = false;
        }

        if (voiceOverSource != null)
        {
            voiceOverSource.playOnAwake = false;
            voiceOverSource.loop = false;
        }

        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        // Initialize position tracking
        if (playerRig != null)
        {
            lastPosition = playerRig.position;
        }
    }

    private void Update()
    {
        // Handle footstep detection
        if (footstepEnabled && playerRig != null && terrain != null)
        {
            HandleFootstepDetection();
        }
    }

    private void HandleFootstepDetection()
    {
        // Check if player is on the assigned terrain
        bool isOnTerrain = false;
        RaycastHit hit;
        Vector3 origin = playerRig.position + Vector3.up * 0.1f;

        if (Physics.Raycast(origin, Vector3.down, out hit, groundCheckDistance))
        {
            Terrain hitTerrain = hit.collider.GetComponent<Terrain>();
            if (hitTerrain == terrain)
            {
                isOnTerrain = true;
            }
        }

        if (!isOnTerrain)
        {
            if (wasMoving)
            {
                StopFootstep();
                wasMoving = false;
            }
            lastPosition = playerRig.position;
            return;
        }

        // Calculate movement speed
        float distance = Vector3.Distance(playerRig.position, lastPosition);
        float deltaTime = Time.deltaTime;
        float speed = (deltaTime > 0f) ? (distance / deltaTime) : 0f;

        // Check if moving fast enough
        if (speed >= moveThreshold)
        {
            if (!wasMoving)
            {
                StartFootstep();
                wasMoving = true;
            }
        }
        else
        {
            if (wasMoving)
            {
                StopFootstep();
                wasMoving = false;
            }
        }

        lastPosition = playerRig.position;
    }

    private void StartFootstep()
    {
        if (footstepSource == null || footstepClips == null || footstepClips.Length == 0)
        {
            Debug.LogWarning("[AudioManager] Cannot start footstep: missing source or clips");
            return;
        }

        if (!footstepSource.isActiveAndEnabled)
        {
            Debug.LogWarning("[AudioManager] Footstep source is not active and enabled");
            return;
        }

        // Select random footstep clip
        int clipIndex = (footstepClips.Length == 1) ? 0 : Random.Range(0, footstepClips.Length);
        footstepSource.clip = footstepClips[clipIndex];
        footstepSource.loop = true;
        footstepSource.volume = 0.8f; // Ensure volume is audible
        footstepSource.Play();
        Debug.Log($"[AudioManager] Started footstep with clip: {footstepClips[clipIndex].name}");
    }

    private void StopFootstep()
    {
        if (footstepSource != null)
        {
            footstepSource.loop = false;
            footstepSource.Stop();
        }
    }

    // Public methods for footstep control
    public void EnableFootstep(bool enable)
    {
        footstepEnabled = enable;
        if (!enable)
        {
            StopFootstep();
            wasMoving = false;
        }
    }

    // BGM Control Methods
    public void StartMainMenuMusic()
    {
        if (isPlayingMainMenuMusic) return;

        StopAllMusic();
        isPlayingMainMenuMusic = true;
        
        if (mainMenuBgClips != null && mainMenuBgClips.Length > 0)
        {
            currentBgmCoroutine = StartCoroutine(PlayMainMenuMusicLoop());
        }
    }

    public void StartGameMusic()
    {
        if (isPlayingGameMusic) return;

        StopAllMusic();
        isPlayingGameMusic = true;
        
        if (gameBgClips != null && gameBgClips.Length > 0)
        {
            currentBgmCoroutine = StartCoroutine(PlayGameMusicLoop());
        }
    }

    public void StopAllMusic()
    {
        if (currentBgmCoroutine != null)
        {
            StopCoroutine(currentBgmCoroutine);
            currentBgmCoroutine = null;
        }

        if (bgAudioSource != null)
        {
            bgAudioSource.Stop();
        }

        if (prologBgmSource != null)
        {
            prologBgmSource.Stop();
        }

        isPlayingMainMenuMusic = false;
        isPlayingGameMusic = false;
    }

    private IEnumerator PlayMainMenuMusicLoop()
    {
        List<int> trackIndices = new List<int>();
        for (int i = 0; i < mainMenuBgClips.Length; i++)
        {
            trackIndices.Add(i);
        }

        while (isPlayingMainMenuMusic)
        {
            // Shuffle tracks
            for (int i = trackIndices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = trackIndices[i];
                trackIndices[i] = trackIndices[j];
                trackIndices[j] = temp;
            }

            foreach (int index in trackIndices)
            {
                if (!isPlayingMainMenuMusic) yield break;
                
                bgAudioSource.clip = mainMenuBgClips[index];
                bgAudioSource.Play();
                
                yield return new WaitForSeconds(bgAudioSource.clip.length);
            }
        }
    }

    private IEnumerator PlayGameMusicLoop()
    {
        List<int> trackIndices = new List<int>();
        for (int i = 0; i < gameBgClips.Length; i++)
        {
            trackIndices.Add(i);
        }

        while (isPlayingGameMusic)
        {
            // Shuffle tracks
            for (int i = trackIndices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = trackIndices[i];
                trackIndices[i] = trackIndices[j];
                trackIndices[j] = temp;
            }

            foreach (int index in trackIndices)
            {
                if (!isPlayingGameMusic) yield break;
                
                bgAudioSource.clip = gameBgClips[index];
                bgAudioSource.Play();
                
                yield return new WaitForSeconds(bgAudioSource.clip.length);
            }
        }
    }

    // SFX Methods
    public void PlayClickSound()
    {
        if (sfxSource != null && clickSound != null)
        {
            sfxSource.PlayOneShot(clickSound);
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    // Prolog Audio Methods
    public void PlayVoiceOver(int voIndex)
    {
        if (voiceOverSource != null && voiceOverClips != null && voIndex < voiceOverClips.Length)
        {
            if (voiceOverClips[voIndex] != null)
            {
                voiceOverSource.clip = voiceOverClips[voIndex];
                voiceOverSource.Play();
                Debug.Log($"[AudioManager] Playing VO{voIndex + 1}");
            }
        }
    }

    public void StopVoiceOver()
    {
        if (voiceOverSource != null)
        {
            voiceOverSource.Stop();
        }
    }

    public void OnPrologEnded()
    {
        if (prologBgmSource != null)
        {
            prologBgmSource.Stop();
        }
        StopVoiceOver();
        Debug.Log("[AudioManager] Prolog audio stopped");
    }

    // Public methods to be called by other systems
    public void OnGameModeEntered()
    {
        EnableFootstep(true);
        StartGameMusic();
    }

    public void OnMainMenuEntered()
    {
        EnableFootstep(false);
        StartMainMenuMusic();
    }

    public void OnPrologStarted()
    {
        StopAllMusic();
        EnableFootstep(false);
        
        if (prologBgmSource != null && prologBgmClip != null)
        {
            prologBgmSource.clip = prologBgmClip;
            prologBgmSource.loop = true;
            prologBgmSource.volume = 0.5f;
            prologBgmSource.Play();
            Debug.Log("[AudioManager] Prolog BGM started");
        }
    }

    private void OnDestroy()
    {
        StopAllMusic();
        StopFootstep();
    }
} 