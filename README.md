<div align="center">
  <img width="1920" height="1074" alt="静态低密度体积雾" src="https://github.com/user-attachments/assets/59589282-cd9d-49b4-970a-713522399080" />
  <p>静态低密度体积雾 (Static Low-Density Volumetric Fog)</p>
</div>

<br>

<div align="center">
  <img width="1399" height="754" alt="动态体积光、软阴影与切割" src="https://github.com/user-attachments/assets/b77ebd72-4d11-4e62-a8a1-8dad79556a63" />
  <p>动态体积光 & 软阴影 & 切割 (Dynamic Volumetric Light, Soft Shadows & Slicing)</p>
</div>

## 数学原理

本项目基于 SDF 与 Raymarching 实现，整个渲染管线可以归纳为：从屏幕像素发出射线，遍历几何体，最终进行表面着色与体积光照的积分计算。

### 1. 核心执行链
对于屏幕上的每一个像素，发射一条射线 $\mathbf{r}(t)$，其执行过程为：

```math
\text{Pixel} \xrightarrow{\mathbf{r}(t)=\mathbf{o}+t\mathbf{d}} [t_{\text{enter}}, t_{\text{exit}}] \xrightarrow{D(\mathbf{p})} \text{Surface Hit / Depth End}
```

* $\mathbf{o}$ 为光线起点（相机位置或局部空间起点），$\mathbf{d}$ 为光线方向。
* 光线在包围盒确定的相交区间 $[t_{\text{enter}}, t_{\text{exit}}]$ 内步进。
* 在每次步进中计算空间点 $\mathbf{p}$ 的有向距离 $D(\mathbf{p})$。

### 2. 几何建模
场景中的复杂几何体与动态切割效果通过构建构造实体几何 (CSG) 和布尔运算（交、并、差）来实现。

整体场景的 SDF 函数 $D(\mathbf{p})$ 定义为各个实体切削结果的并集：

```math
D(\mathbf{p}) = \min_i \left[ \max(D_{\text{base}, i}(\mathbf{p}), D_{\text{cut planes}, i}(\mathbf{p})) \right]
```

其中，基础形状的 SDF 根据需求进行混合：

```math
D_{\text{base}} = D_{\text{sphere}} \lor D_{\text{clipped box}} \lor \min(D_{\text{sphere}}, D_{\text{clipped box}})
```

* $\max$ 操作用于**布尔差集**，实现切割面 ($D_{\text{cut planes}}$) 对基础形状 ($D_{\text{base}}$) 的裁剪。
* $\min$ 操作用于**布尔并集**，将多个独立形状组合在一起。

### 3. 表面着色
当光线命中几何体表面时（即 $D(\mathbf{p}) < \epsilon$），计算表面法线与光照模型。

表面法线 $\mathbf{n}$ 通过距离场的梯度求得：

```math
\mathbf{n} = \text{normalize}(\nabla D(\mathbf{p}))
```

最终表面颜色 $C_{\text{surface}}$ 包含环境光、漫反射以及边缘高光强调：

```math
C_{\text{surface}} = C_{\text{ambient}} + C_{\text{diffuse}} + C_{\text{edge accent}}
```

其中漫反射分量结合了传统的 Lambertian 光照以及阴影和遮蔽：

```math
C_{\text{diffuse}} = C_{\text{albedo}} \cdot L \cdot \max(0, \mathbf{n} \cdot \mathbf{l}) \cdot S_{\text{unity}} \cdot S_{\text{sdf}} \cdot AO
```

* $L$: 光源强度
* $\mathbf{l}$: 指向光源的方向向量
* $S_{\text{unity}}$: Unity 阴影贴图 (Shadowmap) 的遮挡值
* $S_{\text{sdf}}$: 基于 SDF 步进求得的软阴影 (Soft Shadow)
* $AO$: 环境光遮蔽 (Ambient Occlusion)

### 4. 体积渲染 
在光线穿透体积的路径上，根据参与介质的物理特性进行吸收、散射和透射计算。

**介质密度场 $\rho$**：

```math
\rho = f(M_{\text{shape}}, M_{\text{cut}}, M_{\text{height}}, N_{\text{noise}}, F_{\text{boundary}})
```

即体积密度受形状遮罩、切割状态、高度渐变、3D噪声以及边界衰减的多重控制。

**光学系数**：
吸收系数 $\sigma_a = \rho \cdot k_a$ ，散射系数 $\sigma_s = \rho \cdot k_s$ ，消光系数 $\sigma_t = \sigma_a + \sigma_s$。

**相位函数**：
决定光线在介质中发生散射时的方向分布，采用 Henyey-Greenstein 相位函数：

```math
p(\omega_l, \omega_v) = \frac{1}{4\pi} \frac{1-g^2}{(1+g^2-2g\cos\theta)^{3/2}}
```

* $\theta$: 视线方向 $\omega_v$ 与光线方向 $\omega_l$ 的夹角
* $g$: 各向异性因子 ($-1 \le g \le 1$)

**光线透射与积分**：
每一小段步进距离 $\Delta s_i$ 内的透射率 $T_i$ 与散射入射亮度 $I_i$：

```math
\begin{aligned}
T_i &= e^{-\sigma_{t,i} \Delta s_i} \\
I_i &= (\sigma_{s,i} L_{\text{in},i} p_i + L_{e,i}) \Delta s_i
\end{aligned}
```

通过前向射线步进 (Front-to-back Raymarching) 累加体积光和透射率：

```math
\begin{aligned}
L_{\text{volume}} &\leftarrow L_{\text{volume}} + T_{\text{acc}} I_i \\
T_{\text{acc}} &\leftarrow T_{\text{acc}} T_i
\end{aligned}
```

### 5. 最终合成
将表面光照与体积光照结合，映射到输出颜色 $C_{\text{out}}$：

**单 Pass 渲染模式**：

```math
C_{\text{out}} = \text{Map}(C_{\text{surface}} T_{\text{acc}} + L_{\text{volume}})
```

**屏幕空间体积光合成模式**：
如果采用分离的体积光 Pass 后处理：

```math
C_{\text{out}} = C_{\text{src}} T_{\text{blend}} + \text{Map}(L_{\text{volume}}) \alpha_{\text{volume}}
```

其中 $C_{\text{src}}$ 为不透明物体渲染完成后的原始场景颜色。
