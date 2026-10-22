using System.Collections.Generic;
using UnityEngine;

namespace LoongRacing
{
    public enum KartRaceState { Ready, Countdown, Racing, Finished, Practice }

    [DefaultExecutionOrder(200), DisallowMultipleComponent, RequireComponent(typeof(KartRouteProgress))]
    public sealed class KartRaceSession : MonoBehaviour
    {
        [SerializeField, Range(1, 3)] private int selectedLaps = 1;
        private ArcadeKartController kart;
        private KartRouteProgress progress;
        private KartAudioController sounds;
        private readonly List<float> lapTimes = new List<float>();
        private double countdownEnd, raceStart, lapStart, goUntil;
        private float finishedTime;
        private bool resetting;
        private int lastCountdownNumber;

        public KartRaceState State { get; private set; }
        public int TotalLaps => selectedLaps;
        public int CurrentLap => Mathf.Min(lapTimes.Count + 1, selectedLaps);
        public IReadOnlyList<float> LapTimes => lapTimes;
        public int CountdownNumber => State == KartRaceState.Countdown ? Mathf.Max(1, Mathf.CeilToInt((float)(countdownEnd-Time.timeAsDouble))) : 0;
        public bool ShowGo => State == KartRaceState.Racing && Time.timeAsDouble < goUntil;
        public float ElapsedSeconds => State == KartRaceState.Racing ? Mathf.Max(0f, (float)(Time.timeAsDouble-raceStart)) : finishedTime;
        public float CurrentLapSeconds => State == KartRaceState.Racing ? Mathf.Max(0f, (float)(Time.timeAsDouble-lapStart))
            : State == KartRaceState.Finished && lapTimes.Count > 0 ? lapTimes[lapTimes.Count-1] : 0f;
        public float BestLap { get; private set; }
        public float BestTotal { get; private set; }
        public bool NewBestLap { get; private set; }
        public bool NewBestTotal { get; private set; }
        public string RecordPrefix => "LoongRacing.Race.v1." + progress.Route.name + "." + Mathf.RoundToInt(progress.Route.Length);
        public string BestLapKey => RecordPrefix + ".lap";
        public string BestTotalKey => RecordPrefix + ".total." + selectedLaps;

        private void Awake()
        {
            kart = GetComponent<ArcadeKartController>();
            progress = GetComponent<KartRouteProgress>();
            sounds = GetComponent<KartAudioController>();
        }

        private void OnEnable()
        {
            progress.CircuitCompleted += OnCircuitCompleted;
            kart.PracticeReset += OnExternalReset;
            ReturnToMenu();
        }

        private void OnDisable()
        {
            if (progress != null) { progress.CircuitCompleted -= OnCircuitCompleted; progress.SetCheckpointCollection(true); }
            if (kart != null) { kart.PracticeReset -= OnExternalReset; kart.SetDrivingEnabled(true); }
        }

        private void ResetRun(KartRaceState state)
        {
            resetting = true;
            kart.ResetToSpawn();
            resetting = false;
            lapTimes.Clear();
            finishedTime = 0f;
            NewBestLap = NewBestTotal = false;
            State = state;
            bool driving = state == KartRaceState.Practice;
            kart.SetDrivingEnabled(driving);
            progress.SetCheckpointCollection(driving);
            LoadRecords();
        }

        private void LoadRecords()
        {
            BestLap = RaceRecordStore.Load(BestLapKey);
            BestTotal = RaceRecordStore.Load(BestTotalKey);
        }

        public void SelectLaps(int count)
        {
            if (State != KartRaceState.Ready && State != KartRaceState.Finished) return;
            selectedLaps = count == 3 ? 3 : 1;
            LoadRecords();
        }

        public void ReturnToMenu() => ResetRun(KartRaceState.Ready);
        public void StartPractice() => ResetRun(KartRaceState.Practice);

        public void StartChallenge(int laps)
        {
            selectedLaps = laps == 3 ? 3 : 1;
            ResetRun(KartRaceState.Countdown);
            countdownEnd = Time.timeAsDouble + 3.0;
            lastCountdownNumber = 3;
            if (sounds != null) sounds.PlayCountdown();
        }

        private void OnExternalReset()
        {
            if (!resetting) ReturnToMenu();
        }

        private void Update()
        {
            if (State != KartRaceState.Countdown) return;
            if (Time.timeAsDouble >= countdownEnd)
            {
                State = KartRaceState.Racing;
                raceStart = lapStart = Time.timeAsDouble;
                goUntil = raceStart + 0.8;
                progress.SetCheckpointCollection(true);
                kart.SetDrivingEnabled(true);
                if (sounds != null) sounds.PlayRaceStart();
            }
            else if (CountdownNumber != lastCountdownNumber)
            {
                lastCountdownNumber = CountdownNumber;
                if (sounds != null) sounds.PlayCountdown();
            }
        }

        private void OnCircuitCompleted()
        {
            if (State != KartRaceState.Racing) return;
            double now = Time.timeAsDouble;
            lapTimes.Add(Mathf.Max(0f, (float)(now-lapStart)));
            lapStart = now;
            if (sounds != null) sounds.PlayGasReady();
            if (lapTimes.Count < selectedLaps) return;

            finishedTime = Mathf.Max(0f, (float)(now-raceStart));
            State = KartRaceState.Finished;
            kart.SetDrivingEnabled(false);
            progress.SetCheckpointCollection(false);
            float fastest = float.PositiveInfinity;
            foreach (float lap in lapTimes) fastest = Mathf.Min(fastest, lap);
            NewBestLap = RaceRecordStore.TryImprove(BestLapKey, fastest);
            NewBestTotal = RaceRecordStore.TryImprove(BestTotalKey, finishedTime);
            LoadRecords();
        }
    }
}
