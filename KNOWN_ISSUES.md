# Known Issues

This document provides detailed information about known bugs and limitations in the Souls-like Combat System.

---

## Critical Issues

### 1. Dual State Tracking Desync

**Severity:** Critical
**Status:** Open
**Affected Files:** `PlayerCombat.cs`, `CombatStateMachine.cs`

**Description:**
The combat system maintains state in two places: `PlayerCombat._currentState` and `CombatStateMachine.CurrentStateType`. These can become desynchronized, causing unpredictable behavior.

**Reproduction Steps:**
1. Start attacking
2. Get hit during attack animation
3. Immediately try to block
4. State may be incorrect

**Expected Behavior:**
Single source of truth for combat state.

**Current Workaround:**
Always use `CombatStateMachine.CurrentStateType` for state checks.

**Potential Fix:**
Remove `PlayerCombat._currentState` and delegate all state tracking to `CombatStateMachine`.

---

### 2. Parry Timing Mismatch

**Severity:** Critical
**Status:** Open
**Affected Files:** `PlayerCombat.cs`, `DefenseSystem.cs`, `CombatSettings.cs`

**Description:**
`PlayerCombat.cs` uses a hardcoded 300ms parry window, while `DefenseSystem.cs` and other files use 180ms. This causes inconsistent parry behavior.

**Reproduction Steps:**
1. Time a parry at ~200ms after block start
2. Observe inconsistent success/failure

**Expected Behavior:**
Consistent parry window across all systems.

**Current Workaround:**
Manually synchronize the values by editing `PlayerCombat.cs`.

**Potential Fix:**
Use `CombatSettings` ScriptableObject as single source for all timing values.

**Related Code:**
```csharp
// PlayerCombat.cs (line ~145)
private const float ParryWindow = 0.3f; // 300ms - WRONG

// DefenseSystem.cs
private float _parryWindow = 0.18f; // 180ms - CORRECT
```

---

### 3. Missing AnimatorHash Constants

**Severity:** Critical
**Status:** Open
**Affected Files:** `AnimatorHash.cs`, `HitReactionSystem.cs`, `DeadState.cs`

**Description:**
`IsStunned` and `Destroy` animator parameters are used but not defined in `AnimatorHash.cs`, causing string-based lookups and potential errors.

**Reproduction Steps:**
1. Trigger a stun state
2. Check console for missing parameter warnings

**Potential Fix:**
Add missing constants to `AnimatorHash.cs`:
```csharp
public static readonly int IsStunned = Animator.StringToHash("IsStunned");
public static readonly int Destroy = Animator.StringToHash("Destroy");
```

---

## High Priority Issues

### 4. Network State Desync

**Severity:** High
**Status:** Open
**Affected Files:** `NetworkCombatState.cs`, `CombatStateMachine.cs`

**Description:**
In multiplayer, client-side combat state can temporarily show incorrect values before server reconciliation corrects it.

**Reproduction Steps:**
1. Play in multiplayer with 100ms+ latency
2. Attack while another player attacks
3. Observe brief visual glitches

**Current Workaround:**
Server reconciliation exists but has noticeable delay.

**Potential Fix:**
Implement client-side prediction with rollback.

---

### 5. Dodge i-Frame Timing Unreliable

**Severity:** High
**Status:** Open
**Affected Files:** `DodgingState.cs`, `PlayerCombat.cs`

**Description:**
With high network latency (150ms+), i-frames may not register correctly, causing damage during what should be invincibility.

**Reproduction Steps:**
1. Play with artificial 200ms latency
2. Dodge through an attack
3. May still take damage

**Current Workaround:**
Works correctly in singleplayer.

**Potential Fix:**
Server-authoritative i-frame validation with latency compensation.

---

### 6. Stamina Race Condition

**Severity:** High
**Status:** Open
**Affected Files:** `PlayerStaminaSystem.cs`, `NetworkCombatController.cs`

**Description:**
In multiplayer, rapid actions can cause stamina to go negative due to client-server timing differences.

**Reproduction Steps:**
1. Spam attacks rapidly in multiplayer
2. Observe stamina going below 0

**Current Workaround:**
None - causes ability to attack without stamina.

**Potential Fix:**
Add server-side stamina validation before allowing actions.

---

## Medium Priority Issues

### 7. String Literals in NetworkCombatState

**Severity:** Medium
**Status:** Open
**Affected Files:** `NetworkCombatState.cs`

**Description:**
Animator parameters use string lookups instead of cached hashes, causing unnecessary GC allocations.

**Impact:**
Performance degradation, especially in multiplayer with many players.

**Potential Fix:**
Replace all `animator.SetBool("StateName")` with `animator.SetBool(AnimatorHash.StateName)`.

---

### 8. Duplicate Code Patterns

**Severity:** Medium
**Status:** Open
**Affected Files:** Multiple (7+ files)

**Description:**
`GetLayerIndex()` and `LoadCombatSettings()` patterns are duplicated across many files instead of being centralized.

**Impact:**
Maintenance burden, potential for inconsistencies.

**Potential Fix:**
Create utility class or base class with these common operations.

---

### 9. Legacy Animator Code

**Severity:** Medium
**Status:** Open
**Affected Files:** `NetworkCombatState.cs`

**Description:**
`OnCombatStateChanged()` contains animator updates that may conflict with state machine's animator control.

**Impact:**
Potential animation glitches.

**Potential Fix:**
Remove redundant animator updates, let state machine handle all animation.

---

### 10. Unused AnimatorHash Parameters

**Severity:** Medium
**Status:** Open
**Affected Files:** `AnimatorHash.cs`

**Description:**
18 defined animator hash constants are never used in the codebase.

**Impact:**
Code clutter, confusion.

**Potential Fix:**
Audit and remove unused constants.

---

## Low Priority Issues

### 11. Magic Numbers

**Severity:** Low
**Status:** Open
**Affected Files:** Multiple

**Description:**
70+ hardcoded values throughout the codebase instead of using ScriptableObject configuration.

**Examples:**
```csharp
yield return new WaitForSeconds(0.5f); // What is 0.5?
if (distance < 2.0f) // What is this distance?
```

**Potential Fix:**
Move to `CombatSettings` ScriptableObject.

---

### 12. God Class - CameraController

**Severity:** Low
**Status:** Open
**Affected Files:** `CameraController.cs`

**Description:**
`CameraController.cs` is 1,391 lines and handles too many responsibilities.

**Potential Fix:**
Split into:
- `CameraMovement.cs`
- `CameraLockOn.cs`
- `CameraCollision.cs`
- `CameraSettings.cs`

---

### 13. Missing Unit Tests

**Severity:** Low
**Status:** Open
**Affected Files:** None (tests don't exist)

**Description:**
No unit tests for state transitions or combat logic.

**Impact:**
Regressions may go unnoticed.

**Potential Fix:**
Add test suite for:
- State transitions
- Damage calculations
- Stamina consumption
- Network synchronization

---

## Reporting New Issues

If you find a bug not listed here:

1. Check existing [GitHub Issues](https://github.com/TeaYololo/soulslike/issues)
2. Create a new issue with:
   - Clear title
   - Reproduction steps
   - Expected vs actual behavior
   - Unity version
   - FishNet version (if multiplayer)
   - Screenshots/videos if applicable
