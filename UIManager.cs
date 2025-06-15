using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class UIManager : MonoBehaviour
{
    [Header("Progress UI")]
    public TextMeshProUGUI progressText;
    
    [Header("Game UI")]
    [Tooltip("Parent GameObject dari canvas UI utama (yang diatur ke World Space)")]
    public GameObject mainInterfacesCanvas;
    public TextMeshProUGUI currentTimeText;
    public TextMeshProUGUI uhiIntensityText;
    public TextMeshProUGUI hotspotMitigationText;

    [Header("Error UI")]
    public GameObject errorPopup;
    public TextMeshProUGUI errorMessageText;
    public Button tryAgainButton;
    public Button usePrefetchedButton;

    [Header("Heatmap UI")]
    public Button showFuzzyHeatmapButton;
    public Button showRawHeatmapButton;

    [Header("Settings")]
    public bool showProgressInMainMenu = true;
    public bool showGameUI = false;
    [Tooltip("Jarak UI dari kamera dalam satuan meter untuk World Space Canvas")]
    public float distanceFromCamera = 2f;

    public static UIManager Instance { get; private set; }

    // Time tracking
    private readonly TimeZoneInfo wibTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
    #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            "SE Asia Standard Time"
    #else
            "Asia/Jakarta"
    #endif
    );

    // Progress tracking
    private DateTime fetchStartTime;
    private bool isFetching = false;
    private bool isFuzzying = false;

    // Camera and Time tracking
    private Camera mainCamera;

    // XR Input tracking
    private InputDevice leftController;
    private bool wasLeftTriggerPressed = false;
    private bool isPrologActive = false; // Flag untuk mendeteksi prolog

    // Events for error popup
    public System.Action OnTryAgainClicked;
    public System.Action OnUsePrefetchedClicked;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeUI();
            mainCamera = Camera.main; // Cache the main camera
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeUI()
    {
        // Hide error popup initially
        if (errorPopup != null)
            errorPopup.SetActive(false);

        // Setup button listeners
        if (tryAgainButton != null)
            tryAgainButton.onClick.AddListener(() => OnTryAgainClicked?.Invoke());
        if (usePrefetchedButton != null)
            usePrefetchedButton.onClick.AddListener(() => OnUsePrefetchedClicked?.Invoke());

        // Setup heatmap button listeners
        if (showFuzzyHeatmapButton != null)
            showFuzzyHeatmapButton.onClick.AddListener(ToggleFuzzyHeatmap);
        if (showRawHeatmapButton != null)
            showRawHeatmapButton.onClick.AddListener(ToggleRawHeatmap);

        // Start time update coroutine
        StartCoroutine(UpdateTimeDisplay());

        // Initialize text displays
        UpdateProgressText("Starting system...");
        UpdateUHIIntensity(0f);
        UpdateHotspotMitigation(0, 5);

        // Initialize heatmap buttons
        InitializeHeatmapButtons();
        
        // Update prefetched button text
        UpdatePrefetchedButtonText();
        
        // Clear progress text after a short delay to let auto-start processes take over
        StartCoroutine(ClearInitialProgressText());
    }

    private IEnumerator ClearInitialProgressText()
    {
        yield return new WaitForSeconds(5f); // Wait 5 seconds
        
        // Only clear if still showing initial text and no active processes
        if (progressText != null && 
            (progressText.text == "Starting system..." || progressText.text == "Initializing...") &&
            !isFetching && !isFuzzying)
        {
            progressText.text = "";
            Debug.Log("[UIManager] Cleared initial progress text");
        }
    }

    private void Update()
    {
        // Update XR controller references if needed
        if (!leftController.isValid)
        {
            leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        }

        // Check for left controller trigger press
        if (leftController.isValid)
        {
            bool isLeftTriggerPressed = false;
            if (leftController.TryGetFeatureValue(CommonUsages.triggerButton, out isLeftTriggerPressed))
            {
                // Detect trigger press (not held) - only trigger on press, not hold
                if (isLeftTriggerPressed && !wasLeftTriggerPressed)
                {
                    ToggleFuzzyHeatmap();
                    Debug.Log("[UIManager] Left controller trigger pressed - toggling fuzzy heatmap");
                }
                wasLeftTriggerPressed = isLeftTriggerPressed;
            }
        }

        // Update visibility based on game state
        // Progress text shows when there's active progress OR in main menu (if enabled) OR when there's text to show
        if (progressText != null)
        {
            bool hasActiveProgress = isFetching || isFuzzying;
            bool hasInitialText = progressText.text == "Starting system..." || progressText.text == "Initializing...";
            bool hasImportantText = !string.IsNullOrEmpty(progressText.text) && !hasInitialText;
            bool shouldShowProgress = hasActiveProgress || hasImportantText || (showProgressInMainMenu && !showGameUI);
            
            // Debug logging untuk troubleshooting
            if (hasInitialText && Time.frameCount % 300 == 0) // Log setiap 5 detik (60fps * 5)
            {
                Debug.Log($"[UIManager] Progress text showing initial text: '{progressText.text}' - hasActiveProgress: {hasActiveProgress}, hasImportantText: {hasImportantText}, showProgressInMainMenu: {showProgressInMainMenu}, showGameUI: {showGameUI}");
            }
            
            progressText.gameObject.SetActive(shouldShowProgress);
        }

        // Individual visibility is now handled by controlling the parent mainInterfacesCanvas in LateUpdate
        // if (currentTimeText != null)
        //     currentTimeText.gameObject.SetActive(showGameUI);
        // if (uhiIntensityText != null)
        //     uhiIntensityText.gameObject.SetActive(showGameUI);
        // if (hotspotMitigationText != null)
        //     hotspotMitigationText.gameObject.SetActive(showGameUI);

        // Handle keyboard shortcuts for heatmap toggles
        if (Input.GetKeyDown(KeyCode.N))
        {
            ToggleRawHeatmap(); // N key for Raw Heatmap
        }
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleFuzzyHeatmap(); // M key for Fuzzy Heatmap
        }
    }

    private void LateUpdate()
    {
        // Kurangi frekuensi update UI jika prolog sedang aktif
        if (isPrologActive && Time.frameCount % 3 != 0) // Update setiap 3 frame saja
            return;
            
        // This logic makes the World Space UI follow the camera
        if (mainInterfacesCanvas != null && mainCamera != null)
        {
            if (showGameUI)
            {
                mainInterfacesCanvas.SetActive(true);
                // Position the canvas in front of the camera
                mainInterfacesCanvas.transform.position = mainCamera.transform.position + mainCamera.transform.forward * distanceFromCamera;
                // Rotate the canvas to face the camera
                mainInterfacesCanvas.transform.rotation = Quaternion.LookRotation(mainInterfacesCanvas.transform.position - mainCamera.transform.position);
            }
            else
            {
                mainInterfacesCanvas.SetActive(false);
            }
        }
    }

    public void SetGameUIVisibility(bool visible)
    {
        showGameUI = visible;
    }

    public void SetProgressVisibility(bool visible)
    {
        showProgressInMainMenu = visible;
    }

    // Progress text management
    public void OnFetchStarted()
    {
        fetchStartTime = DateTime.UtcNow;
        isFetching = true;
        StartCoroutine(UpdateFetchProgress());
    }

    public void OnFetchCompleted()
    {
        isFetching = false;
        StopAllCoroutines();
        StartCoroutine(ShowCompletionMessage());
        StartCoroutine(UpdateTimeDisplay()); // Restart time display
    }

    public void OnFetchFailed(string errorMessage)
    {
        isFetching = false;
        ShowErrorPopup(errorMessage);
    }

    public void OnFuzzyStarted()
    {
        isFuzzying = true;
        StartCoroutine(DelayedFuzzyStart());
    }

    public void OnFuzzyProgress(int current, int total)
    {
        if (isFuzzying && progressText != null)
        {
            progressText.text = $"mengkalkulasi {current}/{total} titik";
        }
    }

    public void OnFuzzyCompleted()
    {
        isFuzzying = false;
        StartCoroutine(ShowFuzzyCompletion());
    }

    public void OnFuzzyFailed(string errorMessage)
    {
        isFuzzying = false;
        ShowErrorPopup($"Fuzzy calculation failed: {errorMessage}");
    }

    private IEnumerator UpdateFetchProgress()
    {
        while (isFetching)
        {
            TimeSpan elapsed = DateTime.UtcNow - fetchStartTime;
            if (progressText != null)
            {
                // Use TotalSeconds to get accurate decimal representation
                double totalSeconds = elapsed.TotalSeconds;
                int minutes = (int)(totalSeconds / 60);
                double remainingSeconds = totalSeconds % 60;
                
                progressText.text = $"fetching.. {minutes}m {remainingSeconds:F1}s";
            }
            yield return new WaitForSeconds(0.1f); // Update every 0.1 seconds for smooth decimal changes
        }
    }

    private IEnumerator ShowCompletionMessage()
    {
        if (progressText != null)
        {
            progressText.text = "Fetching complete!";
        }
        yield return new WaitForSeconds(3f);
    }

    private IEnumerator DelayedFuzzyStart()
    {
        yield return new WaitForSeconds(3f); // Wait 3 seconds after fetch completion
        // Fuzzy progress will be updated by OnFuzzyProgress calls
    }

    private IEnumerator ShowFuzzyCompletion()
    {
        if (progressText != null)
        {
            progressText.text = "Proses Selesai!";
        }
        yield return new WaitForSeconds(3f);
        
        // Clear progress text after completion (visibility will be handled by Update() method)
        if (progressText != null)
        {
            progressText.text = "";
        }
    }

    private IEnumerator UpdateTimeDisplay()
    {
        while (true)
        {
            if (currentTimeText != null && showGameUI)
            {
                DateTime wibTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, wibTimeZone);
                currentTimeText.text = wibTime.ToString("HH:mm:ss WIB\ndd MMM yyyy");
            }
            yield return new WaitForSeconds(1f);
        }
    }

    // UHI and Mitigation UI
    public void UpdateUHIIntensity(float intensity)
    {
        if (uhiIntensityText != null)
        {
            uhiIntensityText.text = $"UHI Intensity: {intensity:F1}°C";
        }
    }

    public void UpdateHotspotMitigation(int mitigated, int total)
    {
        if (hotspotMitigationText != null)
        {
            hotspotMitigationText.text = $"{mitigated}/{total} Hotspot termitigasi";
        }
    }

    // Error popup management
    public void ShowErrorPopup(string message)
    {
        if (errorPopup != null && errorMessageText != null)
        {
            errorMessageText.text = message;
            errorPopup.SetActive(true);

            // Position in front of camera if world-space
            var rect = errorPopup.GetComponent<RectTransform>();
            var canvas = errorPopup.GetComponentInParent<Canvas>();
            if (rect != null && canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    // Place 2 meters in front of camera
                    rect.position = cam.transform.position + cam.transform.forward * 100.0f;
                    // Face the camera
                    rect.rotation = Quaternion.LookRotation(rect.position - cam.transform.position);
                }
            }
        }
    }

    public void HideErrorPopup()
    {
        if (errorPopup != null)
        {
            errorPopup.SetActive(false);
        }
    }

    // Public method to update any custom text
    public void UpdateProgressText(string text)
    {
        if (progressText != null)
        {
            progressText.text = text;
        }
    }

    // Method to be called when transitioning from main menu to game
    public void TransitionToGameMode()
    {
        showProgressInMainMenu = false;
        showGameUI = true;
        isPrologActive = false; // Prolog selesai
        HideErrorPopup();
        // Note: Progress text will automatically show in game mode when there's active progress (isFetching/isFuzzying) or text to display
    }

    // Method to be called when returning to main menu
    public void TransitionToMainMenu()
    {
        showProgressInMainMenu = true;
        showGameUI = false;
        isPrologActive = false; // Reset prolog flag
        HideErrorPopup();
    }

    // Method untuk mengatur status prolog (dipanggil dari PrologController)
    public void SetPrologActive(bool active)
    {
        isPrologActive = active;
    }

    // Heatmap control methods
    private void ToggleFuzzyHeatmap()
    {
        if (HeatmapGenerator.Instance != null)
        {
            Debug.Log("[UIManager] Toggling Fuzzy Heatmap");
            HeatmapGenerator.Instance.ToggleFuzzyHeatmap();
            UpdateHeatmapButtonText();
        }
        else if (GameController.Instance != null && GameController.Instance.heatmapGenerator != null)
        {
            Debug.Log("[UIManager] Using GameController HeatmapGenerator for Fuzzy Heatmap");
            GameController.Instance.heatmapGenerator.ToggleFuzzyHeatmap();
            UpdateHeatmapButtonText();
        }
        else
        {
            Debug.LogWarning("[UIManager] HeatmapGenerator not found! Make sure HeatmapGenerator is assigned in GameController.");
        }
    }

    private void ToggleRawHeatmap()
    {
        if (HeatmapGenerator.Instance != null)
        {
            Debug.Log("[UIManager] Toggling Raw Heatmap");
            HeatmapGenerator.Instance.ToggleRawHeatmap();
            UpdateHeatmapButtonText();
        }
        else if (GameController.Instance != null && GameController.Instance.heatmapGenerator != null)
        {
            Debug.Log("[UIManager] Using GameController HeatmapGenerator for Raw Heatmap");
            GameController.Instance.heatmapGenerator.ToggleRawHeatmap();
            UpdateHeatmapButtonText();
        }
        else
        {
            Debug.LogWarning("[UIManager] HeatmapGenerator not found! Make sure HeatmapGenerator is assigned in GameController.");
        }
    }

    private void UpdateHeatmapButtonText()
    {
        HeatmapGenerator heatmapGen = null;
        
        if (HeatmapGenerator.Instance != null)
        {
            heatmapGen = HeatmapGenerator.Instance;
        }
        else if (GameController.Instance != null && GameController.Instance.heatmapGenerator != null)
        {
            heatmapGen = GameController.Instance.heatmapGenerator;
        }
        
        if (heatmapGen != null)
        {
            // Update fuzzy heatmap button
            if (showFuzzyHeatmapButton != null)
            {
                var btnText = showFuzzyHeatmapButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.text = heatmapGen.IsFuzzyHeatmapVisible 
                        ? "Hide Fuzzy Heatmap" 
                        : "Show Fuzzy Heatmap";
                }
            }

            // Update raw heatmap button  
            if (showRawHeatmapButton != null)
            {
                var btnText = showRawHeatmapButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.text = heatmapGen.IsRawHeatmapVisible 
                        ? "Hide Raw Heatmap" 
                        : "Show Raw Heatmap";
                }
            }
        }
    }

    // Initialize heatmap button text
    public void InitializeHeatmapButtons()
    {
        UpdateHeatmapButtonText();
    }

    // Update prefetched button text to be more clear
    public void UpdatePrefetchedButtonText()
    {
        if (usePrefetchedButton != null)
        {
            var btnText = usePrefetchedButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = "Use Previous Data";
            }
        }
    }
} 