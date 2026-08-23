using UnityEngine;
using UnityEngine.UI;

namespace Hotel.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public class RedDotIndicator : MonoBehaviour
    {
        [SerializeField] private Image _image;

        public Image DotImage => _image != null ? _image : (_image = GetComponent<Image>());

        private void Awake()
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
