using System.Collections.Generic;
using UnityEngine;

public class RoomFloorRegistry : MonoBehaviour
{
    [System.Serializable]
    public class FloorAnchorMapping
    {
        public Transform anchor;
        public int floorIndex;
    }

    public static RoomFloorRegistry Instance { get; private set; }

    [SerializeField] private List<FloorAnchorMapping> mappings = new List<FloorAnchorMapping>();

    private readonly Dictionary<Transform, int> _anchorToFloor = new Dictionary<Transform, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Rebuild();
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;
        Rebuild();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Rebuild()
    {
        _anchorToFloor.Clear();
        if (mappings == null)
            return;
        for (int i = 0; i < mappings.Count; i++)
        {
            FloorAnchorMapping mapping = mappings[i];
            if (mapping == null || mapping.anchor == null)
                continue;
            _anchorToFloor[mapping.anchor] = mapping.floorIndex;
        }
    }

    public bool TryGetFloorForSlot(RoomTenantAvatarSlot slot, out int floor)
    {
        floor = 0;
        if (slot == null)
            return false;
        Transform anchor = slot.PositionAnchor;
        if (anchor == null)
            return false;
        return _anchorToFloor.TryGetValue(anchor, out floor);
    }
}
