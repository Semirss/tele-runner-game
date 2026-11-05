using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Keeps scene-editable UI available in scenes already open during a source update.</summary>
[InitializeOnLoad]
public static class TeleRunnerSceneUiInstaller
{
    static TeleRunnerSceneUiInstaller()
    {
        EditorApplication.delayCall += InstallOpenScenes;
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += InstallOpenScenes;
    }

    static void InstallOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || (scene.name != "Start" && scene.name != "Main")) continue;

            GameObject[] roots = scene.GetRootGameObjects();
            bool hasRegistration = false;
            bool hasStyle = false;
            GameObject styleHost = null;
            for (int r = 0; r < roots.Length; r++)
            {
                GameObject root = roots[r];
                if (root.GetComponent<PlayerRegistrationUI>() != null) hasRegistration = true;
                if (root.GetComponent<RegistrationSceneStyle>() != null) hasStyle = true;
                if (scene.name == "Start" && root.name == "Canvas") styleHost = root;
                if (scene.name == "Main" && root.name == "Loadout") styleHost = root;
            }

            bool changed = false;
            if (!hasStyle && styleHost != null)
            {
                Undo.AddComponent<RegistrationSceneStyle>(styleHost);
                changed = true;
            }
            if (!hasRegistration)
            {
                GameObject form = new GameObject("PlayerRegistrationUI", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(form, scene);
                Undo.RegisterCreatedObjectUndo(form, "Add Player Registration UI");
                Undo.AddComponent<PlayerRegistrationUI>(form);
                changed = true;
            }

            if (scene.name == "Start" && styleHost != null)
                changed |= EnsureStartRunner(styleHost.transform);

            if (changed) EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    static bool EnsureStartRunner(Transform canvas)
    {
        if (canvas.Find("Start Runner - Sun Chips") != null) return false;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Resources/TransitMenu/StartRunner.png");
        if (sprite == null) return false;

        GameObject runner = new GameObject("Start Runner - Sun Chips",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(runner, "Add Start Runner");
        runner.layer = 5;
        runner.transform.SetParent(canvas, false);
        runner.transform.SetSiblingIndex(Mathf.Min(1, canvas.childCount - 1));
        RectTransform rect = runner.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.54f, .5f);
        rect.sizeDelta = new Vector2(720f, 720f);
        Image image = runner.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return true;
    }
}
