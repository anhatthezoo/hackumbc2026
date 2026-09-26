# King and Economy Integration

These systems intentionally avoid dependencies on the boat builder, water, scene, or UI implementations.

## King prefab

Use `Assets/Prefabs/King/King.prefab`. It is currently a gold sphere with a `Rigidbody`, `SphereCollider`, `KingController`, `KingHealth`, `KingBoatLink`, and `KingCollisionDamage`.

The King remains an independent physics object. `KingBoatLink` records which connected boat Rigidbody currently supports him; it never parents or welds the King to that boat.

The building or boat system should update the link when its support checks change:

```csharp
KingController king = kingObject.GetComponent<KingController>();

// The King is standing on this connected boat chunk.
king.BoatLink.Connect(boatChunkRigidbody, supported: true);

// He is still associated with the chunk but no longer physically supported.
king.BoatLink.SetSupported(false);

// The King is no longer associated with that chunk.
king.BoatLink.Disconnect(boatChunkRigidbody);
```

`king.CanLaunch` is true only while the King is alive and marked as supported by a connected boat Rigidbody. Support detection remains owned by the building system, so it can use a raycast, collider contacts, grid occupancy, or a later approach without modifying the King scripts.

Subscribe to `KingHealth.HealthChanged` and `KingHealth.Died` for game-state and UI integration. Call `KingController.NotifyFellIntoWater()` from the eventual water kill volume.

## Economy stub

Use `EconomyAccess.Current` anywhere that needs the economy contract:

```csharp
using RoyaltyBoat.Economy;

if (EconomyAccess.Current.TrySpend(modulePrice))
{
    // Complete the already-validated purchase.
}

EconomyAccess.Current.AddFunds(levelReward);
int currentBalance = EconomyAccess.Current.Balance;
```

`EconomyService` is created automatically after a scene loads and starts with 500 funds. A deliberately small in-memory implementation is available through `EconomyAccess.Current` before that component is created.

UI can subscribe to `EconomyAccess.Current.BalanceChanged`. Systems that keep a long-lived subscription should also listen to `EconomyAccess.ServiceChanged` and move their balance subscription when the implementation changes.

The final game-session economy can implement `IEconomyService` and install itself with `EconomyAccess.Install(service)`. Existing purchasing, repair, reward, and UI callers do not need to change.
