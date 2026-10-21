# 黄色跑车与霓虹高架样板

2026-10-21。本轮完成一辆原创 Loong Vela 双门跑车与约 120 米夜景美术样板；完整 903 米路线仍可练习和比赛。隧道、多层天桥及全赛道精修留待下一轮。

## 体验

在 Unity 菜单选择 `NeonRush > Open Neon Harbor`，点击 Play，再点击 Game 窗口。选择单圈、三圈或自由练习。推荐方向键驾驶，E 漂移、Q 氮气、轻点 W 小喷/空喷/落地喷，R 救援，Esc 暂停。

原 `HarborCircuit.unity`、`TrainingGround.unity` 和 `BlockKart.prefab` 保留。新夜景场景使用独立路线资源和成绩标识。

## 交付文件

- `LoongVela_NeonHarbor.blend`：可编辑源模型，工作场景 `Loong Neon Studio`，保留 Blender 原场景。
- `../../NeonRush/Assets/LoongRacing/Art/NeonHarbor/Models/`：赛车及六个环境模块的 FBX。
- `../../NeonRush/Assets/LoongRacing/Art/NeonHarbor/Textures/`：2K 车漆、湿路颜色/法线/金属平滑度贴图及原创广告图。
- `../../NeonRush/Assets/LoongRacing/Art/NeonHarbor/Materials/`：URP 材质、天空 Shader 和后处理配置。
- `../../NeonRush/Assets/LoongRacing/Prefabs/VelaRacer.prefab`：驾驶系统可用的跑车 Prefab。
- `../../NeonRush/Assets/LoongRacing/Scenes/NeonHarbor.unity`：独立夜景场景。
- `../../NeonRush/Captures/NeonHarbor/`：三视图、车尾渲染、实际漂移、驾驶与样板总览、验证及性能 JSON。

## 模型和接入

Blender 使用 X 向右、Y 向前、Z 向上，车长约 4.8 米；Unity 模型组合为车头 +Z、车长 3.2 米。车身宽约 1.24 米，后视镜使整体宽度更大。源模型评估 39,188 三角面，Unity 可见模型 38,932 三角面，车身与四轮合计 5 个渲染器。外部小细节使用独立几何及程序材质；内饰简化、车窗深色，本轮未制作可用驾驶舱。

`KartVisualRig` 提供车身、四轮、前轮转向枢轴、双喷口与后轮接地点，驱动车轮滚动和刹车灯；现有驾驶组件保留车身侧倾。胎痕与喷射读取挂点，原方块车保留固定坐标回退。新 Prefab 使用单个物理碰撞体，保留原车底接地高度与动力参数。镜头高度、距离和注视高度可单独调整。

环境模块包括护栏、门架、桥墩、两种楼体和广告牌。路线距离 125–245 米布置精修样板，其余段采用简化夜景模块。霓虹以自发光为主；静态网格按材质合并，使用三个烘焙反射探针及少量无阴影局部照明。`NeonEnvironment` 提供运行时环境光探针，避免编辑器残留环境光影响驾驶画面。

## 验证

- Play Mode 共 31 项全部通过，包含原 28 项与新车完整三圈、空喷/落地喷和视觉绑定测试。
- 新车真实物理驾驶完成三圈，总用时约 94.90 秒、36 个检查点、0 次救援，最大路线横向偏差约 1.17 米。
- 跳台分别触发空喷与落地喷，最高约 181.4 km/h。撞墙保留氮气、救援保留集气与库存的既有测试通过。
- 截图实测漂移约 130.3 km/h、34.4°、集气约 59.7%；另一次救援前后未满集气均为 81.27588%。
- RTX 2070、1920×1080 Unity 编辑器 Game View、关闭帧率限制及垂直同步，真实单圈排除前 2 秒后采样 3,280 帧：平均 9.25 ms、P95 11.59 ms，GPU 平均 2.74 ms；样板段平均 9.27 ms。达到本次编辑器测试的 60 FPS 目标；尚未进行独立 Windows 构建性能测量。
- 记录使用临时赛道标识隔离，测试结束清理；性能采样结束恢复垂直同步、帧率限制和临时统计设置。

详细原始结果见 `validation-results.json` 和 `performance.json`。

## 再编辑

`../../Tools/Art/build_neon_assets.py` 是 Blender 生成源脚本；已有 `Loong Neon Studio` 时主动拒绝覆盖，常规修改请直接编辑 .blend。Unity `NeonHarborSetup` 也会拒绝覆盖已存在场景；菜单正常打开现有成果。修改源模块后需重新导出 FBX，并重做受影响的静态合并与反射探针烘焙。

`NeonPerformanceValidation.Start()` 为显式调用的编辑器验证工具，使用真实驾驶输入跑一圈并输出性能 JSON；未接入玩家操作流程。
