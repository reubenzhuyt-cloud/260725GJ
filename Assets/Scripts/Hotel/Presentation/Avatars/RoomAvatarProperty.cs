using System.Collections.Generic;
using UnityEngine;

public class RoomAvatarProperty : MonoBehaviour
{
    [SerializeField] private string roomId;
    [SerializeField] private bool allowDoubleOccupancy = true;

    private static readonly Dictionary<string, RoomAvatarProperty> Registry =
        new Dictionary<string, RoomAvatarProperty>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Registry.Clear();
    }

    public string RoomId => roomId;
    public bool AllowDoubleOccupancy => allowDoubleOccupancy;

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(roomId))
            return;
        Registry[roomId] = this;
    }

    private void OnDisable()
    {
        if (string.IsNullOrEmpty(roomId))
            return;
        if (Registry.TryGetValue(roomId, out RoomAvatarProperty current) && current == this)
            Registry.Remove(roomId);
    }

    public static bool TryGetCapacity(string roomId, out int capacity)
    {
        capacity = 1;
        if (string.IsNullOrEmpty(roomId))
            return false;
        if (!Registry.TryGetValue(roomId, out RoomAvatarProperty property) || property == null)
            return false;
        if (!property.isActiveAndEnabled)
            return false;
        capacity = property.allowDoubleOccupancy ? 2 : 1;
        return true;
    }
}
