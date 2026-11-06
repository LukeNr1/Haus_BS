using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnsureX20ModelInScene
{
    [MenuItem("Tools/X20/Ensure Steuerung Model In Scene")]
    public static void Run()
    {
        const string scenePath = "Assets/Scenes/Schalter.unity";
        const string modelPath = "Assets/Models/Steuerung-Modell.glb";
        const string modelName = "Steuerung-Modell";

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != scenePath)
            scene = EditorSceneManager.OpenScene(scenePath);

        GameObject instance = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == modelName)
            {
                instance = root;
                break;
            }
        }

        if (instance == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab == null)
                throw new System.InvalidOperationException("X20 model not found at " + modelPath);

            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = modelName;
        }

        instance.transform.SetPositionAndRotation(
            new Vector3(4.1f, 14.2f, 0f),
            Quaternion.Euler(0f, 90f, 0f));
        instance.transform.localScale = new Vector3(180f, 180f, 180f);

        EditorUtility.SetDirty(instance);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Steuerung-Modell is saved in Schalter.unity and placed above the rooms.");
    }
}
