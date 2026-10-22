# Assets 目录迁移验证 — 2026-10-22

目标结构与后续放置规则见仓库根目录的 [PROJECT_STRUCTURE.md](../../../PROJECT_STRUCTURE.md)。

## 迁移范围

- `Assets/LoongRacing` 自有内容迁入 `Assets/_Project`。
- Runtime 脚本分为 Core、Gameplay、UI；配置类型在 Gameplay/Configuration，配置资产实例在 Data。
- Editor、EditMode、PlayMode 脚本统一归入 Scripts；材质和网格统一归入 Art。
- URP 设置移入 Data/Rendering，Input Actions 移入 Data/Input。
- Unity 原始模板示例移入 ThirdParty/UnityTemplate。UPM 插件保持 Packages 管理方式。
- 保留原有命名空间和 Runtime/Editor/PlayMode 程序集名称；新增纯 C# Core 与仅编辑器使用的 EditMode 测试程序集。
- 场景构建路径、Build Settings、模板路径、音频与 Blender 生成路径、文档和 Blender 源文件外部贴图引用均已更新。

## 结果

| 检查 | 结果 |
| --- | --- |
| 既有资源及文件夹 GUID | 161 / 161 保留，无重复、无丢失 |
| 既有 `.meta` 文件 | 全部 SHA-256 与迁移前一致 |
| 原有资产内容 | 125 个文件逐字节不变；其余 10 个仅为 C# 或 asmdef 修改 |
| Unity 重新导入与编译 | 通过 |
| EditMode | 23 / 23 通过，约 1.77 秒 |
| PlayMode | 34 / 34 通过，约 131.93 秒 |
| 自有场景及 Prefab 缺失脚本检查 | 通过 |
| Build Settings / 音频配置引用 | 通过 |
| Blender 源文件两张外部贴图 | 已修复相对路径，文件均存在 |

首次 EditMode 运行发现新验证代码在批处理初始无活动场景时不能直接恢复空的 SceneManagerSetup；已修正清理分支，最终整套重跑通过。该问题属于新测试的环境清理逻辑。

PlayMode 保留原有完整范围，包括三圈实际物理驾驶、跳台空喷和落地喷、菜单与暂停、撞墙保留氮气、救援保留集气与库存。此次未改动物理参数及驾驶流程。

## 记录

- `before-assets.json`：迁移前路径、GUID 与 `.meta` SHA-256。
- `migration-verification.json`：新旧路径映射和静态验证结论。
- `asset-content-verification.json`：原有资产文件内容对比。
- `editmode-results.xml`、`playmode-results.xml`：实际 Unity Test Framework 原始结果。
- `blender-textures.json`：源文件贴图链接验证。

使用 `python Tools/verify_project_structure.py` 可再次执行静态检查。Unity Test Runner 中可分别运行 EditMode 和 PlayMode。旧版历史 Captures 中的路径保持当时记录，不作为当前资源加载入口。

## 编辑器空场景恢复跟进

用户重新打开 Unity 后看到空场景。三个自有场景文件均完好，编辑器最近场景记录为空；批处理验证结束后未恢复工作场景是本次遗漏。已在用户打开的编辑器中恢复 NeonHarbor，并目视确认赛车和夜景赛道。

资源检查改为使用 OpenPreviewScene / ClosePreviewScene，避免替换或重新创建用户的工作场景；新增 SceneInspectionPreservesTheOpenEditorScenes，检查前后场景路径、加载/激活状态、活动场景句柄及脏标记保持一致。在当前编辑器 Test Runner 运行最新全部 EditMode，24 / 24 通过；测试后 NeonHarbor 仍为加载且激活状态。上表和原始 XML 的 23 项是迁移阶段批处理记录；本轮 UI 验证摘要见 scene-recovery-verification.json。
