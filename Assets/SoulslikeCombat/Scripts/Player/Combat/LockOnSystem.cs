using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Dungeons.Core;
using Dungeons.Data.Interfaces;
using Dungeons.Player;

namespace Dungeons.Combat
{
    /// <summary>
    /// Lock-on targeting sistemi.
    /// Düşmana kilitlenme ve kamera kontrolü.
    /// </summary>
    public class LockOnSystem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float maxLockDistance = 20f;
        [SerializeField] private float lockOnAngle = 60f;
        [SerializeField] private float switchTargetAngle = 30f;
        [SerializeField] private LayerMask targetLayers;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private GameObject lockOnIndicatorPrefab;

        // State
        private ILockOnTarget currentTarget;
        private GameObject indicatorInstance;
        private LockOnIndicator indicatorComponent;
        private List<ILockOnTarget> potentialTargets = new();

        // Static buffer for Physics NonAlloc (GC optimization)
        private static readonly Collider[] _overlapBuffer = new Collider[32];

        // Properties
        public bool IsLockedOn => currentTarget != null;
        public Transform CurrentTargetTransform => currentTarget?.LockOnPoint;

        private void Update()
        {
            if (IsLockedOn)
            {
                ValidateCurrentTarget();
                UpdateIndicatorPosition();
            }
        }

        /// <summary>
        /// Lock-on toggle
        /// </summary>
        public void ToggleLockOn()
        {
            if (IsLockedOn)
            {
                ReleaseLock();
            }
            else
            {
                TryLockOn();
            }
        }

        /// <summary>
        /// Hedef bul ve kilitle
        /// </summary>
        public bool TryLockOn()
        {
            RefreshPotentialTargets();

            if (potentialTargets.Count == 0) return false;

            // En yakın ve açıya uygun hedefi seç
            var bestTarget = potentialTargets
                .Where(t => t.CanBeLocked)
                .OrderByDescending(t => t.LockOnPriority)
                .ThenBy(t => Vector3.Distance(transform.position, t.LockOnPoint.position))
                .FirstOrDefault();

            if (bestTarget != null)
            {
                SetTarget(bestTarget);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Kilidi bırak
        /// </summary>
        public void ReleaseLock()
        {
            if (currentTarget != null)
            {
                currentTarget.OnLockLost();

                EventBus.Publish(new LockOnEvent
                {
                    Player = gameObject,
                    Target = (currentTarget as MonoBehaviour)?.gameObject,
                    IsLocking = false
                });
            }

            currentTarget = null;
            HideIndicator();
        }

        /// <summary>
        /// Hedef değiştir (sağ/sol)
        /// </summary>
        public void SwitchTarget(float direction)
        {
            if (!IsLockedOn) return;

            RefreshPotentialTargets();

            if (potentialTargets.Count <= 1) return;

            var currentPos = currentTarget.LockOnPoint.position;
            var toCamera = cameraTransform.position - currentPos;
            var right = Vector3.Cross(Vector3.up, toCamera).normalized;

            // Hedef yönüne göre sırala
            var sortedTargets = potentialTargets
                .Where(t => t != currentTarget && t.CanBeLocked)
                .Select(t => new
                {
                    Target = t,
                    Dot = Vector3.Dot((t.LockOnPoint.position - currentPos).normalized, right)
                })
                .Where(t => direction > 0 ? t.Dot > 0.1f : t.Dot < -0.1f)
                .OrderBy(t => direction > 0 ? -t.Dot : t.Dot)
                .Select(t => t.Target)
                .ToList();

            if (sortedTargets.Count > 0)
            {
                SetTarget(sortedTargets[0]);
            }
        }

        private void SetTarget(ILockOnTarget target)
        {
            if (currentTarget != null)
            {
                currentTarget.OnLockLost();
            }

            currentTarget = target;
            currentTarget.OnLockedOn();

            ShowIndicator();

            EventBus.Publish(new LockOnEvent
            {
                Player = gameObject,
                Target = (currentTarget as MonoBehaviour)?.gameObject,
                IsLocking = true
            });
        }

        private void RefreshPotentialTargets()
        {
            potentialTargets.Clear();

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, maxLockDistance, _overlapBuffer, targetLayers);

            for (int i = 0; i < hitCount; i++)
            {
                var col = _overlapBuffer[i];
                if (!col.TryGetComponent<ILockOnTarget>(out var target)) continue;
                if (!target.CanBeLocked) continue;

                // Açı kontrolü
                var toTarget = (target.LockOnPoint.position - transform.position).normalized;
                var angle = Vector3.Angle(transform.forward, toTarget);

                if (angle <= lockOnAngle)
                {
                    // Görüş çizgisi kontrolü
                    if (!Physics.Linecast(transform.position + Vector3.up, target.LockOnPoint.position,
                        ~targetLayers, QueryTriggerInteraction.Ignore))
                    {
                        potentialTargets.Add(target);
                    }
                }
            }

            // Multiplayer: Diğer player'ları da hedef listesine ekle
            // (Player layer targetLayers'da olmayabilir, ayrıca ara)
            // LockOnTargetRegistry kullanılıyor — GC allocation önler
            var playerTargets = LockOnTargetRegistry.Targets;
            foreach (var pt in playerTargets)
            {
                if (pt.gameObject == gameObject) continue; // Kendimizi ekleme
                if (!pt.CanBeLocked) continue;
                if (potentialTargets.Contains(pt)) continue; // Zaten ekli

                float dist = Vector3.Distance(transform.position, pt.LockOnPoint.position);
                if (dist > maxLockDistance) continue;

                var toTarget = (pt.LockOnPoint.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, toTarget);
                if (angle > lockOnAngle) continue;

                if (!Physics.Linecast(transform.position + Vector3.up, pt.LockOnPoint.position,
                    ~targetLayers, QueryTriggerInteraction.Ignore))
                {
                    potentialTargets.Add(pt);
                }
            }
        }

        private void ValidateCurrentTarget()
        {
            if (currentTarget == null) return;

            var targetTransform = currentTarget.LockOnPoint;

            // Mesafe kontrolü
            if (Vector3.Distance(transform.position, targetTransform.position) > maxLockDistance)
            {
                ReleaseLock();
                return;
            }

            // Hedef hâlâ kilitlenebilir mi
            if (!currentTarget.CanBeLocked)
            {
                ReleaseLock();
            }
        }

        private void ShowIndicator()
        {
            if (lockOnIndicatorPrefab == null) return;

            if (indicatorInstance == null)
            {
                indicatorInstance = Instantiate(lockOnIndicatorPrefab);
                indicatorComponent = indicatorInstance.GetComponent<LockOnIndicator>();
            }

            indicatorInstance.SetActive(true);

            // LockOnIndicator varsa hedef belirle
            if (indicatorComponent != null && currentTarget != null)
            {
                var targetMono = currentTarget as MonoBehaviour;
                if (targetMono != null)
                {
                    indicatorComponent.SetTarget(targetMono.transform);
                }
            }
            else
            {
                // Legacy: Sadece pozisyon güncelle
                UpdateIndicatorPosition();
            }
        }

        private void HideIndicator()
        {
            if (indicatorComponent != null)
            {
                indicatorComponent.ClearTarget();
            }

            if (indicatorInstance != null)
            {
                indicatorInstance.SetActive(false);
            }
        }

        private void UpdateIndicatorPosition()
        {
            // LockOnIndicator component varsa, o kendi pozisyonunu yönetir
            if (indicatorComponent != null) return;

            // Legacy fallback
            if (indicatorInstance != null && currentTarget != null)
            {
                indicatorInstance.transform.position = currentTarget.LockOnPoint.position;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, maxLockDistance);
        }
    }
}
