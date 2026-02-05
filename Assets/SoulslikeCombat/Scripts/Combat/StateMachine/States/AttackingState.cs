using UnityEngine;
using Dungeons.Data;
using Dungeons.Core.Utilities;
using Dungeons.Combat;

namespace Dungeons.Combat.StateMachine.States
{
    /// <summary>
    /// Attacking state - saldırı sırasında.
    /// Recovery phase'de dodge/block/combo yapılabilir.
    /// Hyper armor varken stagger olmaz.
    /// </summary>
    public class AttackingState : CombatStateBase
    {
        public override CombatState StateType => CombatState.Attacking;

        // Attack timing
        private float _attackDuration;
        private bool _inRecoveryPhase;
        private bool _attackComplete;

        // Recovery phase başlangıç yüzdesi (AttackExecutor'dan alınır)
        private const float DEFAULT_RECOVERY_START = 0.7f;

        public override bool IsComplete => _attackComplete;

        /// <summary>
        /// Recovery phase'de mi?
        /// </summary>
        public bool InRecoveryPhase => _inRecoveryPhase;

        public override void Enter(CombatStateMachine context)
        {
            base.Enter(context);

            _attackComplete = false;
            _inRecoveryPhase = false;

            // AttackExecutor'dan attack duration'ı al
            if (Context.AttackExecutor != null)
            {
                _attackDuration = Context.AttackExecutor.CurrentAttackDuration;
                if (_attackDuration <= 0)
                {
                    _attackDuration = 1.0f; // Fallback
                }
            }
            else
            {
                _attackDuration = 1.0f;
            }
        }

        public override void Update()
        {
            float elapsed = StateTime;
            float progress = _attackDuration > 0 ? elapsed / _attackDuration : 1f;

            // Recovery phase check
            float recoveryStart = Context.ComboSystem?.RecoveryPhaseStart ?? DEFAULT_RECOVERY_START;
            if (progress >= recoveryStart && !_inRecoveryPhase)
            {
                _inRecoveryPhase = true;
            }

            // Fallback: Attack tamamlandı (animation event gelmezse)
            if (progress >= 1f && !_attackComplete)
            {
                OnAttackComplete();
            }

            // Safety net: 2 saniyeden uzun süren attack'ı zorla bitir
            if (StateTime > 2f)
            {
                OnAttackComplete();
            }
        }

        public override void Exit()
        {
            base.Exit();
            _attackComplete = false;
            _inRecoveryPhase = false;
        }

        public override bool CanTransitionTo(CombatState newState)
        {
            switch (newState)
            {
                case CombatState.Idle:
                    // Attack tamamlandığında Idle'a geçebilir
                    return _attackComplete || _inRecoveryPhase;

                case CombatState.Attacking:
                    // Combo: Recovery phase'de ve hit confirm varsa
                    if (_inRecoveryPhase)
                    {
                        bool hitLanded = Context.ComboSystem?.HitLandedThisAttack ?? false;
                        bool allowHitConfirm = Context.ComboSystem?.AllowHitConfirmCombo ?? true;
                        return allowHitConfirm && hitLanded;
                    }
                    return false;

                case CombatState.Dodging:
                    // Recovery phase'de dodge yapılabilir
                    return _inRecoveryPhase;

                case CombatState.Blocking:
                    // Recovery phase'de veya instant block aktifse
                    return _inRecoveryPhase;

                case CombatState.Staggered:
                    // Hyper armor varken stagger olmaz
                    if (Context.AttackExecutor != null && Context.AttackExecutor.IsHyperArmorActive)
                    {
                        return false;
                    }
                    return true;

                case CombatState.Dead:
                    // Her zaman ölüme geçebilir
                    return true;

                default:
                    return false;
            }
        }

        public override void OnAnimationEvent(string eventName)
        {
            switch (eventName)
            {
                case "AttackEnd":
                case "OnAttackEnd":
                    OnAttackComplete();
                    break;

                case "HitboxEnable":
                case "OnHitboxEnable":
                    Context.AttackExecutor?.EnableHitbox();
                    break;

                case "HitboxDisable":
                case "OnHitboxDisable":
                    Context.AttackExecutor?.DisableHitbox();
                    break;

                case "RecoveryStart":
                    _inRecoveryPhase = true;
                    break;
            }
        }

        public override void OnHit(DamageInfo damage)
        {
            // Hyper armor kontrolü
            bool hasHyperArmor = Context.AttackExecutor?.IsHyperArmorActive ?? false;

            if (hasHyperArmor)
            {
                // Hyper armor: Hasar alınır ama stagger olmaz
                // Hasar Combat.TakeDamage'da işlenir
                return;
            }

            // Hyper armor yoksa stagger'a geçmeyi dene
            if (damage.CausesFlinch && CanTransitionTo(CombatState.Staggered))
            {
                Context.TryChangeState(CombatState.Staggered);
            }
        }

        private void OnAttackComplete()
        {
            _attackComplete = true;
            Context.TryChangeState(CombatState.Idle);
        }
    }
}
