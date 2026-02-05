using UnityEngine;
using Dungeons.Data;
using Dungeons.Combat;

namespace Dungeons.Combat.StateMachine.States
{
    /// <summary>
    /// UsingSkill state - özel yetenek kullanırken.
    /// Channeled veya instant skill'ler için.
    /// </summary>
    public class UsingSkillState : CombatStateBase
    {
        public override CombatState StateType => CombatState.UsingSkill;

        private float _skillDuration;
        private bool _skillComplete;
        private bool _isChanneled;
        private bool _isCancelable;

        public override bool IsComplete => _skillComplete;

        /// <summary>
        /// Skill süresi
        /// </summary>
        public float SkillDuration
        {
            get => _skillDuration;
            set => _skillDuration = value;
        }

        /// <summary>
        /// Channeled skill mi? (tutulan süre boyunca aktif)
        /// </summary>
        public bool IsChanneled
        {
            get => _isChanneled;
            set => _isChanneled = value;
        }

        /// <summary>
        /// Skill cancel edilebilir mi?
        /// </summary>
        public bool IsCancelable
        {
            get => _isCancelable;
            set => _isCancelable = value;
        }

        public override void Enter(CombatStateMachine context)
        {
            base.Enter(context);

            _skillComplete = false;

            // Default skill duration
            if (_skillDuration <= 0)
            {
                _skillDuration = 1.0f;
            }
        }

        public override void Update()
        {
            // Channeled olmayan skill'ler için duration kontrolü
            if (!_isChanneled && StateTime >= _skillDuration && !_skillComplete)
            {
                OnSkillComplete();
            }
        }

        public override void Exit()
        {
            base.Exit();
            _skillComplete = false;
            _skillDuration = 0f;
            _isChanneled = false;
            _isCancelable = false;
        }

        public override bool CanTransitionTo(CombatState newState)
        {
            switch (newState)
            {
                case CombatState.Idle:
                    // Skill tamamlandığında veya cancel edildiğinde
                    return _skillComplete || _isCancelable;

                case CombatState.Staggered:
                    // Skill sırasında stagger olunabilir (cancelable ise)
                    return _isCancelable;

                case CombatState.Dead:
                    return true;

                case CombatState.Dodging:
                    // Cancelable skill'ler dodge ile iptal edilebilir
                    return _isCancelable;

                default:
                    return false;
            }
        }

        public override void OnAnimationEvent(string eventName)
        {
            switch (eventName)
            {
                case "SkillEnd":
                case "OnSkillEnd":
                    OnSkillComplete();
                    break;

                case "SkillEffect":
                    // Skill efekti tetiklendi
                    // Alt sistemler burada efekt spawn edebilir
                    break;
            }
        }

        public override void OnHit(DamageInfo damage)
        {
            // Cancelable skill ise stagger'a geçebilir
            if (_isCancelable && damage.CausesFlinch)
            {
                Context.TryChangeState(CombatState.Staggered);
                return;
            }

            // Cancelable değilse hasar alınır ama skill devam eder
            // (super armor gibi)
        }

        /// <summary>
        /// Skill'i manuel tamamla (channeled skill'ler için)
        /// </summary>
        public void CompleteSkill()
        {
            OnSkillComplete();
        }

        /// <summary>
        /// Skill'i iptal et
        /// </summary>
        public void CancelSkill()
        {
            if (_isCancelable)
            {
                _skillComplete = true;
                Context.TryChangeState(CombatState.Idle);
            }
        }

        private void OnSkillComplete()
        {
            _skillComplete = true;
            Context.TryChangeState(CombatState.Idle);
        }
    }
}
