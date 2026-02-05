using UnityEngine;
using Dungeons.Data;
using Dungeons.Core;

namespace Dungeons.Combat
{
    /// <summary>
    /// Poise (Super Armor) sistemi.
    /// Souls-like oyunlardaki gibi her vuruş stagger vermez,
    /// poise değeri 0'a düşene kadar karakter saldırısına devam edebilir.
    ///
    /// Poise Damage = Silahın stagger değeri
    /// CurrentPoise -= PoiseDamage
    /// if CurrentPoise <= 0 → STAGGER! ve poise reset
    /// </summary>
    public class PoiseSystem : MonoBehaviour
    {
        [Header("Poise Stats")]
        [Tooltip("Maksimum poise değeri (Heavy armor = daha yüksek)")]
        [SerializeField] private float maxPoise = 100f;

        [Tooltip("Poise yenilenme hızı (saniyede)")]
        [SerializeField] private float poiseRegenRate = 20f;

        [Tooltip("Hasar aldıktan sonra poise yenilenme gecikmesi")]
        [SerializeField] private float poiseRegenDelay = 2f;

        [Header("Hyper Armor")]
        [Tooltip("Hyper armor aktifken poise hasarı bu kadar azaltılır")]
        [SerializeField, Range(0f, 1f)] private float hyperArmorReduction = 0.5f;

        // State
        private float _currentPoise;
        private float _regenDelayTimer;
        private bool _hasHyperArmor;
        private float _hyperArmorMultiplier = 1f;

        // Components
        private HyperArmorWindow _hyperArmorWindow;

        // Properties
        public float MaxPoise => maxPoise;
        public float CurrentPoise => _currentPoise;
        public float PoisePercentage => _currentPoise / maxPoise;
        public bool HasHyperArmor => _hasHyperArmor;

        // Events
        public System.Action<float, float> OnPoiseChanged;
        public System.Action OnPoiseBroken;
        public System.Action<HitReaction> OnHitReactionDetermined;

        private void Awake()
        {
            _currentPoise = maxPoise;
            _hyperArmorWindow = GetComponent<HyperArmorWindow>();
        }

        private void Start()
        {
            // CombatSettings'ten değerleri al
            if (CombatSettings.Instance != null)
            {
                poiseRegenRate = CombatSettings.Instance.PoiseRegenRate;
                poiseRegenDelay = CombatSettings.Instance.PoiseRegenDelay;
            }
        }

        private void Update()
        {
            UpdatePoiseRegen();
            UpdateHyperArmorState();
        }

        /// <summary>
        /// Poise hasarı al. Returns: Tetiklenen HitReaction
        /// </summary>
        /// <param name="poiseDamage">Silahın poise damage değeri</param>
        /// <returns>Uygun HitReaction tipi</returns>
        public HitReaction TakePoiseDamage(float poiseDamage)
        {
            // Hyper armor varsa hasar azalt
            float effectiveDamage = poiseDamage;
            if (_hasHyperArmor)
            {
                effectiveDamage *= (1f - hyperArmorReduction);
            }

            _currentPoise -= effectiveDamage;
            _regenDelayTimer = poiseRegenDelay;

            OnPoiseChanged?.Invoke(_currentPoise, maxPoise);

            // Hit reaction belirle
            HitReaction reaction = DetermineHitReaction(poiseDamage);

            // Poise kırıldı mı?
            if (_currentPoise <= 0)
            {
                OnPoiseBroken?.Invoke();
                _currentPoise = maxPoise; // Reset poise
            }

            OnHitReactionDetermined?.Invoke(reaction);
            return reaction;
        }

        /// <summary>
        /// Poise hasarına göre hit reaction belirle
        /// </summary>
        private HitReaction DetermineHitReaction(float poiseDamage)
        {
            var settings = CombatSettings.Instance;
            float threshold = poiseDamage / maxPoise;

            // Hyper armor varsa ve threshold düşükse tepki yok
            if (_hasHyperArmor && threshold < settings.StaggerThreshold)
            {
                return HitReaction.None;
            }

            // Normal reaction belirleme
            if (_currentPoise > 0)
            {
                // Poise henüz kırılmadı
                if (threshold < settings.FlinchThreshold)
                    return HitReaction.None;
                else if (threshold < settings.StaggerThreshold)
                    return HitReaction.Flinch;
                else
                    return HitReaction.None; // Poise absorb etti
            }
            else
            {
                // Poise kırıldı!
                if (threshold < settings.HeavyStaggerThreshold)
                    return HitReaction.Stagger;
                else if (threshold < settings.KnockdownThreshold)
                    return HitReaction.HeavyStagger;
                else
                    return HitReaction.Knockdown;
            }
        }

        /// <summary>
        /// Poise yenilenme güncelleme
        /// </summary>
        private void UpdatePoiseRegen()
        {
            if (_regenDelayTimer > 0)
            {
                _regenDelayTimer -= Time.deltaTime;
                return;
            }

            if (_currentPoise < maxPoise)
            {
                _currentPoise = Mathf.Min(maxPoise, _currentPoise + poiseRegenRate * Time.deltaTime);
                OnPoiseChanged?.Invoke(_currentPoise, maxPoise);
            }
        }

        /// <summary>
        /// Hyper armor durumunu güncelle
        /// </summary>
        private void UpdateHyperArmorState()
        {
            if (_hyperArmorWindow != null)
            {
                _hasHyperArmor = _hyperArmorWindow.IsHyperArmorActive;
                _hyperArmorMultiplier = _hyperArmorWindow.PoiseMultiplier;
            }
        }

        /// <summary>
        /// Hyper armor'u manuel olarak aktive et
        /// </summary>
        public void SetHyperArmor(bool active, float multiplier = 1f)
        {
            _hasHyperArmor = active;
            _hyperArmorMultiplier = multiplier;
        }

        /// <summary>
        /// Maksimum poise değerini değiştir (zırh değiştiğinde)
        /// </summary>
        public void SetMaxPoise(float newMax)
        {
            float ratio = _currentPoise / maxPoise;
            maxPoise = newMax;
            _currentPoise = maxPoise * ratio;
        }

        /// <summary>
        /// Poise'u tamamen restore et
        /// </summary>
        public void RestorePoise()
        {
            _currentPoise = maxPoise;
            OnPoiseChanged?.Invoke(_currentPoise, maxPoise);
        }

        /// <summary>
        /// Anlık poise ekle (buff için)
        /// </summary>
        public void AddPoise(float amount)
        {
            _currentPoise = Mathf.Min(maxPoise, _currentPoise + amount);
            OnPoiseChanged?.Invoke(_currentPoise, maxPoise);
        }

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        private void OnGUI()
        {
            if (!showDebugGizmos) return;

            // Poise bar göster
            GUILayout.BeginArea(new Rect(10, 100, 200, 60));
            GUILayout.Label($"Poise: {_currentPoise:F0}/{maxPoise:F0}");
            GUILayout.HorizontalSlider(_currentPoise / maxPoise, 0f, 1f);
            if (_hasHyperArmor)
            {
                GUILayout.Label("HYPER ARMOR ACTIVE");
            }
            GUILayout.EndArea();
        }
#endif
    }
}
