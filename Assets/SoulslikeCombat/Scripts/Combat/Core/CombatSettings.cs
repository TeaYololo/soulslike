using Dungeons.Utilities;
using UnityEngine;
using Dungeons.Data;

namespace Dungeons.Combat
{
    /// <summary>
    /// Combat sistemi için merkezi ayarlar.
    /// Tüm timing değerleri ve denge parametreleri burada tutulur.
    /// ScriptableObject olarak farklı zorluk seviyeleri oluşturulabilir.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatSettings", menuName = "Dungeons/Combat/Combat Settings")]
    public class CombatSettings : ScriptableObject
    {
        private static CombatSettings _instance;
        public static CombatSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<CombatSettings>("CombatSettings");
                    if (_instance == null)
                    {
                        DLog.LogWarning("[CombatSettings] No CombatSettings found in Resources! Using defaults.");
                        _instance = CreateInstance<CombatSettings>();
                    }
                }
                return _instance;
            }
        }

        [Header("Difficulty")]
        [SerializeField] private ParryDifficulty parryDifficulty = ParryDifficulty.Normal;
        public ParryDifficulty ParryDifficulty => parryDifficulty;

        // ==================== PARRY SETTINGS ====================
        [Header("Parry Timing")]
        [Tooltip("Easy: 250ms, Normal: 180ms, Hard: 120ms, Sekiro: 100ms")]
        [SerializeField] private float parryWindowEasy = 0.25f;
        [SerializeField] private float parryWindowNormal = 0.18f;
        [SerializeField] private float parryWindowHard = 0.12f;
        [SerializeField] private float parryWindowSekiro = 0.10f;

        public float GetParryWindow()
        {
            return parryDifficulty switch
            {
                ParryDifficulty.Easy => parryWindowEasy,
                ParryDifficulty.Normal => parryWindowNormal,
                ParryDifficulty.Hard => parryWindowHard,
                ParryDifficulty.Sekiro => parryWindowSekiro,
                _ => parryWindowNormal
            };
        }

        [Tooltip("Parry başarılı olunca düşman stagger süresi")]
        [SerializeField] private float parryStunDuration = 1.5f;
        public float ParryStunDuration => parryStunDuration;

        [Tooltip("Network latency tolerance for parry (ms)")]
        [SerializeField] private float parryLatencyTolerance = 0.15f;
        public float ParryLatencyTolerance => parryLatencyTolerance;

        // ==================== DODGE SETTINGS ====================
        [Header("Dodge Timing")]
        [Tooltip("Perfect dodge penceresi (Bayonetta: 60ms, MGR: 80ms)")]
        [SerializeField] private float perfectDodgeWindow = 0.08f;
        public float PerfectDodgeWindow => perfectDodgeWindow;

        [Tooltip("Perfect dodge slow-mo aktif mi? (Souls-like: false)")]
        [SerializeField] private bool enablePerfectDodgeSlowmo = false;
        public bool EnablePerfectDodgeSlowmo => enablePerfectDodgeSlowmo;

        [Tooltip("Dodge stamina cost")]
        [SerializeField] private float dodgeStamina = 25f;
        public float DodgeStamina => dodgeStamina;

        [Tooltip("Dodge mesafesi (metre)")]
        [SerializeField] private float dodgeDistance = 3.5f;
        public float DodgeDistance => dodgeDistance;

        [Tooltip("Dodge toplam süresi")]
        [SerializeField] private float dodgeDuration = 0.5f;
        public float DodgeDuration => dodgeDuration;

        [Tooltip("i-Frame başlangıç gecikmesi")]
        [SerializeField] private float dodgeIFrameStart = 0.05f;
        public float DodgeIFrameStart => dodgeIFrameStart;

        [Tooltip("i-Frame süresi - Equip load'a göre değişir")]
        [SerializeField] private float iFrameLight = 0.40f;
        [SerializeField] private float iFrameMedium = 0.33f;
        [SerializeField] private float iFrameHeavy = 0.20f;

        public float GetIFrameDuration(float equipLoadRatio)
        {
            if (equipLoadRatio < 0.3f) return iFrameLight;
            if (equipLoadRatio < 0.7f) return iFrameMedium;
            return iFrameHeavy;
        }

        [Tooltip("Varsayılan i-frame süresi")]
        [SerializeField] private float dodgeIFrameDuration = 0.33f;
        public float DodgeIFrameDuration => dodgeIFrameDuration;

        // ==================== BLOCK SETTINGS ====================
        [Header("Block Balance")]
        [Tooltip("Block hasar azaltma (1.0 = %100, önerilen: 0.7)")]
        [SerializeField, Range(0f, 1f)] private float blockDamageReduction = 0.7f;
        public float BlockDamageReduction => blockDamageReduction;

        [Tooltip("Block sırasında stamina hasarı çarpanı")]
        [SerializeField] private float blockStaminaDamageRatio = 1.5f;
        public float BlockStaminaDamageRatio => blockStaminaDamageRatio;

        [Tooltip("Guard break sonrası stagger süresi")]
        [SerializeField] private float guardBreakStaggerDuration = 1.5f;
        public float GuardBreakStaggerDuration => guardBreakStaggerDuration;

        // ==================== ATTACK SETTINGS ====================
        [Header("Attack Stamina")]
        [Tooltip("Light attack stamina cost")]
        [SerializeField] private float lightAttackStamina = 15f;
        public float LightAttackStamina => lightAttackStamina;

        [Tooltip("Heavy attack stamina cost")]
        [SerializeField] private float heavyAttackStamina = 30f;
        public float HeavyAttackStamina => heavyAttackStamina;

        [Tooltip("Kick stamina cost")]
        [SerializeField] private float kickStaminaCost = 15f;
        public float KickStaminaCost => kickStaminaCost;

        [Header("Attack Cooldowns")]
        [Tooltip("Light attack cooldown")]
        [SerializeField] private float lightAttackCooldown = 0.5f;
        public float LightAttackCooldown => lightAttackCooldown;

        [Tooltip("Heavy attack cooldown")]
        [SerializeField] private float heavyAttackCooldown = 1.0f;
        public float HeavyAttackCooldown => heavyAttackCooldown;

        // ==================== COMBO SETTINGS ====================
        [Header("Combo Timing")]
        [Tooltip("Combo reset süresi (önerilen: 0.8s)")]
        [SerializeField] private float comboResetTime = 0.8f;
        public float ComboResetTime => comboResetTime;

        [Tooltip("Default max combo count")]
        [SerializeField] private int defaultMaxCombo = 3;
        public int DefaultMaxCombo => defaultMaxCombo;

        [Tooltip("Combo damage multipliers (Souls-like: minimal scaling)")]
        [SerializeField] private float[] comboMultipliers = { 1.0f, 1.05f, 1.1f };
        public float[] ComboMultipliers => comboMultipliers;

        public float GetComboMultiplier(int comboIndex)
        {
            if (comboMultipliers == null || comboMultipliers.Length == 0) return 1f;
            int index = Mathf.Clamp(comboIndex - 1, 0, comboMultipliers.Length - 1);
            return comboMultipliers[index];
        }

        // ==================== STAMINA SETTINGS ====================
        [Header("Stamina")]
        [Tooltip("Maksimum stamina")]
        [SerializeField] private float maxStamina = 100f;
        public float MaxStamina => maxStamina;

        [Tooltip("Stamina regen rate (saniyede)")]
        [SerializeField] private float staminaRegenRate = 20f;
        public float StaminaRegenRate => staminaRegenRate;

        [Tooltip("Stamina regen başlama gecikmesi")]
        [SerializeField] private float staminaRegenDelay = 1.5f;
        public float StaminaRegenDelay => staminaRegenDelay;

        // ==================== POISE SETTINGS ====================
        [Header("Poise System")]
        [Tooltip("Maksimum poise")]
        [SerializeField] private float maxPoise = 100f;
        public float MaxPoise => maxPoise;

        [Tooltip("Poise yenilenme hızı (saniyede)")]
        [SerializeField] private float poiseRegenRate = 10f;
        public float PoiseRegenRate => poiseRegenRate;

        [Tooltip("Poise yenilenme gecikmesi (hasar aldıktan sonra)")]
        [SerializeField] private float poiseRegenDelay = 2f;
        public float PoiseRegenDelay => poiseRegenDelay;

        // ==================== BACKSTAB/RIPOSTE SETTINGS ====================
        [Header("Backstab & Riposte")]
        [Tooltip("Backstab açısı (derece)")]
        [SerializeField] private float backstabAngle = 45f;
        public float BackstabAngle => backstabAngle;

        [Tooltip("Backstab mesafesi")]
        [SerializeField] private float backstabRange = 1.5f;
        public float BackstabRange => backstabRange;

        [Tooltip("Backstab hasar çarpanı")]
        [SerializeField] private float backstabDamageMultiplier = 3f;
        public float BackstabDamageMultiplier => backstabDamageMultiplier;

        [Tooltip("Riposte penceresi (parry sonrası)")]
        [SerializeField] private float riposteWindow = 1.5f;
        public float RiposteWindow => riposteWindow;

        [Tooltip("Riposte hasar çarpanı")]
        [SerializeField] private float riposteDamageMultiplier = 2.5f;
        public float RiposteDamageMultiplier => riposteDamageMultiplier;

        // ==================== HIT REACTION THRESHOLDS ====================
        [Header("Hit Reaction Thresholds")]
        [Tooltip("Flinch threshold (poise yüzdesi olarak)")]
        [SerializeField, Range(0f, 1f)] private float flinchThreshold = 0.3f;
        public float FlinchThreshold => flinchThreshold;

        [Tooltip("Stagger threshold (poise yüzdesi olarak)")]
        [SerializeField, Range(0f, 1f)] private float staggerThreshold = 0.6f;
        public float StaggerThreshold => staggerThreshold;

        [Tooltip("Heavy stagger threshold (poise yüzdesi olarak)")]
        [SerializeField, Range(0f, 1f)] private float heavyStaggerThreshold = 1.0f;
        public float HeavyStaggerThreshold => heavyStaggerThreshold;

        [Tooltip("Knockdown threshold (poise yüzdesi olarak)")]
        [SerializeField, Range(1f, 2f)] private float knockdownThreshold = 1.5f;
        public float KnockdownThreshold => knockdownThreshold;

        // ==================== HIT ZONE MULTIPLIERS ====================
        [Header("Hit Zone Damage Multipliers")]
        [SerializeField] private float headDamageMultiplier = 1.5f;
        [SerializeField] private float bodyDamageMultiplier = 1.0f;
        [SerializeField] private float armsDamageMultiplier = 0.8f;
        [SerializeField] private float legsDamageMultiplier = 0.7f;

        public float GetHitZoneMultiplier(HitZone zone)
        {
            return zone switch
            {
                HitZone.Head => headDamageMultiplier,
                HitZone.Body => bodyDamageMultiplier,
                HitZone.Arms => armsDamageMultiplier,
                HitZone.Legs => legsDamageMultiplier,
                _ => bodyDamageMultiplier
            };
        }

        // ==================== HIT REACTION DURATIONS ====================
        [Header("Hit Reaction Durations")]
        [SerializeField] private float flinchDuration = 0.15f;
        public float FlinchDuration => flinchDuration;

        [SerializeField] private float staggerDuration = 0.4f;
        public float StaggerDuration => staggerDuration;

        [SerializeField] private float heavyStaggerDuration = 0.8f;
        public float HeavyStaggerDuration => heavyStaggerDuration;

        [SerializeField] private float knockbackDuration = 0.6f;
        public float KnockbackDuration => knockbackDuration;

        [SerializeField] private float knockdownDuration = 2.0f;
        public float KnockdownDuration => knockdownDuration;

        [SerializeField] private float launchDuration = 1.5f;
        public float LaunchDuration => launchDuration;

        [SerializeField] private float crumpleDuration = 3.0f;
        public float CrumpleDuration => crumpleDuration;

        // ==================== BOW SETTINGS ====================
        [Header("Bow")]
        [SerializeField] private float minDrawTime = 0.15f;
        public float MinDrawTime => minDrawTime;

        [SerializeField] private float maxDrawTime = 1.2f;
        public float MaxDrawTime => maxDrawTime;

        [SerializeField] private float minArrowSpeed = 20f;
        public float MinArrowSpeed => minArrowSpeed;

        [SerializeField] private float maxArrowSpeed = 50f;
        public float MaxArrowSpeed => maxArrowSpeed;

        [SerializeField] private float swayStartTime = 2.5f;
        public float SwayStartTime => swayStartTime;

        [SerializeField] private float maxSwayAngle = 4f;
        public float MaxSwayAngle => maxSwayAngle;

        // ==================== HYPER ARMOR SETTINGS ====================
        [Header("Hyper Armor")]
        [Tooltip("Hyper armor başlama zamanı (animasyon yüzdesi)")]
        [SerializeField, Range(0f, 1f)] private float hyperArmorStart = 0.3f;
        public float HyperArmorStart => hyperArmorStart;

        [Tooltip("Hyper armor bitiş zamanı (animasyon yüzdesi)")]
        [SerializeField, Range(0f, 1f)] private float hyperArmorEnd = 0.7f;
        public float HyperArmorEnd => hyperArmorEnd;

        // ==================== VALIDATION ====================
        private void OnValidate()
        {
            // Ensure values are in valid ranges
            parryWindowEasy = Mathf.Max(0.1f, parryWindowEasy);
            parryWindowNormal = Mathf.Max(0.05f, parryWindowNormal);
            parryWindowHard = Mathf.Max(0.05f, parryWindowHard);
            parryWindowSekiro = Mathf.Max(0.05f, parryWindowSekiro);

            perfectDodgeWindow = Mathf.Max(0.03f, perfectDodgeWindow);
            comboResetTime = Mathf.Max(0.3f, comboResetTime);

            // Ensure hyper armor end > start
            if (hyperArmorEnd <= hyperArmorStart)
            {
                hyperArmorEnd = hyperArmorStart + 0.1f;
            }
        }
    }
}
