using UnityEngine;
using Dungeons.Core;

namespace Dungeons.Combat
{
    /// <summary>
    /// Stamina yönetim sistemi.
    /// PlayerCombat'tan ayrılmış - Single Responsibility.
    ///
    /// Sorumluluklar:
    /// - Stamina tüketimi
    /// - Stamina yenilenmesi
    /// - Stamina bazlı aksiyon kontrolü
    /// </summary>
    public class PlayerStaminaSystem : MonoBehaviour
    {
        [Header("Stamina Stats")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaRegenRate = 15f;
        [SerializeField] private float staminaRegenDelay = 1.5f;

        [Header("Action Costs")]
        [SerializeField] private float lightAttackCost = 1f;
        [SerializeField] private float heavyAttackCost = 30f;
        [SerializeField] private float chargedAttackCost = 40f;
        [SerializeField] private float dodgeCost = 25f;
        [SerializeField] private float sprintCostPerSecond = 10f;

        [Header("Block Settings")]
        [SerializeField] private float blockDrainPerSecond = 5f;
        [SerializeField] private float blockHitCost = 15f;

        // State
        private float _currentStamina;
        private float _regenDelayTimer;
        private bool _isRegenerating = true;
        private bool _isBlocking;

        // Properties
        public float CurrentStamina => _currentStamina;
        public float MaxStamina => maxStamina;
        public float StaminaPercent => _currentStamina / maxStamina;
        public bool IsExhausted => _currentStamina <= 0;
        public bool CanRegenerate => _regenDelayTimer <= 0 && !_isBlocking;

        // Cost accessors
        public float LightAttackCost => lightAttackCost;
        public float HeavyAttackCost => heavyAttackCost;
        public float ChargedAttackCost => chargedAttackCost;
        public float DodgeCost => dodgeCost;

        // Events
        public System.Action<float, float> OnStaminaChanged;
        public System.Action OnStaminaDepleted;
        public System.Action OnStaminaRecovered;

        private void Awake()
        {
            _currentStamina = maxStamina;

            // DEBUG: Force light attack cost to 1 for testing
            lightAttackCost = 1f;
        }

        private void Start()
        {
            // CombatSettings'ten değerleri al
            if (CombatSettings.Instance != null)
            {
                staminaRegenRate = CombatSettings.Instance.StaminaRegenRate;
                staminaRegenDelay = CombatSettings.Instance.StaminaRegenDelay;
            }
        }

        private void Update()
        {
            UpdateRegenDelay();
            UpdateStaminaRegen();
            UpdateBlockDrain();
        }

        /// <summary>
        /// Stamina tüket
        /// </summary>
        public bool Consume(float amount)
        {
            if (amount <= 0) return true;

            if (_currentStamina < amount)
            {
                // Yetersiz stamina - aksiyon iptal
                OnStaminaDepleted?.Invoke();
                return false;
            }

            _currentStamina = Mathf.Max(0, _currentStamina - amount);
            _regenDelayTimer = staminaRegenDelay;

            OnStaminaChanged?.Invoke(_currentStamina, maxStamina);

            // Event bus
            EventBus.Publish(new StaminaChangedEvent
            {
                Character = gameObject,
                CurrentStamina = _currentStamina,
                MaxStamina = maxStamina
            });

            if (_currentStamina <= 0)
            {
                OnStaminaDepleted?.Invoke();
            }

            return true;
        }

        /// <summary>
        /// Light attack için stamina tüket
        /// </summary>
        public bool ConsumeLightAttack() => Consume(lightAttackCost);

        /// <summary>
        /// Heavy attack için stamina tüket
        /// </summary>
        public bool ConsumeHeavyAttack() => Consume(heavyAttackCost);

        /// <summary>
        /// Charged attack için stamina tüket
        /// </summary>
        public bool ConsumeChargedAttack() => Consume(chargedAttackCost);

        /// <summary>
        /// Dodge için stamina tüket
        /// </summary>
        public bool ConsumeDodge() => Consume(dodgeCost);

        /// <summary>
        /// Block sırasında hit aldığında stamina tüket
        /// </summary>
        public bool ConsumeBlockHit(float damageAmount = 0)
        {
            float cost = blockHitCost + (damageAmount * 0.1f);
            return Consume(cost);
        }

        /// <summary>
        /// Yeterli stamina var mı?
        /// </summary>
        public bool HasStamina(float amount) => _currentStamina >= amount;

        /// <summary>
        /// Light attack için yeterli stamina var mı?
        /// </summary>
        public bool CanLightAttack => HasStamina(lightAttackCost);

        /// <summary>
        /// Heavy attack için yeterli stamina var mı?
        /// </summary>
        public bool CanHeavyAttack => HasStamina(heavyAttackCost);

        /// <summary>
        /// Dodge için yeterli stamina var mı?
        /// </summary>
        public bool CanDodge => HasStamina(dodgeCost);

        /// <summary>
        /// Stamina restore et
        /// </summary>
        public void Restore(float amount)
        {
            float previousStamina = _currentStamina;
            _currentStamina = Mathf.Min(maxStamina, _currentStamina + amount);

            if (previousStamina <= 0 && _currentStamina > 0)
            {
                OnStaminaRecovered?.Invoke();
            }

            OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
        }

        /// <summary>
        /// Stamina'yı full yap
        /// </summary>
        public void RestoreFull()
        {
            Restore(maxStamina);
        }

        /// <summary>
        /// Block durumunu ayarla
        /// </summary>
        public void SetBlocking(bool isBlocking)
        {
            _isBlocking = isBlocking;
        }

        /// <summary>
        /// Max stamina'yı değiştir (equipment effect)
        /// </summary>
        public void SetMaxStamina(float newMax)
        {
            float ratio = _currentStamina / maxStamina;
            maxStamina = newMax;
            _currentStamina = maxStamina * ratio;

            OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
        }

        /// <summary>
        /// Regen delay timer güncelleme
        /// </summary>
        private void UpdateRegenDelay()
        {
            if (_regenDelayTimer > 0)
            {
                _regenDelayTimer -= Time.deltaTime;
            }
        }

        /// <summary>
        /// Stamina yenilenme
        /// </summary>
        private void UpdateStaminaRegen()
        {
            if (!CanRegenerate) return;
            if (_currentStamina >= maxStamina) return;

            float previousStamina = _currentStamina;
            _currentStamina = Mathf.Min(maxStamina, _currentStamina + staminaRegenRate * Time.deltaTime);

            if (previousStamina <= 0 && _currentStamina > 0)
            {
                OnStaminaRecovered?.Invoke();
            }

            OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
        }

        /// <summary>
        /// Block sırasında stamina drain
        /// </summary>
        private void UpdateBlockDrain()
        {
            if (!_isBlocking) return;

            _currentStamina = Mathf.Max(0, _currentStamina - blockDrainPerSecond * Time.deltaTime);
            OnStaminaChanged?.Invoke(_currentStamina, maxStamina);

            if (_currentStamina <= 0)
            {
                OnStaminaDepleted?.Invoke();
            }
        }

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool showDebugUI = false;

        private void OnGUI()
        {
            if (!showDebugUI) return;

            GUILayout.BeginArea(new Rect(10, 160, 200, 80));
            GUILayout.Label($"Stamina: {_currentStamina:F0}/{maxStamina:F0}");
            GUILayout.HorizontalSlider(_currentStamina / maxStamina, 0f, 1f);
            GUILayout.Label($"Regen Delay: {_regenDelayTimer:F1}s");
            GUILayout.Label($"Blocking: {_isBlocking}");
            GUILayout.EndArea();
        }
#endif
    }
}
