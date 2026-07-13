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

    void Awake()
    {
        RemoveOldShopSection("CharacterList");
        RemoveOldShopSection("ThemeList");
        RemoveOldShopSection("CharacterAccessoriesList");
        RemoveOldShopSection("TabsSwitch");
        RemoveOldShopSection("IAPPopup");
        RemoveOldShopSection("AddCoins");
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
