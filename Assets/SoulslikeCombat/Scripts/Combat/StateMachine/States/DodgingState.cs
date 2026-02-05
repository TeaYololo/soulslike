using UnityEngine;
using Dungeons.Data;
using Dungeons.Core.Utilities;
using Dungeons.Combat;

namespace Dungeons.Combat.StateMachine.States
{
    /// <summary>
    /// Dodging state - dodge/roll sırasında.
    /// i-Frame yönetimi yapar.
    /// </summary>
    public class DodgingState : CombatStateBase
    {
        public override CombatState StateType => CombatState.Dodging;

        // i-Frame timing
        private float _dodgeDuration;
        private float _iFrameStart;
        private float _iFrameDuration;
        private bool _isInvincible;
        private bool _dodgeComplete;

        /// <summary>
        /// i-Frame'de mi?
        /// </summary>
        public bool IsInvincible => _isInvincible;

        public override bool IsComplete => _dodgeComplete;

        public override void Enter(CombatStateMachine context)
        {
            base.Enter(context);

            _dodgeComplete = false;
            _isInvincible = false;

            // CombatSettings'den timing değerlerini al
            var settings = CombatSettings.Instance;
            if (settings != null)
            {
                _dodgeDuration = settings.DodgeDuration;
                _iFrameStart = settings.DodgeIFrameStart;
                _iFrameDuration = settings.DodgeIFrameDuration;
            }
            else
            {
                // Fallback değerler
                _dodgeDuration = 0.6f;
                _iFrameStart = 0.05f;
                _iFrameDuration = 0.4f;
            }

            // Dodge animation
            PlayAnimation(ae => ae.PlayDodge(), AnimatorHash.Dodge);
        }

        public override void Update()
        {
            float elapsed = StateTime;

            // i-Frame window kontrolü
            bool shouldBeInvincible = elapsed >= _iFrameStart && elapsed < (_iFrameStart + _iFrameDuration);

            if (shouldBeInvincible != _isInvincible)
            {
                _isInvincible = shouldBeInvincible;
                Combat?.SetInvincible(_isInvincible);
            }

            // Dodge tamamlandı mı?
            if (elapsed >= _dodgeDuration && !_dodgeComplete)
            {
                OnDodgeComplete();
            }
        }

        public override void Exit()
        {
            base.Exit();

            // i-Frame'i kapat
            _isInvincible = false;
            Combat?.SetInvincible(false);

            _dodgeComplete = false;
        }

        public override bool CanTransitionTo(CombatState newState)
        {
            switch (newState)
            {
                case CombatState.Idle:
                    // Dodge tamamlandığında Idle'a geçebilir
                    return _dodgeComplete;

                case CombatState.Dead:
                    // Her zaman ölüme geçebilir
                    return true;

                case CombatState.Attacking:
                    // Bazı sistemler dodge attack destekler (Hades style)
                    if (Context.Defense != null && Context.Defense.CanAttackDuringDodge)
                    {
                        return true;
                    }
                    return false;

                default:
                    // Dodge sırasında diğer state'lere geçilemez
                    return false;
            }
        }

        public override void OnHit(DamageInfo damage)
        {
            // i-Frame'deyse hasar yok
            if (_isInvincible)
            {
                // Perfect dodge check
                var settings = CombatSettings.Instance;
                float perfectDodgeWindow = settings?.PerfectDodgeWindow ?? 0.08f;

                if (StateTime <= perfectDodgeWindow)
                {
                    // Perfect Dodge!
                    Combat?.OnPerfectDodge();

                    // Slow-mo efekti (aktifse)
                    if (settings != null && settings.EnablePerfectDodgeSlowmo)
                    {
                        CombatTimeManager.Instance?.ApplyParrySlowMo();
                    }
                }

                return; // Hasar yok
            }

            // i-Frame dışında normal hasar
            base.OnHit(damage);
        }

        public override void OnAnimationEvent(string eventName)
        {
            switch (eventName)
            {
                case "DodgeEnd":
                case "OnDodgeEnd":
                    OnDodgeComplete();
                    break;

                case "IFrameStart":
                    _isInvincible = true;
                    Combat?.SetInvincible(true);
                    break;

                case "IFrameEnd":
                    _isInvincible = false;
                    Combat?.SetInvincible(false);
                    break;
            }
        }

        private void OnDodgeComplete()
        {
            _dodgeComplete = true;
            _isInvincible = false;
            Combat?.SetInvincible(false);
            Context.TryChangeState(CombatState.Idle);
        }
    }
}
