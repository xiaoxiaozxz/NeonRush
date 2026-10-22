namespace LoongRacing
{
    /// <summary>Record rules independent of Unity objects, storage and frame updates.</summary>
    public static class RaceTimeRules
    {
        public static bool IsValid(float seconds) =>
            seconds > 0f && !float.IsNaN(seconds) && !float.IsInfinity(seconds);

        public static bool IsImprovement(float candidate, float previous) =>
            IsValid(candidate) && (!IsValid(previous) || candidate < previous);
    }
}
