using System;
using RoyaltyBoat.Gameplay;
using RoyaltyBoat.MapGeneration;
using RoyaltyBoat.Obstacles;
using RoyaltyBoat.Water;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoyaltyBoat.Flow
{
    public static class VoyageFlow
    {
        public const string GameplaySceneName = "SampleScene";
        public const string ShipBuildingSceneName = "ShipBuilding";

        private static Ship builtShip;
        private static int runSeed;

        public static bool IsVoyageActive { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            builtShip = null;
            runSeed = 0;
            IsVoyageActive = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedHandler()
        {
            SceneManager.sceneLoaded -= ConfigureLoadedScene;
            SceneManager.sceneLoaded += ConfigureLoadedScene;
        }

        private static void ConfigureLoadedScene(Scene scene, LoadSceneMode loadMode)
        {
            if (!IsVoyageActive || scene.name != GameplaySceneName)
            {
                return;
            }

            ProceduralLevelGenerator generator = UnityEngine.Object.FindAnyObjectByType<ProceduralLevelGenerator>();
            if (generator == null)
            {
                Debug.LogError("The gameplay scene has no ProceduralLevelGenerator.");
                return;
            }

            generator.GenerateLevel(runSeed, 1);
            PlaceBuiltShipOnWater();
        }

        public static void OpenShipBuilder()
        {
            IsVoyageActive = false;
            builtShip = null;
            SceneManager.LoadScene(ShipBuildingSceneName);
        }

        public static bool LaunchBuiltShip(Ship ship)
        {
            if (ship == null)
            {
                Debug.LogError("Cannot launch without a Ship in the building scene.");
                return false;
            }

            ship.AttachTouchingBlocks();
            ship.transform.SetParent(null, true);
            UnityEngine.Object.DontDestroyOnLoad(ship.gameObject);

            builtShip = ship;
            runSeed = unchecked((int)DateTime.UtcNow.Ticks);
            IsVoyageActive = true;
            SceneManager.LoadScene(GameplaySceneName);
            return true;
        }

        private static void PlaceBuiltShipOnWater()
        {
            if (builtShip == null)
            {
                Debug.LogError("The built ship was lost before the gameplay scene loaded.");
                return;
            }

            Transform shipTransform = builtShip.transform;
            shipTransform.localScale = Vector3.one * 2.5f;
            shipTransform.SetPositionAndRotation(new Vector3(0f, 0.5f, 0f), Quaternion.identity);

            Rigidbody body = builtShip.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = false;
                body.useGravity = true;
            }

            SimpleBuoyantBody flatWaterBuoyancy = builtShip.GetComponent<SimpleBuoyantBody>();
            if (flatWaterBuoyancy != null)
            {
                UnityEngine.Object.Destroy(flatWaterBuoyancy);
            }

            OceanWaveBuoyancy buoyancy = builtShip.GetComponent<OceanWaveBuoyancy>();
            if (buoyancy == null)
            {
                buoyancy = builtShip.gameObject.AddComponent<OceanWaveBuoyancy>();
            }

            if (body != null)
            {
                body.constraints = RigidbodyConstraints.FreezeRotationY;

                BoatMovementController movement = builtShip.GetComponent<BoatMovementController>();
                if (movement == null)
                {
                    builtShip.gameObject.AddComponent<BoatMovementController>();
                }
            }

            VoyageShipPresentation presentation =
                builtShip.GetComponent<VoyageShipPresentation>();
            if (presentation == null)
            {
                presentation = builtShip.gameObject.AddComponent<VoyageShipPresentation>();
            }

            presentation.Configure(builtShip);

            Camera gameplayCamera = Camera.main;
            if (gameplayCamera == null)
            {
                Debug.LogError("The gameplay scene has no Main Camera for the voyage view.");
                return;
            }

            VoyageCameraController cameraController = gameplayCamera.GetComponent<VoyageCameraController>();
            if (cameraController == null)
            {
                cameraController = gameplayCamera.gameObject.AddComponent<VoyageCameraController>();
            }

            cameraController.SetTarget(shipTransform);
        }
    }
}
