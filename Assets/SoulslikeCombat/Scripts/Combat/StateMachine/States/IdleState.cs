using Dungeons.Data;
using Dungeons.Combat;

namespace Dungeons.Combat.StateMachine.States
{
    /// <summary>
    /// Idle state - varsayılan duruş.
    /// Tüm action'lara geçiş yapılabilir.
    /// </summary>
    public class IdleState : CombatStateBase
    {
        public override CombatState StateType => CombatState.Idle;

        public override void Enter(CombatStateMachine context)
        {
            base.Enter(context);

            // Reset any combat flags
            if (Combat != null)
            {
                Combat.SetInvincible(false);
            }
        }

        public override bool CanTransitionTo(CombatState newState)
        {
            // Idle'dan tüm state'lere geçilebilir
            // Dead için health kontrolü yapılabilir ama genelde Death doğrudan zorlanır
            return true;
        }

        public override void OnHit(DamageInfo damage)
        {
            // Hit alınca stagger veya hit reaction
            if (damage.CausesFlinch)
            {
                Context.TryChangeState(CombatState.Staggered);
            }
        }
    }
}
