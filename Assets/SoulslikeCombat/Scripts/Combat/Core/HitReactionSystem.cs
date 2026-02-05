using Dungeons.Utilities;
using UnityEngine;
using System.Collections;
using Dungeons.Data;
using Dungeons.Core;

namespace Dungeons.Combat
{
    /// <summary>
    /// Hit Reaction sistemi.
    /// Gelen hasara ve poise durumuna göre farklı tepkiler üretir.
    ///
    /// Tepki Tipleri:
    /// - None: Poise yeterli, tepki yok (super armor)
    /// - Flinch: Hafif sarsılma (animasyon kesilmez)
    /// - Stagger: Orta sarsılma (animasyon kesilir)
    /// - HeavyStagger: Büyük sarsılma (uzun recovery)
    /// - Knockback: Geri itilme
    /// - Knockdown: Yere düşme
    /// - Launch: Havaya fırlatma
    /// - Crumple: Yavaşça yere çökme (killing blow)
    /// </summary>
    public class HitReactionSystem : MonoBehaviour
    {
        [Header("Reaction Durations")]
        [SerializeField] private float flinchDuration = 0.15f;
        [SerializeField] private float staggerDuration = 0.4f;
        [SerializeField] private float heavyStaggerDuration = 0.8f;
        [SerializeField] private float knockbackDuration = 0.6f;
        [SerializeField] private float knockdownDuration = 2.0f;
        [SerializeField] private float launchDuration = 1.5f;
        [SerializeField] private float crumpleDuration = 3.0f;

        [Header("Physics")]
        [Tooltip("Knockback kuvveti")]
        [SerializeField] private float knockbackForce = 5f;

        [Tooltip("Launch yukarı kuvveti")]
        [SerializeField] private float launchUpForce = 8f;

        [Tooltip("Launch geri kuvveti")]
        [SerializeField] private float launchBackForce = 3f;

        [Header("Animation")]
        [SerializeField] private string flinchTrigger = "Flinch";
        [SerializeField] private string staggerTrigger = "Stagger";
        [SerializeField] private string heavyStaggerTrigger = "HeavyStagger";
        [SerializeField] private string knockbackTrigger = "Knockback";
        [SerializeField] private string knockdownTrigger = "Knockdown";
        [SerializeField] private string launchTrigger = "Launch";
        [SerializeField] private string crumpleTrigger = "Crumple";
        [SerializeField] private string getUpTrigger = "GetUp";

        // State
        private HitReaction _currentReaction = HitReaction.None;
        private bool _isReacting;
        private float _reactionEndTime;
        private Coroutine _reactionCoroutine;

        // Components
        private Animator _animator;
        private CharacterController _controller;
        private Rigidbody _rigidbody;
        private PoiseSystem _poiseSystem;
        private AttackExecutor _attackExecutor;
        private Dungeons.Player.PlayerNetworkAnimEvents _animEvents;

        // Properties
        public HitReaction CurrentReaction => _currentReaction;
        public bool IsReacting => _isReacting;
        public bool IsGrounded => _controller?.isGrounded ?? true;

        // Events
        public System.Action<HitReaction> OnReactionStart;
        public System.Action<HitReaction> OnReactionEnd;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _controller = GetComponent<CharacterController>();
            _rigidbody = GetComponent<Rigidbody>();
            _poiseSystem = GetComponent<PoiseSystem>();
            _attackExecutor = GetComponent<AttackExecutor>();
            _animEvents = GetComponent<Dungeons.Player.PlayerNetworkAnimEvents>();

            if (_poiseSystem != null)
            {
                _poiseSystem.OnHitReactionDetermined += HandleHitReaction;
            }
        }

        private void OnDestroy()
        {
            if (_poiseSystem != null)
            {
                _poiseSystem.OnHitReactionDetermined -= HandleHitReaction;
            }
        }

        private void Update()
        {
            if (_isReacting && Time.time >= _reactionEndTime)
            {
                EndReaction();
            }
        }

        /// <summary>
        /// Hit reaction uygula
        /// </summary>
        public void ApplyHitReaction(HitReaction reaction, Vector3 hitDirection)
        {
            // Souls-like: Hyper Armor kontrolü
            // Heavy attack'in Active phase'inde Flinch/Stagger/HeavyStagger ignore edilir
            // Knockback ve üstü (priority 4+) hâlâ etkili
            if (_attackExecutor != null && _attackExecutor.HasHyperArmor())
            {
                int reactionPriority = GetReactionPriority(reaction);
                if (reactionPriority <= 3) // Flinch(1), Stagger(2), HeavyStagger(3)
                {
                    DLog.Log($"[HitReaction] HYPER ARMOR - {reaction} ignored during heavy attack!");
                    return;
                }
            }

            // Mevcut tepkiden daha öncelikli mi?
            if (_isReacting && GetReactionPriority(reaction) <= GetReactionPriority(_currentReaction))
            {
                // Daha düşük öncelikli, ignore et
                return;
            }

            // Mevcut tepkiyi iptal et
            if (_reactionCoroutine != null)
            {
                StopCoroutine(_reactionCoroutine);
            }

            _currentReaction = reaction;
            _isReacting = reaction != HitReaction.None;

            if (!_isReacting) return;

            float duration = GetReactionDuration(reaction);
            _reactionEndTime = Time.time + duration;

            // Animasyon tetikle
            TriggerAnimation(reaction);

            // Fizik uygula
            ApplyPhysics(reaction, hitDirection);

            // Event
            OnReactionStart?.Invoke(reaction);

            DLog.Log($"[HitReaction] Applied: {reaction}, Duration: {duration}s");
        }

        /// <summary>
        /// PoiseSystem'den gelen reaction'ı işle
        /// </summary>
        private void HandleHitReaction(HitReaction reaction)
        {
            if (reaction == HitReaction.None) return;

            // Default direction (backward)
            Vector3 hitDir = -transform.forward;

            ApplyHitReaction(reaction, hitDir);
        }

        /// <summary>
        /// Hit direction ile reaction uygula (DamageInfo'dan)
        /// </summary>
        public void ApplyHitReactionFromDamage(DamageInfo damageInfo, HitReaction reaction)
        {
            Vector3 hitDir = Vector3.zero;

            if (damageInfo.Attacker != null)
            {
                hitDir = (transform.position - damageInfo.Attacker.transform.position).normalized;
            }
            else if (damageInfo.HitPoint != Vector3.zero)
            {
                hitDir = (transform.position - damageInfo.HitPoint).normalized;
            }
            else
            {
                hitDir = -transform.forward;
            }

            hitDir.y = 0;
            ApplyHitReaction(reaction, hitDir);
        }

        /// <summary>
        /// Tepkiyi bitir
        /// </summary>
        private void EndReaction()
        {
            HitReaction endedReaction = _currentReaction;
            _currentReaction = HitReaction.None;
            _isReacting = false;

            // GetUp animasyonu (knockdown/launch için)
            if (endedReaction == HitReaction.Knockdown || endedReaction == HitReaction.Launch)
            {
                if (_animEvents != null)
                    _animEvents.PlayGetUp();
                else
                    _animator?.SetTrigger(getUpTrigger);
            }

            OnReactionEnd?.Invoke(endedReaction);
            DLog.Log($"[HitReaction] Ended: {endedReaction}");
        }

        /// <summary>
        /// Animasyon tetikle (Network-safe)
        /// </summary>
        private void TriggerAnimation(HitReaction reaction)
        {
            DLog.Log($"[HitReaction] TriggerAnimation: {reaction}, _animEvents={_animEvents != null}, _animator={_animator != null}");
            if (_animator == null) return;

            // Network-safe: PlayerNetworkAnimEvents varsa onu kullan
            if (_animEvents != null)
            {
                switch (reaction)
                {
                    case HitReaction.Flinch:
                        _animEvents.PlayFlinch();
                        break;
                    case HitReaction.Stagger:
                        _animEvents.PlayStagger();
                        break;
                    case HitReaction.HeavyStagger:
                        _animEvents.FireTrigger(heavyStaggerTrigger);
                        break;
                    case HitReaction.Knockback:
                        _animEvents.PlayKnockback();
                        break;
                    case HitReaction.Knockdown:
                        _animEvents.PlayKnockdown();
                        break;
                    case HitReaction.Launch:
                        _animEvents.FireTrigger(launchTrigger);
                        break;
                    case HitReaction.Crumple:
                        _animEvents.FireTrigger(crumpleTrigger);
                        break;
                }
                return;
            }

            // Fallback: Direkt animator (NPC/Enemy için)
            string trigger = reaction switch
            {
                HitReaction.Flinch => flinchTrigger,
                HitReaction.Stagger => staggerTrigger,
                HitReaction.HeavyStagger => heavyStaggerTrigger,
                HitReaction.Knockback => knockbackTrigger,
                HitReaction.Knockdown => knockdownTrigger,
                HitReaction.Launch => launchTrigger,
                HitReaction.Crumple => crumpleTrigger,
                _ => null
            };

            if (!string.IsNullOrEmpty(trigger))
            {
                _animator.SetTrigger(trigger);
            }
        }

        /// <summary>
        /// Fizik efektlerini uygula
        /// </summary>
        private void ApplyPhysics(HitReaction reaction, Vector3 hitDirection)
        {
            switch (reaction)
            {
                case HitReaction.Knockback:
                    _reactionCoroutine = StartCoroutine(KnockbackCoroutine(hitDirection));
                    break;

                case HitReaction.Knockdown:
                case HitReaction.Launch:
                    _reactionCoroutine = StartCoroutine(LaunchCoroutine(hitDirection, reaction == HitReaction.Launch));
                    break;
            }
        }

        /// <summary>
        /// Knockback coroutine
        /// </summary>
        private IEnumerator KnockbackCoroutine(Vector3 direction)
        {
            float elapsed = 0f;
            float duration = knockbackDuration;
            Vector3 knockbackDir = direction.normalized;
            knockbackDir.y = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float force = knockbackForce * (1f - t); // Zamanla azalan kuvvet

                if (_controller != null)
                {
                    _controller.Move(knockbackDir * force * Time.deltaTime);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Launch/Knockdown coroutine
        /// </summary>
        private IEnumerator LaunchCoroutine(Vector3 direction, bool isLaunch)
        {
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;

                Vector3 launchDir = direction.normalized * launchBackForce;
                if (isLaunch)
                {
                    launchDir.y = launchUpForce;
                }

                _rigidbody.AddForce(launchDir, ForceMode.Impulse);

                // Wait for landing
                yield return new WaitForSeconds(0.5f);

                while (!IsGrounded)
                {
                    yield return null;
                }

                _rigidbody.isKinematic = true;
            }
            else if (_controller != null)
            {
                // CharacterController ile simülasyon
                float elapsed = 0f;
                float duration = isLaunch ? launchDuration : knockdownDuration;
                Vector3 velocity = direction.normalized * launchBackForce;

                if (isLaunch)
                {
                    velocity.y = launchUpForce;
                }

                while (elapsed < duration * 0.5f)
                {
                    elapsed += Time.deltaTime;
                    velocity.y -= 20f * Time.deltaTime; // Gravity
                    _controller.Move(velocity * Time.deltaTime);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Tepki süresini al
        /// </summary>
        public float GetReactionDuration(HitReaction reaction)
        {
            return reaction switch
            {
                HitReaction.None => 0f,
                HitReaction.Flinch => flinchDuration,
                HitReaction.Stagger => staggerDuration,
                HitReaction.HeavyStagger => heavyStaggerDuration,
                HitReaction.Knockback => knockbackDuration,
                HitReaction.Knockdown => knockdownDuration,
                HitReaction.Launch => launchDuration,
                HitReaction.Crumple => crumpleDuration,
                _ => 0f
            };
        }

        /// <summary>
        /// Tepki önceliğini al (yüksek = daha öncelikli)
        /// </summary>
        private int GetReactionPriority(HitReaction reaction)
        {
            return reaction switch
            {
                HitReaction.None => 0,
                HitReaction.Flinch => 1,
                HitReaction.Stagger => 2,
                HitReaction.HeavyStagger => 3,
                HitReaction.Knockback => 4,
                HitReaction.Knockdown => 5,
                HitReaction.Launch => 6,
                HitReaction.Crumple => 7,
                _ => 0
            };
        }

        /// <summary>
        /// Tepkiyi zorla bitir (örn: i-frame için)
        /// </summary>
        public void ForceEndReaction()
        {
            if (_reactionCoroutine != null)
            {
                StopCoroutine(_reactionCoroutine);
            }
            EndReaction();
        }
    }
}
