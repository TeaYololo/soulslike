using UnityEngine;
using Dungeons.Data;

namespace Dungeons.Combat
{
    /// <summary>
    /// Hasar bilgisi taşıyan struct.
    /// Her saldırı bu bilgiyi üretir.
    /// </summary>
    [System.Serializable]
    public struct DamageInfo
    {
        public GameObject Attacker;
        public int BaseDamage;
        public int FinalDamage;
        public ElementType Element;
        public AttackType AttackType;
        public bool IsCritical;
        public bool IsBlocked;
        public bool IsParried;
        public float KnockbackForce;
        public float StaggerValue;
        public Vector3 HitPoint;
        public Vector3 HitDirection;
        public StatusEffect AppliedStatus;
        public float StatusDuration;

        // Advanced Combat System fields
        public bool IsBackstab;
        public bool IsRiposte;
        public HitZone HitZone;
        public float PoiseDamage;
        public bool IgnoresPoise; // Backstab/Riposte gibi saldırılar poise'ı bypass eder

        /// <summary>
        /// Bu hasar flinch/stagger'a neden olacak mı?
        /// </summary>
        public bool CausesFlinch => StaggerValue > 0 || PoiseDamage > 0;

        public static DamageInfo Create(GameObject attacker, int damage, ElementType element = ElementType.Physical)
        {
            return new DamageInfo
            {
                Attacker = attacker,
                BaseDamage = damage,
                FinalDamage = damage,
                Element = element,
                AttackType = AttackType.LightAttack,
                IsCritical = false,
                IsBlocked = false,
                IsParried = false,
                KnockbackForce = 0f,
                StaggerValue = 0f,
                HitPoint = Vector3.zero,
                HitDirection = Vector3.forward,
                AppliedStatus = StatusEffect.None,
                StatusDuration = 0f,
                // Advanced Combat
                IsBackstab = false,
                IsRiposte = false,
                HitZone = HitZone.Body,
                PoiseDamage = damage * 0.5f, // Default: hasar * 0.5
                IgnoresPoise = false
            };
        }

        /// <summary>
        /// Hit zone çarpanını uygula
        /// </summary>
        public void ApplyHitZoneMultiplier()
        {
            float multiplier = CombatSettings.Instance?.GetHitZoneMultiplier(HitZone) ?? 1f;
            FinalDamage = Mathf.RoundToInt(BaseDamage * multiplier);
        }

        /// <summary>
        /// Backstab hasarı oluştur
        /// </summary>
        public static DamageInfo CreateBackstab(GameObject attacker, int baseDamage)
        {
            var info = Create(attacker, baseDamage);
            info.IsBackstab = true;
            info.IsCritical = true;
            info.IgnoresPoise = true;
            info.FinalDamage = Mathf.RoundToInt(baseDamage * (CombatSettings.Instance?.BackstabDamageMultiplier ?? 3f));
            return info;
        }

        /// <summary>
        /// Riposte hasarı oluştur
        /// </summary>
        public static DamageInfo CreateRiposte(GameObject attacker, int baseDamage)
        {
            var info = Create(attacker, baseDamage);
            info.IsRiposte = true;
            info.IsCritical = true;
            info.IgnoresPoise = true;
            info.FinalDamage = Mathf.RoundToInt(baseDamage * (CombatSettings.Instance?.RiposteDamageMultiplier ?? 2.5f));
            return info;
        }
    }
}
