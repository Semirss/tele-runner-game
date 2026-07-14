using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Mainly used as a data container to define a character. This script is attached to the prefab
/// (found in the Bundles/Characters folder) and is to define all data related to the character.
/// </summary>
public class Character : MonoBehaviour
{
    public string characterName;
    public int cost;
    public int premiumCost;

    public CharacterAccessories[] accessories;

    public Animator animator;
    public Sprite icon;

    [Header("Bike Visual Toggle")]
    [Tooltip("Objects to turn ON while the bike powerup is active. Put your positioned bike child object(s) here. Keep them disabled in the prefab.")]
    public GameObject[] bikeVisuals;
    [Tooltip("Objects to turn OFF while the bike powerup is active. Assign only the visible running model/mesh objects, not the character root and not a parent of the bike.")]
    public GameObject[] runningVisuals;
    [Tooltip("Objects inside Bike Visuals that must stay OFF while riding, such as imported demo rider/dance objects inside the bike prefab.")]
    public GameObject[] bikeHiddenVisuals;

    [Header("Bike Animation")]
    public bool animateBikeVisuals = true;
    [Tooltip("Base pedal/cycle speed. Wheels and pedals use this same phase so the animation stays synced.")]
    public float bikeCycleDegreesPerSecond = 540.0f;
    [Tooltip("Wheel transforms to spin while riding. Assign FrontWheel/BackWheel here.")]
    public Transform[] bikeWheels;
    public Vector3 bikeWheelLocalAxis = Vector3.right;
    public float bikeWheelSpeedMultiplier = 2.5f;
    [Tooltip("Pedal/crank root transforms to rotate while riding. If the pedals are separate, create an empty at the crank center and parent the pedals under it.")]
    public Transform[] bikePedalRotators;
    public Vector3 bikePedalLocalAxis = Vector3.forward;
    public float bikePedalSpeedMultiplier = 1.0f;
    [Tooltip("Recommended ON. Disables colliders inside Bike Visuals while riding so the visual bike cannot physically stick on obstacles.")]
    public bool disableBikeVisualColliders = true;
    [Tooltip("Recommended ON. Disables Animators inside Bike Visuals so imported clips like victory/dance do not play over the scripted cycling.")]
    public bool disableBikeVisualAnimators = true;

    [Header("Bike Leg Animation")]
    [Tooltip("Optional. Assign the sitting/riding model leg bones here for script cycling. Leave any missing bones empty.")]
    public Transform leftUpperLeg;
    public Transform leftLowerLeg;
    public Transform leftFoot;
    public Transform rightUpperLeg;
    public Transform rightLowerLeg;
    public Transform rightFoot;
    public Vector3 upperLegLocalAxis = Vector3.right;
    public Vector3 lowerLegLocalAxis = Vector3.right;
    public Vector3 footLocalAxis = Vector3.right;
    public float upperLegSwingDegrees = 28.0f;
    public float lowerLegSwingDegrees = 35.0f;
    public float footSwingDegrees = 18.0f;

    [Header("Sound")]
    public AudioClip jumpSound;
    public AudioClip hitSound;
    public AudioClip deathSound;

    public bool isBikeRiding { get { return m_BikeRiding; } }

    bool m_BikeRiding;
    bool m_WarnedUnsafeRunningVisual;
    float m_BikeCycleAngle;

    RotationState[] m_WheelStates;
    RotationState[] m_PedalStates;
    RotationState m_LeftUpperLegState;
    RotationState m_LeftLowerLegState;
    RotationState m_LeftFootState;
    RotationState m_RightUpperLegState;
    RotationState m_RightLowerLegState;
    RotationState m_RightFootState;
    ColliderState[] m_BikeColliderStates;
    BehaviourState[] m_BikeAnimatorStates;

    class ColliderState
    {
        public Collider target;
        public bool enabled;

        public ColliderState(Collider target)
        {
            this.target = target;
            enabled = target != null && target.enabled;
        }
    }

    class BehaviourState
    {
        public Behaviour target;
        public bool enabled;

        public BehaviourState(Behaviour target)
        {
            this.target = target;
            enabled = target != null && target.enabled;
        }
    }

    class RotationState
    {
        public Transform target;
        public Quaternion restLocalRotation;

        public RotationState(Transform target)
        {
            this.target = target;
            restLocalRotation = target != null ? target.localRotation : Quaternion.identity;
        }
    }

    void Awake()
    {
        CacheBikeAnimationRestPose();
        SetBikeRidingVisual(false);
    }

    void LateUpdate()
    {
        if (m_BikeRiding && animateBikeVisuals)
            AnimateBike(Time.deltaTime);
    }

    // Called by the game when an accessory changes, enable/disable the accessories children objects accordingly
    // a value of -1 as parameter disables all accessory.
    public void SetupAccesory(int accessory)
    {
        for (int i = 0; i < accessories.Length; ++i)
        {
            accessories[i].gameObject.SetActive(i == PlayerData.instance.usedAccessory);
        }
    }

    public bool HasBikeVisual()
    {
        return HasAnyAssigned(bikeVisuals);
    }

    public bool SetBikeRidingVisual(bool riding)
    {
        if (!HasBikeVisual())
            return false;

        if (m_WheelStates == null || m_PedalStates == null)
            CacheBikeAnimationRestPose();

        m_BikeRiding = riding;

        if (riding)
        {
            SetObjectsActive(bikeVisuals, true);
            SetObjectsActive(bikeHiddenVisuals, false);
            DisableBikeVisualRuntimeComponents();
            SetRunningObjectsActive(false);
        }
        else
        {
            RestoreBikeVisualRuntimeComponents();
            RestoreBikeAnimationRestPose();
            SetRunningObjectsActive(true);
            SetObjectsActive(bikeHiddenVisuals, false);
            SetObjectsActive(bikeVisuals, false);
        }

        return true;
    }

    void DisableBikeVisualRuntimeComponents()
    {
        if (disableBikeVisualColliders)
        {
            Collider[] colliders = GetBikeVisualComponents<Collider>();
            List<ColliderState> states = new List<ColliderState>();
            for (int i = 0; i < colliders.Length; ++i)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                states.Add(new ColliderState(collider));
                collider.enabled = false;
            }
            m_BikeColliderStates = states.ToArray();
        }

        if (disableBikeVisualAnimators)
        {
            Animator[] animators = GetBikeVisualComponents<Animator>();
            List<BehaviourState> states = new List<BehaviourState>();
            for (int i = 0; i < animators.Length; ++i)
            {
                Animator animator = animators[i];
                if (animator == null)
                    continue;

                states.Add(new BehaviourState(animator));
                animator.enabled = false;
            }
            m_BikeAnimatorStates = states.ToArray();
        }
    }

    void RestoreBikeVisualRuntimeComponents()
    {
        if (m_BikeColliderStates != null)
        {
            for (int i = 0; i < m_BikeColliderStates.Length; ++i)
            {
                ColliderState state = m_BikeColliderStates[i];
                if (state != null && state.target != null)
                    state.target.enabled = state.enabled;
            }
            m_BikeColliderStates = null;
        }

        if (m_BikeAnimatorStates != null)
        {
            for (int i = 0; i < m_BikeAnimatorStates.Length; ++i)
            {
                BehaviourState state = m_BikeAnimatorStates[i];
                if (state != null && state.target != null)
                    state.target.enabled = state.enabled;
            }
            m_BikeAnimatorStates = null;
        }
    }

    T[] GetBikeVisualComponents<T>() where T : Component
    {
        List<T> components = new List<T>();
        if (bikeVisuals == null)
            return components.ToArray();

        for (int i = 0; i < bikeVisuals.Length; ++i)
        {
            GameObject bikeVisual = bikeVisuals[i];
            if (bikeVisual == null)
                continue;

            components.AddRange(bikeVisual.GetComponentsInChildren<T>(true));
        }

        return components.ToArray();
    }

    void AnimateBike(float deltaTime)
    {
        m_BikeCycleAngle = Mathf.Repeat(m_BikeCycleAngle + bikeCycleDegreesPerSecond * deltaTime, 360.0f);

        ApplyRotationStates(m_WheelStates, bikeWheelLocalAxis, m_BikeCycleAngle * bikeWheelSpeedMultiplier);
        ApplyRotationStates(m_PedalStates, bikePedalLocalAxis, m_BikeCycleAngle * bikePedalSpeedMultiplier);
        AnimateLegs(m_BikeCycleAngle);
    }

    void AnimateLegs(float cycleAngle)
    {
        float leftPhase = cycleAngle * Mathf.Deg2Rad;
        float rightPhase = (cycleAngle + 180.0f) * Mathf.Deg2Rad;

        ApplyBoneSwing(m_LeftUpperLegState, upperLegLocalAxis, Mathf.Sin(leftPhase) * upperLegSwingDegrees);
        ApplyBoneSwing(m_RightUpperLegState, upperLegLocalAxis, Mathf.Sin(rightPhase) * upperLegSwingDegrees);

        ApplyBoneSwing(m_LeftLowerLegState, lowerLegLocalAxis, Mathf.Cos(leftPhase) * lowerLegSwingDegrees);
        ApplyBoneSwing(m_RightLowerLegState, lowerLegLocalAxis, Mathf.Cos(rightPhase) * lowerLegSwingDegrees);

        ApplyBoneSwing(m_LeftFootState, footLocalAxis, -Mathf.Sin(leftPhase) * footSwingDegrees);
        ApplyBoneSwing(m_RightFootState, footLocalAxis, -Mathf.Sin(rightPhase) * footSwingDegrees);
    }

    void ApplyRotationStates(RotationState[] states, Vector3 localAxis, float degrees)
    {
        if (states == null)
            return;

        Vector3 axis = SafeAxis(localAxis);
        for (int i = 0; i < states.Length; ++i)
            ApplyBoneSwing(states[i], axis, degrees);
    }

    void ApplyBoneSwing(RotationState state, Vector3 localAxis, float degrees)
    {
        if (state == null || state.target == null)
            return;

        state.target.localRotation = state.restLocalRotation * Quaternion.AngleAxis(degrees, SafeAxis(localAxis));
    }

    Vector3 SafeAxis(Vector3 axis)
    {
        return axis.sqrMagnitude < 0.0001f ? Vector3.right : axis.normalized;
    }

    void CacheBikeAnimationRestPose()
    {
        m_WheelStates = CacheRotationStates(bikeWheels);
        m_PedalStates = CacheRotationStates(bikePedalRotators);
        m_LeftUpperLegState = new RotationState(leftUpperLeg);
        m_LeftLowerLegState = new RotationState(leftLowerLeg);
        m_LeftFootState = new RotationState(leftFoot);
        m_RightUpperLegState = new RotationState(rightUpperLeg);
        m_RightLowerLegState = new RotationState(rightLowerLeg);
        m_RightFootState = new RotationState(rightFoot);
    }

    RotationState[] CacheRotationStates(Transform[] transforms)
    {
        if (transforms == null)
            return new RotationState[0];

        RotationState[] states = new RotationState[transforms.Length];
        for (int i = 0; i < transforms.Length; ++i)
            states[i] = new RotationState(transforms[i]);
        return states;
    }

    void RestoreBikeAnimationRestPose()
    {
        RestoreRotationStates(m_WheelStates);
        RestoreRotationStates(m_PedalStates);
        RestoreRotationState(m_LeftUpperLegState);
        RestoreRotationState(m_LeftLowerLegState);
        RestoreRotationState(m_LeftFootState);
        RestoreRotationState(m_RightUpperLegState);
        RestoreRotationState(m_RightLowerLegState);
        RestoreRotationState(m_RightFootState);
    }

    void RestoreRotationStates(RotationState[] states)
    {
        if (states == null)
            return;

        for (int i = 0; i < states.Length; ++i)
            RestoreRotationState(states[i]);
    }

    void RestoreRotationState(RotationState state)
    {
        if (state != null && state.target != null)
            state.target.localRotation = state.restLocalRotation;
    }

    void SetRunningObjectsActive(bool active)
    {
        if (runningVisuals == null)
            return;

        for (int i = 0; i < runningVisuals.Length; ++i)
        {
            GameObject visual = runningVisuals[i];
            if (visual == null)
                continue;

            if (!active && IsUnsafeRunningVisual(visual))
            {
                WarnUnsafeRunningVisual(visual);
                continue;
            }

            visual.SetActive(active);
        }
    }

    bool IsUnsafeRunningVisual(GameObject visual)
    {
        if (visual == gameObject)
            return true;

        if (bikeVisuals == null)
            return false;

        Transform visualTransform = visual.transform;
        for (int i = 0; i < bikeVisuals.Length; ++i)
        {
            GameObject bikeVisual = bikeVisuals[i];
            if (bikeVisual == null)
                continue;

            Transform bikeTransform = bikeVisual.transform;
            if (bikeTransform == visualTransform || bikeTransform.IsChildOf(visualTransform))
                return true;
        }

        return false;
    }

    void WarnUnsafeRunningVisual(GameObject visual)
    {
        if (m_WarnedUnsafeRunningVisual)
            return;

        Debug.LogWarning("Character bike setup skipped an unsafe Running Visual: " + visual.name + ". Assign only the runner mesh/model object, not the character root and not a parent of the bike visual.", this);
        m_WarnedUnsafeRunningVisual = true;
    }

    void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; ++i)
        {
            if (objects[i] != null)
                objects[i].SetActive(active);
        }
    }

    bool HasAnyAssigned(GameObject[] objects)
    {
        if (objects == null)
            return false;

        for (int i = 0; i < objects.Length; ++i)
        {
            if (objects[i] != null)
                return true;
        }

        return false;
    }
}
