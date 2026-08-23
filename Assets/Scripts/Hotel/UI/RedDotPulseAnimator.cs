using UnityEngine;
using UnityEngine.UI;

namespace Hotel.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public class RedDotPulseAnimator : MonoBehaviour
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private float cycleDuration = 2.5f;

        private bool _isPlaying = true;
        private Color _initialColor = Color.white;

        private void Awake()
        {
            if (targetImage == null)
                targetImage = GetComponent<Image>();
            if (targetImage != null)
                _initialColor = targetImage.color;
        }

        private void OnEnable()
        {
            _isPlaying = true;
            if (targetImage != null)
            {
                Color c = targetImage.color;
                c.a = _initialColor.a;
                targetImage.color = c;
            }
        }

        private void OnDisable()
        {
            _isPlaying = false;
            if (targetImage != null)
            {
                Color c = targetImage.color;
                c.a = _initialColor.a;
                targetImage.color = c;
            }
        }

        private void Update()
        {
            if (!_isPlaying || targetImage == null || !targetImage.enabled || !targetImage.gameObject.activeInHierarchy)
                return;

            float duration = cycleDuration > 0.001f ? cycleDuration : 2.5f;
            float t = (Mathf.Sin(Time.time * (Mathf.PI * 2f / duration)) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(0.5f, 1f, t);

            Color c = targetImage.color;
            c.a = alpha;
            targetImage.color = c;
        }

        public void SetPlaying(bool playing)
        {
            _isPlaying = playing;
            if (!playing && targetImage != null)
            {
                Color c = targetImage.color;
                c.a = _initialColor.a;
                targetImage.color = c;
            }
        }
    }
}
