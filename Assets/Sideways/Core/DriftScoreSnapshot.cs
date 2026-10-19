namespace Sideways.Core
{
    public readonly struct DriftScoreSnapshot
    {
        public DriftScoreSnapshot(int totalScore, int bankedScore, int activeComboScore, float multiplier, float comboDriftTime, float graceRemaining, bool scoring)
        {
            TotalScore = totalScore;
            BankedScore = bankedScore;
            ActiveComboScore = activeComboScore;
            Multiplier = multiplier;
            ComboDriftTime = comboDriftTime;
            GraceRemaining = graceRemaining;
            Scoring = scoring;
        }

        public int TotalScore { get; }
        public int BankedScore { get; }
        public int ActiveComboScore { get; }
        public float Multiplier { get; }
        public float ComboDriftTime { get; }
        public float GraceRemaining { get; }
        public bool Scoring { get; }
        public bool HasActiveCombo => ActiveComboScore > 0;
    }
}
