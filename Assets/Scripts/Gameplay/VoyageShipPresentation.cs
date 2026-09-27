using System.Collections;
using System.Collections.Generic;
using RoyaltyBoat.Economy;
using RoyaltyBoat.Flow;
using UnityEngine;

namespace RoyaltyBoat.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class VoyageShipPresentation : MonoBehaviour
    {
        private List<Block> displayedBlocks;

        private Ship ship;
        private GUIStyle instructionStyle;
        private GUIStyle balanceStyle;
        private GUIStyle panelStyle;
        private GUIStyle headingStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;

        private bool levelCompleteVisible;
        private int completedLevel;
        private float completionKingHealth;
        private LevelRewardResult completionReward;
        private bool completionTransitionRequested;

        public void Configure(Ship targetShip)
        {
            ship = targetShip;
            levelCompleteVisible = false;
            completionTransitionRequested = false;
            EnsureCollisionBoxes();
            RestoreBuildMaterials();
        }

        public void ShowLevelComplete(
            int level,
            float normalizedKingHealth,
            LevelRewardResult reward)
        {
            completedLevel = Mathf.Max(1, level);
            completionKingHealth = Mathf.Clamp01(normalizedKingHealth);
            completionReward = reward;
            completionTransitionRequested = false;
            levelCompleteVisible = true;
        }

        private void Awake()
        {
            ship = GetComponent<Ship>();
            EnsureRuntimeState();
        }

        private void Start()
        {
            Configure(ship);
        }

        private void OnDisable()
        {
            RestoreBuildMaterials();
        }

        private void EnsureCollisionBoxes()
        {
            if (ship == null)
            {
                return;
            }

            foreach (Block block in ship.GetComponentsInChildren<Block>(true))
            {
                Collider collisionBox = block.GetComponent<Collider>();
                if (collisionBox == null)
                {
                    collisionBox = block.gameObject.AddComponent<BoxCollider>();
                }

                collisionBox.enabled = true;
                collisionBox.isTrigger = false;
            }
        }

        private void RestoreBuildMaterials()
        {
            if (ship == null)
            {
                return;
            }

            foreach (Renderer shipRenderer in ship.GetComponentsInChildren<Renderer>(true))
            {
                shipRenderer.SetPropertyBlock(null);
            }
        }

        private void OnGUI()
        {
            if (ship == null)
            {
                return;
            }

            BuildStyles();
            DrawBalance();
            DrawSteeringInstructions();
            DrawHealthPanel();

            if (levelCompleteVisible)
            {
                DrawCompletionPanel();
            }
        }

        private void BuildStyles()
        {
            if (instructionStyle != null)
            {
                return;
            }

            instructionStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.84f, 0.34f) }
            };

            balanceStyle = new GUIStyle(instructionStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 23
            };

            panelStyle = new GUIStyle(GUI.skin.box);
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = Color.white }
            };
            valueStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold
            };
        }

        private void DrawBalance()
        {
            const float width = 190f;
            Rect balance = new Rect(Screen.width - width - 24f, 24f, width, 54f);
            GUI.Box(balance, $"COINS  {EconomyAccess.Balance}", balanceStyle);
        }

        private void DrawSteeringInstructions()
        {
            const float width = 260f;
            Rect instructions = new Rect((Screen.width - width) * 0.5f, 24f, width, 54f);
            GUI.Box(instructions, "STEER  ↑  ↓", instructionStyle);
        }

        private void DrawHealthPanel()
        {
            EnsureRuntimeState();
            displayedBlocks.Clear();
            foreach (Block block in ship.Blocks)
            {
                if (block != null)
                {
                    displayedBlocks.Add(block);
                }
            }

            float panelHeight = 58f + displayedBlocks.Count * 34f;
            Rect panel = new Rect(24f, 24f, 280f, panelHeight);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(42f, 34f, 240f, 28f), "SHIP INTEGRITY", headingStyle);

            for (int index = 0; index < displayedBlocks.Count; ++index)
            {
                Block block = displayedBlocks[index];
                float normalizedHealth = block.MaxHealth <= 0
                    ? 0f
                    : Mathf.Clamp01((float)block.Health / block.MaxHealth);
                float y = 68f + index * 34f;

                GUI.Label(new Rect(42f, y, 72f, 24f), $"BLOCK {index + 1}", labelStyle);
                Rect background = new Rect(116f, y + 4f, 120f, 16f);
                DrawSolidRect(background, new Color(0.08f, 0.1f, 0.14f, 0.92f));
                DrawSolidRect(
                    new Rect(background.x + 2f, background.y + 2f,
                        (background.width - 4f) * normalizedHealth, background.height - 4f),
                    HealthColor(normalizedHealth));
                GUI.Label(new Rect(238f, y, 48f, 24f),
                    $"{block.Health}", valueStyle);
            }
        }

        private void DrawCompletionPanel()
        {
            const float width = 430f;
            float bonusOffset = completionReward.DecreeBonus > 0 ? 33f : 0f;
            float height = 344f + bonusOffset;
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUI.Box(panel, GUIContent.none, panelStyle);

            GUIStyle centeredHeading = new GUIStyle(headingStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32,
                normal = { textColor = new Color(1f, 0.84f, 0.34f) }
            };
            GUIStyle centeredLabel = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20
            };
            GUIStyle totalStyle = new GUIStyle(centeredLabel)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.84f, 0.34f) }
            };

            GUI.Label(new Rect(panel.x + 20f, panel.y + 18f, width - 40f, 40f),
                $"LEVEL {completedLevel} COMPLETE", centeredHeading);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 67f, width - 40f, 28f),
                $"KING HEALTH  {completionKingHealth:P0}", centeredLabel);
            GUI.Label(new Rect(panel.x + 45f, panel.y + 108f, width - 90f, 26f),
                $"Completion pay                  +{completionReward.CompletionPay}", labelStyle);
            GUI.Label(new Rect(panel.x + 45f, panel.y + 141f, width - 90f, 26f),
                $"King health bonus             +{completionReward.HealthBonus}", labelStyle);
            if (completionReward.DecreeBonus > 0)
            {
                GUI.Label(new Rect(panel.x + 45f, panel.y + 174f, width - 90f, 26f),
                    $"Barry's decree bonus          +{completionReward.DecreeBonus}", labelStyle);
            }
            DrawSolidRect(new Rect(panel.x + 40f, panel.y + 176f + bonusOffset, width - 80f, 2f),
                new Color(1f, 0.84f, 0.34f, 0.7f));
            GUI.Label(new Rect(panel.x + 20f, panel.y + 188f + bonusOffset, width - 40f, 34f),
                $"+{completionReward.TotalReward} COINS", totalStyle);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 232f + bonusOffset, width - 40f, 28f),
                $"NEW BALANCE  {EconomyAccess.Balance}", centeredLabel);

            string buttonText = $"RETURN TO SHOP  •  LEVEL {completedLevel + 1}";
            if (!completionTransitionRequested && GUI.Button(
                    new Rect(panel.x + 65f, panel.y + 278f + bonusOffset, width - 130f, 44f),
                    buttonText))
            {
                completionTransitionRequested = true;
                StartCoroutine(AdvanceAfterCompletionPanel());
            }
        }

        private IEnumerator AdvanceAfterCompletionPanel()
        {
            yield return null;
            VoyageFlow.AdvanceAfterLevel(completedLevel);
        }

        private void EnsureRuntimeState()
        {
            displayedBlocks ??= new List<Block>();
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static Color HealthColor(float normalizedHealth)
        {
            if (normalizedHealth > 0.6f)
            {
                return new Color(0.18f, 0.9f, 0.34f);
            }

            if (normalizedHealth > 0.3f)
            {
                return new Color(1f, 0.72f, 0.12f);
            }

            return new Color(0.95f, 0.16f, 0.12f);
        }
    }
}
