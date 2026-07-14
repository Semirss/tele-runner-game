using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Defines a piece of the track and its obstacle path data.
/// </summary>
public class TrackSegment : MonoBehaviour
{
    public Transform pathParent;
    public TrackManager manager;

    public Transform objectRoot;
    public Transform collectibleTransform;

    public AssetReference[] possibleObstacles;

    [HideInInspector]
    public float[] obstaclePositions;

    public float worldLength { get { return m_WorldLength; } }

    protected float m_WorldLength;
    readonly List<GameObject> m_AddressableInstances = new List<GameObject>();
    bool m_CleanedUp;

    void OnEnable()
    {
        m_CleanedUp = false;
        m_AddressableInstances.Clear();

        if (!HasValidPath())
        {
            enabled = false;
            return;
        }

        UpdateWorldLength();

        GameObject obj = new GameObject("ObjectRoot");
        obj.transform.SetParent(transform);
        objectRoot = obj.transform;

        obj = new GameObject("Collectibles");
        obj.transform.SetParent(objectRoot);
        collectibleTransform = obj.transform;
    }

    public void GetPointAtInWorldUnit(float wt, out Vector3 pos, out Quaternion rot)
    {
        float t = m_WorldLength <= 0f ? 0f : wt / m_WorldLength;
        GetPointAt(t, out pos, out rot);
    }

    public void GetPointAt(float t, out Vector3 pos, out Quaternion rot)
    {
        if (!HasValidPath())
        {
            pos = transform.position;
            rot = transform.rotation;
            return;
        }

        float clampedT = Mathf.Clamp01(t);
        float scaledT = (pathParent.childCount - 1) * clampedT;
        int index = Mathf.FloorToInt(scaledT);
        float segmentT = scaledT - index;

        Transform orig = pathParent.GetChild(index);
        if (index == pathParent.childCount - 1)
        {
            pos = orig.position;
            rot = orig.rotation;
            return;
        }

        Transform target = pathParent.GetChild(index + 1);

        pos = Vector3.Lerp(orig.position, target.position, segmentT);
        rot = Quaternion.Lerp(orig.rotation, target.rotation, segmentT);
    }

    protected void UpdateWorldLength()
    {
        m_WorldLength = 0;
        if (!HasValidPath())
            return;

        for (int i = 1; i < pathParent.childCount; ++i)
        {
            Transform orig = pathParent.GetChild(i - 1);
            Transform end = pathParent.GetChild(i);

            Vector3 vec = end.position - orig.position;
            m_WorldLength += vec.magnitude;
        }
    }

    bool HasValidPath()
    {
        return pathParent != null && pathParent.childCount > 0;
    }

    public void TrackAddressableInstance(GameObject instance)
    {
        if (instance != null && instance != gameObject && !m_AddressableInstances.Contains(instance))
            m_AddressableInstances.Add(instance);
    }

    public void UntrackAddressableInstance(GameObject instance)
    {
        if (instance != null)
            m_AddressableInstances.Remove(instance);
    }

    public void Cleanup()
    {
        if (m_CleanedUp)
            return;

        m_CleanedUp = true;

        for (int i = m_AddressableInstances.Count - 1; i >= 0; --i)
        {
            GameObject instance = m_AddressableInstances[i];
            if (instance != null)
                Addressables.ReleaseInstance(instance);
        }
        m_AddressableInstances.Clear();

        if (collectibleTransform != null)
        {
            while (collectibleTransform.childCount > 0)
            {
                Transform t = collectibleTransform.GetChild(0);
                t.SetParent(null);
                if (Coin.coinPool != null)
                    Coin.coinPool.Free(t.gameObject);
                else
                    Destroy(t.gameObject);
            }
        }

        if (!Addressables.ReleaseInstance(gameObject))
            Destroy(gameObject);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!HasValidPath())
            return;

        Color c = Gizmos.color;
        Gizmos.color = Color.red;
        for (int i = 1; i < pathParent.childCount; ++i)
        {
            Transform orig = pathParent.GetChild(i - 1);
            Transform end = pathParent.GetChild(i);

            Gizmos.DrawLine(orig.position, end.position);
        }

        Gizmos.color = Color.blue;
        if (obstaclePositions != null)
        {
            for (int i = 0; i < obstaclePositions.Length; ++i)
            {
                Vector3 pos;
                Quaternion rot;
                GetPointAt(obstaclePositions[i], out pos, out rot);
                Gizmos.DrawSphere(pos, 0.5f);
            }
        }

        Gizmos.color = c;
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(TrackSegment))]
class TrackSegmentEditor : Editor
{
    protected TrackSegment m_Segment;

    public void OnEnable()
    {
        m_Segment = target as TrackSegment;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Add obstacles"))
            ArrayUtility.Add(ref m_Segment.obstaclePositions, 0.0f);

        if (m_Segment.obstaclePositions != null)
        {
            int toremove = -1;
            for (int i = 0; i < m_Segment.obstaclePositions.Length; ++i)
            {
                GUILayout.BeginHorizontal();
                m_Segment.obstaclePositions[i] = EditorGUILayout.Slider(m_Segment.obstaclePositions[i], 0.0f, 1.0f);
                if (GUILayout.Button("-", GUILayout.MaxWidth(32)))
                    toremove = i;
                GUILayout.EndHorizontal();
            }

            if (toremove != -1)
                ArrayUtility.RemoveAt(ref m_Segment.obstaclePositions, toremove);
        }
    }
}
#endif


