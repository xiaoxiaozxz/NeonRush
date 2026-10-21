using UnityEngine;

namespace LoongRacing
{
    public sealed partial class PracticeHud
    {
        private Texture2D routeTexture;
        private TrackRoute cachedMapRoute;
        private Vector2[] gateMapPoints;
        private float cachedMapLength;
        private string mapTitle;
        private float nextTextRefresh;
        private int textGate = -1, textCharges = -1, textRecoveries = -1;
        private KartRaceState textRaceState;
        private string modeCaption, clockCaption, timeCaption, lapCaption, chargeCaption;
        private string topSpeedCaption, recordCaption, speedCaption, gateCaption, circuitCaption;
        private bool fontWarmed;

        private void RefreshHudText()
        {
            var state = race == null ? KartRaceState.Practice : race.State;
            int gate = progress == null ? -1 : progress.NextCheckpointIndex;
            int recoveries = progress == null ? 0 : progress.RecoveryCount;
            // Only display text is sampled at 20 Hz. Physics, boost windows, bars and race clocks stay full rate.
            if (timeCaption != null && Time.unscaledTime < nextTextRefresh && state == textRaceState &&
                gate == textGate && kart.NitroCharges == textCharges && recoveries == textRecoveries) return;
            nextTextRefresh = Time.unscaledTime + 0.05f;
            textGate = gate; textCharges = kart.NitroCharges; textRecoveries = recoveries; textRaceState = state;
            bool challenge = race != null && state != KartRaceState.Practice;
            modeCaption = progress == null ? "自由练习    ·    环形训练场" :
                (challenge ? "计时挑战" : "自由练习") + "    ·    " + progress.Route.DisplayName;
            clockCaption = !challenge ? "本次驾驶时间" : state == KartRaceState.Countdown ? "准备起跑" :
                $"第 {race.CurrentLap} / {race.TotalLaps} 圈  ·  总用时";
            timeCaption = FormatTime(challenge ? race.ElapsedSeconds : kart.DrivingTime, false);
            lapCaption = challenge ? "本圈 " + FormatTime(race.CurrentLapSeconds) : "";
            chargeCaption = $"储气   {kart.NitroCharges} / {ArcadeKartController.MaximumNitroCharges}      集气 {kart.DriftEnergy:0}%";
            topSpeedCaption = $"最高速度     {kart.TopSpeedKph:0} km/h";
            recordCaption = challenge ? $"最佳 {FormatTime(race.BestLap)}    救援 {recoveries} 次" :
                progress == null ? $"行驶距离     {kart.DistanceMetres:0} m" : $"距离 {kart.DistanceMetres:0} m    救援 {recoveries} 次";
            speedCaption = Mathf.RoundToInt(kart.SpeedKph).ToString("000");
            if (progress != null)
            {
                gateCaption = $"目标检查点  {gate + 1} / {progress.Route.CheckpointCount}";
                circuitCaption = challenge ? $"已完成 {race.LapTimes.Count} / {race.TotalLaps} 圈" : $"已完整通过 {progress.CompletedCircuits} 次";
            }
        }

        private void WarmHudFont()
        {
            if (fontWarmed || Event.current.type != EventType.Repaint) return;
            fontWarmed = true;
            const string digits = "0123456789:-.% /kmh";
            const string glyphs = "NEONRUSH DRIFT SHIFT REVERSE SPEED WASD QWEAMR F5 GO! ↑↓←→●◇" + digits +
                "本次驾驶时间自由练习计时挑战等待起跑准备第圈总用本圈操作加速刹车停稳后倒转向漂移释放氮气小喷空落地救援暂停继续静音恢复声音重新" +
                "集气中储提按住方向键机就绪出弯腾或留意提示松开再轻点触发持续无效加最高度最佳距离检查已完整通过抓行驶窗口启成目标正在逆请赛道箭头黄色左侧可从右绕所有保秒与库存";
            string warm = glyphs + "记录路线+（）()：，。、！·→" +
                (progress == null || progress.Route == null ? "环形训练场" : progress.Route.DisplayName);
            foreach (int size in new[] { 12, 13, 14, 15, 16, 17, 18, 19, 20, 22, 24, 27 })
            {
                label.fontSize = size; label.fontStyle = FontStyle.Normal;
                font.RequestCharactersInTexture(warm, size, FontStyle.Normal);
                label.CalcSize(new GUIContent(warm));
                if (size == 15 || size == 18 || size == 20 || size == 22 || size == 24 || size == 27)
                {
                    label.fontStyle = FontStyle.Bold;
                    font.RequestCharactersInTexture(warm, size, FontStyle.Bold);
                    label.CalcSize(new GUIContent(warm));
                }
            }
            foreach (int size in new[] { 83, 92, 112 })
            {
                label.fontSize = size; label.fontStyle = FontStyle.Bold;
                font.RequestCharactersInTexture(digits + "GO!", size, FontStyle.Bold);
                label.CalcSize(new GUIContent(digits + "GO!"));
            }
        }

        private void EnsureMapCache()
        {
            var route = progress == null ? null : progress.Route;
            if (route == null) return;
            if (route == cachedMapRoute && routeTexture != null && Mathf.Approximately(route.Length, cachedMapLength) &&
                gateMapPoints.Length == route.CheckpointCount) return;
            cachedMapRoute = route; cachedMapLength = route.Length;
            mapTitle = $"路线  /  {route.Length:0} m";
            var samples = new Vector3[129];
            mapMinimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            mapMaximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < samples.Length; i++)
            {
                route.Sample(route.Length * i / 128f, out samples[i], out Vector3 forward);
                mapMinimum = Vector2.Min(mapMinimum, new Vector2(samples[i].x, samples[i].z));
                mapMaximum = Vector2.Max(mapMaximum, new Vector2(samples[i].x, samples[i].z));
            }
            const int width = 512, height = 448;
            var pixels = new Color32[width * height];
            for (int i = 0; i < samples.Length - 1; i++)
            {
                Vector2 a = MapPoint(samples[i]), b = MapPoint(samples[i + 1]);
                a = new Vector2((a.x - 1294) * width / 240f, (581 - a.y) * height / 210f);
                b = new Vector2((b.x - 1294) * width / 240f, (581 - b.y) * height / 210f);
                RasterMapLine(pixels, width, height, a, b);
            }
            if (routeTexture != null) Destroy(routeTexture);
            routeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "Cached Route Map", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave };
            routeTexture.SetPixels32(pixels); routeTexture.Apply(false, true);
            gateMapPoints = new Vector2[route.CheckpointCount];
            for (int i = 0; i < gateMapPoints.Length; i++)
            { route.Sample(route.CheckpointDistance(i), out Vector3 p, out Vector3 f); gateMapPoints[i] = MapPoint(p); }
            nextTextRefresh = 0;
        }

        private static void RasterMapLine(Color32[] pixels, int width, int height, Vector2 a, Vector2 b)
        {
            const float radius = 3.2f;
            Vector2 delta = b - a;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 1), 0, width - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 1), 0, width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 1), 0, height - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 1), 0, height - 1);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, delta) / Mathf.Max(0.001f, delta.sqrMagnitude));
                byte alpha = (byte)(255 * Mathf.Clamp01(radius + 0.5f - Vector2.Distance(p, a + delta * t)));
                int index = y * width + x;
                if (alpha > pixels[index].a) pixels[index] = new Color32(92, 133, 148, alpha);
            }
        }
    }
}
