using UnityEngine;
using UnityEditor;

public static class TimeEventAssetCreator
{
    [MenuItem("Assets/Create/Events/Time Events")]
    public static void CreateTimeEvents()
    {
        string path = "Assets/Data/Events";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder("Assets/Data", "Events");
        }

        CreateAsset<TimePhaseChangedEvent>(path + "/TimePhaseChangedEvent.asset");
        CreateAsset<DayStartedEvent>(path + "/DayStartedEvent.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TimeEventAssetCreator] Time event assets created.");
    }

    private static void CreateAsset<T>(string path) where T : ScriptableObject
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) return;
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
    }
}