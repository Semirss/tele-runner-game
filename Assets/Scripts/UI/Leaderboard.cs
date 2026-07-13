using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Leaderboard : MonoBehaviour
{
    public RectTransform entriesRoot;
    public int entriesCount;
    public HighscoreUI rowPrefab;

    public HighscoreUI playerEntry;
    public bool forcePlayerDisplay;
    public bool displayPlayer = true;

    readonly List<HighscoreUI> m_SpawnedRows = new List<HighscoreUI>();
    int m_RequestId;

    public void Open()
    {
        gameObject.SetActive(true);
        Populate();
    }

    public void Close()
    {
        StartCoroutine(CloseNextFrame());
    }

    IEnumerator CloseNextFrame()
    {
        yield return null;
        gameObject.SetActive(false);
    }

    public void Populate()
    {
        SupabaseClient client = SupabaseClient.instance;
        if (client == null || !client.IsConfigured)
        {
            ApplyRemoteMessage("Configure Supabase");
            return;
        }

        int requestId = ++m_RequestId;
        SetRowsLoading();
        client.FetchLeaderboard(entriesCount, result =>
        {
            if (requestId != m_RequestId || this == null)
                return;

            if (result.success)
                ApplyRemoteRows(result.rows);
            else
                ApplyRemoteMessage("Leaderboard unavailable");
        });
    }

    void SetRowsLoading()
    {
        HideAllRows();
        List<HighscoreUI> rows = PrepareRows(1);

        if (rows.Count > 0)
        {
            rows[0].gameObject.SetActive(true);
            rows[0].SetData(0, "Loading...", 0);
            if (rows[0].number != null)
                rows[0].number.text = "";
            if (rows[0].score != null)
                rows[0].score.text = "";
        }
    }

    void ApplyRemoteRows(List<SupabaseClient.LeaderboardRow> remoteRows)
    {
        HideAllRows();

        int count = remoteRows == null ? 0 : Mathf.Min(entriesCount, remoteRows.Count);
        List<HighscoreUI> rows = PrepareRows(Mathf.Max(count, 1));

        for (int i = 0; i < count && i < rows.Count; ++i)
        {
            SupabaseClient.LeaderboardRow remote = remoteRows[i];
            rows[i].gameObject.SetActive(true);
            rows[i].SetData(remote.rank, remote.player_name, remote.score);
        }

        if (count == 0)
            ApplyRemoteMessage("No scores yet");
    }

    void ApplyRemoteMessage(string message)
    {
        HideAllRows();
        List<HighscoreUI> rows = PrepareRows(1);

        if (rows.Count > 0)
        {
            rows[0].gameObject.SetActive(true);
            rows[0].SetData(0, message, 0);
            if (rows[0].number != null)
                rows[0].number.text = "";
            if (rows[0].score != null)
                rows[0].score.text = "";
        }
    }

    List<HighscoreUI> PrepareRows(int count)
    {
        if (rowPrefab != null)
        {
            while (m_SpawnedRows.Count < count)
            {
                HighscoreUI row = Instantiate(rowPrefab, entriesRoot, false);
                row.gameObject.SetActive(false);
                m_SpawnedRows.Add(row);
            }

            return m_SpawnedRows;
        }

        List<HighscoreUI> rows = new List<HighscoreUI>();
        int childCount = entriesRoot == null ? 0 : entriesRoot.childCount;
        for (int i = 0; i < childCount && rows.Count < count; ++i)
        {
            HighscoreUI row = entriesRoot.GetChild(i).GetComponent<HighscoreUI>();
            if (row != null && row != playerEntry)
                rows.Add(row);
        }

        return rows;
    }

    void HideAllRows()
    {
        if (entriesRoot != null)
        {
            int childCount = entriesRoot.childCount;
            for (int i = 0; i < childCount; ++i)
            {
                HighscoreUI row = entriesRoot.GetChild(i).GetComponent<HighscoreUI>();
                if (row != null && row != playerEntry)
                    row.gameObject.SetActive(false);
            }
        }

        for (int i = 0; i < m_SpawnedRows.Count; ++i)
        {
            if (m_SpawnedRows[i] != null)
                m_SpawnedRows[i].gameObject.SetActive(false);
        }

        if (playerEntry != null)
            playerEntry.gameObject.SetActive(false);
    }
}
