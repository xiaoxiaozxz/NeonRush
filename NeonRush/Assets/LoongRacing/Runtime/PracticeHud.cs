using UnityEngine;
using UnityEngine.InputSystem;

namespace LoongRacing
{
    public sealed partial class PracticeHud : MonoBehaviour
    {
        [SerializeField] private ArcadeKartController kart;
        private Font font;
        private GUIStyle label;
        private static readonly Unity.Profiling.ProfilerMarker DrawMarker = new Unity.Profiling.ProfilerMarker("NeonRush.HudDraw");
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
            EnsureMapCache();
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 24);
        }

        private void Update()
        {
            SynchronizeMenu();
            var keyboard = Keyboard.current;
            if (keyboard == null || !Application.isFocused) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { BackFromMenu(); return; }
            if (keyboard.mKey.wasPressedThisFrame && sounds != null) sounds.ToggleMute();
            if (IsMenuVisible)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame) NavigateMenu(-1);
                if (keyboard.downArrowKey.wasPressedThisFrame) NavigateMenu(1);
                if (settingsOpen && menuSelection == 0 && sounds != null)
                {
                    if (keyboard.leftArrowKey.wasPressedThisFrame) sounds.SetMasterVolume(sounds.MasterVolume - 0.05f);
                    if (keyboard.rightArrowKey.wasPressedThisFrame) sounds.SetMasterVolume(sounds.MasterVolume + 0.05f);
                }
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) ConfirmMenuSelection();
                return;
            }
            if (race != null && keyboard.f5Key.wasPressedThisFrame) BeginChallenge(race.TotalLaps);
        }

        public void TogglePause()
        {
            if (!paused && race != null && (race.State == KartRaceState.Ready || race.State == KartRaceState.Finished)) return;
            paused = !paused;
            settingsOpen = false;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (sounds != null) sounds.PlayPause(paused);
            if (kart != null) { kart.SetDriveInput(0f, 0f); kart.SetDriftInput(false); kart.CancelBufferedActions(); }
            SynchronizeMenu();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (isActiveAndEnabled && !focused && !IsMenuVisible && Application.isPlaying) TogglePause();
        }

        private void OnGUI()
        {
            using (DrawMarker.Auto()) DrawHud();
        }

        private void DrawHud()
        {
            if (kart == null) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { font = font, richText = false, wordWrap = false, padding = new RectOffset() };
            SynchronizeMenu();
            // The driving HUD has no interactive controls. Layout/mouse events need no redraw.
            if (!IsMenuVisible && Event.current.type != EventType.Repaint) return;
            WarmHudFont();
            Matrix4x4 previous = GUI.matrix;
            if (IsMenuVisible) DrawMenuBackdrop();
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600f * scale) / 2f,
                (Screen.height - 900f * scale) / 2f, 0f), Quaternion.identity, Vector3.one * scale);

            // Menus and driving HUD are mutually exclusive, including pause and results.
            if (IsMenuVisible)
            {
                DrawMenu();
                GUI.matrix = previous;
                GUI.color = Color.white;
                return;
            }
            RefreshHudText();

            Panel(36, 32, 328, 104);
            Box(new Rect(36, 32, 5, 104), accent);
            Text(58, 40, 282, 40, "NEONRUSH / 霓虹疾驰", 22, Color.white, FontStyle.Bold);
            Text(58, 84, 282, 28, modeCaption, 18, muted);

            bool challenge = race != null && race.State != KartRaceState.Practice;
            Panel(642, 32, 316, challenge ? 104 : 74);
            Text(642, 42, 316, 20, clockCaption, 14, muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Text(642, 64, 316, 30, timeCaption, 27, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (challenge) Text(642, 102, 316, 20, lapCaption, 14, accent, FontStyle.Normal, TextAnchor.MiddleCenter);

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
            Text(56, 237, 286, 24, chargeCaption, 17, Color.white);
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
            Text(56, 787, 290, 25, topSpeedCaption, 20, Color.white);
            Text(56, 822, 290, 22, recordCaption, 16, muted);

            Panel(1224, 670, 340, 194);
            Text(1246, 681, 292, 24, kart.IsReversing ? "REVERSE  /  倒车" : "SPEED  /  速度", 15, muted);
            Text(1242, 702, 216, 99, speedCaption, 83, Color.white, FontStyle.Bold);
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

            if (race != null) DrawRaceOverlay();
            GUI.matrix = previous;
            GUI.color = Color.white;
        }

        public static string FormatTime(float seconds, bool emptyWhenZero = true)
        {
            if (seconds <= 0f) return emptyWhenZero ? "--:--.--" : "00:00.00";
            int hundredths = Mathf.RoundToInt(seconds*100f);
            return $"{hundredths/6000:00}:{hundredths/100%60:00}.{hundredths%100:00}";
        }

        private void BeginChallenge(int laps)
        {
            RestoreMenuTime();
            race.StartChallenge(laps);
            SynchronizeMenu();
        }

        private void DrawRaceOverlay()
        {
            if (race.State == KartRaceState.Countdown)
            {
                Panel(650,300,300,240);
                Text(670,318,260,36,"准备起跑",24,muted,FontStyle.Normal,TextAnchor.MiddleCenter);
                Text(670,355,260,142,race.CountdownNumber.ToString(),112,yellow,FontStyle.Bold,TextAnchor.MiddleCenter);
                Text(670,502,260,24,"倒计时结束后可加速",16,muted,FontStyle.Normal,TextAnchor.MiddleCenter);
            }
            else if (race.ShowGo)
                Text(650,330,300,130,"GO!",92,accent,FontStyle.Bold,TextAnchor.MiddleCenter);
        }

        private void DrawRouteMap()
        {
            EnsureMapCache();
            Panel(1264, 326, 300, 314);
            Text(1284, 338, 260, 24, mapTitle, 18, accent, FontStyle.Bold);
            GUI.DrawTexture(new Rect(1294, 371, 240, 210), routeTexture);
            for (int i = 0; i < progress.Route.CheckpointCount; i++)
            {
                Vector2 dot = gateMapPoints[i];
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
            Text(1284, 592, 260, 22, gateCaption, 17, yellow);
            Text(1284, 616, 260, 18, circuitCaption, 13, muted);
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

        private void OnDisable() { RestoreMenuTime(); }
        private void OnDestroy()
        {
            if (font != null) Destroy(font);
            if (menuGradient != null) Destroy(menuGradient);
            if (selectionGradient != null) Destroy(selectionGradient);
            if (routeTexture != null) Destroy(routeTexture);
        }
    }
}
