using UnityEngine;

namespace Dungeons.Combat.Utility
{
    /// <summary>
    /// Animator layer helper metotlarini barindir static utility sinifi.
    /// Duplicate metotlarin tek noktadan yonetilmesini saglar.
    /// Unity'nin AnimatorUtility sinifiyla cakismayi onlemek icin farkli isim kullanildi.
    /// </summary>
    public static class AnimatorLayerHelper
    {
        /// <summary>
        /// Animator layer index'ini guvenli sekilde alir.
        /// Layer bulunamazsa -1 doner.
        /// </summary>
        /// <param name="animator">Animator component</param>
        /// <param name="layerName">Layer adi</param>
        /// <returns>Layer index veya -1 (bulunamazsa)</returns>
        public static int GetLayerIndex(Animator animator, string layerName)
        {
            if (animator == null) return -1;

            for (int i = 0; i < animator.layerCount; i++)
            {
                if (animator.GetLayerName(i) == layerName)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Layer weight'ini guvenli sekilde ayarlar.
        /// Animator veya layer bulunamazsa islem yapmaz.
        /// </summary>
        /// <param name="animator">Animator component</param>
        /// <param name="layerName">Layer adi</param>
        /// <param name="weight">Hedef weight (0-1)</param>
        /// <returns>Basarili mi?</returns>
        public static bool SetLayerWeight(Animator animator, string layerName, float weight)
        {
            int index = GetLayerIndex(animator, layerName);
            if (index < 0) return false;

            animator.SetLayerWeight(index, weight);
            return true;
        }

        /// <summary>
        /// Layer'i aktif/pasif yapar (weight 1 veya 0).
        /// </summary>
        public static bool SetLayerEnabled(Animator animator, string layerName, bool enabled)
        {
            return SetLayerWeight(animator, layerName, enabled ? 1f : 0f);
        }
    }
}
