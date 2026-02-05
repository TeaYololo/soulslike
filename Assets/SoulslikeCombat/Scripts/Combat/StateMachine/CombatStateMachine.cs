// ============================================================================
// Souls-like Combat System
// Open Source Project: https://github.com/AhmetKardesCan/soulslike-combat
// License: MIT
// Author: Toprak Eren Akpınar
//
// CombatStateMachine.cs — Core state machine that manages combat states
// ============================================================================

using System;
using UnityEngine;
using Dungeons.Data;
using Dungeons.Utilities;
using Dungeons.Combat;
using Dungeons.Player;
using FishNet.Object;

namespace Dungeons.Combat.StateMachine
{
    /// <summary>
    /// Combat State Machine - state yönetim merkezi.
    /// PlayerCombat'a eklenir ve state geçişlerini yönetir.
    /// Network mode'da state değişiklikleri server üzerinden sync edilir.
    /// </summary>
    public class CombatStateMachine : MonoBehaviour
    {
        #region Cached Components

        /// <summary>
        /// Animator component
        /// </summary>
        public Animator Animator { get; private set; }

        /// <summary>
        /// PlayerCombat component
        /// </summary>
        public PlayerCombat Combat { get; private set; }

        /// <summary>
        /// CharacterController component
        /// </summary>
        public CharacterController Controller { get; private set; }

        /// <summary>
        /// DefenseSystem component (nullable)
        /// </summary>
        public DefenseSystem Defense { get; private set; }

        /// <summary>
        /// AttackExecutor component (nullable)
        /// </summary>
        public AttackExecutor AttackExecutor { get; private set; }

        /// <summary>
        /// PlayerComboSystem component (nullable)
        /// </summary>
        public PlayerComboSystem ComboSystem { get; private set; }

        /// <summary>
        /// PlayerStaminaSystem component (nullable)
        /// </summary>
        public PlayerStaminaSystem StaminaSystem { get; private set; }

        /// <summary>
        /// PoiseSystem component (nullable)
        /// </summary>
        public PoiseSystem PoiseSystem { get; private set; }

        /// <summary>
        /// PlayerNetworkAnimEvents component (nullable)
        /// </summary>
        public PlayerNetworkAnimEvents AnimEvents { get; private set; }

        /// <summary>
        /// NetworkCombatState component (nullable - network mode)
        /// </summary>
        public NetworkCombatState NetworkState { get; private set; }

        /// <summary>
        /// NetworkObject component (nullable - network mode)
        /// </summary>
        private NetworkObject _networkObject;

        #endregion

        #region State Management

        private ICombatState _currentState;
        private CombatStateFactory _factory;
        private bool _isInitialized;

        /// <summary>
        /// Mevcut state'in CombatState enum değeri
        /// </summary>
        public CombatState CurrentStateType => _currentState?.StateType ?? CombatState.Idle;

        /// <summary>
        /// Mevcut state objesi
        /// </summary>
        public ICombatState CurrentState => _currentState;

        /// <summary>
        /// State değiştiğinde tetiklenir
        /// </summary>
        public event Action<CombatState, CombatState> OnStateChanged;

        #endregion

        #region Debug

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            CacheComponents();
            InitializeStateMachine();
        }

        private void Update()
        {
            if (!_isInitialized) return;

            _currentState?.Update();

            // Auto-transition for completed states
            if (_currentState != null && _currentState.IsComplete)
            {
                TryChangeState(CombatState.Idle);
            }
        }

        private void FixedUpdate()
        {
            if (!_isInitialized) return;

            _currentState?.FixedUpdate();
        }

        #endregion

        #region Initialization

        private void CacheComponents()
        {
            Animator = GetComponent<Animator>();
            Combat = GetComponent<PlayerCombat>();
            Controller = GetComponent<CharacterController>();
            Defense = GetComponent<DefenseSystem>();
            AttackExecutor = GetComponent<AttackExecutor>();
            ComboSystem = GetComponent<PlayerComboSystem>();
            StaminaSystem = GetComponent<PlayerStaminaSystem>();
            PoiseSystem = GetComponent<PoiseSystem>();
            AnimEvents = GetComponent<PlayerNetworkAnimEvents>();

            // Network components
            NetworkState = GetComponent<NetworkCombatState>();
            _networkObject = GetComponent<NetworkObject>();
        }

        /// <summary>
        /// Network mode'da mı? (NetworkObject varsa ve spawned ise)
        /// </summary>
        public bool IsNetworkMode => _networkObject != null && _networkObject.IsSpawned;

        /// <summary>
        /// Bu client server mı?
        /// </summary>
        public bool IsServer => _networkObject != null && _networkObject.IsServerInitialized;

        /// <summary>
        /// Bu client owner mı?
        /// </summary>
        public bool IsOwner => _networkObject != null && _networkObject.IsOwner;

        private void InitializeStateMachine()
        {
            _factory = new CombatStateFactory();

            // Start in Idle state
            _currentState = _factory.GetState(CombatState.Idle);
            _currentState.Enter(this);

            _isInitialized = true;

            if (enableDebugLogs)
            {
                DLog.Log("[CombatStateMachine] Initialized - starting in Idle state");
            }
        }

        #endregion

        #region State Transitions

        /// <summary>
        /// State değiştirmeyi dene.
        /// Mevcut state geçişe izin vermezse false döner.
        /// </summary>
        public bool TryChangeState(CombatState newState)
        {
            if (!_isInitialized)
            {
                DLog.LogWarning("[CombatStateMachine] Not initialized, cannot change state");
                return false;
            }

            // Aynı state'e geçiş yok
            if (_currentState != null && _currentState.StateType == newState)
            {
                return false;
            }

            // Mevcut state geçişe izin veriyor mu?
            if (_currentState != null && !_currentState.CanTransitionTo(newState))
            {
                if (enableDebugLogs)
                {
                    DLog.Log($"[CombatStateMachine] Transition blocked: {_currentState.StateType} -> {newState}");
                }
                return false;
            }

            // Geçiş yap
            PerformStateTransition(newState);
            return true;
        }

        /// <summary>
        /// State'i zorla değiştir (validation bypass).
        /// Sadece özel durumlar için kullan (örn: Death).
        /// </summary>
        public void ForceChangeState(CombatState newState)
        {
            if (!_isInitialized)
            {
                DLog.LogWarning("[CombatStateMachine] Not initialized, cannot force change state");
                return;
            }

            PerformStateTransition(newState);
        }

        private void PerformStateTransition(CombatState newState)
        {
            var oldState = _currentState?.StateType ?? CombatState.Idle;

            // Exit current state
            _currentState?.Exit();

            // Enter new state
            _currentState = _factory.GetState(newState);
            _currentState.Enter(this);

            // Network sync - server broadcasts state change
            SyncStateToNetwork(newState);

            // Fire event
            OnStateChanged?.Invoke(oldState, newState);

            if (enableDebugLogs)
            {
                DLog.Log($"[CombatStateMachine] State changed: {oldState} -> {newState}");
            }
        }

        /// <summary>
        /// State değişikliğini network'e sync et (server-only)
        /// </summary>
        private void SyncStateToNetwork(CombatState newState)
        {
            if (!IsNetworkMode) return;
            if (!IsServer) return;
            if (NetworkState == null) return;

            // CombatState enum -> int conversion
            int stateValue = CombatStateToNetworkValue(newState);
            NetworkState.ServerSetCombatState(stateValue);
        }

        /// <summary>
        /// Network'ten gelen state değişikliğini uygula (client-only)
        /// Bu metod NetworkCombatState'in OnCombatStateChanged callback'inden çağrılır.
        /// </summary>
        public void ApplyNetworkStateChange(int networkStateValue)
        {
            if (!_isInitialized) return;

            // Server kendi state'ini zaten yönetiyor
            if (IsServer) return;

            CombatState newState = NetworkValueToCombatState(networkStateValue);

            // Aynı state'e geçiş yok
            if (_currentState != null && _currentState.StateType == newState) return;

            // Exit current state
            _currentState?.Exit();

            // Enter new state (validation bypass - server'dan geldi)
            _currentState = _factory.GetState(newState);
            _currentState.Enter(this);

            // Fire event
            var oldState = _currentState?.StateType ?? CombatState.Idle;
            OnStateChanged?.Invoke(oldState, newState);

            if (enableDebugLogs)
            {
                DLog.Log($"[CombatStateMachine] Network state applied: {newState}");
            }
        }

        /// <summary>
        /// CombatState enum'u network int değerine çevir
        /// </summary>
        private int CombatStateToNetworkValue(CombatState state)
        {
            return state switch
            {
                CombatState.Idle => 0,
                CombatState.Attacking => 1,
                CombatState.Blocking => 2,
                CombatState.Dodging => 3,
                CombatState.Staggered => 5,
                CombatState.Recovering => 4,
                CombatState.Dead => -1,
                CombatState.UsingSkill => 7,
                _ => 0
            };
        }

        /// <summary>
        /// Network int değerini CombatState enum'a çevir
        /// </summary>
        private CombatState NetworkValueToCombatState(int value)
        {
            return value switch
            {
                0 => CombatState.Idle,
                1 => CombatState.Attacking,
                2 => CombatState.Blocking,
                3 => CombatState.Dodging,
                4 => CombatState.Recovering,
                5 => CombatState.Staggered,
                6 => CombatState.Staggered, // HitReaction -> Staggered
                7 => CombatState.UsingSkill,
                -1 => CombatState.Dead,
                _ => CombatState.Idle
            };
        }

        #endregion

        #region External Events

        /// <summary>
        /// Animation event'i mevcut state'e ilet
        /// </summary>
        public void OnAnimationEvent(string eventName)
        {
            _currentState?.OnAnimationEvent(eventName);
        }

        /// <summary>
        /// Hasar bilgisini mevcut state'e ilet
        /// </summary>
        public void OnHit(DamageInfo damage)
        {
            _currentState?.OnHit(damage);
        }

        /// <summary>
        /// Mevcut state'in belirtilen state'e geçişe izin verip vermediğini kontrol et
        /// </summary>
        public bool CanTransitionTo(CombatState newState)
        {
            if (_currentState == null) return true;
            return _currentState.CanTransitionTo(newState);
        }

        #endregion

        #region State Queries

        /// <summary>
        /// Belirtilen state'de mi?
        /// </summary>
        public bool IsInState(CombatState state)
        {
            return CurrentStateType == state;
        }

        /// <summary>
        /// Idle state'de mi?
        /// </summary>
        public bool IsIdle => IsInState(CombatState.Idle);

        /// <summary>
        /// Attacking state'de mi?
        /// </summary>
        public bool IsAttacking => IsInState(CombatState.Attacking);

        /// <summary>
        /// Blocking state'de mi?
        /// </summary>
        public bool IsBlocking => IsInState(CombatState.Blocking);

        /// <summary>
        /// Dodging state'de mi?
        /// </summary>
        public bool IsDodging => IsInState(CombatState.Dodging);

        /// <summary>
        /// Staggered state'de mi?
        /// </summary>
        public bool IsStaggered => IsInState(CombatState.Staggered);

        /// <summary>
        /// Dead state'de mi?
        /// </summary>
        public bool IsDead => IsInState(CombatState.Dead);

        #endregion
    }
}
