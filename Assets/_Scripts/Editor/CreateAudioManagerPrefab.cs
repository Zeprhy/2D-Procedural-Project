using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility to create AudioManager prefab with one click.
/// Access via: Tools > Audio > Create AudioManager Prefab
/// </summary>
public class CreateAudioManagerPrefab
{
    [MenuItem("Tools/Audio/Create AudioManager Prefab")]
    public static void Create()
    {
        // Ensure prefab directory exists
        string prefabDir = "Assets/_Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabDir))
        {
            AssetDatabase.CreateFolder("Assets", "_Prefabs");
        }

        GameObject go = new GameObject("AudioManager");
        var audioManager = go.AddComponent<AudioManager>();
        string prefabPath = "Assets/_Prefabs/AudioManager.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        
        Debug.Log($"<color=green>[AudioManager]</color> Prefab created at: {prefabPath}");
        AssetDatabase.Refresh();
        
        // Ping the created prefab in Project window
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        EditorGUIUtility.PingObject(prefab);
    }

    [MenuItem("Tools/Audio/Create AudioManager Prefab", validate = true)]
    public static bool ValidateCreate()
    {
        // Disable if prefab already exists
        return !AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/AudioManager.prefab");
    }
}