using UnityEngine;

namespace LoongRacing
{
    public sealed partial class PracticeHud
    {
        private enum MenuContext { None, Main, Pause, Results }
        private static readonly string[] MainOptions = { "单圈挑战", "三圈挑战", "自由练习", "设置", "退出游戏" };
        private static readonly string[] ResultOptions = { "再次挑战", "自由练习", "返回主菜单", "设置", "退出游戏" };
        private MenuContext menuContext;
        private int menuSelection, settingsReturnSelection;
        private bool settingsOpen;
        private Vector2 lastMenuMouse = new Vector2(-1000f, -1000f);
        private Texture2D menuGradient, selectionGradient;

        public bool IsMenuVisible => paused || settingsOpen || race != null &&
            (race.State == KartRaceState.Ready || race.State == KartRaceState.Finished);
        public bool IsSettingsOpen => settingsOpen;

        private MenuContext CurrentMenu => paused ? MenuContext.Pause : race == null ? MenuContext.None :
            race.State == KartRaceState.Ready ? MenuContext.Main :
            race.State == KartRaceState.Finished ? MenuContext.Results : MenuContext.None;

        private void SynchronizeMenu()
        {
            if (menuContext == CurrentMenu) return;
            menuContext = CurrentMenu;
            menuSelection = 0;
            settingsOpen = false;
            GUIUtility.hotControl = 0;
        }

        private int MenuOptionCount => settingsOpen ? 3 : menuContext == MenuContext.Pause && race == null ? 4 : 5;

        public void NavigateMenu(int direction)
        {
            SynchronizeMenu();
            if (!IsMenuVisible) return;
            menuSelection = (menuSelection + direction % MenuOptionCount + MenuOptionCount) % MenuOptionCount;
        }

        public void ConfirmMenuSelection()
        {
            SynchronizeMenu();
            if (!IsMenuVisible) return;
            if (settingsOpen)
            {
                if (menuSelection == 1 && sounds != null) sounds.ToggleMute();
                else if (menuSelection == 2) CloseSettings();
                return;
            }
            if (menuContext == MenuContext.Main)
            {
                if (menuSelection < 2) BeginChallenge(menuSelection == 0 ? 1 : 3);
                else if (menuSelection == 2) StartMenuPractice();
                else if (menuSelection == 3) OpenSettings();
                else QuitGame();
            }
            else if (menuContext == MenuContext.Results)
            {
                if (menuSelection == 0) BeginChallenge(race.TotalLaps);
                else if (menuSelection == 1) StartMenuPractice();
                else if (menuSelection == 2) ReturnToMainMenu();
                else if (menuSelection == 3) OpenSettings();
                else QuitGame();
            }
            else if (menuContext == MenuContext.Pause)
            {
                if (menuSelection == 0) TogglePause();
                else if (menuSelection == 1)
                {
                    if (race != null && race.State != KartRaceState.Practice) BeginChallenge(race.TotalLaps);
                    else StartMenuPractice();
                }
                else if (race != null && menuSelection == 2) ReturnToMainMenu();
                else if (menuSelection == MenuOptionCount - 2) OpenSettings();
                else QuitGame();
            }
        }

        public void BackFromMenu()
        {
            SynchronizeMenu();
            if (settingsOpen) CloseSettings();
            else if (menuContext == MenuContext.Results) ReturnToMainMenu();
            else if (menuContext != MenuContext.Main) TogglePause();
        }

        private void OpenSettings()
        {
            settingsReturnSelection = menuSelection;
            menuSelection = 0;
            settingsOpen = true;
        }

        private void CloseSettings()
        {
            settingsOpen = false;
            menuSelection = settingsReturnSelection;
            GUIUtility.hotControl = 0;
        }

        private void RestoreMenuTime()
        {
            paused = false;
            settingsOpen = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        private void StartMenuPractice()
        {
            RestoreMenuTime();
            if (race != null) race.StartPractice();
            else kart.ResetToSpawn();
            SynchronizeMenu();
        }

        private void ReturnToMainMenu()
        {
            RestoreMenuTime();
            race.ReturnToMenu();
            SynchronizeMenu();
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void DrawMenuBackdrop()
        {
            GUI.matrix = Matrix4x4.identity;
            Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.005f, 0.012f, 0.03f, 0.38f));
            if (menuGradient == null)
            {
                menuGradient = MakeMenuGradient(false);
                selectionGradient = MakeMenuGradient(true);
            }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), menuGradient);
        }

        private static Texture2D MakeMenuGradient(bool selection)
        {
            var texture = new Texture2D(128, 1, TextureFormat.RGBA32, false)
            { name = selection ? "Menu Selection" : "Menu Shade", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[128];
            for (int x = 0; x < pixels.Length; x++)
            {
                float t = x / 127f;
                pixels[x] = selection ? new Color(0f, 0.7f, 0.8f, Mathf.Lerp(0.24f, 0.03f, t)) :
                    new Color(0.002f, 0.01f, 0.022f, Mathf.Lerp(0.7f, 0.02f, Mathf.SmoothStep(0f, 1f, t)));
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void DrawMenu()
        {
            Text(64, 65, 600, 92, "NEONRUSH", 66, Color.white, FontStyle.BoldAndItalic);
            Text(70, 152, 400, 44, "霓虹疾驰", 28, muted, FontStyle.Italic);
            string section = settingsOpen ? "声音设置" : menuContext == MenuContext.Main ? "主菜单" :
                menuContext == MenuContext.Pause ? "已暂停" : "挑战完成";
            Text(64, 235, 340, 40, section, 20, muted);
            Box(new Rect(150, 257, 275, 1), new Color(0.37f, 0.52f, 0.6f, 0.3f));

            bool mouseMoved = Event.current.type == EventType.Repaint &&
                (Event.current.mousePosition - lastMenuMouse).sqrMagnitude > 0.5f;
            if (settingsOpen)
            {
                DrawMenuRow(0, 300, sounds == null ? "音效不可用" : $"音效音量  {sounds.MasterVolume * 100:0}%", mouseMoved);
                DrawVolumeSlider();
                DrawMenuRow(1, 420, sounds != null && sounds.MasterVolume < 0.001f ? "静音：开" : "静音：关", mouseMoved);
                DrawMenuRow(2, 498, "返回", mouseMoved);
            }
            else
                for (int i = 0; i < MenuOptionCount; i++) DrawMenuRow(i, 300 + i * 78, MenuOptionLabel(i), mouseMoved);
            if (Event.current.type == EventType.Repaint) lastMenuMouse = Event.current.mousePosition;

            string footer = settingsOpen ? "↑ ↓ 选择    ← → 音量    Esc 返回" :
                menuContext == MenuContext.Pause ? "↑ ↓ 选择    Enter 确认    Esc 继续" : "↑ ↓ 选择    Enter 确认";
            Text(68, 804, 580, 40, footer, 17, muted);
            if (settingsOpen) DrawSettingsDetail();
            else if (menuContext == MenuContext.Results) DrawResultsDetail();
            else DrawMenuDetail();
        }

        private string MenuOptionLabel(int index)
        {
            if (menuContext == MenuContext.Main) return MainOptions[index];
            if (menuContext == MenuContext.Results) return ResultOptions[index];
            if (index == 0) return "继续驾驶";
            if (index == 1) return race != null && race.State != KartRaceState.Practice ? "重新挑战" : "重置本次练习";
            if (race != null && index == 2) return "返回主菜单";
            return index == MenuOptionCount - 2 ? "设置" : "退出游戏";
        }

        private void DrawMenuRow(int index, float y, string caption, bool mouseMoved)
        {
            var rect = new Rect(64, y, 360, 66);
            if (mouseMoved && rect.Contains(Event.current.mousePosition)) menuSelection = index;
            bool selected = menuSelection == index;
            if (selected)
            {
                GUI.DrawTexture(rect, selectionGradient);
                Box(new Rect(rect.x, rect.y, 4, rect.height), accent);
                Text(378, y + 6, 32, 52, "›", 38, accent, FontStyle.Normal, TextAnchor.MiddleCenter);
            }
            Text(96, y + 8, 276, 50, caption, 27, selected ? Color.white : new Color(0.86f, 0.9f, 0.93f), FontStyle.Bold);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) { menuSelection = index; ConfirmMenuSelection(); }
        }

        private void DrawVolumeSlider()
        {
            if (sounds == null) return;
            var hit = new Rect(96, 371, 290, 30);
            int id = GUIUtility.GetControlID(7318, FocusType.Passive, hit);
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition))
            { GUIUtility.hotControl = id; menuSelection = 0; }
            if (GUIUtility.hotControl == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            { sounds.SetMasterVolume(Mathf.Clamp01((e.mousePosition.x - hit.x) / hit.width)); e.Use(); }
            if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
            Box(new Rect(hit.x, hit.y + 12, hit.width, 4), new Color(0.22f, 0.32f, 0.37f, 0.8f));
            Box(new Rect(hit.x, hit.y + 12, hit.width * sounds.MasterVolume, 4), accent);
            Box(new Rect(hit.x + hit.width * sounds.MasterVolume - 4, hit.y + 7, 8, 14), Color.white);
        }

        private void DrawMenuDetail()
        {
            const float x = 1230;
            Box(new Rect(x - 24, 651, 2, 169), accent);
            Text(x, 645, 306, 32, progress == null ? "环形训练场" : progress.Route.DisplayName, 18, accent);
            string title = menuContext == MenuContext.Pause ? "已暂停" : MainOptions[menuSelection];
            Text(x, 680, 306, 50, title, 32, Color.white, FontStyle.Bold);
            string detail = menuContext == MenuContext.Pause ? "当前驾驶已暂停" : menuSelection == 2 ? "完整路线 / 自由练习" :
                menuSelection == 3 ? "调整音效音量与静音" : menuSelection == 4 ? "退出 NeonRush" :
                $"{progress.Route.Length:0} 米 / 单人计时";
            Text(x, 734, 306, 32, detail, 19, muted);
            Box(new Rect(x, 779, 274, 1), new Color(0.37f, 0.52f, 0.6f, 0.35f));
            string foot = menuContext == MenuContext.Pause ? "Esc 返回当前驾驶" : menuSelection < 2 ?
                "本地最佳圈速  " + FormatTime(race.BestLap) : menuSelection == 2 ? "练习不写入挑战成绩" : "Enter 确认选择";
            Text(x, 790, 306, 34, foot, 16, muted);
        }

        private void DrawSettingsDetail()
        {
            Box(new Rect(1206, 651, 2, 169), accent);
            Text(1230, 645, 306, 32, "设置", 18, accent);
            Text(1230, 680, 306, 50, "声音", 32, Color.white, FontStyle.Bold);
            Text(1230, 734, 306, 32, "拖动滑杆或按 ← → 调整", 18, muted);
            Text(1230, 790, 306, 34, "Esc 返回上一级菜单", 16, muted);
        }

        private void DrawResultsDetail()
        {
            const float x = 1080;
            Box(new Rect(x - 24, 340, 2, 480), accent);
            Text(x, 332, 460, 36, progress.Route.DisplayName, 20, accent);
            Text(x, 377, 460, 54, "挑战完成", 36, Color.white, FontStyle.Bold);
            Text(x, 438, 460, 65, FormatTime(race.ElapsedSeconds), 48, Color.white, FontStyle.Bold);
            Text(x, 508, 460, 32, $"{race.TotalLaps} 圈挑战  /  救援 {progress.RecoveryCount} 次", 18, muted);
            for (int i = 0; i < race.LapTimes.Count; i++)
                Text(x, 550 + i * 34, 460, 32, $"第 {i + 1} 圈        {FormatTime(race.LapTimes[i])}", 21, Color.white);
            Box(new Rect(x, 670, 420, 1), new Color(0.37f, 0.52f, 0.6f, 0.35f));
            Text(x, 690, 460, 34, race.NewBestTotal ? "新的最佳总成绩！" : race.NewBestLap ? "新的最佳圈速！" : "本次成绩已记录", 22, accent);
            Text(x, 738, 460, 32, "最佳圈速    " + FormatTime(race.BestLap), 18, muted);
            Text(x, 778, 460, 32, "最佳总用时  " + FormatTime(race.BestTotal), 18, muted);
        }
    }
}
