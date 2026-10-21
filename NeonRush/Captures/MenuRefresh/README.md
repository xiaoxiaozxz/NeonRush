# 左侧菜单与 HUD 分离验证

- `approved-target.png`：用户确认的效果图，由内置 image_gen 基于实际驾驶截图生成，仅用作设计参考。
- `main-menu.png`：Unity 实际主菜单，左侧选项与全屏渐变半透明遮罩。
- `pause-menu.png`：实际暂停界面，驾驶 HUD 已隐藏。
- `audio-settings.png`：实际音量/静音设置页。
- `results-menu.png`：真实物理单圈完成后的结算界面，约 32.34 秒、0 次救援。
- `driving-hud-restored.png`：离开菜单进入练习后，驾驶 HUD 恢复。
- `main-menu-720p.png`、`main-menu-ultrawide.png`：1280×720 和 2560×1080 分辨率检查；1080p 截图为便于预览缩至 1600×900。

确认图方向：清除全部驾驶 HUD，使用覆盖全屏的深色半透明遮罩和左深右浅渐变；左侧白色 NeonRush 标题与纵向中文选项，青色高亮；中右区域保留黄色赛车及霓虹道路，右下角仅保留所选模式的菜单信息。

菜单支持鼠标与方向键/Enter，设置支持拖动音量滑杆、左右键调整和静音。Esc 从设置返回上一层，不会直接解除暂停；主菜单不再叠加暂停界面。预览与测试使用临时赛道记录，未覆盖玩家成绩，音量检查结束恢复原值。

完整 Play Mode 回归 34 / 34 通过，用时约 128.76 秒。原始测试结果及实际输入检查见 `validation-results.json`。验证后恢复原 Game View 分辨率和 Enter Play Mode 设置，并移除验证用临时分辨率。
