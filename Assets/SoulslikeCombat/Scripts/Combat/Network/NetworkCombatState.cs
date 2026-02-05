// ============================================================================
// Souls-like Combat System
// Open Source Project: https://github.com/AhmetKardesCan/soulslike-combat
// License: MIT
// Author: Toprak Eren Akpınar
//
// NetworkCombatState.cs — FishNet multiplayer state synchronization
// ============================================================================

using Dungeons.Utilities;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Dungeons.Core;
using Dungeons.Core.Utilities;
using Dungeons.Player;
using UnityEngine;

namespace Dungeons.Combat
{
    /// <summary>
    /// Player'ın combat state'ini network üzerinden sync eder.
    /// Server-authoritative: Sadece server değiştirir, client'lar SyncVar callback ile UI günceller.
    /// </summary>
    public class NetworkCombatState : NetworkBehaviour
    {
        // ========================================
        // SYNCED STATE — Server yazar, Client okur
        // ========================================

        public readonly SyncVar<float> _currentHealth = new();
        public readonly SyncVar<float> _maxHealth = new();
        public readonly SyncVar<float> _currentPoise = new();
        public readonly SyncVar<float> _maxPoise = new();
        public readonly SyncVar<float> _currentStamina = new();
        public readonly SyncVar<float> _maxStamina = new();
        public readonly SyncVar<int> _combatState = new();
        public readonly SyncVar<bool> _isDead = new();
        public readonly SyncVar<bool> _isInvulnerable = new();

        // ========================================
        // SERVER-ONLY CONFIG
        // ========================================

        [Header("Regen Rates")]
        [SerializeField] private float staminaRegenRate = 15f;
        [SerializeField] private float staminaRegenDelay = 1.5f;
        [SerializeField] private float poiseRegenRate = 10f;

        private float _staminaRegenDelayTimer;

        // ========================================
        // PUBLIC PROPERTIES
        // ========================================

        public float CurrentHealth => _currentHealth.Value;
        public float MaxHealth => _maxHealth.Value;
        public float CurrentPoise => _currentPoise.Value;
        public float MaxPoise => _maxPoise.Value;
        public float CurrentStamina => _currentStamina.Value;
        public float MaxStamina => _maxStamina.Value;
        public int CombatStateValue => _combatState.Value;
        public bool IsDead => _isDead.Value;
        public bool IsInvulnerable => _isInvulnerable.Value;

        public float HealthPercent => _maxHealth.Value > 0 ? _currentHealth.Value / _maxHealth.Value : 0f;
        public float StaminaPercent => _maxStamina.Value > 0 ? _currentStamina.Value / _maxStamina.Value : 0f;

        // ========================================
        // INITIALIZATION
        // ========================================

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // SyncVar OnChange callbacks
            _currentHealth.OnChange += OnHealthChanged;
            _currentPoise.OnChange += OnPoiseChanged;
            _currentStamina.OnChange += OnStaminaChanged;
            _combatState.OnChange += OnCombatStateChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            _currentHealth.OnChange -= OnHealthChanged;
            _currentPoise.OnChange -= OnPoiseChanged;
            _currentStamina.OnChange -= OnStaminaChanged;
            _combatState.OnChange -= OnCombatStateChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            InitializeFromExistingSystem();
        }

        private void InitializeFromExistingSystem()
        {
            // PlayerCombat'tan HP ve Poise
            var combat = GetComponent<PlayerCombat>();
            if (combat != null)
            {
                _maxHealth.Value = combat.MaxHealth;
                _currentHealth.Value = combat.CurrentHealth;
                _maxPoise.Value = 100f; // PlayerCombat.maxPoise default
            }
            else
            {
                _maxHealth.Value = 100f;
                _currentHealth.Value = 100f;
                _maxPoise.Value = 100f;
            }
            _currentPoise.Value = _maxPoise.Value;

            // PlayerStaminaSystem'den Stamina
            var staminaSys = GetComponent<PlayerStaminaSystem>();
            if (staminaSys != null)
            {
                _maxStamina.Value = staminaSys.MaxStamina;
                _currentStamina.Value = staminaSys.CurrentStamina;
            }
            else
            {
                _maxStamina.Value = 100f;
                _currentStamina.Value = 100f;
            }

            // CombatSettings'ten regen değerlerini al
            if (CombatSettings.Instance != null)
            {
                staminaRegenRate = CombatSettings.Instance.StaminaRegenRate;
                staminaRegenDelay = CombatSettings.Instance.StaminaRegenDelay;
            }
        }

        // ========================================
        // SERVER-ONLY MUTATIONS
        // ========================================

        [Server]
        public void ServerApplyDamage(float damage)
        {
            if (_isDead.Value || _isInvulnerable.Value) return;
            _currentHealth.Value = Mathf.Max(0f, _currentHealth.Value - damage);
            if (_currentHealth.Value <= 0f)
            {
                _isDead.Value = true;
                OnServerDeath();
            }
        }

        [Server]
        public bool ServerApplyPoiseDamage(float poiseDamage)
        {
            if (_isDead.Value) return false;
            _currentPoise.Value = Mathf.Max(0f, _currentPoise.Value - poiseDamage);
            bool stagger = _currentPoise.Value <= 0f;
            if (stagger)
                _currentPoise.Value = _maxPoise.Value; // Reset poise on stagger
            return stagger;
        }

        [Server]
        public bool ServerConsumeStamina(float amount)
        {
            // Negative amount = restore
            if (amount > 0 && _currentStamina.Value < amount) return false;
            _currentStamina.Value = Mathf.Clamp(_currentStamina.Value - amount, 0f, _maxStamina.Value);
            _staminaRegenDelayTimer = staminaRegenDelay;
            return true;
        }

        [Server]
        public void ServerHeal(float amount)
        {
            if (_isDead.Value) return;
            _currentHealth.Value = Mathf.Min(_maxHealth.Value, _currentHealth.Value + amount);
        }

        [Server]
        public void ServerSetCombatState(int state) => _combatState.Value = state;

        [Server]
        public void ServerSetInvulnerable(bool value) => _isInvulnerable.Value = value;

        [Server]
        public void ServerRegenPoise(float rate, float delta)
        {
            if (_isDead.Value || _currentPoise.Value >= _maxPoise.Value) return;
            _currentPoise.Value = Mathf.Min(_maxPoise.Value, _currentPoise.Value + rate * delta);
        }

        [Server]
        public void ServerRegenStamina(float rate, float delta)
        {
            if (_isDead.Value || _currentStamina.Value >= _maxStamina.Value) return;
            if (_staminaRegenDelayTimer > 0) return;
            _currentStamina.Value = Mathf.Min(_maxStamina.Value, _currentStamina.Value + rate * delta);
        }

        // ========================================
        // SERVER DEATH
        // ========================================

        [Server]
        private void OnServerDeath()
        {
            _combatState.Value = -1;
            DLog.Log($"[NetworkCombatState] Player #{OwnerId} öldü!");

            // NetworkDeathRespawn'a bildir — respawn sürecini başlat
            var deathRespawn = GetComponent<Player.NetworkDeathRespawn>();
            if (deathRespawn != null)
                deathRespawn.ServerTriggerDeath();

            ObserversOnDeath();
        }

        [ObserversRpc]
        private void ObserversOnDeath()
        {
            var animEvents = GetComponent<PlayerNetworkAnimEvents>();
            animEvents?.PlayDeath();

            EventBus.Publish(new CharacterDiedEvent
            {
                Character = gameObject,
                Killer = null,
                IsEnemy = false
            });
        }

        // ========================================
        // SYNCVAR CALLBACKS — Client UI Update
        // ========================================

        private void OnHealthChanged(float prev, float next, bool asServer)
        {
            if (asServer) return;
            EventBus.Publish(new PlayerHealthChangedEvent
            {
                CurrentHealth = Mathf.RoundToInt(next),
                MaxHealth = Mathf.RoundToInt(_maxHealth.Value)
            });
        }

        private void OnPoiseChanged(float prev, float next, bool asServer)
        {
            // Poise UI yok şimdilik
        }

        private void OnStaminaChanged(float prev, float next, bool asServer)
        {
            if (asServer) return;
            EventBus.Publish(new StaminaChangedEvent
            {
                Character = gameObject,
                CurrentStamina = next,
                MaxStamina = _maxStamina.Value
            });
        }

        private void OnCombatStateChanged(int prev, int next, bool asServer)
        {
            if (asServer) return;

            // CombatStateMachine varsa ona bildir (yeni sistem)
            // State machine Enter/Exit metodları animator bool'larını ayarlar
            var stateMachine = GetComponent<StateMachine.CombatStateMachine>();
            if (stateMachine != null)
            {
                stateMachine.ApplyNetworkStateChange(next);
                return; // State machine tüm animator güncellemelerini yapar
            }

            // Legacy fallback: CombatStateMachine yoksa manual animator güncelleme
            var animator = GetComponent<Animator>();
            if (animator == null) return;

            animator.SetBool(AnimatorHash.IsBlocking, next == 2);
            animator.SetBool(AnimatorHash.IsStunned, next == 5 || next == 6);
            animator.SetBool(AnimatorHash.IsDead, next == -1);

            if (next == -1 && prev != -1) animator.SetTrigger(AnimatorHash.Death);
        }

        // ========================================
        // SERVER REGEN LOOP
        // ========================================

        private void Update()
        {
            if (!IsServerInitialized) return;
            float delta = Time.deltaTime;

            // Stamina regen delay timer
            if (_staminaRegenDelayTimer > 0)
                _staminaRegenDelayTimer -= delta;

            ServerRegenStamina(staminaRegenRate, delta);
            ServerRegenPoise(poiseRegenRate, delta);
        }
    }
}
