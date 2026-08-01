using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private MonoBehaviour[] managedPanels;

    private void Start()
    {
        foreach (var panel in managedPanels)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(true);
                panel.enabled = true;
            }
        }
    }
}
