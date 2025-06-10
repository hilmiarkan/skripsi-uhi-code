using UnityEngine;

public class HotspotAnimator : MonoBehaviour
{
    [Header("Rotasi")]
    [Tooltip("Kecepatan rotasi di sekitar sumbu Z (derajat per detik)")]
    public float rotationSpeed = 90f;

    [Header("Gerakan Naik-Turun")]
    [Tooltip("Amplitudo (jarak) vertikal yang ditempuh dari posisi awal")]
    public float bobAmplitude = 0.5f;
    [Tooltip("Kecepatan osilasi naik-turun")]
    public float bobFrequency = 1f;

    // Posisi awal (y) untuk referensi osilasi
    private float initialY;

    void Start()
    {
        // Simpan posisi Y awal agar gerakan bobbing berpatokan ke sini
        initialY = transform.position.y;
    }

    void Update()
    {
        // 1. Rotasi di sumbu Z (bukan Y)
        float zRotation = rotationSpeed * Time.deltaTime;
        transform.Rotate(0f, 0f, zRotation, Space.Self);

        // 2. Gerakan naik-turun (bobbing)
        float newY = initialY + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        Vector3 pos = transform.position;
        pos.y = newY;
        transform.position = pos;
    }
}
