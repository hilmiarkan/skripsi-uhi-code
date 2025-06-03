using UnityEngine;

[ExecuteAlways]
public class DrawGizmos : MonoBehaviour
{
    public Color gizmoColor = Color.cyan;
    public float sphereSize = 1.0f; // Lebih besar agar terlihat jelas

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, sphereSize);

// #if UNITY_EDITOR
//         UnityEditor.Handles.color = Color.white;
//         UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, gameObject.name);
// #endif
    }
}
