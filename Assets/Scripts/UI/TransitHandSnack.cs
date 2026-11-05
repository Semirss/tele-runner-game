using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Menu-only Sun Chips prop that follows Habesha's animated right hand.</summary>
public sealed class TransitHandSnack : MonoBehaviour
{
    LoadoutState loadout;
    Character actor;
    Transform hand;
    GameObject packet;
    Mesh packetMesh;
    Material packetMaterial;
    Camera menuCamera;
    bool approximateHand;

    void Awake()
    {
        loadout = GetComponent<LoadoutState>();
        menuCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (loadout == null || loadout.charPosition == null) return;
        bool menuVisible = loadout.inventoryCanvas != null &&
            loadout.inventoryCanvas.gameObject.activeInHierarchy;
        Character current = loadout.charPosition.GetComponentInChildren<Character>(true);
        if (current != actor)
        {
            ReleasePacket();
            actor = current;
            if (actor != null && actor.characterName == "Habesha")
                BuildPacket();
        }
        if (packet == null || hand == null) return;
        packet.SetActive(menuVisible);
        if (!menuVisible) return;

        if (menuCamera == null) menuCamera = Camera.main;
        if (menuCamera == null) return;
        // The bottom of the packet sits in the palm. Its front stays readable as
        // the wrist turns through the idle and dance clips.
        Vector3 palm = approximateHand
            ? actor.transform.position + actor.transform.up * 1.13f
                + menuCamera.transform.right * .22f
            : hand.position + menuCamera.transform.up * .06f;
        // Bring the artwork in front of the fingers so the animated mesh cannot
        // hide it, and keep its printed face toward the menu camera.
        packet.transform.position = palm - menuCamera.transform.forward * .14f;
        packet.transform.rotation = Quaternion.LookRotation(
            menuCamera.transform.forward, menuCamera.transform.up) *
            Quaternion.Euler(0f, 0f, -12f);
    }

    void BuildPacket()
    {
        Animator animator = actor.animator != null ? actor.animator : actor.GetComponent<Animator>();
        if (animator != null && animator.isHuman)
        {
            try { hand = animator.GetBoneTransform(HumanBodyBones.RightHand); }
            catch (System.InvalidOperationException) { hand = null; }
        }
        if (hand == null)
        {
            foreach (SkinnedMeshRenderer skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (Transform bone in skin.bones)
                {
                    if (IsRightHand(bone)) { hand = bone; break; }
                }
                if (hand != null) break;
            }
        }
        if (hand == null)
            foreach (Transform candidate in actor.GetComponentsInChildren<Transform>(true))
                if (IsRightHand(candidate)) { hand = candidate; break; }
        if (hand == null)
        {
            hand = actor.transform;
            approximateHand = true;
        }

        Texture2D art = Resources.Load<Texture2D>("TransitMenu/SunChipsBag");
        if (art == null) return;
        packet = new GameObject("Sun Chips hand packet", typeof(MeshFilter), typeof(MeshRenderer));
        packet.layer = actor.gameObject.layer;
        packet.transform.SetParent(hand, false);

        // A shallow center fold and crimped top/bottom give the front art volume.
        packetMesh = new Mesh { name = "Sun Chips packet front" };
        const float w = .23f;
        const float h = .33f;
        packetMesh.vertices = new[] {
            new Vector3(-w/2, -h/2, 0), new Vector3(0, -h/2, -.012f),
            new Vector3(w/2, -h/2, 0),
            new Vector3(-w/2, h/2, 0), new Vector3(0, h/2, -.012f),
            new Vector3(w/2, h/2, 0)
        };
        packetMesh.uv = new[] {
            new Vector2(0, 0), new Vector2(.5f, 0), new Vector2(1, 0),
            new Vector2(0, 1), new Vector2(.5f, 1), new Vector2(1, 1)
        };
        packetMesh.triangles = new[] { 0, 3, 1, 1, 3, 4, 1, 4, 2, 2, 4, 5 };
        packetMesh.RecalculateNormals();
        packet.GetComponent<MeshFilter>().sharedMesh = packetMesh;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        packetMaterial = new Material(shader) { name = "Sun Chips packet art" };
        if (packetMaterial.HasProperty("_BaseMap")) packetMaterial.SetTexture("_BaseMap", art);
        packetMaterial.mainTexture = art;
        if (packetMaterial.HasProperty("_Surface")) packetMaterial.SetFloat("_Surface", 1f);
        if (packetMaterial.HasProperty("_SrcBlend")) packetMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (packetMaterial.HasProperty("_DstBlend")) packetMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (packetMaterial.HasProperty("_ZWrite")) packetMaterial.SetFloat("_ZWrite", 0f);
        packetMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        packetMaterial.renderQueue = (int)RenderQueue.Transparent;
        if (packetMaterial.HasProperty("_Cull")) packetMaterial.SetFloat("_Cull", 0f);
        MeshRenderer renderer = packet.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = packetMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    void ReleasePacket()
    {
        if (packet != null) Destroy(packet);
        if (packetMesh != null) Destroy(packetMesh);
        if (packetMaterial != null) Destroy(packetMaterial);
        packet = null;
        packetMesh = null;
        packetMaterial = null;
        hand = null;
        approximateHand = false;
    }

    public static bool IsRightHand(Transform candidate)
    {
        if (candidate == null) return false;
        string name = candidate.name.ToLowerInvariant();
        return name.EndsWith("righthand") || name.EndsWith("right_hand") ||
            name.EndsWith("hand.r") || name.EndsWith("rightwrist");
    }

    void OnDestroy() => ReleasePacket();
}
