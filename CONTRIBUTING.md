# Contributing to Souls-like Combat System

Thank you for your interest in contributing to this project!

## Getting Started

1. **Fork the repository**
2. **Clone your fork**
   ```bash
   git clone https://github.com/YOUR_USERNAME/soulslike.git
   ```
3. **Create a branch** for your changes
   ```bash
   git checkout -b fix/your-feature-name
   ```

## Code Style

Please follow these conventions:

### Naming
- **PascalCase** for public members, classes, methods
- **_camelCase** for private fields (underscore prefix)
- **camelCase** for local variables and parameters

### Structure
- Add XML documentation for public methods
- Keep methods under 50 lines when possible
- Use `#region` for organization in large files

### Example
```csharp
/// <summary>
/// Executes a light attack.
/// </summary>
/// <param name="comboIndex">Current combo index (1-3)</param>
/// <returns>True if attack was executed</returns>
public bool ExecuteLightAttack(int comboIndex)
{
    if (!CanAttack()) return false;

    _currentCombo = comboIndex;
    // ... implementation
    return true;
}
```

## Pull Request Process

1. **Test your changes** in both singleplayer and multiplayer (if applicable)
2. **Update documentation** if needed
   - Update `README.md` for new features
   - Update `KNOWN_ISSUES.md` if you fix a bug
3. **Write a clear PR description** explaining what and why
4. **Request review** from maintainers

## Priority Areas

We especially need help with:

### Critical Bug Fixes
- [ ] Fix dual state tracking issue (`PlayerCombat._currentState` vs `CombatStateMachine.CurrentStateType`)
- [ ] Unify parry window timing (300ms vs 180ms inconsistency)
- [ ] Add missing `IsStunned` and `Destroy` AnimatorHash constants

### High Priority
- [ ] Network stamina validation (prevent negative stamina)
- [ ] Improve dodge i-frame reliability with network latency
- [ ] Server reconciliation improvements

### Medium Priority
- [ ] Replace string literals with AnimatorHash in NetworkCombatState
- [ ] Refactor duplicate `GetLayerIndex()` and `LoadCombatSettings()` patterns
- [ ] Clean up unused AnimatorHash parameters

### Testing
- [ ] Unit tests for state transitions
- [ ] Integration tests for combat flow
- [ ] Network sync tests

## Commit Messages

Use clear, descriptive commit messages:

```
fix: resolve parry timing mismatch between PlayerCombat and DefenseSystem

- Changed PlayerCombat.cs parry window from 300ms to 180ms
- Added reference to CombatSettings for centralized timing
- Updated documentation
```

Prefixes:
- `fix:` for bug fixes
- `feat:` for new features
- `refactor:` for code improvements
- `docs:` for documentation
- `test:` for tests

## Questions?

- Open an **Issue** for bug reports or feature requests
- Use **Discussions** for questions and community chat

Thank you for contributing!
