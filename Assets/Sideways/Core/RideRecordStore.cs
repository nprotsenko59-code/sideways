using UnityEngine;

namespace Sideways.Core
{
    public sealed class RideRecordStore
    {
        private readonly string bestScoreKey;
        private readonly string lastScoreKey;

        public RideRecordStore(string keyPrefix = "sideways.ride")
        {
            bestScoreKey = $"{keyPrefix}.bestScore";
            lastScoreKey = $"{keyPrefix}.lastScore";
        }

        public int BestScore => PlayerPrefs.GetInt(bestScoreKey, 0);
        public int LastScore => PlayerPrefs.GetInt(lastScoreKey, 0);

        public void Record(int score)
        {
            var safeScore = Mathf.Max(0, score);
            PlayerPrefs.SetInt(lastScoreKey, safeScore);
            if (safeScore > BestScore)
            {
                PlayerPrefs.SetInt(bestScoreKey, safeScore);
            }

            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(bestScoreKey);
            PlayerPrefs.DeleteKey(lastScoreKey);
        }
    }
}
