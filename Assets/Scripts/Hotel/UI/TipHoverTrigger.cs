using UnityEngine;
using UnityEngine.EventSystems;

public class TipHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TipPanel tipPanel;
    [SerializeField] [TextArea] private string tipText = "";
    [SerializeField] private float hoverStillDelay = 0.2f;

    private bool _hovered;
    private Vector2 _pointerPosition;
    private float _hoverTimer;

    public TipPanel TipPanel
    {
        get => tipPanel;
        set => tipPanel = value;
    }

    public string TipText
    {
        get => tipText;
        set => tipText = value;
    }

    public float HoverStillDelay
    {
        get => hoverStillDelay;
        set => hoverStillDelay = value;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        _pointerPosition = eventData.position;
        _hoverTimer = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        _hoverTimer = 0f;
        SafeHide();
    }

    private void Update()
    {
        if (!_hovered || string.IsNullOrEmpty(tipText))
            return;

        if (Input.mousePresent)
            _pointerPosition = Input.mousePosition;

        _hoverTimer += Time.unscaledDeltaTime;
        if (_hoverTimer >= hoverStillDelay)
        {
            if (tipPanel != null && !tipPanel.IsShowing)
            {
                tipPanel.Show(tipText, _pointerPosition);
            }
        }
    }

    private void OnDisable()
    {
        _hovered = false;
        _hoverTimer = 0f;
        SafeHide();
    }

    private void SafeHide()
    {
        if (tipPanel != null && tipPanel.IsShowing)
        {
            tipPanel.Hide();
        }
    }
}
