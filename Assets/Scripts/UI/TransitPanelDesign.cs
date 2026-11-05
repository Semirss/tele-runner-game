using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Applies the transit visual language to the existing functional panels.</summary>
public sealed class TransitPanelDesign : MonoBehaviour
{
    static readonly Color Navy = new Color32(4, 30, 86, 255);
    static readonly Color Blue = new Color32(8, 51, 142, 255);
    static readonly Color Yellow = new Color32(255, 211, 24, 255);
    static readonly Color Paper = new Color32(248, 250, 254, 255);
    static readonly Color PaleBlue = new Color32(226, 236, 252, 255);

    readonly HashSet<int> styledRows = new HashSet<int>();
    MissionUI mission;
    SettingPopup settings;
    ShopUI shop;
    bool missionStyled;
    bool settingsStyled;
    bool shopStyled;

    void Awake()
    {
        LoadoutState loadout = GetComponent<LoadoutState>();
        if (loadout != null)
        {
            mission = loadout.missionPopup;
            settings = loadout.inventoryCanvas != null
                ? loadout.inventoryCanvas.GetComponentInChildren<SettingPopup>(true) : null;
        }
    }

    void LateUpdate()
    {
        if (mission != null && mission.gameObject.activeInHierarchy)
        {
            if (!missionStyled) { StyleMission(); missionStyled = true; }
            foreach (MissionEntry row in mission.GetComponentsInChildren<MissionEntry>(true))
                StyleMissionRow(row);
        }
        if (settings != null && settings.gameObject.activeInHierarchy && !settingsStyled)
        {
            StyleSettings();
            settingsStyled = true;
        }
        if (shop == null && Time.frameCount % 20 == 0)
            shop = FindObjectOfType<ShopUI>();
        if (shop != null && shop.gameObject.activeInHierarchy)
        {
            if (!shopStyled) { StyleShop(); shopStyled = true; }
            foreach (ShopItemListItem row in shop.GetComponentsInChildren<ShopItemListItem>(true))
                StyleShopRow(row);
        }
        else if (shop == null) shopStyled = false;
    }

    static Transform Find(Transform root, string path) => root != null ? root.Find(path) : null;

    static void Panel(Transform target, string textureName)
    {
        if (target == null) return;
        Image image = target.GetComponent<Image>();
        Texture2D texture = Resources.Load<Texture2D>("TransitMenu/" + textureName);
        if (image == null || texture == null) return;
        image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(.5f, .5f));
        image.type = Image.Type.Simple;
        image.color = Color.white;
    }

    static void Flat(Image image, Color color)
    {
        if (image == null) return;
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
    }

    static void TextColor(Transform target, Color color, int minimumSize = 0)
    {
        if (target == null) return;
        Text text = target.GetComponent<Text>();
        if (text == null) return;
        text.color = color;
        text.fontStyle = FontStyle.BoldAndItalic;
        if (minimumSize > 0) text.fontSize = Mathf.Max(text.fontSize, minimumSize);
        text.raycastTarget = false;
    }

    static void Accent(Transform parent, string name, Color color, Vector2 anchor, Vector2 size)
    {
        if (parent == null || parent.Find(name) != null) return;
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(LayoutElement));
        go.layer = 5;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        rect.SetAsFirstSibling();
    }

    static void CloseButton(Transform button)
    {
        if (button == null) return;
        Flat(button.GetComponent<Image>(), Yellow);
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = "×";
            label.fontSize = Mathf.Max(label.fontSize, 38);
            TextColor(label.transform, Navy);
            return;
        }
        GameObject go = new GameObject("Transit close symbol", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text));
        go.layer = 5;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(button, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "×";
        text.fontSize = 38;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Navy;
        text.raycastTarget = false;
    }

    void StyleMission()
    {
        Transform panel = Find(mission.transform, "MissionBackground");
        Panel(panel, "MissionPanel");
        TextColor(Find(panel, "Text"), Color.white, 52);
        CloseButton(Find(panel, "CloseButton"));
        Flat(Find(panel, "Divider")?.GetComponent<Image>(), PaleBlue);
        Transform container = Find(panel, "MissionsContainer");
        Flat(container?.GetComponent<Image>(), Color.clear);
        Flat(Find(container, "Scroll View")?.GetComponent<Image>(), Color.clear);
    }

    void StyleMissionRow(MissionEntry row)
    {
        if (row == null) return;
        int id = row.GetInstanceID();
        if (styledRows.Add(id))
        {
            Flat(row.background, Paper);
            Accent(row.transform, "Transit mission marker", Yellow,
                new Vector2(0f, .5f), new Vector2(9f, 125f));
            TextColor(row.descText.transform, Blue);
            TextColor(row.rewardText.transform, Navy);
            TextColor(row.progressText.transform, Navy);
            Flat(row.claimButton.GetComponent<Image>(), Yellow);
            Text claimText = row.claimButton.GetComponentInChildren<Text>(true);
            if (claimText != null) TextColor(claimText.transform, Navy);
        }
        // MissionEntry.FillWithMission resets these colors whenever the list refreshes.
        if (row.background != null) row.background.color = Paper;
        if (row.descText != null) row.descText.color = Blue;
        if (row.progressText != null) row.progressText.color = Navy;
        if (row.rewardText != null) row.rewardText.color = Navy;
    }

    void StyleSettings()
    {
        Transform panel = Find(settings.transform, "Background");
        Panel(panel, "SettingsPanel");
        TextColor(Find(panel, "Title"), Color.white, 50);
        CloseButton(Find(panel, "CloseButton"));
        StyleSlider(settings.masterSlider);
        StyleSlider(settings.musicSlider);
        StyleSlider(settings.masterSFXSlider);
        Transform delete = Find(panel, "DeleteData");
        if (delete != null)
        {
            Flat(delete.GetComponent<Image>(), PaleBlue);
            TextColor(Find(delete, "Text"), Navy);
        }
    }

    static void StyleSlider(Slider slider)
    {
        if (slider == null) return;
        Text label = slider.GetComponentInChildren<Text>(true);
        if (label != null) TextColor(label.transform, Navy);
        Flat(Find(slider.transform, "Background")?.GetComponent<Image>(), PaleBlue);
        Flat(Find(slider.transform, "Fill Area/Fill")?.GetComponent<Image>(), Blue);
        Flat(Find(slider.transform, "Handle Slide Area/Handle")?.GetComponent<Image>(), Yellow);
    }

    void StyleShop()
    {
        Transform panel = Find(shop.transform, "Background");
        Panel(panel, "StorePanel");
        Transform title = Find(panel, "StoreTitle");
        TextColor(title, Color.white, 55);
        RectTransform titleRect = title as RectTransform;
        if (titleRect != null)
        {
            titleRect.localScale = Vector3.one;
            titleRect.sizeDelta = new Vector2(620f, 100f);
            titleRect.anchoredPosition = new Vector2(0f, -118f);
        }
        Transform list = Find(panel, "ItemsList");
        RectTransform listRect = list as RectTransform;
        if (listRect != null)
        {
            listRect.anchorMin = Vector2.zero;
            listRect.anchorMax = Vector2.one;
            listRect.pivot = new Vector2(.5f, .5f);
            listRect.offsetMin = new Vector2(28f, 28f);
            listRect.offsetMax = new Vector2(-28f, -300f);
        }
        // ItemsList is also a UI Mask; its graphic must stay opaque for rows to render.
        Flat(list?.GetComponent<Image>(), Paper);
        Transform close = Find(panel, "Button");
        RectTransform closeRect = close as RectTransform;
        if (closeRect != null)
        {
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(0f, 1f);
            closeRect.pivot = new Vector2(0f, 1f);
            closeRect.localScale = Vector3.one;
            closeRect.sizeDelta = new Vector2(88f, 88f);
            closeRect.anchoredPosition = new Vector2(36f, -75f);
        }
        CloseButton(close);
        if (shop.coinCounter != null) TextColor(shop.coinCounter.transform, Navy);
        if (shop.premiumCounter != null) TextColor(shop.premiumCounter.transform, Navy);
    }

    void StyleShopRow(ShopItemListItem row)
    {
        if (row == null || !styledRows.Add(row.GetInstanceID())) return;
        Image background = row.GetComponent<Image>();
        Flat(background, Paper);
        Accent(row.transform, "Transit store marker", Yellow,
            new Vector2(0f, .5f), new Vector2(9f, 125f));
        TextColor(row.nameText.transform, Navy);
        TextColor(row.pricetext.transform, Navy);
        TextColor(row.premiumText.transform, Navy);
        TextColor(row.countText.transform, Blue);
        Image buy = row.buyButton.GetComponent<Image>();
        Flat(buy, Yellow);
        Text buyText = row.buyButton.GetComponentInChildren<Text>(true);
        if (buyText != null) TextColor(buyText.transform, Navy);
    }
}
