using UnityEngine;
using UnityEngine.Events;

public abstract class GameEventListener<T> : MonoBehaviour
{
    public abstract GameEvent<T> GetGameEvent();
    public abstract UnityEvent<T> GetResponse();

    private void OnEnable()
    {
        var evt = GetGameEvent();
        if (evt != null)
            evt.Register(this);
    }

    private void OnDisable()
    {
        var evt = GetGameEvent();
        if (evt != null)
            evt.Unregister(this);
    }

    public void OnEventRaised(T data)
    {
        GetResponse()?.Invoke(data);
    }
}