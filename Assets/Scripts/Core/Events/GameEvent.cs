using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewGameEvent", menuName = "Events/GameEvent")]
public class GameEvent : ScriptableObject
{
    private List<GameEventListener> listeners = new();
    private List<Action> callbacks = new();

    public void Raise()
    {
        for (int i = listeners.Count - 1; i >= 0; i--)
            listeners[i].OnEventRaised();

        for (int i = callbacks.Count - 1; i >= 0; i--)
            callbacks[i]?.Invoke();
    }

    public void Register(GameEventListener listener)
    {
        if (!listeners.Contains(listener))
            listeners.Add(listener);
    }

    public void Unregister(GameEventListener listener)
    {
        listeners.Remove(listener);
    }

    public void Register(Action callback)
    {
        if (!callbacks.Contains(callback))
            callbacks.Add(callback);
    }

    public void Unregister(Action callback)
    {
        callbacks.Remove(callback);
    }
}