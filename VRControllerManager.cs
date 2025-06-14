using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple VR Controller Manager - only handles grip button for heatmap toggle
/// </summary>
public class VRControllerManager : MonoBehaviour
{
    [Header("Input Action References")]
    [Tooltip("Aksi untuk tombol grip/grab di controller kiri.")]
    public InputActionReference leftGripAction;

    private bool isControlActive = false;

    private void Awake()
    {
        // Enable grip action
        if (leftGripAction != null)
        {
            leftGripAction.action.Enable();
            leftGripAction.action.performed += OnLeftGripPressed;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe untuk mencegah memory leak
        if (leftGripAction != null)
        {
            leftGripAction.action.performed -= OnLeftGripPressed;
        }
    }

    public void ActivateControls()
    {
        isControlActive = true;
        Debug.Log("[VRControllerManager] Controls activated - grip button ready for heatmap toggle");
    }

    public void DeactivateControls()
    {
        isControlActive = false;
        Debug.Log("[VRControllerManager] Controls deactivated");
    }

    private void OnLeftGripPressed(InputAction.CallbackContext context)
    {
        if (!isControlActive) return;
        
        Debug.Log("[VRControllerManager] Left grip pressed - toggling raw heatmap");
        
        // Toggle raw heatmap
        if (HeatmapGenerator.Instance != null)
        {
            HeatmapGenerator.Instance.ToggleRawHeatmap();
        }
        else
        {
            Debug.LogWarning("[VRControllerManager] HeatmapGenerator.Instance not found");
        }
    }
} 