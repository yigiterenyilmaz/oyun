using UnityEngine;
using UnityEngine.InputSystem;

public class MapController : MonoBehaviour
{
    [Header("Referanslar")]
    public SpriteRenderer mapRenderer; // Harita objesini buraya sürükle

    [Header("Zoom Ayarları")]
    public float zoomSpeed = 1.2f;
    public float minSize = 2f;
    private float maxSize; // Haritaya göre otomatik hesaplanacak

    private Camera cam;
    private Vector3 dragOrigin;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (mapRenderer != null)
        {
            CalculateMaxZoom();
        }
    }

    // Haritanın dışındaki siyah boşlukların görünmemesi için max zoom'u otomatik ayarlar
    void CalculateMaxZoom()
    {
        float mapHeight = mapRenderer.bounds.size.y / 2f;
        float mapWidthSize = (mapRenderer.bounds.size.x / 2f) / cam.aspect;
        maxSize = Mathf.Min(mapHeight, mapWidthSize);
        
        // Başlangıçta en uzak zoom ile başla
        cam.orthographicSize = maxSize;
    }

    void LateUpdate()
    {
        if (mapRenderer == null) return;

        HandleZoom();
        HandlePan();

        // Her hareket sonrası kamerayı harita sınırlarına hapset
        transform.position = ClampCamera(transform.position);
    }

    void HandlePan()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragOrigin = GetMouseWorldPosition();
        }

        if (Mouse.current.leftButton.isPressed)
        {
            Vector3 difference = dragOrigin - GetMouseWorldPosition();
            transform.position += difference;
        }
    }

    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.1f) return;

        // Zoom öncesi mouse pozisyonunu tut (Mouse'un üzerine zoom yapmak için)
        Vector3 mouseBefore = GetMouseWorldPosition();

        float newSize = cam.orthographicSize - (scroll * zoomSpeed * 0.01f);
        cam.orthographicSize = Mathf.Clamp(newSize, minSize, maxSize);

        // Zoom sonrası mouse pozisyonu arasındaki farkı kameraya uygula
        Vector3 mouseAfter = GetMouseWorldPosition();
        transform.position += (mouseBefore - mouseAfter);
    }

    // "Out of view frustum" hatasını engelleyen güvenli mouse-dünya pozisyon çevirici
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 screenPos = Mouse.current.position.ReadValue();
        // Z değeri 0 olmamalı, kameranın önünde bir mesafe olmalı
        screenPos.z = 10f; 
        return cam.ScreenToWorldPoint(screenPos);
    }

    private Vector3 ClampCamera(Vector3 targetPosition)
    {
        float camHeight = cam.orthographicSize;
        float camWidth = cam.orthographicSize * cam.aspect;

        // Harita sınırlarını hesapla
        float minX = mapRenderer.bounds.min.x + camWidth;
        float maxX = mapRenderer.bounds.max.x - camWidth;
        float minY = mapRenderer.bounds.min.y + camHeight;
        float maxY = mapRenderer.bounds.max.y - camHeight;

        // Kameranın yeni pozisyonunu bu sınırlar içinde tut
        float newX = Mathf.Clamp(targetPosition.x, minX, maxX);
        float newY = Mathf.Clamp(targetPosition.y, minY, maxY);

        return new Vector3(newX, newY, targetPosition.z);
    }
}