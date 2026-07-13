using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    ProfileMode m_Mode = ProfileMode.Register;

    public static void Show()
    {
        Ensure();
        s_Instance.gameObject.SetActive(true);
        s_Instance.SetMode(ProfileMode.Register);
    }

    public static void Ensure()
    {
        if (s_Instance != null)
            return;

        GameObject root = new GameObject("PlayerRegistrationUI");
        s_Instance = root.AddComponent<PlayerRegistrationUI>();
        s_Instance.Build();
    }

    void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_Instance = this;
    }

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        Image backdrop = CreateImage("Backdrop", transform, new Color(0f, 0f, 0f, 0.72f));
        Stretch(backdrop.rectTransform);

        GameObject panel = CreateRect("Panel", transform, new Vector2(680f, 760f));
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.1f, 0.13f, 0.96f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(44, 44, 44, 44);
        layout.spacing = 18;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;

        m_TitleText = CreateText("Title", panel.transform, "Register player", 36, TextAnchor.MiddleCenter);
        m_TitleText.color = Color.white;
        m_TitleText.fontStyle = FontStyle.Bold;
        SetPreferredHeight(m_TitleText.gameObject, 58f);

        GameObject modeButtons = CreateRect("ModeButtons", panel.transform, new Vector2(0f, 64f));
        HorizontalLayoutGroup modeLayout = modeButtons.AddComponent<HorizontalLayoutGroup>();
        modeLayout.spacing = 18;
        modeLayout.childControlWidth = true;
        modeLayout.childForceExpandWidth = true;
        modeLayout.childControlHeight = true;
        modeLayout.childForceExpandHeight = true;
        SetPreferredHeight(modeButtons, 64f);

        m_RegisterModeButton = CreateButton(modeButtons.transform, "Register", () => SetMode(ProfileMode.Register));
        m_SignInModeButton = CreateButton(modeButtons.transform, "Sign in", () => SetMode(ProfileMode.SignIn));

        m_NameInput = CreateInput(panel.transform, "Name", false);
        m_PhoneInput = CreateInput(panel.transform, "Phone number", false);
        m_EmailInput = CreateInput(panel.transform, "Email (optional)", false);
        m_PasswordInput = CreateInput(panel.transform, "Password", true);

        m_RegisterButton = CreateButton(panel.transform, "Register", Register);
        m_SignInButton = CreateButton(panel.transform, "Sign in", SignIn);

        m_StatusText = CreateText("Status", panel.transform, "", 24, TextAnchor.MiddleCenter);
        m_StatusText.color = new Color(0.86f, 0.9f, 0.96f, 1f);
        SetPreferredHeight(m_StatusText.gameObject, 96f);

        SetMode(ProfileMode.Register);
    }

    void SetMode(ProfileMode mode)
    {
        m_Mode = mode;
        bool registerMode = m_Mode == ProfileMode.Register;

        if (m_TitleText != null)
            m_TitleText.text = registerMode ? "Register player" : "Sign in";

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
            SetStatus("Create a new player profile.", false);
        else
            SetStatus("Sign in with an existing phone and password.", false);
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
            image.color = selected ? new Color(0.2f, 0.45f, 0.95f, 1f) : new Color(0.18f, 0.22f, 0.28f, 1f);
    }

    void SetStatus(string message, bool isError)
    {
        m_StatusText.text = message;
        m_StatusText.color = isError ? new Color(1f, 0.44f, 0.36f, 1f) : new Color(0.86f, 0.9f, 0.96f, 1f);
    }

    string Clean(string value)
    {
        return string.IsNullOrEmpty(value) ? "" : value.Trim();
    }

    InputField CreateInput(Transform parent, string placeholder, bool password)
    {
        GameObject root = CreateRect(placeholder, parent, new Vector2(0f, 62f));
        Image image = root.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.12f);

        InputField field = root.AddComponent<InputField>();
        field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
        field.targetGraphic = image;

        Text text = CreateText("Text", root.transform, "", 26, TextAnchor.MiddleLeft);
        text.color = Color.white;
        Stretch(text.rectTransform, 24f, 10f, 24f, 10f);
        field.textComponent = text;

        Text placeholderText = CreateText("Placeholder", root.transform, placeholder, 26, TextAnchor.MiddleLeft);
        placeholderText.color = new Color(1f, 1f, 1f, 0.45f);
        Stretch(placeholderText.rectTransform, 24f, 10f, 24f, 10f);
        field.placeholder = placeholderText;

        SetPreferredHeight(root, 62f);
        return field;
    }

    Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction click)
    {
        GameObject root = CreateRect(label, parent, new Vector2(0f, 64f));
        Image image = root.AddComponent<Image>();
        image.color = new Color(0.2f, 0.45f, 0.95f, 1f);

        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(click);

        Text text = CreateText("Text", root.transform, label, 28, TextAnchor.MiddleCenter);
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;
        Stretch(text.rectTransform);

        SetPreferredHeight(root, 64f);
        return button;
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
