using UnityEngine;
using System.Collections;
using Dungeons.Utilities;

namespace Dungeons.Combat
{
    /// <summary>
    /// Combat slow-motion efektlerini yönetir.
    /// Parry, perfect dodge, critical hit gibi anlarda slow-mo uygular.
    /// </summary>
    public class CombatTimeManager : Singleton<CombatTimeManager>
    {

        [Header("Parry Slow-Mo")]
        [SerializeField] private float parrySlowMoScale = 0.1f;
        [SerializeField] private float parrySlowMoDuration = 0.4f;
        [SerializeField] private float parrySlowMoEaseIn = 0.05f;
        [SerializeField] private float parrySlowMoEaseOut = 0.15f;

        [Header("Perfect Dodge Slow-Mo")]
        [SerializeField] private float dodgeSlowMoScale = 0.2f;
        [SerializeField] private float dodgeSlowMoDuration = 0.3f;

        [Header("Hit Freeze")]
        [SerializeField] private float hitFreezeScale = 0.0f;
        [SerializeField] private float hitFreezeDuration = 0.05f;

        // State
        private Coroutine _currentSlowMo;
        private float _targetTimeScale = 1f;
        private float _originalFixedDeltaTime;
        private bool _isSlowMoActive;

        public bool IsSlowMoActive => _isSlowMoActive;
        public float CurrentTimeScale => Time.timeScale;

        // Events
        public System.Action<float> OnSlowMoStart;
        public System.Action OnSlowMoEnd;

        protected override void Awake()
        {
            base.Awake();
            _originalFixedDeltaTime = Time.fixedDeltaTime;
        }

        protected override void OnDestroy()
        {
            // Restore time scale
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            base.OnDestroy();
        }

        /// <summary>
        /// Parry slow-motion efekti uygula
        /// </summary>
        public void ApplyParrySlowMo()
        {
            ApplySlowMo(parrySlowMoScale, parrySlowMoDuration, parrySlowMoEaseIn, parrySlowMoEaseOut);
            DLog.Log($"[CombatTimeManager] Parry slow-mo applied: {parrySlowMoScale}x for {parrySlowMoDuration}s");
        }

        /// <summary>
        /// Perfect dodge slow-motion efekti uygula (Witch Time benzeri)
        /// </summary>
        public void ApplyDodgeSlowMo()
        {
            ApplySlowMo(dodgeSlowMoScale, dodgeSlowMoDuration, 0.02f, 0.1f);
            DLog.Log($"[CombatTimeManager] Dodge slow-mo applied: {dodgeSlowMoScale}x for {dodgeSlowMoDuration}s");
        }

        /// <summary>
        /// Hit freeze efekti (hitstop)
        /// </summary>
        public void ApplyHitFreeze()
        {
            if (_currentSlowMo != null)
            {
                StopCoroutine(_currentSlowMo);
            }
            _currentSlowMo = StartCoroutine(HitFreezeCoroutine());
        }

        /// <summary>
        /// Özel slow-motion efekti
        /// </summary>
        public void ApplySlowMo(float timeScale, float duration, float easeIn = 0.05f, float easeOut = 0.1f)
        {
            if (_currentSlowMo != null)
            {
                StopCoroutine(_currentSlowMo);
            }
            _currentSlowMo = StartCoroutine(SlowMoCoroutine(timeScale, duration, easeIn, easeOut));
        }

        /// <summary>
        /// Slow-mo'yu anında durdur
        /// </summary>
        public void StopSlowMo()
        {
            if (_currentSlowMo != null)
            {
                StopCoroutine(_currentSlowMo);
                _currentSlowMo = null;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            _isSlowMoActive = false;
            OnSlowMoEnd?.Invoke();
        }

        private IEnumerator SlowMoCoroutine(float targetScale, float duration, float easeIn, float easeOut)
        {
            _isSlowMoActive = true;
            OnSlowMoStart?.Invoke(targetScale);

            // Ease in (normal -> slow)
            float elapsed = 0f;
            float startScale = Time.timeScale;

            while (elapsed < easeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / easeIn;
                Time.timeScale = Mathf.Lerp(startScale, targetScale, EaseOutQuad(t));
                Time.fixedDeltaTime = _originalFixedDeltaTime * Time.timeScale;
                yield return null;
            }

            Time.timeScale = targetScale;
            Time.fixedDeltaTime = _originalFixedDeltaTime * Time.timeScale;

            // Hold slow-mo
            yield return new WaitForSecondsRealtime(duration);

            // Ease out (slow -> normal)
            elapsed = 0f;
            startScale = Time.timeScale;

            while (elapsed < easeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / easeOut;
                Time.timeScale = Mathf.Lerp(startScale, 1f, EaseInQuad(t));
                Time.fixedDeltaTime = _originalFixedDeltaTime * Time.timeScale;
                yield return null;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            _isSlowMoActive = false;
            _currentSlowMo = null;
            OnSlowMoEnd?.Invoke();
        }

        private IEnumerator HitFreezeCoroutine()
        {
            _isSlowMoActive = true;

            // Instant freeze
            Time.timeScale = hitFreezeScale;
            Time.fixedDeltaTime = _originalFixedDeltaTime * Mathf.Max(0.01f, hitFreezeScale);

            yield return new WaitForSecondsRealtime(hitFreezeDuration);

            // Instant restore
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            _isSlowMoActive = false;
            _currentSlowMo = null;
        }

        // Easing functions
        private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private float EaseInQuad(float t) => t * t;
        private float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }
}
