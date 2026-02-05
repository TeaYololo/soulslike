using UnityEngine;

namespace Dungeons.Combat
{
    /// <summary>
    /// Saldırı isteği — Client → Server.
    /// </summary>
    public struct AttackRequest
    {
        public int AttackType;          // 0: light, 1: heavy, 2: special
        public int ComboIndex;          // Combo zincirindeki sıra
        public uint ClientTick;         // Client'ın tick'i (latency compensation)
        public Vector3 AimDirection;    // Saldırı yönü
    }

    /// <summary>
    /// Defense isteği — Client → Server.
    /// </summary>
    public struct DefenseRequest
    {
        public DefenseType Type;
        public uint ClientTick;
        public Vector3 DodgeDirection;
    }

    public enum DefenseType : byte
    {
        Block = 0,
        Parry = 1,
        Dodge = 2
    }

    /// <summary>
    /// Hit sonucu — Server hesaplar, Client'lara broadcast.
    /// </summary>
    public struct NetworkHitResult
    {
        public int TargetObjectId;      // NetworkObject ID
        public float DamageDealt;
        public float PoiseDamage;
        public HitResultType ResultType;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public int HitReactionType;     // Animasyon tipi
    }

    public enum HitResultType : byte
    {
        NormalHit = 0,
        Blocked = 1,
        Parried = 2,
        Dodged = 3,
        Killed = 4,
        Staggered = 5,
        CriticalHit = 6
    }
}
