using FishNet.Object;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Dungeons.Core;
using Dungeons.Data;
using Dungeons.Data.Interfaces;
using Dungeons.Items;
using Dungeons.Player;
using Dungeons.Character;
using Dungeons.VFX;

namespace Dungeons.Combat
{
    /// <summary>
    /// Server-authoritative combat controller.
    /// Attack request → validate → hit detect → damage apply → broadcast.
    ///
    /// Singleplayer'da: Host = server + client, aynı frame'de çalışır.
    /// </summary>
    public class NetworkCombatController : NetworkBehaviour
    {
        [Header("Hit Detection")]
        [SerializeField] private float hitDetectionRadius = 1.5f;
        [SerializeField] private float hitDetectionRange = 2.5f;
        [SerializeField] private LayerMask hitLayers = -1;

        [Header("Parry Tolerance")]
        [SerializeField] private float parryLatencyToleranceMs = 150f;

        [Header("Dodge")]
        [SerializeField] private float dodgeIFrameDuration = 0.4f;

        [Header("Cooldowns")]
        [SerializeField] private float lightAttackCooldown = 0.5f;
        [SerializeField] private float heavyAttackCooldown = 1.0f;

        // Components
        private NetworkCombatState _combatState;
        private NetworkComboSync _comboSync;
        private PlayerNetworkAnimEvents _animEvents;
        private Animator _animator;
        private PlayerCombat _playerCombat;
        private EquipmentManager _equipmentManager;

        // Server state
        private float _lastAttackTime;
        private float _parryWindowStart;
        private float _parryWindowDuration = 0.3f;
        private bool _isParryWindowActive;
        private bool _isBlocking;

        // Static buffer for OverlapSphere
        private static readonly Collider[] _hitBuffer = new Collider[16];
        private static readonly HashSet<int> _processedTargets = new();

        private void Awake()
        {
            _combatState = GetComponent<NetworkCombatState>();
            _comboSync = GetComponent<NetworkComboSync>();
            _animEvents = GetComponent<PlayerNetworkAnimEvents>();
            _animator = GetComponent<Animator>();
            _playerCombat = GetComponent<PlayerCombat>();
            _equipmentManager = GetComponent<EquipmentManager>();

            LoadCombatSettings();
        }

        /// <summary>
        /// CombatSettings'ten değerleri yükle
        /// </summary>
        private void LoadCombatSettings()
        {
            if (CombatSettings.Instance == null) return;

            var settings = CombatSettings.Instance;

            // Parry settings
            parryLatencyToleranceMs = settings.ParryLatencyTolerance * 1000f; // seconds to ms
            _parryWindowDuration = settings.GetParryWindow();

            // Dodge settings
            dodgeIFrameDuration = settings.DodgeIFrameDuration;

            // Attack cooldowns
            lightAttackCooldown = settings.LightAttackCooldown;
            heavyAttackCooldown = settings.HeavyAttackCooldown;
        }

        // ========================================
        // ATTACK — Client → Server → Broadcast
        // ========================================

        [ServerRpc]
        public void ServerRequestAttack(AttackRequest request)
        {
            if (!ValidateAttack(request)) return;

            // Combo sync — server-validated combo index
            int comboIndex = request.ComboIndex;
            if (_comboSync != null)
                comboIndex = _comboSync.ProcessAttack(request.ComboIndex);

            // Stamina
            float cost = GetAttackStaminaCost(request.AttackType);
            if (!_combatState.ServerConsumeStamina(cost)) return;

            _lastAttackTime = Time.time;
            _combatState.ServerSetCombatState(1); // Attacking

            // Animasyon broadcast (validated combo index ile)
            ObserversPlayAttack(request.AttackType, comboIndex);

            // Hit detection (delayed — animasyon hit frame)
            float delay = GetHitFrameDelay(request.AttackType);
            StartCoroutine(DelayedHitDetection(request, delay));
        }

        private bool ValidateAttack(AttackRequest request)
        {
            if (_combatState.IsDead) return false;
            float cd = request.AttackType == 0 ? lightAttackCooldown : heavyAttackCooldown;
            if (Time.time - _lastAttackTime < cd) return false;
            // Stagger/hitstun sırasında saldıramaz
            int state = _combatState.CombatStateValue;
            if (state == 5 || state == 6) return false; // Staggered or HitReaction
            return true;
        }

        private IEnumerator DelayedHitDetection(AttackRequest request, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            PerformHitDetection(request);
            // Saldırı bitti
            yield return new WaitForSeconds(0.3f);
            if (_combatState.CombatStateValue == 1) // Hala attacking'deyse
                _combatState.ServerSetCombatState(0); // Idle
        }

        private void PerformHitDetection(AttackRequest request)
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 dir = request.AimDirection != Vector3.zero
                ? request.AimDirection.normalized
                : transform.forward;

            int hitCount = Physics.OverlapSphereNonAlloc(
                origin + dir * (hitDetectionRange * 0.5f),
                hitDetectionRadius,
                _hitBuffer,
                hitLayers,
                QueryTriggerInteraction.Ignore);

            _processedTargets.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _hitBuffer[i];
                if (hit.transform == transform) continue;
                if (hit.transform.IsChildOf(transform)) continue;

                // Player target (NetworkObject var) - TryGetComponent avoids GC allocation on null
                hit.TryGetComponent<NetworkObject>(out var targetNob);
                if (targetNob == null) { var p = hit.transform.parent; if (p != null) p.TryGetComponent<NetworkObject>(out targetNob); }
                if (targetNob != null && targetNob != NetworkObject && !_processedTargets.Contains(targetNob.ObjectId))
                {
                    _processedTargets.Add(targetNob.ObjectId);
                    var targetState = targetNob.GetComponent<NetworkCombatState>();
                    if (targetState != null)
                    {
                        ProcessPlayerHit(request, targetNob, targetState, hit);
                        continue;
                    }
                }

                // NPC / Animal target (IDamageable var) - TryGetComponent avoids GC allocation on null
                hit.TryGetComponent<IDamageable>(out var damageable);
                if (damageable == null) { var p = hit.transform.parent; if (p != null) p.TryGetComponent<IDamageable>(out damageable); }
                if (damageable != null && !ReferenceEquals(damageable, _playerCombat))
                {
                    ProcessNPCHit(request, damageable, hit);
                }
            }
        }

        private void ProcessPlayerHit(AttackRequest request, NetworkObject targetNob,
            NetworkCombatState targetState, Collider hitCol)
        {
            // 1. Dodge (i-frame)
            if (targetState.IsInvulnerable)
            {
                BroadcastHit(HitResultType.Dodged, targetNob, 0, 0, hitCol);
                return;
            }

            // 2. Parry
            var targetCtrl = targetNob.GetComponent<NetworkCombatController>();
            if (targetCtrl != null && targetCtrl.IsParryActive())
            {
                HandleParrySuccess(targetNob, targetCtrl);
                BroadcastHit(HitResultType.Parried, targetNob, 0, 0, hitCol);
                return;
            }

            // 3. Block
            if (targetCtrl != null && targetCtrl._isBlocking)
            {
                float chip = CalculateDamage(request.AttackType) * 0.2f;
                targetState.ServerApplyDamage(chip);
                targetState.ServerConsumeStamina(GetBlockStaminaDrain(request.AttackType));
                BroadcastHit(HitResultType.Blocked, targetNob, chip, 0, hitCol);

                // Block hit animation
                var targetAnim = targetNob.GetComponent<PlayerNetworkAnimEvents>();
                targetAnim?.PlayBlockHit();
                return;
            }

            // 4. Normal hit
            float dmg = CalculateDamage(request.AttackType);
            float poise = CalculatePoiseDamage(request.AttackType);
            targetState.ServerApplyDamage(dmg);
            bool stagger = targetState.ServerApplyPoiseDamage(poise);

            var result = targetState.IsDead ? HitResultType.Killed
                       : stagger ? HitResultType.Staggered
                       : HitResultType.NormalHit;

            BroadcastHit(result, targetNob, dmg, poise, hitCol);

            // Hit reaction animation - TargetRpc ile client'a gönder
            var targetCtrlForAnim = targetNob.GetComponent<NetworkCombatController>();
            if (targetCtrlForAnim != null)
            {
                targetCtrlForAnim.TargetPlayHitReaction(targetNob.Owner, stagger);
            }
        }

        private void ProcessNPCHit(AttackRequest request, IDamageable target, Collider hitCol)
        {
            float dmg = CalculateDamage(request.AttackType);
            int intDmg = Mathf.RoundToInt(dmg);

            // DamageInfo oluştur (mevcut sisteme uygun)
            var damageInfo = DamageInfo.Create(gameObject, intDmg);
            damageInfo.AttackType = request.AttackType == 0 ? Data.AttackType.LightAttack : Data.AttackType.HeavyAttack;
            damageInfo.HitDirection = transform.forward;
            damageInfo.PoiseDamage = CalculatePoiseDamage(request.AttackType);
            damageInfo.StaggerValue = damageInfo.PoiseDamage;

            if (hitCol != null)
                damageInfo.HitPoint = hitCol.ClosestPoint(transform.position);

            // Mevcut IDamageable.TakeDamage() çağır
            target.TakeDamage(intDmg);

            // Hit landed event
            EventBus.Publish(new AttackLandedEvent
            {
                Attacker = gameObject,
                Target = (target as Component)?.gameObject,
                Damage = intDmg,
                IsCritical = false,
                IsStaggering = false,
                HitPoint = damageInfo.HitPoint
            });
        }

        // ========================================
        // DEFENSE — Block / Parry / Dodge
        // ========================================

        [ServerRpc]
        public void ServerRequestBlock(bool blocking)
        {
            if (_combatState.IsDead) return;
            _isBlocking = blocking;
            _combatState.ServerSetCombatState(blocking ? 2 : 0); // 2=Blocking, 0=Idle
        }

        [ServerRpc]
        public void ServerRequestParry(DefenseRequest request)
        {
            if (_combatState.IsDead) return;
            _isParryWindowActive = true;
            _parryWindowStart = Time.time;
            _combatState.ServerSetCombatState(3); // Parrying
            ObserversPlayParry();
            StartCoroutine(CloseParryWindow());
        }

        private IEnumerator CloseParryWindow()
        {
            float window = _parryWindowDuration + (parryLatencyToleranceMs / 1000f);
            yield return new WaitForSeconds(window);
            _isParryWindowActive = false;
            if (_combatState.CombatStateValue == 3)
                _combatState.ServerSetCombatState(0);
        }

        public bool IsParryActive()
        {
            if (!_isParryWindowActive) return false;
            float elapsed = Time.time - _parryWindowStart;
            return elapsed <= _parryWindowDuration + (parryLatencyToleranceMs / 1000f);
        }

        private void HandleParrySuccess(NetworkObject targetNob, NetworkCombatController targetCtrl)
        {
            // Attacker (bu player) stagger
            _combatState.ServerSetCombatState(5); // Staggered
            _animEvents?.PlayStagger();

            // Parry yapana stamina refund
            var targetState = targetNob.GetComponent<NetworkCombatState>();
            targetState?.ServerConsumeStamina(-10f); // Negative = restore

            StartCoroutine(EndStagger(1.5f));
        }

        private IEnumerator EndStagger(float dur)
        {
            yield return new WaitForSeconds(dur);
            if (_combatState.CombatStateValue == 5)
                _combatState.ServerSetCombatState(0);
        }

        [ServerRpc]
        public void ServerRequestDodge(DefenseRequest request)
        {
            if (_combatState.IsDead) return;
            float cost = 25f;
            if (!_combatState.ServerConsumeStamina(cost)) return;

            _combatState.ServerSetInvulnerable(true);
            _combatState.ServerSetCombatState(4); // Dodging
            ObserversPlayDodge();
            StartCoroutine(EndDodge());
        }

        private IEnumerator EndDodge()
        {
            yield return new WaitForSeconds(dodgeIFrameDuration);
            _combatState.ServerSetInvulnerable(false);
            if (_combatState.CombatStateValue == 4)
                _combatState.ServerSetCombatState(0);
        }

        // ========================================
        // DAMAGE CALCULATION
        // ========================================

        private float CalculateDamage(int type)
        {
            // Mevcut silahtan damage oku
            float baseDmg = 10f;
            if (_playerCombat != null)
            {
                var weapon = _playerCombat.GetCurrentWeapon();
                if (weapon != null && weapon.Data != null)
                    baseDmg = weapon.Data.baseDamage;
            }

            float dmg = type switch
            {
                0 => baseDmg,            // Light
                1 => baseDmg * 1.8f,     // Heavy
                2 => baseDmg * 2.5f,     // Special
                _ => baseDmg
            };

            // Combo multiplier
            if (_comboSync != null)
                dmg *= _comboSync.GetComboMultiplier();

            return dmg;
        }

        private float CalculatePoiseDamage(int type) =>
            type switch { 0 => 15f, 1 => 30f, 2 => 50f, _ => 15f };

        private float GetAttackStaminaCost(int type)
        {
            var settings = CombatSettings.Instance;
            if (settings == null)
                return type switch { 0 => 15f, 1 => 30f, 2 => 40f, _ => 15f };

            return type switch
            {
                0 => settings.LightAttackStamina,
                1 => settings.HeavyAttackStamina,
                2 => settings.HeavyAttackStamina * 1.33f, // Special attack
                _ => settings.LightAttackStamina
            };
        }

        private float GetBlockStaminaDrain(int type) =>
            GetAttackStaminaCost(type) * 0.8f;

        private float GetHitFrameDelay(int type) =>
            type switch { 0 => 0.15f, 1 => 0.35f, 2 => 0.5f, _ => 0.15f };

        // ========================================
        // BROADCAST
        // ========================================

        private void BroadcastHit(HitResultType type, NetworkObject target,
            float dmg, float poise, Collider col)
        {
            Vector3 point = col != null ? col.ClosestPoint(transform.position) : transform.position;
            Vector3 normal = (point - transform.position).normalized;

            ObserversOnHit(new NetworkHitResult
            {
                TargetObjectId = target.ObjectId,
                DamageDealt = dmg,
                PoiseDamage = poise,
                ResultType = type,
                HitPoint = point,
                HitNormal = normal,
                HitReactionType = (int)type
            });
        }

        [ObserversRpc]
        private void ObserversOnHit(NetworkHitResult result)
        {
            // 1. Hit VFX — result type'a göre
            switch (result.ResultType)
            {
                case HitResultType.NormalHit:
                case HitResultType.Staggered:
                    HitSparkVFX.Instance?.SpawnHitSpark(result.HitPoint, result.HitNormal, HitSparkType.Default);
                    break;
                case HitResultType.CriticalHit:
                    HitSparkVFX.Instance?.SpawnHitSpark(result.HitPoint, result.HitNormal, HitSparkType.Critical);
                    break;
                case HitResultType.Blocked:
                    HitSparkVFX.Instance?.SpawnHitSpark(result.HitPoint, result.HitNormal, HitSparkType.Block);
                    break;
                case HitResultType.Parried:
                    HitSparkVFX.Instance?.SpawnHitSpark(result.HitPoint, result.HitNormal, HitSparkType.Parry);
                    break;
                case HitResultType.Killed:
                    HitSparkVFX.Instance?.SpawnHitSpark(result.HitPoint, result.HitNormal, HitSparkType.Blood);
                    break;
            }

            // 2. Damage popup + event
            if (result.DamageDealt > 0)
            {
                EventBus.Publish(new AttackLandedEvent
                {
                    Attacker = gameObject,
                    Target = null,
                    Damage = Mathf.RoundToInt(result.DamageDealt),
                    IsCritical = result.ResultType == HitResultType.CriticalHit,
                    IsStaggering = result.ResultType == HitResultType.Staggered,
                    HitPoint = result.HitPoint
                });
            }
        }

        [ObserversRpc]
        private void ObserversPlayAttack(int attackType, int comboIndex)
        {
            if (_animator != null)
            {
                _animator.SetInteger(Dungeons.Core.Utilities.AnimatorHash.ComboIndex, comboIndex > 0 ? comboIndex : 1);
            }

            if (attackType == 1)
                _animEvents?.PlayHeavyAttack();
            else
                _animEvents?.PlayLightAttack();
        }

        [ObserversRpc]
        private void ObserversPlayParry()
        {
            _animEvents?.PlayParry();
        }

        [ObserversRpc]
        private void ObserversPlayDodge()
        {
            _animEvents?.PlayDodge();
        }

        /// <summary>
        /// Server → Target client: Hit reaction animasyonu oynat
        /// </summary>
        [TargetRpc]
        public void TargetPlayHitReaction(FishNet.Connection.NetworkConnection conn, bool isStagger)
        {
            if (isStagger)
                _animEvents?.PlayStagger();
            else
                _animEvents?.PlayHit();
        }

        // ========================================
        // KICK — Server Validation
        // ========================================

        [ServerRpc]
        public void ServerRequestKick(Vector3 direction)
        {
            if (_combatState.IsDead) return;

            // Stamina cost from CombatSettings
            float kickStaminaCost = CombatSettings.Instance?.KickStaminaCost ?? 15f;
            if (!_combatState.ServerConsumeStamina(kickStaminaCost)) return;

            // Combo reset on kick
            _comboSync?.ResetCombo();

            // Kick animation broadcast
            ObserversPlayKick();

            // Kick hit detection
            Vector3 kickDir = direction.normalized;
            Vector3 kickOrigin = transform.position + kickDir * 1f + Vector3.up * 0.5f;
            int hitCount = Physics.OverlapSphereNonAlloc(
                kickOrigin, 2f, _hitBuffer, hitLayers, QueryTriggerInteraction.Collide);

            _processedTargets.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _hitBuffer[i];
                if (hit.transform == transform) continue;
                if (hit.transform.IsChildOf(transform)) continue;

                // IKickable check (TryGetComponent avoids GC allocation on null)
                if (!hit.TryGetComponent<Data.Interfaces.IKickable>(out var kickable))
                {
                    var kp = hit.transform.parent;
                    if (kp != null) kp.TryGetComponent<Data.Interfaces.IKickable>(out kickable);
                }
                if (kickable != null)
                {
                    kickable.Kick(kickDir, 15f);
                }

                // Player target → knockback + small damage (TryGetComponent avoids GC allocation on null)
                FishNet.Object.NetworkObject targetNob = null;
                if (!hit.TryGetComponent<FishNet.Object.NetworkObject>(out targetNob))
                {
                    var tp = hit.transform.parent;
                    if (tp != null) tp.TryGetComponent<FishNet.Object.NetworkObject>(out targetNob);
                }
                if (targetNob != null && targetNob != NetworkObject && !_processedTargets.Contains(targetNob.ObjectId))
                {
                    _processedTargets.Add(targetNob.ObjectId);
                    var targetState = targetNob.GetComponent<NetworkCombatState>();
                    if (targetState != null)
                    {
                        targetState.ServerApplyDamage(5f);

                        // Hit VFX broadcast
                        Vector3 hitPoint = hit.ClosestPoint(transform.position);
                        ObserversOnHit(new NetworkHitResult
                        {
                            TargetObjectId = targetNob.ObjectId,
                            DamageDealt = 5f,
                            PoiseDamage = 20f,
                            ResultType = HitResultType.NormalHit,
                            HitPoint = hitPoint,
                            HitNormal = kickDir,
                            HitReactionType = 0
                        });
                    }
                }
            }
        }

        [ObserversRpc]
        private void ObserversPlayKick()
        {
            _animEvents?.PlayKick();
        }
    }
}
