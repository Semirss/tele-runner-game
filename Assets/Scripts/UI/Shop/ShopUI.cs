using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif

public class ShopUI : MonoBehaviour
{
    public ConsumableDatabase consumableDatabase;

    public ShopItemList itemList;

    [Header("UI")]
    public Text coinCounter;
    public Text premiumCounter;
    public Button cheatButton;

    protected ShopList m_OpenList;

    protected const int k_CheatCoins = 1000000;
    protected const int k_CheatPremium = 1000;
    RectTransform safePanel;
    Rect lastSafeArea;
    Vector2Int lastScreenSize;

    void Awake()
    {
        RemoveOldShopSection("CharacterList");
        RemoveOldShopSection("ThemeList");
        RemoveOldShopSection("CharacterAccessoriesList");
        RemoveOldShopSection("TabsSwitch");
        RemoveOldShopSection("IAPPopup");
        RemoveOldShopSection("AddCoins");
        SetupSafeLayout();
    }

    void SetupSafeLayout()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.matchWidthOrHeight = 0f;
        safePanel = transform.Find("Background") as RectTransform;
        if (safePanel == null) return;

        GameObject backdrop = new GameObject("Store full-screen backdrop",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backdrop.layer = 5;
        RectTransform rect = backdrop.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();
        Image image = backdrop.GetComponent<Image>();
        image.color = new Color32(4, 30, 86, 255);
        image.raycastTarget = false;
        ApplySafeLayout();
    }

    void ApplySafeLayout()
    {
        if (safePanel == null || Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        Vector2Int size = new Vector2Int(Screen.width, Screen.height);
        if (safe == lastSafeArea && size == lastScreenSize) return;
        lastSafeArea = safe;
        lastScreenSize = size;
        float sideInset = Mathf.Min(16f, safe.width * .025f);
        safe.xMin += sideInset;
        safe.xMax -= sideInset;
        safe.yMin += 8f;
        safe.yMax -= 8f;
        safePanel.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safePanel.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        safePanel.offsetMin = safePanel.offsetMax = Vector2.zero;
    }

    void Start()
    {
        PlayerData.Create();

        consumableDatabase.Load();

#if UNITY_ANALYTICS
        AnalyticsEvent.StoreOpened(StoreType.Soft);
#endif

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        cheatButton.interactable = false;
#else
        cheatButton.interactable = true;
#endif

        m_OpenList = itemList;
        if (itemList != null)
            itemList.Open();
    }

    void Update()
    {
        ApplySafeLayout();
        if (coinCounter != null)
            coinCounter.text = PlayerData.instance.coins.ToString();

        if (premiumCounter != null)
            premiumCounter.text = PlayerData.instance.premium.ToString();
    }

    public void OpenItemList()
    {
        if (m_OpenList != null && m_OpenList != itemList)
            m_OpenList.Close();

        if (itemList != null)
            itemList.Open();

        m_OpenList = itemList;
    }


    public void LoadScene(string scene)
    {
        SceneManager.LoadScene(scene, LoadSceneMode.Single);
    }

    public void CloseScene()
    {
        StartCoroutine(CloseSceneRoutine());
    }

    IEnumerator CloseSceneRoutine()
    {
        yield return null;
        SceneManager.UnloadSceneAsync("shop");

        LoadoutState loadoutState = GameManager.instance.topState as LoadoutState;
        if (loadoutState != null)
            loadoutState.Refresh();
    }

    public void CheatCoin()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif

        PlayerData.instance.coins += k_CheatCoins;
        PlayerData.instance.premium += k_CheatPremium;
        PlayerData.instance.Save();
    }

    void RemoveOldShopSection(string sectionName)
    {
        Transform section = FindChildRecursive(transform, sectionName);
        if (section != null)
            Destroy(section.gameObject);
    }

    Transform FindChildRecursive(Transform root, string childName)
    {
        foreach (Transform child in root)
        {
            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
