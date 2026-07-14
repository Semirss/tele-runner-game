using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif
using System.Collections.Generic;
 
/// <summary>
/// State pushed on top of the GameManager when the player dies.
/// </summary>
public class GameOverState : AState
{
    public TrackManager trackManager;
    public Canvas canvas;
    public MissionUI missionPopup;

    public AudioClip gameOverTheme;

    public Leaderboard miniLeaderboard;
    public Leaderboard fullLeaderboard;

    public GameObject addButton;

    public override void Enter(AState from)
    {
        if (canvas != null)
            canvas.gameObject.SetActive(true);

        string displayName = GetCurrentPlayerName();
        int finalScore = trackManager == null ? 0 : trackManager.score;

        ApplyPlayerEntry(miniLeaderboard, displayName, finalScore);
        SubmitScoreAndPopulateLeaderboard();

        if (missionPopup != null)
        {
            if (PlayerData.instance != null && PlayerData.instance.AnyMissionComplete())
                StartCoroutine(missionPopup.Open());
            else
                missionPopup.gameObject.SetActive(false);
        }

        CreditCoins();

        if (MusicPlayer.instance != null && MusicPlayer.instance.GetStem(0) != gameOverTheme)
        {
            MusicPlayer.instance.SetStem(0, gameOverTheme);
            StartCoroutine(MusicPlayer.instance.RestartAllStems());
        }
    }

    public override void Exit(AState to)
    {
        StopAllCoroutines();

        if (miniLeaderboard != null)
            miniLeaderboard.gameObject.SetActive(false);
        if (fullLeaderboard != null)
            fullLeaderboard.gameObject.SetActive(false);
        if (missionPopup != null)
            missionPopup.gameObject.SetActive(false);
        if (canvas != null)
            canvas.gameObject.SetActive(false);

        FinishRun();
    }

    public override string GetName()
    {
        return "GameOver";
    }

    public override void Tick()
    {
    }

    public void OpenLeaderboard()
    {
        if (fullLeaderboard == null)
            return;

        string displayName = GetCurrentPlayerName();
        int finalScore = trackManager == null ? 0 : trackManager.score;

        if (miniLeaderboard != null)
            miniLeaderboard.gameObject.SetActive(false);

        fullLeaderboard.forcePlayerDisplay = false;
        fullLeaderboard.displayPlayer = true;
        ApplyPlayerEntry(fullLeaderboard, displayName, finalScore);
        fullLeaderboard.Open();
    }

    public void GoToStore()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("shop", UnityEngine.SceneManagement.LoadSceneMode.Additive);
    }


    public void GoToLoadout()
    {
        if (trackManager != null)
            trackManager.isRerun = false;
        manager.SwitchState("Loadout");
    }

    public void RunAgain()
    {
        if (trackManager != null)
            trackManager.isRerun = false;
        manager.SwitchState("Game");
    }

    protected void CreditCoins()
    {
        if (PlayerData.instance == null)
            return;

        PlayerData.instance.Save();

#if UNITY_ANALYTICS
        if (trackManager == null || trackManager.characterController == null)
            return;

        var transactionId = System.Guid.NewGuid().ToString();
        var transactionContext = "gameplay";
        var level = PlayerData.instance.rank.ToString();
        var itemType = "consumable";
        
        if (trackManager.characterController.coins > 0)
        {
            AnalyticsEvent.ItemAcquired(
                AcquisitionType.Soft,
                transactionContext,
                trackManager.characterController.coins,
                "fishbone",
                PlayerData.instance.coins,
                itemType,
                level,
                transactionId
            );
        }

        if (trackManager.characterController.premium > 0)
        {
            AnalyticsEvent.ItemAcquired(
                AcquisitionType.Premium,
                transactionContext,
                trackManager.characterController.premium,
                "anchovies",
                PlayerData.instance.premium,
                itemType,
                level,
                transactionId
            );
        }
#endif 
    }

    protected void FinishRun()
    {
        if (PlayerData.instance == null)
            return;

        string displayName = GetCurrentPlayerName();
        if (!string.IsNullOrEmpty(displayName))
            PlayerData.instance.previousName = displayName;

        if (trackManager != null && (SupabaseClient.instance == null || !SupabaseClient.instance.HasLocalPlayer))
            PlayerData.instance.InsertScore(trackManager.score, displayName);

#if UNITY_ANALYTICS
        if (trackManager != null && trackManager.characterController != null && trackManager.characterController.characterCollider != null)
        {
            CharacterCollider.DeathEvent de = trackManager.characterController.characterCollider.deathData;
            AnalyticsEvent.GameOver(null, new Dictionary<string, object> {
                { "coins", de.coins },
                { "premium", de.premium },
                { "score", de.score },
                { "distance", de.worldDistance },
                { "obstacle",  de.obstacleType },
                { "theme", de.themeUsed },
                { "character", de.character },
            });
        }
#endif

        PlayerData.instance.Save();

        if (trackManager != null)
            trackManager.End();
    }

    void SubmitScoreAndPopulateLeaderboard()
    {
        SupabaseClient client = SupabaseClient.instance;
        int finalScore = trackManager == null ? 0 : trackManager.score;

        if (miniLeaderboard != null)
            miniLeaderboard.Populate();

        if (client != null && client.HasLocalPlayer)
        {
            client.SubmitScore(finalScore, result =>
            {
                if (miniLeaderboard != null)
                    miniLeaderboard.Populate();
            });
        }
    }

    void ApplyPlayerEntry(Leaderboard leaderboard, string displayName, int score)
    {
        if (leaderboard == null || leaderboard.playerEntry == null)
            return;

        if (leaderboard.playerEntry.inputName != null)
        {
            leaderboard.playerEntry.inputName.text = displayName;
            leaderboard.playerEntry.inputName.interactable = false;
        }

        if (leaderboard.playerEntry.playerName != null)
            leaderboard.playerEntry.playerName.text = displayName;

        if (leaderboard.playerEntry.score != null)
            leaderboard.playerEntry.score.text = score.ToString();
    }

    string GetCurrentPlayerName()
    {
        if (SupabaseClient.instance != null && !string.IsNullOrEmpty(SupabaseClient.instance.DisplayName))
            return SupabaseClient.instance.DisplayName;

        if (PlayerData.instance != null && !string.IsNullOrEmpty(PlayerData.instance.previousName))
            return PlayerData.instance.previousName;

        return "Player";
    }
}
