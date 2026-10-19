using Sideways.Police;
using UnityEngine;
using UnityEngine.UI;

namespace Sideways.UI
{
    public sealed class PoliceHud : MonoBehaviour
    {
        [SerializeField] private PoliceDirector director;
        [SerializeField] private Text stateText;
        [SerializeField] private Image panel;

        private float spottedRemaining;
        private float bustedRemaining;
        private readonly string[] searchLabels = new string[21];

        private void Awake()
        {
            for (var i = 0; i < searchLabels.Length; i++) searchLabels[i] = $"SEARCHING  {i}";
        }

        private void OnEnable()
        {
            if (director == null) return;
            director.StateChanged += HandleStateChanged;
            director.StatusUpdated += HandleStatus;
            director.PlayerBusted += HandleBusted;
        }

        private void OnDisable()
        {
            if (director == null) return;
            director.StateChanged -= HandleStateChanged;
            director.StatusUpdated -= HandleStatus;
            director.PlayerBusted -= HandleBusted;
        }

        private void Update()
        {
            spottedRemaining = Mathf.Max(0f, spottedRemaining - Time.deltaTime);
            bustedRemaining = Mathf.Max(0f, bustedRemaining - Time.deltaTime);
        }

        public void Configure(PoliceDirector policeDirector, Text label, Image background)
        {
            director = policeDirector;
            stateText = label;
            panel = background;
            if (isActiveAndEnabled)
            {
                director.StateChanged += HandleStateChanged;
                director.StatusUpdated += HandleStatus;
                director.PlayerBusted += HandleBusted;
            }
            Render("PATROL", new Color(0.08f, 0.12f, 0.18f, 0.8f));
        }

        private void HandleStateChanged(PoliceStateTransition transition)
        {
            if (transition.Current == PoliceState.Pursuit) spottedRemaining = 1.5f;
        }

        private void HandleBusted()
        {
            bustedRemaining = 2f;
            Render("BUSTED — COMBO LOST", new Color(0.65f, 0.05f, 0.04f, 0.92f));
        }

        private void HandleStatus(PoliceStatusSnapshot status)
        {
            if (bustedRemaining > 0f) return;
            switch (status.State)
            {
                case PoliceState.Pursuit:
                    Render(spottedRemaining > 0f ? "SPOTTED" : "PURSUED", new Color(0.70f, 0.06f, 0.04f, 0.9f));
                    break;
                case PoliceState.Search:
                    var seconds = Mathf.Clamp(Mathf.CeilToInt(status.SearchRemaining), 0, searchLabels.Length - 1);
                    Render(searchLabels[seconds], new Color(0.75f, 0.42f, 0.03f, 0.9f));
                    break;
                case PoliceState.Escaped:
                    Render("ESCAPED", new Color(0.03f, 0.55f, 0.30f, 0.9f));
                    break;
                default:
                    Render("PATROL", new Color(0.08f, 0.12f, 0.18f, 0.8f));
                    break;
            }
        }

        private void Render(string value, Color color)
        {
            if (stateText != null) stateText.text = value;
            if (panel != null) panel.color = color;
        }
    }
}
