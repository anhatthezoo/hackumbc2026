# Obstacle Prefabs

These prefabs are authored building blocks for the future seeded map generator. They do not own water rendering and should be placed in fixed world space under the generated course root.

## Assets

- `Iceberg.prefab` is a static, faceted collision obstacle. Place its root at the average water height (`Y = 0`).
- `FloatingLog.prefab` and `DebrisCluster.prefab` are physical obstacles with lightweight two-point buoyancy. Their `SimpleBuoyantBody.WaterHeight` defaults to `0` and can later be updated by a CPU water-surface service.
- `AcidicWater.prefab` is an 18 x 18 meter trigger region. Its collider spans from `Y = -3.5` to `Y = 0.5`, so modest visual waves do not create gaps in hazard detection.

Every prefab includes `ObstacleDescriptor`, which gives map generation a stable ID, kind, difficulty cost, and X/Z footprint without coupling the prefab to one generator implementation.

## Damage integration

Acid deals 20 damage per second in 0.25-second ticks. It directly supports the current `KingHealth`. Future boat modules should implement:

```csharp
public interface IHazardDamageReceiver
{
    void ApplyHazardDamage(float amount, HazardType hazardType, GameObject source);
}
```

The volume groups colliders by their attached Rigidbody or transform root, so a multi-collider boat receives one damage tick instead of one tick per collider. When a target implements `IHazardDamageReceiver`, that receiver owns distribution of damage across its modules.

Iceberg and debris damage remains impact-driven. The current King already converts collision speed into damage through `KingCollisionDamage`; future boat modules can use the same relative-impact approach.

## Rebuilding

The generated assets can be rebuilt from Unity with `Tools > Royalty Boat > Rebuild Obstacle Prefabs`. The builder recreates the iceberg mesh and updates materials and prefabs without modifying a scene.
