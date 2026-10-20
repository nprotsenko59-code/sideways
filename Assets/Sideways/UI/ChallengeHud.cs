using Sideways.Challenges;
using UnityEngine;
using UnityEngine.UI;

namespace Sideways.UI
{
    public sealed class ChallengeHud : MonoBehaviour
    {
        [SerializeField] private ChallengeController controller;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text progressText;
        [SerializeField] private Text timeText;
        [SerializeField] private Button actionButton;
        [SerializeField] private Text actionText;

        private void OnEnable()
        {
            if (controller != null) controller.StatusUpdated += Render;
            if (actionButton != null) actionButton.onClick.AddListener(HandleAction);
        }

        private void OnDisable()
        {
            if (controller != null) controller.StatusUpdated -= Render;
            if (actionButton != null) actionButton.onClick.RemoveListener(HandleAction);
        }

        public void Configure(ChallengeController challengeController, GameObject challengePanel, Text title, Text progress, Text time, Button button, Text buttonLabel)
        {
            controller = challengeController;
            panel = challengePanel;
            titleText = title;
            progressText = progress;
            timeText = time;
            actionButton = button;
            actionText = buttonLabel;
            if (isActiveAndEnabled)
            {
                controller.StatusUpdated += Render;
                actionButton.onClick.AddListener(HandleAction);
            }
            Render(controller.Snapshot);
        }

        private void HandleAction() => controller?.ActivateOrRetry();

        private void Render(ChallengeSnapshot snapshot)
        {
            if (panel == null) return;
            panel.SetActive(true);
            switch (snapshot.State)
            {
                case ChallengeState.Active:
                    titleText.text = snapshot.DisplayName;
                    progressText.text = $"{snapshot.Progress:N0} / {snapshot.Target:N0}";
                    timeText.text = $"{Mathf.CeilToInt(snapshot.TimeRemaining)}s";
                    actionText.text = "STOP";
                    break;
                case ChallengeState.Succeeded:
                    titleText.text = "CHALLENGE COMPLETE";
                    progressText.text = $"+{snapshot.Reward:N0} REWARD";
                    timeText.text = snapshot.DisplayName;
                    actionText.text = "NEXT  [C]";
                    break;
                case ChallengeState.Failed:
                    titleText.text = "CHALLENGE FAILED";
                    progressText.text = snapshot.DisplayName;
                    timeText.text = "THE DRIVE CONTINUES";
                    actionText.text = "RETRY  [C]";
                    break;
                case ChallengeState.Cancelled:
                    titleText.text = "CHALLENGE STOPPED";
                    progressText.text = snapshot.DisplayName;
                    timeText.text = "FREE ROAM";
                    actionText.text = "RETRY  [C]";
                    break;
                default:
                    titleText.text = "OPTIONAL CHALLENGE";
                    progressText.text = "Destroy signs • Drift score";
                    timeText.text = "60 SECOND EVENTS";
                    actionText.text = "START  [C]";
                    break;
            }
        }
    }
}
