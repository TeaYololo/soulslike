// ============================================================================
// Souls-like Combat System
// Open Source Project: https://github.com/AhmetKardesCan/soulslike-combat
// License: MIT
// Author: Toprak Eren Akpınar
//
// DefenseSystem.cs — Handles blocking, parrying, and defensive mechanics
// ============================================================================

using Dungeons.Utilities;
using UnityEngine;
using System.Collections;
using Dungeons.Core;
using Dungeons.Character;
using Dungeons.Data;
using Dungeons.Data.Interfaces;
using Dungeons.Player;

namespace Dungeons.Combat
{
    /// <summary>
    /// Akışkan savunma sistemi: Block, Parry, Dodge.
    /// - Block: %100 hasar azaltma, basılı tutulur
    /// - Parry: Geniş pencere (~300ms), başarılı = slow-mo + counter fırsatı
    /// - Dodge: i-frame'li, saldırı başlatılabilir
    /// </summary>
    public class DefenseSystem : MonoBehaviour
    {
        [Header("Block Settings")]
        [Tooltip("Block hasar azaltma oranı (0.7 = %70) - Balance güncellendi")]
        [SerializeField] private float blockDamageReduction = 0.7f; // %70 hasar engelleme (eskiden %100)
        [Tooltip("Block stamina maliyeti (saniye başına)")]
        [SerializeField] private float blockStaminaDrainPerSecond = 10f;
        [Tooltip("Saldırı block edildiğinde ekstra stamina maliyeti")]
        [SerializeField] private float blockHitStaminaCost = 15f;
        [Tooltip("Block sırasında gelen hasar stamina'ya da zarar verir (1.5x)")]
        [SerializeField] private float blockStaminaDamageRatio = 1.5f;

        [Header("Parry Settings")]
        [Tooltip("Parry pencere süresi - CombatSettings'ten alınır")]
        [SerializeField] private float parryWindow = 0.18f; // 180ms - Normal difficulty (eskiden 300ms)
        [Tooltip("Parry tuşuna basıldığında bu süre boyunca parry aktif")]
        [SerializeField] private float parryActiveDuration = 0.25f;
        [Tooltip("Başarılı parry sonrası counter attack süresi")]
        [SerializeField] private float counterAttackWindow = 0.8f;
        [Tooltip("Başarısız parry sonrası hasar çarpanı (1.0 = normal hasar)")]
        [SerializeField] private float failedParryDamageMultiplier = 1.0f;

        [Header("Dodge Settings")]
        [Tooltip("Dodge stamina maliyeti")]
        [SerializeField] private float dodgeStaminaCost = 20f;
        [Tooltip("Dodge mesafesi")]
        [SerializeField] private float dodgeDistance = 4f;
        [Tooltip("Dodge süresi")]
        [SerializeField] private float dodgeDuration = 0.5f;
        [Tooltip("i-frame başlangıcı")]
        [SerializeField] private float dodgeIFrameStart = 0.05f;
        [Tooltip("i-frame süresi - Equip load'a göre değişebilir")]
        [SerializeField] private float dodgeIFrameDuration = 0.33f; // Medium roll default
        [Tooltip("Perfect dodge penceresi - Balance güncellendi (80ms)")]
        [SerializeField] private float perfectDodgeWindow = 0.08f; // 80ms (eskiden 150ms)
        [Tooltip("Dodge sırasında saldırı başlatılabilir mi")]
        [SerializeField] private bool canAttackDuringDodge = true;
        [Tooltip("Dodge attack başlayabileceği zaman (dodge'un %'si)")]
        [SerializeField, Range(0.3f, 0.8f)] private float dodgeAttackWindowStart = 0.5f;

        [Header("Input Buffer")]
        [Tooltip("Input buffer süresi (kısa = responsive)")]
        [SerializeField] private float inputBufferDuration = 0.1f; // Kısa buffer = responsive

        [Header("Attack Lerp (Aim Assist)")]
        [Tooltip("Saldırı sırasında hedefe yönelme açısı")]
        [SerializeField] private float attackLerpAngle = 45f;
        [Tooltip("Saldırı sırasında hedefe yönelme mesafesi")]
        [SerializeField] private float attackLerpDistance = 3f;

        // Components
        private CharacterController _controller;
        private Animator _animator;
        private PlayerCombat _combat;
        private LockOnSystem _lockOn;
        private GuardBreakSystem _guardBreakSystem;
        private PlayerNetworkAnimEvents _animEvents;
        private PlayerComboSystem _comboSystem;
        private AttackExecutor _attackExecutor;

        // Network bypass
        private bool _networkControlled;
        private NetworkCombatController _networkCombatController;

        /// <summary>
        /// Network modunda defense action'ları NetworkCombatController'a yönlendir.
        /// </summary>
        public void SetNetworkControlled(bool controlled)
        {
            _networkControlled = controlled;
            if (controlled)
                _networkCombatController = GetComponent<NetworkCombatController>();
        }

        // State
        private DefenseState _currentState = DefenseState.None;
        private float _stateStartTime;
        private float _parryEndTime;
        private float _counterAttackEndTime;
        private bool _isInvincible;
        private bool _perfectDodgeTriggered;
        private Vector3 _dodgeDirection;

        // Static buffer for Physics NonAlloc (GC optimization)
        private static readonly Collider[] _overlapBuffer = new Collider[16];
        private Coroutine _dodgeCoroutine;

        // Input buffer
        private float _lastParryInputTime = -10f;
        private float _lastDodgeInputTime = -10f;
        private float _lastBlockInputTime = -10f;
        private bool _blockHeld;

        // Properties
        public DefenseState CurrentState => _currentState;
        public bool IsBlocking => _currentState == DefenseState.Blocking;
        public bool IsParrying => _currentState == DefenseState.Parrying && Time.time < _parryEndTime;
        public bool IsDodging => _currentState == DefenseState.Dodging;
        public bool IsInvincible => _isInvincible;
        public bool CanCounterAttack => Time.time < _counterAttackEndTime;
        public bool CanAttackDuringDodge => canAttackDuringDodge && IsDodging && GetDodgeProgress() >= dodgeAttackWindowStart;
        public float ParryWindow => parryWindow;

        // Events
        public System.Action OnBlockStart;
        public System.Action OnBlockEnd;
        public System.Action<bool> OnParryAttempt; // bool = success
        public System.Action OnPerfectDodge;
        public System.Action OnCounterAttackReady;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
            _combat = GetComponent<PlayerCombat>();
            _lockOn = GetComponent<LockOnSystem>();
            _guardBreakSystem = GetComponent<GuardBreakSystem>();
            _animEvents = GetComponent<PlayerNetworkAnimEvents>();
            _comboSystem = GetComponent<PlayerComboSystem>();
            _attackExecutor = GetComponent<AttackExecutor>();
        }

        private void Start()
        {
            // CombatSettings'ten balance değerlerini al
            LoadCombatSettings();
        }

        /// <summary>
        /// CombatSettings'ten değerleri yükle
        /// </summary>
        private void LoadCombatSettings()
        {
            if (CombatSettings.Instance == null) return;

            var settings = CombatSettings.Instance;

            // Parry settings
            parryWindow = settings.GetParryWindow();
            parryActiveDuration = parryWindow + 0.07f; // Parry window + buffer

            // Block settings
            blockDamageReduction = settings.BlockDamageReduction;
            blockStaminaDamageRatio = settings.BlockStaminaDamageRatio;

            // Dodge settings
            dodgeStaminaCost = settings.DodgeStamina;
            dodgeDistance = settings.DodgeDistance;
            dodgeDuration = settings.DodgeDuration;
            dodgeIFrameStart = settings.DodgeIFrameStart;
            dodgeIFrameDuration = settings.DodgeIFrameDuration;
            perfectDodgeWindow = settings.PerfectDodgeWindow;

            DLog.Log($"[DefenseSystem] Loaded CombatSettings - Parry: {parryWindow * 1000}ms, PerfectDodge: {perfectDodgeWindow * 1000}ms, Block: {blockDamageReduction * 100}%, DodgeDist: {dodgeDistance}m");
        }

        private void Update()
        {
            UpdateState();
            ProcessInputBuffer();
        }

        #region Public Input Methods

        /// <summary>
        /// Block tuşuna basıldı/bırakıldı
        /// </summary>
        public void SetBlock(bool held)
        {
            // Network bypass
            if (_networkControlled && _networkCombatController != null)
            {
                _networkCombatController.ServerRequestBlock(held);
                return;
            }

            _blockHeld = held;

            if (held)
            {
                _lastBlockInputTime = Time.time;
                TryStartBlock();
            }
            else
            {
                EndBlock();
            }
        }

        /// <summary>
        /// Parry tuşuna basıldı (tap)
        /// </summary>
        public void TryParry()
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

            _lastParryInputTime = Time.time;

            if (CanParry())
            {
                StartParry();
            }
        }

        /// <summary>
        /// Dodge tuşuna basıldı
        /// </summary>
        public void TryDodge(Vector3 direction)
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

            _lastDodgeInputTime = Time.time;
            _dodgeDirection = direction.magnitude > 0.1f ? direction.normalized : -transform.forward;

            if (CanDodge())
            {
                StartDodge();
            }
        }

        #endregion

        #region Damage Processing

        /// <summary>
        /// Gelen hasarı savunma sisteminden geçir.
        /// Returns: İşlenmiş hasar miktarı
        /// </summary>
        public DamageResult ProcessIncomingDamage(DamageInfo damageInfo)
        {
            var result = new DamageResult
            {
                OriginalDamage = damageInfo.FinalDamage,
                FinalDamage = damageInfo.FinalDamage,
                WasBlocked = false,
                WasParried = false,
                WasDodged = false
            };

            // Invincible (dodge i-frames)
            if (_isInvincible)
            {
                result.FinalDamage = 0;
                result.WasDodged = true;

                // Perfect dodge kontrolü
                if (!_perfectDodgeTriggered && IsDodging)
                {
                    float dodgeElapsed = Time.time - _stateStartTime;
                    if (dodgeElapsed <= perfectDodgeWindow)
                    {
                        TriggerPerfectDodge();
                    }
                }

                return result;
            }

            // Parry kontrolü
            if (IsParrying)
            {
                result.FinalDamage = 0;
                result.WasParried = true;
                TriggerSuccessfulParry(damageInfo);
                return result;
            }

            // Block kontrolü
            if (IsBlocking)
            {
                // GuardBreakSystem varsa onu kullan
                if (_guardBreakSystem != null && _combat != null)
                {
                    var (finalDamage, staminaDamage, guardBroken) = _guardBreakSystem.ProcessBlockedDamage(
                        damageInfo.FinalDamage,
                        _combat.CurrentStamina
                    );

                    if (guardBroken)
                    {
                        // Guard break! Full damage
                        result.FinalDamage = damageInfo.FinalDamage;
                        result.WasBlocked = false; // Guard kırıldı
                        EndBlock(); // Block'ı bitir
                        return result;
                    }

                    result.FinalDamage = finalDamage;
                    result.WasBlocked = true;

                    // Stamina'dan da kes
                    // TODO: _combat.ConsumeStamina(staminaDamage) public yapılmalı
                }
                else
                {
                    // Fallback: Eski sistem
                    result.FinalDamage = Mathf.RoundToInt(damageInfo.FinalDamage * (1f - blockDamageReduction));
                    result.WasBlocked = true;
                }

                OnBlockHit(damageInfo);
                return result;
            }

            // Savunma yok - normal hasar
            return result;
        }

        #endregion

        #region State Management

        private void UpdateState()
        {
            switch (_currentState)
            {
                case DefenseState.Blocking:
                    UpdateBlocking();
                    break;

                case DefenseState.Parrying:
                    UpdateParrying();
                    break;

                case DefenseState.Dodging:
                    // Dodge coroutine ile yönetiliyor
                    break;
            }
        }

        private void ProcessInputBuffer()
        {
            // Parry buffer
            if (Time.time - _lastParryInputTime <= inputBufferDuration && CanParry())
            {
                StartParry();
                _lastParryInputTime = -10f;
            }

            // Dodge buffer
            if (Time.time - _lastDodgeInputTime <= inputBufferDuration && CanDodge())
            {
                StartDodge();
                _lastDodgeInputTime = -10f;
            }

            // Block buffer (held kontrolü)
            if (_blockHeld && _currentState == DefenseState.None)
            {
                TryStartBlock();
            }
        }

        private void SetState(DefenseState newState)
        {
            if (_currentState == newState) return;

            // Exit current state
            switch (_currentState)
            {
                case DefenseState.Blocking:
                    OnBlockEnd?.Invoke();
                    break;
            }

            _currentState = newState;
            _stateStartTime = Time.time;

            DLog.Log($"[DefenseSystem] State changed to: {newState}");
        }

        #endregion

        #region Block

        private bool CanBlock()
        {
            if (_currentState == DefenseState.Dodging) return false;
            if (_combat != null && _combat.CurrentCombatState == CombatState.Staggered) return false;
            return true;
        }

        private void TryStartBlock()
        {
            if (!CanBlock()) return;

            SetState(DefenseState.Blocking);
            _animator?.SetBool("IsBlocking", true);
            OnBlockStart?.Invoke();

            // Souls-like: Block yapınca combo sıfırlanır (commitment)
            _comboSystem?.ResetCombo();

            DLog.Log("[DefenseSystem] Block started - combo reset");
        }

        private void UpdateBlocking()
        {
            // Stamina drain
            if (_combat != null)
            {
                // TODO: Implement stamina drain
            }

            // Block bırakıldıysa
            if (!_blockHeld)
            {
                EndBlock();
            }
        }

        private void EndBlock()
        {
            if (_currentState != DefenseState.Blocking) return;

            SetState(DefenseState.None);
            _animator?.SetBool("IsBlocking", false);

            DLog.Log("[DefenseSystem] Block ended");
        }

        private void OnBlockHit(DamageInfo damageInfo)
        {
            // Block hit animation
            if (_animEvents != null) _animEvents.PlayBlockHit(); else _animator?.SetTrigger("BlockHit");

            // Stamina cost
            // TODO: Implement stamina cost

            DLog.Log($"[DefenseSystem] Blocked attack! Original damage: {damageInfo.FinalDamage}");
        }

        #endregion

        #region Parry

        private bool CanParry()
        {
            if (_currentState == DefenseState.Dodging) return false;
            if (_currentState == DefenseState.Parrying) return false;
            if (_combat != null && _combat.CurrentCombatState == CombatState.Staggered) return false;
            return true;
        }

        private void StartParry()
        {
            SetState(DefenseState.Parrying);
            _parryEndTime = Time.time + parryActiveDuration;

            if (_animEvents != null) _animEvents.PlayParry(); else _animator?.SetTrigger("Parry");

            DLog.Log($"[DefenseSystem] Parry started! Window: {parryWindow}s");
        }

        private void UpdateParrying()
        {
            // Parry süresi bitti
            if (Time.time >= _parryEndTime)
            {
                EndParry(false);
            }
        }

        private void EndParry(bool wasSuccessful)
        {
            SetState(DefenseState.None);
            OnParryAttempt?.Invoke(wasSuccessful);

            if (!wasSuccessful)
            {
                DLog.Log("[DefenseSystem] Parry ended without blocking an attack");
            }
        }

        private void TriggerSuccessfulParry(DamageInfo damageInfo)
        {
            DLog.Log("[DefenseSystem] SUCCESSFUL PARRY!");

            // Slow-mo efekti
            CombatTimeManager.Instance?.ApplyParrySlowMo();

            // Counter attack penceresi
            _counterAttackEndTime = Time.time + counterAttackWindow;
            OnCounterAttackReady?.Invoke();

            // Parry animation
            if (_animEvents != null) _animEvents.PlayParrySuccess(); else _animator?.SetTrigger("ParrySuccess");

            // Saldırganı stagger et
            if (damageInfo.Attacker != null)
            {
                var attackerCombat = damageInfo.Attacker.GetComponent<ICombatant>();
                attackerCombat?.ApplyStagger(0.8f);
            }

            // Event
            EventBus.Publish(new ParrySuccessEvent
            {
                Defender = gameObject,
                Attacker = damageInfo.Attacker,
                DamageBlocked = damageInfo.FinalDamage
            });

            // State reset
            SetState(DefenseState.None);
            OnParryAttempt?.Invoke(true);
        }

        #endregion

        #region Dodge

        private bool CanDodge()
        {
            if (_currentState == DefenseState.Dodging) return false;
            if (_combat != null)
            {
                if (_combat.CurrentCombatState == CombatState.Staggered) return false;
                if (_combat.CurrentStamina < dodgeStaminaCost) return false;
            }

            // Souls-like: Saldırı sırasında sadece Recovery phase'de dodge yapılabilir
            // Wind-Up ve Active phase'de dodge YASAK = commitment
            if (_attackExecutor != null && _attackExecutor.IsAttacking)
            {
                if (!_attackExecutor.CanDodgeCancelAttack())
                {
                    DLog.Log($"[DefenseSystem] Dodge BLOCKED - Attack phase: {_attackExecutor.CurrentPhase} (only Recovery/ComboWindow allows cancel)");
                    return false;
                }
            }

            return true;
        }

        private void StartDodge()
        {
            if (_dodgeCoroutine != null)
            {
                StopCoroutine(_dodgeCoroutine);
            }

            SetState(DefenseState.Dodging);
            _perfectDodgeTriggered = false;

            // Souls-like: Dodge yapınca combo sıfırlanır (commitment)
            _comboSystem?.ResetCombo();

            // Dodge başlamadan önce karakteri dodge yönüne döndür
            if (_dodgeDirection.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(_dodgeDirection);
            }

            // Stamina cost
            // TODO: Implement via PlayerCombat

            _dodgeCoroutine = StartCoroutine(DodgeCoroutine());

            DLog.Log($"[DefenseSystem] Dodge started - combo reset! Direction: {_dodgeDirection}");
        }

        private IEnumerator DodgeCoroutine()
        {
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + _dodgeDirection * dodgeDistance;

            if (_animEvents != null) _animEvents.PlayDodge(); else _animator?.SetTrigger("Dodge");

            while (elapsed < dodgeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dodgeDuration;

                // i-frames
                _isInvincible = (elapsed >= dodgeIFrameStart && elapsed <= dodgeIFrameStart + dodgeIFrameDuration);

                // Movement (eased)
                float easedT = EaseOutQuad(t);
                Vector3 targetPos = Vector3.Lerp(startPos, endPos, easedT);

                if (_controller != null)
                {
                    _controller.Move(targetPos - transform.position);
                }

                yield return null;
            }

            _isInvincible = false;
            SetState(DefenseState.None);
            _dodgeCoroutine = null;

            DLog.Log("[DefenseSystem] Dodge ended");
        }

        private float GetDodgeProgress()
        {
            if (_currentState != DefenseState.Dodging) return 0f;
            return (Time.time - _stateStartTime) / dodgeDuration;
        }

        private void TriggerPerfectDodge()
        {
            _perfectDodgeTriggered = true;

            DLog.Log("[DefenseSystem] PERFECT DODGE!");

            // Slow-mo efekti - Souls-like'da kapalı (CombatSettings'ten kontrol)
            if (CombatSettings.Instance != null && CombatSettings.Instance.EnablePerfectDodgeSlowmo)
            {
                CombatTimeManager.Instance?.ApplyDodgeSlowMo();
            }

            OnPerfectDodge?.Invoke();

            // Event
            EventBus.Publish(new PerfectDodgeEvent
            {
                Character = gameObject
            });
        }

        #endregion

        #region Attack Lerp (Aim Assist)

        /// <summary>
        /// Saldırı sırasında en yakın hedefe doğru yönelme
        /// </summary>
        public Vector3 GetAttackLerpDirection()
        {
            // Lock-on varsa ona bak
            if (_lockOn != null && _lockOn.CurrentTargetTransform != null)
            {
                Vector3 toTarget = _lockOn.CurrentTargetTransform.position - transform.position;
                toTarget.y = 0;
                return toTarget.normalized;
            }

            // Yakındaki düşmanları ara
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, attackLerpDistance, _overlapBuffer, LayerMask.GetMask("Enemy"));

            if (hitCount == 0)
                return transform.forward;

            // En yakın ve açı içinde olanı bul
            Transform bestTarget = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < hitCount; i++)
            {
                var col = _overlapBuffer[i];
                Vector3 toTarget = col.transform.position - transform.position;
                toTarget.y = 0;

                float distance = toTarget.magnitude;
                float angle = Vector3.Angle(transform.forward, toTarget);

                if (angle > attackLerpAngle)
                    continue;

                // Score: yakın ve önde olan tercih edilir
                float score = (attackLerpDistance - distance) + (attackLerpAngle - angle) * 0.1f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = col.transform;
                }
            }

            if (bestTarget != null)
            {
                Vector3 dir = bestTarget.position - transform.position;
                dir.y = 0;
                return dir.normalized;
            }

            return transform.forward;
        }

        /// <summary>
        /// Saldırı sırasında karakteri hedefe doğru döndür
        /// </summary>
        public void ApplyAttackLerp()
        {
            Vector3 targetDir = GetAttackLerpDirection();

            if (targetDir.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.5f);
            }
        }

        #endregion

        #region Utility

        private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        #endregion
    }

    public enum DefenseState
    {
        None,
        Blocking,
        Parrying,
        Dodging
    }

    public struct DamageResult
    {
        public int OriginalDamage;
        public int FinalDamage;
        public bool WasBlocked;
        public bool WasParried;
        public bool WasDodged;
    }

    // Events
    public struct ParrySuccessEvent
    {
        public GameObject Defender;
        public GameObject Attacker;
        public int DamageBlocked;
    }

    public struct PerfectDodgeEvent
    {
        public GameObject Character;
    }
}
