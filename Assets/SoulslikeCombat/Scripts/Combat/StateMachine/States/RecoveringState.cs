using UnityEngine;
using Dungeons.Data;
using Dungeons.Combat;

namespace Dungeons.Combat.StateMachine.States
{
    /// <summary>
    /// Recovering state - ağır bir aksiyon sonrası toparlanma.
    /// Guard break, knockdown gibi durumlardan sonra kullanılır.
    /// </summary>
    public class RecoveringState : CombatStateBase
    {
        public override CombatState StateType => CombatState.Recovering;

        private float _recoveryDuration;
        private bool _recoveryComplete;

        public override bool IsComplete => _recoveryComplete;

        /// <summary>
        /// Recovery süresi (dışarıdan set edilebilir)
        /// </summary>
        public float RecoveryDuration
        {
            get => _recoveryDuration;
            set => _recoveryDuration = value;
        }

        public override void Enter(CombatStateMachine context)
        {
            base.Enter(context);

            _recoveryComplete = false;

            // Default recovery duration
            if (_recoveryDuration <= 0)
            {
                _recoveryDuration = 0.5f;
            }
        }

        public override void Update()
        {
            // Recovery süresi doldu mu?
            if (StateTime >= _recoveryDuration && !_recoveryComplete)
            {
                OnRecoveryComplete();
            }
        }

        public override void Exit()
        {
            base.Exit();
            _recoveryComplete = false;
            _recoveryDuration = 0f;
        }

        public override bool CanTransitionTo(CombatState newState)
        {
            switch (newState)
            {
                case CombatState.Idle:
                    // Recovery tamamlandığında
                    return _recoveryComplete;

                case CombatState.Attacking:
                case CombatState.Blocking:
                case CombatState.Dodging:
                    // Recovery sırasında bu aksiyonlara geçilebilir
                    return true;

                case CombatState.Staggered:
                    // Recovery sırasında stagger olunabilir
                    return true;

                case CombatState.Dead:
                    return true;

                default:
                    return false;
            }
        }

        public override void OnAnimationEvent(string eventName)
        {
            switch (eventName)
            {
                case "RecoveryEnd":
                case "OnRecoveryEnd":
                    OnRecoveryComplete();
                    break;
            }
        }

        private void OnRecoveryComplete()
        {
            _recoveryComplete = true;
            Context.TryChangeState(CombatState.Idle);
        }
    }
}
