using UnityEngine;
using UnityEngine.EventSystems; // jika menggunakan IPointerClickHandler
using UnityEngine.XR.Interaction.Toolkit; // jika XR‐clickable, tapi di contoh ini kita pakai OnMouseDown untuk klik biasa

public class ActionMitigasiController : MonoBehaviour
{
    [Header("Referensi Objek Hotspot")]
    [Tooltip("GameObject Ring yang harus dihapus saat Tanam Pohon.")]
    public GameObject ringObject;
    [Tooltip("GameObject Marker (terbang) yang harus dihapus saat Tanam Pohon.")]
    public GameObject markerObject;

    [Header("Prefab dan UI")]
    [Tooltip("Prefab pohon yang akan di‐Instantiate ketika Tanam Pohon diklik.")]
    public GameObject treePrefab;
    [Tooltip("Prefab Mitigasi UI (Canvas) yang menampilkan dua tombol.")]
    public GameObject mitigasiUIPrefab;

    // Instance Mitigasi UI yang sudah di‐Instantiate
    private MitigasiUIController _mitigasiUIInstance;

    /// <summary>
    /// Saat user mengklik ActionMitigasi (misalnya OnMouseDown),
    /// kita tampilkan atau buat Mitigasi UI, lalu kirim data reference ke UI Controller.
    /// </summary>
    private void OnMouseDown()
    {
        // Kalau belum ada instance UI, Instantiate
        if (_mitigasiUIInstance == null)
        {
            GameObject uiGO = Instantiate(mitigasiUIPrefab);
            _mitigasiUIInstance = uiGO.GetComponent<MitigasiUIController>();

            if (_mitigasiUIInstance == null)
            {
                Debug.LogError("[ActionMitigasiController] MitigasiUIController tidak ditemukan di prefab UI.");
                return;
            }
        }
        else
        {
            // Jika sudah pernah di‐instantiate, cukup enable
            _mitigasiUIInstance.gameObject.SetActive(true);
        }

        // Inisialisasi UI dengan referensi objek‐objek ini
        _mitigasiUIInstance.Initialize(ringObject, markerObject, this.gameObject, treePrefab);
    }
}
