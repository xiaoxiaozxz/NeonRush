using UnityEngine;

namespace LoongRacing
{
    [RequireComponent(typeof(ArcadeKartController)), DisallowMultipleComponent]
    public sealed class KartAudioController : MonoBehaviour
    {
        private const string VolumeKey = "LoongRacing.Audio.Volume";
        [SerializeField] private KartAudioBank bank;
        private ArcadeKartController kart;
        private AudioSource idle, drive, tyre, wind, effects, ui;
        private float master = 0.65f, restoredVolume = 0.65f, impactCooldown;
        private int collisionVariation;
        public float MasterVolume => master;
        public float EnginePitch => drive == null ? 0f : drive.pitch;
        public float EngineVolume => drive == null ? 0f : drive.volume;
        public KartAudioBank Bank => bank;

        public void Configure(KartAudioBank sounds) => bank = sounds;

        private void Awake()
        {
            kart = GetComponent<ArcadeKartController>();
            master = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.65f));
            restoredVolume = master > 0f ? master : 0.65f;
            if (bank == null) return;
            idle = Source("Idle", bank.idleLoop, true);
            drive = Source("Drive", bank.driveLoop, true);
            tyre = Source("Tyres", bank.tyreLoop, true);
            wind = Source("Wind", bank.windLoop, true);
            effects = Source("Vehicle SFX", null, false);
            ui = Source("UI SFX", null, false);
            ui.ignoreListenerPause = true;
            ui.priority = 40;
            kart.Respawned += PlayReset;
            kart.NitroChargeAdded += PlayGasReady;
            kart.BoostStarted += PlayBoost;
        }

        private AudioSource Source(string title, AudioClip clip, bool loop)
        {
            var child = new GameObject(title);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.loop = loop;
            source.volume = 0f;
            source.clip = clip;
            if (loop && clip != null) source.Play();
            return source;
        }

        private void Update()
        {
            if (bank == null || drive == null) return;
            float dt = Time.unscaledDeltaTime;
            impactCooldown = Mathf.Max(0f, impactCooldown - Time.deltaTime);
            float speed = Mathf.Clamp01(kart.SpeedKph / (kart.Tuning.maximumForwardSpeed * 3.6f));
            float throttle = Mathf.Abs(kart.Throttle);
            float smooth = 1f - Mathf.Exp(-9f * dt);
            float audible = Time.timeScale > 0f ? master : 0f;
            drive.pitch = Mathf.Lerp(drive.pitch, 0.76f + speed * 1.05f + throttle * 0.12f, smooth);
            idle.pitch = Mathf.Lerp(idle.pitch, 0.88f + throttle * 0.16f, smooth);
            idle.volume = audible * 0.18f * (1f - speed * 0.7f);
            drive.volume = Mathf.Lerp(drive.volume, audible * (0.065f + speed * 0.2f + throttle * 0.085f), smooth);
            wind.volume = audible * speed * speed * 0.14f;
            float slip = kart.Body == null ? 0f : Mathf.Abs(Vector3.Dot(kart.Body.linearVelocity, kart.transform.right));
            float braking = kart.Throttle < -0.1f && kart.ForwardSpeed > 4f ? speed * 0.65f : 0f;
            float skid = kart.IsGrounded ? Mathf.Max(Mathf.InverseLerp(1.8f, 9f, slip), braking) : 0f;
            tyre.volume = Mathf.Lerp(tyre.volume, audible * skid * 0.32f, smooth);
            tyre.pitch = 0.8f + speed * 0.35f;
        }

        public void SetMasterVolume(float value)
        {
            master = Mathf.Clamp01(value);
            if (master > 0f) restoredVolume = master;
            PlayerPrefs.SetFloat(VolumeKey, master);
        }

        public void ToggleMute() => SetMasterVolume(master > 0.001f ? 0f : restoredVolume);

        public void PlayPause(bool paused)
        {
            if (bank != null) PlayUi(paused ? bank.pause : bank.resume);
        }

        public void PlayCountdown() { if (bank != null) PlayUi(bank.countdown); }
        public void PlayRaceStart() { if (bank != null) PlayUi(bank.raceStart); }

        public void PlayGasReady() { if (bank != null) PlayEffect(bank.gasReady, 0.65f); }
        public void PlayNitro() { if (bank != null) PlayEffect(bank.nitro, 0.7f); }
        public void PlayMiniBoost() { if (bank != null) PlayEffect(bank.miniBoost, 0.6f); }
        public void PlayAirBoost() { if (bank != null) PlayEffect(bank.airBoost, 0.6f); }
        private void PlayBoost(KartBoostKind kind)
        {
            switch (kind)
            {
                case KartBoostKind.Nitro: PlayNitro(); break;
                case KartBoostKind.Air: PlayAirBoost(); break;
                case KartBoostKind.Mini:
                case KartBoostKind.Landing: PlayMiniBoost(); break;
            }
        }
        private void PlayReset() { if (bank != null) PlayUi(bank.reset); }

        private void PlayUi(AudioClip clip)
        {
            if (ui != null && clip != null) { ui.volume = master; ui.PlayOneShot(clip, 0.5f); }
        }

        private void PlayEffect(AudioClip clip, float volume)
        {
            if (effects != null && clip != null) { effects.volume = master; effects.PlayOneShot(clip, volume); }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (bank == null || effects == null || impactCooldown > 0f || collision.contactCount == 0) return;
            ContactPoint contact = collision.GetContact(0);
            float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (impact < 2.5f) return;
            float strength = Mathf.Lerp(0.18f, 0.75f, Mathf.InverseLerp(2.5f, 24f, impact));
            if (Vector3.Dot(contact.normal, Vector3.up) > 0.5f) PlayEffect(bank.landing, strength * 0.6f);
            else if (bank.collisions != null && bank.collisions.Length > 0)
                PlayEffect(bank.collisions[collisionVariation++ % bank.collisions.Length], strength);
            impactCooldown = 0.18f;
        }

        private void OnEnable()
        {
            foreach (var source in GetComponentsInChildren<AudioSource>())
                if (source.loop && source.clip != null && !source.isPlaying) source.Play();
        }

        private void OnDisable()
        {
            foreach (var source in GetComponentsInChildren<AudioSource>()) source.Stop();
        }

        private void OnDestroy()
        {
            if (kart == null) return;
            kart.Respawned -= PlayReset;
            kart.NitroChargeAdded -= PlayGasReady;
            kart.BoostStarted -= PlayBoost;
        }
    }
}
