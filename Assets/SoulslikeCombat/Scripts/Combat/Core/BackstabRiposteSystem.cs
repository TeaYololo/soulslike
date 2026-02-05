using Dungeons.Utilities;
using UnityEngine;
using System.Collections;
using Dungeons.Data;
using Dungeons.Data.Interfaces;
using Dungeons.Core;

namespace Dungeons.Combat
{
    /// <summary>
    /// Backstab ve Riposte sistemi.
    ///
    /// Backstab: Düşmanın arkasından saldırı (yüksek hasar, özel animasyon)
    /// Riposte: Başarılı parry sonrası yapılan saldırı (yüksek hasar, özel animasyon)
    ///
    /// Her iki saldırı da:
    /// - Yüksek hasar çarpanı
    /// - Özel animasyon
    /// - i-frame sağlar
    /// - Hedefi kilitle
    /// </summary>
    public class BackstabRiposteSystem : MonoBehaviour
    {
        [Header("Backstab Settings")]
        [Tooltip("Backstab yapılabilecek açı (derece)")]
        [SerializeField] private float backstabAngle = 45f;

        [Tooltip("Backstab mesafesi")]
        [SerializeField] private float backstabRange = 1.5f;

        [Tooltip("Backstab hasar çarpanı")]
        [SerializeField] private float backstabDamageMultiplier = 3f;

        [Tooltip("Backstab animasyon süresi")]
        [SerializeField] private float backstabDuration = 2.5f;

        [Header("Riposte Settings")]
        [Tooltip("Parry sonrası riposte penceresi")]
        [SerializeField] private float riposteWindow = 1.5f;

        [Tooltip("Riposte hasar çarpanı")]
        [SerializeField] private float riposteDamageMultiplier = 2.5f;

        [Tooltip("Riposte mesafesi")]
        [SerializeField] private float riposteRange = 2f;

        [Tooltip("Riposte animasyon süresi")]
        [SerializeField] private float riposteDuration = 2f;

        [Header("Detection")]
        [Tooltip("Backstab/Riposte hedef layer")]
        [SerializeField] private LayerMask targetLayer;

        [Header("Animation")]
        [SerializeField] private string backstabTrigger = "Backstab";
        [SerializeField] private string riposteTrigger = "Riposte";
        [SerializeField] private string victimBackstabTrigger = "BackstabVictim";
        [SerializeField] private string victimRiposteTrigger = "RiposteVictim";

        // State
        private BackstabState _currentState = BackstabState.None;
        private float _riposteEndTime;
        private bool _canRiposte;
        private GameObject _currentTarget;
        private Coroutine _criticalAttackCoroutine;

        // Components
        private Animator _animator;
        private PlayerCombat _playerCombat;
        private DefenseSystem _defenseSystem;

        // Static buffer for Physics NonAlloc (GC optimization)
        private static readonly Collider[] _overlapBuffer = new Collider[16];

        // Properties
        public BackstabState CurrentState => _currentState;
        public bool CanBackstab => _currentState == BackstabState.None && FindBackstabTarget() != null;
        public bool CanRiposte => _canRiposte && Time.time < _riposteEndTime && FindRiposteTarget() != null;
        public bool IsPerformingCriticalAttack => _currentState == BackstabState.Backstabbing || _currentState == BackstabState.Riposting;

        // Events
        public System.Action<GameObject, bool> OnCriticalAttackStart; // target, isBackstab
        public System.Action<GameObject, int> OnCriticalAttackHit; // target, damage
        public System.Action OnCriticalAttackEnd;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _playerCombat = GetComponent<PlayerCombat>();
            _defenseSystem = GetComponent<DefenseSystem>();

            // Parry success event'ini dinle
            if (_defenseSystem != null)
            {
                _defenseSystem.OnParryAttempt += OnParryAttempt;
            }
        }

        private void Start()
        {
            // CombatSettings'ten değerleri al
            if (CombatSettings.Instance != null)
            {
                backstabAngle = CombatSettings.Instance.BackstabAngle;
                backstabRange = CombatSettings.Instance.BackstabRange;
                backstabDamageMultiplier = CombatSettings.Instance.BackstabDamageMultiplier;
                riposteWindow = CombatSettings.Instance.RiposteWindow;
                riposteDamageMultiplier = CombatSettings.Instance.RiposteDamageMultiplier;
            }
        }

        private void OnDestroy()
        {
            if (_defenseSystem != null)
            {
                _defenseSystem.OnParryAttempt -= OnParryAttempt;
            }
        }

        private void Update()
        {
            // Riposte window timeout
            if (_canRiposte && Time.time >= _riposteEndTime)
            {
                _canRiposte = false;
            }
        }

        /// <summary>
        /// Backstab dene
        /// </summary>
        public bool TryBackstab()
        {
            if (_currentState != BackstabState.None) return false;

            GameObject target = FindBackstabTarget();
            if (target == null) return false;

            StartCoroutine(PerformBackstab(target));
            return true;
        }

        /// <summary>
        /// Riposte dene
        /// </summary>
        public bool TryRiposte()
        {
            if (!_canRiposte || _currentState != BackstabState.None) return false;
            if (Time.time >= _riposteEndTime) return false;

            GameObject target = FindRiposteTarget();
            if (target == null) return false;

            _canRiposte = false;
            StartCoroutine(PerformRiposte(target));
            return true;
        }

        /// <summary>
        /// Backstab hedefi bul
        /// </summary>
        private GameObject FindBackstabTarget()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, backstabRange, _overlapBuffer, targetLayer);

            for (int i = 0; i < hitCount; i++)
            {
                var col = _overlapBuffer[i];
                if (col.gameObject == gameObject) continue;

                // Arkada mı?
                Vector3 toTarget = (col.transform.position - transform.position).normalized;
                float angle = Vector3.Angle(col.transform.forward, toTarget);

                // Hedefin arkasındayız mı?
                if (angle < backstabAngle)
                {
                    // Backstab yapılabilir mi? (stagger, idle, vb.)
                    var targetCombat = col.GetComponent<ICombatant>();
                    if (targetCombat != null && CanBeBackstabbed(targetCombat))
                    {
                        return col.gameObject;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Riposte hedefi bul (staggered düşman)
        /// </summary>
        private GameObject FindRiposteTarget()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, riposteRange, _overlapBuffer, targetLayer);

            for (int i = 0; i < hitCount; i++)
            {
                var col = _overlapBuffer[i];
                if (col.gameObject == gameObject) continue;

                var targetCombat = col.GetComponent<ICombatant>();
                if (targetCombat != null && targetCombat.CurrentCombatState == CombatState.Staggered)
                {
                    // Önde mi?
                    Vector3 toTarget = (col.transform.position - transform.position).normalized;
                    float angle = Vector3.Angle(transform.forward, toTarget);

                    if (angle < 60f) // Önümüzde
                    {
                        return col.gameObject;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Hedef backstab yapılabilir mi?
        /// </summary>
        private bool CanBeBackstabbed(ICombatant target)
        {
            // Idle veya walking durumunda
            return target.CurrentCombatState == CombatState.Idle;
        }

        /// <summary>
        /// Backstab gerçekleştir
        /// </summary>
        private IEnumerator PerformBackstab(GameObject target)
        {
            _currentState = BackstabState.Backstabbing;
            _currentTarget = target;

            // Event
            OnCriticalAttackStart?.Invoke(target, true);

            // Pozisyonları ayarla
            PositionForCriticalAttack(target, true);

            // Animasyonlar
            _animator?.SetTrigger(backstabTrigger);

            var targetAnimator = target.GetComponent<Animator>();
            targetAnimator?.SetTrigger(victimBackstabTrigger);

            // Hedef state
            var targetCombat = target.GetComponent<ICombatant>();

            // i-frame (hem attacker hem victim)
            // TODO: Implement invincibility

            // Hasar (animasyonun ortasında)
            yield return new WaitForSeconds(backstabDuration * 0.5f);

            // Hasar hesapla ve uygula
            int baseDamage = _playerCombat?.Stats?.Attack ?? 50;
            int criticalDamage = Mathf.RoundToInt(baseDamage * backstabDamageMultiplier);

            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                // IDamageable.TakeDamage(int, ElementType) kullan
                damageable.TakeDamage(criticalDamage, ElementType.Physical);
            }

            // Hitstop
            HitstopSystem.Instance?.TriggerHitstop(HitstopType.KillingBlow);

            // Event
            OnCriticalAttackHit?.Invoke(target, criticalDamage);

            // Kalan animasyon
            yield return new WaitForSeconds(backstabDuration * 0.5f);

            // Bitir
            EndCriticalAttack();
        }

        /// <summary>
        /// Riposte gerçekleştir
        /// </summary>
        private IEnumerator PerformRiposte(GameObject target)
        {
            _currentState = BackstabState.Riposting;
            _currentTarget = target;

            // Event
            OnCriticalAttackStart?.Invoke(target, false);

            // Pozisyonları ayarla
            PositionForCriticalAttack(target, false);

            // Animasyonlar
            _animator?.SetTrigger(riposteTrigger);

            var targetAnimator = target.GetComponent<Animator>();
            targetAnimator?.SetTrigger(victimRiposteTrigger);

            // Hasar (animasyonun ortasında)
            yield return new WaitForSeconds(riposteDuration * 0.4f);

            // Hasar hesapla ve uygula
            int baseDamage = _playerCombat?.Stats?.Attack ?? 50;
            int criticalDamage = Mathf.RoundToInt(baseDamage * riposteDamageMultiplier);

            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                // IDamageable.TakeDamage(int, ElementType) kullan
                damageable.TakeDamage(criticalDamage, ElementType.Physical);
            }

            // Hitstop
            HitstopSystem.Instance?.TriggerHitstop(HitstopType.Critical);

            // Event
            OnCriticalAttackHit?.Invoke(target, criticalDamage);

            // Kalan animasyon
            yield return new WaitForSeconds(riposteDuration * 0.6f);

            // Bitir
            EndCriticalAttack();
        }

        /// <summary>
        /// Critical attack için pozisyon ayarla
        /// </summary>
        private void PositionForCriticalAttack(GameObject target, bool isBackstab)
        {
            if (isBackstab)
            {
                // Hedefin arkasına pozisyonlan
                Vector3 behindTarget = target.transform.position - target.transform.forward * 1f;
                transform.position = behindTarget;
                transform.LookAt(target.transform);
            }
            else
            {
                // Hedefin önüne pozisyonlan
                Vector3 inFrontOfTarget = target.transform.position + target.transform.forward * 1.2f;
                transform.position = inFrontOfTarget;

                // Hedefe bak
                Vector3 lookDir = target.transform.position - transform.position;
                lookDir.y = 0;
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        /// <summary>
        /// Critical attack bitir
        /// </summary>
        private void EndCriticalAttack()
        {
            _currentState = BackstabState.None;
            _currentTarget = null;

            OnCriticalAttackEnd?.Invoke();
        }

        /// <summary>
        /// Parry attempt callback
        /// </summary>
        private void OnParryAttempt(bool success)
        {
            if (success)
            {
                _canRiposte = true;
                _riposteEndTime = Time.time + riposteWindow;
                DLog.Log($"[Backstab/Riposte] Riposte window active for {riposteWindow}s");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Backstab range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, backstabRange);

            // Riposte range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, riposteRange);

            // Backstab angle cone
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Vector3 forward = transform.forward * backstabRange;
            Quaternion leftRot = Quaternion.Euler(0, -backstabAngle, 0);
            Quaternion rightRot = Quaternion.Euler(0, backstabAngle, 0);
            Gizmos.DrawLine(transform.position, transform.position + leftRot * forward);
            Gizmos.DrawLine(transform.position, transform.position + rightRot * forward);
        }
#endif
    }
}
