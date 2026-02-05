using UnityEngine;

namespace Dungeons.Core.Utilities
{
    /// <summary>
    /// Animator parameter hash'leri - Magic string'leri önler.
    /// StringToHash compile-time'da çağrılır, runtime'da sadece int karşılaştırması yapılır.
    ///
    /// Kullanım:
    /// animator.SetTrigger(AnimatorHash.Death);  // "Death" yerine
    /// animator.SetBool(AnimatorHash.IsBlocking, true);
    ///
    /// NOT: Bu hash'ler PlayerAnimator.controller ile senkronize tutulmalıdır.
    /// ✅ = Animator'de mevcut
    /// ⚠️ = Animator'e eklenmesi gerekiyor (SafeSet metodları ile güvenli kullanılabilir)
    /// </summary>
    public static class AnimatorHash
    {
        // ==================== TRIGGERS ====================

        // Combat - Mevcut ✅
        public static readonly int LightAttack = Animator.StringToHash("LightAttack");    // ✅
        public static readonly int HeavyAttack = Animator.StringToHash("HeavyAttack");    // ✅
        public static readonly int Dodge = Animator.StringToHash("Dodge");                 // ✅
        public static readonly int Hit = Animator.StringToHash("Hit");                     // ✅
        public static readonly int Death = Animator.StringToHash("Death");                 // ✅
        public static readonly int Jump = Animator.StringToHash("Jump");                   // ✅
        public static readonly int Land = Animator.StringToHash("Land");                   // ✅

        // Combat - Gelecek için ⚠️
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int ChargedAttack = Animator.StringToHash("ChargedAttack"); // ⚠️ UNUSED
        public static readonly int Parry = Animator.StringToHash("Parry");                 // ⚠️
        public static readonly int ParrySuccess = Animator.StringToHash("ParrySuccess");   // ⚠️
        public static readonly int BlockHit = Animator.StringToHash("BlockHit");           // ⚠️
        public static readonly int GuardBreak = Animator.StringToHash("GuardBreak");       // ⚠️

        // Hit Reactions - Gelecek için ⚠️
        public static readonly int Flinch = Animator.StringToHash("Flinch");               // ✅ USED
        public static readonly int Stagger = Animator.StringToHash("Stagger");             // ✅ USED
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int HeavyStagger = Animator.StringToHash("HeavyStagger");   // ⚠️ UNUSED
        public static readonly int Knockback = Animator.StringToHash("Knockback");         // ✅ USED
        public static readonly int Knockdown = Animator.StringToHash("Knockdown");         // ✅ USED
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int Launch = Animator.StringToHash("Launch");               // ⚠️ UNUSED
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int Crumple = Animator.StringToHash("Crumple");             // ⚠️ UNUSED
        public static readonly int GetUp = Animator.StringToHash("GetUp");                 // ✅ USED

        // Critical Attacks - Gelecek için ⚠️
        public static readonly int Backstab = Animator.StringToHash("Backstab");           // ✅ USED
        public static readonly int Riposte = Animator.StringToHash("Riposte");             // ✅ USED
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int BackstabVictim = Animator.StringToHash("BackstabVictim"); // ⚠️ UNUSED
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int RiposteVictim = Animator.StringToHash("RiposteVictim"); // ⚠️ UNUSED

        // Character State
        public static readonly int Respawn = Animator.StringToHash("Respawn");             // ⚠️
        public static readonly int Interact = Animator.StringToHash("Interact");           // ⚠️
        public static readonly int Revive = Animator.StringToHash("Revive");               // ⚠️
        public static readonly int Kick = Animator.StringToHash("Kick");                   // ⚠️

        // Enemy/AI Combat
        public static readonly int Attack = Animator.StringToHash("Attack");               // ⚠️

        // Animal Specific
        public static readonly int Eat = Animator.StringToHash("Eat");                     // ⚠️
        public static readonly int Die = Animator.StringToHash("Die");                     // ⚠️ (alias: Death)
        public static readonly int Destroy = Animator.StringToHash("Destroy");             // ✅ Network sync için

        // Ranged - Mevcut ✅ (isimler Animator'e göre)
        public static readonly int DrawBow = Animator.StringToHash("DrawBow");             // ✅
        public static readonly int ShootBow = Animator.StringToHash("ShootBow");           // ✅ (ReleaseBow yerine)
        public static readonly int CancelDraw = Animator.StringToHash("CancelDraw");       // ⚠️

        // Backward compatibility alias
        public static readonly int ReleaseBow = ShootBow;  // Eski kod için alias

        // ==================== BOOLEANS ====================

        // Mevcut ✅
        public static readonly int IsBlocking = Animator.StringToHash("IsBlocking");       // ✅
        public static readonly int IsAiming = Animator.StringToHash("IsAiming");           // ✅
        public static readonly int IsGrounded = Animator.StringToHash("IsGrounded");       // ✅
        public static readonly int IsDead = Animator.StringToHash("IsDead");               // ✅
        public static readonly int IsRunning = Animator.StringToHash("IsRunning");         // ✅ (IsSprinting yerine)
        public static readonly int IsLocked = Animator.StringToHash("IsLocked");           // ✅
        public static readonly int HasWeapon = Animator.StringToHash("HasWeapon");         // ✅
        public static readonly int HasShield = Animator.StringToHash("HasShield");         // ✅
        public static readonly int BowEquipped = Animator.StringToHash("BowEquipped");     // ✅
        public static readonly int IsAttacking = Animator.StringToHash("IsAttacking");     // ✅

        // Backward compatibility alias
        public static readonly int IsSprinting = IsRunning;  // Eski kod için alias

        // Network/State - Mevcut ✅
        public static readonly int IsStunned = Animator.StringToHash("IsStunned");         // ✅ Network sync için

        // Gelecek için ⚠️
        public static readonly int IsMoving = Animator.StringToHash("IsMoving");           // ✅ USED
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int IsCrouching = Animator.StringToHash("IsCrouching");     // ⚠️ UNUSED
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int IsInCombat = Animator.StringToHash("IsInCombat");       // ⚠️ UNUSED

        // ==================== INTEGERS ====================

        // Mevcut ✅
        public static readonly int ComboIndex = Animator.StringToHash("ComboIndex");       // ✅
        public static readonly int WeaponCategory = Animator.StringToHash("WeaponCategory"); // ✅
        public static readonly int AttackIndex = Animator.StringToHash("AttackIndex");     // ✅
        public static readonly int IdleIndex = Animator.StringToHash("IdleIndex");         // ✅ Idle varyasyonları için

        // Gelecek için ⚠️
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int MovementState = Animator.StringToHash("MovementState"); // ⚠️ UNUSED
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int CombatState = Animator.StringToHash("CombatState");     // ⚠️ UNUSED

        // ==================== FLOATS ====================

        // Mevcut ✅ (isimler Animator'e göre)
        public static readonly int Speed = Animator.StringToHash("Speed");                 // ✅
        public static readonly int SpeedX = Animator.StringToHash("SpeedX");               // ✅ (MoveX yerine)
        public static readonly int SpeedY = Animator.StringToHash("SpeedY");               // ✅ (MoveY yerine)
        public static readonly int DrawAmount = Animator.StringToHash("DrawAmount");       // ✅ (DrawProgress yerine)

        // Backward compatibility aliases
        public static readonly int MoveX = SpeedX;       // Eski kod için alias
        public static readonly int MoveY = SpeedY;       // Eski kod için alias
        public static readonly int DrawProgress = DrawAmount;  // Eski kod için alias

        // Gelecek için ⚠️
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int Health = Animator.StringToHash("Health");               // ⚠️ UNUSED
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int Stamina = Animator.StringToHash("Stamina");             // ⚠️ UNUSED

        // Animal Specific Floats
        public static readonly int Vert = Animator.StringToHash("Vert");                   // ⚠️ (Vertical movement)
        public static readonly int State = Animator.StringToHash("State");                 // ⚠️ (Animal state)

        // Locomotion Blend Tree
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int VelocityX = Animator.StringToHash("VelocityX");         // ⚠️ UNUSED
        [System.Obsolete("Kullanılmıyor — gelecek sürümde kaldırılabilir")]
        public static readonly int VelocityZ = Animator.StringToHash("VelocityZ");         // ⚠️ UNUSED

        // ==================== LAYER NAMES ====================

        public const string BaseLayer = "Base Layer";
        public const string Combat1HLayer = "Combat_1H";
        public const string Combat2HLayer = "Combat_2H";
        public const string CombatPolearmLayer = "Combat_Polearm";
        public const string CombatBowLayer = "Combat_Bow";
        public const string UpperBodyLayer = "Upper Body Idle";
        public const string CombatUpperBodyLayer = "Combat_UpperBody";
        public const string BowLocomotionLayer = "Bow_Locomotion";  // Yeni eklendi
    }

    /// <summary>
    /// Animator extension metodları
    /// </summary>
    public static class AnimatorExtensions
    {
        // Cache per RuntimeAnimatorController — animator.parameters allocates a new array every call
        private static readonly System.Collections.Generic.Dictionary<RuntimeAnimatorController, System.Collections.Generic.HashSet<int>> _paramHashCache = new();
        private static readonly System.Collections.Generic.Dictionary<RuntimeAnimatorController, System.Collections.Generic.HashSet<string>> _paramNameCache = new();

        /// <summary>
        /// Animator'da parametre var mı kontrol et (cached)
        /// </summary>
        public static bool HasParameter(this Animator animator, int paramHash)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;

            var controller = animator.runtimeAnimatorController;
            if (!_paramHashCache.TryGetValue(controller, out var hashSet))
            {
                hashSet = new System.Collections.Generic.HashSet<int>();
                var parameters = animator.parameters;
                for (int i = 0; i < parameters.Length; i++)
                    hashSet.Add(parameters[i].nameHash);
                _paramHashCache[controller] = hashSet;
            }

            return hashSet.Contains(paramHash);
        }

        /// <summary>
        /// Animator'da parametre var mı kontrol et (string)
        /// </summary>
        public static bool HasParameter(this Animator animator, string paramName)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;

            var controller = animator.runtimeAnimatorController;
            if (!_paramNameCache.TryGetValue(controller, out var nameSet))
            {
                nameSet = new System.Collections.Generic.HashSet<string>();
                var parameters = animator.parameters;
                for (int i = 0; i < parameters.Length; i++)
                    nameSet.Add(parameters[i].name);
                _paramNameCache[controller] = nameSet;
            }

            return nameSet.Contains(paramName);
        }

        /// <summary>
        /// Güvenli SetTrigger - parametre yoksa hata vermez
        /// </summary>
        public static void SafeSetTrigger(this Animator animator, int paramHash)
        {
            if (animator != null && animator.HasParameter(paramHash))
            {
                animator.SetTrigger(paramHash);
            }
        }

        /// <summary>
        /// Güvenli SetBool - parametre yoksa hata vermez
        /// </summary>
        public static void SafeSetBool(this Animator animator, int paramHash, bool value)
        {
            if (animator != null && animator.HasParameter(paramHash))
            {
                animator.SetBool(paramHash, value);
            }
        }

        /// <summary>
        /// Güvenli SetInteger - parametre yoksa hata vermez
        /// </summary>
        public static void SafeSetInteger(this Animator animator, int paramHash, int value)
        {
            if (animator != null && animator.HasParameter(paramHash))
            {
                animator.SetInteger(paramHash, value);
            }
        }

        /// <summary>
        /// Güvenli SetFloat - parametre yoksa hata vermez
        /// </summary>
        public static void SafeSetFloat(this Animator animator, int paramHash, float value)
        {
            if (animator != null && animator.HasParameter(paramHash))
            {
                animator.SetFloat(paramHash, value);
            }
        }
    }
}
