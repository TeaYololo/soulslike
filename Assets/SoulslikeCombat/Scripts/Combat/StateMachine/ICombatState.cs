using Dungeons.Data;
using Dungeons.Combat;

namespace Dungeons.Combat.StateMachine
{
    /// <summary>
    /// Combat state interface.
    /// Her state bu interface'i implement eder.
    /// </summary>
    public interface ICombatState
    {
        /// <summary>
        /// Bu state'in CombatState enum karşılığı
        /// </summary>
        CombatState StateType { get; }

        /// <summary>
        /// State'e girildiğinde çağrılır
        /// </summary>
        void Enter(CombatStateMachine context);

        /// <summary>
        /// Her frame çağrılır
        /// </summary>
        void Update();

        /// <summary>
        /// Physics update - her fixed frame çağrılır
        /// </summary>
        void FixedUpdate();

        /// <summary>
        /// State'den çıkıldığında çağrılır
        /// </summary>
        void Exit();

        /// <summary>
        /// Bu state'den belirtilen state'e geçiş yapılabilir mi?
        /// </summary>
        bool CanTransitionTo(CombatState newState);

        /// <summary>
        /// Animation event geldiğinde çağrılır
        /// </summary>
        void OnAnimationEvent(string eventName);

        /// <summary>
        /// Hasar alındığında çağrılır
        /// </summary>
        void OnHit(DamageInfo damage);

        /// <summary>
        /// State'in kendi başına tamamlanıp tamamlanmadığı (duration-based states için)
        /// </summary>
        bool IsComplete { get; }
    }
}
