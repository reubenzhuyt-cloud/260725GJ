using UnityEngine;
using UnityEngine.Events;

public class GameEventListener : MonoBehaviour
{
    [Tooltip("The GameEvent SO asset to listen to")]
    public GameEvent gameEvent;

    [Tooltip("Response invoked when the event is raised")]
    public UnityEvent response;

    private void OnEnable()
    {
        if (gameEvent != null)
            gameEvent.Register(this);
    }

    private void OnDisable()
    {
        if (gameEvent != null)
            gameEvent.Unregister(this);
    }

    public void OnEventRaised()
    {
        response?.Invoke();
    }
}