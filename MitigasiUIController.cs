using UnityEngine;
using UnityEngine.UI;

public class MitigasiUIController : MonoBehaviour
{
    [Header("Tombol‐Tombol pada UI")]
    public Button tanamPohonButton;
    public Button bangunBangunanButton; // (Anda bisa implementasikan aksi lain untuk bangun bangunan nanti)

    // Referensi ke objek yang akan di‐destroy saat Tanam Pohon
    private GameObject _ringObject;
    private GameObject _markerObject;
    private GameObject _actionMitigasiObject; // ActionMitigasi instance itu sendiri
    private GameObject _treePrefab;

    /// <summary>
    /// Dipanggil oleh ActionMitigasiController agar UI ini tahu
    /// objek mana yang harus dihapus, dan prefab pohon mana yang dipakai.
    /// </summary>
    public void Initialize(GameObject ring, GameObject marker, GameObject actionMitigasi, GameObject treePrefab)
    {
        _ringObject = ring;
        _markerObject = marker;
        _actionMitigasiObject = actionMitigasi;
        _treePrefab = treePrefab;

        // Pastikan UI aktif
        gameObject.SetActive(true);

        // Sambungkan button listener
        tanamPohonButton.onClick.RemoveAllListeners();
        tanamPohonButton.onClick.AddListener(OnTanamPohonClicked);

        bangunBangunanButton.onClick.RemoveAllListeners();
        bangunBangunanButton.onClick.AddListener(OnBangunBangunanClicked);
    }

    /// <summary>
    /// Dipanggil ketika tombol Tanam Pohon ditekan.
    /// </summary>
    private void OnTanamPohonClicked()
    {
        // 1. Hapus ring, marker, dan actionMitigasi
        if (_ringObject != null)     Destroy(_ringObject);
        if (_markerObject != null)   Destroy(_markerObject);
        if (_actionMitigasiObject != null) Destroy(_actionMitigasiObject);

        // 2. Instantiate pohon di posisi ring (atau marker)
        Vector3 spawnPosition = Vector3.zero;
        if (_ringObject != null)
        {
            spawnPosition = _ringObject.transform.position;
        }
        else if (_markerObject != null)
        {
            spawnPosition = _markerObject.transform.position;
        }
        else
        {
            // Kalau kedua‐duanya null (seharusnya tidak pernah), kita ambil posisi actionMitigasi
            spawnPosition = _actionMitigasiObject?.transform.position ?? Vector3.zero;
        }

        if (_treePrefab != null)
        {
            Instantiate(_treePrefab, spawnPosition, _treePrefab.transform.rotation);
        }

        // 3. Nonaktifkan UI ini (atau Destroy jika tidak akan dipakai lagi)
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Tombol Bangun Bangunan (Anda bisa isi nanti sesuai kebutuhan).
    /// </summary>
    private void OnBangunBangunanClicked()
    {
        // Contoh: hanya sembunyikan UI, tanpa tindakan lain
        gameObject.SetActive(false);
    }
}
