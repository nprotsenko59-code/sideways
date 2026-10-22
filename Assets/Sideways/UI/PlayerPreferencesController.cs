using Sideways.CameraSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sideways.UI
{
    public sealed class PlayerPreferencesController : MonoBehaviour
    {
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private ChaseCameraController chaseCamera;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button audioButton;
        [SerializeField] private Button hapticsButton;
        [SerializeField] private Button contrastButton;
        [SerializeField] private Button largeHudButton;
        [SerializeField] private Button reducedMotionButton;
        [SerializeField] private Text audioLabel;
        [SerializeField] private Text hapticsLabel;
        [SerializeField] private Text contrastLabel;
        [SerializeField] private Text largeHudLabel;
        [SerializeField] private Text reducedMotionLabel;

        private readonly PlayerPreferencesStore store = new();
        private PlayerPreferences preferences;
        private Image[] images;
        private Color[] imageColors;
        private Text[] texts;
        private Color[] textColors;
        private bool subscribed;
        private bool open;
        private float previousTimeScale = 1f;

        public PlayerPreferences Preferences => preferences;
        public bool IsOpen => open;

        private void Awake()
        {
            preferences = store.Load();
            CacheVisuals();
            Apply();
            SetOpen(false);
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            if (open) SetOpen(false);
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Keyboard.current?.pKey.wasPressedThisFrame == true)
            {
                SetOpen(!open);
            }
        }

        public void Configure(
            CanvasScaler scaler,
            ChaseCameraController cameraController,
            GameObject settingsPanel,
            Button showButton,
            Button dismissButton,
            Button audioToggle,
            Text audioToggleLabel,
            Button hapticsToggle,
            Text hapticsToggleLabel,
            Button contrastToggle,
            Text contrastToggleLabel,
            Button largeHudToggle,
            Text largeHudToggleLabel,
            Button reducedMotionToggle,
            Text reducedMotionToggleLabel)
        {
            Unsubscribe();
            canvasScaler = scaler;
            chaseCamera = cameraController;
            panel = settingsPanel;
            openButton = showButton;
            closeButton = dismissButton;
            audioButton = audioToggle;
            audioLabel = audioToggleLabel;
            hapticsButton = hapticsToggle;
            hapticsLabel = hapticsToggleLabel;
            contrastButton = contrastToggle;
            contrastLabel = contrastToggleLabel;
            largeHudButton = largeHudToggle;
            largeHudLabel = largeHudToggleLabel;
            reducedMotionButton = reducedMotionToggle;
            reducedMotionLabel = reducedMotionToggleLabel;
            CacheVisuals();
            preferences = store.Load();
            Apply();
            SetOpen(false);
            if (isActiveAndEnabled) Subscribe();
        }

        public void SetPreferences(PlayerPreferences value)
        {
            preferences = value;
            store.Save(preferences);
            Apply();
        }

        public void SetOpen(bool value)
        {
            if (open == value)
            {
                if (panel != null) panel.SetActive(value);
                return;
            }

            open = value;
            if (panel != null) panel.SetActive(value);
            if (value)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else if (Time.timeScale == 0f)
            {
                Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            }
        }

        private void Subscribe()
        {
            if (subscribed) return;
            openButton?.onClick.AddListener(Open);
            closeButton?.onClick.AddListener(Close);
            audioButton?.onClick.AddListener(ToggleAudio);
            hapticsButton?.onClick.AddListener(ToggleHaptics);
            contrastButton?.onClick.AddListener(ToggleContrast);
            largeHudButton?.onClick.AddListener(ToggleLargeHud);
            reducedMotionButton?.onClick.AddListener(ToggleReducedMotion);
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            openButton?.onClick.RemoveListener(Open);
            closeButton?.onClick.RemoveListener(Close);
            audioButton?.onClick.RemoveListener(ToggleAudio);
            hapticsButton?.onClick.RemoveListener(ToggleHaptics);
            contrastButton?.onClick.RemoveListener(ToggleContrast);
            largeHudButton?.onClick.RemoveListener(ToggleLargeHud);
            reducedMotionButton?.onClick.RemoveListener(ToggleReducedMotion);
            subscribed = false;
        }

        private void Open() => SetOpen(true);
        private void Close() => SetOpen(false);

        private void ToggleAudio() => SetPreferences(new PlayerPreferences(!preferences.AudioEnabled, preferences.HapticsEnabled, preferences.HighContrast, preferences.LargeHud, preferences.ReducedMotion));
        private void ToggleHaptics() => SetPreferences(new PlayerPreferences(preferences.AudioEnabled, !preferences.HapticsEnabled, preferences.HighContrast, preferences.LargeHud, preferences.ReducedMotion));
        private void ToggleContrast() => SetPreferences(new PlayerPreferences(preferences.AudioEnabled, preferences.HapticsEnabled, !preferences.HighContrast, preferences.LargeHud, preferences.ReducedMotion));
        private void ToggleLargeHud() => SetPreferences(new PlayerPreferences(preferences.AudioEnabled, preferences.HapticsEnabled, preferences.HighContrast, !preferences.LargeHud, preferences.ReducedMotion));
        private void ToggleReducedMotion() => SetPreferences(new PlayerPreferences(preferences.AudioEnabled, preferences.HapticsEnabled, preferences.HighContrast, preferences.LargeHud, !preferences.ReducedMotion));

        private void Apply()
        {
            AudioListener.volume = preferences.AudioEnabled ? 1f : 0f;
            if (canvasScaler != null) canvasScaler.referenceResolution = preferences.LargeHud ? new Vector2(1600f, 900f) : new Vector2(1920f, 1080f);
            chaseCamera?.SetReducedMotion(preferences.ReducedMotion);
            ApplyContrast();
            UpdateLabels();
        }

        private void CacheVisuals()
        {
            images = transform.GetComponentsInChildren<Image>(true);
            imageColors = new Color[images.Length];
            for (var i = 0; i < images.Length; i++) imageColors[i] = images[i].color;
            texts = transform.GetComponentsInChildren<Text>(true);
            textColors = new Color[texts.Length];
            for (var i = 0; i < texts.Length; i++) textColors[i] = texts[i].color;
        }

        private void ApplyContrast()
        {
            if (images != null && imageColors != null)
            {
                for (var i = 0; i < images.Length; i++)
                {
                    if (images[i] == null) continue;
                    var color = imageColors[i];
                    if (preferences.HighContrast && color.a >= 0.2f) color.a = Mathf.Max(color.a, 0.96f);
                    images[i].color = color;
                }
            }

            if (texts != null && textColors != null)
            {
                for (var i = 0; i < texts.Length; i++)
                {
                    if (texts[i] == null) continue;
                    var color = textColors[i];
                    if (preferences.HighContrast) color = new Color(1f, 1f, 1f, 1f);
                    texts[i].color = color;
                }
            }
        }

        private void UpdateLabels()
        {
            SetToggleLabel(audioLabel, "AUDIO", preferences.AudioEnabled);
            SetToggleLabel(hapticsLabel, "HAPTICS", preferences.HapticsEnabled);
            SetToggleLabel(contrastLabel, "HIGH CONTRAST", preferences.HighContrast);
            SetToggleLabel(largeHudLabel, "LARGE HUD", preferences.LargeHud);
            SetToggleLabel(reducedMotionLabel, "REDUCED MOTION", preferences.ReducedMotion);
        }

        private static void SetToggleLabel(Text label, string name, bool enabled)
        {
            if (label != null) label.text = $"{name}  {(enabled ? "ON" : "OFF")}";
        }
    }
}
