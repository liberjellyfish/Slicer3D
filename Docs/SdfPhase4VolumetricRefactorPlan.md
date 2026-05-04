# SDF Phase 4 Volumetric Lighting Refactor Plan

## Goal

将阶段 4 从“切面附近的启发式发光雾效”重构为基于 SDF 几何边界和参与介质积分的单次散射体积光。

最终合成遵循：

```hlsl
finalColor = surfaceLighting * volume.transmittanceToSurface + volume.inscatteredLight;
```

这意味着体积光不再是命中点之后简单 `finalColor += scattering`，而是沿相机射线持续积分介质，并让介质消光真实影响表面可见亮度。

## Reference Principles

参考脚本中需要迁移到当前项目的核心结构是：

- `getParticipatingMedia(out sigmaS, out sigmaE, pos)`：空间采样点返回散射系数和消光系数。
- `volumetricShadow(from, to)`：从体积采样点朝光源方向估算透射率。
- `traceScene()`：视线方向同时进行 SDF march 和体积积分。
- Frostbite improved integration：每个步长内用 `Sint = S * (1 - exp(-sigmaE * stepLength)) / sigmaE` 积分，避免强介质下能量表现离谱。
- 体积积分步长需要上限，不能完全依赖 SDF 大步长，否则会漏掉介质贡献。

## Current Problems

当前 `SdfPhase1Raymarch.shader` 的阶段 4 主要问题：

- `EvaluateLocalVolumeMediumMask()` 返回的是切面附近 mask，不是 `sigmaS / sigmaE` 物理量。
- `EvaluateVolumeLighting()` 只采样命中表面前一小段，视觉上像贴在表面前的雾。
- 体积阴影复用了表面阴影概念，缺少采样点到光源路径的介质透射。
- 最终颜色是 `finalColor += volumeTerms.scattering`，没有用体积透射削弱表面项。
- debug 只暴露一个 `VolumeLight` 灰度值，难以定位是介质、阴影、积分还是合成出错。

## Refactor Phases

### Phase 1: Documentation and Boundaries

产出当前文档，明确阶段 4 的数学结构和执行边界。

保留：

- `Map()` / `BaseShapeMap()` / `ApplyCutPlanes()`
- `EstimateNormalOS()`
- `SampleSdfSoftShadow()`
- 表面主光照和切面着色

替换：

- `EvaluateLocalVolumeMediumMask()`
- `EvaluateVolumeShadowVisibility()`
- `EvaluateVolumeLighting()` 的积分方式和输出语义

### Phase 2: Participating Media Model

新增 `GetParticipatingMedia()`，输出：

- `sigmaS`：散射系数
- `sigmaE`：消光系数
- `densityDebug`：仅用于 debug 的介质密度预览

第一版介质由三部分组成：

- 弱基础介质：让体积光能稳定被看见。
- SDF 表面附近介质：用 `Map(p)` 的距离形成靠近几何的柔和介质层。
- 切面增强介质：有切割面时，用主导切割平面和基础形体内部深度增强新鲜切面附近的介质。

这样仍然利用切割数据，但切割只负责调制介质系数，不直接等同于光照。

### Phase 3: View-Ray Integration

新增 `IntegrateVolumeAlongViewRay()`。

输入：

- object-space ray origin / direction
- world-space ray direction
- `tEnter`
- `hitDistance`
- main light direction and color

输出：

- `inscatteredLight`
- `transmittance`
- debug fields

积分方式：

```hlsl
S = lightColor * lightVisibility * sigmaS * phase;
segmentTransmittance = exp(-sigmaE * stepLength);
Sint = S * (1 - segmentTransmittance) / sigmaE;
inscattered += transmittance * Sint;
transmittance *= segmentTransmittance;
```

### Phase 4: Light-Ray Transmittance

新增 `EvaluateVolumeLightTransmittance()`。

从每个体积采样点朝主光方向 raymarch：

- 如果 SDF 几何命中，则认为几何遮挡光源。
- 沿途累积介质消光 `exp(-sigmaE * ds)`。
- 用 `_VolumeLightShadowStrength` 控制体积阴影参与度。

### Phase 5: Driver and Scene Preset

同步 `SdfPhase1Driver` 参数名义和默认值：

- 将旧的 `VolumeLightDensity` 作为主要散射密度。
- 将 `VolumeLightShapeDepth` 用作 SDF 表面介质厚度。
- 将 `VolumeLightPlaneBand` 用作切面增强带宽。
- 将 `VolumeLightRemovedDepth` 用作切面内部深度。
- `VolumeLightSurfaceFadeDistance` 改为视线积分远端/近表面淡出宽度。

`SdfSandbox` 验证预设：

- 开启体积光。
- 保持 debug view 为 `Lighting`，先看最终合成。
- 需要诊断时依次切到 `VolumeDensity`、`VolumeTransmittance`、`VolumeShadow`、`VolumeLight`。
- 默认关闭非 SDF 粒子粉尘，避免干扰数学体积光判断。

## Validation Order

1. 打开 `Assets/Scenes/SdfSandbox.unity`。
2. Play 模式下不切割，确认基础表面光照正常。
3. 切一次，确认两个 piece 的切面仍然正确显示。
4. 在 `SdfPhase1Driver` 中切换 `Debug View = VolumeDensity`，确认体积只在 proxy 内、SDF 表面附近和切面附近增强。
5. 切到 `VolumeTransmittance`，确认密度增加时表面透射变暗。
6. 切到 `VolumeShadow`，确认光源方向上被 SDF 几何遮挡的位置会变暗。
7. 回到 `Debug View = Lighting`，旋转 Game View 相机，确认体积光随视角和主光方向连续变化。
8. 调整 `Volume Light Density / Intensity / Shadow Strength`，确认变化符合物理直觉：密度增加会增强散射，也会降低表面透射。

## Risks

- 目前没有阶段 3 tile culling，体积阴影会显著增加 fragment 成本。
- 当前 pass 只在 proxy mesh 被命中时输出体积结果，背景空射线不会显示独立雾体。
- Unity 工程的 `Assembly-CSharp.csproj` 仍引用旧路径文件，`dotnet build` 不能作为完整验证依据。
