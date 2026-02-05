using Dungeons.Utilities;
using UnityEngine;
using Dungeons.Data;
using Dungeons.Core;
using Dungeons.Core.Utilities;

namespace Dungeons.Combat
{
    /// <summary>
    /// Combo yönetim sistemi.
    /// PlayerCombat'tan ayrılmış - Single Responsibility.
    ///
    /// Sorumluluklar:
    /// - Combo counter takibi
    /// - Combo reset timer
    /// - Hit confirm tracking
    /// - Combo multiplier hesaplama
    /// </summary>
    public class PlayerComboSystem : MonoBehaviour
    {
        [Header("Combo Settings")]
        [Tooltip("Combo reset süresi")]
        [SerializeField] private float comboResetTime = 1.2f;

        [Tooltip("Maksimum combo sayısı (silah yoksa)")]
        [SerializeField] private int defaultMaxCombo = 3;

        [Header("Hit Confirm")]
        [Tooltip("Hit confirm ile combo continuation")]
        [SerializeField] private bool allowHitConfirmCombo = true;

        [Tooltip("Recovery fazı başlangıcı (0-1)")]
        [SerializeField, Range(0.5f, 0.9f)] private float recoveryPhaseStart = 0.7f;

        [Header("Damage Multipliers")]
        [SerializeField] private float[] comboMultipliers = { 1.0f, 1.1f, 1.2f, 1.3f, 1.5f };

        // State
        private int _currentCombo;
        private float _comboTimer;
        private bool _hitLandedThisAttack;
        private int _maxCombo;

        // Components
        private PlayerCombat _combat;
        private Animator _animator;

        // Properties
        public int CurrentCombo => _currentCombo;
        public int MaxCombo => _maxCombo;
        public bool HitLandedThisAttack => _hitLandedThisAttack;
        public float RecoveryPhaseStart => recoveryPhaseStart;
        public bool AllowHitConfirmCombo => allowHitConfirmCombo;

        /// <summary>
        /// Mevcut combo'nun hasar çarpanı
        /// </summary>
        public float CurrentComboMultiplier
        {
            get
            {
                int index = Mathf.Clamp(_currentCombo - 1, 0, comboMultipliers.Length - 1);
                return comboMultipliers[index];
            }
        }

        /// <summary>
        /// Combo devam ettirilebilir mi? (hit confirm + recovery)
        /// </summary>
        public bool CanContinueCombo => allowHitConfirmCombo && _hitLandedThisAttack;

        // Events
        public System.Action<int> OnComboChanged;
        public System.Action OnComboReset;
        public System.Action OnHitConfirm;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
            _animator = GetComponent<Animator>();
            _maxCombo = defaultMaxCombo;
        }

        private void Start()
        {
            // CombatSettings'ten değerleri al
            if (CombatSettings.Instance != null)
            {
                var settings = CombatSettings.Instance;
                comboResetTime = settings.ComboResetTime;
                defaultMaxCombo = settings.DefaultMaxCombo;
                comboMultipliers = settings.ComboMultipliers;
                _maxCombo = defaultMaxCombo;

                DLog.Log($"[ComboSystem] Loaded settings - ResetTime: {comboResetTime}s, MaxCombo: {defaultMaxCombo}, Multipliers: [{string.Join(", ", comboMultipliers)}]");
            }
        }

        private void Update()
        {
            UpdateComboTimer();
        }

        /// <summary>
        /// Yeni saldırı başladığında çağrılır
        /// </summary>
        public void OnAttackStart(int weaponMaxCombo = 0)
        {
            int previousCombo = _currentCombo;

            // Max combo güncelle
            _maxCombo = weaponMaxCombo > 0 ? weaponMaxCombo : defaultMaxCombo;

            // Combo artır
            _currentCombo++;
            if (_currentCombo > _maxCombo)
            {
                _currentCombo = 1;
            }

            // Timer reset
            _comboTimer = comboResetTime;

            // Hit flag reset
            _hitLandedThisAttack = false;

            // Animator'a bildir
            _animator?.SafeSetInteger(AnimatorHash.ComboIndex, _currentCombo);

            // Event
            OnComboChanged?.Invoke(_currentCombo);

            DLog.Log($"[ComboSystem] ATTACK START - Previous: {previousCombo} → Current: {_currentCombo}/{_maxCombo}, Timer: {comboResetTime}s, WeaponMaxCombo: {weaponMaxCombo}");
        }

        /// <summary>
        /// Saldırı hasar verdiğinde çağrılır
        /// </summary>
        public void OnHitLanded()
        {
            if (_hitLandedThisAttack) return;

            _hitLandedThisAttack = true;
            OnHitConfirm?.Invoke();

            DLog.Log("[ComboSystem] Hit confirmed! Combo continuation enabled.");
        }

        /// <summary>
        /// Saldırı bittiğinde çağrılır (hasar vermeden)
        /// </summary>
        public void OnAttackEnd()
        {
            // Hasar vermeden saldırı biterse, combo reset timer'ı çalışmaya devam eder
            // Timer bitince combo sıfırlanır
        }

        /// <summary>
        /// Animation Event: Combo penceresi acildi.
        /// Bu pencere icinde oyuncu tekrar saldirirsa combo devam eder.
        /// </summary>
        public void OpenComboWindow()
        {
            _comboTimer = comboResetTime;
            DLog.Log($"[ComboSystem] Combo window OPEN - ComboIndex: {_currentCombo}");
        }

        /// <summary>
        /// Animation Event: Combo penceresi kapandi.
        /// Bu noktadan sonra saldiri yeni combo baslatir.
        /// </summary>
        public void CloseComboWindow()
        {
            // Combo window kapandi, kisa bir sure daha bekle
            _comboTimer = Mathf.Min(_comboTimer, 0.3f);
            DLog.Log($"[ComboSystem] Combo window CLOSED - remaining timer: {_comboTimer:F2}s");
        }

        /// <summary>
        /// Combo'yu zorla sıfırla
        /// </summary>
        public void ResetCombo()
        {
            if (_currentCombo > 0)
            {
                DLog.Log($"[ComboSystem] COMBO RESET! Was: {_currentCombo}, Timer expired");
                _currentCombo = 0;
                _comboTimer = 0;
                _hitLandedThisAttack = false;

                _animator?.SafeSetInteger(AnimatorHash.ComboIndex, 0);

                OnComboReset?.Invoke();
            }
        }

        /// <summary>
        /// Combo timer güncelleme
        /// </summary>
        private void UpdateComboTimer()
        {
            if (_comboTimer > 0)
            {
                _comboTimer -= Time.deltaTime;

                if (_comboTimer <= 0)
                {
                    ResetCombo();
                }
            }
        }

        /// <summary>
        /// Combo için hasar hesapla
        /// </summary>
        public int CalculateComboDamage(int baseDamage)
        {
            return Mathf.RoundToInt(baseDamage * CurrentComboMultiplier);
        }

        /// <summary>
        /// Mevcut combo'nun son vuruş mu?
        /// </summary>
        public bool IsFinisher => _currentCombo >= _maxCombo;

        /// <summary>
        /// Combo zinciri aktif mi?
        /// </summary>
        public bool IsComboActive => _currentCombo > 0 && _comboTimer > 0;
    }
}
