using UnityEngine;
using UnityEngine.Tilemaps;

namespace Environment
{
    public class ObjectFader : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Range(0, 1)] private float transparentAlpha = 0.7f; // ความโปร่งใสเมื่อตัวละครอยู่ข้างหลัง (0.0 - 1.0)
        [SerializeField] private float fadeSpeed = 5f;                       // ความเร็วในการจาง (ยิ่งมากยิ่งเร็ว)

        private SpriteRenderer _spriteRenderer;
        private Tilemap _tilemap;

        private Color _defaultColor;
        private Color _targetColor;
        private bool _hasRenderer;

        private void Start()
        {
            // พยายามดึงทั้งสอง Component
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _tilemap = GetComponent<Tilemap>();

            if (_spriteRenderer != null)
            {
                _defaultColor = _spriteRenderer.color;
                _hasRenderer = true;
            }
            else if (_tilemap != null)
            {
                _defaultColor = _tilemap.color;
                _hasRenderer = true;
            }
            else
            {
                Debug.LogError($"ObjectFader on {gameObject.name} requires either a SpriteRenderer or a Tilemap!");
                enabled = false;
                return;
            }

            _targetColor = _defaultColor;
        }

        private void Update()
        {
            if (!_hasRenderer) return;

            Color currentColor = GetCurrentColor();

            // ทำการค่อยๆ ปรับสีเพื่อให้ดูนุ่มนวล
            if (currentColor != _targetColor)
            {
                SetCurrentColor(Color.Lerp(currentColor, _targetColor, fadeSpeed * Time.deltaTime));
            }
        }

        private Color GetCurrentColor()
        {
            if (_spriteRenderer != null) return _spriteRenderer.color;
            if (_tilemap != null) return _tilemap.color;
            return Color.white;
        }

        private void SetCurrentColor(Color color)
        {
            if (_spriteRenderer != null) _spriteRenderer.color = color;
            else if (_tilemap != null) _tilemap.color = color;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                SetTransparent(true);
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                SetTransparent(false);
            }
        }

        private void SetTransparent(bool isTransparent)
        {
            if (isTransparent)
            {
                _targetColor = new Color(_defaultColor.r, _defaultColor.g, _defaultColor.b, transparentAlpha);
            }
            else
            {
                _targetColor = _defaultColor;
            }
        }
    }
}
