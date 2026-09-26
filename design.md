# Royalty Boat Game
## HackUMBC 2026 — MVP Game Design Document

**Inspiration:** Bad Piggies × Build a Boat for Treasure × Roblox Robot OA

**Core pitch:** Build a physics-driven boat around a King, launch into procedurally generated hazards, keep the King alive and comfortable, reach the next dry dock, repair and upgrade, then repeat.

---

## 1. Game Overview

The game is a 3D physics-based boat-building survival game built in Unity. The player constructs a boat from modular parts and must safely transport a physical King through increasingly dangerous water courses.

The boat is expendable. The King is the real objective. A run can still succeed if the player reaches the dock with a badly damaged boat, as long as the King survives.

---

## 2. Design Pillars

- **Physics first** — boat shape, mass, balance, buoyancy, damage, and forces should meaningfully affect outcomes.
- **Build your own solution** — players freely construct the boat using 3D grid snapping instead of choosing a preset hull.
- **Protect the King** — the passenger is a physical object that can be thrown around, injured, or lost.
- **Damage matters** — individual modules can crack, break, detach, and alter the boat's behavior.
- **Persistent consequences** — damage survives between levels and repairs cost money.
- **Simple progression** — stronger modules are available from the start but are gated by cost rather than unlock trees.

---

## 3. Core Gameplay Loop

**Dry Dock → Build / Repair → Launch → Survive Obstacles → Reach Dock → Earn Money → Repeat**

### Dry Dock

- The player's current boat persists from the previous level.
- Add new modules, repair damaged modules, replace destroyed parts, or rearrange the design.
- All purchases permanently consume money; removing a module does not refund its cost.
- Repairing damaged components also costs money.

### Voyage

- The course progresses primarily from left to right.
- The player can steer the boat toward the upper or lower parts of the screen.
- Currents, rapids, impacts, hazards, and the boat's own construction affect movement.
- Modules take localized damage and can physically detach.
- The King is affected by real physics and can be thrown around inside or off the boat.

### Arrival

If the King reaches the next dock alive, the player receives money based primarily on the King's remaining health and satisfaction. The player then enters the next dry dock and continues.

---

## 4. Failure Condition

The run ends if the King dies or falls into the water.

The King has a continuous health value. Small impacts can reduce health without immediately ending the run, while major hazards can deal lethal damage instantly.

Examples of lethal hazards include:

- Falling into the water
- A major collision
- A direct lightning strike
- Other large hazards or direct attacks

---

## 5. Boat Building

Boat construction is freeform, modular, and grid-snapped in 3D. There is no required hull shape.

The only core requirement is that the King must be included in the boat when the player launches.

The design should make the following physically meaningful:

- Mass and weight distribution
- Center of mass
- Buoyancy
- Drag
- Propulsion placement
- Structural layout
- Stability
- Protection around the King

The intent is that designing the boat is part of solving the level rather than merely choosing cosmetic pieces.

---

## 6. Physics and Damage

Every module is an independent physical component with properties such as:

- Mass
- Health
- Attachment state
- Collision behavior

A simple damage progression can be:

**Normal → Cracked → Heavily Damaged → Destroyed**

Destroyed components can detach from the boat. Damage should create secondary physics problems rather than only reducing a shared health bar.

For example:

- The boat may become unbalanced or begin spinning.
- A propulsion or steering module may be lost.
- The hull may expose the King.
- A broken section may separate from the main boat.
- Changes in mass distribution may make later hazards harder to survive.

Example interaction:

> Cannonball hits the hull → plank cracks → another hit destroys it → plank breaks away → the boat becomes unbalanced → the King gets thrown toward the exposed side.

---

## 7. The King

The King is a real physics object and the central objective.

The player must design the boat to protect him.

The King can:

- Collide with the boat interior
- Be thrown around by violent movement
- Take non-lethal damage from rough travel
- Fall out of the boat
- Die from major hazards
- Affect the player's final reward through his remaining health

The player should be encouraged to place and protect the King intelligently rather than simply attaching him anywhere.

---

## 8. King Satisfaction and Rewards

Satisfaction provides a reason to build a stable and comfortable boat instead of merely surviving.

### Satisfaction Factors

| Factor | Effect |
|---|---|
| Health | Higher remaining health increases the reward. |
| Ride smoothness | Large impacts, violent rotation, and repeated rough movement reduce satisfaction. |
| Luxury modules | Comfort-focused modules provide positive satisfaction bonuses. |

The exact reward formula is intentionally left for balancing.

For the MVP, a simple model is enough:

**Final Reward = Base Reward × Health / Satisfaction Modifier**

The precise weights can be adjusted later.

---

## 9. Boat Control and Camera

The game uses a 3D angled top-down camera inspired by the Roblox robot assessment presentation.

The boat travels mainly from **left to right** while steering moves it toward the **upper or lower portions of the screen**.

Camera goals:

- Follow the boat or King.
- Keep enough space visible ahead for the player to react to hazards.
- Maintain a clear angled top-down view.
- Avoid feeling like a fully free-roaming third-person vehicle game.

Movement should remain heavily affected by:

- Current direction
- Rapids
- Boat mass
- Boat stability
- Boat damage
- Steering-related modules

Stronger rapids and currents can reduce steering authority.

---

## 10. Modules

Initial module ideas:

| Category | Examples |
|---|---|
| Structure | Wooden planks, stronger wood, metal/ore armor |
| Movement | Sail, paddle wheel, steam engine |
| Control / Utility | Rudder, lightning rod |
| Luxury | Throne, canopy, royal cabin, decorative comfort pieces |

There are no module unlock levels in the MVP.

High-end equipment is available from the beginning but is priced high enough that the player cannot realistically buy it early.

Money is permanently consumed when buying modules.

Removing or selling a module does **not** refund its value.

---

## 11. Damage and Repairs

Damage persists after completing a level.

At the next dry dock, the player can spend money to repair damaged modules.

This creates a tradeoff between:

- Repairing existing components
- Replacing destroyed parts
- Buying stronger modules
- Buying new utility modules
- Continuing with partially damaged components to save money

A player who survives a level with a nearly destroyed boat may still pass, but they will enter the next dock in a much worse economic position.

---

## 12. Obstacles

### Rocks

Static collision hazards.

They can:

- Damage the hull
- Redirect the boat
- Break modules
- Throw the King around

### Icebergs

Larger collision hazards with higher damage potential.

They should require more deliberate steering than small rocks.

### Poisonous / Hazardous Water

Danger zones that punish the player for entering them.

Possible effects include:

- Damaging exposed boat parts
- Damaging the King if sufficiently exposed
- Applying a temporary environmental effect

The exact behavior can be simplified for the MVP.

### Skeleton Ships

Enemy ships that appear during the voyage and fire cannonballs.

Cannonballs can:

- Damage individual boat modules
- Break attachments
- Potentially hit the King directly

Player-controlled combat can be explored later. For the MVP, skeleton ships can simply act as hazards that the player survives.

### Whirlpools

Whirlpools apply inward and rotational forces to the boat.

They are particularly dangerous to:

- Unbalanced boats
- Large boats
- Boats with weak structural connections
- Boats with poor steering

### Thunderstorms

Lightning periodically strikes the level.

Possible effects:

- Heavy module damage
- Direct lethal damage to the King
- Temporary disruption

A **lightning rod** can act as a defensive module against this hazard.

### Rapids / Currents

Water regions that apply strong directional forces.

They can:

- Push the boat vertically
- Reduce steering authority
- Push the player toward obstacles
- Amplify existing instability

---

## 13. Procedural Level Generation

For the MVP, procedural generation should remain simple rather than using a complicated probabilistic model.

The recommended approach is a **weighted obstacle chunk generator**.

A level is built from sequential chunks of water.

For each chunk:

1. Select an obstacle or obstacle pattern from a weighted pool.
2. Randomize relevant parameters such as position, size, strength, or direction.
3. Place the chunk.
4. Continue until the target level length is reached.

Example early-game weighting:

| Obstacle | Relative Weight |
|---|---|
| Rocks | High |
| Rapids | High |
| Icebergs | Medium |
| Whirlpool | Low |
| Skeleton Ship | Low |
| Thunderstorm | Low |

Purely independent random obstacle placement should be avoided because it could create impossible or uninteresting situations.

A better approach is to use **predefined obstacle patterns with randomized parameters**.

Examples:

- Rock cluster
- Narrow rock passage
- Rapid section
- Whirlpool near a safe lane
- Skeleton ship encounter
- Thunderstorm zone

Later stages can combine patterns.

Examples:

- Rapids + rocks
- Rocks + skeleton ship
- Whirlpool + narrow passage
- Rapids + thunderstorm

This allows procedural variety without requiring a complicated generator.

---

## 14. Difficulty Scaling

Difficulty should increase as the player progresses through docks.

Possible scaling variables:

- Higher obstacle density
- Stronger currents
- Narrower safe paths
- Larger obstacles
- More dangerous obstacle combinations
- Faster projectiles
- Higher environmental damage
- Longer stages

The economy should scale alongside the difficulty.

The intended progression loop is:

**Better boat → harder waters → more money → better boat → harder waters**

---

## 15. MVP Scope

The hackathon MVP should prioritize proving the core idea rather than implementing every possible module or hazard.

### Must Have

- Unity 3D project
- Angled top-down camera
- Left-to-right course progression
- Grid-snapped boat building
- Physical King
- Boat physics
- Per-module health
- Module destruction / detachment
- King health
- Basic satisfaction calculation
- Dry dock between levels
- Money system
- Persistent boat damage
- Repair costs
- At least a few purchasable modules
- Procedurally assembled obstacle sections
- At least 3 distinct obstacle types

### Strong Additions

- Visual crack states
- Lightning rod interaction
- Skeleton ship cannon fire
- Luxury modules
- Better water forces
- More sophisticated reward balancing
- More obstacle combinations

### Out of Scope for Initial MVP

- Deep combat system
- Large technology tree
- Complex unlock progression
- Highly sophisticated procedural-generation AI/model
- Large number of module types
- Large number of enemy types
- Fully polished long-term economy

---

## 16. Core Player Experience

The intended player experience is:

1. Look at the available money and modules.
2. Build something that seems capable of surviving.
3. Place and protect the King.
4. Launch the boat.
5. Watch the construction interact with real physics.
6. Steer through hazards while parts crack and break.
7. Try to keep the King alive and comfortable.
8. Limp into the next dock, possibly with half the original boat missing.
9. Decide whether to repair, replace, or upgrade.
10. Launch again into a harder course.

The fun should come from seeing a player-created machine survive — or catastrophically fail — under physics.

---

## 17. Game Identity

The most important design principle is:

> **The King is the objective. The boat is the solution.**

The game should not require the boat itself to remain intact.

A successful run may end with:

- Missing hull sections
- Broken propulsion
- Severe structural damage
- A heavily injured King
- Only a small remaining portion of the original construction

If the King reaches the next dock alive, the player succeeds.

How safely and comfortably they transported him determines how well they are rewarded.
