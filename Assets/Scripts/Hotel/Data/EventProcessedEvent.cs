using Hotel.Runtime;
using UnityEngine;

public class EventProcessedData
{
    public string eventId;
    public string optionId;
    public EventEffect[] effects;
    public string ownerTenantId;
    public TenantAbility[] requiredTags;
}

[CreateAssetMenu(fileName = "EventProcessedEvent", menuName = "Events/EventProcessedEvent")]
public class EventProcessedEvent : GameEvent<string>
{
    [System.NonSerialized] private EventProcessedData _lastProcessedData;

    public EventProcessedData LastProcessedData => _lastProcessedData;

    public void RaiseProcessed(EventProcessedData data)
    {
        _lastProcessedData = data;
        if (data != null)
            Raise(data.eventId);
    }
}
