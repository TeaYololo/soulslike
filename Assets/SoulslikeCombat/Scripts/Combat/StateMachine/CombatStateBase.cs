// ============================================================================
// Souls-like Combat System
// Open Source Project: https://github.com/AhmetKardesCan/soulslike-combat
// License: MIT
// Author: Toprak Eren Akpınar
//
// CombatStateBase.cs — Abstract base class for all combat states
// ============================================================================

using System;
using UnityEngine;
using Dungeons.Data;
using Dungeons.Utilities;
using Dungeons.Combat;
using Dungeons.Player;
using Dungeons.Core.Utilities;

namespace Dungeons.Combat.StateMachine
{
    /// <summary>
    /// Combat state'ler için abstract base class.
    /// Ortak functionality burada tanımlanır.
    /// </summary>
    public abstract class CombatStateBase : ICombatState
    {
        /// <summary>
        /// State machine context - tüm component referanslarına erişim sağlar
        /// </summary>
        protected CombatStateMachine Context { get; private set; }

        /// <summary>
        /// Animator referansı (shortcut)
        /// </summary>
        protected Animator Animator => Context.Animator;

        /// <summary>
        /// PlayerCombat referansı (shortcut)
        /// </summary>
        protected PlayerCombat Combat => Context.Combat;

        /// <summary>
        /// CharacterController referansı (shortcut)
        /// </summary>
        protected CharacterController Controller => Context.Controller;

        /// <summary>
        /// State'e giriş zamanı
        /// </summary>
        protected float StateEnterTime { get; private set; }

        /// <summary>
        /// State'de geçen süre
        /// </summary>
        protected float StateTime => Time.time - StateEnterTime;

        /// <summary>
        /// Bu state'in CombatState enum karşılığı
        /// </summary>
        public abstract CombatState StateType { get; }

        /// <summary>
        /// State tamamlandı mı? (override edilebilir)
        /// </summary>
        public virtual bool IsComplete => false;

        /// <summary>
        /// State'e girildiğinde çağrılır
        /// </summary>
        public virtual void Enter(CombatStateMachine context)
        {
            Context = context;
            StateEnterTime = Time.time;

            DLog.Log($"[StateMachine] Entered {StateType}");
        }

        /// <summary>
        /// Her frame çağrılır
        /// </summary>
        public virtual void Update()
        {
            // Alt sınıflar override eder
        }

        /// <summary>
        /// Physics update
        /// </summary>
        public virtual void FixedUpdate()
        {
            // Alt sınıflar override eder
        }

        /// <summary>
        /// State'den çıkıldığında çağrılır
        /// </summary>
        public virtual void Exit()
        {
            DLog.Log($"[StateMachine] Exited {StateType} (duration: {StateTime:F2}s)");
        }

        /// <summary>
        /// Bu state'den belirtilen state'e geçiş yapılabilir mi?
        /// Alt sınıflar bu metodu override ederek kendi transition kurallarını tanımlar.
        /// </summary>
        public abstract bool CanTransitionTo(CombatState newState);

        /// <summary>
        /// Animation event geldiğinde çağrılır
        /// </summary>
        public virtual void OnAnimationEvent(string eventName)
        {
            // Alt sınıflar override eder
        }

        /// <summary>
        /// Hasar alındığında çağrılır
        /// </summary>
        public virtual void OnHit(DamageInfo damage)
        {
            // Default: Stagger'a geçmeyi dene
            if (damage.CausesFlinch && CanTransitionTo(CombatState.Staggered))
            {
                Context.TryChangeState(CombatState.Staggered);
            }
        }

        /// <summary>
        /// Animator trigger'ı tetikle (güvenli)
        /// </summary>
        protected void SetAnimatorTrigger(int hash)
        {
            if (Animator != null)
            {
                Animator.SetTrigger(hash);
            }
        }

        /// <summary>
        /// Animator bool parametresini ayarla (güvenli)
        /// </summary>
        protected void SetAnimatorBool(int hash, bool value)
        {
            if (Animator != null)
            {
                Animator.SetBool(hash, value);
            }
        }

        /// <summary>
        /// Animator integer parametresini ayarla (güvenli)
        /// </summary>
        protected void SetAnimatorInt(int hash, int value)
        {
            if (Animator != null)
            {
                Animator.SetInteger(hash, value);
            }
        }

        /// <summary>
        /// Animator float parametresini ayarla (güvenli)
        /// </summary>
        protected void SetAnimatorFloat(int hash, float value)
        {
            if (Animator != null)
            {
                Animator.SetFloat(hash, value);
            }
        }

        // ==================== ANIMATION HELPERS ====================
        // Network-aware animasyon tetikleme için helper metodlar.
        // AnimEvents varsa öncelikli olarak kullanılır (network sync için).

        /// <summary>
        /// Animasyon trigger'ı tetikle. AnimEvents varsa öncelikli kullanılır.
        /// </summary>
        /// <param name="animEventAction">AnimEvents üzerinden çağrılacak action</param>
        /// <param name="animatorHash">AnimEvents yoksa kullanılacak animator hash</param>
        protected void PlayAnimation(Action<PlayerNetworkAnimEvents> animEventAction, int animatorHash)
        {
            if (Context.AnimEvents != null)
            {
                animEventAction(Context.AnimEvents);
            }
            else
            {
                SetAnimatorTrigger(animatorHash);
            }
        }

        /// <summary>
        /// Bool animator parametresini ayarla. AnimEvents action varsa önce onu çağırır.
        /// </summary>
        protected void SetAnimationBool(Action<PlayerNetworkAnimEvents> animEventAction, int animatorHash, bool value)
        {
            if (Context.AnimEvents != null && animEventAction != null)
            {
                animEventAction(Context.AnimEvents);
            }
            else
            {
                SetAnimatorBool(animatorHash, value);
            }
        }

        /// <summary>
        /// Güvenli trigger tetikleme - parametre yoksa hata vermez
        /// </summary>
        protected void SafeSetTrigger(int hash)
        {
            Animator?.SafeSetTrigger(hash);
        }
    }
}
