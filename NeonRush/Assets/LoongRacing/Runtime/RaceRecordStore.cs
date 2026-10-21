using UnityEngine;

namespace LoongRacing
{
    public static class RaceRecordStore
    {
        public static float Load(string key)
        {
            float value = PlayerPrefs.GetFloat(key, 0f);
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value) ? value : 0f;
        }

        public static bool TryImprove(string key, float seconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return false;
            float previous = Load(key);
            if (previous > 0f && seconds >= previous) return false;
            PlayerPrefs.SetFloat(key, seconds);
            PlayerPrefs.Save();
            return true;
        }
    }
}
