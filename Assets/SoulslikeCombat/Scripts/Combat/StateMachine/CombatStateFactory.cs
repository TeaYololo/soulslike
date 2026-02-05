using System.Collections.Generic;
using Dungeons.Data;
using Dungeons.Combat.StateMachine.States;

namespace Dungeons.Combat.StateMachine
{
    /// <summary>
    /// Combat state factory.
    /// State objelerini oluşturur ve cache'ler.
    /// </summary>
    public class CombatStateFactory
    {
        private readonly Dictionary<CombatState, ICombatState> _stateCache;

        public CombatStateFactory()
        {
            _stateCache = new Dictionary<CombatState, ICombatState>();
            CreateStates();
        }

        private void CreateStates()
        {
            // Tüm state'leri önceden oluştur (allocation-free runtime)
            _stateCache[CombatState.Idle] = new IdleState();
            _stateCache[CombatState.Attacking] = new AttackingState();
            _stateCache[CombatState.Blocking] = new BlockingState();
            _stateCache[CombatState.Dodging] = new DodgingState();
            _stateCache[CombatState.Staggered] = new StaggeredState();
            _stateCache[CombatState.Recovering] = new RecoveringState();
            _stateCache[CombatState.Dead] = new DeadState();
            _stateCache[CombatState.UsingSkill] = new UsingSkillState();
        }

        /// <summary>
        /// Belirtilen state'i al (cache'den)
        /// </summary>
        public ICombatState GetState(CombatState stateType)
        {
            if (_stateCache.TryGetValue(stateType, out var state))
            {
                return state;
            }

            // Fallback: Idle state
            return _stateCache[CombatState.Idle];
        }

        /// <summary>
        /// Custom state ekle (extension için)
        /// </summary>
        public void RegisterState(CombatState stateType, ICombatState state)
        {
            _stateCache[stateType] = state;
        }
    }
}
