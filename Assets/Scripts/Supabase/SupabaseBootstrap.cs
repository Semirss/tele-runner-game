using UnityEngine;
using UnityEngine.SceneManagement;

public static class SupabaseBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (SupabaseClient.instance == null)
        {
            GameObject clientObject = new GameObject("SupabaseClient");
            clientObject.AddComponent<SupabaseClient>();
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Start" && (SupabaseClient.instance == null || !SupabaseClient.instance.HasLocalPlayer))
            PlayerRegistrationUI.Show();
    }
}

