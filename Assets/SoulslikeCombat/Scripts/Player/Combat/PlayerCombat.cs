// ============================================================================
// Souls-like Combat System
// Open Source Project: https://github.com/AhmetKardesCan/soulslike-combat
// License: MIT
// Author: Toprak Eren Akpınar
//
// PlayerCombat.cs — Main player combat controller
// ============================================================================

using Dungeons.Utilities;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Dungeons.Core;
using Dungeons.Core.Utilities;
using Dungeons.Data;
using Dungeons.Data.Interfaces;
using Dungeons.Character;
using Dungeons.Items;
using Dungeons.VFX;
using Dungeons.Player;
using Dungeons.Combat.StateMachine;
using Dungeons.Combat.Utility;

namespace Dungeons.Combat
{
    /// <summary>
    /// Player real-time combat controller.
    /// Input'u alır, combo'ları yönetir, dodge/block işler.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCombat : MonoBehaviour, ICombatant, IDamageable
    {
        [Header("References")]
        [SerializeField] private CharacterStats stats;
        [SerializeField] private Transform weaponHolder;

        [Header("Stamina (Legacy - PlayerStaminaSystem kullanılır)")]
        [Tooltip("Deprecated: PlayerStaminaSystem bu değerleri yönetir")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaRegenRate = 20f;
        [SerializeField] private float staminaRegenDelay = 1f;

        [Header("Attack Settings (Legacy - PlayerStaminaSystem kullanılır)")]
        [Tooltip("Deprecated: PlayerStaminaSystem.LightAttackCost kullanılır")]
        [SerializeField] private float lightAttackStamina = 15f;
        [Tooltip("Deprecated: PlayerStaminaSystem.HeavyAttackCost kullanılır")]
        [SerializeField] private float heavyAttackStamina = 30f;

        [Header("Dodge Settings")]
        [Tooltip("Deprecated: PlayerStaminaSystem.DodgeCost kullanılır")]
        [SerializeField] private float dodgeStamina = 25f;
        [SerializeField] private float dodgeDistance = 3f; // Adjust to match animation
        [SerializeField] private float dodgeDuration = 0.6f; // Adjust to match animation length
        [SerializeField] private float dodgeIFrameStart = 0.05f;
        [SerializeField] private float dodgeIFrameDuration = 0.4f;

        [Header("Block Settings")]
        [Tooltip("Deprecated: PlayerStaminaSystem.ConsumeBlockHit() kullanılır")]
        [SerializeField] private float blockStaminaDrain = 5f;
        [SerializeField] private float perfectParryWindow = 0.3f; // Geniş parry penceresi (300ms)
        [SerializeField] private float blockDamageReduction = 1.0f; // %100 hasar engelleme

        [Header("Stagger")]
        [SerializeField] private float maxPoise = 100f;
        [SerializeField] private float poiseRegenRate = 10f;

        [Header("Animation Canceling")]
        [Tooltip("Recovery fazında dodge yapılabilir")]
        [SerializeField] private bool allowRecoveryDodge = true;
        [Tooltip("Block her zaman aktive edilebilir")]
        [SerializeField] private bool allowInstantBlock = true;
        [Tooltip("Koşarken saldırı yapılabilir")]
        [SerializeField] private bool allowRunningAttack = true;

        // State Machine - tek kaynak olarak CombatStateMachine kullanılır
        private CombatStateMachine _stateMachine;
        private float _currentHealth;
        private float _currentPoise;
        private float _staggerEndTime;
        private bool _isInvincible;
        private bool _isBlocking;
        private float _blockStartTime;
        private float _attackStateStartTime; // Safety net for stuck attack state

        // Components
        private Animator _animator;
        private CharacterController _controller;
        private Weapon _currentWeapon;
        private DefenseSystem _defenseSystem;
        private RangedAimController _aimController;
        private Items.BowWeapon _currentBow;
        private BowAnimationController _bowAnimController;

        // Refactored Sub-Systems (SRP)
        private AttackExecutor _attackExecutor;
        private PlayerComboSystem _comboSystem;
        private PlayerStaminaSystem _staminaSystem;

        // Network anim sync
        private PlayerNetworkAnimEvents _animEvents;

        // Advanced Combat Systems
        private PoiseSystem _poiseSystem;
        private HyperArmorWindow _hyperArmorWindow;
        private HitReactionSystem _hitReactionSystem;
        private GuardBreakSystem _guardBreakSystem;
        private BackstabRiposteSystem _backstabSystem;

        // Network bypass
        private bool _networkControlled;
        private NetworkCombatController _networkCombatController;

        // Animator parameter cache
        private HashSet<string> _animatorParameters;

        // Properties - ICombatant
        public CharacterStats Stats => stats;
        public CombatState CurrentCombatState => _stateMachine?.CurrentStateType ?? CombatState.Idle;
        public float CurrentStamina => _staminaSystem?.CurrentStamina ?? 0;
        public float MaxStamina => _staminaSystem?.MaxStamina ?? maxStamina;
        public bool IsInRecoveryPhase => CurrentCombatState == CombatState.Attacking && (_attackExecutor?.AttackProgress ?? 0f) >= (_comboSystem?.RecoveryPhaseStart ?? 0.7f);
        public bool HitLandedThisAttack => _comboSystem?.HitLandedThisAttack ?? false;

        // Animation Canceling destekli CanAttack
        public bool CanAttack
        {
            get
            {
                if (CurrentCombatState == CombatState.Idle) return true;

                // Hit confirm'de combo devam edebilir (delegated to ComboSystem)
                bool allowHitConfirm = _comboSystem?.AllowHitConfirmCombo ?? true;
                bool hitLanded = _comboSystem?.HitLandedThisAttack ?? false;
                if (allowHitConfirm && CurrentCombatState == CombatState.Attacking && hitLanded && IsInRecoveryPhase)
                    return true;

                // Dodge sırasında saldırı (Hades style)
                if (_defenseSystem != null && _defenseSystem.CanAttackDuringDodge)
                    return true;

                return false;
            }
        }

        public bool CanAttackWithStamina(float cost) => CanAttack && (_staminaSystem?.HasStamina(cost) ?? true);

        // Animation Canceling destekli CanDodge (delegated to StaminaSystem)
        public bool CanDodge
        {
            get
            {
                if (_staminaSystem != null && !_staminaSystem.CanDodge) return false;
                if (_staminaSystem == null && CurrentStamina < dodgeStamina) return false;

                if (CurrentCombatState == CombatState.Idle) return true;

                // Recovery fazında dodge yapılabilir
                if (allowRecoveryDodge && IsInRecoveryPhase)
                    return true;

                return false;
            }
        }

        // Animation Canceling destekli CanBlock
        public bool CanBlock
        {
            get
            {
                if (CurrentCombatState == CombatState.Idle || CurrentCombatState == CombatState.Blocking) return true;

                // Her an block aktive edilebilir
                if (allowInstantBlock && CurrentCombatState == CombatState.Attacking)
                    return true;

                return false;
            }
        }

        public bool IsInvincible => _isInvincible;
        public bool AllowRunningAttack => allowRunningAttack;

        /// <summary>
        /// Hyper armor aktif mi? (AttackExecutor'dan)
        /// </summary>
        public bool HasHyperArmor => _attackExecutor?.IsHyperArmorActive ?? false;

        /// <summary>
        /// Mevcut saldırının süresi
        /// </summary>
        public float CurrentAttackDuration => _attackExecutor?.CurrentAttackDuration ?? 1f;

        /// <summary>
        /// Invincibility durumunu ayarla (dodge i-frames vb.)
        /// </summary>
        public void SetInvincible(bool invincible)
        {
            _isInvincible = invincible;
        }

        /// <summary>
        /// Perfect dodge yapıldığında çağrılır
        /// </summary>
        public void OnPerfectDodge()
        {
            DLog.Log("[PlayerCombat] Perfect Dodge!");

            // Event yayınla - StyleSystem bunu EventBus üzerinden dinliyor
            EventBus.Publish(new PerfectDodgeEvent
            {
                Character = gameObject
            });
        }

        /// <summary>
        /// Network modunda combat action'ları NetworkCombatController'a yönlendir.
        /// </summary>
        public void SetNetworkControlled(bool controlled)
        {
            _networkControlled = controlled;
            if (controlled)
                _networkCombatController = GetComponent<NetworkCombatController>();
        }

        // Properties - IDamageable
        public int CurrentHealth => Mathf.RoundToInt(_currentHealth);
        public int MaxHealth => stats.MaxHealth;
        public bool IsAlive => _currentHealth > 0;

        // Events
        public System.Action<float, float> OnStaminaChanged;
        public System.Action<int, int> OnHealthChanged;
        public System.Action<CombatState> OnCombatStateChanged;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _controller = GetComponent<CharacterController>();
            _defenseSystem = GetComponent<DefenseSystem>();
            _aimController = GetComponent<RangedAimController>();
            _bowAnimController = GetComponent<BowAnimationController>();

            // Refactored Sub-Systems (SRP) - auto-add if missing
            _attackExecutor = GetComponent<AttackExecutor>();
            if (_attackExecutor == null)
            {
                _attackExecutor = gameObject.AddComponent<AttackExecutor>();
                DLog.LogWarning("[PlayerCombat] AttackExecutor was missing - auto-added. Add it to the prefab.");
            }

            _comboSystem = GetComponent<PlayerComboSystem>();
            if (_comboSystem == null)
            {
                _comboSystem = gameObject.AddComponent<PlayerComboSystem>();
                DLog.LogWarning("[PlayerCombat] PlayerComboSystem was missing - auto-added. Add it to the prefab.");
            }

            _staminaSystem = GetComponent<PlayerStaminaSystem>();
            if (_staminaSystem == null)
            {
                _staminaSystem = gameObject.AddComponent<PlayerStaminaSystem>();
                DLog.LogWarning("[PlayerCombat] PlayerStaminaSystem was missing - auto-added. Add it to the prefab.");
            }

            // CombatStateMachine - tek kaynak state tracking
            _stateMachine = GetComponent<CombatStateMachine>();
            if (_stateMachine == null)
            {
                _stateMachine = gameObject.AddComponent<CombatStateMachine>();
                DLog.LogWarning("[PlayerCombat] CombatStateMachine was missing - auto-added. Add it to the prefab.");
            }

            _animEvents = GetComponent<PlayerNetworkAnimEvents>();

            // Advanced Combat Systems
            _poiseSystem = GetComponent<PoiseSystem>();
            _hyperArmorWindow = GetComponent<HyperArmorWindow>();
            _hitReactionSystem = GetComponent<HitReactionSystem>();
            _guardBreakSystem = GetComponent<GuardBreakSystem>();
            _backstabSystem = GetComponent<BackstabRiposteSystem>();

            // BowAnimationController yoksa ekle
            if (_bowAnimController == null)
            {
                _bowAnimController = gameObject.AddComponent<BowAnimationController>();
            }

            // Initialize health and poise (stamina delegated to StaminaSystem)
            _currentHealth = stats.MaxHealth;
            _currentPoise = maxPoise;

            // Animator parametrelerini cache'le
            CacheAnimatorParameters();

            // Başlangıçta WeaponCategory'yi Unarmed (0) yap
            if (_animator != null && HasAnimatorParameter("WeaponCategory"))
            {
                _animator.SetInteger(AnimatorHash.WeaponCategory, 0);
                DLog.Log("[PlayerCombat] WeaponCategory initialized to 0 (Unarmed)");
            }

            // CombatSettings'ten değerleri yükle
            LoadCombatSettings();
        }

        private void OnEnable()
        {
            if (_attackExecutor != null)
                _attackExecutor.OnAttackEnded += HandleAttackEnded;
        }

        private void OnDisable()
        {
            if (_attackExecutor != null)
                _attackExecutor.OnAttackEnded -= HandleAttackEnded;
        }

        private void HandleAttackEnded()
        {
            SetState(CombatState.Idle);
        }

        /// <summary>
        /// CombatSettings'ten değerleri yükle
        /// </summary>
        private void LoadCombatSettings()
        {
            var settings = CombatSettings.Instance;
            if (settings == null) return;

            // Stamina settings
            staminaRegenRate = settings.StaminaRegenRate;
            staminaRegenDelay = settings.StaminaRegenDelay;

            // Dodge settings
            dodgeDistance = settings.DodgeDistance;
            dodgeDuration = settings.DodgeDuration;
            dodgeIFrameStart = settings.DodgeIFrameStart;
            dodgeIFrameDuration = settings.DodgeIFrameDuration;
            dodgeStamina = settings.DodgeStamina;

            // Parry settings (difficulty-based)
            perfectParryWindow = settings.GetParryWindow();

            // Block settings
            blockDamageReduction = settings.BlockDamageReduction;

            DLog.Log($"[PlayerCombat] Loaded CombatSettings - ParryWindow: {perfectParryWindow}s, DodgeDuration: {dodgeDuration}s, StaminaRegen: {staminaRegenRate}/s");
        }

        private void CacheAnimatorParameters()
        {
            _animatorParameters = new HashSet<string>();
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var param in _animator.parameters)
                {
                    _animatorParameters.Add(param.name);
                }
            }
        }

        private bool HasAnimatorParameter(string paramName)
        {
            return _animatorParameters != null && _animatorParameters.Contains(paramName);
        }

        private void Update()
        {
            UpdateTimers();
            UpdatePoiseRegen();
            _attackExecutor?.UpdateHyperArmor(CurrentCombatState);

            if (CurrentCombatState == CombatState.Staggered && Time.time >= _staggerEndTime)
            {
                SetState(CombatState.Idle);
            }

            // Fallback: Reset attack state if animation event didn't trigger
            if (CurrentCombatState == CombatState.Attacking)
            {
                if (_attackExecutor != null && _attackExecutor.IsFallbackTimerExpired)
                {
                    HandleAttackEnded();
                }
                // Safety net: force reset if stuck in Attacking for too long (2 seconds max)
                else if (Time.time - _attackStateStartTime > 2f)
                {
                    DLog.LogWarning("[PlayerCombat] Attack state stuck for 2s - force resetting to Idle");
                    _attackExecutor?.CancelCurrentAttack();
                    HandleAttackEnded();
                }
            }
        }

        #region Combat Actions

        public void Attack(AttackType attackType)
        {
            // Network bypass — server'a RPC gönder
            if (_networkControlled && _networkCombatController != null)
            {
                _networkCombatController.ServerRequestAttack(new AttackRequest
                {
                    AttackType = attackType == AttackType.HeavyAttack ? 1 : (attackType == AttackType.ChargedAttack ? 2 : 0),
                    ComboIndex = _comboSystem?.CurrentCombo ?? 1,
                    AimDirection = transform.forward
                });
                return;
            }

            if (_currentWeapon != null && !_currentWeapon.CanAttack) return;

            // Stamina cost: Weapon data > StaminaSystem > Legacy fallback
            float staminaCost = _currentWeapon?.Data?.staminaCost ??
                (attackType == AttackType.HeavyAttack
                    ? (_staminaSystem?.HeavyAttackCost ?? heavyAttackStamina)
                    : (_staminaSystem?.LightAttackCost ?? lightAttackStamina));

            if (!CanAttackWithStamina(staminaCost)) return;

            // Recovery cancel (hit confirm)
            bool hitLanded = _comboSystem?.HitLandedThisAttack ?? false;
            if (CurrentCombatState == CombatState.Attacking && IsInRecoveryPhase && hitLanded)
            {
                _attackExecutor?.CancelCurrentAttack();
            }

            // Consume stamina
            if (_staminaSystem != null)
            {
                if (attackType == AttackType.LightAttack)
                    _staminaSystem.ConsumeLightAttack();
                else if (attackType == AttackType.HeavyAttack)
                    _staminaSystem.ConsumeHeavyAttack();
                else
                    _staminaSystem.Consume(staminaCost);
            }
            else
            {
                ConsumeStamina(staminaCost);
            }

            SetState(CombatState.Attacking);

            // Delegate to AttackExecutor
            int comboIndex = _attackExecutor?.ExecuteAttack(attackType, _currentWeapon) ?? 1;

            // Event (stays in orchestrator)
            EventBus.Publish(new AttackStartedEvent
            {
                Attacker = gameObject,
                AttackType = attackType,
                ComboIndex = comboIndex
            });
        }

        public void Dodge(Vector3 direction)
        {
            // Network bypass
            if (_networkControlled && _networkCombatController != null)
            {
                _networkCombatController.ServerRequestDodge(new DefenseRequest
                {
                    Type = DefenseType.Dodge,
                    DodgeDirection = direction
                });
                return;
            }

            // DefenseSystem varsa ona yönlendir
            if (_defenseSystem != null)
            {
                if (!CanDodge) return;

                // Recovery fazında dodge yapıyorsak, saldırıyı cancel et
                if (CurrentCombatState == CombatState.Attacking)
                {
                    _attackExecutor?.CancelCurrentAttack();
                    DLog.Log("[PlayerCombat] Recovery dodge - attack canceled!");
                }

                // Consume stamina (delegated to StaminaSystem)
                if (_staminaSystem != null)
                    _staminaSystem.ConsumeDodge();
                else
                    ConsumeStamina(dodgeStamina);

                _defenseSystem.TryDodge(direction);

                EventBus.Publish(new DodgePerformedEvent
                {
                    Character = gameObject,
                    Direction = direction,
                    IsInvincible = true
                });
                return;
            }

            // Legacy dodge
            if (!CanDodge) return;

            // Recovery fazında dodge yapıyorsak, saldırıyı cancel et
            if (CurrentCombatState == CombatState.Attacking)
            {
                _attackExecutor?.CancelCurrentAttack();
                DLog.Log("[PlayerCombat] Recovery dodge - attack canceled!");
            }

            // Consume stamina (delegated to StaminaSystem)
            if (_staminaSystem != null)
                _staminaSystem.ConsumeDodge();
            else
                ConsumeStamina(dodgeStamina);

            SetState(CombatState.Dodging);

            StartCoroutine(DodgeCoroutine(direction.normalized));

            EventBus.Publish(new DodgePerformedEvent
            {
                Character = gameObject,
                Direction = direction,
                IsInvincible = true
            });
        }

        public void Block(bool isBlocking)
        {
            // Network bypass
            if (_networkControlled && _networkCombatController != null)
            {
                _networkCombatController.ServerRequestBlock(isBlocking);
                return;
            }

            // DefenseSystem varsa ona yönlendir
            if (_defenseSystem != null)
            {
                _defenseSystem.SetBlock(isBlocking);
                return;
            }

            // Legacy block logic
            if (isBlocking)
            {
                if (!CanBlock) return;

                // Saldırı sırasında block yapıyorsak, saldırıyı cancel et
                if (CurrentCombatState == CombatState.Attacking)
                {
                    _attackExecutor?.CancelCurrentAttack();
                    DLog.Log("[PlayerCombat] Instant block - attack canceled!");
                }

                _isBlocking = true;
                _blockStartTime = Time.time;
                SetState(CombatState.Blocking);
                _animator.SetBool(AnimatorHash.IsBlocking, true);
            }
            else
            {
                _isBlocking = false;
                _animator.SetBool(AnimatorHash.IsBlocking, false);

                // Only set to Idle if currently blocking (don't interrupt other states like Dodging)
                if (CurrentCombatState == CombatState.Blocking)
                {
                    SetState(CombatState.Idle);
                }
            }
        }

        /// <summary>
        /// Parry girişimi (tap input)
        /// </summary>
        public void Parry()
        {
            // Network bypass
            if (_networkControlled && _networkCombatController != null)
            {
                _networkCombatController.ServerRequestParry(new DefenseRequest
                {
                    Type = DefenseType.Parry
                });
                return;
            }

            if (_defenseSystem != null)
            {
                _defenseSystem.TryParry();
            }
            else
            {
                // Legacy: Block ile aynı, tap olarak kullan
                _isBlocking = true;
                _blockStartTime = Time.time;
                SetState(CombatState.Blocking);
                if (_animEvents != null) _animEvents.PlayParry(); else _animator.SetTrigger(AnimatorHash.Parry);
            }
        }

        #endregion

        #region Ranged Combat

        /// <summary>
        /// Yay equipli mi?
        /// </summary>
        public bool IsRangedWeaponEquipped
        {
            get
            {
                return _currentWeapon != null &&
                       _currentWeapon.WeaponType == WeaponType.Bow;
            }
        }

        /// <summary>
        /// Nişan almaya başla (yay çek)
        /// </summary>
        public void StartAim()
        {
            if (!IsRangedWeaponEquipped)
            {
                DLog.Log("[PlayerCombat] Cannot aim - no ranged weapon equipped");
                return;
            }

            if (CurrentCombatState != CombatState.Idle && CurrentCombatState != CombatState.Attacking)
            {
                return;
            }

            // BowWeapon component'i al veya cache'den kullan
            if (_currentBow == null && _currentWeapon != null)
            {
                _currentBow = _currentWeapon.GetComponent<Items.BowWeapon>();
                _currentBow?.Initialize(this);

                // BowAnimationController'a bağla - bu kritik!
                // SetWeapon'da yapılmamış olabilir (WeaponType kontrolü nedeniyle)
                if (_currentBow != null && _bowAnimController != null)
                {
                    DLog.Log("[PlayerCombat] Late binding: Connecting BowAnimationController to BowWeapon");
                    _bowAnimController.SetBowWeapon(_currentBow);
                }
            }

            if (_currentBow == null)
            {
                DLog.LogWarning("[PlayerCombat] No BowWeapon component on equipped bow!");
                return;
            }

            // Yay çekmeye başla
            if (_currentBow.StartDraw())
            {
                _aimController?.StartAiming();
                SetState(CombatState.Attacking);

                DLog.Log("[PlayerCombat] Started aiming");
            }
        }

        /// <summary>
        /// Nişan güncelle (her frame çağrılır)
        /// </summary>
        public void UpdateAim()
        {
            if (_currentBow == null || !_currentBow.IsDrawing) return;

            // BowWeapon.Update() zaten draw progress'i güncelliyor
            // Burada sadece aim controller'a bildir
            _aimController?.UpdateDrawProgress(_currentBow.DrawProgress);
        }

        /// <summary>
        /// Nişanı bırak ve ateş et
        /// </summary>
        public void ReleaseAim()
        {
            if (_currentBow == null) return;

            _currentBow.Release();
            _aimController?.StopAiming();
            _aimController?.UpdateDrawProgress(0f);

            // Crosshair punch efekti
            _aimController?.PunchCrosshair(1.3f);

            SetState(CombatState.Idle);

            DLog.Log("[PlayerCombat] Released aim - arrow shot");
        }

        /// <summary>
        /// Nişan almayı iptal et (ateşlemeden)
        /// </summary>
        public void CancelAim()
        {
            if (_currentBow == null) return;

            _currentBow.CancelDraw();
            _aimController?.StopAiming();
            _aimController?.UpdateDrawProgress(0f);

            SetState(CombatState.Idle);

            DLog.Log("[PlayerCombat] Aim canceled");
        }

        /// <summary>
        /// Mevcut BowWeapon'ı al
        /// </summary>
        public Items.BowWeapon GetCurrentBow()
        {
            return _currentBow;
        }

        #endregion

        #region Damage & Defense

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (!IsAlive) return;
            if (_isInvincible) return;

            int finalDamage = damageInfo.FinalDamage;

            // DefenseSystem varsa ona yönlendir
            if (_defenseSystem != null)
            {
                var result = _defenseSystem.ProcessIncomingDamage(damageInfo);
                finalDamage = result.FinalDamage;

                if (result.WasParried)
                {
                    damageInfo.IsParried = true;
                    // Parry slow-mo DefenseSystem tarafından uygulanıyor
                    return; // Parry başarılı, hasar yok
                }

                if (result.WasDodged)
                {
                    return; // Dodge ile kaçınıldı
                }

                if (result.WasBlocked)
                {
                    damageInfo.IsBlocked = true;
                    // Block'ta %100 hasar engelleme

                    // BlockHit animasyonu
                    if (_animEvents != null)
                        _animEvents.PlayBlockHit();
                    else if (_animator != null)
                        _animator.SetTrigger(AnimatorHash.BlockHit);
                }
            }
            else
            {
                // Legacy block logic
                if (_isBlocking)
                {
                    float timeSinceBlock = Time.time - _blockStartTime;

                    if (timeSinceBlock <= perfectParryWindow)
                    {
                        // Perfect Parry
                        damageInfo.IsParried = true;
                        finalDamage = 0;

                        // Parry stamina cost (azaltılmış)
                        if (_staminaSystem != null)
                            _staminaSystem.ConsumeBlockHit(0); // Parry minimal cost
                        else
                            ConsumeStamina(blockStaminaDrain * 0.5f);

                        // Slow-mo efekti
                        CombatTimeManager.Instance?.ApplyParrySlowMo();

                        EventBus.Publish(new BlockPerformedEvent
                        {
                            Blocker = gameObject,
                            Attacker = damageInfo.Attacker,
                            DamageBlocked = damageInfo.FinalDamage,
                            IsPerfectParry = true
                        });

                        // Stagger attacker
                        if (damageInfo.Attacker != null && damageInfo.Attacker.TryGetComponent<ICombatant>(out var attacker))
                        {
                            attacker.ApplyStagger(1f);
                        }

                        if (_animEvents != null) _animEvents.PlayParry(); else _animator.SetTrigger(AnimatorHash.Parry);
                        return;
                    }
                    else
                    {
                        // Normal Block - %100 hasar engelleme
                        damageInfo.IsBlocked = true;
                        finalDamage = Mathf.RoundToInt(finalDamage * (1f - blockDamageReduction));

                        // Block stamina cost
                        if (_staminaSystem != null)
                            _staminaSystem.ConsumeBlockHit(damageInfo.StaggerValue);
                        else
                            ConsumeStamina(blockStaminaDrain + damageInfo.StaggerValue);

                        EventBus.Publish(new BlockPerformedEvent
                        {
                            Blocker = gameObject,
                            Attacker = damageInfo.Attacker,
                            DamageBlocked = damageInfo.FinalDamage - finalDamage,
                            IsPerfectParry = false
                        });

                        // BlockHit trigger'ı varsa kullan
                        if (_animator != null && HasAnimatorParameter("BlockHit"))
                        {
                            if (_animEvents != null) _animEvents.PlayBlockHit(); else _animator.SetTrigger(AnimatorHash.BlockHit);
                        }
                    }
                }
            }

            // Apply damage
            _currentHealth -= finalDamage;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            // StyleSystem'e hasar bildir
            StyleSystem.Instance?.OnDamageTaken(finalDamage);

            // Poise System entegrasyonu
            HitReaction hitReaction = HitReaction.None;
            if (_poiseSystem != null && !damageInfo.IgnoresPoise)
            {
                // Poise damage hesapla (stagger değeri veya hasar * 0.5)
                float poiseDamage = damageInfo.PoiseDamage > 0 ? damageInfo.PoiseDamage : damageInfo.StaggerValue;
                hitReaction = _poiseSystem.TakePoiseDamage(poiseDamage);
            }
            else
            {
                // Legacy poise (yeni sistem yoksa)
                _currentPoise -= damageInfo.StaggerValue;
                if (_currentPoise <= 0)
                {
                    hitReaction = HitReaction.Stagger;
                    _currentPoise = maxPoise;
                }
            }

            // Hit Reaction uygula
            if (_hitReactionSystem != null && hitReaction != HitReaction.None)
            {
                _hitReactionSystem.ApplyHitReactionFromDamage(damageInfo, hitReaction);
            }

            // Event
            EventBus.Publish(new AttackLandedEvent
            {
                Attacker = damageInfo.Attacker,
                Target = gameObject,
                Damage = finalDamage,
                IsCritical = damageInfo.IsCritical,
                IsStaggering = hitReaction >= HitReaction.Stagger,
                HitPoint = damageInfo.HitPoint
            });

            if (!IsAlive)
            {
                Die();
            }
            else if (!_isBlocking && finalDamage > 0 && hitReaction == HitReaction.None)
            {
                // Sadece poise absorb edildiyse hit animation
                if (_animEvents != null) _animEvents.PlayHit(); else _animator.SetTrigger(AnimatorHash.Hit);
            }
        }

        public void TakeDamage(int damage, ElementType element = ElementType.Physical)
        {
            TakeDamage(DamageInfo.Create(null, damage, element));
        }

        public void Heal(int amount)
        {
            _currentHealth = Mathf.Min(_currentHealth + amount, stats.MaxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void RestoreStamina(float amount)
        {
            if (_staminaSystem != null)
            {
                _staminaSystem.Restore(amount);
            }
        }

        public void Die()
        {
            SetState(CombatState.Dead);
            if (_animEvents != null) _animEvents.PlayDeath(); else _animator.SetTrigger(AnimatorHash.Death);

            EventBus.Publish(new CharacterDiedEvent
            {
                Character = gameObject,
                Killer = null,
                IsEnemy = false
            });
        }

        public void ApplyStagger(float duration)
        {
            SetState(CombatState.Staggered);
            _staggerEndTime = Time.time + duration;
            if (_animEvents != null) _animEvents.PlayStagger(); else _animator.SetTrigger(AnimatorHash.Stagger);

            EventBus.Publish(new StaggerEvent
            {
                Target = gameObject,
                StaggerDuration = duration
            });
        }

        public void OnHitLanded(GameObject target, DamageInfo damageInfo)
        {
            // Called when player's attack hits a target
            // Hasar verildiğinde ComboSystem'e bildir (combo continuation için)
            _comboSystem?.OnHitLanded();

            DLog.Log($"[PlayerCombat] Hit landed on {target.name}! Hit confirm active for combo.");
        }

        #endregion

        #region Private Methods

        private void SetState(CombatState newState)
        {
            // State Machine yoksa veya henüz başlatılmamışsa erken çık
            if (_stateMachine == null) return;

            // Aynı state'e geçiş yok
            if (CurrentCombatState == newState) return;

            // CombatStateMachine üzerinden state değiştir
            bool changed = _stateMachine.TryChangeState(newState);

            if (changed)
            {
                if (newState == CombatState.Attacking)
                    _attackStateStartTime = Time.time;

                // PlayerCombat'ın kendi event'ini de tetikle (geriye uyumluluk)
                OnCombatStateChanged?.Invoke(newState);
            }
        }


        /// <summary>
        /// Stamina tüket (legacy - StaminaSystem varsa ona yönlendirilir)
        /// </summary>
        public void ConsumeStamina(float amount)
        {
            if (_staminaSystem != null)
            {
                _staminaSystem.Consume(amount);
                return;
            }

            // Legacy fallback (StaminaSystem yoksa)
            // Bu kod geriye uyumluluk için bırakıldı
            DLog.LogWarning("[PlayerCombat] Using legacy stamina consumption - consider adding PlayerStaminaSystem component");
        }

        private void UpdatePoiseRegen()
        {
            if (_currentPoise < maxPoise && CurrentCombatState == CombatState.Idle)
            {
                _currentPoise = Mathf.Min(maxPoise, _currentPoise + poiseRegenRate * Time.deltaTime);
            }
        }

        // NOTE: UpdateComboTimer moved to PlayerComboSystem

        private void UpdateTimers()
        {
            // General timer updates
        }

        private IEnumerator DodgeCoroutine(Vector3 direction)
        {
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + direction * dodgeDistance;

            if (_animEvents != null) _animEvents.PlayDodge(); else _animator.SetTrigger(AnimatorHash.Dodge);

            // Dodge dust efekti
            ImpactDustVFX.Instance?.SpawnDodgeDust(startPos, direction);

            while (elapsed < dodgeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dodgeDuration;

                // i-frames
                _isInvincible = (elapsed >= dodgeIFrameStart && elapsed <= dodgeIFrameStart + dodgeIFrameDuration);

                // Movement
                Vector3 newPos = Vector3.Lerp(startPos, endPos, t);
                _controller.Move(newPos - transform.position);

                yield return null;
            }

            _isInvincible = false;
            SetState(CombatState.Idle);
        }


        #endregion

        // Animation Events (OnAttackStart, OnAttackHitboxEnable/Disable, OnAttackEnd)
        // are now handled by AttackExecutor component on the same GameObject.

        #region Critical Attacks (Backstab/Riposte)

        /// <summary>
        /// Backstab dene - düşmanın arkasındayken
        /// </summary>
        public bool TryBackstab()
        {
            if (_backstabSystem == null) return false;
            if (CurrentCombatState != CombatState.Idle) return false;

            return _backstabSystem.TryBackstab();
        }

        /// <summary>
        /// Riposte dene - parry sonrası
        /// </summary>
        public bool TryRiposte()
        {
            if (_backstabSystem == null) return false;
            if (CurrentCombatState != CombatState.Idle) return false;

            return _backstabSystem.TryRiposte();
        }

        /// <summary>
        /// Backstab yapılabilir mi?
        /// </summary>
        public bool CanBackstab => _backstabSystem?.CanBackstab ?? false;

        /// <summary>
        /// Riposte yapılabilir mi?
        /// </summary>
        public bool CanRiposte => _backstabSystem?.CanRiposte ?? false;

        #endregion

        #region Weapon Management

        /// <summary>
        /// Silah ata
        /// </summary>
        public void SetWeapon(Weapon weapon)
        {
            _currentWeapon = weapon;
            _attackExecutor?.SetCurrentWeapon(weapon);
            weapon?.Initialize(this);

            // BowWeapon kontrolü - daha detaylı debug
            _currentBow = null;
            if (weapon != null)
            {
                DLog.Log($"[PlayerCombat] SetWeapon - WeaponType: {weapon.WeaponType}, checking for BowWeapon component...");

                // WeaponType.Bow kontrolü VEYA direkt BowWeapon component kontrolü
                // Bazı silahlar WeaponType ayarlanmamış olabilir
                var bowComponent = weapon.GetComponent<Items.BowWeapon>();
                if (bowComponent != null)
                {
                    DLog.Log("[PlayerCombat] Found BowWeapon component on weapon!");
                    _currentBow = bowComponent;
                    _currentBow.Initialize(this);
                }
                else if (weapon.WeaponType == WeaponType.Bow)
                {
                    DLog.LogWarning("[PlayerCombat] WeaponType is Bow but no BowWeapon component found!");
                }
            }

            // BowAnimationController'ı güncelle
            DLog.Log($"[PlayerCombat] BowAnimController: {(_bowAnimController != null ? "OK" : "NULL")}, _currentBow: {(_currentBow != null ? "OK" : "NULL")}");

            if (_bowAnimController == null)
            {
                DLog.LogWarning("[PlayerCombat] BowAnimationController is NULL! Adding it now...");
                _bowAnimController = gameObject.AddComponent<BowAnimationController>();
            }

            if (_bowAnimController != null)
            {
                if (_currentBow != null)
                {
                    DLog.Log("[PlayerCombat] Calling SetBowWeapon...");
                    _bowAnimController.SetBowWeapon(_currentBow);
                }
                else
                {
                    _bowAnimController.SetBowEquipped(false);
                }
            }

            // Animator'da silah kategorisini güncelle
            UpdateWeaponAnimationCategory();

            DLog.Log($"[PlayerCombat] Weapon set: {weapon?.Data?.displayName ?? "null"}, IsBow: {_currentBow != null}");
        }

        /// <summary>
        /// Animator'daki WeaponCategory parametresini ve layer weight'lerini güncelle
        /// </summary>
        private void UpdateWeaponAnimationCategory()
        {
            if (_animator == null) return;

            int categoryValue = 0; // Default: Unarmed

            if (_currentWeapon != null && _currentWeapon.Data != null)
            {
                var category = _currentWeapon.Data.GetAnimationCategory();
                categoryValue = (int)category;
            }

            if (HasAnimatorParameter("WeaponCategory"))
            {
                _animator.SetInteger(AnimatorHash.WeaponCategory, categoryValue);
            }

            // Layer weight'lerini güncelle
            UpdateCombatLayerWeights((WeaponAnimationCategory)categoryValue);

            DLog.Log($"[PlayerCombat] WeaponCategory set to {categoryValue} ({(WeaponAnimationCategory)categoryValue})");
        }

        /// <summary>
        /// Silah kategorisine göre combat layer weight'lerini ayarla.
        /// Melee silahlar Base Layer + WeaponCategory parametresi kullanır.
        /// Sadece Bow için ayrı layer var.
        /// </summary>
        private void UpdateCombatLayerWeights(WeaponAnimationCategory category)
        {
            if (_animator == null) return;

            // Sadece Bow layer'ını kontrol et - melee silahlar Base Layer kullanır
            int combatBowIndex = AnimatorLayerHelper.GetLayerIndex(_animator, "Combat_Bow");

            // Bow layer'ı kapat (varsayılan)
            if (combatBowIndex >= 0) _animator.SetLayerWeight(combatBowIndex, 0f);

            // Sadece Ranged için Bow layer'ı aç
            if (category == WeaponAnimationCategory.Ranged)
            {
                if (combatBowIndex >= 0)
                {
                    _animator.SetLayerWeight(combatBowIndex, 1f);
                    DLog.Log("[PlayerCombat] Combat_Bow layer enabled");
                }
            }

            // Melee silahlar (OneHanded, TwoHanded, Polearm, Unarmed):
            // Base Layer'daki WeaponCategory parametresi ile animasyon seçilir
            // LightAttack trigger'ı tetiklendiğinde WeaponCategory'ye göre doğru animasyon oynar
        }



        /// <summary>
        /// IWeapon interface ile silah ata (geriye uyumluluk)
        /// </summary>
        public void SetWeapon(IWeapon weapon)
        {
            // Null kontrolü - silah bırakıldığında
            if (weapon == null)
            {
                _currentWeapon = null;
                _currentBow = null;
                _attackExecutor?.SetCurrentWeapon(null);
                UpdateWeaponAnimationCategory(); // WeaponCategory'yi 0 (Unarmed) yap
                DLog.Log("[PlayerCombat] Weapon unequipped, reset to Unarmed");
                return;
            }

            if (weapon is Weapon w)
            {
                SetWeapon(w);
            }
            else
            {
                DLog.LogWarning("[PlayerCombat] SetWeapon called with non-Weapon IWeapon. Use Weapon class instead.");
            }
        }

        public void EquipWeapon(IWeapon weapon)
        {
            SetWeapon(weapon);
        }

        public void UnequipWeapon()
        {
            _currentWeapon = null;
            UpdateWeaponAnimationCategory(); // Reset to Unarmed
        }

        public Weapon GetCurrentWeapon()
        {
            return _currentWeapon;
        }

        #endregion
    }
}
