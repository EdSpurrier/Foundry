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

Foundry currently includes early reusable gameplay building blocks such as:

### Actions
- `Spawn`
- `Sound`

### Transformers
- `Transformer`
- `Follower`

### Planned / Expanding
- trigger framework
- additional actions
- additional transformers
- common gameplay utilities

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