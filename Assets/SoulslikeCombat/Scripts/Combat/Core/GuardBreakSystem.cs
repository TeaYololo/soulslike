using Dungeons.Utilities;
using UnityEngine;
using System.Collections;
using Dungeons.Data;
using Dungeons.Core;

namespace Dungeons.Combat
{
    /// <summary>
    /// Guard Break sistemi.
    /// Block sırasında stamina tükenirse guard kırılır ve uzun stagger uygulanır.
    ///
    /// Block Balance:
    /// - Block %70 hasar engeller (önceki %100 yerine)
    /// - Block sırasında gelen hasar stamina'dan da kesilir
    /// - Stamina 0'a düşerse GUARD BREAK!
    /// </summary>
    public class GuardBreakSystem : MonoBehaviour
    {
        [Header("Guard Break Settings")]
        [Tooltip("Guard break stagger süresi")]
        [SerializeField] private float guardBreakStaggerDuration = 1.5f;

        [Tooltip("Guard break sonrası invincibility süresi (exploitation önleme)")]
        [SerializeField] private float guardBreakInvincibilityDuration = 0.5f;

        [Header("Block Balance")]
        [Tooltip("Block hasar azaltma (0.7 = %70)")]
        [SerializeField, Range(0f, 1f)] private float blockDamageReduction = 0.7f;

        [Tooltip("Block sırasında stamina hasar çarpanı")]
        [SerializeField] private float blockStaminaDamageRatio = 1.5f;

        [Tooltip("Chip damage (block edilse bile geçen hasar) çarpanı")]
        [SerializeField, Range(0f, 0.5f)] private float chipDamageRatio = 0.0f; // Optional

        [Header("Visual Feedback")]
        [SerializeField] private ParticleSystem guardBreakVFX;
        [SerializeField] private AudioClip guardBreakSound;

        // State
        private GuardState _currentState = GuardState.None;
        private float _guardBreakEndTime;
        private bool _isInvincible;
        private Coroutine _guardBreakCoroutine;

        // Components
        private Animator _animator;
        private DefenseSystem _defenseSystem;
        private AudioSource _audioSource;

        // Properties
        public GuardState CurrentState => _currentState;
        public bool IsGuardBroken => _currentState == GuardState.GuardBroken;
        public float BlockDamageReduction => blockDamageReduction;
        public float BlockStaminaDamageRatio => blockStaminaDamageRatio;

        // Events
        public System.Action OnGuardBreak;
        public System.Action OnGuardBreakEnd;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _defenseSystem = GetComponent<DefenseSystem>();
            _audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            // CombatSettings'ten değerleri al
            if (CombatSettings.Instance != null)
            {
                guardBreakStaggerDuration = CombatSettings.Instance.GuardBreakStaggerDuration;
                blockDamageReduction = CombatSettings.Instance.BlockDamageReduction;
                blockStaminaDamageRatio = CombatSettings.Instance.BlockStaminaDamageRatio;
            }
        }

        private void Update()
        {
            if (_currentState == GuardState.GuardBroken && Time.time >= _guardBreakEndTime)
            {
                EndGuardBreak();
            }
        }

        /// <summary>
        /// Block sırasında hasar işle.
        /// Returns: (Final damage, Stamina damage, Is Guard Broken)
        /// </summary>
        public (int finalDamage, float staminaDamage, bool guardBroken) ProcessBlockedDamage(
            int incomingDamage,
            float currentStamina)
        {
            // Hasar hesapla
            int reducedDamage = Mathf.RoundToInt(incomingDamage * (1f - blockDamageReduction));

            // Chip damage (opsiyonel)
            int chipDamage = Mathf.RoundToInt(incomingDamage * chipDamageRatio);
            int finalDamage = reducedDamage + chipDamage;

            // Stamina hasarı
            float staminaDamage = incomingDamage * blockStaminaDamageRatio;

            // Guard break kontrolü
            bool guardBroken = staminaDamage >= currentStamina;

            if (guardBroken)
            {
                TriggerGuardBreak();
                // Guard break olunca full damage
                finalDamage = incomingDamage;
            }

            return (finalDamage, staminaDamage, guardBroken);
        }

        /// <summary>
        /// Guard break tetikle
        /// </summary>
        public void TriggerGuardBreak()
        {
            if (_currentState == GuardState.GuardBroken) return;

            _currentState = GuardState.GuardBroken;
            _guardBreakEndTime = Time.time + guardBreakStaggerDuration;

            // Animasyon
            _animator?.SetTrigger("GuardBreak");

            // VFX
            if (guardBreakVFX != null)
            {
                guardBreakVFX.Play();
            }

            // Sound
            if (guardBreakSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(guardBreakSound);
            }

            // Hitstop efekti
            HitstopSystem.Instance?.TriggerHitstop(HitstopType.Heavy);

            // Event
            OnGuardBreak?.Invoke();

            // Event bus
            EventBus.Publish(new GuardBreakEvent
            {
                Target = gameObject,
                StaggerDuration = guardBreakStaggerDuration
            });

            DLog.Log($"[GuardBreak] GUARD BROKEN! Stagger duration: {guardBreakStaggerDuration}s");

            // Invincibility coroutine (optional, exploitation önleme)
            if (_guardBreakCoroutine != null)
            {
                StopCoroutine(_guardBreakCoroutine);
            }
            _guardBreakCoroutine = StartCoroutine(GuardBreakInvincibilityCoroutine());
        }

        /// <summary>
        /// Guard break bitir
        /// </summary>
        private void EndGuardBreak()
        {
            _currentState = GuardState.Recovering;

            // Kısa recovery sonrası normal duruma dön
            StartCoroutine(GuardBreakRecoveryCoroutine());

            OnGuardBreakEnd?.Invoke();
            DLog.Log("[GuardBreak] Guard break ended, recovering...");
        }

        /// <summary>
        /// Guard break invincibility (exploitation önleme)
        /// </summary>
        private IEnumerator GuardBreakInvincibilityCoroutine()
        {
            // Guard break animasyonunun başında kısa invincibility
            // Bu, guard break animation lock sırasında oyuncunun fazla hasar yememesini sağlar
            _isInvincible = true;
            yield return new WaitForSeconds(guardBreakInvincibilityDuration);
            _isInvincible = false;
        }

        /// <summary>
        /// Guard break recovery
        /// </summary>
        private IEnumerator GuardBreakRecoveryCoroutine()
        {
            yield return new WaitForSeconds(0.3f);
            _currentState = GuardState.None;
        }

        /// <summary>
        /// Guard durumunu set et
        /// </summary>
        public void SetGuardState(GuardState state)
        {
            if (_currentState == GuardState.GuardBroken) return; // Guard break override edilemez

            _currentState = state;
        }

        /// <summary>
        /// Guard break sırasında invincible mi?
        /// </summary>
        public bool IsInvincibleDuringGuardBreak()
        {
            return _currentState == GuardState.GuardBroken && _isInvincible;
        }
    }

    /// <summary>
    /// Guard break event
    /// </summary>
    public struct GuardBreakEvent
    {
        public GameObject Target;
        public float StaggerDuration;
    }
}
