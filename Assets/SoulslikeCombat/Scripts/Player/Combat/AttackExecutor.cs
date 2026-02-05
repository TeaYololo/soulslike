// ============================================================================
// Souls-like Combat System
// Open Source Project: https://github.com/AhmetKardesCan/soulslike-combat
// License: MIT
// Author: Toprak Eren Akpınar
//
// AttackExecutor.cs — Handles attack execution, hitboxes, and damage dealing
// ============================================================================

using Dungeons.Utilities;
using UnityEngine;
using System.Collections;
using Dungeons.Core.Utilities;
using Dungeons.Data;
using Dungeons.Items;
using Dungeons.Player;
using Dungeons.Combat.Utility;

namespace Dungeons.Combat
{
    /// <summary>
    /// Saldırı fazları (Souls-like).
    /// Wind-Up → Active → Recovery → ComboWindow
    /// </summary>
    public enum AttackPhase
    {
        None,       // Saldırı yok
        WindUp,     // Hazırlık - iptal edilemez, hasar yok
        Active,     // Hitbox aktif - iptal edilemez
        Recovery,   // Hitbox kapalı - SADECE dodge ile cancel edilebilir
        ComboWindow // Input buffer açık
    }

    /// <summary>
    /// Saldırı yürütme motoru.
    /// Hitbox zamanlama, animasyon event'leri, attack canceling, hyper armor.
    /// PlayerCombat'tan ayrılmış - Single Responsibility.
    /// Souls-like attack phases: Wind-Up → Active → Recovery
    /// </summary>
    public class AttackExecutor : MonoBehaviour
    {
        [Header("Attack Timing")]
        [Tooltip("Fallback duration if no animation event")]
        [SerializeField] private float attackDuration = 0.4f;
        [Tooltip("When hitbox starts (relative to attack start)")]
        [SerializeField] private float hitboxEnableDelay = 0.1f;
        [Tooltip("How long hitbox stays active")]
        [SerializeField] private float hitboxActiveDuration = 0.3f;
        [Tooltip("Set true if animation events are configured")]
        [SerializeField] private bool useAnimationEvents = false;

        [Header("Animation")]
        [Tooltip("Saldırı animasyon hız çarpanı (1 = normal, 0.5 = yarı hız)")]
        [SerializeField, Range(0.3f, 2f)] private float attackAnimationSpeed = 0.8f;

        // State
        private float _attackStartTime;
        private float _attackEndTime;
        private Coroutine _hitboxCoroutine;

        // Attack Phase tracking (Souls-like)
        private AttackPhase _currentPhase = AttackPhase.None;
        private bool _hitboxActive = false;
        private AttackType _currentAttackType;

        // Components
        private Animator _animator;
        private PlayerComboSystem _comboSystem;
        private HyperArmorWindow _hyperArmorWindow;
        private Weapon _currentWeapon;
        private PlayerNetworkAnimEvents _animEvents;

        // Events
        public event System.Action OnAttackEnded;

        // Properties
        public float AttackAnimSpeed => attackAnimationSpeed;
        public AttackPhase CurrentPhase => _currentPhase;
        public bool IsAttacking => _currentPhase != AttackPhase.None;
        public bool IsHitboxActive => _hitboxActive;
        public AttackType CurrentAttackType => _currentAttackType;

        /// <summary>
        /// Souls-like: Sadece Recovery phase'de dodge ile cancel edilebilir.
        /// Wind-Up ve Active phase'de iptal yok = commitment.
        /// </summary>
        public bool CanDodgeCancelAttack()
        {
            // Recovery phase'de dodge cancel mümkün
            return _currentPhase == AttackPhase.Recovery || _currentPhase == AttackPhase.ComboWindow;
        }

        /// <summary>
        /// Hiçbir fazda iptal edilebilir mi? (stagger gibi durumlar için)
        /// </summary>
        public bool CanForceCancel()
        {
            // Sadece knockback+ gibi güçlü reaction'lar iptal edebilir
            return true;
        }

        /// <summary>
        /// Souls-like: Heavy attack'in Active phase'inde Hyper Armor aktif.
        /// Flinch ve Stagger ignore edilir, ama Knockback+ hâlâ etkili.
        /// </summary>
        public bool HasHyperArmor()
        {
            // Sadece Heavy Attack ve Active phase'de hyper armor
            return _currentAttackType == AttackType.HeavyAttack &&
                   _currentPhase == AttackPhase.Active;
        }

        /// <summary>
        /// Mevcut saldırının ilerleme yüzdesi (0-1)
        /// </summary>
        public float AttackProgress
        {
            get
            {
                float elapsed = Time.time - _attackStartTime;
                float adjustedDuration = attackDuration / attackAnimationSpeed;
                return Mathf.Clamp01(elapsed / adjustedDuration);
            }
        }

        /// <summary>
        /// Fallback timer süresi doldu mu?
        /// </summary>
        public bool IsFallbackTimerExpired => Time.time >= _attackEndTime;

        /// <summary>
        /// Mevcut saldırının toplam süresi
        /// </summary>
        public float CurrentAttackDuration => attackDuration / attackAnimationSpeed;

        /// <summary>
        /// Hyper armor aktif mi? (State Machine için property alias)
        /// </summary>
        public bool IsHyperArmorActive => HasHyperArmor();

        /// <summary>
        /// Hitbox'ı aç (State Machine için public method)
        /// </summary>
        public void EnableHitbox()
        {
            _currentWeapon?.EnableHitbox();
            _hitboxActive = true;
        }

        /// <summary>
        /// Hitbox'ı kapat (State Machine için public method)
        /// </summary>
        public void DisableHitbox()
        {
            _currentWeapon?.DisableHitbox();
            _hitboxActive = false;
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _comboSystem = GetComponent<PlayerComboSystem>();
            _hyperArmorWindow = GetComponent<HyperArmorWindow>();
            _animEvents = GetComponent<PlayerNetworkAnimEvents>();
        }

        /// <summary>
        /// Mevcut silahı güncelle
        /// </summary>
        public void SetCurrentWeapon(Weapon weapon)
        {
            _currentWeapon = weapon;
        }

        /// <summary>
        /// Saldırıyı yürüt. Combo index döndürür.
        /// Çağrılmadan önce stamina kontrolü ve tüketimi yapılmış olmalı.
        /// </summary>
        public int ExecuteAttack(AttackType attackType, Weapon weapon)
        {
            _currentWeapon = weapon;
            _currentAttackType = attackType;
            _attackStartTime = Time.time;
            float adjustedDuration = attackDuration / attackAnimationSpeed;
            _attackEndTime = Time.time + adjustedDuration;

            // Souls-like: Saldırı başlarken Wind-Up phase
            _currentPhase = AttackPhase.WindUp;
            _hitboxActive = false;

            // Combat_UpperBody layer weight → 1
            EnableCombatUpperBodyLayer(true);

            // Saldırı animasyon hızı
            _animator.speed = attackAnimationSpeed;

            // Combo tracking
            int currentCombo = 1;
            if (_comboSystem != null)
            {
                int weaponMaxCombo = weapon?.ComboMaxCount ?? 0;
                _comboSystem.OnAttackStart(weaponMaxCombo);
                currentCombo = _comboSystem.CurrentCombo;
            }

            // Animation - saldiri tipine gore trigger sec
            _animator.SetInteger(AnimatorHash.ComboIndex, currentCombo);
            if (attackType == AttackType.HeavyAttack)
            {
                if (_animEvents != null) _animEvents.PlayHeavyAttack(); else _animator.SetTrigger(AnimatorHash.HeavyAttack);
            }
            else
            {
                if (_animEvents != null) _animEvents.PlayLightAttack(); else _animator.SetTrigger(AnimatorHash.LightAttack);
            }

            DLog.Log($"[AttackExecutor] ATTACK - ComboIndex: {currentCombo}, Weapon: {weapon?.Data?.displayName ?? "none"}");

            // Hyper Armor
            if (_hyperArmorWindow != null && weapon != null)
            {
                _hyperArmorWindow.OnAttackStart(attackType, weapon.WeaponType);
            }

            // Fallback hitbox (animation event yoksa)
            if (!useAnimationEvents)
            {
                if (_hitboxCoroutine != null)
                    StopCoroutine(_hitboxCoroutine);
                _hitboxCoroutine = StartCoroutine(HitboxCoroutine());
            }

            return currentCombo;
        }

        /// <summary>
        /// Mevcut saldırıyı iptal et (animation canceling)
        /// Souls-like: Sadece Recovery phase'de veya force cancel ile çağrılmalı
        /// </summary>
        public void CancelCurrentAttack()
        {
            _currentWeapon?.DisableHitbox();
            _currentPhase = AttackPhase.None;
            _hitboxActive = false;

            if (_hitboxCoroutine != null)
            {
                StopCoroutine(_hitboxCoroutine);
                _hitboxCoroutine = null;
            }

            EnableCombatUpperBodyLayer(false);
            _animator.speed = 1f;

            DLog.Log("[AttackExecutor] Attack canceled (animation canceling)");
        }

        /// <summary>
        /// Hyper armor progress güncelle (Update'te çağrılır)
        /// </summary>
        public void UpdateHyperArmor(CombatState currentState)
        {
            if (_hyperArmorWindow != null && currentState == CombatState.Attacking)
            {
                _hyperArmorWindow.UpdateAttackProgress(AttackProgress);
            }
        }

        #region Animation Events (Called by Animator)

        public void OnAttackStart()
        {
            // Wind-Up phase başlıyor
            _currentPhase = AttackPhase.WindUp;
            _hitboxActive = false;

            int currentCombo = _comboSystem?.CurrentCombo ?? 1;
            _currentWeapon?.StartAttack(_currentAttackType, currentCombo);

            DLog.Log($"[AttackExecutor] Phase: WIND-UP (no cancel, no damage)");
        }

        public void OnAttackHitboxEnable()
        {
            // Active phase başlıyor
            _currentPhase = AttackPhase.Active;
            _hitboxActive = true;

            _currentWeapon?.EnableHitbox();

            DLog.Log($"[AttackExecutor] Phase: ACTIVE (no cancel, hitbox ON)");
        }

        public void OnAttackHitboxDisable()
        {
            // Recovery phase başlıyor
            _currentPhase = AttackPhase.Recovery;
            _hitboxActive = false;

            _currentWeapon?.DisableHitbox();

            DLog.Log($"[AttackExecutor] Phase: RECOVERY (dodge cancel OK, hitbox OFF)");
        }

        public void OnAttackEnd()
        {
            _currentPhase = AttackPhase.None;
            _hitboxActive = false;

            EnableCombatUpperBodyLayer(false);
            _animator.speed = 1f;
            _hyperArmorWindow?.OnAttackEnd();
            _comboSystem?.OnAttackEnd();

            OnAttackEnded?.Invoke();

            DLog.Log($"[AttackExecutor] Phase: NONE (attack ended)");
        }

        /// <summary>
        /// Animation Event: Combo penceresi acildi, oyuncu combo yapabilir.
        /// </summary>
        public void ComboWindowOpen()
        {
            _currentPhase = AttackPhase.ComboWindow;
            _comboSystem?.OpenComboWindow();
            DLog.Log("[AttackExecutor] Phase: COMBO WINDOW (input buffer active)");
        }

        /// <summary>
        /// Animation Event: Combo penceresi kapandi, combo zamani gecti.
        /// </summary>
        public void ComboWindowClose()
        {
            _comboSystem?.CloseComboWindow();
            DLog.Log("[AttackExecutor] Combo window CLOSED");
        }

        /// <summary>
        /// Animation Event: Silahi gorunur yap (unsheathe sirasinda).
        /// </summary>
        public void EquipWeaponVisual()
        {
            // Silah holder'ini aktif yap
            DLog.Log("[AttackExecutor] EquipWeaponVisual called");
        }

        /// <summary>
        /// Animation Event: Silahi gizle (sheathe sirasinda).
        /// </summary>
        public void UnequipWeaponVisual()
        {
            DLog.Log("[AttackExecutor] UnequipWeaponVisual called");
        }

        #endregion

        #region Private Methods

        private IEnumerator HitboxCoroutine()
        {
            yield return new WaitForSeconds(hitboxEnableDelay);

            int currentCombo = _comboSystem?.CurrentCombo ?? 1;
            _currentWeapon?.StartAttack(AttackType.LightAttack, currentCombo);
            _currentWeapon?.EnableHitbox();

            yield return new WaitForSeconds(hitboxActiveDuration);

            _currentWeapon?.DisableHitbox();
            _hitboxCoroutine = null;
        }

        private void EnableCombatUpperBodyLayer(bool enable)
        {
            AnimatorLayerHelper.SetLayerEnabled(_animator, "Combat_UpperBody", enable);
        }

        #endregion
    }
}
