using UnityEngine;
using Dungeons.Data;
using Dungeons.Core.Utilities;
using Dungeons.Combat;

namespace Dungeons.Combat.StateMachine.States
{
    /// <summary>
    /// Staggered state - poise kırıldığında veya parry yediğinde.
    /// Belirli süre hiçbir aksiyon alınamaz.
    /// </summary>
    public class StaggeredState : CombatStateBase
    {
        public override CombatState StateType => CombatState.Staggered;

        private float _staggerDuration;
        private bool _staggerComplete;

        public override bool IsComplete => _staggerComplete;

        /// <summary>
        /// Stagger süresi (dışarıdan set edilebilir)
        /// </summary>
        public float StaggerDuration
        {
            get => _staggerDuration;
            set => _staggerDuration = value;
        }

        public override void Enter(CombatStateMachine context)
        {
            base.Enter(context);

            _staggerComplete = false;

            // Default stagger duration (dışarıdan set edilmemişse)
            if (_staggerDuration <= 0)
            {
                _staggerDuration = 1.0f;
            }

            // Stagger animation
            PlayAnimation(ae => ae.PlayStagger(), AnimatorHash.Stagger);

            // IsStunned bool'unu set et (network sync için)
            SetAnimatorBool(AnimatorHash.IsStunned, true);

            // Mevcut attack'ı cancel et
            Context.AttackExecutor?.CancelCurrentAttack();
        }

        public override void Update()
        {
            // Stagger süresi doldu mu?
            if (StateTime >= _staggerDuration && !_staggerComplete)
            {
                OnStaggerComplete();
            }
        }

        public override void Exit()
        {
            base.Exit();

            // IsStunned bool'unu temizle
            SetAnimatorBool(AnimatorHash.IsStunned, false);

            _staggerComplete = false;
            _staggerDuration = 0f; // Reset for next use
        }

        public override bool CanTransitionTo(CombatState newState)
        {
            switch (newState)
            {
                case CombatState.Idle:
                    // Stagger tamamlandığında Idle'a geçebilir
                    return _staggerComplete;

                case CombatState.Recovering:
                    // Stagger'dan recovery'ye geçebilir
                    return _staggerComplete;

                case CombatState.Dead:
                    // Her zaman ölüme geçebilir
                    return true;

                case CombatState.Staggered:
                    // Stagger sırasında tekrar stagger olunabilir (combo hit)
                    return true;

                default:
                    // Stagger sırasında aksiyon alınamaz
                    return false;
            }
        }

        public override void OnAnimationEvent(string eventName)
        {
            switch (eventName)
            {
                case "StaggerEnd":
                case "OnStaggerEnd":
                    OnStaggerComplete();
                    break;
            }
        }

        public override void OnHit(DamageInfo damage)
        {
            // Stagger sırasında yeni hit: stagger süresini uzat
            if (damage.CausesFlinch)
            {
                // Stagger'ı yeniden başlat
                _staggerDuration = damage.StaggerValue > 0 ? damage.StaggerValue * 0.1f : 0.5f;
                _staggerComplete = false;

                // Re-enter animation
                PlayAnimation(ae => ae.PlayStagger(), AnimatorHash.Stagger);
            }
        }

        private void OnStaggerComplete()
        {
            _staggerComplete = true;
            Context.TryChangeState(CombatState.Idle);
        }
    }
}
