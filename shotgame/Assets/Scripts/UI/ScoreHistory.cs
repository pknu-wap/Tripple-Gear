using System;
using System.Collections.Generic;
using UnityEngine;

internal static class ScoreHistory
{
    private const string Key = "TrippleGear.ScoreHistory";
    private const string StarterScoresKey = "TrippleGear.StarterScoresAdded";

    [Serializable]
    private sealed class Records
    {
        public List<int> scores = new List<int>();
    }

    internal static IReadOnlyList<int> Read() => Load().scores;

    internal static void Save(int score)
    {
        Records records = Load();
        records.scores.Add(score);
        records.scores.Sort((left, right) => right.CompareTo(left));
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(records));
        PlayerPrefs.Save();
    }

    private static Records Load()
    {
        Records records = ReadSavedRecords();
        if (PlayerPrefs.GetInt(StarterScoresKey, 0) != 0) return records;

        if (!records.scores.Contains(18745)) records.scores.Add(18745);
        if (!records.scores.Contains(16025)) records.scores.Add(16025);
        records.scores.Sort((left, right) => right.CompareTo(left));
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(records));
        PlayerPrefs.SetInt(StarterScoresKey, 1);
        PlayerPrefs.Save();
        return records;
    }

    private static Records ReadSavedRecords()
    {
        string json = PlayerPrefs.GetString(Key, string.Empty);
        if (string.IsNullOrEmpty(json)) return new Records();

        try
        {
            Records records = JsonUtility.FromJson<Records>(json);
            return records?.scores != null ? records : new Records();
        }
        catch (ArgumentException)
        {
            return new Records();
        }
    }
}
