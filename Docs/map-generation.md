# Seeded Map Generation

The default generator builds a finite course along world `+X`. Every chunk is 80 meters long, uses `Z` for lateral steering, and places content relative to the average ocean surface at `Y = 0`. Chunks never contain water meshes or wave simulators; the single global ocean remains responsible for rendering water.

## Runtime API

Place `Assets/MapGeneration/Prefabs/ProceduralLevelGenerator.prefab` in the gameplay scene and call:

```csharp
generator.GenerateLevel(runSeed, levelNumber);
```

The same run seed and level number always produce the same chunk sequence. `GeneratedChunks`, `GeneratedLength`, and `GeneratedRoot` expose the result for camera, finish-dock, and cleanup integrations. Call `ClearLevel()` before leaving the voyage or generating a replacement.

The generator emits `CourseGenerated(runSeed, levelNumber, length)` and `CourseCleared` events. It does not depend on the boat builder, tags, the King, UI, or the ocean implementation.

## Course structure

Every course contains:

1. One open-water warmup chunk.
2. Six weighted hazard chunks at level 1, increasing to ten at higher levels.
3. One open-water cooldown chunk for the future finish dock.

The initial hazard library includes iceberg slaloms, a debris field, an acid route with a safe right lane, a narrow iceberg passage, combined ice and debris, an acid/debris gauntlet, and a low-weight ghost ship encounter from level 3 onward. Each prefab stores a visible authoring route used to verify that at least one reasonable path exists through the arrangement.

Difficulty rises through a larger per-level budget, additional chunks every two levels, minimum-level gates for combined hazards, and selection weights. Repetition cooldowns avoid immediate duplicate chunks. Lane masks prevent incompatible authored entrances and exits from being joined.

## Extending the library

Create a prefab with `LevelChunkAuthoring`, keep its root at the chunk entrance, and place its exit at local `X = 80`. Add a `LevelChunkDefinition` with weight, difficulty cost, level range, and repetition cooldown, then include it in `DefaultLevelCatalog`.

Obstacle content should remain within the 54-meter course width and leave transition areas near local `X = 0` and `X = 80` open. Add currents, whirlpools, or enemy encounters as chunk-local gameplay components without adding another ocean renderer.

The generated content can be rebuilt or validated from Unity under `Tools > Royalty Boat`. Runtime generation, editor builders, and validation share `MapGenerationDefaults`; change course dimensions or default scaling there so the generated assets and validation expectations cannot drift apart.
