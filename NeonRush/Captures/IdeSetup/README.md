# VS Code C# 导航修复记录 — 2026-10-22

检查时已安装 C#、C# Dev Kit、Unity 扩展及 .NET SDK。问题来自 Unity 未启用实际 VS Code 安装的项目集成，工程未生成解决方案/项目文件；另外，用户级 NuGet.Config 含 509 个空字节，导致语言服务项目还原失败。

已备份并修复本机损坏配置，通过 Unity CodeEditor 选择实际 VS Code，生成 NeonRush.slnx 和 7 个 csproj，重新加载窗口后 C# Dev Kit 完成解决方案加载与还原。实测 F12 从 Gameplay/RaceRecordStore.cs 跳转到 Core/RaceTimeRules.cs。一次性修复脚本已移除。

## 换电脑或重新克隆后的设置

1. 安装 VS Code 的 Microsoft Unity 扩展及其 C#、C# Dev Kit 依赖。
2. Unity 的 Edit > Preferences > External Tools > External Script Editor 选择实际 VS Code 安装；当前项目已包含 com.unity.ide.visualstudio 2.0.26。
3. 点击 Regenerate project files，再从 Unity 双击脚本打开工程；也可在 VS Code 打开包含 Assets、Packages、ProjectSettings 的 NeonRush 工程根目录。
4. 如语言服务仍显示“杂项文件”，执行 Developer: Reload Window，确认状态栏显示 NeonRush.slnx 和对应 LoongRacing 项目上下文。

Ctrl+左键或 F12 跳到定义，Shift+F12 查找引用，Alt+左方向键返回。发生还原错误时检查 C# / C# Dev Kit 输出日志，不需要修改车辆脚本。

生成的 slnx、csproj、.vscode 设置与本机 NuGet 配置均属于本地开发环境，不作为游戏资源提交。generated-projects.txt 是本次生成文件清单，verification.json 是实际验证摘要。
