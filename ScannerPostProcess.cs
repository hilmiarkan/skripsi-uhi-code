using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ScannerPostProcess : MonoBehaviour
{
    [Tooltip("Drag material ScannerMat di sini")]
    public Material scannerMaterial;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (scannerMaterial != null)
            // Kirim frame kamera (src) ke shader, hasilnya ke layar (dst)
            Graphics.Blit(src, dst, scannerMaterial);
        else
            // Fallback: langsung tampil tanpa efek
            Graphics.Blit(src, dst);
    }
}