using System.Collections.Generic;
using UnityEngine;
using Dungeons.Player;

namespace Dungeons.Combat
{
    /// <summary>
    /// Static registry for lock-on targets.
    /// FindObjectsByType yerine kullanılır - GC allocation önler.
    /// </summary>
    public static class LockOnTargetRegistry
    {
        private static readonly HashSet<PlayerLockOnTarget> _targets = new();

        /// <summary>
        /// Tüm aktif lock-on target'ları
        /// </summary>
        public static IReadOnlyCollection<PlayerLockOnTarget> Targets => _targets;

        /// <summary>
        /// Target sayısı
        /// </summary>
        public static int Count => _targets.Count;

        /// <summary>
        /// Target'ı registry'e ekle
        /// </summary>
        public static void Register(PlayerLockOnTarget target)
        {
            if (target != null)
            {
                _targets.Add(target);
            }
        }

        /// <summary>
        /// Target'ı registry'den çıkar
        /// </summary>
        public static void Unregister(PlayerLockOnTarget target)
        {
            if (target != null)
            {
                _targets.Remove(target);
            }
        }

        /// <summary>
        /// Registry'i temizle (scene değişikliğinde çağrılabilir)
        /// </summary>
        public static void Clear()
        {
            _targets.Clear();
        }
    }
}
