using System;
using RoyaltyBoat.Economy;
using RoyaltyBoat.Gameplay;
using RoyaltyBoat.King;
using RoyaltyBoat.MapGeneration;
using RoyaltyBoat.Obstacles;
using RoyaltyBoat.Water;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoyaltyBoat.Flow
{
    public static class VoyageFlow
    {
        public const string GameplaySceneName = "Voyage";
        public const string DevMapSceneName = "DevMap";
        public const string ShipBuildingSceneName = "ShipBuilding";
        public const int LevelsPerRun = 3;
        public const int StartingFunds = 500;

        private static Ship builtShip;
        private static KingBuildPlacement builtKing;
        private static Vector3 builtKingLocalPosition;
        private static Quaternion builtKingLocalRotation;
        private static Vector3 builtKingScale;
        private static Vector3 builtShipScale;
        private static int runSeed;
        private static string destinationSceneName = GameplaySceneName;
        private static bool returningToBuilder;
        private static bool levelComplete;

        public static bool IsVoyageActive { get; private set; }
        public static bool IsRunActive { get; private set; }
        public static int CurrentLevel { get; private set; } = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            builtShip = null;
            builtKing = null;
            builtKingLocalPosition = Vector3.zero;
            builtKingLocalRotation = Quaternion.identity;
            builtKingScale = Vector3.one;
            builtShipScale = Vector3.one;
            runSeed = 0;
            destinationSceneName = GameplaySceneName;
            returningToBuilder = false;
            levelComplete = false;
            IsVoyageActive = false;
            IsRunActive = false;
            CurrentLevel = 1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedHandler()
        {
            SceneManager.sceneLoaded -= ConfigureLoadedScene;
            SceneManager.sceneLoaded += ConfigureLoadedScene;
        }

        private static void ConfigureLoadedScene(Scene scene, LoadSceneMode loadMode)
        {
            if (scene.name == ShipBuildingSceneName && returningToBuilder)
            {
                returningToBuilder = false;
                PrepareBuiltShipForBuilding();
                return;
            }

            if (!IsVoyageActive || scene.name != destinationSceneName)
            {
                return;
            }

            if (scene.name == DevMapSceneName)
            {
                DevMapCourse devCourse = UnityEngine.Object.FindAnyObjectByType<DevMapCourse>();
                if (devCourse == null)
                {
                    Debug.LogError("The dev map scene has no DevMapCourse.");
                    return;
                }

                devCourse.ConfigureObstacleDamage();
                PlaceBuiltShipOnWater();
                CreateFinishPoint(devCourse.transform, devCourse.FinishX, CurrentLevel);
                return;
            }

            ProceduralLevelGenerator generator = UnityEngine.Object.FindAnyObjectByType<ProceduralLevelGenerator>();
            if (generator == null)
            {
                Debug.LogError("The gameplay scene has no ProceduralLevelGenerator.");
                return;
            }

            generator.GenerateLevel(runSeed, CurrentLevel);
            PlaceBuiltShipOnWater();
            CreateFinishPoint(generator);
        }

        public static void OpenShipBuilder()
        {
            StartNewRun();
        }

        public static void AdvanceAfterLevel(int completedLevel)
        {
            if (!levelComplete || completedLevel != CurrentLevel)
            {
                return;
            }

            if (CurrentLevel >= LevelsPerRun)
            {
                StartNewRun();
                return;
            }

            UnsubscribeFromKingDeath();
            CurrentLevel++;
            levelComplete = false;
            IsVoyageActive = false;
            returningToBuilder = true;
            destinationSceneName = GameplaySceneName;
            SceneManager.LoadScene(ShipBuildingSceneName);
        }

        public static void MarkLevelComplete(int completedLevel)
        {
            if (IsVoyageActive && completedLevel == CurrentLevel)
            {
                levelComplete = true;
            }
        }

        public static bool LaunchBuiltShip(Ship ship)
        {
            return LaunchBuiltShip(ship, false);
        }

        public static bool LaunchBuiltShip(Ship ship, bool useDevMap)
        {
            if (ship == null)
            {
                Debug.LogError("Cannot launch without a Ship in the building scene.");
                return false;
            }

            ship.AttachTouchingBlocks();
            if (!ship.IsAlive)
            {
                Debug.LogError("Cannot launch a ship without at least one living block.");
                return false;
            }

            if (!ship.AreAllBlocksConnected(out int disconnectedBlockCount))
            {
                Debug.LogError(
                    $"Cannot launch while {disconnectedBlockCount} ship part(s) are disconnected.");
                return false;
            }

            KingBuildPlacement king =
                UnityEngine.Object.FindAnyObjectByType<KingBuildPlacement>();
            if (king == null || !king.IsPlaced || king.SupportingShip != ship)
            {
                Debug.LogError("Cannot launch until the King is placed on the connected ship.");
                return false;
            }

            if (!IsRunActive)
            {
                IsRunActive = true;
                CurrentLevel = 1;
                runSeed = unchecked((int)DateTime.UtcNow.Ticks);
            }

            ship.CenterRootOnStructure();
            builtKingLocalPosition = ship.transform.InverseTransformPoint(
                king.transform.position);
            builtKingLocalRotation = Quaternion.Inverse(ship.transform.rotation)
                * king.transform.rotation;
            builtKingScale = king.transform.localScale;
            builtShipScale = ship.transform.localScale;
            ship.transform.SetParent(null, true);
            UnityEngine.Object.DontDestroyOnLoad(ship.gameObject);
            king.transform.SetParent(null, true);
            UnityEngine.Object.DontDestroyOnLoad(king.gameObject);

            builtShip = ship;
            builtKing = king;
            if (runSeed == 0)
            {
                runSeed = unchecked((int)DateTime.UtcNow.Ticks);
            }

            destinationSceneName = useDevMap ? DevMapSceneName : GameplaySceneName;
            levelComplete = false;
            IsVoyageActive = true;
            SceneManager.LoadScene(destinationSceneName);
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

            if (builtKing != null)
            {
                Transform kingTransform = builtKing.transform;
                kingTransform.localScale = builtKingScale * 2.5f;
                kingTransform.SetPositionAndRotation(
                    shipTransform.TransformPoint(builtKingLocalPosition),
                    shipTransform.rotation * builtKingLocalRotation);
                builtKing.EnterVoyage(builtShip);
            }

            Rigidbody body = builtShip.GetComponent<Rigidbody>();
            if (body != null)
            {
                DisableExtraRigidbodies(builtShip, body);
                builtShip.RefreshPhysicsMass();
                body.isKinematic = false;
                body.useGravity = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.linearDamping = 0.15f;
                body.angularDamping = 0.35f;
                body.maxAngularVelocity = 4f;
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
            buoyancy.enabled = true;

            if (body != null)
            {
                body.constraints = RigidbodyConstraints.None;

                BoatMovementController movement = builtShip.GetComponent<BoatMovementController>();
                if (movement == null)
                {
                    movement = builtShip.gameObject.AddComponent<BoatMovementController>();
                }
                movement.enabled = true;
            }

            VoyageShipPresentation presentation =
                builtShip.GetComponent<VoyageShipPresentation>();
            if (presentation == null)
            {
                presentation = builtShip.gameObject.AddComponent<VoyageShipPresentation>();
            }

            presentation.enabled = true;
            presentation.Configure(builtShip);
            SubscribeToKingDeath();

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

        private static void StartNewRun()
        {
            UnsubscribeFromKingDeath();

            if (builtKing != null)
            {
                builtKing.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(builtKing.gameObject);
            }

            if (builtShip != null)
            {
                builtShip.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(builtShip.gameObject);
            }

            builtShip = null;
            builtKing = null;
            builtShipScale = Vector3.one;
            builtKingScale = Vector3.one;
            CurrentLevel = 1;
            runSeed = unchecked((int)DateTime.UtcNow.Ticks);
            destinationSceneName = GameplaySceneName;
            returningToBuilder = false;
            levelComplete = false;
            IsVoyageActive = false;
            IsRunActive = true;
            EconomyAccess.Current.ResetBalance(StartingFunds);
            SceneManager.LoadScene(ShipBuildingSceneName);
        }

        private static void PrepareBuiltShipForBuilding()
        {
            ShipBuildArea buildArea = UnityEngine.Object.FindAnyObjectByType<ShipBuildArea>();
            if (buildArea == null || builtShip == null || builtKing == null)
            {
                Debug.LogError("Could not return the completed ship to the shipyard.");
                StartNewRun();
                return;
            }

            buildArea.AdoptReturningShip(
                builtShip,
                builtKing,
                builtShipScale,
                builtKingScale);
        }

        private static void SubscribeToKingDeath()
        {
            KingHealth health = builtKing == null
                ? null
                : builtKing.GetComponent<KingHealth>();
            if (health == null)
            {
                return;
            }

            health.Died -= HandleKingDied;
            health.Died += HandleKingDied;
        }

        private static void UnsubscribeFromKingDeath()
        {
            KingHealth health = builtKing == null
                ? null
                : builtKing.GetComponent<KingHealth>();
            if (health != null)
            {
                health.Died -= HandleKingDied;
            }
        }

        private static void HandleKingDied(KingDeathCause cause)
        {
            if (!IsVoyageActive || levelComplete)
            {
                return;
            }

            Debug.Log($"The King died during level {CurrentLevel}. Restarting the run.");
            StartNewRun();
        }

        private static void DisableExtraRigidbodies(Ship ship, Rigidbody rootBody)
        {
            Rigidbody[] bodies = ship.GetComponentsInChildren<Rigidbody>(true);

            foreach (Rigidbody childBody in bodies)
            {
                if (childBody == null || childBody == rootBody)
                {
                    continue;
                }

                childBody.isKinematic = true;
                childBody.detectCollisions = false;
                UnityEngine.Object.Destroy(childBody);
                Debug.LogWarning(
                    "Removed an extra child Rigidbody so the ship uses one stable compound body.",
                    ship);
            }
        }

        private static void CreateFinishPoint(ProceduralLevelGenerator generator)
        {
            if (builtShip == null || generator.GeneratedRoot == null ||
                generator.GeneratedChunks.Count == 0)
            {
                Debug.LogError("Cannot place the finish point without a generated course and ship.");
                return;
            }

            GeneratedChunkInstance cooldown =
                generator.GeneratedChunks[generator.GeneratedChunks.Count - 1];
            float finishX = Mathf.Lerp(cooldown.StartX, cooldown.EndX, 0.75f);

            CreateFinishPoint(generator.GeneratedRoot, finishX, generator.ActiveLevelNumber);
        }

        private static void CreateFinishPoint(Transform parent, float finishX, int levelNumber)
        {
            if (builtShip == null || parent == null)
            {
                Debug.LogError("Cannot place the finish point without a course and ship.");
                return;
            }

            GameObject finishObject = new GameObject("Royal Finish Gate");
            finishObject.transform.SetParent(parent, false);
            finishObject.transform.localPosition = new Vector3(finishX, 0f, 0f);

            finishObject.AddComponent<BoxCollider>();
            VoyageFinishPoint finishPoint = finishObject.AddComponent<VoyageFinishPoint>();
            finishPoint.Configure(builtShip, levelNumber);
        }
    }
}
