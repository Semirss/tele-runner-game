using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class PlayerRegistrationUI : MonoBehaviour
{
    enum ProfileMode
    {
        Register,
        SignIn
    }

    static PlayerRegistrationUI s_Instance;

    Text m_TitleText;
    InputField m_NameInput;
    InputField m_PhoneInput;
    InputField m_EmailInput;
    InputField m_PasswordInput;
    Button m_RegisterModeButton;
    Button m_SignInModeButton;
    Button m_RegisterButton;
    Button m_SignInButton;
    Text m_StatusText;
    RectTransform m_PanelRect;
    RegistrationSceneStyle m_Style;
    ProfileMode m_Mode = ProfileMode.Register;
    [SerializeField, HideInInspector] int m_DesignVersion;

    Color Navy => m_Style != null ? m_Style.navy : new Color32(3, 27, 82, 255);
    Color Blue => m_Style != null ? m_Style.blue : new Color32(14, 101, 195, 255);
    Color Yellow => m_Style != null ? m_Style.yellow : new Color32(255, 210, 24, 255);
    Color Paper => m_Style != null ? m_Style.paper : new Color32(250, 251, 253, 255);

    public static void Show()
    {
        Ensure();
        s_Instance.gameObject.SetActive(true);
        Canvas canvas = s_Instance.GetComponent<Canvas>();
        if (canvas != null) canvas.enabled = true;
        s_Instance.SetMode(ProfileMode.Register);
    }

    public static void Ensure()
    {
        if (s_Instance != null)
            return;

        s_Instance = FindObjectOfType<PlayerRegistrationUI>();
        if (s_Instance == null)
        {
            GameObject root = new GameObject("PlayerRegistrationUI", typeof(RectTransform));
            s_Instance = root.AddComponent<PlayerRegistrationUI>();
        }
    }

    void Awake()
    {
        if (!Application.isPlaying) return;
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_Instance = this;
        if (GetComponent<Canvas>() == null)
            Build();
        else
            BindExisting();
        GetComponent<Canvas>().enabled = false;
    }

#if UNITY_EDITOR
    void OnEnable()
    {
        if (!Application.isPlaying)
            EditorApplication.delayCall += BuildEditorForm;
    }

    void OnDisable()
    {
        if (!Application.isPlaying)
            EditorApplication.delayCall -= BuildEditorForm;
    }

    void BuildEditorForm()
    {
        EditorApplication.delayCall -= BuildEditorForm;
        if (this == null || Application.isPlaying ||
            (m_DesignVersion >= 2 && GetComponent<Canvas>() != null)) return;
        if (GetComponent<Canvas>() != null)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            DestroyImmediate(GetComponent<GraphicRaycaster>());
            DestroyImmediate(GetComponent<CanvasScaler>());
            DestroyImmediate(GetComponent<Canvas>());
        }
        Build();
        EditorUtility.SetDirty(gameObject);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    void BindExisting()
    {
        m_Style = FindObjectOfType<RegistrationSceneStyle>();
        Transform panel = transform.Find("Registration Panel");
        if (panel == null) return;
        m_PanelRect = panel.GetComponent<RectTransform>();
        m_TitleText = panel.Find("Title").GetComponent<Text>();
        Transform modes = panel.Find("ModeButtons");
        m_RegisterModeButton = modes.Find("Register").GetComponent<Button>();
        m_SignInModeButton = modes.Find("Sign in").GetComponent<Button>();
        m_NameInput = panel.Find("Name").GetComponent<InputField>();
        m_PhoneInput = panel.Find("Phone number").GetComponent<InputField>();
        m_EmailInput = panel.Find("Email (optional)").GetComponent<InputField>();
        m_PasswordInput = panel.Find("Password").GetComponent<InputField>();
        m_RegisterButton = panel.Find("Register").GetComponent<Button>();
        m_SignInButton = panel.Find("Sign in").GetComponent<Button>();
        m_StatusText = panel.Find("Status").GetComponent<Text>();
        m_RegisterModeButton.onClick.AddListener(() => SetMode(ProfileMode.Register));
        m_SignInModeButton.onClick.AddListener(() => SetMode(ProfileMode.SignIn));
        m_RegisterButton.onClick.AddListener(Register);
        m_SignInButton.onClick.AddListener(SignIn);
        EnsureEventSystem();
    }

    void Build()
    {
        m_Style = FindObjectOfType<RegistrationSceneStyle>();
        m_DesignVersion = 2;
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0f;

        gameObject.AddComponent<GraphicRaycaster>();
        if (Application.isPlaying) EnsureEventSystem();

        Image backdrop = CreateImage("Backdrop", transform,
            m_Style != null ? m_Style.backdrop : new Color(0.008f, 0.035f, 0.12f, 0.88f));
        Stretch(backdrop.rectTransform);

        float width = m_Style != null ? m_Style.panelWidth : 820f;
        float height = m_Style != null ? m_Style.registerHeight : 1080f;
        GameObject panel = CreateRect("Registration Panel", transform, new Vector2(width, height));
        m_PanelRect = panel.GetComponent<RectTransform>();
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = m_Style != null ? m_Style.panel : Navy;
        Shadow panelShadow = panel.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, .55f);
        panelShadow.effectDistance = new Vector2(12f, -16f);
        Outline panelEdge = panel.AddComponent<Outline>();
        panelEdge.effectColor = Blue;
        panelEdge.effectDistance = new Vector2(4f, 4f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        int padding = Mathf.RoundToInt(m_Style != null ? m_Style.sidePadding : 48f);
        layout.padding = new RectOffset(padding, padding, 36, 32);
        layout.spacing = m_Style != null ? m_Style.rowSpacing : 16f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;

        Image ticketEdge = CreateImage("Yellow Ticket Edge", panel.transform, Yellow);
        ticketEdge.rectTransform.anchorMin = new Vector2(0f, 0f);
        ticketEdge.rectTransform.anchorMax = new Vector2(0f, 1f);
        ticketEdge.rectTransform.offsetMin = Vector2.zero;
        ticketEdge.rectTransform.offsetMax = new Vector2(12f, 0f);
        ticketEdge.raycastTarget = false;
        ticketEdge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        GameObject header = CreateRect("Transit Header", panel.transform, new Vector2(0f, 140f));
        header.AddComponent<Image>().color = Blue;
        SetPreferredHeight(header, 140f);

        Text brand = CreateText("Tele Runner Brand", header.transform,
            m_Style != null ? m_Style.brandText : "TELE RUNNER", 48, TextAnchor.MiddleLeft);
        brand.color = Paper;
        brand.fontStyle = FontStyle.Bold;
        RectTransform brandRect = brand.rectTransform;
        brandRect.anchorMin = brandRect.anchorMax = new Vector2(0f, 1f);
        brandRect.pivot = new Vector2(0f, 1f);
        brandRect.anchoredPosition = new Vector2(30f, -17f);
        brandRect.sizeDelta = new Vector2(470f, 66f);

        Text partner = CreateText("Partner", header.transform,
            m_Style != null ? m_Style.partnerText : "SUN CHIPS", 25, TextAnchor.MiddleRight);
        partner.color = Yellow;
        partner.fontStyle = FontStyle.Bold;
        RectTransform partnerRect = partner.rectTransform;
        partnerRect.anchorMin = partnerRect.anchorMax = new Vector2(1f, 1f);
        partnerRect.pivot = new Vector2(1f, 1f);
        partnerRect.anchoredPosition = new Vector2(-28f, -31f);
        partnerRect.sizeDelta = new Vector2(230f, 45f);

        Image line = CreateImage("Route Line", header.transform, Paper);
        line.rectTransform.anchorMin = new Vector2(0f, 0f);
        line.rectTransform.anchorMax = new Vector2(1f, 0f);
        line.rectTransform.offsetMin = new Vector2(30f, 37f);
        line.rectTransform.offsetMax = new Vector2(-30f, 41f);
        Image stopOne = CreateImage("Green Stop", header.transform, new Color32(0, 201, 111, 255));
        PlaceHeaderStop(stopOne.rectTransform, 86f);
        Image stopTwo = CreateImage("Yellow Stop", header.transform, Yellow);
        PlaceHeaderStop(stopTwo.rectTransform, 118f);
        Image headerAccent = CreateImage("Header Yellow Edge", header.transform, Yellow);
        headerAccent.rectTransform.anchorMin = new Vector2(0f, 0f);
        headerAccent.rectTransform.anchorMax = new Vector2(1f, 0f);
        headerAccent.rectTransform.offsetMin = new Vector2(0f, 0f);
        headerAccent.rectTransform.offsetMax = new Vector2(0f, 8f);

        m_TitleText = CreateText("Title", panel.transform,
            m_Style != null ? m_Style.registerTitle : "CREATE YOUR PLAYER", 48, TextAnchor.MiddleLeft);
        m_TitleText.color = Color.white;
        m_TitleText.fontStyle = FontStyle.BoldAndItalic;
        m_TitleText.resizeTextForBestFit = true;
        m_TitleText.resizeTextMinSize = 28;
        m_TitleText.resizeTextMaxSize = 48;
        SetPreferredHeight(m_TitleText.gameObject, 68f);

        Text kicker = CreateText("Route Tagline", panel.transform,
            "ADDIS ABABA  /  YOUR RUN STARTS HERE", 22, TextAnchor.MiddleLeft);
        kicker.color = Yellow;
        kicker.fontStyle = FontStyle.Bold;
        SetPreferredHeight(kicker.gameObject, 32f);

        GameObject modeButtons = CreateRect("ModeButtons", panel.transform, new Vector2(0f, 68f));
        HorizontalLayoutGroup modeLayout = modeButtons.AddComponent<HorizontalLayoutGroup>();
        modeLayout.spacing = 12;
        modeLayout.childControlWidth = true;
        modeLayout.childForceExpandWidth = true;
        modeLayout.childControlHeight = true;
        modeLayout.childForceExpandHeight = true;
        SetPreferredHeight(modeButtons, 68f);

        m_RegisterModeButton = CreateButton(modeButtons.transform, "Register", () => SetMode(ProfileMode.Register));
        m_SignInModeButton = CreateButton(modeButtons.transform, "Sign in", () => SetMode(ProfileMode.SignIn));

        m_NameInput = CreateInput(panel.transform, "Name", false);
        m_PhoneInput = CreateInput(panel.transform, "Phone number", false);
        m_EmailInput = CreateInput(panel.transform, "Email (optional)", false);
        m_PasswordInput = CreateInput(panel.transform, "Password", true);

        m_RegisterButton = CreateButton(panel.transform, "Register", Register);
        m_SignInButton = CreateButton(panel.transform, "Sign in", SignIn);
        StylePrimaryButton(m_RegisterButton, "CREATE PLAYER");
        StylePrimaryButton(m_SignInButton, "SIGN IN");

        m_StatusText = CreateText("Status", panel.transform, "", 24, TextAnchor.MiddleCenter);
        m_StatusText.color = Paper;
        SetPreferredHeight(m_StatusText.gameObject, 62f);

        SetMode(ProfileMode.Register);
    }

    void SetMode(ProfileMode mode)
    {
        m_Mode = mode;
        bool registerMode = m_Mode == ProfileMode.Register;

        if (m_TitleText != null)
            m_TitleText.text = registerMode
                ? (m_Style != null ? m_Style.registerTitle : "CREATE YOUR PLAYER")
                : (m_Style != null ? m_Style.signInTitle : "WELCOME BACK");

        if (m_PanelRect != null)
        {
            Vector2 size = m_PanelRect.sizeDelta;
            size.y = m_Style != null ? (registerMode ? m_Style.registerHeight : m_Style.signInHeight)
                : (registerMode ? 1080f : 890f);
            m_PanelRect.sizeDelta = size;
            FitPanelToScreen();
        }

        if (m_NameInput != null)
            m_NameInput.gameObject.SetActive(registerMode);
        if (m_EmailInput != null)
            m_EmailInput.gameObject.SetActive(registerMode);
        if (m_RegisterButton != null)
            m_RegisterButton.gameObject.SetActive(registerMode);
        if (m_SignInButton != null)
            m_SignInButton.gameObject.SetActive(!registerMode);

        SetButtonSelected(m_RegisterModeButton, registerMode);
        SetButtonSelected(m_SignInModeButton, !registerMode);
        ResetStatus();
    }

    void Register()
    {
        string displayName = Clean(m_NameInput.text);
        string phone = Clean(m_PhoneInput.text);
        string email = Clean(m_EmailInput.text);
        string password = m_PasswordInput.text;

        if (!ValidateCommon(phone, password))
            return;

        if (string.IsNullOrEmpty(displayName))
        {
            SetStatus("Name is required.", true);
            return;
        }

        if (!string.IsNullOrEmpty(email) && (!email.Contains("@") || !email.Contains(".")))
        {
            SetStatus("Email is optional, but it must be valid if entered.", true);
            return;
        }

        SetBusy(true, "Registering...");
        SupabaseClient.instance.Register(displayName, phone, email, password, OnProfileResult);
    }

    void SignIn()
    {
        string phone = Clean(m_PhoneInput.text);
        string password = m_PasswordInput.text;

        if (!ValidateCommon(phone, password))
            return;

        SetBusy(true, "Signing in...");
        SupabaseClient.instance.SignIn(phone, password, OnProfileResult);
    }

    void OnProfileResult(SupabaseClient.SupabaseResult result)
    {
        SetBusy(false, result == null ? "Supabase did not return a result." : result.message);

        if (result == null)
            return;

        if (!result.success)
        {
            SetStatus(result.message, true);
            return;
        }

        if (SupabaseClient.instance != null && SupabaseClient.instance.HasLocalPlayer)
        {
            if (PlayerData.instance != null)
                PlayerData.instance.previousName = SupabaseClient.instance.DisplayName;

            gameObject.SetActive(false);
        }
    }

    bool ValidateCommon(string phone, string password)
    {
        if (SupabaseClient.instance == null || !SupabaseClient.instance.IsConfigured)
        {
            SetStatus("Configure Assets/Resources/SupabaseConfig.json first.", true);
            return false;
        }

        if (string.IsNullOrEmpty(phone))
        {
            SetStatus("Phone number is required.", true);
            return false;
        }

        if (string.IsNullOrEmpty(password))
        {
            SetStatus("Password is required.", true);
            return false;
        }

        return true;
    }

    void ResetStatus()
    {
        if (m_StatusText == null)
            return;

        if (SupabaseClient.instance != null && !SupabaseClient.instance.IsConfigured)
            SetStatus("Configure Assets/Resources/SupabaseConfig.json before registering.", true);
        else if (m_Mode == ProfileMode.Register)
            SetStatus("YOUR ROUTE BEGINS HERE", false);
        else
            SetStatus("PICK UP WHERE YOU LEFT OFF", false);
    }

    void SetBusy(bool busy, string message)
    {
        m_RegisterModeButton.interactable = !busy;
        m_SignInModeButton.interactable = !busy;
        m_RegisterButton.interactable = !busy;
        m_SignInButton.interactable = !busy;
        SetStatus(message, false);
    }

    void SetButtonSelected(Button button, bool selected)
    {
        if (button == null || button.targetGraphic == null)
            return;

        Image image = button.targetGraphic as Image;
        if (image != null)
            image.color = selected ? Yellow : Blue;
        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
            label.color = selected ? Navy : Paper;
    }

    void SetStatus(string message, bool isError)
    {
        m_StatusText.text = message;
        m_StatusText.color = isError ? new Color(1f, 0.73f, 0.48f, 1f) : Paper;
    }

    string Clean(string value)
    {
        return string.IsNullOrEmpty(value) ? "" : value.Trim();
    }

    InputField CreateInput(Transform parent, string placeholder, bool password)
    {
        GameObject root = CreateRect(placeholder, parent, new Vector2(0f, 76f));
        Image image = root.AddComponent<Image>();
        image.color = Paper;
        Image stripe = CreateImage("Yellow Field Edge", root.transform, Yellow);
        stripe.rectTransform.anchorMin = new Vector2(0f, 0f);
        stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
        stripe.rectTransform.offsetMin = Vector2.zero;
        stripe.rectTransform.offsetMax = new Vector2(11f, 0f);
        stripe.raycastTarget = false;

        InputField field = root.AddComponent<InputField>();
        field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
        field.targetGraphic = image;

        Text text = CreateText("Text", root.transform, "", 26, TextAnchor.MiddleLeft);
        text.color = Navy;
        Stretch(text.rectTransform, 34f, 10f, 24f, 10f);
        field.textComponent = text;

        Text placeholderText = CreateText("Placeholder", root.transform, placeholder, 26, TextAnchor.MiddleLeft);
        placeholderText.color = m_Style != null ? m_Style.mutedText : new Color32(91, 113, 153, 255);
        placeholderText.fontStyle = FontStyle.Bold;
        placeholderText.text = placeholder.ToUpperInvariant();
        Stretch(placeholderText.rectTransform, 34f, 10f, 24f, 10f);
        field.placeholder = placeholderText;

        SetPreferredHeight(root, 76f);
        return field;
    }

    Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction click)
    {
        GameObject root = CreateRect(label, parent, new Vector2(0f, 64f));
        Image image = root.AddComponent<Image>();
        image.color = Blue;

        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(click);

        Text text = CreateText("Text", root.transform, label, 28, TextAnchor.MiddleCenter);
        text.color = Paper;
        text.fontStyle = FontStyle.Bold;
        Stretch(text.rectTransform);

        SetPreferredHeight(root, 64f);
        return button;
    }

    void StylePrimaryButton(Button button, string label)
    {
        Image image = button.GetComponent<Image>();
        Sprite ticket = Resources.Load<Sprite>("TransitMenu/RunButton");
        if (ticket != null)
        {
            image.sprite = ticket;
            image.color = Color.white;
            image.type = Image.Type.Simple;
        }
        else image.color = Yellow;
        Text text = button.GetComponentInChildren<Text>();
        text.text = label;
        text.color = Navy;
        text.fontSize = 40;
        text.fontStyle = FontStyle.BoldAndItalic;
        Stretch(text.rectTransform, 95f, 0f, 160f, 0f);
        SetPreferredHeight(button.gameObject, 92f);
    }

    void PlaceHeaderStop(RectTransform rect, float x)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(x, 39f);
        rect.sizeDelta = new Vector2(18f, 18f);
    }

    void FitPanelToScreen()
    {
        RectTransform root = transform as RectTransform;
        if (root == null || m_PanelRect == null) return;
        float width = root.rect.width;
        float height = root.rect.height;
        if (width <= 0f || height <= 0f) return;
        float scale = Mathf.Min(1f, (width - 48f) / m_PanelRect.sizeDelta.x,
            (height - 72f) / m_PanelRect.sizeDelta.y);
        m_PanelRect.localScale = Vector3.one * Mathf.Max(.1f, scale);
    }

    void OnRectTransformDimensionsChange()
    {
        FitPanelToScreen();
    }

    Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor)
    {
        GameObject root = CreateRect(name, parent, new Vector2(0f, 40f));
        Text text = root.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject root = CreateRect(name, parent, Vector2.zero);
        Image image = root.AddComponent<Image>();
        image.color = color;
        return image;
    }

    GameObject CreateRect(string name, Transform parent, Vector2 size)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return root;
    }

    void Stretch(RectTransform rect)
    {
        Stretch(rect, 0f, 0f, 0f, 0f);
    }

    void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    void SetPreferredHeight(GameObject root, float height)
    {
        LayoutElement layout = root.GetComponent<LayoutElement>();
        if (layout == null)
            layout = root.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }
}
