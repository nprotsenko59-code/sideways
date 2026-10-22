using Sideways.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sideways.UI
{
    public sealed class ScoreHud : MonoBehaviour
    {
        [SerializeField] private DriftScoreController scoring;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text comboText;
        [SerializeField] private Image comboGraceFill;

        private int lastTotal = int.MinValue;
        private int lastCombo = int.MinValue;
        private int lastMultiplier = int.MinValue;
        private bool lastHasCombo;
        private bool lastScoring;

        private void Update()
        {
            if (scoring == null) return;
            var snapshot = scoring.Snapshot;
            if (scoreText != null && snapshot.TotalScore != lastTotal)
            {
                scoreText.text = snapshot.TotalScore.ToString("N0");
                lastTotal = snapshot.TotalScore;
            }

            if (comboText != null)
            {
                var multiplier = Mathf.RoundToInt(snapshot.Multiplier * 100f);
                if (snapshot.HasActiveCombo != lastHasCombo || snapshot.Scoring != lastScoring || snapshot.ActiveComboScore != lastCombo || multiplier != lastMultiplier)
                {
                    if (snapshot.HasActiveCombo)
                    {
                        var state = snapshot.Scoring ? "DRIFT" : "KEEP IT";
                        comboText.text = $"{state}  +{snapshot.ActiveComboScore:N0}   ×{snapshot.Multiplier:0.00}";
                        comboText.color = snapshot.Scoring ? new Color(1f, 0.82f, 0.2f) : Color.white;
                    }
                    else
                    {
                        comboText.text = "BUILD A DRIFT COMBO";
                        comboText.color = new Color(1f, 1f, 1f, 0.58f);
                    }
                    lastHasCombo = snapshot.HasActiveCombo;
                    lastScoring = snapshot.Scoring;
                    lastCombo = snapshot.ActiveComboScore;
                    lastMultiplier = multiplier;
                }
            }

            if (comboGraceFill != null)
            {
                comboGraceFill.fillAmount = snapshot.HasActiveCombo
                    ? snapshot.Scoring ? 1f : Mathf.Clamp01(snapshot.GraceRemaining / scoring.ComboGraceDuration)
                    : 0f;
            }
        }

        public void Configure(DriftScoreController scoreController, Text totalLabel, Text comboLabel, Image graceFill)
        {
            scoring = scoreController;
            scoreText = totalLabel;
            comboText = comboLabel;
            comboGraceFill = graceFill;
        }
    }
}
