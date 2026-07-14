using UnityEngine;
using System.Collections;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Defines a consumable, called a power up in game.
/// </summary>
public abstract class Consumable : MonoBehaviour
{
    public float duration;

    public enum ConsumableType
    {
        NONE,
        COIN_MAG,
        SCORE_MULTIPLAYER,
        INVINCIBILITY,
        EXTRALIFE,
        BIKE_LANE,
        MAX_COUNT
    }

    public Sprite icon;
    public AudioClip activatedSound;
    public AssetReference ActivatedParticleReference;
    public bool canBeSpawned = true;

    public bool active { get { return m_Active; } }
    public float timeActive { get { return m_SinceStart; } }

    protected bool m_Active = true;
    protected float m_SinceStart;
    protected ParticleSystem m_ParticleSpawned;

    public abstract ConsumableType GetConsumableType();
    public abstract string GetConsumableName();
    public abstract int GetPrice();
    public abstract int GetPremiumCost();

    public void ResetTime()
    {
        m_SinceStart = 0;
    }

    public virtual bool CanBeUsed(CharacterInputController c)
    {
        return true;
    }

    public virtual IEnumerator Started(CharacterInputController c)
    {
        m_SinceStart = 0;

        if (ActivatedParticleReference == null || !ActivatedParticleReference.RuntimeKeyIsValid())
            yield break;

        AsyncOperationHandle<GameObject> op;
        try
        {
            op = ActivatedParticleReference.InstantiateAsync();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Unable to spawn consumable activation particle: " + e.Message);
            yield break;
        }

        yield return op;

        GameObject particleObject = op.Result;
        if (particleObject == null)
        {
            if (op.IsValid())
                Addressables.Release(op);
            yield break;
        }

        m_ParticleSpawned = particleObject.GetComponent<ParticleSystem>();
        if (m_ParticleSpawned == null)
        {
            Addressables.ReleaseInstance(particleObject);
            yield break;
        }

        if (!m_ParticleSpawned.main.loop)
            StartCoroutine(TimedRelease(m_ParticleSpawned.gameObject, m_ParticleSpawned.main.duration));

        if (c != null && c.characterCollider != null)
            m_ParticleSpawned.transform.SetParent(c.characterCollider.transform);

        m_ParticleSpawned.transform.localPosition = particleObject.transform.position;
    }

    IEnumerator TimedRelease(GameObject obj, float time)
    {
        yield return new WaitForSeconds(time);
        Addressables.ReleaseInstance(obj);
    }

    public virtual void Tick(CharacterInputController c)
    {
        m_SinceStart += Time.deltaTime;
        if (m_SinceStart >= duration)
            m_Active = false;
    }

    public virtual void Ended(CharacterInputController c)
    {
        if (c == null)
            return;

        if (m_ParticleSpawned != null && m_ParticleSpawned.main.loop)
            Addressables.ReleaseInstance(m_ParticleSpawned.gameObject);

        AudioSource source = c == null ? null : c.powerupSource;
        if (CanUseAudioSource(source) && activatedSound != null && source.clip == activatedSound)
            source.Stop();

        for (int i = 0; i < c.consumables.Count; ++i)
        {
            if (c.consumables[i].active && c.consumables[i].activatedSound != null && CanUseAudioSource(source))
            {
                source.clip = c.consumables[i].activatedSound;
                source.Play();
            }
        }
    }

    bool CanUseAudioSource(AudioSource source)
    {
        return source != null && source.enabled && source.gameObject.activeInHierarchy;
    }
}
