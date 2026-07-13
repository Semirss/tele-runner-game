using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Pushed on top of the GameManager during gameplay. Takes care of initializing all the UI and starts the TrackManager.
/// </summary>
public class GameState : AState
{
    static int s_DeadHash = Animator.StringToHash("Dead");

    public Canvas canvas;
    public TrackManager trackManager;

    public AudioClip gameTheme;

    [Header("UI")]
    public Text coinText;
    public Text premiumText;
    public Text scoreText;
    public Text distanceText;
    public Text multiplierText;
    public Text countdownText;
    public RectTransform powerupZone;
    public RectTransform lifeRectTransform;

    public RectTransform pauseMenu;
    public RectTransform wholeUI;
    public Button pauseButton;

    public Image inventoryIcon;

    public GameObject gameOverPopup;
    public Button premiumForLifeButton;
    public Text premiumCurrencyOwned;

    [Header("Prefabs")]
    public GameObject PowerupIconPrefab;

    public Modifier currentModifier = new Modifier();

    protected bool m_Finished;
    protected float m_TimeSinceStart;
    protected List<PowerupIcon> m_PowerupIcons = new List<PowerupIcon>();
    protected Image[] m_LifeHearts;

    protected RectTransform m_CountdownRectTransform;
    protected bool m_WasMoving;

    protected bool m_GameoverSelectionDone = false;

    protected int k_MaxLives = 3;

    public override void Enter(AState from)
    {
        m_CountdownRectTransform = countdownText.GetComponent<RectTransform>();

        m_LifeHearts = new Image[k_MaxLives];
        for (int i = 0; i < k_MaxLives; ++i)
            m_LifeHearts[i] = lifeRectTransform.GetChild(i).GetComponent<Image>();

        if (MusicPlayer.instance.GetStem(0) != gameTheme)
        {
            MusicPlayer.instance.SetStem(0, gameTheme);
            CoroutineHandler.StartStaticCoroutine(MusicPlayer.instance.RestartAllStems());
        }

        m_GameoverSelectionDone = false;

        StartGame();
    }

    public override void Exit(AState to)
    {
        canvas.gameObject.SetActive(false);
        ClearPowerup();
    }

    public void StartGame()
    {
        canvas.gameObject.SetActive(true);
        pauseMenu.gameObject.SetActive(false);
        wholeUI.gameObject.SetActive(true);
        pauseButton.gameObject.SetActive(true);
        gameOverPopup.SetActive(false);

        if (!trackManager.isRerun)
        {
            m_TimeSinceStart = 0;
            trackManager.characterController.currentLife = trackManager.characterController.maxLife;
        }

        currentModifier.OnRunStart(this);

        m_Finished = false;
        m_PowerupIcons.Clear();

        StartCoroutine(trackManager.Begin());
    }

    public override string GetName()
    {
        return "Game";
    }

    public override void Tick()
    {
        if (m_Finished)
            return;

        if (!trackManager.isLoaded)
        {
            UpdateUI();
            return;
        }

        CharacterInputController chrCtrl = trackManager.characterController;
        m_TimeSinceStart += Time.deltaTime;

        if (chrCtrl.currentLife <= 0)
        {
            pauseButton.gameObject.SetActive(false);
            chrCtrl.CleanConsumable();
            chrCtrl.character.animator.SetBool(s_DeadHash, true);
            chrCtrl.characterCollider.koParticle.gameObject.SetActive(true);
            StartCoroutine(WaitForGameOver());
        }

        List<Consumable> toRemove = new List<Consumable>();
        List<PowerupIcon> toRemoveIcon = new List<PowerupIcon>();

        for (int i = 0; i < chrCtrl.consumables.Count; ++i)
        {
            PowerupIcon icon = null;
            for (int j = 0; j < m_PowerupIcons.Count; ++j)
            {
                if (m_PowerupIcons[j].linkedConsumable == chrCtrl.consumables[i])
                {
                    icon = m_PowerupIcons[j];
                    break;
                }
            }

            chrCtrl.consumables[i].Tick(chrCtrl);
            if (!chrCtrl.consumables[i].active)
            {
                toRemove.Add(chrCtrl.consumables[i]);
                toRemoveIcon.Add(icon);
            }
            else if (icon == null)
            {
                GameObject o = Instantiate(PowerupIconPrefab);
                icon = o.GetComponent<PowerupIcon>();

                icon.linkedConsumable = chrCtrl.consumables[i];
                icon.transform.SetParent(powerupZone, false);

                m_PowerupIcons.Add(icon);
            }
        }

        for (int i = 0; i < toRemove.Count; ++i)
        {
            toRemove[i].Ended(trackManager.characterController);

            Addressables.ReleaseInstance(toRemove[i].gameObject);
            if (toRemoveIcon[i] != null)
                Destroy(toRemoveIcon[i].gameObject);

            chrCtrl.consumables.Remove(toRemove[i]);
            m_PowerupIcons.Remove(toRemoveIcon[i]);
        }

        UpdateUI();

        currentModifier.OnRunTick(this);
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) Pause();
    }

    void OnApplicationFocus(bool focusStatus)
    {
        if (!focusStatus) Pause();
    }

    public void Pause(bool displayMenu = true)
    {
        if (m_Finished || AudioListener.pause == true)
            return;

        AudioListener.pause = true;
        Time.timeScale = 0;

        pauseButton.gameObject.SetActive(false);
        pauseMenu.gameObject.SetActive(displayMenu);
        wholeUI.gameObject.SetActive(false);
        m_WasMoving = trackManager.isMoving;
        trackManager.StopMove();
    }

    public void Resume()
    {
        Time.timeScale = 1.0f;
        pauseButton.gameObject.SetActive(true);
        pauseMenu.gameObject.SetActive(false);
        wholeUI.gameObject.SetActive(true);
        if (m_WasMoving)
            trackManager.StartMove(false);

        AudioListener.pause = false;
    }

    public void QuitToLoadout()
    {
        Time.timeScale = 1.0f;
        AudioListener.pause = false;
        trackManager.End();
        trackManager.isRerun = false;
        PlayerData.instance.Save();
        manager.SwitchState("Loadout");
    }

    protected void UpdateUI()
    {
        coinText.text = trackManager.characterController.coins.ToString();
        premiumText.text = trackManager.characterController.premium.ToString();

        for (int i = 0; i < 3; ++i)
        {
            if (trackManager.characterController.currentLife > i)
                m_LifeHearts[i].color = Color.white;
            else
                m_LifeHearts[i].color = Color.black;
        }

        scoreText.text = trackManager.score.ToString();
        multiplierText.text = "x " + trackManager.multiplier;

        distanceText.text = Mathf.FloorToInt(trackManager.worldDistance).ToString() + "m";

        if (!trackManager.isLoaded)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = "Loading...";
            m_CountdownRectTransform.localScale = Vector3.one;
        }
        else if (trackManager.timeToStart >= 0)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = Mathf.Ceil(trackManager.timeToStart).ToString();
            m_CountdownRectTransform.localScale = Vector3.one * (1.0f - (trackManager.timeToStart - Mathf.Floor(trackManager.timeToStart)));
        }
        else
        {
            m_CountdownRectTransform.localScale = Vector3.zero;
        }

        if (trackManager.characterController.inventory != null)
        {
            inventoryIcon.transform.parent.gameObject.SetActive(true);
            inventoryIcon.sprite = trackManager.characterController.inventory.icon;
        }
        else
        {
            inventoryIcon.transform.parent.gameObject.SetActive(false);
        }
    }

    IEnumerator WaitForGameOver()
    {
        m_Finished = true;
        trackManager.StopMove();

        Shader.SetGlobalFloat("_BlinkingValue", 0.0f);

        yield return new WaitForSeconds(2.0f);
        if (currentModifier.OnRunEnd(this))
        {
            if (trackManager.isRerun)
                manager.SwitchState("GameOver");
            else
                OpenGameOverPopup();
        }
    }

    protected void ClearPowerup()
    {
        for (int i = 0; i < m_PowerupIcons.Count; ++i)
        {
            if (m_PowerupIcons[i] != null)
                Destroy(m_PowerupIcons[i].gameObject);
        }

        if (trackManager != null && trackManager.characterController != null && trackManager.characterController.powerupSource != null && trackManager.characterController.powerupSource.enabled)
            trackManager.characterController.powerupSource.Stop();

        m_PowerupIcons.Clear();
    }

    public void OpenGameOverPopup()
    {
        premiumForLifeButton.interactable = PlayerData.instance.premium >= 3;
        premiumCurrencyOwned.text = PlayerData.instance.premium.ToString();

        ClearPowerup();

        gameOverPopup.SetActive(true);
    }

    public void GameOver()
    {
        manager.SwitchState("GameOver");
    }

    public void PremiumForLife()
    {
        if (m_GameoverSelectionDone)
            return;

        m_GameoverSelectionDone = true;

        PlayerData.instance.premium -= 3;
        trackManager.characterController.premium -= Mathf.Min(trackManager.characterController.premium, 3);

        SecondWind();
    }

    public void SecondWind()
    {
        trackManager.characterController.currentLife = 1;
        trackManager.isRerun = true;
        StartGame();
    }
}
