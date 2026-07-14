using System.Collections;
using UnityEngine;

public class BikeLanePowerup : Consumable
{
    [Header("Bike Lane")]
    [Tooltip("-1 = use TrackManager bike lane. Otherwise 0 = left, 1 = middle, 2 = right.")]
    public int targetLane = -1;
    [Tooltip("0 or lower = use TrackManager bike speed multiplier.")]
    public float speedMultiplier = 0.0f;
    [Tooltip("If enabled, lane swipes are overridden while the powerup is active.")]
    public bool keepPlayerInBikeLane = true;

    [Header("Bike Visual")]
    [Tooltip("Optional visual bike/rider prefab. Assign your bike prefab here in the powerup prefab inspector.")]
    public GameObject bikePrefab;
    public Vector3 bikeLocalPosition = Vector3.zero;
    public Vector3 bikeLocalEulerAngles = Vector3.zero;
    public Vector3 bikeLocalScale = Vector3.one;
    [Tooltip("Hide the normal running character while the bike visual is active. Turn off if your bike prefab needs the runner visible.")]
    public bool hideCharacterModelWhileRiding = true;

    protected GameObject m_BikeInstance;
    protected GameObject m_HiddenCharacterObject;

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
        ActivateBikeLane(c);
        c.characterCollider.SetInvincible(duration);
        ShowBike(c);
    }

    public override void Tick(CharacterInputController c)
    {
        base.Tick(c);

        if (keepPlayerInBikeLane && c != null && c.trackManager != null)
        {
            ActivateBikeLane(c);
        }
    }

    public override void Ended(CharacterInputController c)
    {
        HideBike();

        if (c != null && c.trackManager != null)
        {
            c.trackManager.EndBikeLane();
        }

        base.Ended(c);
    }

    protected void ActivateBikeLane(CharacterInputController c)
    {
        int laneSource = targetLane < 0 ? c.trackManager.bikeLaneIndex : targetLane;
        int lane = Mathf.Clamp(laneSource, 0, 2);
        float multiplierSource = speedMultiplier <= 0.0f ? c.trackManager.bikeLaneSpeedMultiplier : speedMultiplier;
        float multiplier = Mathf.Max(1.0f, multiplierSource);
        c.trackManager.BeginBikeLane(lane, multiplier);
    }

    protected void ShowBike(CharacterInputController c)
    {
        if (bikePrefab == null || m_BikeInstance != null)
            return;

        if (hideCharacterModelWhileRiding && c.character != null)
        {
            m_HiddenCharacterObject = c.character.gameObject;
            m_HiddenCharacterObject.SetActive(false);
        }

        Transform parent = c.characterCollider != null ? c.characterCollider.transform : c.transform;
        m_BikeInstance = Instantiate(bikePrefab, parent);
        m_BikeInstance.transform.localPosition = bikeLocalPosition;
        m_BikeInstance.transform.localRotation = Quaternion.Euler(bikeLocalEulerAngles);
        m_BikeInstance.transform.localScale = bikeLocalScale;
    }

    protected void HideBike()
    {
        if (m_HiddenCharacterObject != null)
        {
            m_HiddenCharacterObject.SetActive(true);
            m_HiddenCharacterObject = null;
        }

        if (m_BikeInstance == null)
            return;

        Destroy(m_BikeInstance);
        m_BikeInstance = null;
    }
}