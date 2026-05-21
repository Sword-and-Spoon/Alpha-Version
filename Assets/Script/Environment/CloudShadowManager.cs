using System.Collections.Generic;
using UnityEngine;

public class CloudShadowManager : MonoBehaviour, IEnvironmentObserver
{
    [Header("Test Override")]
    [SerializeField] private Sprite testSprite;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 0;

    private EnvironmentStateController _controller;
    private Camera _cam;
    private readonly List<CloudData> _clouds = new List<CloudData>();
    private Texture2D _placeholderTexture;
    private bool _isVisible = true;

    private class CloudData
    {
        public Transform Trans;
        public SpriteRenderer Renderer;
        public float Speed;
        public float HalfExtentX;
        public float HalfExtentY;
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private void Start()
    {
        _cam = Camera.main;
        _controller = EnvironmentStateController.Instance;

        if (_controller == null)
        {
            Debug.LogError("[CloudShadowManager] EnvironmentStateController not found.");
            enabled = false;
            return;
        }

        _controller.Register(this);

        // Sync initial state (ซ่อนเมฆทั้ง Indoor และ Cave)
        _isVisible = _controller.CurrentState == EnvironmentState.Outdoor;

        SpawnClouds();
    }

    private void OnDestroy()
    {
        _controller?.Unregister(this);

        if (_placeholderTexture != null)
            Destroy(_placeholderTexture);
    }

    // ── Update ──────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isVisible || _cam == null) return;

        var settings = _controller.Settings;
        Vector2 wind = settings.windDirection;
        if (wind == Vector2.zero) return;
        wind.Normalize();

        float camHalfH = _cam.orthographicSize;
        float camHalfW = camHalfH * _cam.aspect;
        Vector3 camPos  = _cam.transform.position;

        float dt = Time.deltaTime;

        foreach (var cloud in _clouds)
        {
            Vector3 pos = cloud.Trans.position;
            pos.x += wind.x * cloud.Speed * dt;
            pos.y += wind.y * cloud.Speed * dt;

            // Seamless loop: ออกขอบด้านไหน → ย้ายไปขอบตรงข้าม
            float minX = camPos.x - camHalfW - cloud.HalfExtentX;
            float maxX = camPos.x + camHalfW + cloud.HalfExtentX;
            float minY = camPos.y - camHalfH - cloud.HalfExtentY;
            float maxY = camPos.y + camHalfH + cloud.HalfExtentY;

            if (pos.x > maxX) pos.x = minX;
            else if (pos.x < minX) pos.x = maxX;

            if (pos.y > maxY) pos.y = minY;
            else if (pos.y < minY) pos.y = maxY;

            cloud.Trans.position = pos;
        }
    }

    // ── IEnvironmentObserver ────────────────────────────────────────────────

    public void OnEnvironmentStateChanged(EnvironmentState newState)
    {
        _isVisible = newState == EnvironmentState.Outdoor;
        foreach (var cloud in _clouds)
            cloud.Renderer.enabled = _isVisible;
    }

    // ── Spawn ───────────────────────────────────────────────────────────────

    private void SpawnClouds()
    {
        var settings = _controller.Settings;

        Sprite sprite = testSprite != null ? testSprite
            : settings.cloudShadowSprite != null ? settings.cloudShadowSprite
            : CreatePlaceholderSprite();

        float camHalfH = _cam != null ? _cam.orthographicSize : 5f;
        float camHalfW = _cam != null ? camHalfH * _cam.aspect : 8f;

        // กระจายในพื้นที่ 2.5x ของกล้อง
        float spawnHalfW = camHalfW * 2.5f;
        float spawnHalfH = camHalfH * 2.5f;

        int count = Mathf.Max(1, settings.maxCloudCount);

        // แบ่งแกน X เป็น count ช่อง แกน Y เป็น 2 แถว (บน/ล่าง)
        // → รับประกันว่าเมฆไม่อยู่ช่องเดียวกันเลย
        float zoneW = (spawnHalfW * 2f) / count;
        float zoneHalfH = spawnHalfH * 0.45f; // jitter range ในแต่ละแถว

        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject($"Cloud_{i}");
            go.transform.SetParent(transform);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;

            // สลับ size: คู่ = ใหญ่, คี่ = เล็ก
            float scale = (i % 2 == 0)
                ? Random.Range(settings.cloudScaleMax * 0.65f, settings.cloudScaleMax)
                : Random.Range(settings.cloudScaleMin, settings.cloudScaleMin * 1.8f);

            go.transform.localScale = new Vector3(scale, scale * 0.45f, 1f);

            // ก้อนใหญ่เข้มกว่า
            float alpha = (i % 2 == 0)
                ? Random.Range(settings.cloudAlphaMin + 0.05f, settings.cloudAlphaMax)
                : Random.Range(settings.cloudAlphaMin, settings.cloudAlphaMax - 0.05f);
            sr.color = new Color(0.1f, 0.1f, 0.15f, Mathf.Clamp01(alpha));

            // Zone-based X: ก้อนที่ i อยู่ในช่อง i เท่านั้น + jitter 20-80% ของช่อง
            float zoneLeft = -spawnHalfW + zoneW * i;
            float x = zoneLeft + Random.Range(zoneW * 0.2f, zoneW * 0.8f);

            // แถว Y สลับบน/ล่าง + jitter
            float yCenter = (i % 2 == 0) ? spawnHalfH * 0.35f : -spawnHalfH * 0.35f;
            float y = yCenter + Random.Range(-zoneHalfH, zoneHalfH);

            go.transform.position = new Vector3(x, y, 0f);

            float speed = Random.Range(settings.windSpeedMin, settings.windSpeedMax);
            float halfX = sprite.bounds.size.x * scale * 0.5f;
            float halfY = sprite.bounds.size.y * scale * 0.45f * 0.5f;

            _clouds.Add(new CloudData
            {
                Trans        = go.transform,
                Renderer     = sr,
                Speed        = speed,
                HalfExtentX  = halfX,
                HalfExtentY  = halfY,
            });
        }

        if (!_isVisible)
            foreach (var cloud in _clouds)
                cloud.Renderer.enabled = false;
    }

    // ── Placeholder Sprite ──────────────────────────────────────────────────
    //
    private Sprite CreatePlaceholderSprite()
    {
        const int size = 128;
        _placeholderTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius   = size * 0.5f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float t     = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = t < 1f ? (1f - t) * (1f - t) : 0f; // quadratic falloff ขอบฟุ้ง
                _placeholderTexture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }

        _placeholderTexture.Apply();

        return Sprite.Create(
            _placeholderTexture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
    }
}
