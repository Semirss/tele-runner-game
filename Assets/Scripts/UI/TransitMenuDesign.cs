using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

/// <summary>
/// Visual skin for the existing loadout menu. The animated Addressables character and
/// the original button callbacks remain owned by LoadoutState and the scene.
/// </summary>
[RequireComponent(typeof(LoadoutState))]
[ExecuteAlways]
public sealed class TransitMenuDesign : MonoBehaviour
{
    const float DesignWidth = 1080f;
    const float DesignHeight = 1920f;
    const float CharacterDistanceScale = 1.28f;
    const float PavementViewportHeight = 0.292f;

    static readonly Color Ink = new Color32(3, 27, 82, 255);
    static readonly Color Paper = new Color32(250, 251, 253, 255);

    LoadoutState loadout;
    Camera menuCamera;
    RectTransform overlay;
    GameObject backgroundPlate;
    Mesh backgroundMesh;
    Material backgroundMaterial;
    Button runControl;
    Text runLabel;
    Text playerLabel;
    Text rankLabel;
    Button[] legacyNav;
    Transform settingsPopup;
    float originalCharacterDepth;
#if UNITY_EDITOR
    GameObject previewCharacter;
    GameObject previewPacket;
    Mesh previewPacketMesh;
    Material previewPacketMaterial;
    Transform previewHand;
    readonly Dictionary<CanvasRenderer, bool> previewCanvasCull = new Dictionary<CanvasRenderer, bool>();
    readonly Dictionary<Renderer, bool> previewRendererOff = new Dictionary<Renderer, bool>();
    readonly List<Sprite> previewSprites = new List<Sprite>();
#endif

    void Awake()
    {
        if (!Application.isPlaying) return;
        loadout = GetComponent<LoadoutState>();
        menuCamera = Camera.main;
        if (loadout == null || loadout.inventoryCanvas == null || menuCamera == null)
        {
            Debug.LogError("Transit menu requires the existing LoadoutState, canvas, and Main Camera.", this);
            enabled = false;
            return;
        }

        // Hide the old flag before the first menu frame is rendered.
        if (loadout.backgrounddeletable != null)
            loadout.backgrounddeletable.SetActive(false);
        if (loadout.skyMeshFilter != null)
            loadout.skyMeshFilter.gameObject.SetActive(false);
        if (loadout.UIGroundFilter != null)
            loadout.UIGroundFilter.gameObject.SetActive(false);

        if (loadout.charPosition != null)
            originalCharacterDepth = Vector3.Dot(
                loadout.charPosition.position - menuCamera.transform.position,
                menuCamera.transform.forward);

        BuildBackgroundPlate();
        BuildInterface();
        HideLegacyMenuArtwork();
        if (GetComponent<TransitPanelDesign>() == null)
            gameObject.AddComponent<TransitPanelDesign>();
        if (GetComponent<TransitHandSnack>() == null)
            gameObject.AddComponent<TransitHandSnack>();
    }

#if UNITY_EDITOR
    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            EditorApplication.update += EditorPreviewUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += CreateEditorPreview;
        }
    }

    void EditorPreviewUpdate()
    {
        if (this == null || !enabled || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (overlay == null) CreateEditorPreview();
        else RefreshEditorPreview();
    }

    void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
            ClearEditorPreview();
        else if (change == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += CreateEditorPreview;
    }

    void CreateEditorPreview()
    {
        if (this == null || !enabled || EditorApplication.isPlayingOrWillChangePlaymode || overlay != null) return;
        loadout = GetComponent<LoadoutState>();
        menuCamera = Camera.main;
        if (loadout == null || loadout.inventoryCanvas == null || menuCamera == null) return;

        if (loadout.charPosition != null)
            originalCharacterDepth = Vector3.Dot(
                loadout.charPosition.position - menuCamera.transform.position,
                menuCamera.transform.forward);

        BuildBackgroundPlate();
        BuildInterface();
        BuildPreviewCharacter();
        RefreshEditorPreview();
    }

    void BuildPreviewCharacter()
    {
        // The runtime character is loaded with Addressables. Use its source prefab
        // for a static editor preview without starting the game or loading player data.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Bundles/Characters/Raccoon/character.prefab");
        if (prefab == null || loadout.charPosition == null) return;
        previewCharacter = Instantiate(prefab);
        previewCharacter.name = "Transit Menu Character Preview";
        foreach (Transform child in previewCharacter.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = LayerMask.NameToLayer("UI");
        }
        previewCharacter.hideFlags = HideFlags.HideAndDontSave;
        previewCharacter.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        PositionPreviewCharacter();
        BuildPreviewPacket();
    }

    void BuildPreviewPacket()
    {
        Character actor = previewCharacter != null ? previewCharacter.GetComponent<Character>() : null;
        if (actor == null || menuCamera == null) return;
        Animator animator = actor.animator != null ? actor.animator : actor.GetComponent<Animator>();
        if (animator != null && animator.isHuman)
        {
            try { previewHand = animator.GetBoneTransform(HumanBodyBones.RightHand); }
            catch (System.InvalidOperationException) { previewHand = null; }
        }
        if (previewHand == null)
            foreach (SkinnedMeshRenderer skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (Transform bone in skin.bones)
                    if (TransitHandSnack.IsRightHand(bone)) { previewHand = bone; break; }
                if (previewHand != null) break;
            }
        if (previewHand == null)
            foreach (Transform candidate in actor.GetComponentsInChildren<Transform>(true))
                if (TransitHandSnack.IsRightHand(candidate)) { previewHand = candidate; break; }

        Texture2D art = Resources.Load<Texture2D>("TransitMenu/SunChipsBag");
        if (art == null) return;
        previewPacket = new GameObject("Sun Chips preview packet", typeof(MeshFilter), typeof(MeshRenderer));
        previewPacket.hideFlags = HideFlags.HideAndDontSave;
        previewPacket.layer = LayerMask.NameToLayer("UI");
        previewPacketMesh = new Mesh { name = "Sun Chips preview quad", hideFlags = HideFlags.HideAndDontSave };
        previewPacketMesh.vertices = new[] {
            new Vector3(-.115f, -.165f, 0), new Vector3(.115f, -.165f, 0),
            new Vector3(-.115f, .165f, 0), new Vector3(.115f, .165f, 0)
        };
        previewPacketMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        previewPacketMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        previewPacketMesh.RecalculateNormals();
        previewPacket.GetComponent<MeshFilter>().sharedMesh = previewPacketMesh;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        previewPacketMaterial = new Material(shader) {
            name = "Sun Chips preview material", hideFlags = HideFlags.HideAndDontSave };
        if (previewPacketMaterial.HasProperty("_BaseMap"))
            previewPacketMaterial.SetTexture("_BaseMap", art);
        previewPacketMaterial.mainTexture = art;
        if (previewPacketMaterial.HasProperty("_Surface")) previewPacketMaterial.SetFloat("_Surface", 1f);
        if (previewPacketMaterial.HasProperty("_SrcBlend"))
            previewPacketMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (previewPacketMaterial.HasProperty("_DstBlend"))
            previewPacketMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (previewPacketMaterial.HasProperty("_ZWrite")) previewPacketMaterial.SetFloat("_ZWrite", 0f);
        if (previewPacketMaterial.HasProperty("_Cull")) previewPacketMaterial.SetFloat("_Cull", 0f);
        previewPacketMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        previewPacketMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        previewPacket.GetComponent<MeshRenderer>().sharedMaterial = previewPacketMaterial;
        PositionPreviewPacket();
    }

    void PositionPreviewPacket()
    {
        if (previewPacket == null || previewCharacter == null || menuCamera == null) return;
        Vector3 palm = previewHand != null
            ? previewHand.position + menuCamera.transform.up * .06f
            : previewCharacter.transform.position + previewCharacter.transform.up * 1.13f
                + menuCamera.transform.right * .22f;
        previewPacket.transform.position = palm - menuCamera.transform.forward * .14f;
        previewPacket.transform.rotation = Quaternion.LookRotation(
            menuCamera.transform.forward, menuCamera.transform.up) * Quaternion.Euler(0f, 0f, -12f);
    }

    void PositionPreviewCharacter()
    {
        if (previewCharacter == null || menuCamera == null) return;
        float depth = originalCharacterDepth * CharacterDistanceScale;
        if (depth <= menuCamera.nearClipPlane) return;
        previewCharacter.transform.position = menuCamera.ViewportToWorldPoint(
            new Vector3(0.5f, PavementViewportHeight, depth));
    }

    void RefreshEditorPreview()
    {
        if (Application.isPlaying || overlay == null || loadout == null) return;
        PositionBackgroundPlate();
        PositionPreviewCharacter();
        PositionPreviewPacket();
        if (playerLabel != null && loadout.playerNameDisplay != null)
            playerLabel.text = loadout.playerNameDisplay.text;
        if (rankLabel != null && loadout.playerRankDisplay != null)
            rankLabel.text = loadout.playerRankDisplay.text;
        if (runLabel != null) runLabel.text = "RUN";

        // Cull only the old presentation. These renderer properties are restored
        // when the preview is removed, so the scene asset is never rewritten.
        foreach (CanvasRenderer renderer in loadout.inventoryCanvas.GetComponentsInChildren<CanvasRenderer>(true))
        {
            if (renderer == null || renderer.transform.IsChildOf(overlay)) continue;
            if (!previewCanvasCull.ContainsKey(renderer))
                previewCanvasCull.Add(renderer, renderer.cull);
            renderer.cull = true;
        }
        CullPreviewRenderer(loadout.backgrounddeletable);
        CullPreviewRenderer(loadout.skyMeshFilter != null ? loadout.skyMeshFilter.gameObject : null);
        CullPreviewRenderer(loadout.UIGroundFilter != null ? loadout.UIGroundFilter.gameObject : null);
    }

    void CullPreviewRenderer(GameObject root)
    {
        if (root == null) return;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!previewRendererOff.ContainsKey(renderer))
                previewRendererOff.Add(renderer, renderer.forceRenderingOff);
            renderer.forceRenderingOff = true;
        }
    }

    void ClearEditorPreview()
    {
        foreach (var pair in previewCanvasCull)
            if (pair.Key != null) pair.Key.cull = pair.Value;
        previewCanvasCull.Clear();
        foreach (var pair in previewRendererOff)
            if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
        previewRendererOff.Clear();
        if (previewCharacter != null) DestroyImmediate(previewCharacter);
        if (previewPacket != null) DestroyImmediate(previewPacket);
        if (previewPacketMaterial != null) DestroyImmediate(previewPacketMaterial);
        if (previewPacketMesh != null) DestroyImmediate(previewPacketMesh);
        if (overlay != null) DestroyImmediate(overlay.gameObject);
        if (backgroundPlate != null) DestroyImmediate(backgroundPlate);
        foreach (Sprite sprite in previewSprites)
            if (sprite != null) DestroyImmediate(sprite);
        previewSprites.Clear();
        if (backgroundMaterial != null) DestroyImmediate(backgroundMaterial);
        if (backgroundMesh != null) DestroyImmediate(backgroundMesh);
        previewCharacter = null;
        previewPacket = null;
        previewPacketMaterial = null;
        previewPacketMesh = null;
        previewHand = null;
        overlay = null;
        backgroundPlate = null;
        backgroundMaterial = null;
        backgroundMesh = null;
    }
#endif

    void LateUpdate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (overlay == null) CreateEditorPreview();
            else RefreshEditorPreview();
            return;
        }
#endif
        bool visible = loadout.inventoryCanvas != null && loadout.inventoryCanvas.gameObject.activeInHierarchy;
        if (backgroundPlate != null && backgroundPlate.activeSelf != visible)
            backgroundPlate.SetActive(visible);
        if (overlay != null && overlay.gameObject.activeSelf != visible)
            overlay.gameObject.SetActive(visible);
        if (!visible)
            return;

        // LoadoutState enables the old flag and ground on Enter. The new background
        // replaces only that presentation; the character prefab and Animator are untouched.
        if (loadout.backgrounddeletable != null && loadout.backgrounddeletable.activeSelf)
            loadout.backgrounddeletable.SetActive(false);
        if (loadout.skyMeshFilter != null && loadout.skyMeshFilter.gameObject.activeSelf)
            loadout.skyMeshFilter.gameObject.SetActive(false);
        if (loadout.UIGroundFilter != null && loadout.UIGroundFilter.gameObject.activeSelf)
            loadout.UIGroundFilter.gameObject.SetActive(false);

        PositionBackgroundPlate();
        PositionCharacterOnPavement();

        if (playerLabel != null && loadout.playerNameDisplay != null)
            playerLabel.text = loadout.playerNameDisplay.text;
        if (rankLabel != null && loadout.playerRankDisplay != null)
            rankLabel.text = loadout.playerRankDisplay.text;

        if (runControl != null && loadout.runButton != null)
        {
            runControl.interactable = loadout.runButton.interactable;
            if (runLabel != null)
                runLabel.text = runControl.interactable ? "RUN" : "LOADING";
        }

        // Popups should cover the new controls when opened.
        if (loadout.missionPopup != null && loadout.missionPopup.gameObject.activeSelf)
            loadout.missionPopup.transform.SetAsLastSibling();
        if (settingsPopup != null && settingsPopup.gameObject.activeSelf)
            settingsPopup.SetAsLastSibling();
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorPreviewUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= CreateEditorPreview;
            ClearEditorPreview();
            return;
        }
#endif
        // LoadoutState turns off its own canvas when a run begins. Its external
        // world-space background must disappear in the same frame.
        if (backgroundPlate != null)
            backgroundPlate.SetActive(false);
    }

    void BuildBackgroundPlate()
    {
        Texture2D texture = Resources.Load<Texture2D>("TransitMenu/TransitBackground");
        if (texture == null)
        {
            Debug.LogError("TransitBackground is missing from Resources/TransitMenu.", this);
            return;
        }

        backgroundPlate = new GameObject("Transit Menu Background Plate", typeof(MeshFilter), typeof(MeshRenderer));
        backgroundMesh = new Mesh { name = "Transit Background Quad" };
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            backgroundPlate.hideFlags = HideFlags.HideAndDontSave;
            backgroundMesh.hideFlags = HideFlags.HideAndDontSave;
        }
#endif
        backgroundMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        backgroundMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        backgroundMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        backgroundMesh.RecalculateNormals();
        backgroundPlate.GetComponent<MeshFilter>().sharedMesh = backgroundMesh;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        backgroundMaterial = new Material(shader) { name = "Transit Menu Background" };
#if UNITY_EDITOR
        if (!Application.isPlaying)
            backgroundMaterial.hideFlags = HideFlags.HideAndDontSave;
#endif
        if (backgroundMaterial.HasProperty("_BaseMap"))
            backgroundMaterial.SetTexture("_BaseMap", texture);
        backgroundMaterial.mainTexture = texture;
        MeshRenderer renderer = backgroundPlate.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = backgroundMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        PositionBackgroundPlate();
    }

    void PositionBackgroundPlate()
    {
        if (backgroundPlate == null || menuCamera == null) return;
        float characterDepth = loadout.charPosition != null
            ? Vector3.Dot(loadout.charPosition.position - menuCamera.transform.position, menuCamera.transform.forward)
            : 4f;
        float depth = Mathf.Max(characterDepth + 4f, 8f);
        float height = 2f * depth * Mathf.Tan(menuCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        backgroundPlate.transform.SetPositionAndRotation(
            menuCamera.transform.position + menuCamera.transform.forward * depth,
            menuCamera.transform.rotation);
        backgroundPlate.transform.localScale = new Vector3(height * menuCamera.aspect * 1.01f, height * 1.01f, 1f);
    }

    void PositionCharacterOnPavement()
    {
        if (loadout.charPosition == null || menuCamera == null) return;
        float depth = originalCharacterDepth * CharacterDistanceScale;
        if (depth <= menuCamera.nearClipPlane) return;
        // The character root is at the feet. The pavement in the art is at 29% screen height.
        loadout.charPosition.position = menuCamera.ViewportToWorldPoint(
            new Vector3(0.5f, PavementViewportHeight, depth));
    }

    void BuildInterface()
    {
        RectTransform canvas = loadout.inventoryCanvas.GetComponent<RectTransform>();
        GameObject root = new GameObject("Transit Menu Controls", typeof(RectTransform));
#if UNITY_EDITOR
        if (!Application.isPlaying) root.hideFlags = HideFlags.HideAndDontSave;
#endif
        root.layer = 5;
        overlay = root.GetComponent<RectTransform>();
        overlay.SetParent(canvas, false);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;

        Font uiFont = loadout.playerNameDisplay != null ? loadout.playerNameDisplay.font : null;
        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        playerLabel = MakeText("Player name", uiFont, 44, FontStyle.BoldAndItalic, Paper,
            669, 112, 246, 54);
        playerLabel.resizeTextForBestFit = true;
        playerLabel.resizeTextMinSize = 20;
        playerLabel.resizeTextMaxSize = 44;
        playerLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        playerLabel.verticalOverflow = VerticalWrapMode.Truncate;
        rankLabel = MakeText("Player rank", uiFont, 28, FontStyle.BoldAndItalic, Paper,
            669, 164, 246, 44);
        rankLabel.resizeTextForBestFit = true;
        rankLabel.resizeTextMinSize = 18;
        rankLabel.resizeTextMaxSize = 28;
        rankLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        rankLabel.verticalOverflow = VerticalWrapMode.Truncate;

        Button originalRun = loadout.runButton;
        runControl = MakeButton("Run", "TransitMenu/RunButton", 540, 1565, 960, 188);
        runControl.onClick.AddListener(() =>
        {
            if (originalRun != null && originalRun.interactable)
                originalRun.onClick.Invoke();
        });
        runLabel = MakeText("Run label", uiFont, 112, FontStyle.BoldAndItalic, Ink,
            430, 1565, 540, 160);

        string[] names = { "OpenLeaderboard", "StoreButton", "MissionButton", "SettingButton" };
        string[] labels = { "Leaderboard", "Shop", "Missions", "Settings" };
        string[] icons = { "Leaderboard", "Shop", "Missions", "Settings" };
        int[] centers = { 140, 405, 675, 940 };
        legacyNav = new Button[names.Length];
        settingsPopup = canvas.Find("SettingPopup");
        for (int i = 0; i < names.Length; i++)
        {
            Transform oldTransform = canvas.Find(names[i]);
            legacyNav[i] = oldTransform != null ? oldTransform.GetComponent<Button>() : null;
            int index = i;
            Button nav = MakeButton(labels[i], null, centers[i], 1805, 238, 185);
            nav.onClick.AddListener(() =>
            {
                if (legacyNav[index] != null)
                    legacyNav[index].onClick.Invoke();
            });
            Image hit = nav.GetComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            Image icon = MakeImage(icons[i] + " icon", "TransitMenu/" + icons[i],
                centers[i], 1766, 108, 108);
            icon.raycastTarget = false;
            nav.targetGraphic = icon;
            Text navLabel = MakeText(labels[i] + " label", uiFont, 38,
                FontStyle.BoldAndItalic, Paper, centers[i], 1857, 248, 64);
            navLabel.text = labels[i];
        }
    }

    void HideLegacyMenuArtwork()
    {
        Transform canvas = loadout.inventoryCanvas.transform;
        foreach (Transform child in canvas)
        {
            if (child == overlay || child == loadout.runButton.transform) continue;
            child.gameObject.SetActive(false);
        }

        // Keep the original Run Button active: LoadoutState writes its child Text each
        // frame and uses its interactable property while data finishes loading.
        CanvasGroup group = loadout.runButton.GetComponent<CanvasGroup>();
        if (group == null) group = loadout.runButton.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    RectTransform Place(GameObject go, float cx, float cy, float w, float h)
    {
        go.layer = 5;
#if UNITY_EDITOR
        if (!Application.isPlaying) go.hideFlags = HideFlags.HideAndDontSave;
#endif
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(overlay, false);
        rect.anchorMin = rect.anchorMax = new Vector2(cx / DesignWidth, 1f - cy / DesignHeight);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    Text MakeText(string name, Font uiFont, int size, FontStyle style, Color color,
        float cx, float cy, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        Place(go, cx, cy, w, h);
        Text label = go.GetComponent<Text>();
        label.font = uiFont;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = color;
        label.raycastTarget = false;
        label.supportRichText = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        return label;
    }

    Image MakeImage(string name, string path, float cx, float cy, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Place(go, cx, cy, w, h);
        Image img = go.GetComponent<Image>();
        Texture2D tex = Resources.Load<Texture2D>(path);
        if (tex != null)
        {
            img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                img.sprite.hideFlags = HideFlags.HideAndDontSave;
                previewSprites.Add(img.sprite);
            }
#endif
        }
        img.type = Image.Type.Simple;
        return img;
    }

    Button MakeButton(string name, string path, float cx, float cy, float w, float h)
    {
        Image image = MakeImage(name, path, cx, cy, w, h);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.96f, 1f);
        colors.pressedColor = new Color(0.76f, 0.86f, 1f);
        colors.disabledColor = new Color(0.65f, 0.68f, 0.73f, 0.8f);
        button.colors = colors;
        return button;
    }

    void OnDestroy()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ClearEditorPreview();
            return;
        }
#endif
        if (backgroundPlate != null) Destroy(backgroundPlate);
        if (backgroundMaterial != null) Destroy(backgroundMaterial);
        if (backgroundMesh != null) Destroy(backgroundMesh);
    }
}
