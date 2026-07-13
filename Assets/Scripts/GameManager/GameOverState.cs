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
        canvas.gameObject.SetActive(true);

        string displayName = GetCurrentPlayerName();
        if (miniLeaderboard.playerEntry.inputName != null)
        {
            miniLeaderboard.playerEntry.inputName.text = displayName;
            miniLeaderboard.playerEntry.inputName.interactable = false;
        }
        if (miniLeaderboard.playerEntry.playerName != null)
            miniLeaderboard.playerEntry.playerName.text = displayName;

        miniLeaderboard.playerEntry.score.text = trackManager.score.ToString();
        SubmitScoreAndPopulateLeaderboard();

        if (PlayerData.instance.AnyMissionComplete())
            StartCoroutine(missionPopup.Open());
        else
            missionPopup.gameObject.SetActive(false);

        CreditCoins();

        if (MusicPlayer.instance.GetStem(0) != gameOverTheme)
        {
            MusicPlayer.instance.SetStem(0, gameOverTheme);
            StartCoroutine(MusicPlayer.instance.RestartAllStems());
        }
    }

    public override void Exit(AState to)
    {
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
        string displayName = GetCurrentPlayerName();
        fullLeaderboard.forcePlayerDisplay = false;
        fullLeaderboard.displayPlayer = true;
        fullLeaderboard.playerEntry.playerName.text = displayName;
        fullLeaderboard.playerEntry.score.text = trackManager.score.ToString();

        fullLeaderboard.Open();
    }

    public void GoToStore()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("shop", UnityEngine.SceneManagement.LoadSceneMode.Additive);
    }


    public void GoToLoadout()
    {
        trackManager.isRerun = false;
        manager.SwitchState("Loadout");
    }

    public void RunAgain()
    {
        trackManager.isRerun = false;
        manager.SwitchState("Game");
    }

    protected void CreditCoins()
    {
        PlayerData.instance.Save();

#if UNITY_ANALYTICS
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
        string displayName = GetCurrentPlayerName();
        if (!string.IsNullOrEmpty(displayName))
            PlayerData.instance.previousName = displayName;

        if (SupabaseClient.instance == null || !SupabaseClient.instance.HasLocalPlayer)
            PlayerData.instance.InsertScore(trackManager.score, displayName);

        CharacterCollider.DeathEvent de = trackManager.characterController.characterCollider.deathData;
#if UNITY_ANALYTICS
        AnalyticsEvent.GameOver(null, new Dictionary<string, object> {
            { "coins", de.coins },
            { "premium", de.premium },
            { "score", de.score },
            { "distance", de.worldDistance },
            { "obstacle",  de.obstacleType },
            { "theme", de.themeUsed },
            { "character", de.character },
        });
#endif

        PlayerData.instance.Save();

        trackManager.End();
    }

    void SubmitScoreAndPopulateLeaderboard()
    {
        SupabaseClient client = SupabaseClient.instance;
        if (client != null && client.HasLocalPlayer)
        {
            client.SubmitScore(trackManager.score, result =>
            {
                miniLeaderboard.Populate();
            });
        }
        else
        {
            miniLeaderboard.Populate();
        }
    }

    string GetCurrentPlayerName()
    {
        if (SupabaseClient.instance != null && !string.IsNullOrEmpty(SupabaseClient.instance.DisplayName))
            return SupabaseClient.instance.DisplayName;

        if (!string.IsNullOrEmpty(PlayerData.instance.previousName))
            return PlayerData.instance.previousName;

        return "Player";
    }
}
