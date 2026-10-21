using UnityEngine;

namespace LoongRacing
{
    [CreateAssetMenu(menuName = "NeonRush/Audio Bank")]
    public sealed class KartAudioBank : ScriptableObject
    {
        public AudioClip idleLoop, driveLoop, tyreLoop, windLoop;
        public AudioClip[] collisions;
        public AudioClip reset, pause, resume, gasReady;
        public AudioClip nitro, miniBoost, airBoost, landing, countdown, raceStart;
    }
}
