using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using GameObject = UnityEngine.GameObject;

/// <summary>
/// The TrackManager handles creating track segments, moving them and handling the whole pace of the game.
/// 
/// The cycle is as follows:
/// - Begin is called when the game starts.
///     - if it's a first run, init the controller, collider etc. and start the movement of the track.
///     - if it's a rerun (after watching ads on GameOver) just restart the movement of the track.
/// - Update moves the character and - if the character reaches a certain distance from origin (given by floatingOriginThreshold) -
/// moves everything back by that threshold to "reset" the player to the origin. This allow to avoid floating point error on long run.
/// It also handles creating the tracks segements when needed.
/// 
/// If the player has no more lives, it pushes the GameOver state on top of the GameState without removing it. That way we can just go back to where
/// we left off if the player watches an ad and gets a second chance. If the player quits, then:
/// 
/// - End is called and everything is cleared and destroyed, and we go back to the Loadout State.
/// </summary>
public class TrackManager : MonoBehaviour
{
    static public TrackManager instance { get { return s_Instance; } }
    static protected TrackManager s_Instance;

    static int s_StartHash = Animator.StringToHash("Start");

    public delegate int MultiplierModifier(int current);
    public MultiplierModifier modifyMultiply;

    [Header("Character & Movements")]
    public CharacterInputController characterController;
    public float minSpeed = 5.0f;
    public float maxSpeed = 10.0f;
    public int speedStep = 4;
    public float laneOffset = 1.0f;

    public bool invincible = false;

    [Header("Bike Lane Powerup")]
    [Tooltip("0 = left, 1 = middle, 2 = right.")]
    public int bikeLaneIndex = 2;
    [Tooltip("Multiplier applied to track speed while the bike lane powerup is active.")]
    public float bikeLaneSpeedMultiplier = 1.65f;

    [Header("Objects")]
    public ConsumableDatabase consumableDatabase;
    public MeshFilter skyMeshFilter;

    [Header("Parallax")]
    public Transform parallaxRoot;
    public float parallaxRatio = 0.5f;


    public System.Action<TrackSegment> newSegmentCreated;
    public System.Action<TrackSegment> currentSegementChanged;

    public int trackSeed { get { return m_TrackSeed; } set { m_TrackSeed = value; } }

    public float timeToStart { get { return m_TimeToStart; } }  // Will return -1 if already started (allow to update UI)

    public int score { get { return m_Score; } }
    public int multiplier { get { return m_Multiplier; } }
    public float currentSegmentDistance { get { return m_CurrentSegmentDistance; } }
    public float worldDistance { get { return m_TotalWorldDistance; } }
    public float speed { get { return m_BikeLaneActive ? m_Speed * m_ActiveBikeLaneSpeedMultiplier : m_Speed; } }
    public float speedRatio { get { return Mathf.Clamp01((speed - minSpeed) / Mathf.Max(0.01f, maxSpeed - minSpeed)); } }
    public int currentZone { get { return m_CurrentZone; } }

    public TrackSegment currentSegment { get { return m_Segments[0]; } }
    public List<TrackSegment> segments { get { return m_Segments; } }
    public ThemeData currentTheme { get { return m_CurrentThemeData; } }

    public bool isMoving { get { return m_IsMoving; } }
    public bool isRerun { get { return m_Rerun; } set { m_Rerun = value; } }
    public bool isLoaded { get; set; }
    public bool loadFailed { get; private set; }
    public string loadError { get; private set; }
    public bool bikeLaneActive { get { return m_BikeLaneActive; } }


    protected float m_TimeToStart = -1.0f;

    // If this is set to -1, random seed is init to system clock, otherwise init to that value
    // Allow to play the same game multiple time (useful to make specific competition/challenge fair between players)
    protected int m_TrackSeed = -1;

    protected float m_CurrentSegmentDistance;
    protected float m_TotalWorldDistance;
    protected bool m_IsMoving;
    protected float m_Speed;
    protected bool m_BikeLaneActive;
    protected int m_ActiveBikeLaneIndex = 1;
    protected float m_ActiveBikeLaneSpeedMultiplier = 1.0f;

    protected float m_TimeSincePowerup;     // The higher it goes, the higher the chance of spawning one
    protected float m_TimeSinceLastPremium;

    protected int m_Multiplier;

    protected List<TrackSegment> m_Segments = new List<TrackSegment>();
    protected List<TrackSegment> m_PastSegments = new List<TrackSegment>();
    protected int m_SafeSegementLeft;

    protected ThemeData m_CurrentThemeData;
    protected int m_CurrentZone;
    protected float m_CurrentZoneDistance;
    protected int m_PreviousSegment = -1;

    protected int m_Score;
    protected float m_ScoreAccum;
    protected bool m_Rerun;     // This lets us know if we are entering a game over (ads) state or starting a new game (see GameState)
    protected int m_SegmentSpawnFailures;
    protected float m_NextSegmentSpawnRetryTime;


    
    Vector3 m_CameraOriginalPos = Vector3.zero;
    
    const float k_FloatingOriginThreshold = 10000f;

    protected const float k_CountdownToStartLength = 5f;
    protected const float k_CountdownSpeed = 1.5f;
    protected const float k_StartingSegmentDistance = 2f;
    protected const int k_StartingSafeSegments = 2;
    protected const int k_StartingCoinPoolSize = 256;
    protected const int k_DesiredSegmentCount = 10;
    protected const float k_SegmentRemovalDistance = -30f;
    protected const float k_Acceleration = 0.2f;
    protected const int k_MaxSegmentSpawnFailures = 25;
    protected const float k_FailedSegmentRetryDelay = 0.25f;
    protected const float k_InitialSegmentLoadTimeout = 15.0f;
    
    protected void Awake()
    {
        m_ScoreAccum = 0.0f;
        s_Instance = this;
    }

    public void StartMove(bool isRestart = true)
    {
        characterController.StartMoving();
        m_IsMoving = true;
        if (isRestart)
        {
            m_Speed = minSpeed;
            EndBikeLane();
        }
    }

    public void StopMove()
    {
        m_IsMoving = false;
    }

    public void BeginBikeLane(int laneIndex, float speedMultiplier)
    {
        m_BikeLaneActive = true;
        m_ActiveBikeLaneIndex = Mathf.Clamp(laneIndex, 0, 2);
        m_ActiveBikeLaneSpeedMultiplier = Mathf.Max(1.0f, speedMultiplier);

        if (characterController != null)
        {
            characterController.ForceLane(m_ActiveBikeLaneIndex);
        }
    }

    public void EndBikeLane()
    {
        m_BikeLaneActive = false;
        m_ActiveBikeLaneSpeedMultiplier = 1.0f;
    }

    IEnumerator WaitToStart()
    {
        characterController.character.animator.Play(s_StartHash);
        float length = k_CountdownToStartLength;
        m_TimeToStart = length;

        while (m_TimeToStart >= 0)
        {
            yield return null;
            m_TimeToStart -= Time.deltaTime * k_CountdownSpeed;
        }

        m_TimeToStart = -1;

        if (m_Rerun)
        {
            // Make invincible on rerun, to avoid problems if the character died in front of an obstacle
            characterController.characterCollider.SetInvincible();
        }

        characterController.StartRunning();
        StartMove();
    }

    public IEnumerator Begin()
    {
        isLoaded = false;
        loadFailed = false;
        loadError = string.Empty;
        m_SegmentSpawnFailures = 0;
        m_NextSegmentSpawnRetryTime = 0.0f;

        if (!m_Rerun)
        {
            if (Camera.main != null)
                m_CameraOriginalPos = Camera.main.transform.position;

            if (PlayerData.instance == null || PlayerData.instance.characters == null || PlayerData.instance.characters.Count == 0 || PlayerData.instance.themes == null || PlayerData.instance.themes.Count == 0)
            {
                FailLoad("PlayerData is missing character or theme data.");
                yield break;
            }

            PlayerData.instance.usedCharacter = Mathf.Clamp(PlayerData.instance.usedCharacter, 0, PlayerData.instance.characters.Count - 1);
            PlayerData.instance.usedTheme = Mathf.Clamp(PlayerData.instance.usedTheme, 0, PlayerData.instance.themes.Count - 1);
            
            if (m_TrackSeed != -1)
                Random.InitState(m_TrackSeed);
            else
                Random.InitState((int)System.DateTime.Now.Ticks);

            // Since this is not a rerun, init the whole system (on rerun we want to keep the states we had on death)
            m_CurrentSegmentDistance = k_StartingSegmentDistance;
            m_TotalWorldDistance = 0.0f;

            characterController.gameObject.SetActive(true);

            //Addressables 1.0.1-preview
            // Spawn the player
            var op = Addressables.InstantiateAsync(PlayerData.instance.characters[PlayerData.instance.usedCharacter],
                Vector3.zero,
                Quaternion.identity);
            yield return op;
            if (op.Result == null || !(op.Result is GameObject))
            {
                Debug.LogWarning(string.Format("Unable to load character {0}.", PlayerData.instance.characters[PlayerData.instance.usedCharacter]));
                yield break;
            }
            Character player = op.Result.GetComponent<Character>();

            player.SetupAccesory(PlayerData.instance.usedAccessory);

            characterController.character = player;
            characterController.trackManager = this;

            characterController.Init();
            characterController.CheatInvincible(invincible);
            
            player.transform.SetParent(characterController.characterCollider.transform, false);
            Camera.main.transform.SetParent(characterController.transform, true);

            m_CurrentThemeData = ThemeDatabase.GetThemeData(PlayerData.instance.themes[PlayerData.instance.usedTheme]);

            m_CurrentZone = 0;
            m_CurrentZoneDistance = 0;

            if (skyMeshFilter != null)
                skyMeshFilter.sharedMesh = m_CurrentThemeData.skyMesh;
            RenderSettings.fogColor = m_CurrentThemeData.fogColor;
            RenderSettings.fog = true;

            gameObject.SetActive(true);
            characterController.gameObject.SetActive(true);
            characterController.coins = 0;
            characterController.premium = 0;

            m_Score = 0;
            m_ScoreAccum = 0;

            m_SafeSegementLeft = k_StartingSafeSegments;

            Coin.coinPool = new Pooler(currentTheme.collectiblePrefab, k_StartingCoinPoolSize);

            PlayerData.instance.StartRunMissions(this);

#if UNITY_ANALYTICS
            AnalyticsEvent.GameStart(new Dictionary<string, object>
            {
                { "theme", m_CurrentThemeData.themeName},
                { "character", player.characterName },
                { "accessory",  PlayerData.instance.usedAccessory >= 0 ? player.accessories[PlayerData.instance.usedAccessory].accessoryName : "none"}
            });
#endif
        }

        characterController.Begin();

        // Wait until at least the minimum required segments have loaded via Addressables before starting.
        // Stop waiting if the Addressables/theme setup is broken; otherwise the game can sit on Loading forever.
        float loadStartTime = Time.unscaledTime;
        yield return new WaitUntil(() => m_Segments.Count >= k_StartingSafeSegments + 1 || loadFailed || Time.unscaledTime - loadStartTime >= k_InitialSegmentLoadTimeout);

        if (loadFailed || m_Segments.Count < k_StartingSafeSegments + 1)
        {
            if (!loadFailed)
                FailLoad("Track startup timed out before enough segments loaded. Check theme segment Addressables.");
            yield break;
        }

        isLoaded = true;
        StartCoroutine(WaitToStart());
    }

    public void End()
    {
        for (int i = 0; i < m_Segments.Count; ++i)
        {
            if (m_Segments[i] != null)
                m_Segments[i].Cleanup();
        }

        for (int i = 0; i < m_PastSegments.Count; ++i)
        {
            if (m_PastSegments[i] != null)
                m_PastSegments[i].Cleanup();
        }

        m_Segments.Clear();
        m_PastSegments.Clear();
        _spawnedSegments = 0;

        if (characterController != null)
        {
            characterController.End();

            if (characterController.character != null)
            {
                Addressables.ReleaseInstance(characterController.character.gameObject);
                characterController.character = null;
            }

            characterController.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);

        if (Camera.main != null)
        {
            Camera.main.transform.SetParent(null);
            Camera.main.transform.position = m_CameraOriginalPos;
        }

        if (parallaxRoot != null)
        {
            for (int i = parallaxRoot.childCount - 1; i >= 0; --i)
                Destroy(parallaxRoot.GetChild(i).gameObject);
        }
        _parallaxRootChildren = 0;

        //if our consumable wasn't used, we put it back in our inventory
        if (characterController != null && characterController.inventory != null)
        {
            PlayerData.instance.Add(characterController.inventory.GetConsumableType());
            characterController.inventory = null;
        }
    }

    private int _parallaxRootChildren = 0;
    private int _spawnedSegments = 0;
    void Update()
    {
        if (loadFailed || m_CurrentThemeData == null || m_CurrentThemeData.zones == null || m_CurrentThemeData.zones.Length == 0)
            return;

        if (m_SegmentSpawnFailures < k_MaxSegmentSpawnFailures && Time.unscaledTime >= m_NextSegmentSpawnRetryTime)
        {
            while (_spawnedSegments < k_DesiredSegmentCount)
            {
                StartCoroutine(SpawnNewSegment());
                _spawnedSegments++;
            }
        }

        if (parallaxRoot != null && currentTheme.cloudPrefabs != null && currentTheme.cloudPrefabs.Length > 0)
        {
            while (_parallaxRootChildren < currentTheme.cloudNumber)
            {
                float lastZ = parallaxRoot.childCount == 0 ? 0 : parallaxRoot.GetChild(parallaxRoot.childCount - 1).position.z + currentTheme.cloudMinimumDistance.z;

                GameObject cloud = currentTheme.cloudPrefabs[Random.Range(0, currentTheme.cloudPrefabs.Length)];
                if (cloud != null)
                {
                    GameObject obj = Instantiate(cloud);
                    obj.transform.SetParent(parallaxRoot, false);

                    obj.transform.localPosition =
                        Vector3.up * (currentTheme.cloudMinimumDistance.y +
                                      (Random.value - 0.5f) * currentTheme.cloudSpread.y)
                        + Vector3.forward * (lastZ + (Random.value - 0.5f) * currentTheme.cloudSpread.z)
                        + Vector3.right * (currentTheme.cloudMinimumDistance.x +
                                           (Random.value - 0.5f) * currentTheme.cloudSpread.x);

                    obj.transform.localScale = obj.transform.localScale * (1.0f + (Random.value - 0.5f) * 0.5f);
                    obj.transform.localRotation = Quaternion.AngleAxis(Random.value * 360.0f, Vector3.up);
                    _parallaxRootChildren++;
                }
            }
        }

        if (!m_IsMoving)
            return;

        if (m_Segments.Count == 0 || characterController == null)
            return;

        float scaledSpeed = speed * Time.deltaTime;
        m_ScoreAccum += scaledSpeed;
        m_CurrentZoneDistance += scaledSpeed;

        int intScore = Mathf.FloorToInt(m_ScoreAccum);
        if (intScore != 0) AddScore(intScore);
        m_ScoreAccum -= intScore;

        m_TotalWorldDistance += scaledSpeed;
        m_CurrentSegmentDistance += scaledSpeed;

        if (m_CurrentSegmentDistance > m_Segments[0].worldLength)
        {
            m_CurrentSegmentDistance -= m_Segments[0].worldLength;

            // m_PastSegments are segment we already passed, we keep them to move them and destroy them later 
            // but they aren't part of the game anymore 
            m_PastSegments.Add(m_Segments[0]);
            m_Segments.RemoveAt(0);
            _spawnedSegments--;

            if (currentSegementChanged != null) currentSegementChanged.Invoke(m_Segments[0]);
        }

        Vector3 currentPos;
        Quaternion currentRot;
        Transform characterTransform = characterController.transform;

        m_Segments[0].GetPointAtInWorldUnit(m_CurrentSegmentDistance, out currentPos, out currentRot);


        // Floating origin implementation
        // Move the whole world back to 0,0,0 when we get too far away.
        bool needRecenter = currentPos.sqrMagnitude > k_FloatingOriginThreshold;

        // Parallax Handling
        if (parallaxRoot != null)
        {
            Vector3 difference = (currentPos - characterTransform.position) * parallaxRatio; ;
            int count = parallaxRoot.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform cloud = parallaxRoot.GetChild(i);
                cloud.position += difference - (needRecenter ? currentPos : Vector3.zero);
            }
        }

        if (needRecenter)
        {
            int count = m_Segments.Count;
            for (int i = 0; i < count; i++)
            {
                m_Segments[i].transform.position -= currentPos;
            }

            count = m_PastSegments.Count;
            for (int i = 0; i < count; i++)
            {
                m_PastSegments[i].transform.position -= currentPos;
            }

            // Recalculate current world position based on the moved world
            m_Segments[0].GetPointAtInWorldUnit(m_CurrentSegmentDistance, out currentPos, out currentRot);
        }

        characterTransform.rotation = currentRot;
        characterTransform.position = currentPos;

        if (parallaxRoot != null && currentTheme.cloudPrefabs != null && currentTheme.cloudPrefabs.Length > 0)
        {
            for (int i = 0; i < parallaxRoot.childCount; ++i)
            {
                Transform child = parallaxRoot.GetChild(i);

                // Destroy unneeded clouds
                if ((child.localPosition - currentPos).z < -50)
                {
                    _parallaxRootChildren--;
                    Destroy(child.gameObject);
                }
            }
        }

        // Still move past segment until they aren't visible anymore.
        for (int i = 0; i < m_PastSegments.Count; ++i)
        {
            if ((m_PastSegments[i].transform.position - currentPos).z < k_SegmentRemovalDistance)
            {
                m_PastSegments[i].Cleanup();
                m_PastSegments.RemoveAt(i);
                i--;
            }
        }

        PowerupSpawnUpdate();

        if (m_Speed < maxSpeed)
            m_Speed += k_Acceleration * Time.deltaTime;
        else
            m_Speed = maxSpeed;

        m_Multiplier = 1 + Mathf.FloorToInt(speedRatio * speedStep);

        if (modifyMultiply != null)
        {
            foreach (MultiplierModifier part in modifyMultiply.GetInvocationList())
            {
                m_Multiplier = part(m_Multiplier);
            }
        }

        //check for next rank achieved
        int currentTarget = (PlayerData.instance.rank + 1) * 300;
        if (m_TotalWorldDistance > currentTarget)
        {
            PlayerData.instance.rank += 1;
            PlayerData.instance.Save();
#if UNITY_ANALYTICS
//"level" in our game are milestone the player have to reach : one every 300m
        AnalyticsEvent.LevelUp(PlayerData.instance.rank);
#endif
        }

        PlayerData.instance.UpdateMissions(this);

        MusicPlayer.instance.UpdateVolumes(speedRatio);
    }

    public void PowerupSpawnUpdate()
    {
        m_TimeSincePowerup += Time.deltaTime;
        m_TimeSinceLastPremium += Time.deltaTime;
    }

    public void ChangeZone()
    {
        m_CurrentZone += 1;
        if (m_CurrentZone >= m_CurrentThemeData.zones.Length)
            m_CurrentZone = 0;

        m_CurrentZoneDistance = 0;
    }

    private readonly Vector3 _offScreenSpawnPos = new Vector3(-100f, -100f, -100f);
    public IEnumerator SpawnNewSegment()
    {
        if (m_CurrentThemeData == null || m_CurrentThemeData.zones == null || m_CurrentThemeData.zones.Length == 0)
        {
            CancelPendingSegmentSpawn();
            yield break;
        }

        if (m_CurrentZone < 0 || m_CurrentZone >= m_CurrentThemeData.zones.Length)
            m_CurrentZone = 0;

        if (m_CurrentThemeData.zones[m_CurrentZone].length < m_CurrentZoneDistance)
            ChangeZone();

        AssetReference[] prefabList = m_CurrentThemeData.zones[m_CurrentZone].prefabList;
        if (prefabList == null || prefabList.Length == 0)
        {
            Debug.LogWarning("Unable to spawn track segment because the current theme zone has no segment prefabs.");
            CancelPendingSegmentSpawn();
            yield break;
        }

        int segmentUse = Random.Range(0, prefabList.Length);
        if (segmentUse == m_PreviousSegment) segmentUse = (segmentUse + 1) % prefabList.Length;
        m_PreviousSegment = segmentUse;

        AssetReference segmentReference = prefabList[segmentUse];
        if (segmentReference == null)
        {
            Debug.LogWarning("Unable to spawn track segment because the selected segment reference is empty.");
            CancelPendingSegmentSpawn();
            yield break;
        }

        AsyncOperationHandle<GameObject> segmentToUseOp;
        try
        {
            segmentToUseOp = segmentReference.InstantiateAsync(_offScreenSpawnPos, Quaternion.identity);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Unable to start loading track segment. Key: " + segmentReference.RuntimeKey + " Error: " + e.Message);
            CancelPendingSegmentSpawn();
            yield break;
        }

        yield return segmentToUseOp;

        if (segmentToUseOp.Status != AsyncOperationStatus.Succeeded || segmentToUseOp.Result == null)
        {
            Debug.LogWarning("Unable to load track segment. Key: " + segmentReference.RuntimeKey);
            if (segmentToUseOp.IsValid())
                Addressables.Release(segmentToUseOp);
            CancelPendingSegmentSpawn();
            yield break;
        }

        GameObject segmentObject = segmentToUseOp.Result;
        TrackSegment newSegment = segmentObject.GetComponent<TrackSegment>();
        if (newSegment == null)
        {
            Debug.LogWarning("Loaded segment does not have a TrackSegment component. Key: " + segmentReference.RuntimeKey);
            Addressables.ReleaseInstance(segmentObject);
            CancelPendingSegmentSpawn();
            yield break;
        }

        Vector3 currentExitPoint;
        Quaternion currentExitRotation;
        if (m_Segments.Count > 0)
        {
            m_Segments[m_Segments.Count - 1].GetPointAt(1.0f, out currentExitPoint, out currentExitRotation);
        }
        else
        {
            currentExitPoint = transform.position;
            currentExitRotation = transform.rotation;
        }

        newSegment.transform.rotation = currentExitRotation;

        Vector3 entryPoint;
        Quaternion entryRotation;
        newSegment.GetPointAt(0.0f, out entryPoint, out entryRotation);

        Vector3 pos = currentExitPoint + (newSegment.transform.position - entryPoint);
        newSegment.transform.position = pos;
        newSegment.manager = this;

        newSegment.transform.localScale = new Vector3((Random.value > 0.5f ? -1 : 1), 1, 1);
        if (newSegment.objectRoot != null)
            newSegment.objectRoot.localScale = new Vector3(1.0f / newSegment.transform.localScale.x, 1, 1);

        if (m_SafeSegementLeft <= 0)
        {
            SpawnObstacle(newSegment);
        }
        else
            m_SafeSegementLeft -= 1;

        m_SegmentSpawnFailures = 0;
        m_NextSegmentSpawnRetryTime = 0.0f;

        m_Segments.Add(newSegment);

        if (newSegmentCreated != null) newSegmentCreated.Invoke(newSegment);
    }

    void CancelPendingSegmentSpawn()
    {
        _spawnedSegments = Mathf.Max(0, _spawnedSegments - 1);
        m_SegmentSpawnFailures++;
        m_NextSegmentSpawnRetryTime = Time.unscaledTime + k_FailedSegmentRetryDelay;

        if (m_SegmentSpawnFailures >= k_MaxSegmentSpawnFailures && !loadFailed)
            FailLoad("Too many track segment load failures. Check the current theme segment Addressables.");
    }

    void FailLoad(string message)
    {
        loadFailed = true;
        loadError = string.IsNullOrEmpty(message) ? "Track failed to load." : message;
        isLoaded = false;
        m_IsMoving = false;
        Debug.LogError(loadError);
    }

    public void SpawnObstacle(TrackSegment segment)
    {
        if (!m_BikeLaneActive && segment != null && segment.possibleObstacles != null && segment.possibleObstacles.Length != 0 && segment.obstaclePositions != null)
        {
            for (int i = 0; i < segment.obstaclePositions.Length; ++i)
            {
                AssetReference assetRef = segment.possibleObstacles[Random.Range(0, segment.possibleObstacles.Length)];
                StartCoroutine(SpawnFromAssetReference(assetRef, segment, i));
            }
        }

        StartCoroutine(SpawnCoinAndPowerup(segment));
    }

    private IEnumerator SpawnFromAssetReference(AssetReference reference, TrackSegment segment, int posIndex)
    {
        if (reference == null || segment == null)
            yield break;

        AsyncOperationHandle<GameObject> op;
        try
        {
            op = Addressables.LoadAssetAsync<GameObject>(reference);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Skipping obstacle because its AssetReference is not loadable. Mark the prefab as Addressable. Key: " + reference.RuntimeKey + " Error: " + e.Message);
            yield break;
        }

        yield return op;

        if (op.Status != AsyncOperationStatus.Succeeded || op.Result == null)
        {
            Debug.LogWarning("Skipping obstacle because Addressables could not load it. Mark the prefab as Addressable. Key: " + reference.RuntimeKey);
            if (op.IsValid())
                Addressables.Release(op);
            yield break;
        }

        GameObject obj = op.Result;
        Obstacle obstacle = obj.GetComponent<Obstacle>();
        if (obstacle != null)
            yield return obstacle.Spawn(segment, segment.obstaclePositions[posIndex]);

        if (op.IsValid())
            Addressables.Release(op);
    }
    public IEnumerator SpawnCoinAndPowerup(TrackSegment segment)
    {
        if (segment == null)
            yield break;

        const float increment = 1.5f;
        float currentWorldPos = 0.0f;
        bool forceBikeLaneCoins = m_BikeLaneActive;
        int currentLane = forceBikeLaneCoins ? m_ActiveBikeLaneIndex : Random.Range(0, 3);

        float powerupChance = forceBikeLaneCoins ? 0.0f : Mathf.Clamp01(Mathf.Floor(m_TimeSincePowerup) * 0.5f * 0.001f);
        float premiumChance = forceBikeLaneCoins ? 0.0f : Mathf.Clamp01(Mathf.Floor(m_TimeSinceLastPremium) * 0.5f * 0.0001f);

        while (currentWorldPos < segment.worldLength)
        {
            Vector3 pos;
            Quaternion rot;
            segment.GetPointAtInWorldUnit(currentWorldPos, out pos, out rot);

            bool laneValid = true;
            int testedLane = currentLane;

            if (forceBikeLaneCoins)
            {
                testedLane = m_ActiveBikeLaneIndex;
            }
            else
            {
                while (Physics.CheckSphere(pos + ((testedLane - 1) * laneOffset * (rot * Vector3.right)), 0.4f, 1 << 9))
                {
                    testedLane = (testedLane + 1) % 3;
                    if (currentLane == testedLane)
                    {
                        // Couldn't find a valid lane.
                        laneValid = false;
                        break;
                    }
                }
            }

            currentLane = testedLane;

            if (laneValid)
            {
                pos = pos + ((currentLane - 1) * laneOffset * (rot * Vector3.right));

                GameObject toUse = null;
                if (!forceBikeLaneCoins && consumableDatabase != null && consumableDatabase.consumbales != null && consumableDatabase.consumbales.Length > 0 && Random.value < powerupChance)
                {
                    int picked = Random.Range(0, consumableDatabase.consumbales.Length);

                    //if the powerup can't be spawned, we don't reset the time since powerup to continue to have a high chance of picking one next track segment
                    if (consumableDatabase.consumbales[picked].canBeSpawned)
                    {
                        // Spawn a powerup instead.
                        m_TimeSincePowerup = 0.0f;
                        powerupChance = 0.0f;

                        AsyncOperationHandle op = Addressables.InstantiateAsync(consumableDatabase.consumbales[picked].gameObject.name, pos, rot);
                        yield return op;
                        if (op.Result == null || !(op.Result is GameObject))
                        {
                            Debug.LogWarning(string.Format("Unable to load consumable {0}.", consumableDatabase.consumbales[picked].gameObject.name));
                            if (op.IsValid())
                                Addressables.Release(op);
                            currentWorldPos += increment;
                            continue;
                        }
                        toUse = op.Result as GameObject;
                        segment.TrackAddressableInstance(toUse);
                        toUse.transform.SetParent(segment.transform, true);
                    }
                }
                else if (!forceBikeLaneCoins && currentTheme.premiumCollectible != null && Random.value < premiumChance)
                {
                    m_TimeSinceLastPremium = 0.0f;
                    premiumChance = 0.0f;

                    AsyncOperationHandle op = Addressables.InstantiateAsync(currentTheme.premiumCollectible.name, pos, rot);
                    yield return op;
                    if (op.Result == null || !(op.Result is GameObject))
                    {
                        Debug.LogWarning(string.Format("Unable to load collectable {0}.", currentTheme.premiumCollectible.name));
                        if (op.IsValid())
                            Addressables.Release(op);
                        currentWorldPos += increment;
                        continue;
                    }
                    toUse = op.Result as GameObject;
                    segment.TrackAddressableInstance(toUse);
                    toUse.transform.SetParent(segment.transform, true);
                }
                else
                {
                    toUse = Coin.coinPool.Get(pos, rot);
                    toUse.transform.SetParent(segment.collectibleTransform, true);
                }

                if (toUse != null)
                {
                    //TODO : remove that hack related to #issue7
                    Vector3 oldPos = toUse.transform.position;
                    toUse.transform.position += Vector3.back;
                    toUse.transform.position = oldPos;
                }
            }

            currentWorldPos += increment;
        }
    }
    public void AddScore(int amount)
    {
        int finalAmount = amount;
        m_Score += finalAmount * m_Multiplier;
    }
}








