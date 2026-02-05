using System;
using UnityEngine;
using Dungeons.Combat;

namespace Dungeons.Combat.Utility
{
    /// <summary>
    /// CombatSettings'ten deger yukleme islemlerini merkezi hale getirir.
    /// Her component kendi ihtiyaci olan degerleri bu loader uzerinden alir.
    /// Null-safe erisim ve fallback degerleri saglar.
    /// </summary>
    public static class CombatSettingsLoader
    {
        /// <summary>
        /// CombatSettings mevcut mu kontrol eder.
        /// </summary>
        public static bool IsAvailable => CombatSettings.Instance != null;

        /// <summary>
        /// CombatSettings instance'ina guvenli erisim saglar.
        /// </summary>
        public static CombatSettings Settings => CombatSettings.Instance;

        /// <summary>
        /// Guvenli float deger alir - CombatSettings yoksa fallback doner.
        /// </summary>
        /// <param name="selector">CombatSettings'ten degeri secen fonksiyon</param>
        /// <param name="fallback">CombatSettings yoksa kullanilacak varsayilan deger</param>
        /// <returns>Seçilen deger veya fallback</returns>
        public static float GetFloat(Func<CombatSettings, float> selector, float fallback)
        {
            return IsAvailable ? selector(Settings) : fallback;
        }

        /// <summary>
        /// Guvenli int deger alir - CombatSettings yoksa fallback doner.
        /// </summary>
        public static int GetInt(Func<CombatSettings, int> selector, int fallback)
        {
            return IsAvailable ? selector(Settings) : fallback;
        }

        /// <summary>
        /// Guvenli bool deger alir - CombatSettings yoksa fallback doner.
        /// </summary>
        public static bool GetBool(Func<CombatSettings, bool> selector, bool fallback)
        {
            return IsAvailable ? selector(Settings) : fallback;
        }

        // ==================== COMMON SHORTCUTS ====================
        // Sik kullanilan degerler icin kisa erisim metodlari

        /// <summary>
        /// Parry window suresi (zorluga gore)
        /// </summary>
        public static float ParryWindow => GetFloat(s => s.GetParryWindow(), 0.18f);

        /// <summary>
        /// Dodge suresi
        /// </summary>
        public static float DodgeDuration => GetFloat(s => s.DodgeDuration, 0.5f);

        /// <summary>
        /// Dodge i-frame baslangici
        /// </summary>
        public static float DodgeIFrameStart => GetFloat(s => s.DodgeIFrameStart, 0.05f);

        /// <summary>
        /// Dodge i-frame suresi
        /// </summary>
        public static float DodgeIFrameDuration => GetFloat(s => s.DodgeIFrameDuration, 0.33f);

        /// <summary>
        /// Dodge mesafesi
        /// </summary>
        public static float DodgeDistance => GetFloat(s => s.DodgeDistance, 3.5f);

        /// <summary>
        /// Dodge stamina maliyeti
        /// </summary>
        public static float DodgeStamina => GetFloat(s => s.DodgeStamina, 25f);

        /// <summary>
        /// Light attack stamina maliyeti
        /// </summary>
        public static float LightAttackStamina => GetFloat(s => s.LightAttackStamina, 15f);

        /// <summary>
        /// Heavy attack stamina maliyeti
        /// </summary>
        public static float HeavyAttackStamina => GetFloat(s => s.HeavyAttackStamina, 30f);

        /// <summary>
        /// Stamina regen hizi
        /// </summary>
        public static float StaminaRegenRate => GetFloat(s => s.StaminaRegenRate, 20f);

        /// <summary>
        /// Stamina regen gecikmesi
        /// </summary>
        public static float StaminaRegenDelay => GetFloat(s => s.StaminaRegenDelay, 1.5f);

        /// <summary>
        /// Combo reset suresi
        /// </summary>
        public static float ComboResetTime => GetFloat(s => s.ComboResetTime, 0.8f);

        /// <summary>
        /// Parry sonrasi stun suresi
        /// </summary>
        public static float ParryStunDuration => GetFloat(s => s.ParryStunDuration, 1.5f);

        /// <summary>
        /// Perfect dodge window
        /// </summary>
        public static float PerfectDodgeWindow => GetFloat(s => s.PerfectDodgeWindow, 0.08f);

        /// <summary>
        /// Hyper armor baslangic zamani (animasyon yuzdesi)
        /// </summary>
        public static float HyperArmorStart => GetFloat(s => s.HyperArmorStart, 0.3f);

        /// <summary>
        /// Hyper armor bitis zamani (animasyon yuzdesi)
        /// </summary>
        public static float HyperArmorEnd => GetFloat(s => s.HyperArmorEnd, 0.7f);
    }
}
