# Souls-like Combat System for Unity

![Unity Version](https://img.shields.io/badge/Unity-2022.3%2B-blue)
![License](https://img.shields.io/badge/License-Apache%202.0-green)
![Status](https://img.shields.io/badge/Status-Work%20In%20Progress-yellow)

A **free, open-source** souls-like combat system for Unity. Features state machine architecture, parry/block mechanics, stamina management, and FishNet multiplayer support.

> **Note:** This is a work-in-progress project with known bugs. See [Known Issues](#known-issues) before using.

---

## Features

### Combat Mechanics
- **State Machine Pattern** - 8 combat states (Idle, Attacking, Blocking, Dodging, Staggered, Recovering, UsingSkill, Dead)
- **Combo System** - Light and heavy attack chains
- **Parry/Block** - Timed parry with configurable window, stamina-based blocking
- **Dodge Roll** - i-Frame based dodge with configurable invincibility
- **Poise System** - Stagger on poise break
- **Stamina Management** - All actions consume stamina

### Technical
- **Lock-On System** - Target locking with camera integration
- **Network Ready** - FishNet multiplayer support (server-authoritative)
- **Configurable** - ScriptableObject based settings
- **Event-Driven** - Loosely coupled architecture

---

## Requirements

- Unity **2022.3 LTS** or newer
- **FishNet** networking (for multiplayer) - [Get FishNet](https://assetstore.unity.com/packages/tools/network/fish-net-networking-evolved-207815)
- Your own **animation set** (not included due to licensing)

### Recommended Animation Assets
- [Souls-like Essential Animations](https://assetstore.unity.com/packages/3d/animations/souls-like-essential-animations-178889)
- [Kubold Animations](https://assetstore.unity.com/publishers/27987)
- Or any Humanoid-compatible animation set

---

## Quick Start

### 1. Clone the Repository
```bash
git clone https://github.com/TeaYololo/soulslike.git
```

### 2. Open in Unity
- Open Unity Hub
- Add project from disk
- Select the cloned folder

### 3. Import Dependencies
- Install FishNet from Asset Store (for multiplayer)
- Import your animation set

### 4. Setup Animator
See [Documentation/SETUP_GUIDE.md](Assets/_Project/Documentation/SETUP_GUIDE.md) for animator parameter setup.

### 5. Add to Character
```csharp
// Required components on player:
// - CombatStateMachine
// - PlayerCombat
// - PlayerStaminaSystem
// - Animator (with proper controller)
```

---

## Architecture

```
+-------------------------------------------------------------+
|                     State Machine                           |
+-------------------------------------------------------------+
|  CombatStateMachine.cs        - State manager               |
|  CombatStateFactory.cs        - Factory pattern             |
|  ICombatState.cs              - Interface                   |
|  CombatStateBase.cs           - Abstract base               |
|                                                             |
|  States/                                                    |
|    +-- IdleState.cs           - Default state               |
|    +-- AttackingState.cs      - Attack execution            |
|    +-- BlockingState.cs       - Block/parry handling        |
|    +-- DodgingState.cs        - Dodge with i-frames         |
|    +-- StaggeredState.cs      - Poise break recovery        |
|    +-- RecoveringState.cs     - Post-action recovery        |
|    +-- DeadState.cs           - Death handling              |
|    +-- UsingSkillState.cs     - Skill execution             |
+-------------------------------------------------------------+
```

### State Flow
```
              +----------+
              |   IDLE   |<----------------------------+
              +----+-----+                             |
                   |                                   |
       +-----------+-----------+-----------+          |
       v           v           v           v          |
  +---------+ +---------+ +---------+ +---------+     |
  |ATTACKING| |BLOCKING | | DODGING | |  SKILL  |     |
  +----+----+ +----+----+ +----+----+ +----+----+     |
       |           |           |           |          |
       +-----------+-----------+-----------+          |
                   |                                   |
                   v                                   |
             +----------+                              |
             |STAGGERED |                              |
             +----+-----+                              |
                  |                                    |
                  v                                    |
            +----------+                               |
            |RECOVERING|-------------------------------+
            +----------+
```

---

## Known Issues

> **This system is under active development. The following known issues exist:**

### Critical Issues

| Issue | Description | Workaround |
|-------|-------------|------------|
| **Dual State Tracking** | `PlayerCombat._currentState` and `CombatStateMachine.CurrentStateType` may desync | Use `CombatStateMachine.CurrentStateType` |
| **Parry Timing Mismatch** | `PlayerCombat.cs` uses 300ms, other files use 180ms parry window | Use `CombatSettings` for single value |
| **Missing AnimatorHash** | `IsStunned` and `Destroy` parameters not defined in AnimatorHash.cs | Add manually |

### High Priority Issues

| Issue | Description | Status |
|-------|-------------|--------|
| **Network State Desync** | Client-side state may temporarily show incorrect value | Server reconciliation exists but not perfect |
| **Dodge i-Frame Timing** | i-frames unreliable with high network latency | Works fine in singleplayer |
| **Stamina Race Condition** | Stamina can go negative in multiplayer | Missing server-side validation |

### Medium Priority Issues

| Issue | Description |
|-------|-------------|
| **String Literals in NetworkCombatState** | Animator parameters use strings instead of hashes (performance) |
| **Duplicate Code** | `GetLayerIndex()` and `LoadCombatSettings()` patterns repeated in 7+ files |
| **Legacy Animator Code** | `NetworkCombatState.OnCombatStateChanged()` has unnecessary animator updates |
| **Unused AnimatorHash Parameters** | 18 defined parameters are never used |

### Low Priority / Polish

| Issue | Description |
|-------|-------------|
| **Magic Numbers** | 70+ hardcoded values (should move to ScriptableObject) |
| **God Class** | `CameraController.cs` is 1,391 lines - should be split |
| **Missing Unit Tests** | No state transition tests |

---

## Project Structure

```
Assets/_Project/
+-- Scripts/
|   +-- Combat/
|   |   +-- Core/
|   |   |   +-- PlayerCombat.cs          # Main combat controller
|   |   |   +-- AttackExecutor.cs        # Attack execution
|   |   |   +-- DefenseSystem.cs         # Block/parry
|   |   |   +-- HitReactionSystem.cs     # Hit reactions
|   |   |
|   |   +-- StateMachine/
|   |   |   +-- CombatStateMachine.cs    # State manager
|   |   |   +-- CombatStateFactory.cs    # Factory
|   |   |   +-- ICombatState.cs          # Interface
|   |   |   +-- CombatStateBase.cs       # Abstract base
|   |   |   +-- States/                  # All state implementations
|   |   |
|   |   +-- Network/
|   |       +-- NetworkCombatState.cs    # FishNet state sync
|   |       +-- NetworkCombatController.cs
|   |
|   +-- Player/
|       +-- Combat/
|       |   +-- PlayerCombat.cs
|       |   +-- PlayerComboSystem.cs
|       +-- Core/
|           +-- PlayerStaminaSystem.cs
|
+-- Data/
|   +-- ScriptableObjects/
|       +-- CombatSettings.asset         # Centralized config
|
+-- Prefabs/
    +-- (example prefabs)
```

---

## Configuration

### CombatSettings ScriptableObject

Create via: `Right Click > Create > Dungeons > Combat Settings`

| Parameter | Default | Description |
|-----------|---------|-------------|
| Parry Window | 0.18s | Perfect parry timing window |
| Dodge Duration | 0.6s | Total dodge animation time |
| i-Frame Start | 0.05s | When invincibility begins |
| i-Frame Duration | 0.35s | Invincibility length |
| Light Attack Stamina | 15 | Stamina cost per light attack |
| Heavy Attack Stamina | 25 | Stamina cost per heavy attack |
| Dodge Stamina | 20 | Stamina cost per dodge |

---

## Contributing

Contributions are welcome! This project has known issues that need fixing.

### How to Contribute

1. Fork the repository
2. Create a feature branch (`git checkout -b fix/parry-timing`)
3. Commit your changes (`git commit -m 'Fix parry timing mismatch'`)
4. Push to the branch (`git push origin fix/parry-timing`)
5. Open a Pull Request

### Priority Contributions Needed

- [ ] Fix dual state tracking issue
- [ ] Unify parry window timing
- [ ] Add missing AnimatorHash constants
- [ ] Network stamina validation
- [ ] Unit tests for state transitions

See [CONTRIBUTING.md](CONTRIBUTING.md) for detailed guidelines.

---

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

---

## Acknowledgments

- Inspired by FromSoftware's Dark Souls series
- State machine pattern based on Unity best practices
- Network architecture influenced by FishNet examples

---

## Contact

- **GitHub Issues:** For bugs and feature requests
- **Discussions:** For questions and community chat

---

**If you find this useful, please consider giving it a star!**
