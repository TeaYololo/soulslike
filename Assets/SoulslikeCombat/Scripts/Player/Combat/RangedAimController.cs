using Dungeons.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Dungeons.Data.Interfaces;

namespace Dungeons.Combat
{
    /// <summary>
    /// Uzak mesafe silahları için nişan alma sistemi.
    /// Crosshair UI, kamera FOV değişimi, hedef takibi.
    /// </summary>
    public class RangedAimController : MonoBehaviour
    {
        [Header("Aim Settings")]
        [SerializeField] private float aimDistance = 100f;
        [SerializeField] private LayerMask aimLayers = ~0;
        [SerializeField] private LayerMask ignoreLayers;
        [SerializeField] private float aimSphereRadius = 0.1f;

        [Header("Camera")]
        [SerializeField] private float aimCameraFOV = 45f;
        [SerializeField] private float normalCameraFOV = 60f;
        [SerializeField] private float fovTransitionSpeed = 8f;
        [SerializeField] private Vector3 aimCameraOffset = new Vector3(0.5f, 0.2f, -0.5f);
        [SerializeField] private float cameraOffsetSpeed = 5f;

        [Header("Crosshair UI")]
        [SerializeField] private RectTransform crosshairUI;
        [SerializeField] private Image crosshairImage;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color targetColor = Color.red;
        [SerializeField] private Color friendlyColor = Color.green;
        [SerializeField] private float crosshairSizeNormal = 32f;
        [SerializeField] private float crosshairSizeAiming = 24f;

        [Header("Draw Progress UI")]
        [SerializeField] private Image drawProgressImage;
        [SerializeField] private RectTransform drawProgressRing;

        // State
        private bool _isAiming = false;
        private Vector3 _aimPoint;
        private Transform _aimTarget;
        private UnityEngine.Camera _mainCamera;
        private float _originalCameraFOV;
        private Vector3 _originalCameraOffset;

        // Camera follow reference (opsiyonel)
        private Transform _cameraFollowTarget;
        private Vector3 _currentCameraOffset;

        // Properties
        public bool IsAiming => _isAiming;
        public Vector3 AimPoint => _aimPoint;
        public Transform AimTarget => _aimTarget;
        public bool HasTarget => _aimTarget != null;

        // Events
        public System.Action<bool> OnAimStateChanged;
        public System.Action<Transform> OnTargetChanged;

        private void Awake()
        {
            _mainCamera = UnityEngine.Camera.main;

            if (_mainCamera != null)
            {
                _originalCameraFOV = _mainCamera.fieldOfView;
            }

            // Crosshair başlangıçta gizli
            if (crosshairUI != null)
            {
                crosshairUI.gameObject.SetActive(false);
            }

            if (drawProgressImage != null)
            {
                drawProgressImage.fillAmount = 0f;
            }
        }

        private void Update()
        {
            UpdateCameraFOV();

            if (!_isAiming) return;

            UpdateAimPoint();
            UpdateCrosshairPosition();
            UpdateCrosshairColor();
        }

        #region Aim Control

        /// <summary>
        /// Nişan almaya başla
        /// </summary>
        public void StartAiming()
        {
            if (_isAiming) return;

            _isAiming = true;

            // Crosshair göster
            if (crosshairUI != null)
            {
                crosshairUI.gameObject.SetActive(true);
            }

            // Kamera FOV'u kaydet
            if (_mainCamera != null && _originalCameraFOV == 0)
            {
                _originalCameraFOV = _mainCamera.fieldOfView;
            }

            OnAimStateChanged?.Invoke(true);

            DLog.Log("[RangedAimController] Aiming started");
        }

        /// <summary>
        /// Nişan almayı bırak
        /// </summary>
        public void StopAiming()
        {
            if (!_isAiming) return;

            _isAiming = false;
            _aimTarget = null;

            // Crosshair gizle
            if (crosshairUI != null)
            {
                crosshairUI.gameObject.SetActive(false);
            }

            // Draw progress sıfırla
            if (drawProgressImage != null)
            {
                drawProgressImage.fillAmount = 0f;
            }

            OnAimStateChanged?.Invoke(false);
            OnTargetChanged?.Invoke(null);

            DLog.Log("[RangedAimController] Aiming stopped");
        }

        #endregion

        #region Aim Calculation

        /// <summary>
        /// Nişan noktasını güncelle
        /// </summary>
        private void UpdateAimPoint()
        {
            if (_mainCamera == null)
            {
                _mainCamera = UnityEngine.Camera.main;
                if (_mainCamera == null) return;
            }

            // Ekran merkezinden ray
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
            Ray ray = _mainCamera.ScreenPointToRay(screenCenter);

            // Layer mask
            int layerMask = aimLayers.value & ~ignoreLayers.value;

            Transform newTarget = null;

            // SphereCast daha iyi hedefleme için
            if (Physics.SphereCast(ray, aimSphereRadius, out RaycastHit hit, aimDistance, layerMask))
            {
                _aimPoint = hit.point;

                // Hedef damageable mi? (TryGetComponent daha verimli)
                if (hit.collider.TryGetComponent<IDamageable>(out var damageable) ||
                    (hit.collider.transform.parent != null &&
                     hit.collider.transform.parent.TryGetComponent<IDamageable>(out damageable)))
                {
                    newTarget = hit.collider.transform;
                }
            }
            else
            {
                // Hit yok - maksimum mesafeye nişan al
                _aimPoint = ray.origin + ray.direction * aimDistance;
            }

            // Hedef değişti mi?
            if (newTarget != _aimTarget)
            {
                _aimTarget = newTarget;
                OnTargetChanged?.Invoke(_aimTarget);
            }
        }

        /// <summary>
        /// Bir noktadan nişan noktasına yön al
        /// </summary>
        public Vector3 GetAimDirection(Vector3 fromPoint)
        {
            if (_aimPoint == Vector3.zero)
            {
                return transform.forward;
            }

            return (_aimPoint - fromPoint).normalized;
        }

        /// <summary>
        /// Nişan noktasına mesafe
        /// </summary>
        public float GetDistanceToAimPoint(Vector3 fromPoint)
        {
            return Vector3.Distance(fromPoint, _aimPoint);
        }

        #endregion

        #region Camera

        /// <summary>
        /// Kamera FOV'unu güncelle
        /// </summary>
        private void UpdateCameraFOV()
        {
            if (_mainCamera == null) return;

            float targetFOV = _isAiming ? aimCameraFOV : normalCameraFOV;

            // Smooth FOV transition
            _mainCamera.fieldOfView = Mathf.Lerp(
                _mainCamera.fieldOfView,
                targetFOV,
                fovTransitionSpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// Kamera offset'i ayarla (opsiyonel - CameraController ile entegre)
        /// </summary>
        public Vector3 GetCameraOffset()
        {
            if (!_isAiming)
            {
                return Vector3.zero;
            }

            return aimCameraOffset;
        }

        #endregion

        #region UI Updates

        /// <summary>
        /// Crosshair pozisyonunu güncelle
        /// </summary>
        private void UpdateCrosshairPosition()
        {
            if (crosshairUI == null) return;

            // Crosshair her zaman ekran merkezinde
            crosshairUI.anchoredPosition = Vector2.zero;

            // Boyut ayarla
            float targetSize = _isAiming ? crosshairSizeAiming : crosshairSizeNormal;
            crosshairUI.sizeDelta = Vector2.Lerp(
                crosshairUI.sizeDelta,
                new Vector2(targetSize, targetSize),
                10f * Time.deltaTime
            );
        }

        /// <summary>
        /// Crosshair rengini güncelle
        /// </summary>
        private void UpdateCrosshairColor()
        {
            if (crosshairImage == null) return;

            Color targetColor;

            if (_aimTarget != null)
            {
                // Düşman mı dost mu kontrol et
                bool isEnemy = _aimTarget.CompareTag("Enemy") ||
                              _aimTarget.gameObject.layer == LayerMask.NameToLayer("Enemy");

                targetColor = isEnemy ? this.targetColor : friendlyColor;
            }
            else
            {
                targetColor = normalColor;
            }

            // Smooth color transition
            crosshairImage.color = Color.Lerp(crosshairImage.color, targetColor, 10f * Time.deltaTime);
        }

        /// <summary>
        /// Draw progress UI'ı güncelle
        /// </summary>
        public void UpdateDrawProgress(float progress)
        {
            if (drawProgressImage != null)
            {
                drawProgressImage.fillAmount = progress;

                // Full draw'da renk değiştir
                if (progress >= 1f)
                {
                    drawProgressImage.color = Color.green;
                }
                else
                {
                    drawProgressImage.color = Color.white;
                }
            }

            // Ring scale animasyonu
            if (drawProgressRing != null)
            {
                float scale = Mathf.Lerp(1.2f, 0.8f, progress);
                drawProgressRing.localScale = Vector3.one * scale;
            }
        }

        /// <summary>
        /// Crosshair'i genişlet (shoot feedback)
        /// </summary>
        public void PunchCrosshair(float amount = 1.5f)
        {
            if (crosshairUI == null) return;

            // Geçici olarak büyüt
            crosshairUI.sizeDelta *= amount;
        }

        #endregion

        #region Trajectory Preview (Optional)

        /// <summary>
        /// Ok yörüngesini hesapla (preview için)
        /// </summary>
        public Vector3[] CalculateTrajectory(Vector3 startPos, Vector3 velocity, float gravity, int segments = 20, float timeStep = 0.1f)
        {
            Vector3[] points = new Vector3[segments];
            Vector3 pos = startPos;
            Vector3 vel = velocity;

            for (int i = 0; i < segments; i++)
            {
                points[i] = pos;
                vel += Vector3.down * gravity * timeStep;
                pos += vel * timeStep;

                // Hit check (opsiyonel)
                if (Physics.Raycast(points[Mathf.Max(0, i - 1)], pos - points[Mathf.Max(0, i - 1)], out _, Vector3.Distance(points[Mathf.Max(0, i - 1)], pos)))
                {
                    // Collision - truncate trajectory
                    System.Array.Resize(ref points, i + 1);
                    break;
                }
            }

            return points;
        }

        #endregion

        #region Editor

        private void OnDrawGizmosSelected()
        {
            // Aim distance
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, aimDistance);

            // Aim point (play mode)
            if (Application.isPlaying && _aimPoint != Vector3.zero)
            {
                Gizmos.color = _aimTarget != null ? Color.red : Color.white;
                Gizmos.DrawWireSphere(_aimPoint, 0.3f);

                // Line to aim point
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, _aimPoint);
            }
        }

        #endregion
    }
}
