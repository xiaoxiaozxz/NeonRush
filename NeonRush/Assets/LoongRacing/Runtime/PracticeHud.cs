using UnityEngine;
using UnityEngine.InputSystem;

namespace LoongRacing
{
    public sealed class PracticeHud : MonoBehaviour
    {
        [SerializeField] private ArcadeKartController kart;
        private Font font;
        private GUIStyle label;
        private bool paused;
        private KartAudioController sounds;
        private KartRouteProgress progress;
        private KartRaceSession race;
        private Vector2 mapMinimum, mapMaximum;
        private readonly Color ink = new Color(0.025f, 0.055f, 0.075f, 0.92f);
        private readonly Color muted = new Color(0.57f, 0.7f, 0.74f);
        private readonly Color accent = new Color(0.13f, 0.94f, 0.84f);
        private readonly Color yellow = new Color(1f, 0.79f, 0.24f);

        public bool IsPaused => paused;
        public void Configure(ArcadeKartController vehicle) => kart = vehicle;

        private void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (kart != null) sounds = kart.GetComponent<KartAudioController>();
            if (kart != null) progress = kart.GetComponent<KartRouteProgress>();
            if (kart != null) race = kart.GetComponent<KartRaceSession>();
            mapMinimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            mapMaximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            if (progress != null && progress.Route != null)
                for (int i = 0; i < 128; i++)
                {
                    progress.Route.Sample(progress.Route.Length * i / 128f, out Vector3 p, out Vector3 f);
                    mapMinimum = Vector2.Min(mapMinimum, new Vector2(p.x, p.z));
                    mapMaximum = Vector2.Max(mapMaximum, new Vector2(p.x, p.z));
                }
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 24);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) TogglePause();
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame && sounds != null) sounds.ToggleMute();
            if (race != null && Keyboard.current != null && Application.isFocused)
            {
                if (Keyboard.current.f5Key.wasPressedThisFrame) BeginChallenge(race.TotalLaps);
                else if (Keyboard.current.enterKey.wasPressedThisFrame && !paused
                    && (race.State == KartRaceState.Ready || race.State == KartRaceState.Finished)) BeginChallenge(race.TotalLaps);
            }
        }

        public void TogglePause()
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (sounds != null) sounds.PlayPause(paused);
            if (kart != null) { kart.SetDriveInput(0f, 0f); kart.CancelBufferedActions(); }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (isActiveAndEnabled && !focused && !paused && Application.isPlaying) TogglePause();
        }

        private void OnGUI()
        {
            if (kart == null) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { font = font, richText = false };
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600f * scale) / 2f,
                (Screen.height - 900f * scale) / 2f, 0f), Quaternion.identity, Vector3.one * scale);

            Panel(36, 32, 328, 104);
            Box(new Rect(36, 32, 5, 104), accent);
            Text(58, 40, 282, 40, "NEONRUSH / 霓虹疾驰", 22, Color.white, FontStyle.Bold);
            string mode = race != null && race.State != KartRaceState.Practice ? "计时挑战" : "自由练习";
            Text(58, 84, 282, 28, progress == null ? "自由练习    ·    环形训练场" : mode + "    ·    " + progress.Route.DisplayName, 18, muted);

            bool challenge = race != null && race.State != KartRaceState.Practice;
            Panel(642, 32, 316, challenge ? 104 : 74);
            string clockTitle = !challenge ? "本次驾驶时间" : race.State == KartRaceState.Ready ? "等待起跑"
                : race.State == KartRaceState.Countdown ? "准备起跑" : $"第 {race.CurrentLap} / {race.TotalLaps} 圈  ·  总用时";
            Text(642, 42, 316, 20, clockTitle, 14, muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            float time = challenge ? race.ElapsedSeconds : kart.DrivingTime;
            Text(642, 64, 316, 30, FormatTime(time,false), 27, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (challenge) Text(642, 102, 316, 20, "本圈 " + FormatTime(race.CurrentLapSeconds), 14, accent, FontStyle.Normal, TextAnchor.MiddleCenter);

            Panel(1264, 32, 300, 272);
            Text(1284, 44, 260, 26, "驾驶操作", 18, accent, FontStyle.Bold);
            Text(1284, 78, 260, 24, "WASD / 方向键     驾驶", 16, Color.white);
            Text(1284, 106, 260, 24, "S / ↓     刹车，停稳后倒车", 16, muted);
            Text(1284, 134, 260, 24, "E / SHIFT + 转向   漂移", 16, accent);
            Text(1284, 162, 260, 24, "Q   释放氮气", 16, accent);
            Text(1284, 190, 260, 24, "W   小喷 / 空喷 / 落地喷", 16, accent);
            Text(1284, 218, 260, 24, progress == null ? "R   复位       ESC   暂停" : "R   就近救援    ESC   暂停", 16, muted);
            Text(1284, 246, 260, 24, (sounds != null && sounds.MasterVolume < 0.001f ? "M   恢复声音" : "M   静音") + (race != null ? "    F5   重新挑战" : ""), 15, muted);
            if (progress != null && progress.Route != null) DrawRouteMap();

            Panel(36, 158, 328, 148);
            Text(56, 170, 286, 26, kart.IsDrifting ? "DRIFT  /  漂移中" : "漂移集气", 20, kart.IsDrifting ? yellow : accent, FontStyle.Bold);
            Box(new Rect(56, 213, 286, 14), new Color(0.12f, 0.2f, 0.24f));
            Box(new Rect(56, 213, 286 * kart.DriftEnergy / 100f, 14), accent);
            Text(56, 237, 286, 24, $"储气   {kart.NitroCharges} / {ArcadeKartController.MaximumNitroCharges}      集气 {kart.DriftEnergy:0}%", 17, Color.white);
            Text(56, 272, 286, 20, kart.NitroCharges > 0 ? "按 Q 释放氮气" : "提速后按住 E + 方向键", 13, muted);

            Panel(36, 328, 328, 94);
            var ready = kart.ReadyQuickBoost;
            string readyName = ready == KartBoostKind.Air ? "空喷" : ready == KartBoostKind.Landing ? "落地喷" : "出弯小喷";
            Text(56, 340, 286, 27, ready == KartBoostKind.None ? "W  /  喷射时机" : "W  /  " + readyName + "就绪", 20,
                ready == KartBoostKind.None ? muted : yellow, FontStyle.Bold);
            Text(56, 377, 286, 20, ready == KartBoostKind.None ? "出弯、腾空或落地时留意提示" : "松开再轻点 W 触发（持续按住无效）", 13, muted);

            if (kart.IsBoosting)
            {
                float boostY = challenge ? 152f : 126f;
                Panel(610, boostY, 380, 68);
                string name = kart.IsNitroActive ? (kart.IsBurstActive ? "氮气 + 喷射" : "氮气加速") :
                    kart.ActiveBurst == KartBoostKind.Air ? "空喷" : kart.ActiveBurst == KartBoostKind.Landing ? "落地喷" : "小喷";
                Text(630, boostY+11f, 340, 28, name, 24, accent, FontStyle.Bold, TextAnchor.MiddleCenter);
                float duration = kart.IsNitroActive ? kart.Tuning.nitroDuration : kart.Tuning.quickBoostDuration;
                float remaining = kart.IsNitroActive ? kart.NitroTimeRemaining : kart.BurstTimeRemaining;
                Box(new Rect(638, boostY+52f, 324 * Mathf.Clamp01(remaining / duration), 4), accent);
            }

            Panel(36, 742, 330, 122);
            Text(56, 754, 290, 22, challenge ? "挑战记录" : "练习记录", 15, accent, FontStyle.Bold);
            Text(56, 787, 290, 25, $"最高速度     {kart.TopSpeedKph:0} km/h", 20, Color.white);
            string recordText = challenge ? $"最佳 {FormatTime(race.BestLap)}    救援 {progress.RecoveryCount} 次"
                : progress == null ? $"行驶距离     {kart.DistanceMetres:0} m" : $"距离 {kart.DistanceMetres:0} m    救援 {progress.RecoveryCount} 次";
            Text(56, 822, 290, 22, recordText, 16, muted);

            Panel(1224, 670, 340, 194);
            Text(1246, 681, 292, 24, kart.IsReversing ? "REVERSE  /  倒车" : "SPEED  /  速度", 15, muted);
            Text(1242, 702, 216, 99, $"{Mathf.RoundToInt(kart.SpeedKph):000}", 83, Color.white, FontStyle.Bold);
            Text(1464, 753, 80, 32, "km/h", 20, accent);
            float ratio = Mathf.Clamp01(kart.SpeedKph / (kart.Tuning.maximumForwardSpeed * 3.6f));
            for (int i = 0; i < 24; i++)
                Box(new Rect(1246 + i * 12.1f, 809, 8.5f, 17), i / 24f < ratio ? (i > 18 ? yellow : accent) : new Color(0.13f, 0.21f, 0.25f));
            Text(1246, 836, 292, 16, kart.IsGrounded ? "●  抓地行驶" : "◇  腾空", 12, muted);

            Panel(474, 796, 652, 68);
            Text(494, 806, 612, 23, kart.ReadyQuickBoost != KartBoostKind.None ? "喷射窗口已开启：松开再轻点 W" :
                kart.NitroCharges > 0 ? "集气完成！按 Q 释放氮气" : "练习目标：E 漂移集气 → Q 氮气 → W 小喷", 19, kart.NitroCharges > 0 ? accent : Color.white,
                FontStyle.Normal, TextAnchor.MiddleCenter);
            string routeHint = progress == null ? "松开 E 恢复抓地  /  左侧直道有可绕行跳台，可练习空喷和落地喷"
                : progress.IsWrongWay ? "正在逆行！请按赛道箭头方向行驶"
                : progress.HasNotice ? progress.Notice
                : "按顺序正向通过黄色检查点  /  跳台在左侧，可从右侧绕行";
            Text(494, 836, 612, 18, routeHint, 13, progress != null && progress.IsWrongWay ? yellow : muted, FontStyle.Normal, TextAnchor.MiddleCenter);

            if (race != null && !paused) DrawRaceOverlay();
            if (paused) DrawPause();
            GUI.matrix = previous;
            GUI.color = Color.white;
        }

        private void DrawPause()
        {
            Box(new Rect(0, 0, 1600, 900), new Color(0.01f, 0.025f, 0.04f, 0.76f));
            Panel(560, 260, 480, 400);
            Box(new Rect(560, 260, 480, 4), accent);
            Text(590, 298, 420, 50, "已暂停", 36, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            Text(590, 354, 420, 27, "按 ESC 继续，或选择下方操作", 18, muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            var button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 22, fontStyle = FontStyle.Bold };
            if (sounds != null)
            {
                Text(610, 389, 380, 24, $"音效音量    {sounds.MasterVolume * 100:0}%", 17, muted);
                float volume = GUI.HorizontalSlider(new Rect(610, 426, 380, 20), sounds.MasterVolume, 0f, 1f);
                if (!Mathf.Approximately(volume, sounds.MasterVolume)) sounds.SetMasterVolume(volume);
            }
            if (GUI.Button(new Rect(610, 466, 380, 58), "继续驾驶", button)) TogglePause();
            if (GUI.Button(new Rect(610, 544, 380, 58), race != null && race.State != KartRaceState.Practice ? "重新挑战" : "重置本次练习", button))
            {
                TogglePause();
                if (race == null) kart.ResetToSpawn();
                else if (race.State == KartRaceState.Practice) race.StartPractice();
                else race.StartChallenge(race.TotalLaps);
            }
            if (race != null && GUI.Button(new Rect(610, 614, 380, 32), "返回赛道菜单", button))
            { TogglePause(); race.ReturnToMenu(); }
        }

        public static string FormatTime(float seconds, bool emptyWhenZero = true)
        {
            if (seconds <= 0f) return emptyWhenZero ? "--:--.--" : "00:00.00";
            int hundredths = Mathf.RoundToInt(seconds*100f);
            return $"{hundredths/6000:00}:{hundredths/100%60:00}.{hundredths%100:00}";
        }

        private void BeginChallenge(int laps)
        {
            if (paused) TogglePause();
            race.StartChallenge(laps);
        }

        private void DrawRaceOverlay()
        {
            var button = new GUIStyle(GUI.skin.button) { font=font, fontSize=22, fontStyle=FontStyle.Bold };
            if (race.State == KartRaceState.Ready)
            {
                Panel(490, 250, 620, 430);
                Box(new Rect(490,250,620,4),accent);
                Text(520,276,560,50,progress.Route.DisplayName,36,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);
                Text(520,337,560,28,"单人计时  ·  按顺序通过全部检查点",19,muted,FontStyle.Normal,TextAnchor.MiddleCenter);
                Text(520,385,560,26,"本地最佳圈速  " + FormatTime(race.BestLap),20,accent,FontStyle.Normal,TextAnchor.MiddleCenter);
                if (GUI.Button(new Rect(530,438,255,64),"单圈挑战",button)) BeginChallenge(1);
                if (GUI.Button(new Rect(815,438,255,64),"三圈挑战",button)) BeginChallenge(3);
                if (GUI.Button(new Rect(650,530,300,54),"自由练习",button)) race.StartPractice();
                Text(520,610,560,24,"Enter 开始挑战  /  救援保留集气，计时继续",16,muted,FontStyle.Normal,TextAnchor.MiddleCenter);
            }
            else if (race.State == KartRaceState.Countdown)
            {
                Panel(650,300,300,240);
                Text(670,318,260,36,"准备起跑",24,muted,FontStyle.Normal,TextAnchor.MiddleCenter);
                Text(670,355,260,142,race.CountdownNumber.ToString(),112,yellow,FontStyle.Bold,TextAnchor.MiddleCenter);
                Text(670,502,260,24,"倒计时结束后可加速",16,muted,FontStyle.Normal,TextAnchor.MiddleCenter);
            }
            else if (race.ShowGo)
                Text(650,330,300,130,"GO!",92,accent,FontStyle.Bold,TextAnchor.MiddleCenter);
            else if (race.State == KartRaceState.Finished)
            {
                Panel(470,190,660,570);
                Box(new Rect(470,190,660,4),accent);
                Text(500,208,600,48,"挑战完成",34,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);
                Text(500,262,600,26,$"{race.TotalLaps} 圈挑战  ·  救援 {progress.RecoveryCount} 次",18,muted,FontStyle.Normal,TextAnchor.MiddleCenter);
                Text(500,298,600,60,FormatTime(race.ElapsedSeconds),48,accent,FontStyle.Bold,TextAnchor.MiddleCenter);
                for(int i=0;i<race.LapTimes.Count;i++)
                    Text(540,370+i*30,520,28,$"第 {i+1} 圈                          {FormatTime(race.LapTimes[i])}",21,Color.white);
                Text(500,469,600,25,race.NewBestTotal ? "新的最佳总成绩！" : race.NewBestLap ? "新的最佳圈速！" : "本次成绩已记录",20,yellow,FontStyle.Bold,TextAnchor.MiddleCenter);
                Text(540,506,520,25,"本地最佳圈速          " + FormatTime(race.BestLap),18,muted);
                Text(540,535,520,25,$"本地 {race.TotalLaps} 圈最佳          " + FormatTime(race.BestTotal),18,muted);
                if (GUI.Button(new Rect(510,585,280,58),"再次挑战",button)) BeginChallenge(race.TotalLaps);
                if (GUI.Button(new Rect(810,585,280,58),"返回菜单",button)) race.ReturnToMenu();
                if (GUI.Button(new Rect(650,674,300,48),"自由练习",button)) race.StartPractice();
            }
        }

        private void DrawRouteMap()
        {
            Panel(1264, 326, 300, 314);
            Text(1284, 338, 260, 24, $"路线  /  {progress.Route.Length:0} m", 18, accent, FontStyle.Bold);
            for (int i = 0; i < 128; i++)
            {
                progress.Route.Sample(progress.Route.Length*i/128f, out Vector3 a, out Vector3 af);
                progress.Route.Sample(progress.Route.Length*(i+1)/128f, out Vector3 b, out Vector3 bf);
                MapLine(MapPoint(a), MapPoint(b), 3f, new Color(0.36f, 0.52f, 0.58f));
            }
            for (int i = 0; i < progress.Route.CheckpointCount; i++)
            {
                progress.Route.Sample(progress.Route.CheckpointDistance(i), out Vector3 p, out Vector3 f);
                Vector2 dot = MapPoint(p);
                Color color = i == progress.NextCheckpointIndex ? yellow : i < progress.NextCheckpointIndex ? accent : muted;
                float size = i == progress.NextCheckpointIndex ? 8f : 4f;
                Box(new Rect(dot.x-size/2f,dot.y-size/2f,size,size),color);
            }
            Vector2 player = MapPoint(kart.transform.position);
            Vector2 heading = new Vector2(kart.transform.forward.x,-kart.transform.forward.z).normalized;
            Vector2 side = new Vector2(-heading.y,heading.x);
            MapLine(player+heading*7f,player-heading*4f+side*4f,2f,Color.white);
            MapLine(player+heading*7f,player-heading*4f-side*4f,2f,Color.white);
            MapLine(player-heading*4f+side*4f,player-heading*4f-side*4f,2f,Color.white);
            Text(1284, 592, 260, 22, $"目标检查点  {progress.NextCheckpointIndex+1} / {progress.Route.CheckpointCount}", 17, yellow);
            Text(1284, 616, 260, 18, race != null && race.State != KartRaceState.Practice
                ? $"已完成 {race.LapTimes.Count} / {race.TotalLaps} 圈" : $"已完整通过 {progress.CompletedCircuits} 次", 13, muted);
        }

        private Vector2 MapPoint(Vector3 p)
        {
            Vector2 extent = mapMaximum-mapMinimum;
            float scale = Mathf.Min(240f / Mathf.Max(1f,extent.x),210f / Mathf.Max(1f,extent.y));
            Vector2 center = (mapMinimum+mapMaximum)*0.5f;
            return new Vector2(1414f+(p.x-center.x)*scale,476f-(p.z-center.y)*scale);
        }

        private static void MapLine(Vector2 a, Vector2 b, float thickness, Color color)
        {
            Matrix4x4 matrix = GUI.matrix;
            float angle = Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg;
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(a.x,a.y,0f),Quaternion.Euler(0f,0f,angle),Vector3.one);
            Box(new Rect(0f,-thickness/2f,Vector2.Distance(a,b),thickness),color);
            GUI.matrix = matrix;
        }

        private void Panel(float x, float y, float width, float height) => Box(new Rect(x, y, width, height), ink);
        private static void Box(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void Text(float x, float y, float width, float height, string value, int size, Color color,
            FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = anchor;
            label.normal.textColor = color;
            GUI.Label(new Rect(x, y, width, height), value, label);
        }

        private void OnDisable() { Time.timeScale = 1f; AudioListener.pause = false; paused = false; }
        private void OnDestroy() { if (font != null) Destroy(font); }
    }
}
