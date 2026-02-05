using UnityEngine;
using Dungeons.Data;
using Dungeons.Data.Interfaces;
using Dungeons.Core.Utilities;
using Dungeons.Combat;

namespace Dungeons.Combat.StateMachine.States
{
    /// <summary>
    /// Blocking state - blok tutuluyor.
    /// Perfect parry window kontrolü yapar.
    /// </summary>
    public class BlockingState : CombatStateBase
    {
        public override CombatState StateType => CombatState.Blocking;

        private float _perfectParryWindow;
        private bool _isParryWindowActive;

        /// <summary>
        /// Perfect parry window'unda mı?
        /// </summary>
        public bool IsInParryWindow => _isParryWindowActive;

        public override void Enter(CombatStateMachine context)
        {
            base.Enter(context);

            // CombatSettings'den parry window'u al
            _perfectParryWindow = CombatSettings.Instance?.GetParryWindow() ?? 0.18f;
            _isParryWindowActive = true;

            // Blocking animation
            SetAnimatorBool(AnimatorHash.IsBlocking, true);

            // DefenseSystem varsa bildir
            Context.Defense?.SetBlock(true);
        }

        public override void Update()
        {
            // Perfect parry window kontrolü
            if (_isParryWindowActive && StateTime > _perfectParryWindow)
            {
                _isParryWindowActive = false;
            }
        }

        public override void Exit()
        {
            base.Exit();

            // Blocking animation kapat
            SetAnimatorBool(AnimatorHash.IsBlocking, false);

            // DefenseSystem varsa bildir
            Context.Defense?.SetBlock(false);

            _isParryWindowActive = false;
        }

        public override bool CanTransitionTo(CombatState newState)
        {
            switch (newState)
            {
                case CombatState.Idle:
                case CombatState.Attacking:
                case CombatState.Dodging:
                    // Block'tan bu state'lere her zaman geçilebilir
                    return true;

                case CombatState.Staggered:
                    // Guard break durumunda stagger'a geçebilir
                    return true;

                case CombatState.Dead:
                    return true;

                default:
                    return false;
            }
        }

        public override void OnHit(DamageInfo damage)
        {
            // Perfect parry window'unda mıyız?
            if (_isParryWindowActive)
            {
                // Perfect Parry!
                damage.IsParried = true;

                // Parry animation
                PlayAnimation(ae => ae.PlayParry(), AnimatorHash.Parry);

                // Slow-mo efekti
                CombatTimeManager.Instance?.ApplyParrySlowMo();

                // Attacker'ı stagger et
                if (damage.Attacker != null && damage.Attacker.TryGetComponent<ICombatant>(out var attacker))
                {
                    float stunDuration = CombatSettings.Instance?.ParryStunDuration ?? 1.5f;
                    attacker.ApplyStagger(stunDuration);
                }

                return;
            }

            // Normal block
            damage.IsBlocked = true;

            // Block hit animation
            PlayAnimation(ae => ae.PlayBlockHit(), AnimatorHash.BlockHit);

            // Stamina drain
            float staminaDrain = damage.StaggerValue + 5f;
            Context.StaminaSystem?.Consume(staminaDrain);

            // Guard break kontrolü
            if (Context.StaminaSystem != null && Context.StaminaSystem.CurrentStamina <= 0)
            {
                // Guard broken!
                Context.TryChangeState(CombatState.Staggered);
            }
        }
    }
}
