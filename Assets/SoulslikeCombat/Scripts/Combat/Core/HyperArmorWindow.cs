using Dungeons.Utilities;
using UnityEngine;
using Dungeons.Data;
using Dungeons.Items;

namespace Dungeons.Combat
{
    /// <summary>
    /// Hyper Armor (Attack Armor) sistemi.
    /// Ağır saldırılar sırasında belirli frame'lerde karakter kesintiye uğramaz.
    ///
    /// Silah Bazlı Hyper Armor:
    /// - Dagger: Yok
    /// - Sword: Sadece heavy attack
    /// - Greatsword: Tüm saldırılar
    /// - Warhammer: Tüm saldırılar + Extra poise
    /// </summary>
    public class HyperArmorWindow : MonoBehaviour
    {
        [Header("Window Settings")]
        [Tooltip("Hyper armor başlama zamanı (animasyon yüzdesi)")]
        [SerializeField, Range(0f, 1f)] private float hyperArmorStart = 0.3f;

        [Tooltip("Hyper armor bitiş zamanı (animasyon yüzdesi)")]
        [SerializeField, Range(0f, 1f)] private float hyperArmorEnd = 0.7f;

        [Header("Poise Bonus")]
        [Tooltip("Hyper armor sırasında poise çarpanı")]
        [SerializeField] private float poiseMultiplier = 2f;

        // State
        private bool _isAttacking;
        private float _attackProgress;
        private AttackType _currentAttackType;
        private WeaponType _currentWeaponType;
        private bool _isHyperArmorActive;

        // Components
        private Animator _animator;
        private PlayerCombat _playerCombat;

        // Properties
        public bool IsHyperArmorActive => _isHyperArmorActive;
        public float PoiseMultiplier => _isHyperArmorActive ? poiseMultiplier : 1f;
        public float HyperArmorStart => hyperArmorStart;
        public float HyperArmorEnd => hyperArmorEnd;

        // Events
        public System.Action OnHyperArmorStart;
        public System.Action OnHyperArmorEnd;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _playerCombat = GetComponent<PlayerCombat>();
        }

        private void Start()
        {
            // CombatSettings'ten değerleri al
            if (CombatSettings.Instance != null)
            {
                hyperArmorStart = CombatSettings.Instance.HyperArmorStart;
                hyperArmorEnd = CombatSettings.Instance.HyperArmorEnd;
            }
        }

        private void Update()
        {
            UpdateHyperArmorState();
        }

        /// <summary>
        /// Saldırı başladığında çağrılır
        /// </summary>
        public void OnAttackStart(AttackType attackType, WeaponType weaponType)
        {
            _isAttacking = true;
            _attackProgress = 0f;
            _currentAttackType = attackType;
            _currentWeaponType = weaponType;
        }

        /// <summary>
        /// Saldırı bittiğinde çağrılır
        /// </summary>
        public void OnAttackEnd()
        {
            _isAttacking = false;
            _attackProgress = 0f;

            if (_isHyperArmorActive)
            {
                _isHyperArmorActive = false;
                OnHyperArmorEnd?.Invoke();
            }
        }

        /// <summary>
        /// Animasyon ilerleme güncellemesi (PlayerCombat'tan çağrılır)
        /// </summary>
        public void UpdateAttackProgress(float progress)
        {
            _attackProgress = progress;
        }

        /// <summary>
        /// Hyper armor durumunu güncelle
        /// </summary>
        private void UpdateHyperArmorState()
        {
            if (!_isAttacking)
            {
                if (_isHyperArmorActive)
                {
                    _isHyperArmorActive = false;
                    OnHyperArmorEnd?.Invoke();
                }
                return;
            }

            // Silahın hyper armor verip vermediğini kontrol et
            if (!HasHyperArmor(_currentWeaponType, _currentAttackType))
            {
                _isHyperArmorActive = false;
                return;
            }

            // Animasyon penceresinde mi?
            bool shouldBeActive = _attackProgress >= hyperArmorStart && _attackProgress <= hyperArmorEnd;

            if (shouldBeActive && !_isHyperArmorActive)
            {
                _isHyperArmorActive = true;
                OnHyperArmorStart?.Invoke();
                DLog.Log($"[HyperArmor] STARTED at {_attackProgress:P0}");
            }
            else if (!shouldBeActive && _isHyperArmorActive)
            {
                _isHyperArmorActive = false;
                OnHyperArmorEnd?.Invoke();
                DLog.Log($"[HyperArmor] ENDED at {_attackProgress:P0}");
            }
        }

        /// <summary>
        /// Verilen animasyon ilerlemesinde hyper armor aktif mi?
        /// </summary>
        public bool HasHyperArmor(float animationProgress)
        {
            if (!_isAttacking) return false;
            if (!HasHyperArmor(_currentWeaponType, _currentAttackType)) return false;

            return animationProgress >= hyperArmorStart && animationProgress <= hyperArmorEnd;
        }

        /// <summary>
        /// Silah ve saldırı tipine göre hyper armor var mı?
        /// </summary>
        public bool HasHyperArmor(WeaponType weaponType, AttackType attackType)
        {
            return weaponType switch
            {
                // Hafif silahlar - Hyper armor yok
                WeaponType.Dagger => false,
                WeaponType.Wand => false,

                // Orta silahlar - Sadece heavy attack'te
                WeaponType.Sword => attackType == AttackType.HeavyAttack || attackType == AttackType.ChargedAttack,
                WeaponType.Axe => attackType == AttackType.HeavyAttack || attackType == AttackType.ChargedAttack,
                WeaponType.Mace => attackType == AttackType.HeavyAttack || attackType == AttackType.ChargedAttack,
                WeaponType.Spear => attackType == AttackType.HeavyAttack || attackType == AttackType.ChargedAttack,

                // Ağır silahlar - Tüm saldırılarda
                WeaponType.Greatsword => true,
                WeaponType.Warhammer => true,
                WeaponType.Halberd => true,

                // Diğer
                WeaponType.Staff => attackType == AttackType.ChargedAttack,
                WeaponType.Shield => true, // Shield bash her zaman hyper armor

                _ => false
            };
        }

        /// <summary>
        /// Silah tipine göre poise çarpanını al
        /// </summary>
        public float GetPoiseMultiplier(WeaponType weaponType)
        {
            return weaponType switch
            {
                WeaponType.Warhammer => poiseMultiplier * 1.5f, // Extra poise
                WeaponType.Greatsword => poiseMultiplier * 1.25f,
                WeaponType.Halberd => poiseMultiplier * 1.25f,
                _ => poiseMultiplier
            };
        }

        /// <summary>
        /// Hyper armor penceresini al (silaha özel)
        /// </summary>
        public (float start, float end) GetHyperArmorWindow(WeaponType weaponType)
        {
            return weaponType switch
            {
                // Ağır silahlar daha geniş pencere
                WeaponType.Warhammer => (0.2f, 0.8f),
                WeaponType.Greatsword => (0.25f, 0.75f),
                WeaponType.Halberd => (0.25f, 0.75f),

                // Orta silahlar standart pencere
                WeaponType.Sword => (hyperArmorStart, hyperArmorEnd),
                WeaponType.Axe => (hyperArmorStart, hyperArmorEnd),
                WeaponType.Mace => (hyperArmorStart, hyperArmorEnd),
                WeaponType.Spear => (hyperArmorStart, hyperArmorEnd),

                _ => (hyperArmorStart, hyperArmorEnd)
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_isHyperArmorActive)
            {
                // Hyper armor aktifken glow efekti
                Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position + Vector3.up, 1f);
            }
        }
#endif
    }
}
