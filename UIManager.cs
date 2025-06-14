using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Progress UI")]
    public TextMeshProUGUI progressText;
    
    [Header("Game UI")]
    public GameObject mainInterfacesCanvas;
    public TextMeshProUGUI currentTimeText;
    public TextMeshProUGUI uhiIntensityText;

    [Header("Settings")]
    public bool showGameUI = false;
    public float distanceFromCamera = 2f;

    public static UIManager Instance { get; private set; }

    private TimeZoneInfo wibTimeZone;
    private Camera mainCamera;
    private Coroutine timeDisplayCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeUI();
            mainCamera = Camera.main;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeUI()
    {
        // Initialize timezone
        try
        {
            wibTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                "SE Asia Standard Time"
            #else
                "Asia/Jakarta"
            #endif
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UIManager] Failed to find timezone: {ex.Message}");
            wibTimeZone = null;
        }

        // Start time update coroutine
        timeDisplayCoroutine = StartCoroutine(UpdateTimeDisplay());

        // Initialize displays
        UpdateProgressText("Initializing...");
        UpdateUHIIntensity(0f);
    }

    private void LateUpdate()
    {
        // Make World Space UI follow camera
        if (mainInterfacesCanvas != null && mainCamera != null)
        {
            if (showGameUI)
            {
                mainInterfacesCanvas.SetActive(true);
                // Position in front of camera
                mainInterfacesCanvas.transform.position = mainCamera.transform.position + mainCamera.transform.forward * distanceFromCamera;
                // Face camera
                mainInterfacesCanvas.transform.rotation = Quaternion.LookRotation(mainInterfacesCanvas.transform.position - mainCamera.transform.position);
            }
            else
            {
                mainInterfacesCanvas.SetActive(false);
            }
        }
    }

    private IEnumerator UpdateTimeDisplay()
    {
        while (true)
        {
            if (currentTimeText != null && showGameUI && wibTimeZone != null)
            {
                DateTime wibTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, wibTimeZone);
                currentTimeText.text = wibTime.ToString("HH:mm:ss WIB\ndd MMM yyyy");
            }
            yield return new WaitForSeconds(1f);
        }
    }

    public void UpdateUHIIntensity(float intensity)
    {
        if (uhiIntensityText != null)
        {
            uhiIntensityText.text = $"UHI Intensity: {intensity:F1}°C";
        }
    }

    public void UpdateProgressText(string text)
    {
        if (progressText != null)
        {
            progressText.text = text;
            Debug.Log($"[UIManager] Progress: {text}");
        }
    }

    public void TransitionToGameMode()
    {
        showGameUI = true;
        Debug.Log("[UIManager] Transitioned to game mode");
    }

    public void TransitionToMainMenu()
    {
        showGameUI = false;
        Debug.Log("[UIManager] Transitioned to main menu");
    }
} 