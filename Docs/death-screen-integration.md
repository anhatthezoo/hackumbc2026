# Death Screen Integration

The death screen uses Unity UI Toolkit. It does not use Canvas, uGUI, TextMeshPro UI, or an EventSystem.

## Add it to gameplay

Instantiate `Assets/Prefabs/UI/DeathScreen.prefab` in the gameplay scene or from the eventual UI/game-state bootstrap. The prefab contains its `UIDocument`, transparent panel settings, and `DeathScreenController`.

The overlay has no background image or opaque fullscreen color. The live gameplay camera remains visible behind it so the destroyed boat can become the final background.

If the King already exists when the overlay enables, the controller finds and subscribes to `KingHealth`. If the King is spawned later, bind it explicitly:

```csharp
deathScreen.BindKing(king.Health);
```

The screen opens automatically when `KingHealth.Died` fires. It can also be controlled directly:

```csharp
deathScreen.Show(KingDeathCause.Collision);
deathScreen.Hide();
```

## Button callbacks

The buttons intentionally expose callbacks without assuming the final game-state implementation:

```csharp
deathScreen.TryAgainRequested += RetryCurrentVoyage;
deathScreen.ReturnToDockRequested += ReturnToDryDock;
```

The same callbacks are available as serialized UnityEvents on the prefab for Inspector wiring later.

## Visual assets

- `DeathScreen.uxml` contains the hierarchy.
- `DeathScreen.uss` contains the layout, colors, typography, hover states, and compact layout.
- `DeathCrownElement` draws the broken crown and accent rays as scalable UI Toolkit vector geometry.
- Lilita One and Patrick Hand are included under the SIL Open Font License; their license files are stored alongside the font files.
