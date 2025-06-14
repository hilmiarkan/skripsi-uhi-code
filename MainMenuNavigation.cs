// MainMenuNavigation.cs - Simplified version
using UnityEngine;
using System.Collections;

public class MainMenuNavigation : MonoBehaviour
{
    [Header("References")]
    public Transform playerRig;               
    public Transform mainMenuPoint;           
    public Transform startGamePoint;          

    [Header("Audio Reference")]
    public AudioManager audioManager;

    [Header("UI Panels")]
    public GameObject mainMenuUI;
    public GameObject startGameUI;

    [Header("Startup Settings")]
    [Tooltip("Skip main menu and go directly to game")]
    public bool skipMainMenuOnStart = false;

    public static event System.Action OnGameReady;

    private Camera mainCamera;                
    private Vector3 cameraLocalOffset;        

    void Awake()
    {
        // Cache Camera.main dan local offset
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraLocalOffset = mainCamera.transform.localPosition;
        }

        // Posisikan XR-Rig di mainMenuPoint
        if (playerRig != null && mainMenuPoint != null)
        {
            // Disable CharacterController for menu
            CharacterController cc = playerRig.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }

            playerRig.position = mainMenuPoint.position;
            playerRig.rotation = mainMenuPoint.rotation;
        }

        // Initialize UI
        mainMenuUI.SetActive(true);
        startGameUI.SetActive(false);

        // Start main menu music
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnMainMenuEntered();
        }

        // Skip main menu if enabled
        if (skipMainMenuOnStart)
        {
            GoToCareerMode();
        }
    }

    public void PlayClickSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClickSound();
        }
    }

    public void GoToCareerMode()
    {
        PlayClickSound();
        Debug.Log("[MainMenuNavigation] Career Mode - Direct teleport to start game");
        
        // Direct teleport to start game
        TeleportToStartGame();
    }

    private void TeleportToStartGame()
    {
        if (startGamePoint == null || playerRig == null)
        {
            Debug.LogError("[MainMenuNavigation] Missing references!");
            return;
        }

        // Teleport player rig
        playerRig.position = startGamePoint.position;
        playerRig.rotation = startGamePoint.rotation;

        // Reparent camera and restore offset
        if (mainCamera != null)
        {
            mainCamera.transform.parent = playerRig;
            mainCamera.transform.localPosition = cameraLocalOffset;
            mainCamera.transform.localRotation = Quaternion.identity;
        }

        // Reset physics
        CharacterController cc = playerRig.GetComponent<CharacterController>();
        if (cc != null)
        {
            // Ensure player is above ground
            RaycastHit hit;
            if (Physics.Raycast(playerRig.position + Vector3.up * 0.5f, Vector3.down, out hit, 10f))
            {
                float groundY = hit.point.y + cc.height * 0.5f + 0.1f;
                playerRig.position = new Vector3(playerRig.position.x, groundY, playerRig.position.z);
            }
            
            cc.enabled = true;
        }

        // Reset FirstPersonController
        var fpsController = playerRig.GetComponentInChildren<FirstPersonController>();
        if (fpsController != null)
        {
            fpsController.enabled = true;
        }

        // Update UI
        mainMenuUI.SetActive(false);
        startGameUI.SetActive(true);

        // Notify systems
        if (UIManager.Instance != null)
        {
            UIManager.Instance.TransitionToGameMode();
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnGameModeEntered();
        }

        // Start data processing with prefetched data
        if (GameController.Instance != null)
        {
            GameController.Instance.StartWithPrefetchedData();
        }

        OnGameReady?.Invoke();
    }
}