using System.Collections.Generic;
using RoyaltyBoat.Flow;
using RoyaltyBoat.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RoyaltyBoat.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class VoyageDistanceTravelledController : MonoBehaviour
    {
        private UIDocument document;
        private Label distanceValue;
        private readonly List<Label> distanceValueShadows = new List<Label>();
        private BoatMovementController boat;
        private Vector3 startingPosition;
        private bool hasStartingPosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene activeScene, LoadSceneMode mode)
        {
            if (!VoyageFlow.IsVoyageActive ||
                (activeScene.name != VoyageFlow.GameplaySceneName &&
                 activeScene.name != VoyageFlow.DevMapSceneName) ||
                FindAnyObjectByType<VoyageDistanceTravelledController>() != null)
            {
                return;
            }

            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("Voyage/DistanceTravelled");
            PanelSettings panelSettings = Resources.Load<PanelSettings>("MainMenu/MainMenuPanelSettings");
            if (layout == null || panelSettings == null)
            {
                Debug.LogError("Distance traveled HUD assets could not be loaded.");
                return;
            }

            GameObject host = new GameObject("Distance Traveled HUD");
            UIDocument uiDocument = host.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;
            uiDocument.visualTreeAsset = layout;
            uiDocument.sortingOrder = 90;
            host.AddComponent<VoyageDistanceTravelledController>();
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            VisualElement root = document.rootVisualElement;
            distanceValue = root.Q<Label>("distanceValue");
            distanceValueShadows.Clear();
            root.Query<Label>(className: "distance-value-shadow")
                .ForEach(label => distanceValueShadows.Add(label));
        }

        private void Update()
        {
            if (boat == null)
            {
                boat = FindAnyObjectByType<BoatMovementController>();
                if (boat == null)
                {
                    return;
                }
            }

            if (!hasStartingPosition)
            {
                startingPosition = boat.transform.position;
                hasStartingPosition = true;
            }

            Vector3 travelled = boat.transform.position - startingPosition;
            travelled.y = 0f;
            string formattedDistance = travelled.magnitude.ToString("0.0") + " m";

            if (distanceValue != null)
            {
                distanceValue.text = formattedDistance;
            }

            foreach (Label shadow in distanceValueShadows)
            {
                shadow.text = formattedDistance;
            }
        }
    }
}
