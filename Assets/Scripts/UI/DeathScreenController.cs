using System;
using RoyaltyBoat.Audio;
using RoyaltyBoat.King;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace RoyaltyBoat.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class DeathScreenController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private KingHealth kingHealth;
        [SerializeField] private bool visibleOnEnable;
        [SerializeField] private UnityEvent tryAgainRequested;
        [SerializeField] private UnityEvent returnToDockRequested;

        private VisualElement overlay;
        private VisualElement crownHost;
        private Button tryAgainButton;
        private Button returnToDockButton;
        private Label subtitle;

        public bool IsVisible => overlay != null && !overlay.ClassListContains("is-hidden");

        public event Action TryAgainRequested;
        public event Action ReturnToDockRequested;

        private void Awake()
        {
            uiDocument ??= GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            CacheVisualElements();
            RegisterUiCallbacks();

            if (kingHealth == null)
            {
                kingHealth = FindAnyObjectByType<KingHealth>();
            }

            SubscribeToKing();

            if (visibleOnEnable)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromKing();
            UnregisterUiCallbacks();
        }

        public void BindKing(KingHealth newKingHealth)
        {
            if (kingHealth == newKingHealth)
            {
                return;
            }

            UnsubscribeFromKing();
            kingHealth = newKingHealth;

            if (isActiveAndEnabled)
            {
                SubscribeToKing();
            }
        }

        public void Show(KingDeathCause cause = KingDeathCause.Unknown)
        {
            bool wasVisible = IsVisible;
            if (overlay == null)
            {
                CacheVisualElements();
            }

            subtitle.text = GetSubtitle(cause);
            overlay.RemoveFromClassList("is-hidden");
            overlay.BringToFront();
            tryAgainButton.Focus();
            if (!wasVisible)
            {
                GameAudio.PlayFailure();
            }
        }

        public void Hide()
        {
            if (overlay == null)
            {
                return;
            }

            overlay.AddToClassList("is-hidden");
        }

        private void CacheVisualElements()
        {
            VisualElement root = uiDocument.rootVisualElement;
            GameAudio.BindUi(root);
            overlay = root.Q<VisualElement>("death-screen");
            crownHost = root.Q<VisualElement>("crown-host");
            tryAgainButton = root.Q<Button>("try-again-button");
            returnToDockButton = root.Q<Button>("return-to-dock-button");
            subtitle = root.Q<Label>("subtitle");

            crownHost.Clear();
            crownHost.Add(new DeathCrownElement());
        }

        private void RegisterUiCallbacks()
        {
            tryAgainButton.clicked += OnTryAgainClicked;
            returnToDockButton.clicked += OnReturnToDockClicked;
            overlay.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void UnregisterUiCallbacks()
        {
            if (tryAgainButton != null)
            {
                tryAgainButton.clicked -= OnTryAgainClicked;
            }

            if (returnToDockButton != null)
            {
                returnToDockButton.clicked -= OnReturnToDockClicked;
            }

            overlay?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void SubscribeToKing()
        {
            if (kingHealth != null)
            {
                kingHealth.Died += OnKingDied;
            }
        }

        private void UnsubscribeFromKing()
        {
            if (kingHealth != null)
            {
                kingHealth.Died -= OnKingDied;
            }
        }

        private void OnKingDied(KingDeathCause cause)
        {
            Show(cause);
        }

        private void OnTryAgainClicked()
        {
            TryAgainRequested?.Invoke();
            tryAgainRequested?.Invoke();
        }

        private void OnReturnToDockClicked()
        {
            ReturnToDockRequested?.Invoke();
            returnToDockRequested?.Invoke();
        }

        private void OnGeometryChanged(GeometryChangedEvent geometryEvent)
        {
            overlay.EnableInClassList("is-compact", geometryEvent.newRect.width < 900f);
        }

        private static string GetSubtitle(KingDeathCause cause)
        {
            return cause switch
            {
                KingDeathCause.Collision => "A brutal impact ended the royal voyage...",
                KingDeathCause.Water => "His Majesty has gone overboard...",
                KingDeathCause.Capsized => "The royal vessel has capsized...",
                KingDeathCause.Lightning => "A shocking end to the royal voyage...",
                KingDeathCause.Projectile => "The royal defenses have failed...",
                KingDeathCause.EnvironmentalHazard => "The poisoned waters claimed His Majesty...",
                _ => "Your royal voyage has come to an end..."
            };
        }
    }
}
