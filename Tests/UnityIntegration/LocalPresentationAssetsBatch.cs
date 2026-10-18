using System;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Copy into an isolated project's Assets/Editor. Uses Editor serialization, never hand-edits YAML.</summary>
public static class LocalPresentationAssetsBatch
{
    const string DominoPath = "Assets/Prefabs/Dominoes/domino_original.prefab";
    const string PlayerPath = "Assets/Prefabs/DominoPlayer.prefab";

    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        var console = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true))
            .Single(component => component != null && component.GetType().Name == "QuantumConsole");
        var consoleSettings = new SerializedObject(console);
        consoleSettings.FindProperty("_activateOnStartup").boolValue = false;
        consoleSettings.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(console);
        EditorSceneManager.SaveScene(scene);

        var root = PrefabUtility.LoadPrefabContents(DominoPath);
        try
        {
            foreach (var component in root.GetComponentsInChildren<NetworkTransform>(true))
                UnityEngine.Object.DestroyImmediate(component);
            foreach (var component in root.GetComponentsInChildren<NetworkObject>(true))
                UnityEngine.Object.DestroyImmediate(component);
            PrefabUtility.SaveAsPrefabAsset(root, DominoPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var domino = AssetDatabase.LoadAssetAtPath<GameObject>(DominoPath);
        foreach (var guid in AssetDatabase.FindAssets("t:NetworkPrefabsList"))
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var entry in list.PrefabList.Where(p => p.Prefab == domino).ToArray())
            { list.Remove(entry); EditorUtility.SetDirty(list); }
        }
        AssetDatabase.SaveAssets();
        Check();
    }

    public static void Check()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            var console = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true))
                .Single(component => component != null && component.GetType().Name == "QuantumConsole");
            var consoleSettings = new SerializedObject(console);
            Require(!consoleSettings.FindProperty("_activateOnStartup").boolValue, "console starts closed");
            Require(consoleSettings.FindProperty("_supportedState").intValue == 0, "console remains available in all builds");
            var domino = AssetDatabase.LoadAssetAtPath<GameObject>(DominoPath);
            Require(domino != null, "domino prefab present");
            Require(domino.GetComponentsInChildren<NetworkObject>(true).Length == 0, "tiles have no NetworkObject");
            Require(domino.GetComponentsInChildren<NetworkTransform>(true).Length == 0, "tiles have no NetworkTransform");
            foreach (var guid in AssetDatabase.FindAssets("t:NetworkPrefabsList"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
                Require(!list.Contains(domino), "tiles unregistered in " + path);
            }
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath).GetComponent<NetworkObject>();
            Require(player != null, "player remains networked");
            // NGO recalculates the imported runtime hash in OnValidate; preserve the named serialized asset.
            Require(File.ReadAllText(PlayerPath).Contains("GlobalObjectIdHash: 951099334"),
                "player serialized network hash preserved");
            var tile = UnityEngine.Object.Instantiate(domino);
            var parent = new GameObject("local-parent-probe");
            tile.transform.SetParent(parent.transform, false);
            Require(tile.transform.parent == parent.transform, "tile stays under a local parent without a running network");
            UnityEngine.Object.DestroyImmediate(parent);
            Debug.Log("[LocalPresentationAssets] PASS ALL");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    static void Require(bool condition, string name)
    {
        if (!condition) throw new Exception("[LocalPresentationAssets] FAIL " + name);
        Debug.Log("[LocalPresentationAssets] PASS " + name);
    }
}
