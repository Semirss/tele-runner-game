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
        if (client != null && client.IsConfigured)
        {
            int requestId = ++m_RequestId;
            SetRowsLoading();
            client.FetchLeaderboard(entriesCount, result =>
            {
                if (requestId != m_RequestId || this == null)
                    return;

                if (result.success)
                    ApplyRemoteRows(result.rows);
                else
                    PopulateLocalFallback();
            });
            return;
        }

        PopulateLocalFallback();
    }

    void SetRowsLoading()
    {
        List<HighscoreUI> rows = PrepareRows(1);
        for (int i = 0; i < rows.Count; ++i)
            rows[i].gameObject.SetActive(false);

        if (rows.Count > 0)
        {
            rows[0].gameObject.SetActive(true);
            rows[0].SetData(0, "Loading...", 0);
            if (rows[0].number != null)
                rows[0].number.text = "";
            if (rows[0].score != null)
                rows[0].score.text = "";
        }

        if (playerEntry != null)
            playerEntry.gameObject.SetActive(false);
    }

    void ApplyRemoteRows(List<SupabaseClient.LeaderboardRow> remoteRows)
    {
        int count = remoteRows == null ? 0 : Mathf.Min(entriesCount, remoteRows.Count);
        List<HighscoreUI> rows = PrepareRows(Mathf.Max(count, 1));

        for (int i = 0; i < rows.Count; ++i)
            rows[i].gameObject.SetActive(false);

        for (int i = 0; i < count && i < rows.Count; ++i)
        {
            SupabaseClient.LeaderboardRow remote = remoteRows[i];
            rows[i].gameObject.SetActive(true);
            rows[i].SetData(remote.rank, remote.player_name, remote.score);
        }

        if (count == 0 && rows.Count > 0)
        {
            rows[0].gameObject.SetActive(true);
            rows[0].SetData(0, "No scores yet", 0);
            if (rows[0].number != null)
                rows[0].number.text = "";
            if (rows[0].score != null)
                rows[0].score.text = "";
        }

        SetPlayerEntryVisible(false);
    }

    void PopulateLocalFallback()
    {
        ClearSpawnedRows();

        if (playerEntry != null)
            playerEntry.transform.SetAsLastSibling();

        int localStart = 0;
        int place = -1;
        int localPlace = -1;

        if (displayPlayer && playerEntry != null && playerEntry.score != null)
        {
            int playerScore;
            int.TryParse(playerEntry.score.text, out playerScore);
            place = PlayerData.instance.GetScorePlace(playerScore);
            localPlace = place - localStart;
        }

        if (playerEntry != null)
            playerEntry.gameObject.SetActive(localPlace >= 0 && localPlace < entriesCount && displayPlayer);

        List<HighscoreUI> rows = PrepareRows(entriesCount);
        int currentHighScore = localStart;

        for (int i = 0; i < rows.Count; ++i)
        {
            HighscoreUI hs = rows[i];
            if (hs == null || hs == playerEntry)
                continue;

            if (PlayerData.instance.highscores.Count > currentHighScore)
            {
                hs.gameObject.SetActive(true);
                hs.SetData(localStart + i + 1, PlayerData.instance.highscores[currentHighScore].name, PlayerData.instance.highscores[currentHighScore].score);
                currentHighScore++;
            }
            else
            {
                hs.gameObject.SetActive(false);
            }
        }

        if (forcePlayerDisplay && playerEntry != null)
            playerEntry.gameObject.SetActive(true);

        if (playerEntry != null && playerEntry.number != null)
            playerEntry.number.text = place >= 0 ? (place + 1).ToString() : "";
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

    void ClearSpawnedRows()
    {
        for (int i = 0; i < m_SpawnedRows.Count; ++i)
        {
            if (m_SpawnedRows[i] != null)
                Destroy(m_SpawnedRows[i].gameObject);
        }

        m_SpawnedRows.Clear();
    }

    void SetPlayerEntryVisible(bool visible)
    {
        if (playerEntry != null)
            playerEntry.gameObject.SetActive(visible && displayPlayer);
    }
}
