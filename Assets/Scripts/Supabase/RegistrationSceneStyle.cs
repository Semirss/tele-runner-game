using UnityEngine;

/// <summary>Scene-editable layout and colors for the register/sign-in form.</summary>
public sealed class RegistrationSceneStyle : MonoBehaviour
{
    [Header("Layout (1080 x 1920 reference)")]
    [Range(560f, 940f)] public float panelWidth = 820f;
    [Range(700f, 1200f)] public float registerHeight = 1080f;
    [Range(560f, 1000f)] public float signInHeight = 890f;
    [Range(24f, 80f)] public float sidePadding = 46f;
    [Range(8f, 32f)] public float rowSpacing = 14f;

    [Header("Tele Runner / Sun Chips theme")]
    public Color backdrop = new Color(0.008f, 0.035f, 0.12f, 0.88f);
    public Color panel = new Color32(3, 27, 82, 255);
    public Color navy = new Color32(3, 27, 82, 255);
    public Color blue = new Color32(14, 101, 195, 255);
    public Color yellow = new Color32(255, 210, 24, 255);
    public Color paper = new Color32(250, 251, 253, 255);
    public Color mutedText = new Color32(91, 113, 153, 255);

    [Header("Form copy")]
    public string brandText = "TELE RUNNER";
    public string partnerText = "SUN CHIPS";
    public string registerTitle = "CREATE YOUR PLAYER";
    public string signInTitle = "WELCOME BACK";
}
