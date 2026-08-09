using System.Collections.Generic;
using UnityEngine;

public class RoomWorldHitArea : MonoBehaviour
{
    [System.Serializable]
    public sealed class RoomArea
    {
        [SerializeField] private string roomId;
        [SerializeField] private SpriteRenderer hitAreaSprite;
        [SerializeField] private Transform hitAreaRect;

        public string RoomId => roomId;

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            if (hitAreaSprite != null)
            {
                bounds = hitAreaSprite.bounds;
                return true;
            }

            if (hitAreaRect != null)
            {
                Vector3 scale = hitAreaRect.lossyScale;
                bounds = new Bounds(hitAreaRect.position,
                    new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f));
                return true;
            }

            bounds = default;
            return false;
        }
    }

    public static RoomWorldHitArea Instance { get; private set; }

    [SerializeField] private List<RoomArea> areas = new List<RoomArea>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;
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

    public bool TryResolveRoom(Vector2 screenPosition, out string roomId)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            roomId = null;
            return false;
        }

        Vector3 world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
        Vector2 worldPoint = new Vector2(world.x, world.y);

        string bestRoomId = null;
        float bestSqrDistance = float.MaxValue;

        for (int i = 0; i < areas.Count; i++)
        {
            RoomArea area = areas[i];
            if (area == null || string.IsNullOrEmpty(area.RoomId))
                continue;
            if (!area.TryGetWorldBounds(out Bounds bounds))
                continue;
            if (worldPoint.x < bounds.min.x || worldPoint.x > bounds.max.x)
                continue;
            if (worldPoint.y < bounds.min.y || worldPoint.y > bounds.max.y)
                continue;

            Vector2 center = new Vector2(bounds.center.x, bounds.center.y);
            float sqrDistance = (center - worldPoint).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                bestRoomId = area.RoomId;
            }
        }

        roomId = bestRoomId;
        return bestRoomId != null;
    }

    public static bool TryResolveRoomUnderPointer(Vector2 screenPosition, out string roomId)
    {
        if (Instance != null)
            return Instance.TryResolveRoom(screenPosition, out roomId);

        roomId = null;
        return false;
    }
}
