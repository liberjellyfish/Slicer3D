# SDF Phase 4 Volumetric Lighting Refactor Plan

## 目标

阶段 4 的目标是把体积光从“切面附近的亮雾贴片”重构为基于 SDF 场和参与介质积分的单次散射模型。

最终合成遵循：

```hlsl
finalColor = surfaceLighting * volume.transmittance + volume.scattering;
```

这意味着体积光不再只是简单叠加亮色，而是沿相机射线累积介质散射，并用介质消光削弱后方表面。

## 已完成阶段

### A/B：视线积分与基础合成

- 命中 SDF 表面时，体积积分区间为 `[proxyEnter, hitDistance]`。
- 未命中 SDF 表面时，体积积分区间为 `[proxyEnter, proxyExit]`，并受 `Volume Light Max Distance` 限制。
- 每步使用 Frostbite 风格改进积分：`S * (1 - exp(-sigmaE * ds)) / sigmaE`。
- 表面结果使用 `surface * transmittance + scattering` 合成。

### C：介质模型升级

`GetParticipatingMedia()` 现在输出真正的 `sigmaS / sigmaE / densityDebug`，介质由多项组成：

- `Volume Base Fog Density`：整个 proxy 内的弱基础空间雾。默认很低，避免蓝色体积盒。
- `Volume Height Fog Strength`：object-space 高度雾，让低处介质更浓。
- `Volume Light Shape Depth`：SDF 表面附近的指数衰减介质层。
- `Volume Cut Fog Boost`：切面附近额外密度增强，但不再主导所有体积效果。
- `Volume Noise Contrast`：控制 value noise 的对比度。
- `Volume Light Surface Fade Distance`：proxy 边界淡出，用来隐藏硬盒轮廓。

### D：光源模型增强

体积采样现在支持两类入射光：

- URP Directional Light：仍作为默认主光，表面光照也继续使用它。
- Shader 验证点光：只参与体积散射，用 `intensity / distance^2` 衰减，接近参考 shader 中高能点光的视觉来源。

新增点光参数：

- `Volume Point Light Enabled`
- `Volume Point Light Position WS`
- `Volume Point Light Color`
- `Volume Point Light Intensity`
- `Volume Point Light Range`

### E：体积阴影质量

体积阴影拆成两条路径：

- 几何遮挡：沿光源方向做 SDF march，命中几何则遮挡。
- 介质透射：沿光源方向采样 `sigmaE`，累积 `exp(-sigmaE * ds)`。

最终体积光可见性为：

```hlsl
visibility = geometryVisibility * mediaTransmittance;
visibility = lerp(1.0, visibility, VolumeLightShadowStrength);
```

新增参数：

- `Volume Shadow Samples`
- `Volume Shadow Max Distance`

新增 debug：

- `VolumeGeometryShadow`
- `VolumeMediaTransmittance`
- `VolumeShadow` 仍表示二者合成后的可见性。

### F：曝光与色调

最终非 debug 视图会经过保守显示映射：

- `Volume Exposure` 控制最终输出曝光。
- `Volume Color Tint` 只给体积散射染色，不直接改变表面材质。
- Debug view 不走曝光和 tone mapping，保持诊断值准确。

### G：Unity 验证工具

`SdfValidationEnvironmentController` 已扩展：

- `FinalLighting`
- `Surface`
- `VolumeDensity`
- `VolumeTransmittance`
- `VolumeShadow`
- `VolumeComposite`
- `VolumeGeometryShadow`
- `VolumeMediaTransmittance`

运行时热键：

- `F1`：FinalLighting，最终光照。
- `F2`：VolumeDensity，介质密度。
- `F3`：VolumeTransmittance，视线透射。
- `F4`：VolumeShadow，几何遮挡和介质透射合成。
- `F5`：VolumeComposite，综合体积 debug。
- `F6`：VolumeGeometryShadow，只看 SDF 几何遮挡。
- `F7`：VolumeMediaTransmittance，只看介质透射。

验证控制器还会在体积验证模式中：

- 应用 `CinematicWarm` 体积 preset。
- 禁用明亮天空盒，切换为暗色 Camera background。
- 驱动一个可旋转的虚拟点光，提升体积光束可读性。

### H：渲染架构整理

切割会克隆 `SdfPhase1Driver`。如果每个子物体都同时渲染表面和 no-hit 背景体积，就会产生多个 proxy volume box 叠加，表现为体积光束中出现莫名棱角和盒边。

当前解决方案升级为“表面 pass + 共享体积 pass”：

- 每个切片子物体只负责自己的 SDF 表面，运行在 `VolumeContributionMode.SurfaceOnly`。
- 场景里单独创建一个 `SdfSharedVolumeProxy`，运行在 `VolumeContributionMode.VolumeOnly`，只负责体积雾和体积光。
- `SdfSharedVolumeProxy` 每帧收集所有普通 `SdfPhase1Driver`，把每个物体的 transform、基础形状参数和 cut plane range 打包到 `_SdfSceneShapes` / `_SdfSceneCutPlanes`。
- shader 通过 `_UseSceneSdf` 区分本地 SDF 和场景 SDF：表面物体使用本地 `Map()`，共享体积盒使用聚合后的 `SceneMap()`。
- 共享体积盒可以自动包围所有切片，也可以关闭 `Auto Fit Bounds` 后手动调节 `Manual Center` / `Manual Size`。

这个结构让体积雾只被积分一次，同时体积阴影仍然读取所有切片的 SDF 数据。

## SdfSandbox 默认状态

`Assets/Scenes/SdfSandbox.unity` 已挂好需要的脚本和参数：

- `SdfProxyCube` 上的 `SdfPhase1Driver` 开启体积光，并使用暖色点光参数。
- `SdfSlicerSystem` 上的 `SdfValidationEnvironmentController` 默认 `Validation Mode = FinalLighting`。
- `Use Dark Validation Sky` 已开启，用来避免原天空盒把体积盒染成蓝色。
- `Volume Preset = CinematicWarm`，这是当前推荐的观察 preset。
- `Show Backdrop In Validation Modes` 已关闭，验证背板默认不再遮挡体积光观察。
- 场景中已有 `SdfSharedVolumeProxy`；如果引用丢失，进入 Play 或执行 `Apply Current Mode` 时会自动重新绑定/创建。

## Unity 验证顺序

1. 打开 `Assets/Scenes/SdfSandbox.unity`。
2. 进入 Play，先看 `F1` 最终效果：背景应变暗，蓝色 proxy 盒感应明显减弱，体积光应偏暖色。
3. 按 `F2`：密度应在 proxy 内连续存在，但边界淡出；SDF 表面和切面附近更亮。
4. 按 `F3`：密度更高的区域应更暗，表示相机到表面的透射降低。
5. 按 `F6`：能看到 SDF 几何对光源路径的遮挡。
6. 按 `F7`：能看到介质本身造成的透射衰减。
7. 按 `F4`：应同时包含 F6 和 F7 的效果，是体积光最终使用的阴影可见性。
8. 按 `F5`：综合查看密度、透射和阴影是否集中在合理区域。
9. 按 `F1` 回到最终画面，观察光源旋转时雾中亮带和阴影是否连续移动。
10. 切割一次或多次，确认不会出现多个 proxy volume box 叠加出的硬棱角。

## 调参建议

- 仍看到明显盒子边：增大 `Volume Light Surface Fade Distance` 到 `0.28-0.35`，或降低 `Volume Base Fog Density`。
- 体积光不明显：优先增大 `Volume Point Light Intensity` 或 `Volume Exposure`，不要先盲目提高密度。
- 画面太白：降低 `Volume Exposure` 或 `Volume Point Light Intensity`。
- 画面太卡：把 `Volume Light Samples` 调到 `8-10`，把 `Volume Shadow Samples` 调到 `8-12`，或增大 `Volume Light Max Step Length` 到 `0.12`。
- 想进一步去掉天空影响：保持 `Use Dark Validation Sky` 开启，并使用 `Validation Camera Background Color` 控制背景色。

## 已知风险

- 阶段 3 的 tile culling 尚未执行，体积阴影会显著增加 fragment 成本。
- 当前体积仍在 proxy mesh pass 内渲染，严格的独立 full-screen volume pass 还没有拆出来。
- Unity 生成的 `Assembly-CSharp.csproj` 仍引用了一批已不存在的旧脚本，因此 `dotnet build` 目前不能作为完整项目验证依据。
