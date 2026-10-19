using System;
using Sideways.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sideways.UI
{
    public sealed class RideResultsView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text reasonText;
        [SerializeField] private GameObject scoreGroup;
        [SerializeField] private Text scoreText;
        [SerializeField] private Button restartButton;

        public event Action RestartRequested;

        private void Awake()
        {
            if (restartButton != null) restartButton.onClick.AddListener(HandleRestart);
        }

        private void OnDestroy()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(HandleRestart);
        }

        public void Configure(GameObject rootPanel, Text reasonLabel, GameObject scores, Text scoreLabel, Button restart)
        {
            panel = rootPanel;
            reasonText = reasonLabel;
            scoreGroup = scores;
            scoreText = scoreLabel;
            restartButton = restart;
        }

        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        public void Show(RideEndReason reason, int? score, int? best)
        {
            if (reasonText != null) reasonText.text = reason == RideEndReason.Flipped ? "RIDE OVER — CAR FLIPPED" : "RIDE OVER";
            var hasScore = score.HasValue;
            if (scoreGroup != null) scoreGroup.SetActive(hasScore);
            if (hasScore && scoreText != null) scoreText.text = $"SCORE  {score.Value:N0}\nBEST   {best.GetValueOrDefault():N0}";
            SetVisible(true);
        }

        private void HandleRestart() => RestartRequested?.Invoke();
    }
}
