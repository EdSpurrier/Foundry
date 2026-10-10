# Foundry

Foundry is the gameplay behaviour layer built on top of FrameCoreU.

Where FrameCoreU provides the underlying systems such as events, pooling, timing, sound, and scene flow, Foundry is where those systems are turned into actual gameplay behaviour through Actions, Transformers, and Triggers.

It is designed to be modular, reusable, inspector-driven, and easy to extend.

---

## Overview

**FrameCoreU** = Core systems  
**Foundry** = Reusable gameplay building blocks  
**Game layer** = Project-specific mechanics and content

Foundry sits between the framework and the game itself.

It is the layer where abstract systems become practical behaviour.

---

## Purpose

Foundry exists to provide a flexible behaviour toolkit for building gameplay without tightly coupling logic directly into one-off scripts.

It is intended to:

- keep gameplay systems modular
- make event-driven behaviour easy to build
- support inspector-based workflows
- allow reuse across scenes and projects
- separate core framework code from game-specific code

---

## Core Concepts

## Actions

Actions are discrete units of behaviour that execute when triggered by an event.

They extend `FrameAction` and define what actually happens when an event fires.

Examples include:

- spawning objects
- playing sounds
- triggering other systems
- performing reusable gameplay responses

Actions are designed to be:

- serializable
- reusable
- toggleable
- event-driven

---

## Transformers

Transformers are components that continuously process behaviour over time.

They extend `Transformer` and run on a chosen Unity update loop such as:

- `Update`
- `FixedUpdate`
- `LateUpdate`

Transformers are useful for behaviours like:

- following targets
- smoothing transforms
- procedural movement
- maintaining live relationships between objects

They are intended to be simple, reusable runtime processors.

---

## Triggers

Triggers are responsible for detecting conditions and activating events.

Examples may include:

- trigger volumes
- collision or impact triggers
- state-based triggers
- interaction triggers

Triggers are the detection layer that connects the game world to the event system.

---

## Example Flow

A typical Foundry flow looks like this:

1. A trigger detects something
2. The trigger activates an event
3. The event executes one or more actions
4. Those actions create gameplay results
5. Transformers may continue updating behaviour over time

Example:

- player enters a trigger volume
- a `FrameCoreEvent` is activated
- a `Spawn` action creates an enemy
- a `Sound` action plays an audio cue
- a `Follower` transformer makes an object track the target

---

## Current Components

Foundry has grown well past its early building blocks. This list reflects what is actually implemented — keep it in sync when Actions/Transformers/Triggers are added or removed.

### Actions

General:
- `Spawn` — instantiate a prefab (pooled) at a fixed position/rotation or at a reference Transform, with optional parenting.
- `Sound` — play a sound by index from an assigned `SoundBank`.
- `Activate` (class `Activator`) — set a list of GameObjects active.
- `Deactivate` (class `Deactivator`) — set a list of GameObjects inactive.
- `Destroy` — destroy a GameObject / set of GameObjects.
- `EventManagerAction` (0.8.0) — start `EventManager` chains from any event: the EventManagers listed (optionally letting a Single Use one run again with **Allow Repeat**), or one from an `EventManagerLibrary` by name or at random. This lets several switches share one sequence.
- `InteractableAction` (0.8.0) — use Interactables from any event: Use, Turn On, Turn Off, Flip or Reset. For example, a `VolumeTrigger3D`, a timer or another switch can drive one.
- `Despawn` — return GameObjects to the pool they were spawned from (destroying any that weren't pooled), with an optional delay. Use this rather than `Destroy` for anything created by `Spawn`. Requires FrameCoreU `0.3.0`.
- `Parent` — parent a Transform to another.
- `Unparent` — clear a Transform's parent.
- `Attach` — attach objects via a `AttachmentGroup` (position/rotation offset onto a socket).
- `Detach` — detach objects previously attached via an `AttachmentGroup`.
- `Animation` — set a bool/int/float/trigger parameter on a Mecanim `Animator`.
- `PhysicsAction` — invoke `IPhysicsAction.Activate()` on an assigned behaviour (bridge to `PhysicsImpulse2D`/`PhysicsImpulse3D`, etc.).
- `TransformerAction` — activate/deactivate an assigned `ITransformerAction` (e.g. toggle a `Follower` on/off).

Camera (all extend `CameraActionBase`, which resolves an explicit `CameraCore` or falls back to `Foundry.Camera`):
- `CameraSetTarget` — change the camera's follow target.
- `CameraOffset` — change the camera's follow offset.
- `CameraZoom` — change the camera's orthographic zoom.
- `CameraLookAhead` — enable/disable look-ahead.
- `CameraFocusTemporary` — snap/pan to a target for a duration, then restore the previous state.
- `CameraStopTemporaryFocus` — cancel an active temporary focus early.
- `CameraReset` — reset the camera to its configured defaults.

### Transformers
- `Transformer` — abstract base: runs on a chosen Unity update loop (`Update`/`FixedUpdate`/`LateUpdate`) while active.
- `Follower` — smoothed or snapped position/rotation following, with per-axis locking.
- `ScaleOverTime` — animates scale over time; implements `ITransformerAction` directly.

### Triggers
- `VolumeTriggerBase` (abstract) / `VolumeTrigger2D` / `VolumeTrigger3D` — enter/exit detection with layer filtering and optional threshold events (fire once N tracked objects are in/out).
- `ImpactTrigger2D` / `ImpactTrigger3D` — collision-based impact detection, builds `ImpactData`, notifies an `IImpactReceiver` on the hit object, then fires an `onImpact` event.
- `RayTriggerBase` (abstract, extends `Transformer`) / `RayTrigger2D` / `RayTrigger3D` — continuous raycast with `onHit`/`onHitEnter`/`onHitExit`/`onNoHit` events.
- `WindTrigger3D` (extends `VolumeTrigger3D`; was `UpdraftTrigger3D` before 0.6.0) — a wind zone blowing in any direction: an updraft vent to glide on, a fan blowing across a level, a gust through a gap. `Direction` is relative to the object's rotation by default (`Direction Space = Self`, so a fan is aimed by rotating it) or fixed in world space. Every `FixedUpdate` while active, it notifies any `IWindReceiver` on tracked objects with the wind's direction, velocity, and onset (`WindData`) for as long as they remain inside. Onset is per-instance — `Smooth` (ramps in at a configurable acceleration) or `Instant` (snaps straight to the target velocity), so one vent can be a gentle sustained thermal and another a sudden gust. Optional distance falloff (`Use Falloff`) scales that velocity by a curve evaluated over distance-along-`Direction` from an `Origin` point out to `Max Distance` — e.g. full push near a vent's base, easing to zero by the top of its collider, so a receiver settles into a float instead of launching indefinitely. Foundry doesn't decide what a receiver does with any of this — it's just dispatch, same shape as `ImpactTrigger3D`/`IImpactReceiver`. It also blows **other objects**: any non-kinematic Rigidbody inside that isn't a receiver is pushed along `Direction` like air drag, divided by its mass — `Strength` (kg) is the heaviest mass it could hold up against gravity, so light things (eggs, shell pieces) are carried along — up a vent or across a fan — while a heavy rock doesn't budge; the push is capped so nothing goes faster than the wind. Particles are blown too — the wind is an `IParticleAffector` (see Particles below), pushing the particles of any `ParticlePhysics` system while they're inside its trigger collider (`Particle Catch Up` sets how quickly they catch the wind). The zone is its collider, so use a Box, Sphere, Capsule or convex Mesh collider. **Fit Collider To Wind** (on for newly added zones) sizes and places a Box Collider for you:
    - It starts at the object's pivot (the fan's mouth or the vent's base) and runs `Length` along `Direction`, `Width x Depth` across it.
    - Sizes are metres whatever the object's scale, and it stays in sync as you edit.
    - `Falloff Matches Length` ends the falloff exactly at the far end.
  - **Blocking** (0.7.0): with a `Blocked By` layer mask (and a `BoxCollider` zone), the wind stops at the first thing in its path — the zone shortens to end there, so nothing beyond is blown, and grows back when it moves away. Anything without a `WindReflector` just absorbs the wind. `Stall When Blocked` (off by default) makes something right up against the source (within `Stall Distance`) stall it completely; off, a reflector there gets the full-strength wind. Events: Obstructed/Cleared, Stalled/Resumed.
- `WindReflector` — put on an object to redirect wind that hits it: a new wind zone blows out of the hit point in `Outgoing Direction` (any direction, relative to the object or world), at the incoming wind's speed where it hit × how much of the beam the object covers × `Strength %` (100% default). E.g. a horizontal fan into an angled rock becomes an updraft at the rock — stronger the closer the rock is to the fan (with falloff on the fan). The redirected zone is an ordinary `WindTrigger3D` (glidable, blows eggs/particles; the fan's `WindVisual` particles visibly bend round into it), and can hit another reflector (up to 3 redirections). Reach is either **Fixed** (`Max Reach`, shorter the further from the source, with its own falloff) or **Match Source** (an exact reflection: it carries on as far as the incoming wind would have gone, fading with the source's falloff); in either mode `Reflection Multiplier` stretches its reach and fade for a bigger effect in a short space.
- `WindVisual` (was `UpdraftVisual`) — makes a wind zone visible: drives a particle system so debris streams from the zone's upwind end along its direction at the wind's speed, filling the zone and switching off with the wind. Sets only placement/size/speed/lifetime/rate (on Awake, **Fit To Zone**, and automatically whenever the zone's shape or speed changes), never the look; while blocked, particles reaching a plain blocker end there rather than sailing through it, and particles reaching a `WindReflector` are handed on round the bend into the redirected wind (which has no emitter of its own). **Create Particles** makes a starting blown-debris system to restyle (e.g. with a leaf sprite).

### Particles
- `ParticlePhysics` — add to a particle system to let the environment move its particles. Each frame it runs its particles through every active `IParticleAffector` they're inside (one particle read/write per frame however many affectors). `Sensitivity` (1 = normal, higher = lighter, 0 = unaffected) scales every affector's effect.
- `IParticleAffector` / `ParticleAffectors` — the interface anything that moves particles implements (`Affect(position, velocity, sensitivity, dt)` → new velocity), and the registry affectors join while enabled. `WindTrigger3D` is one; a water current, vortex or explosion could be others without touching `ParticlePhysics`.

### Damage
- `DamageData` / `DamageType` — a damage payload: amount, type (`Generic`, `Bullet`, `Explosion`, `Impact`, `Fire`, `Strike`), source/instigator/target, hit point/direction/normal, and force.
- `IDamageReceiver` — `ApplyDamage(DamageData)`; implement it on anything that can be hurt. Same dispatch shape as `IImpactReceiver` — Foundry delivers the damage, the receiver decides what it means.
- `Life` — a generic health component implementing `IDamageReceiver`: life points with a max, `Damage`/`Heal`/`Die`/`ResetLife`, `hurtEvent`/`healEvent`/`deathEvent`, and optional **life stages** that fire their own event when life drops to a threshold (e.g. a boss changing phase at half health). It's also an `IStrikeReceiver` (0.8.0): strikes, such as the chicken's peck, damage it by their `Damage`. It has a `damagedByStrikes` toggle and an optional `strikeKind` filter. It's also an `IImpactReceiver`: with **Damaged By Impacts**, things thrown into it, such as eggs (`Impact Layers`), damage it if they hit at `Min Impact Speed` or faster, for `Impact Damage` plus an optional `Damage Per Extra Speed`. So each breakable can need pecks, eggs, or either. A window can crack at a life stage and then break on death, or a beam can give way after a few pecks or eggs.
- `ExplosionDamage` — `Explode()` damages every `IDamageReceiver` within a radius (layer-filtered), with optional linear distance falloff. Known quirk: it applies once per *collider* in range, so a receiver with several colliders is hit several times.
- `RaycastDamage` — `Fire()` casts a ray forward from an origin; if the first thing it hits is (or is parented under) an `IDamageReceiver`, it's damaged. Optionally applies a physics force to the hit Rigidbody.

### Interaction (0.8.0)
- `StrikeData` / `IStrikeReceiver` / `Strike` — a deliberate hit on something, such as a peck or a hammer blow.
  - Whoever strikes finds the collider and fills in a `StrikeData`: instigator, source, `Kind` (e.g. "Peck"), point, direction, normal, `Damage`, `Impulse` and `LaunchDirection`.
  - `Strike.Dispatch` knocks a loose (non-kinematic) Rigidbody along the strike by the impulse. Lighter objects fly further, and off-centre hits make them spin.
  - It then calls every enabled `IStrikeReceiver` on the nearest object that has any: the collider's own object, otherwise up its parents.
  - Each receiver decides what a strike means, and each has an optional `strikeKind` filter.
- `Interactable` — switches, buttons, levers and control panels.
  - Modes: **Press** (On Interact on every use), **Toggle** (On Turned On / On Turned Off) or **Once**.
  - It has a cooldown. It can be used by a strike (a peck), by something thrown into it (an egg, via its `ImpactTrigger3D`; `Impact Layers` and `Min Impact Speed`), or by `Interact()` / `InteractFrom(direction)` from anything else.
  - **Use On Trigger:** something entering a trigger collider on the same object uses it: the chicken stepping onto it, a pushed rock or a rolling egg (`Trigger Layers`). For a Toggle, **On While Occupied** makes it a pressure plate: on while anything's inside, and off once it's been empty for `Release Delay`, including when something inside is despawned. The zone is sensed every physics step. Trigger colliders count only if their layer is in Trigger Layers, so the chicken's **Player Feet** sensor presses a thin plate.
  - **Hit Direction** (optional) sets what a hit from each side does (from left / right / above / below): the mode's action, Turn On, Turn Off (Toggle only) or Ignore. It can apply to strikes, impacts or both, using the object's own sides or the world's. E.g. an egg from the left switches it on and one from the right switches it off.
- `Consumable` — something that can be eaten. Each strike takes a bite. After `Bites To Eat` it's consumed: On Consumed fires (e.g. to grant a power-up), and it's despawned, or destroyed if it wasn't pooled.

`ImpactTrigger3D` (0.8.0) now tells **every** enabled `IImpactReceiver` on the nearest object with any, starting from the collider it hit and working up its parents (it used to tell only the first one, on the hit object itself).

### Ropes (0.8.0)
- `Rope` — a physics rope, cable or cord: a chain of jointed segments (`ConfigurableJoint`s, planar by default) drawn as a line.
  - It hangs from its own object to one of: an `End Body` (which swings, and falls when the rope is cut), a fixed `End Anchor` (a cable across a gap), or nothing (a loose end).
  - Striking a segment (`RopeSegment`), or an egg (anything thrown, `Impact Layers`) flying through it at `Min Impact Speed` or faster (`Cut By Impact`), cuts the rope there after `Hits To Cut`.
  - Also: `Max Cuts`, On Fray / On Cut events, and `CutAt()` / `DetachEnd()`.
  - A strike only swings it (`Strike Swing`, m/s). It's tuned to stay stable under heavy loads, and ties to the top of the End Body (`Attach To Surface`).
- `IStrikeLaunchModifier` — put on a struck collider's object to change or cancel the knock a strike gives it (rope segments use it to swing rather than launch).

### Digging (0.8.0)
- `DiggableTerrain` — dirt that can be dug through freely.
  - It's a density grid in local X/Y, meshed with marching squares and extruded along Z, with a matching `MeshCollider`.
  - The mesh is split into chunks, so each bite only rebuilds the chunks it touched.
  - A strike bites a soft round hole (`Dig Radius`; `Hardness` is the number of strikes per full bite), so tunnels form wherever the player digs. `Dig Into` pushes each bite further along the strike. `Dig Across` moves it across the strike: up or down for a forward peck, and the way the chicken faces for an up or down peck.
  - Also: `Pre Dug` holes, a pooled `Dig Effect`, the On Dig event, and `Dig()` / `DensityAt()` / `IsSolidAt()`.
  - **Mode:** **Through**, the original behaviour, where each bite cuts straight through the block. Or **Rounded**, a 3D grid where each bite scoops a sphere, so tunnels are rounded hollows inside the dirt.
  - For Rounded mode, give the dirt a material using the **`Foundry/Dirt (See-Through)`** shader, and put a **`SeeThroughWindow`** on the player. A soft window then opens in the dirt in front of the chicken, showing a slice through the dirt at its plane: solid where undug, tunnels where dug.
- `SeeThroughWindow` — opens that window around the object it's on (`Radius`, `Softness`, `Offset`).
- `BuriedObject` — something buried in the dirt. Its Rigidbody is held still until enough of the dirt around it is dug away (`Reveal Amount`); then it's freed and On Revealed fires.

### Camera System
- `CameraCore` — stable API (`SetTarget`, `SetOffset`, `SetZoom`, `SetLookAhead`, `ResetCamera`, `FocusTemporary`) that the Camera Actions above call into.
- `CameraRig2D` — the actual per-frame follow/zoom/look-ahead behaviour.

### Attachments
- `AttachmentBinding` / `AttachmentGroup` — socket-style parenting with position/rotation offsets and an editor-time position preview, driven by the `Attach`/`Detach` Actions.

### Planned / Expanding
- additional actions, transformers, and triggers as the game layer's needs surface them
- common gameplay utilities

See [Assets/ARCHITECTURE.md](../ARCHITECTURE.md) for how these pieces connect to the event pipeline and to the Game layer.

---

## Design Goals

Foundry is being built around a few key goals:

- **Modular**  
  Systems should be easy to combine without becoming tightly coupled.

- **Reusable**  
  Behaviours should be useful across multiple scenes and projects.

- **Inspector-Driven**  
  Common gameplay setup should be possible directly in the Unity editor.

- **Extendable**  
  New actions, transformers, and triggers should be easy to add.

- **Separated Properly**  
  Core framework code stays in FrameCoreU, reusable gameplay behaviour lives in Foundry, and true game-specific logic stays in the game project layer.

---

## Relationship to FrameCoreU

Foundry depends on FrameCoreU.

### Compatibility

Foundry and FrameCoreU are versioned independently (each repo's current branch name is its version), so their version numbers won't generally match — that's expected, not a mistake. What matters is which pair is known to work together:

| Foundry | FrameCoreU |
|---|---|
| `0.8.0` | `0.3.0` |
| `0.7.0` | `0.3.0` |
| `0.6.0` | `0.3.0` |
| `0.5.0` | `0.3.0` (minimum — the `Despawn` action needs its pool despawn) |
| `0.4.0` | `0.2.0` |
| `0.3.0` | `0.2.0` |

When bumping either repo to a new branch/version, update this table if the pairing changes, so a project vendoring both always knows which combination was actually tested together.

FrameCoreU provides the underlying architecture and shared systems.

Foundry uses those systems to create practical gameplay behaviour.

In simple terms:

- FrameCoreU provides the machinery
- Foundry provides the reusable behavioural parts
- the game project provides the final implementation

---

## Suggested Structure

```text
Foundry/
├── Actions/
│   ├── Spawn.cs
│   ├── Sound.cs
│
├── Transformers/
│   ├── Transformer.cs
│   ├── Follower.cs
│
├── Triggers/
│   ├── VolumeTrigger.cs
│   ├── ImpactTrigger.cs
│
├── Utilities/
│   └── ...
```

---

## Usage

Typical usage is:

- Set up FrameCoreU in the scene
- Create events through the FrameCore event system
- Attach Foundry Actions to those events
- Use Triggers or code to activate events
- Add Transformers where continuous behaviour is needed
- Future Direction

---

## Foundry is intended to grow into a reusable gameplay toolkit that supports:

- rapid prototyping
- clean gameplay composition
- inspector-based behaviour authoring
- scalable event-driven systems
- clearer separation between framework, gameplay layer, and game-specific code

---

# Summary

Foundry is the gameplay construction layer built on top of FrameCoreU.

It turns framework systems into reusable gameplay behaviour through:

- Actions
- Transformers
- Triggers

FrameCoreU provides the systems.
Foundry provides the behaviour.
The game layer provides the final experience.