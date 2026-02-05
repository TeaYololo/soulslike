using Dungeons.Data;
using Dungeons.Core.Utilities;
using Dungeons.Combat;

namespace Dungeons.Combat.StateMachine.States
{
    /// <summary>
    /// Dead state - karakter öldüğünde.
    /// Bu state'den çıkış yoktur (respawn hariç).
    /// </summary>
    public class DeadState : CombatStateBase
    {
        public override CombatState StateType => CombatState.Dead;

        public override void Enter(CombatStateMachine context)
        {
            base.Enter(context);

            // Death animation
            PlayAnimation(ae => ae.PlayDeath(), AnimatorHash.Death);

            // IsDead bool'unu set et
            SetAnimatorBool(AnimatorHash.IsDead, true);

            // Mevcut attack'ı cancel et
            Context.AttackExecutor?.CancelCurrentAttack();

            // Combat'ı deaktive et
            if (Combat != null)
            {
                Combat.SetInvincible(true); // Ölü karaktere hasar verilmez
            }
        }

        public override void Exit()
        {
            base.Exit();

            // Respawn durumunda
            SetAnimatorBool(AnimatorHash.IsDead, false);

            if (Combat != null)
            {
                Combat.SetInvincible(false);
            }
        }

        public override bool CanTransitionTo(CombatState newState)
        {
            // Dead state'den sadece Idle'a geçilebilir (respawn için)
            // Bu geçiş genelde ForceChangeState ile yapılır
            return newState == CombatState.Idle;
        }

        public override void OnHit(DamageInfo damage)
        {
            // Ölü karaktere hasar verilmez
            // Hiçbir şey yapma
        }

        public override void OnAnimationEvent(string eventName)
        {
            // Death animation event'lerini handle et
            switch (eventName)
            {
                case "DeathComplete":
                    // Ragdoll veya başka efektler burada tetiklenebilir
                    break;
            }
        }
    }
}
