using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class MissionUI : MonoBehaviour
{
    public RectTransform missionPlace;
    public AssetReference missionEntryPrefab;

    public IEnumerator Open()
    {
        gameObject.SetActive(true);

        foreach (Transform t in missionPlace)
            Addressables.ReleaseInstance(t.gameObject);

        for (int i = 0; i < 3 && PlayerData.instance.missions.Count > i; ++i)
        {
            AsyncOperationHandle op = missionEntryPrefab.InstantiateAsync();
            yield return op;
            if (op.Result == null || !(op.Result is GameObject))
            {
                Debug.LogWarning(string.Format("Unable to load mission entry {0}.", missionEntryPrefab.Asset.name));
                yield break;
            }

            MissionEntry entry = (op.Result as GameObject).GetComponent<MissionEntry>();
            entry.transform.SetParent(missionPlace, false);
            entry.FillWithMission(PlayerData.instance.missions[i], this);
        }
    }

    public void CallOpen()
    {
        gameObject.SetActive(true);
        StartCoroutine(Open());
    }

    public void Claim(MissionBase m)
    {
        PlayerData.instance.ClaimMission(m);
        StartCoroutine(Open());
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
