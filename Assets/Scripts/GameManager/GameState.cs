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

    [Header("Loading")]
    public Color loadingOverlayColor = new Color(0.02f, 0.06f, 0.09f, 1.0f);
    public string loadingMessage = "Loading...";

    [Header("Prefabs")]
    public GameObject PowerupIconPrefab;

    public Modifier currentModifier = new Modifier();

    protected bool m_Finished;
    protected float m_TimeSinceStart;
    protected List<PowerupIcon> m_PowerupIcons = new List<PowerupIcon>();
    protected Image[] m_LifeHearts;

    protected RectTransform m_CountdownRectTransform;
    protected bool m_WasMoving;
    protected GameObject m_LoadingOverlay;
    protected Text m_LoadingOverlayText;

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
        StopAllCoroutines();
        HideGameOverUI();

        if (pauseMenu != null)
            pauseMenu.gameObject.SetActive(false);
        if (wholeUI != null)
            wholeUI.gameObject.SetActive(false);
        if (canvas != null)
            canvas.gameObject.SetActive(false);

        ClearPowerup();
    }

    public void StartGame()
    {
        if (canvas != null)
            canvas.gameObject.SetActive(true);
        if (pauseMenu != null)
            pauseMenu.gameObject.SetActive(false);
        if (wholeUI != null)
            wholeUI.gameObject.SetActive(false);
        if (pauseButton != null)
            pauseButton.gameObject.SetActive(false);
        SetLoadingOverlayVisible(true);
        HideGameOverUI();

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

        if (!AudioListener.pause)
        {
            SetLoadingOverlayVisible(false);
            SetGameplayUIVisible(true);
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

            if (!Addressables.ReleaseInstance(toRemove[i].gameObject))
                Destroy(toRemove[i].gameObject);
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
            SetGameplayUIVisible(false);
            SetLoadingOverlayVisible(true);
            if (m_LoadingOverlayText != null)
                m_LoadingOverlayText.text = trackManager.loadFailed ? "Load failed\nCheck Console" : loadingMessage;
            if (countdownText != null)
                countdownText.gameObject.SetActive(false);
            return;
        }
        else if (trackManager.timeToStart >= 0)
        {
            if (!AudioListener.pause)
            {
                SetLoadingOverlayVisible(false);
                SetGameplayUIVisible(true);
            }
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

    protected void SetGameplayUIVisible(bool visible)
    {
        if (wholeUI != null && wholeUI.gameObject.activeSelf != visible)
            wholeUI.gameObject.SetActive(visible);
        if (pauseButton != null && pauseButton.gameObject.activeSelf != visible)
            pauseButton.gameObject.SetActive(visible);
    }

    protected void SetLoadingOverlayVisible(bool visible)
    {
        EnsureLoadingOverlay();
        if (m_LoadingOverlay != null && m_LoadingOverlay.activeSelf != visible)
            m_LoadingOverlay.SetActive(visible);
    }

    protected void EnsureLoadingOverlay()
    {
        if (m_LoadingOverlay != null || canvas == null)
            return;

        GameObject overlay = new GameObject("LoadingOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.localScale = Vector3.one;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = loadingOverlayColor;
        overlayImage.raycastTarget = true;

        GameObject message = new GameObject("Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        message.transform.SetParent(overlay.transform, false);

        RectTransform messageRect = message.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0.5f, 0.5f);
        messageRect.anchorMax = new Vector2(0.5f, 0.5f);
        messageRect.pivot = new Vector2(0.5f, 0.5f);
        messageRect.anchoredPosition = Vector2.zero;
        messageRect.sizeDelta = new Vector2(720f, 180f);
        messageRect.localScale = Vector3.one;

        m_LoadingOverlayText = message.GetComponent<Text>();
        m_LoadingOverlayText.text = loadingMessage;
        m_LoadingOverlayText.alignment = TextAnchor.MiddleCenter;
        m_LoadingOverlayText.color = Color.white;
        m_LoadingOverlayText.fontSize = 48;
        m_LoadingOverlayText.resizeTextForBestFit = true;
        m_LoadingOverlayText.resizeTextMinSize = 24;
        m_LoadingOverlayText.resizeTextMaxSize = 56;
        m_LoadingOverlayText.font = countdownText != null && countdownText.font != null
            ? countdownText.font
            : Resources.GetBuiltinResource<Font>("Arial.ttf");
        m_LoadingOverlayText.raycastTarget = false;

        m_LoadingOverlay = overlay;
    }

    IEnumerator WaitForGameOver()
    {
        m_Finished = true;
        trackManager.StopMove();

        Shader.SetGlobalFloat("_BlinkingValue", 0.0f);

        yield return new WaitForSeconds(2.0f);

        if (manager == null || manager.topState != this)
            yield break;

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
        if (premiumForLifeButton != null)
            premiumForLifeButton.interactable = PlayerData.instance.premium >= 3;
        if (premiumCurrencyOwned != null)
            premiumCurrencyOwned.text = PlayerData.instance.premium.ToString();

        ClearPowerup();

        if (gameOverPopup != null)
            gameOverPopup.SetActive(true);
    }

    public void HideGameOverUI()
    {
        if (gameOverPopup != null)
            gameOverPopup.SetActive(false);
    }

    public void GameOver()
    {
        HideGameOverUI();
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
