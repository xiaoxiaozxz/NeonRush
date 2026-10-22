using UnityEngine;

namespace LoongRacing
{
    public static class RaceRecordStore
    {
        public static float Load(string key)
        {
            float value = PlayerPrefs.GetFloat(key, 0f);
            return RaceTimeRules.IsValid(value) ? value : 0f;
        }

        public static bool TryImprove(string key, float seconds)
        {
            if (!RaceTimeRules.IsValid(seconds)) return false;
            float previous = Load(key);
            if (!RaceTimeRules.IsImprovement(seconds, previous)) return false;
            PlayerPrefs.SetFloat(key, seconds);
            PlayerPrefs.Save();
            return true;
        }
    }
}
