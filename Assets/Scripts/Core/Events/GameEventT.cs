using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class GameEvent<T> : ScriptableObject
{
    private List<GameEventListener<T>> listeners = new List<GameEventListener<T>>();
    private List<Action<T>> callbacks = new List<Action<T>>();

    public void Raise(T data)
    {
        for (int i = listeners.Count - 1; i >= 0; i--)
            listeners[i].OnEventRaised(data);

        for (int i = callbacks.Count - 1; i >= 0; i--)
            callbacks[i]?.Invoke(data);
    }

    public void Register(GameEventListener<T> listener)
    {
        if (!listeners.Contains(listener))
            listeners.Add(listener);
    }

    public void Unregister(GameEventListener<T> listener)
    {
        listeners.Remove(listener);
    }

    public void Register(Action<T> callback)
    {
        if (!callbacks.Contains(callback))
            callbacks.Add(callback);
    }

    public void Unregister(Action<T> callback)
    {
        callbacks.Remove(callback);
    }
}