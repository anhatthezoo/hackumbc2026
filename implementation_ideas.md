# Royalty Boat Game — Implementation Ideas
## HackUMBC 2026 MVP Technical Notes

This document collects possible Unity implementation approaches for the game defined in `design.md`.

It is a starting point for prototyping, not a binding technical specification. The team should change or replace these ideas when playtesting, time constraints, or Unity behavior reveal a better approach. Example formulas, class names, values, and system boundaries are illustrative unless the design document says otherwise.

The focus is not on making the most physically perfect simulation possible. The focus is on building a system that:

- Feels physics-driven
- Supports freeform boat construction
- Supports damage and breakage
- Keeps the King physically simulated
- Is stable enough for a hackathon
- Can be expanded later without rewriting everything

---

# 1. High-Level Architecture

Recommended major systems:

```text
GameManager
├── GameState / Economy
├── DryDockManager
├── BoatBuilder
├── BoatAssembly
│   ├── BoatChunk
│   ├── Modules
│   ├── BoatPhysics
│   └── Damage / Structural Graph
├── KingController
├── LevelGenerator
│   └── ObstacleChunks
├── CameraController
└── UI
```

The main game states should be simple:

```text
DryDock
→ Launch
→ Voyage
→ LevelComplete
→ DryDock
→ ...
```

Failure moves to:

```text
Voyage
→ KingDeath
→ GameOver
```

A simple enum is enough:

```csharp
public enum GameState
{
    DryDock,
    Voyage,
    LevelComplete,
    GameOver
}
```

Avoid building a complicated state machine framework for the MVP.

---

# 2. Recommended Project Structure

Suggested Unity folder structure:

```text
Assets/
├── Art/
├── Audio/
├── Materials/
├── Prefabs/
│   ├── Modules/
│   ├── Obstacles/
│   ├── LevelChunks/
│   └── Effects/
├── ScriptableObjects/
│   ├── Modules/
│   └── Obstacles/
├── Scripts/
│   ├── Core/
│   ├── Boat/
│   ├── Building/
│   ├── King/
│   ├── Obstacles/
│   ├── Generation/
│   └── UI/
└── Scenes/
```

For the hackathon, one primary gameplay scene is probably enough.

---

# 3. Module Data

Every buildable piece should be driven by a shared module definition rather than hardcoding values into every prefab.

Use a `ScriptableObject`.

Example:

```csharp
[CreateAssetMenu(menuName = "Boat/Module Definition")]
public class ModuleDefinition : ScriptableObject
{
    public string moduleId;
    public string displayName;

    public GameObject prefab;

    public int price;
    public float maxHealth;
    public float mass;

    public Vector3Int gridSize;

    public ModuleCategory category;

    public float luxuryValue;
}
```

For modules that are not simple boxes, `gridSize` can be replaced or supplemented by a list of occupied cell offsets. Rotate those offsets with the module before checking occupancy. This keeps multi-cell and irregular pieces accurate at every allowed rotation.

Possible categories:

```csharp
public enum ModuleCategory
{
    Structural,
    Propulsion,
    Steering,
    Utility,
    Luxury
}
```

This lets balancing happen from the Inspector without changing code.

---

# 4. Module Runtime Component

Every placed module prefab should have a component similar to:

```csharp
public class BoatModule : MonoBehaviour
{
    public ModuleDefinition definition;

    public float currentHealth;

    public Vector3Int gridPosition;

    public BoatChunk chunk;
}
```

Each module should know:

- Its definition
- Current health
- Grid position
- Rotation
- Which boat chunk it currently belongs to

The module prefab should also contain:

- Collider
- Mesh / model
- Damage visuals if available

---

# 5. Boat Building System

## Recommended Approach

Use a local 3D grid attached to the dry dock / boat build area.

The player:

1. Selects a module.
2. Moves the mouse over the build area.
3. Raycasts into the build space.
4. Converts the hit position into boat-local coordinates.
5. Snaps that position to the nearest grid cell.
6. Shows a transparent preview.
7. Clicks to place.

Conceptually:

```csharp
Vector3 local = buildRoot.InverseTransformPoint(hit.point);

Vector3 snapped = new Vector3(
    Mathf.Round(local.x / gridSize) * gridSize,
    Mathf.Round(local.y / gridSize) * gridSize,
    Mathf.Round(local.z / gridSize) * gridSize
);
```

The exact axes can be adapted to the final build orientation.

---

# 6. Grid Occupancy

Do not determine placement validity purely through physics overlap checks.

Maintain an explicit grid occupancy map.

Example:

```csharp
Dictionary<Vector3Int, BoatModule> occupiedCells;
```

A module definition can occupy one or more grid cells.

When placing:

```text
Requested cells
→ Check occupancy dictionary
→ If all free, allow placement
→ Register module in those cells
```

This is more reliable than repeatedly using collider overlap tests.

Physics overlap checks can still be used as a second validation layer.

---

# 7. Placement Rules

For the MVP:

- 90-degree rotations only
- Grid-snapped placement
- No overlapping modules
- New pieces must touch an existing piece

This creates a connected structure.

A new module can be considered attached if one of its occupied cells has a face-adjacent occupied neighbor.

Use the six cardinal directions:

```text
+x
-x
+y
-y
+z
-z
```

No diagonal structural connection.

This same adjacency system can later be reused for destruction.

Face adjacency can mean "structurally attached" for the first prototype. If playtesting needs weak joints, attachment-specific modules, or touching pieces that are not bonded, store explicit connections and optional connection strength instead of treating every neighboring cell as a permanent edge.

---

# 8. The King During Building

The King should exist physically in the build area while the player constructs the boat.

Possible MVP starting rule:

> The King starts at a default position in the dry dock, but the player can reposition him before launch and build the boat around that placement.

If movable King placement proves too costly during the hackathon, a fixed starting position is an acceptable temporary simplification, but it should be recognized as reducing the player's ability to solve the protection problem through placement.

At launch, validate that the King is physically supported by the boat.

Possible launch validation is:

- Raycast downward from the King
- Require the raycast to hit a boat module within a short distance
- Reject placement or launch states where the King overlaps a module

An overall boat bounds check can be used as a loose secondary check, but bounds alone do not prove that the King is enclosed or protected. The primary requirement is that the King is physically supported and travels with the constructed boat at launch.

Do not weld the King to the boat.

The entire point is that he remains a physical Rigidbody.

---

# 9. Boat Physics — Recommended Architecture

This is the most important implementation decision.

## Do NOT Use One FixedJoint Per Module

The obvious solution would be:

```text
Plank Rigidbody
↕ FixedJoint
Plank Rigidbody
↕ FixedJoint
Plank Rigidbody
```

This is likely to cause:

- Joint jitter
- Explosions from accumulated physics errors
- Poor performance
- Extremely difficult tuning
- Unpredictable behavior as module count increases

For a hackathon, this is risky.

---

# 10. Use One Rigidbody Per Connected Boat Chunk

Recommended model:

```text
BoatChunk
├── Rigidbody
├── Module A collider
├── Module B collider
├── Module C collider
└── Module D collider
```

Modules themselves do not need independent Rigidbodies while connected.

Instead, their colliders are children of the `BoatChunk` Rigidbody.

Unity treats these as a compound collider.

This produces much more stable physics.

---

# 11. Module Mass and Center of Mass

Because the connected modules share a Rigidbody, calculate the total mass manually.

```text
Boat mass = sum(module masses)
```

Also calculate the boat's center of mass from the modules:

```text
COM = Σ(modulePosition × moduleMass) / totalMass
```

Then assign it to the Rigidbody.

Whenever a module is:

- Added
- Removed
- Destroyed

recalculate:

- Rigidbody mass
- Center of mass

This makes building shape and weight distribution matter.

---

# 12. Structural Connectivity

Represent the boat as a graph.

Each module is a node.

An edge exists when two modules are directly attached on the construction grid.

Example:

```text
A — B — C
    |
    D
```

This graph already mostly exists implicitly through the grid occupancy dictionary.

When a module is destroyed:

1. Remove it from the occupancy map.
2. Find neighboring modules.
3. Run BFS or DFS over the remaining modules.
4. Detect connected components.

Example:

```text
Before:

A — B — C
    |
    D

Destroy B:

A

C

D
```

Now the structure contains three separate physical chunks.

---

# 13. Splitting the Boat

If destruction disconnects the boat:

```text
Original BoatChunk
      ↓
Connected component search
      ↓
Chunk 1
Chunk 2
Chunk 3
```

Create one `BoatChunk` GameObject for each connected component.

Move the appropriate modules under each chunk.

Each chunk receives:

- Rigidbody
- Calculated mass
- Calculated center of mass
- Current velocity
- Current angular velocity

The new chunks should inherit the original boat's motion so they do not suddenly stop when separated. Copy the angular velocity. For linear velocity, use the old Rigidbody's velocity at each new chunk's center of mass, for example `oldBody.GetPointVelocity(newCenterWorld)`, so a rotating boat produces physically consistent fragment motion.

Calculate and assign each Rigidbody's center of mass in that chunk's local space after reparenting its modules.

This provides the Bad Piggies-style breakup behavior without needing hundreds of joints.

---

# 14. Hackathon Simplification for Breakage

If full connected-component splitting becomes too time-consuming, use this fallback:

- Destroyed module detaches as visible Rigidbody debris
- Remaining boat stays one Rigidbody
- Do not calculate larger disconnected pieces

This is less physically accurate, but the core destruction effect still works.

Recommended priority:

### Tier 1
Individual modules visually break and detach.

### Tier 2
Full graph-based boat splitting.

Implement Tier 2 only after Tier 1 works reliably.

---

# 15. Buoyancy

Unity does not provide boat buoyancy automatically.

For the MVP, use custom buoyancy forces.

Each buoyant module should contain one or more `BuoyancyPoint` positions.

For every point below the water surface:

```text
submergedDepth = waterHeight - pointY
```

Apply upward force:

```text
force = submergedDepth × buoyancyStrength
```

Clamp `submergedDepth` to a configured maximum so a deeply submerged point cannot generate an unbounded force. A small damping force based on the point's vertical velocity can reduce oscillation. Treat the exact force curve, limits, and damping as tuning values discovered through prototyping.

Use:

```csharp
rigidbody.AddForceAtPosition(
    Vector3.up * force,
    point.position
);
```

Using `AddForceAtPosition` is important because it naturally produces torque.

A boat with uneven buoyancy can tilt.

---

# 16. Buoyancy Points

Simple modules can use one buoyancy point.

Large modules can use multiple points.

Example plank:

```text
+---------+
|  x   x  |
|         |
|  x   x  |
+---------+
```

Four buoyancy points provides much more believable stability than one center point.

For the MVP, even one or two points per module may be enough.

---

# 17. Water Drag

Apply extra drag when the boat is submerged.

Simplest implementation:

```text
linear water drag
angular water drag
```

These values can be based on how many buoyancy points are underwater.

This helps prevent:

- Endless oscillation
- Unrealistic spinning
- Extremely slippery motion

Do not attempt full fluid simulation.

---

# 18. Forward Movement

The course progresses left to right.

Choose one world axis as forward, for example:

```text
+X = course direction
+Z = steering direction
+Y = vertical
```

The boat can receive baseline current force:

```csharp
rb.AddForce(Vector3.right * currentStrength);
```

This means even a basic boat moves through the level.

Movement modules then modify this.

Tune the baseline current so propulsion modules still create a noticeable advantage. Calm or special course sections can reduce the current and make propulsion more important.

---

# 19. Propulsion Modules

Example modules:

- Sail
- Paddle wheel
- Steam engine

Each can simply contribute forward force.

Example:

```csharp
public class PropulsionModule : MonoBehaviour
{
    public float thrust;
}
```

Each active propulsion module can apply forward force at its own world position with `AddForceAtPosition`. This keeps the behavior simple while allowing asymmetric propulsion placement or the loss of an engine to turn and destabilize the boat.

If that behavior is too difficult to tune initially, summing propulsion into a centered forward force is a useful prototype step. It should not be treated as the final behavior if propulsion placement is meant to remain physically meaningful.

---

# 20. Steering

Player input primarily controls vertical screen movement.

If:

```text
+X = forward
+Z = sideways
```

then steering applies force in `Z`.

Example:

```csharp
float steering = Input.GetAxis("Horizontal");

rb.AddForce(Vector3.forward * steering * steeringForce);
```

You can additionally apply torque if you want the boat to physically turn rather than purely strafe.

A more physical implementation:

```text
Rudder produces sideways force at rear of boat
→ force generates torque
→ boat rotates
→ forward motion changes course
```

Possible MVP approach:

Use a combination of:

- Small sideways force
- Small yaw torque

This will feel responsive without completely bypassing physics.

Scale or enable this control using the steering modules that remain attached to the King-carrying boat section. Losing the rudder should reduce steering authority. Applying some rudder force at the module's position is an inexpensive way to make its placement affect rotation, while a small assisted sideways force can preserve responsiveness.

---

# 21. Currents and Rapids

Implement currents as trigger volumes.

```text
RapidVolume
├── BoxCollider (Is Trigger)
└── CurrentForce
```

Any `BoatChunk` inside receives directional force.

Example:

```csharp
rb.AddForce(currentDirection * strength);
```

Different rapid zones can:

- Push upward/downward on screen
- Increase forward speed
- Push toward rocks
- Reduce effective player steering

This is extremely cheap to build and easy to tune.

---

# 22. King Physics

King GameObject:

```text
King
├── Rigidbody
├── CapsuleCollider
├── KingHealth
└── KingSatisfactionTracker
```

Do not parent the King to the boat during gameplay.

He should independently collide with:

- Floor modules
- Walls
- Obstacles
- Detached pieces

Physics settings should probably use:

- Continuous collision detection
- Moderate mass
- Locked/unlocked rotation depending on desired ragdoll behavior

For an MVP, a single Rigidbody capsule is easier than a full ragdoll.

---

# 23. King Damage

King damage can come from:

### Collision Damage

Use collision impact strength.

Possible metric:

```text
collision impulse / King mass
```

or simply relative velocity.

Use a minimum threshold so tiny bumps do nothing.

Conceptually:

```text
impact < safeThreshold
→ no damage

impact > safeThreshold
→ damage based on impact strength
```

### Hazard Damage

Hazards can directly call:

```csharp
kingHealth.TakeDamage(amount);
```

### Instant Death

Examples:

```csharp
kingHealth.Kill();
```

Use for:

- Falling into water
- Direct severe lightning strike
- Extremely large impact

---

# 24. Detecting King Falling Into Water

Add a King-only trigger at or just below the water surface.

```text
King enters KillVolume
→ Game Over
```

This is simpler and more reliable than trying to determine whether the King is swimming. Its depth should match the intended rule: place it close to the surface for immediate failure, or slightly lower if a brief splash/grace period feels better in playtesting.

---

# 25. King Satisfaction

Do not build a complicated satisfaction simulation.

Track three main things during a voyage:

```text
Health
Ride roughness
Luxury
```

Possible final score:

```text
HealthScore = currentHealth / maxHealth

SmoothnessScore = clamp(
    1 - accumulatedRoughness / expectedRoughness
)

LuxuryScore = clamp01(
    sum(activeLuxuryModules) / expectedLuxuryValue
)
```

To avoid counting health twice, keep health separate from satisfaction:

```text
Satisfaction =
    smoothnessWeight × SmoothnessScore
  + luxuryWeight × LuxuryScore
```

Then combine `HealthScore` and `Satisfaction` when calculating the final reward. Keep every score normalized to a known range, such as 0–1, and expose the weights in the Inspector.

---

# 26. Measuring Ride Roughness

A simple approach is to track King acceleration.

Every physics step:

```text
acceleration =
    (currentVelocity - previousVelocity) / fixedDeltaTime
```

Ignore small acceleration.

Accumulate only unusually strong changes.

Also accumulate penalties when:

- King collides strongly with something
- Boat angular velocity is extremely high

This gives a rough approximation of comfort.

Do not attempt to calculate realistic G-forces.

---

# 27. Luxury Modules

Luxury pieces should simply contribute satisfaction.

Example:

```csharp
public class LuxuryModule : MonoBehaviour
{
    public float satisfactionValue;
}
```

At level completion:

```text
LuxuryBonus = sum(luxury modules still attached/intact)
```

Destroyed luxury modules should not count.

---

# 28. Module Damage

Every module tracks:

```text
maxHealth
currentHealth
```

Damage sources include:

- Collision
- Cannonballs
- Lightning
- Environmental hazards

When:

```text
currentHealth <= 0
```

call:

```text
BoatStructure.DestroyModule(module)
```

---

# 29. Collision Damage

The easiest collision damage approach is based on impact impulse or relative velocity.

Do not damage modules from tiny continuous contacts.

Example logic:

```text
impact <= minimumImpact
→ 0 damage

impact > minimumImpact
→ damage proportional to excess impact
```

A large rock collision should damage the modules actually touching the rock.

This makes where the player places armor matter.

Because connected modules use child colliders under a shared Rigidbody, resolve each collision contact's collider back to its owning `BoatModule`. Do not assume a callback on the chunk root identifies which module was hit.

---

# 30. Damage Visuals

For the MVP, use either:

### Material / Texture Swap

```text
Healthy
Cracked
Damaged
```

or:

### Simple Decal / Crack Mesh

When health passes thresholds:

```text
> 66% = normal
33–66% = cracked
< 33% = heavily damaged
```

This provides clear feedback without complicated deformation.

---

# 31. Repair System

At the dry dock, each damaged module can be repaired.

Suggested repair formula:

```text
missingHealthPercent =
    1 - currentHealth / maxHealth

repairCost =
    modulePrice
    × missingHealthPercent
    × repairMultiplier
```

Example:

```text
Module price: $100
50% damaged
Repair multiplier: 0.5

Repair cost:
100 × 0.5 × 0.5 = $25
```

This is easy to tune.

---

# 32. Economy

Maintain runtime money in a single `GameSession` or `GameManager`.

```csharp
public int money;
```

Purchase:

```text
money >= module.price
→ subtract price
→ allow placement
```

There are no refunds.

Potential starting value:

```text
startingMoney
```

should be exposed in Inspector.

---

# 33. End-of-Level Reward

For the MVP:

```text
reward =
    baseReward
    × performanceMultiplier
```

Performance multiplier can consider:

```text
King health
King satisfaction
Level difficulty
```

Example structure:

```text
performance =
    0.6 × KingHealthRatio
  + 0.4 × SatisfactionRatio
```

Then:

```text
reward = baseReward × performance
```

Exact numbers should be balanced through playtesting.

---

# 34. Persistence Between Docks

The easiest implementation is:

> Keep the same boat objects alive between levels.

At level completion:

1. Stop boat movement.
2. Determine which pieces are recovered.
3. Transition to dry dock.
4. Move the recovered modules into the build area.
5. Restore their logical build-grid coordinates and zero Rigidbody velocity.
6. Switch to build mode.
7. Allow repairs, repositioning, removal, and additions.

This avoids serialization entirely.

For the hackathon, this is strongly preferred.

Choose and communicate one recovery rule. The simplest is to recover the connected boat section physically carrying the King when the finish trigger is reached and treat detached debris as lost. Recovering every surviving section is also possible, but it needs a clear way to collect and reposition those pieces.

Keep each module's logical assembly/grid coordinates separate from its current world transform. Splitting, drifting, and returning to the dock should not corrupt build-grid occupancy. Also distinguish repositioning an owned module from discarding it: rearrangement should not require repurchasing the same piece, while permanently removed modules still give no refund.

---

# 35. If Level Scenes Must Be Reloaded

If separate scenes become necessary, serialize the boat as:

```text
Module ID
Grid position
Rotation
Current health
```

Example:

```csharp
[Serializable]
public struct ModuleSaveData
{
    public string moduleId;
    public Vector3Int position;
    public int rotation;
    public float health;
}
```

Then reconstruct the boat at the next dock.

Avoid doing this unless you actually need multiple scenes.

---

# 36. Procedural Generation — Recommended Strategy

Do not place every rock independently with pure randomness.

Use authored **level chunks**.

Example:

```text
Chunk_Rocks_01
Chunk_Rapids_01
Chunk_Whirlpool_01
Chunk_SkeletonShip_01
Chunk_Storm_01
```

Each chunk has:

- Start point
- End point
- Obstacle spawn points
- Optional random parameters
- Difficulty rating

The generator connects them sequentially.

---

# 37. Weighted Chunk Selection

Each chunk can have:

```text
baseWeight
minimumDifficulty
maximumDifficulty
```

Example:

```text
Rock chunk        40
Rapid chunk       30
Ice chunk         20
Whirlpool chunk   10
```

As difficulty increases, alter the weights.

Later levels can gradually increase the probability of dangerous chunks.

---

# 38. Difficulty

Use a single difficulty value:

```text
difficulty = levelNumber
```

or a normalized value:

```text
difficulty = levelNumber * difficultyGrowth
```

Then scale parameters such as:

- Current force
- Obstacle density
- Cannon fire rate
- Lightning frequency
- Level length
- Enemy projectile speed

Avoid complex adaptive difficulty for the MVP.

---

# 39. Seeded Generation

Use a seeded random number generator.

Advantages:

- Bugs can be reproduced
- The same run can be replayed
- Team members can test identical levels

Store:

```text
runSeed
levelNumber
```

Then derive the level seed from both.

---

# 40. Rocks

Implementation:

```text
Rock
├── Mesh
├── Collider
└── DamageDealer (optional)
```

Most damage should come naturally from collision impact.

Rocks should generally be static.

This is one of the easiest obstacles and should be implemented first.

---

# 41. Icebergs

For the MVP, icebergs can simply be larger rocks with a different visual style.

Later they could:

- Move slowly
- Float
- Break

Do not implement complicated iceberg physics unless time remains.

---

# 42. Poisonous Water

Use trigger volumes.

```text
PoisonWaterVolume
├── Trigger Collider
└── DamageOverTime
```

Possible implementation:

- Detect boat chunks/modules inside
- Apply damage periodically

For the MVP, it may be easier to damage the whole boat chunk or King rather than calculating exact submerged modules.

---

# 43. Whirlpool

Use a trigger cylinder.

For each Rigidbody inside:

```text
directionToCenter =
    whirlpoolCenter - bodyPosition
```

Apply:

```text
inwardForce
+
tangentialForce
```

Conceptually:

```text
F = inward pull + rotational push
```

This naturally makes the boat spiral.

Because the boat is physics-driven, the same obstacle will affect different designs differently.

---

# 44. Thunderstorm

Recommended MVP approach:

1. Periodically choose a target location near the boat.
2. Show a brief warning indicator.
3. After a delay, strike.
4. Damage anything near the strike point.

This creates readable gameplay.

Do not use completely instantaneous random lightning.

---

# 45. Lightning Rod

When lightning selects a strike point:

1. Search for active lightning rods within interception radius.
2. If one exists, redirect the strike to the rod.
3. Damage the rod instead of the King / nearby structure.

This makes the upgrade understandable and useful.

---

# 46. Skeleton Ships

Do not implement full enemy boat AI.

Recommended MVP:

```text
SkeletonShipSpawner
→ spawn ship beside course
→ ship follows simple predefined path
→ periodically fire at player
→ despawn
```

The ship can move on:

- A spline
- A straight path
- A simple waypoint list

No navigation system is needed.

---

# 47. Cannonballs

Use normal Rigidbody projectiles.

```text
Cannonball
├── Rigidbody
├── SphereCollider
└── ProjectileDamage
```

On collision:

```text
BoatModule hit
→ ApplyDamage()
→ destroy cannonball
```

If the King is directly hit:

```text
KingHealth.TakeDamage(...)
```

---

# 48. Camera

Recommended layout:

```text
Boat moves: +X
Screen vertical direction: approximately world Z
Camera: above + behind + offset
```

Use either:

- Cinemachine follow camera
- Simple custom follow script

For a hackathon, Cinemachine is convenient if the team already knows it.

Otherwise a custom camera is trivial.

---

# 49. Camera Follow

The camera should follow the King or a deliberately selected primary boat chunk.

Recommended target:

> Follow the King directly for the simplest reliable MVP behavior.

Because the King is an independent Rigidbody, he does not automatically belong to a structural chunk. If following a chunk produces a better camera, track the chunk currently supporting the King through collision/contact data and fall back to the King when no supporting chunk exists.

Camera position:

```text
targetPosition
+ vertical height
+ backward offset
+ angled Z offset
```

Use smooth interpolation.

---

# 50. Camera Lookahead

Show more of the course ahead than behind.

Because movement is primarily +X:

```text
camera target =
    kingPosition + Vector3.right * lookahead
```

This gives the player time to react.

---

# 51. Dry Dock Transition

The simplest structure is to put docks directly into the same scrolling/generated world.

At the end of a course:

```text
Finish Trigger
→ disable hazards
→ slow / stop boat
→ enter DryDock state
→ reposition surviving boat into build area
```

Then the player rebuilds.

The next procedural section can be generated ahead.

---

# 52. UI

Minimum UI:

### Dry Dock

- Current money
- Module list
- Module prices
- Selected module
- Repair button
- Launch button

### Voyage

- King health
- King satisfaction
- Current stage / distance

### End of Level

- King health
- Satisfaction
- Reward earned
- Repair / next dock transition

Do not build a complicated inventory screen.

---

# 53. Input

Suggested controls:

```text
Mouse:
    select / place modules

R:
    rotate module

Right click / Delete:
    remove module

A / D or Left / Right:
    steer boat

Space / button:
    launch
```

Exact bindings can be changed later.

---

# 54. Suggested Core Scripts

```text
GameManager.cs
GameSession.cs

BoatBuilder.cs
BuildGrid.cs
PlacementPreview.cs

BoatAssembly.cs
BoatChunk.cs
BoatModule.cs
BoatStructureGraph.cs
BoatPhysics.cs
BuoyancyPoint.cs

ModuleDefinition.cs
PropulsionModule.cs
LuxuryModule.cs
LightningRodModule.cs

KingHealth.cs
KingSatisfaction.cs

LevelGenerator.cs
LevelChunk.cs
DifficultyManager.cs

CurrentVolume.cs
Whirlpool.cs
PoisonWater.cs
LightningController.cs
SkeletonShip.cs
Cannonball.cs

CameraFollow.cs

DryDockUI.cs
VoyageUI.cs
```

Do not create every script immediately. This is the intended separation once features grow.

---

# 55. Suggested Implementation Order

## Phase 1 — Prove the Physics

Build:

- Water plane
- One boat Rigidbody
- Several child block colliders
- Custom buoyancy
- King Rigidbody
- Basic steering
- Camera follow

Goal:

> A simple hand-built boat can float and carry the King through water.

Do this before building the editor.

---

## Phase 2 — Boat Builder

Implement:

- Grid
- Ghost preview
- Placement
- Rotation
- Deletion
- Occupancy validation
- Money cost

Goal:

> Player can build the same boat that Phase 1 used manually.

---

## Phase 3 — Damage

Implement:

- Module health
- Collision damage
- Destroyed module removal
- Crack visuals

Goal:

> Hitting a rock visibly damages and destroys the boat.

---

## Phase 4 — Structural Breakage

Implement:

- Adjacency graph
- Connected component detection
- Chunk splitting
- Mass / center-of-mass recalculation

Goal:

> Breaking an important plank can split the boat into physical pieces.

If time becomes tight, stop after individual module destruction.

---

## Phase 5 — Game Loop

Implement:

- King health
- Failure
- Finish trigger
- Dry dock
- Money rewards
- Repairs
- Boat persistence

Goal:

> A complete loop exists from building to voyage to the next dock.

---

## Phase 6 — Procedural Course

Implement:

- Level chunks
- Weighted selection
- Difficulty value
- Rocks
- Rapids
- One additional obstacle

Goal:

> Every voyage is different enough to feel procedural.

---

## Phase 7 — Polish / Extra Hazards

Add whichever are fastest:

- Whirlpool
- Lightning
- Lightning rod
- Skeleton ship
- Luxury pieces
- Better visuals
- Satisfaction UI

---

# 56. MVP Priority

If time becomes limited, prioritize in this order:

```text
1. Boat building
2. Buoyancy / physics
3. Physical King
4. Steering
5. Rocks / collisions
6. Module damage
7. Dry dock / money loop
8. Procedural chunk generation
9. Basic satisfaction and reward calculation
10. Two additional obstacle types
11. Structural splitting
12. Extra hazards and combinations
13. Combat
```

The game is recognizable once the first eight work, but the design document's complete MVP also requires basic satisfaction, at least three distinct obstacle types, and physical module detachment. Full connected-component splitting can remain a later improvement if individual destroyed modules already detach physically.

---

# 57. Important Technical Risks

## Too Many Rigidbodies / Joints

Avoid a Rigidbody and FixedJoint on every module.

Use compound Rigidbody chunks.

---

## Unstable Buoyancy

Start with simple upward forces and heavy damping.

Do not attempt realistic fluid dynamics.

---

## Boat Exploding When Rebuilding Chunks

When splitting:

- Preserve old velocity
- Preserve old angular velocity
- Recalculate center of mass
- Avoid spawning colliders overlapping each other

---

## King Falling Through the Boat

Use continuous collision detection on the King and reasonable physics timestep settings.

Avoid extremely thin floor colliders.

---

## Procedural Impossible Levels

Use authored chunks rather than random obstacle soup.

Randomize inside safe templates.

---

## Physics Depending on Frame Rate

All forces and physics logic belong in `FixedUpdate`.

Do not apply boat forces from regular `Update`.

---

# 58. Recommended Simplifications

For HackUMBC, deliberately fake things when the player will not notice.

Good simplifications:

- Flat water height
- Fake buoyancy forces
- Authored procedural chunks
- Simple King capsule instead of ragdoll
- Simple satisfaction formula
- Skeleton ships on scripted paths
- Icebergs as static obstacles
- No module resale
- No technology tree
- Single gameplay scene
- One Rigidbody per connected boat chunk

These preserve the important player-facing behavior while reducing implementation risk.

---

# 59. Core Technical Principle

The implementation should preserve this relationship:

```text
Player construction
        ↓
Physical behavior
        ↓
Damage / instability
        ↓
King condition
        ↓
Reward / economy
        ↓
Next construction decision
```

Every major system should feed back into the boat-building decision.

The player should be able to look at a failed run and think:

> "That happened because of how I built my boat."

That is the technical and gameplay goal of the MVP.
