using FishNet.Object;
using FishNet.Object.Synchronizing;
using Dungeons.Core.Utilities;
using UnityEngine;

namespace Dungeons.Combat
{
    /// <summary>
    /// Combo state'ini server-authoritative sync eder.
    ///
    /// Client: Saldırı input → NetworkCombatController.ServerRequestAttack(comboIndex)
    /// Server: Combo window kontrolü → comboIndex validate → combo ilerlet veya reset
    /// Server → Client: SyncVar ile comboIndex sync → Animator parametresi güncelle
    /// </summary>
    public class NetworkComboSync : NetworkBehaviour
    {
        [Header("Combo Ayarları")]
        [SerializeField] private float comboWindowDuration = 1.2f;
        [SerializeField] private int defaultMaxCombo = 3;

        // Server-authoritative state
        public readonly SyncVar<int> _currentComboIndex = new();
        public readonly SyncVar<bool> _comboWindowOpen = new();

        // Combo multipliers (PlayerComboSystem ile aynı)
        private static readonly float[] ComboMultipliers = { 1.0f, 1.1f, 1.2f, 1.3f, 1.5f };

        // Server-side tracking
        private float _comboWindowEnd;
        private int _maxCombo;

        // Cache
        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _maxCombo = defaultMaxCombo;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _currentComboIndex.OnChange += OnComboIndexChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _currentComboIndex.OnChange -= OnComboIndexChanged;
        }

        // ========================================
        // SERVER API — NetworkCombatController'dan çağrılır
        // ========================================

        /// <summary>
        /// Saldırı geldiğinde combo state güncelle.
        /// NetworkCombatController.ServerRequestAttack() içinden çağrılır.
        /// Returns: validated combo index.
        /// </summary>
        [Server]
        public int ProcessAttack(int requestedComboIndex)
        {
            float now = Time.time;

            if (_comboWindowOpen.Value && now <= _comboWindowEnd)
            {
                // Window açık — combo ilerle
                int next = _currentComboIndex.Value + 1;
                if (next > _maxCombo)
                    next = 1; // Wrap around
                _currentComboIndex.Value = next;
            }
            else
            {
                // Window kapalı — combo sıfırla, ilk saldırı
                _currentComboIndex.Value = 1;
            }

            // Yeni combo window aç
            _comboWindowEnd = now + comboWindowDuration;
            _comboWindowOpen.Value = true;

            return _currentComboIndex.Value;
        }

        /// <summary>
        /// Silah değiştiğinde max combo güncelle.
        /// </summary>
        [Server]
        public void SetMaxCombo(int maxCombo)
        {
            _maxCombo = maxCombo > 0 ? maxCombo : defaultMaxCombo;
        }

        /// <summary>
        /// Combo'yu sıfırla (hit alınca, dodge, stagger vb.)
        /// </summary>
        [Server]
        public void ResetCombo()
        {
            _currentComboIndex.Value = 0;
            _comboWindowOpen.Value = false;
        }

        /// <summary>
        /// Server Update — combo window timeout kontrolü.
        /// </summary>
        private void Update()
        {
            if (!IsServerInitialized) return;

            if (_comboWindowOpen.Value && Time.time > _comboWindowEnd)
            {
                _comboWindowOpen.Value = false;
                _currentComboIndex.Value = 0;
            }
        }

        // ========================================
        // SYNCVAR CALLBACK
        // ========================================

        private void OnComboIndexChanged(int prev, int next, bool asServer)
        {
            if (asServer) return;

            // Client'ta animator güncelle
            if (_animator != null)
                _animator.SetInteger(AnimatorHash.ComboIndex, next);
        }

        // ========================================
        // PUBLIC GETTERS
        // ========================================

        public int CurrentComboIndex => _currentComboIndex.Value;
        public bool IsComboWindowOpen => _comboWindowOpen.Value;

        /// <summary>
        /// Mevcut combo multiplier'ı döndür.
        /// </summary>
        public float GetComboMultiplier()
        {
            int idx = Mathf.Clamp(_currentComboIndex.Value, 0, ComboMultipliers.Length - 1);
            return ComboMultipliers[idx];
        }
    }
}
