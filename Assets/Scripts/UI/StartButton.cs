using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif

public class StartButton : MonoBehaviour
{
    bool m_Loading;

    public void StartGame()
    {
        if (m_Loading)
            return;

        if (SupabaseClient.instance == null || !SupabaseClient.instance.HasLocalPlayer)
        {
            PlayerRegistrationUI.Show();
            return;
        }

        if (PlayerData.instance == null)
            PlayerData.Create();

        if (PlayerData.instance.ftueLevel == 0)
        {
            PlayerData.instance.ftueLevel = 1;
            PlayerData.instance.Save();
#if UNITY_ANALYTICS
            AnalyticsEvent.FirstInteraction("start_button_pressed");
#endif
        }

        StartCoroutine(LoadMainScene());
    }

    IEnumerator LoadMainScene()
    {
        m_Loading = true;
        yield return null;
        SceneManager.LoadScene("Main");
    }
}
