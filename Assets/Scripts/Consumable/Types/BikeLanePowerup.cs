using System.Collections;
using UnityEngine;

public class BikeLanePowerup : Consumable
{
    [Header("Bike Lane")]
    [Tooltip("-1 = use the player current lane. Otherwise 0 = left, 1 = middle, 2 = right. This does not force the player lane.")]
    public int targetLane = -1;
    [Tooltip("0 or lower = use TrackManager bike speed multiplier.")]
    public float speedMultiplier = 0.0f;
    [Tooltip("If enabled, the safe bike lane follows the player current lane while active. It does not force movement.")]
    public bool keepPlayerInBikeLane = true;

    [Header("Bike Visual")]
    [Tooltip("Use the bike object assigned inside the current character prefab. This is the recommended setup.")]
    public bool useCharacterBikeVisual = true;
    [Tooltip("Fallback only: used if the character prefab has no Bike Visuals assigned.")]
    public GameObject bikePrefab;
    [Tooltip("Fallback only: local position used if a bike prefab must be instantiated.")]
    public Vector3 bikeLocalPosition = Vector3.zero;
    [Tooltip("Fallback only: local rotation used if a bike prefab must be instantiated.")]
    public Vector3 bikeLocalEulerAngles = Vector3.zero;
    [Tooltip("Fallback only: scale multiplier used if a bike prefab must be instantiated.")]
    public Vector3 bikeLocalScale = Vector3.one;
    protected GameObject m_BikeInstance;
    protected Character m_BikeCharacter;
    protected int m_ActiveLane = -1;

    public override string GetConsumableName()
    {
        return "Bike Lane";
    }

    public override ConsumableType GetConsumableType()
    {
        return ConsumableType.BIKE_LANE;
    }

    public override int GetPrice()
    {
        return 2000;
    }

    public override int GetPremiumCost()
    {
        return 8;
    }

    public override bool CanBeUsed(CharacterInputController c)
    {
        return c != null && c.trackManager != null && c.trackManager.isMoving;
    }

    public override IEnumerator Started(CharacterInputController c)
    {
        yield return base.Started(c);

        if (c == null || c.trackManager == null)
            yield break;

        c.StopJumping();
        c.StopSliding();
        m_ActiveLane = ResolveLane(c, true);
        ActivateBikeLane(c);
        c.characterCollider.SetInvincible(duration);
        ShowBike(c);
    }

    public override void Tick(CharacterInputController c)
    {
        base.Tick(c);

        if (keepPlayerInBikeLane && c != null && c.trackManager != null)
        {
            m_ActiveLane = ResolveLane(c, true);
            ActivateBikeLane(c);
        }
    }

    public override void Ended(CharacterInputController c)
    {
        m_ActiveLane = -1;
        HideBike();

        if (c != null && c.trackManager != null)
        {
            c.trackManager.EndBikeLane();
        }

        base.Ended(c);
    }

    protected void ActivateBikeLane(CharacterInputController c)
    {
        if (m_ActiveLane < 0)
            m_ActiveLane = ResolveLane(c, true);

        float multiplierSource = speedMultiplier <= 0.0f ? c.trackManager.bikeLaneSpeedMultiplier : speedMultiplier;
        float multiplier = Mathf.Max(1.0f, multiplierSource);
        c.trackManager.BeginBikeLane(m_ActiveLane, multiplier);
    }

    int ResolveLane(CharacterInputController c, bool allowCurrentLane)
    {
        if (targetLane >= 0)
            return Mathf.Clamp(targetLane, 0, 2);

        if (allowCurrentLane && c != null)
            return Mathf.Clamp(c.currentLane, 0, 2);

        return c != null && c.trackManager != null ? Mathf.Clamp(c.trackManager.bikeLaneIndex, 0, 2) : 1;
    }
    protected void ShowBike(CharacterInputController c)
    {
        if (c == null)
            return;

        if (useCharacterBikeVisual && c.character != null && c.character.SetBikeRidingVisual(true))
        {
            m_BikeCharacter = c.character;
            return;
        }

        ShowFallbackBike(c);
    }

    protected void ShowFallbackBike(CharacterInputController c)
    {
        if (bikePrefab == null || m_BikeInstance != null)
            return;

        Transform parent = c.characterCollider != null ? c.characterCollider.transform : c.transform;
        m_BikeInstance = Instantiate(bikePrefab, parent);
        m_BikeInstance.transform.localPosition = bikeLocalPosition;
        m_BikeInstance.transform.localRotation = Quaternion.Euler(bikeLocalEulerAngles);
        m_BikeInstance.transform.localScale = Vector3.Scale(m_BikeInstance.transform.localScale, bikeLocalScale);
    }

    protected void HideBike()
    {
        if (m_BikeCharacter != null)
        {
            m_BikeCharacter.SetBikeRidingVisual(false);
            m_BikeCharacter = null;
        }

        if (m_BikeInstance == null)
            return;

        Destroy(m_BikeInstance);
        m_BikeInstance = null;
    }
}
