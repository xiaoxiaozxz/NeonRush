# NeonRush 项目目录约定

Unity 工程位于 `NeonRush/`。自有内容统一放入 `NeonRush/Assets/_Project/`。

```text
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Runtime/
│   │   │   ├── Core/                   # 纯 C# 规则，无 UnityEngine/UnityEditor 引用
│   │   │   ├── Gameplay/               # 车辆、比赛、输入、镜头、音效与视觉组件
│   │   │   │   └── Configuration/      # ScriptableObject 类型的 C# 定义
│   │   │   └── UI/                     # HUD、菜单及其缓存实现
│   │   ├── Editor/                     # 场景搭建、资源接入、性能测量工具
│   │   └── Tests/
│   │       ├── EditMode/               # 纯逻辑、资源引用和编辑器验证
│   │       └── PlayMode/               # 物理、输入、比赛流程与菜单回归
│   ├── Data/                           # 已实例化的车辆/路线/音频配置等资产
│   │   ├── Input/                      # Input Actions
│   │   └── Rendering/                  # URP Renderer、管线及默认后处理配置
│   ├── Prefabs/
│   ├── Scenes/
│   ├── Art/
│   │   ├── Materials/                  # 灰盒及共用材质
│   │   ├── Meshes/                     # 灰盒及共用网格
│   │   ├── NeonHarbor/                 # 黄色跑车及夜景样板美术
│   │   └── ConnectionProbe/            # Blender 连接验证模型
│   └── Audio/
└── ThirdParty/
    └── UnityTemplate/                  # 保留的 Unity 模板 Readme、示例场景与脚本
```

## 放置原则

- `Core` 独立编译为 `LoongRacing.Core`，启用 `noEngineReferences`。纯数值规则、状态算法和不依赖引擎的类型优先放在这里。当前已有比赛成绩有效性与刷新纪录判断。
- 没有继承 `MonoBehaviour` 不等于纯 C#：`RaceRecordStore` 使用 `PlayerPrefs`，仍属于 Gameplay。物理、时间、输入、场景及音频组件也留在 Gameplay。
- ScriptableObject 的**脚本定义**在 `Scripts/Runtime/Gameplay/Configuration`；其**配置资产实例**在 `Data`。和具体美术资源配套的材质、反射探针、后处理资产保留在对应 Art 子目录。
- Runtime 主程序集保留 `LoongRacing.Runtime` 名称和 `LoongRacing` 命名空间；Core 是其单向依赖。Editor 与 EditMode 测试只在编辑器编译，不进入正常玩家构建。
- 新增不需要实际帧推进和物理的测试优先使用 EditMode；碰撞、漂移、腾空喷射、输入和比赛流程使用 PlayMode。
- 通过 UPM 安装的插件由 `Packages/manifest.json` 管理，不复制到 Assets。将来手动导入的 Asset Store 插件放在 `ThirdParty/<供应商或插件名>/`，遵循该插件对固定目录的要求。
- 新文件及文件夹都需要提交 `.meta`。移动现有资源时保留原 `.meta` 和 GUID；不要通过复制后删除来重新生成身份。
- 修改资源路径时同步检查 Editor 工具、测试、Build Settings、音频/Blender 生成脚本和文档。历史 Captures 报告中的旧路径记录保留原始测量上下文。

## 验证入口

Unity 中选择 `Window > General > Test Runner`，分别运行 EditMode 和 PlayMode。目录迁移的资源验证位于 `ProjectAssetsTests`，纯成绩规则测试位于 `RaceTimeRulesTests`。

`Tools/verify_project_structure.py` 可核对迁移前后 GUID、`.meta` 与目录约定，并输出到 `NeonRush/Captures/ProjectStructure/`。原始测试结果也保存在该目录。

默认场景为 `Assets/_Project/Scenes/NeonHarbor.unity`。仍可用 `NeonRush > Open Neon Harbor` 打开夜景样板，用另两个 Open 菜单打开原训练场和灰盒赛道。
