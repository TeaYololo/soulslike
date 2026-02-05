using UnityEngine;
using System.Collections;
using Dungeons.Data;
using Dungeons.Utilities;

namespace Dungeons.Combat
{
    /// <summary>
    /// Hitstop sistemi - Vuruş anında kısa duraklama efekti.
    /// Singleton olarak çalışır, her yerden erişilebilir.
    /// </summary>
    public class HitstopSystem : Singleton<HitstopSystem>
    {

        [Header("Hitstop Ayarları")]
        [SerializeField] private bool enableHitstop = true;

        [Tooltip("Hafif saldırı duraklama süresi")]
        [SerializeField] private float lightHitDuration = 0.03f;

        [Tooltip("Ağır saldırı duraklama süresi")]
        [SerializeField] private float heavyHitDuration = 0.06f;

        [Tooltip("Kritik vuruş duraklama süresi")]
        [SerializeField] private float criticalHitDuration = 0.1f;

        [Tooltip("Parry duraklama süresi")]
        [SerializeField] private float parryHitDuration = 0.12f;

        [Tooltip("Son vuruş (killing blow) duraklama süresi")]
        [SerializeField] private float killingBlowDuration = 0.15f;

        [Header("Gelişmiş Ayarlar")]
        [Tooltip("Minimum süre aralığı (çok sık hitstop önleme)")]
        [SerializeField] private float minTimeBetweenHitstops = 0.1f;

        [Tooltip("Hitstop sırasında timeScale değeri (0 = tam dur)")]
        [SerializeField, Range(0f, 0.1f)] private float hitstopTimeScale = 0f;

        // State
        private bool _isHitstopActive = false;
        private float _lastHitstopTime = -1f;
        private Coroutine _currentHitstop;


        #region Public Methods

        /// <summary>
        /// Hitstop tetikle (basit)
        /// </summary>
        public void TriggerHitstop(HitstopType type)
        {
            if (!enableHitstop) return;
            if (_isHitstopActive) return;
            if (Time.unscaledTime - _lastHitstopTime < minTimeBetweenHitstops) return;

            float duration = GetDurationForType(type);
            StartHitstop(duration);
        }

        /// <summary>
        /// Hitstop tetikle (özel süre)
        /// </summary>
        public void TriggerHitstop(float duration)
        {
            if (!enableHitstop) return;
            if (_isHitstopActive) return;
            if (Time.unscaledTime - _lastHitstopTime < minTimeBetweenHitstops) return;

            StartHitstop(duration);
        }

        /// <summary>
        /// DamageInfo'dan otomatik hitstop
        /// </summary>
        public void TriggerFromDamage(DamageInfo damageInfo, bool isKillingBlow = false)
        {
            if (!enableHitstop) return;

            HitstopType type;

            if (isKillingBlow)
            {
                type = HitstopType.KillingBlow;
            }
            else if (damageInfo.IsParried)
            {
                type = HitstopType.Parry;
            }
            else if (damageInfo.IsCritical)
            {
                type = HitstopType.Critical;
            }
            else if (damageInfo.AttackType == AttackType.HeavyAttack)
            {
                type = HitstopType.Heavy;
            }
            else
            {
                type = HitstopType.Light;
            }

            TriggerHitstop(type);
        }

        /// <summary>
        /// Hitstop'u hemen durdur
        /// </summary>
        public void CancelHitstop()
        {
            if (_currentHitstop != null)
            {
                StopCoroutine(_currentHitstop);
                _currentHitstop = null;
            }

            Time.timeScale = 1f;
            _isHitstopActive = false;
        }

        /// <summary>
        /// Hitstop aktif mi?
        /// </summary>
        public bool IsHitstopActive => _isHitstopActive;

        #endregion

        #region Private Methods

        private void StartHitstop(float duration)
        {
            if (_currentHitstop != null)
            {
                StopCoroutine(_currentHitstop);
            }

            _currentHitstop = StartCoroutine(HitstopCoroutine(duration));
        }

        private IEnumerator HitstopCoroutine(float duration)
        {
            _isHitstopActive = true;
            _lastHitstopTime = Time.unscaledTime;

            // Zamanı durdur
            float originalTimeScale = Time.timeScale;
            Time.timeScale = hitstopTimeScale;

            // Bekle (unscaled time kullan çünkü timeScale 0)
            yield return new WaitForSecondsRealtime(duration);

            // Zamanı geri getir
            Time.timeScale = originalTimeScale;
            _isHitstopActive = false;
            _currentHitstop = null;
        }

        private float GetDurationForType(HitstopType type)
        {
            return type switch
            {
                HitstopType.Light => lightHitDuration,
                HitstopType.Heavy => heavyHitDuration,
                HitstopType.Critical => criticalHitDuration,
                HitstopType.Parry => parryHitDuration,
                HitstopType.KillingBlow => killingBlowDuration,
                _ => lightHitDuration
            };
        }

        #endregion

        protected override void OnDestroy()
        {
            // Oyun kapanırken timeScale'i resetle
            Time.timeScale = 1f;
            base.OnDestroy();
        }
    }
}
