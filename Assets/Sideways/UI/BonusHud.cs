using Sideways.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sideways.UI
{
    public sealed class BonusHud : MonoBehaviour
    {
        [SerializeField] private DriftScoreController scoring;
        [SerializeField] private Text label;
        [SerializeField] private float visibleDuration = 1.6f;

        private float remaining;

        private void OnEnable()
        {
            if (scoring != null) scoring.BonusAwarded += HandleBonus;
        }

        private void OnDisable()
        {
            if (scoring != null) scoring.BonusAwarded -= HandleBonus;
        }

        private void Update()
        {
            if (remaining <= 0f) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0f && label != null) label.gameObject.SetActive(false);
        }

        public void Configure(DriftScoreController scoreController, Text bonusLabel)
        {
            scoring = scoreController;
            label = bonusLabel;
            if (isActiveAndEnabled && scoring != null) scoring.BonusAwarded += HandleBonus;
            if (label != null) label.gameObject.SetActive(false);
        }

        private void HandleBonus(int points, string reason)
        {
            if (label == null) return;
            label.text = $"{reason.ToUpperInvariant()}  +{points:N0}";
            label.gameObject.SetActive(true);
            remaining = visibleDuration;
        }
    }
}
