using UnityEngine;
using System.Collections.Generic;
using Dungeons.Core;
using Dungeons.Utilities;

namespace Dungeons.Combat
{
    /// <summary>
    /// Combat style/combo sistemi.
    /// Çeşitli aksiyonlar puan kazandırır, aynı hareket spam cezalandırılır.
    /// Yüksek style = bonus hasar/XP.
    /// </summary>
    public class StyleSystem : Singleton<StyleSystem>
    {

        [Header("Style Settings")]
        [SerializeField] private float maxStylePoints = 1000f;
        [SerializeField] private float styleDecayRate = 50f; // Saniyede düşen puan
        [SerializeField] private float styleDecayDelay = 2f; // Decay başlamadan önce bekleme

        [Header("Action Points")]
        [SerializeField] private float lightAttackPoints = 10f;
        [SerializeField] private float heavyAttackPoints = 20f;
        [SerializeField] private float parryPoints = 100f;
        [SerializeField] private float perfectDodgePoints = 80f;
        [SerializeField] private float comboFinisherPoints = 50f;
        [SerializeField] private float counterAttackPoints = 75f;
        [SerializeField] private float airAttackPoints = 30f;

        [Header("Variety Bonus")]
        [Tooltip("Farklı aksiyonlar için çarpan")]
        [SerializeField] private float varietyMultiplier = 1.5f;
        [Tooltip("Aynı aksiyon tekrarında puan düşürme")]
        [SerializeField] private float repeatPenalty = 0.5f;
        [Tooltip("Kaç aksiyon geriye bakılacak")]
        [SerializeField] private int varietyHistorySize = 5;

        [Header("Style Ranks")]
        [SerializeField] private StyleRank[] styleRanks = new StyleRank[]
        {
            new StyleRank { name = "D", minPoints = 0, damageBonus = 0f, xpBonus = 0f },
            new StyleRank { name = "C", minPoints = 100, damageBonus = 0.05f, xpBonus = 0.1f },
            new StyleRank { name = "B", minPoints = 250, damageBonus = 0.1f, xpBonus = 0.2f },
            new StyleRank { name = "A", minPoints = 500, damageBonus = 0.15f, xpBonus = 0.3f },
            new StyleRank { name = "S", minPoints = 750, damageBonus = 0.2f, xpBonus = 0.5f },
            new StyleRank { name = "SS", minPoints = 900, damageBonus = 0.25f, xpBonus = 0.75f },
            new StyleRank { name = "SSS", minPoints = 1000, damageBonus = 0.3f, xpBonus = 1f }
        };

        // State
        private float _currentStylePoints;
        private float _lastActionTime;
        private Queue<StyleAction> _actionHistory;
        private int _currentComboCount;
        private float _comboTimer;
        private float _comboResetTime = 2f;

        // Properties
        public float CurrentStylePoints => _currentStylePoints;
        public float MaxStylePoints => maxStylePoints;
        public float StylePercentage => _currentStylePoints / maxStylePoints;
        public int CurrentComboCount => _currentComboCount;
        public StyleRank CurrentRank => GetCurrentRank();
        public float DamageBonus => CurrentRank.damageBonus;
        public float XPBonus => CurrentRank.xpBonus;

        // Events
        public System.Action<float, float> OnStyleChanged;
        public System.Action<StyleRank> OnRankChanged;
        public System.Action<int> OnComboChanged;
        public System.Action<StyleAction, float> OnActionScored;

        private StyleRank _lastRank;

        protected override void Awake()
        {
            base.Awake();
            _actionHistory = new Queue<StyleAction>();
            _lastRank = styleRanks[0];
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        private void Start()
        {
            // Event subscriptions
            EventBus.Subscribe<AttackLandedEvent>(OnAttackLanded);
            EventBus.Subscribe<ParrySuccessEvent>(OnParrySuccess);
            EventBus.Subscribe<PerfectDodgeEvent>(OnPerfectDodge);
        }

        private void Update()
        {
            UpdateStyleDecay();
            UpdateComboTimer();
            CheckRankChange();
        }

        #region Public Methods

        /// <summary>
        /// Stil puanı ekle (doğrudan)
        /// </summary>
        public void AddStylePoints(float points, StyleAction action)
        {
            // Variety kontrolü
            float multiplier = CalculateVarietyMultiplier(action);
            float finalPoints = points * multiplier;

            _currentStylePoints = Mathf.Min(_currentStylePoints + finalPoints, maxStylePoints);
            _lastActionTime = Time.time;

            // History'e ekle
            _actionHistory.Enqueue(action);
            if (_actionHistory.Count > varietyHistorySize)
            {
                _actionHistory.Dequeue();
            }

            // Combo
            _currentComboCount++;
            _comboTimer = _comboResetTime;
            OnComboChanged?.Invoke(_currentComboCount);

            OnStyleChanged?.Invoke(_currentStylePoints, maxStylePoints);
            OnActionScored?.Invoke(action, finalPoints);

            DLog.Log($"[StyleSystem] +{finalPoints:F0} points ({action}) | Total: {_currentStylePoints:F0} | Combo: {_currentComboCount} | Rank: {CurrentRank.name}");
        }

        /// <summary>
        /// Hasar alındığında stil puanı düşür
        /// </summary>
        public void OnDamageTaken(int damage)
        {
            float penalty = damage * 2f; // Her hasar puanı için 2 stil puanı kaybet
            _currentStylePoints = Mathf.Max(0, _currentStylePoints - penalty);

            // Combo reset
            _currentComboCount = 0;
            OnComboChanged?.Invoke(0);

            OnStyleChanged?.Invoke(_currentStylePoints, maxStylePoints);

            DLog.Log($"[StyleSystem] -{penalty:F0} points (damage taken) | Total: {_currentStylePoints:F0}");
        }

        /// <summary>
        /// Stil puanlarını sıfırla
        /// </summary>
        public void ResetStyle()
        {
            _currentStylePoints = 0;
            _currentComboCount = 0;
            _actionHistory.Clear();

            OnStyleChanged?.Invoke(0, maxStylePoints);
            OnComboChanged?.Invoke(0);
        }

        #endregion

        #region Event Handlers

        private void OnAttackLanded(AttackLandedEvent evt)
        {
            if (evt.Attacker != gameObject) return;

            StyleAction action = evt.IsCritical ? StyleAction.CriticalHit :
                                 evt.IsStaggering ? StyleAction.StaggerHit :
                                 StyleAction.LightAttack;

            float points = evt.IsCritical ? heavyAttackPoints * 1.5f :
                          evt.IsStaggering ? heavyAttackPoints :
                          lightAttackPoints;

            AddStylePoints(points, action);
        }

        private void OnParrySuccess(ParrySuccessEvent evt)
        {
            if (evt.Defender != gameObject) return;
            AddStylePoints(parryPoints, StyleAction.Parry);
        }

        private void OnPerfectDodge(PerfectDodgeEvent evt)
        {
            if (evt.Character != gameObject) return;
            AddStylePoints(perfectDodgePoints, StyleAction.PerfectDodge);
        }

        #endregion

        #region Private Methods

        private void UpdateStyleDecay()
        {
            if (Time.time - _lastActionTime < styleDecayDelay) return;

            if (_currentStylePoints > 0)
            {
                _currentStylePoints = Mathf.Max(0, _currentStylePoints - styleDecayRate * Time.deltaTime);
                OnStyleChanged?.Invoke(_currentStylePoints, maxStylePoints);
            }
        }

        private void UpdateComboTimer()
        {
            if (_comboTimer > 0)
            {
                _comboTimer -= Time.deltaTime;
                if (_comboTimer <= 0 && _currentComboCount > 0)
                {
                    // Combo bitti
                    DLog.Log($"[StyleSystem] Combo ended! Final count: {_currentComboCount}");
                    _currentComboCount = 0;
                    OnComboChanged?.Invoke(0);
                }
            }
        }

        private void CheckRankChange()
        {
            StyleRank currentRank = CurrentRank;
            if (currentRank.name != _lastRank.name)
            {
                _lastRank = currentRank;
                OnRankChanged?.Invoke(currentRank);
                DLog.Log($"[StyleSystem] Rank changed to: {currentRank.name}!");
            }
        }

        private float CalculateVarietyMultiplier(StyleAction action)
        {
            int sameActionCount = 0;

            foreach (var historyAction in _actionHistory)
            {
                if (historyAction == action)
                    sameActionCount++;
            }

            if (sameActionCount == 0)
            {
                // Yeni aksiyon - variety bonus
                return varietyMultiplier;
            }
            else
            {
                // Tekrarlanan aksiyon - penalty
                return Mathf.Max(0.1f, 1f - (sameActionCount * repeatPenalty));
            }
        }

        private StyleRank GetCurrentRank()
        {
            for (int i = styleRanks.Length - 1; i >= 0; i--)
            {
                if (_currentStylePoints >= styleRanks[i].minPoints)
                {
                    return styleRanks[i];
                }
            }
            return styleRanks[0];
        }

        #endregion
    }

    [System.Serializable]
    public struct StyleRank
    {
        public string name;
        public float minPoints;
        public float damageBonus;
        public float xpBonus;
    }

    public enum StyleAction
    {
        LightAttack,
        HeavyAttack,
        CriticalHit,
        StaggerHit,
        Parry,
        PerfectDodge,
        CounterAttack,
        AirAttack,
        ComboFinisher,
        Dodge
    }
}
