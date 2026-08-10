# Color Mask Parallax Shader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **本计划特殊约定：** 按既有计划约定与工作区策略，本计划**不包含任何 bash / git 命令**（不提交、不改动 git 状态），每个任务以「Unity 编译验证 + Console + Play 模式人工验收」作为完成检查点，任务末尾设「评审门」。本计划**不新增任何自动化测试**：`ParallaxBackground` 属默认 Assembly-CSharp，`Hotel.Runtime.Tests` 依赖 `Hotel.Runtime`/`Hotel.Authoring`、无法引用 Assembly-CSharp（既有历史限制，沿用 ARCHITECTURE.md 约定）；**不创建任何 asmdef / tests 目录**。所有验收为 Unity Editor shader 编译检查 + Console + Play 模式人工验收。

**Goal:** 依据已批准规格《颜色掩码视差 Shader 设计（2026-08-11）》实现 URP 14.0.12 / 2D Renderer 下适用于 SpriteRenderer 的 ShaderLab shader `Assets/Shaders/ColorMaskParallax.shader` 与驱动脚本 `Assets/Scripts/Hotel/Camera/ParallaxBackground.cs`：以一张控制贴图（绿=前景、红=中景、透明=后景）+ 鼠标相对屏幕中心偏移驱动单次主纹理采样的 UV 位移，实现伪 3D 视差；材质面板只公开 Green/Red/Feather 三个参数；不与相机、URP 管线资产、Renderer2D 默认材质、HotelMap 产生任何耦合。

**Architecture:** Shader 采用方案 A（单次主纹理采样）：片元阶段先采样控制贴图得到原始权重（`rawGreen = saturate(g−max(r,b))×a`、`rawRed = saturate(r−max(g,b))×a`），对红/绿做 smoothstep 羽化（`fG/fR`），后景取 `fBg = max(0,1−fG−fR)`，**羽化后**三层重归一化（`wG/wR/wBg`，零分母回退 `wBg=1`），最终 UV 偏移 = `wG×(−mouse×Green) + wR×(−mouse×Red) + wBg×(0,0)`，据此对 `_MainTex` 采样；shader 完整自带 sprite 渲染路径（`LightMode = "Universal2D"`、Core.hlsl + Core2D.hlsl、CBUFFER、顶点色 × `_Color` × `_RendererColor`、instancing flip），不 UsePass 任何现成 2D Sprite shader。脚本侧：`ParallaxBackground` 新增序列化字段持有控制图与可选 shader，Start 时为自身创建独立材质实例（绝不触碰 Renderer2D 默认材质/HotelMap），每帧用缓存的材质实例写入隐藏的 `_MouseOffset`（旧 Input Manager `Input.mousePosition` + `Screen` 尺寸归一化），OnDestroy 恢复原材质并销毁运行时材质；既有 `parallaxFactor` transform 视差保持不变，UV 视差在 shader 内叠加。

**Tech Stack:** Unity 2022.3.62f3c1 LTS（`ProjectSettings/ProjectVersion.txt` 实测）、URP 14.0.12（`Packages/manifest.json` `com.unity.render-pipelines.universal: "14.0.12"`）、2D Renderer（`Assets/Settings/Renderer2D.asset`，`m_RendererType: 1`，配套 `UniversalRenderPipelineAsset` = `Assets/Settings/UniversalRP.asset`）、旧 Input Manager（`ProjectSettings/ProjectSettings.asset` `activeInputHandler: 0`）、ShaderLab / HLSL、C#（Assembly-CSharp）。

## Global Constraints

- **独立子项目范围（只允许下列文件）**：新建 `Assets/Shaders/ColorMaskParallax.shader`、`Assets/Editor/GenerateParallaxControlMask.cs`、`Assets/Textures/ParallaxControlMask.png`、`Assets/Materials/BackgroundParallax.mat`；修改 `Assets/Scripts/Hotel/Camera/ParallaxBackground.cs`、`Assets/Scenes/MainScene.unity`（仅 Background 对象的材质/组件字段接线）。**不触碰**相机（`CameraController`）、URP 管线资产（`Assets/Settings/UniversalRP.asset`、`Assets/Settings/Renderer2D.asset`）、Renderer2D 默认材质（URP package 内 Sprite-Lit-Default，guid `a97c105638bdf8b4a8650670310a4cd3`，场景中 12+ 个 SpriteRenderer 共用）、HotelMap 材质、任何其他 SpriteRenderer。
- **Shader 渲染路径**：Pass `Tags { "LightMode" = "Universal2D" }`（2D Renderer 绘制 SpriteRenderer 的入口）；`#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"` 与 `Shaders/2D/Include/Core2D.hlsl`；材质属性放入 `CBUFFER_START(UnityPerMaterial)`。**不得** `UsePass` 引用 `Universal Render Pipeline/2D/Sprite-Unlit-Default` 或任何其他 2D Sprite shader——本 shader 自带完整 pass。
- **Sprite 兼容路径**：`_MainTex` 由 SpriteRenderer 自动赋值（主纹理随 sprite 切换，**不得**把主背景贴图固定为单独序列化字段）；UV 经 `TRANSFORM_TEX(v.uv, _MainTex)` 适配 sprite/图集；顶点色 × `_Color` × `_RendererColor`，`UNITY_INSTANCING_ENABLED` 下再乘 `unity_SpriteColor`、用 `UnityFlipSprite` 处理 flip（与 URP `Sprite-Unlit-Default.shader` 同构）。
- **算法公式（逐字对齐规格 §2.2/§2.3/§3.2）**：`rawGreen = saturate(g − max(r, b)) × a`、`rawRed = saturate(r − max(g, b)) × a`；smoothstep 羽化在**归一化之前**：`fG = smoothstep(0, Feather, rawGreen)`、`fR = smoothstep(0, Feather, rawRed)`、`fBg = max(0, 1 − fG − fR)`；重归一化在羽化之后：`wG = fG/total`、`wR = fR/total`、`wBg = fBg/total`（保证同一像素不会同时获得满幅度红/绿位移，红绿 offset 绝不相加放大）；零分母回退 `wBg = 1`、`wG = wR = 0`；最终偏移 = `wG×greenOffset + wR×redOffset + wBg×(0,0)`，其中 `greenOffset = −(dx,dy)×Green Parallax`、`redOffset = −(dx,dy)×Red Parallax`（**负号必须保留**）。
- **Feather 语义**：范围 0~0.5、默认 0.1（规格 §1.2 不变式 5/§2.3/§4）；`Feather = 0` 退化为硬边选择；HLSL `smoothstep` 要求两个边不相等，实现用 `max(_Feather, 1e-5)` 保证定义。
- **鼠标输入与归一化**：脚本每帧读旧 Input Manager `Input.mousePosition`，归一化 `dx = (mouse.x / Screen.width − 0.5) × 2`、`dy = (mouse.y / Screen.height − 0.5) × 2`（即 `(鼠标坐标 − 中心) ÷ 半宽/半高`，约 −1~1，规格 §3.1）；写入隐藏属性 `_MouseOffset`，材质面板不可见。
- **材质面板**：只公开 `Green Parallax`（0~0.1，默认 0.03）、`Red Parallax`（0~0.1，默认 0.015）、`Feather`（0~0.5，默认 0.1）三个 Range 参数；`_MouseOffset`、`_Color`、`_RendererColor`、`_Flip` 全部 `[HideInInspector]`。
- **运行时材质策略**：`ParallaxBackground` 在 Start 只**为 Background 自身创建独立材质实例**（`new Material(shader)`）并赋给 `spriteRenderer.sharedMaterial`；**绝不**改 Renderer2D 默认材质或 HotelMap 材质；OnDestroy 恢复原材质引用并 `Destroy(parallaxMaterial)`；每帧用**缓存的材质实例**调用 `SetVector`/`SetFloat`，**绝不**调用 `renderer.material`（避免每帧实例化分配）。
- **与现有相机位置视差共存**：保留 `parallaxFactor = 0.2` 与 LateUpdate 中 transform 位移视差（**不改默认值**）；UV 视差在 shader 内叠加，两者独立——transform 平移不改变 UV 空间，鼠标偏移只驱动 UV 采样偏移。
- **控制图**：导入固定 Bilinear（非 Point）+ Clamp（规格不变式 7）；sRGB 关闭、压缩 None（保证纯色区域值精确）；红绿区域不重叠（规格 §6.1）；羽毛过渡带依赖 Bilinear 在颜色层边界的插值带。
- **已知限制纳入验收（规格 §6）**：红绿重叠会折中位移（当前控制图不重叠）；单采样无真实遮挡；无惯性/缓动；仅桌面鼠标；后景不动层在极端幅度下分离（`Green Parallax` 上限 0.1）；采样越界遵循主纹理 Wrap Mode（sprite 默认 Clamp），边缘拉伸/空洞由美术预留安全余量。
- **无 git 操作**：本计划不含任何 bash/git 命令，不做任何提交（用户未请求提交，工作区存在外部变更）；所有任务以「评审门」步骤收尾。

---

## File Structure

| 文件 | 操作 | 职责 |
| --- | --- | --- |
| `Assets/Shaders/ColorMaskParallax.shader` | **创建** | 方案 A 颜色掩码视差 shader（单次主纹理采样、Raw 权重 + smoothstep 羽化 + 重归一化、UV 负偏移、CBUFFER、Universal2D pass） |
| `Assets/Scripts/Hotel/Camera/ParallaxBackground.cs` | **修改** | 新增控制图/可选 shader/三参数序列化字段；Start 创建独立运行时材质；每帧写隐藏 `_MouseOffset`；安全降级与销毁 |
| `Assets/Editor/GenerateParallaxControlMask.cs` | **创建** | Editor 菜单工具：程序化生成 1024×512 三段式控制图 PNG（底部绿/中部红/顶部透明） |
| `Assets/Textures/ParallaxControlMask.png` | **创建**（工具生成） | 控制图，导入设置为 Bilinear + Clamp |
| `Assets/Materials/BackgroundParallax.mat` | **创建** | 绑定 ColorMaskParallax shader 与 ParallaxControlMask，Green=0.03 / Red=0.015 / Feather=0.1 |
| `Assets/Scenes/MainScene.unity` | **修改**（仅 Inspector 操作） | Background（fileID 1124948774）SpriteRenderer 材质 + ParallaxBackground 组件接线 |

## 设计决策（已定，供各任务对齐）

- **`_MainTex` 跟随 sprite**：SpriteRenderer 每帧自动把当前 sprite 写入材质 `_MainTex`/`_MainTex_ST`，故阶段背景切换（`SetBackgroundForPhase` 只改 `spriteRenderer.sprite`）在自定义材质上天然成立，无需脚本处理。
- **`_ControlTex` 用 `[NoScaleOffset]`**：控制图与控制图 ST 均不暴露 Tiling/Offset（`[NoScaleOffset]` 且不声明 `_ControlTex_ST`），片元用与主纹理一致的 `i.uv` 采样，保证控制图与 sprite 内容对齐。
- **Feather 运行时可调**：脚本每帧把 `greenParallax`/`redParallax`/`feather` 三个组件字段推入材质（`SetFloat` 无分配），使 Play 模式下可直接改组件滑块观察过渡（验收场景 3）。
- **只存在一个使用该 shader 的渲染器**（Background），`_MainTex` 无多实例 batching 歧义；CBUFFER（SRP Batcher 兼容）为要求项。

---

### Task 1: 创建 ColorMaskParallax Shader

**Files:**
- Create: `Assets/Shaders/ColorMaskParallax.shader`

**Interfaces:**
- Consumes: 无（本任务为全部后续任务的前置）。
- Produces: shader `Custom/ColorMaskParallax`，属性 `_MainTex`（Sprite 主纹理，SpriteRenderer 自动赋值）、`_ControlTex`（`[NoScaleOffset]` 控制图）、`_GreenParallax`/`_RedParallax`/`_Feather`（Range）、`_MouseOffset`（`[HideInInspector]` Vector，脚本写入）、`_Color`/`_RendererColor`/`_Flip`（`[HideInInspector]`，sprite 兼容）。Task 2 以 `Shader.Find("Custom/ColorMaskParallax")` 或 `parallaxShader` 字段引用；Task 4 材质选择该 shader；Task 5 场景接线使用。

- [ ] **Step 1: 创建 shader 文件**

1. 在 Unity Project 窗口右键 `Assets/` → Create → Folder，命名 `Shaders`（若已存在跳过）。
2. 用任意文本编辑器创建 `Assets/Shaders/ColorMaskParallax.shader`，内容**精确**为：

```shader
Shader "Custom/ColorMaskParallax"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        [NoScaleOffset] _ControlTex ("Control Mask (R=mid, G=front, A=0 bg)", 2D) = "black" {}
        _GreenParallax ("Green Parallax", Range(0.0, 0.1)) = 0.03
        _RedParallax ("Red Parallax", Range(0.0, 0.1)) = 0.015
        _Feather ("Feather", Range(0.0, 0.5)) = 0.1

        // Legacy sprite properties; values come from SpriteRenderer.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        // Runtime mouse offset; script-written only, hidden from material panel.
        [HideInInspector] _MouseOffset ("Mouse Offset", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex ParallaxVertex
            #pragma fragment ParallaxFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_ControlTex);
            SAMPLER(sampler_ControlTex);

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half4  _RendererColor;
                float4 _MainTex_ST;
                float  _GreenParallax;
                float  _RedParallax;
                float  _Feather;
                float4 _MouseOffset;
            CBUFFER_END

            Varyings ParallaxVertex(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            #ifdef UNITY_INSTANCING_ENABLED
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteFlip);
            #endif
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color * _RendererColor;
            #ifdef UNITY_INSTANCING_ENABLED
                o.color *= unity_SpriteColor;
            #endif
                return o;
            }

            half4 ParallaxFragment(Varyings i) : SV_Target
            {
                // Raw weights (spec 2.2): green = foreground, red = mid, transparent = background.
                float4 control = SAMPLE_TEXTURE2D(_ControlTex, sampler_ControlTex, i.uv);
                float rawGreen = saturate(control.g - max(control.r, control.b)) * control.a;
                float rawRed   = saturate(control.r - max(control.g, control.b)) * control.a;

                // Feather via smoothstep BEFORE renormalization (spec 2.3); Feather=0 degrades to hard edge.
                float feather = max(_Feather, 1e-5);
                float fG = smoothstep(0.0, feather, rawGreen);
                float fR = smoothstep(0.0, feather, rawRed);
                float fBg = max(0.0, 1.0 - fG - fR);

                float total = fG + fR + fBg;
                float wG = total > 1e-5 ? fG / total : 0.0;
                float wR = total > 1e-5 ? fR / total : 0.0;
                float wBg = total > 1e-5 ? fBg / total : 1.0; // zero-denominator fallback: background.

                // Negative UV offsets (spec 3.2): greenOffset = -(dx,dy)*Green, redOffset = -(dx,dy)*Red.
                float2 greenOffset = -_MouseOffset.xy * _GreenParallax;
                float2 redOffset   = -_MouseOffset.xy * _RedParallax;
                float2 sampleUV = i.uv + wG * greenOffset + wR * redOffset + wBg * float2(0.0, 0.0);

                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
                mainTex *= i.color;
                return mainTex;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
```

3. 返回 Unity，等待导入完成。

- [ ] **Step 2: Unity 编译验证**

1. 观察 Console（Window → General → Console）：Expected **0 错误、0 警告**，无 `Shader error in 'Custom/ColorMaskParallax'`、无 `cannot open include file`。
2. 在 Project 窗口选中 `Assets/Shaders/ColorMaskParallax.shader`，Inspector 顶部显示 shader 名 `Custom/ColorMaskParallax` 与「Shader Properties」列表：`Sprite Texture`、`Control Mask (R=mid, G=front, A=0 bg)`、`Green Parallax`、`Red Parallax`、`Feather`；**不显示** `Mouse Offset`、`Tint`、`RendererColor`、`Flip`（均 `[HideInInspector]`）。
3. Inspector 底部可见「Compiled Code」按钮且无红色错误标记。

- [ ] **Step 3: 评审门**

将改动清单提交评审者复核：
- 改动文件：仅 `Assets/Shaders/ColorMaskParallax.shader`（新建）。
- 检查点：shader 名/属性名与 Task 2、4、5 引用完全一致（`_ControlTex`、`_GreenParallax`、`_RedParallax`、`_Feather`、`_MouseOffset`、`_MainTex`）；公式带负号、羽化在归一化前、零分母回退；CBUFFER 包裹全部材质属性；pass 为 `Universal2D`；无 `UsePass`。
- 通过标准：评审者确认后进入 Task 2；不通过则在本任务内修复后重新复核。

---

### Task 2: 修改 ParallaxBackground.cs（运行时材质 + 鼠标偏移驱动）

**Files:**
- Modify: `Assets/Scripts/Hotel/Camera/ParallaxBackground.cs`（整文件替换为下述内容）

**Interfaces:**
- Consumes: Task 1 的 shader 名 `Custom/ColorMaskParallax`（`Shader.Find` 回退）与属性名 `_ControlTex`/`_GreenParallax`/`_RedParallax`/`_Feather`/`_MouseOffset`；既有类型 `PhaseEnteredEvent`/`PhaseEnterData`/`GamePhaseManager`/`GamePhase`/`CameraController`。
- Produces: 序列化字段 `controlTexture`（`Texture2D`）、`parallaxShader`（`Shader`，可选）、`greenParallax`（float，默认 0.03）、`redParallax`（float，默认 0.015）、`feather`（float，默认 0.1）；每帧写入 `_MouseOffset`（Vector2，归一化公式见 Global Constraints）。Task 5 在 MainScene 接线这些字段。

- [ ] **Step 1: 替换脚本文件**

用文本编辑器将 `Assets/Scripts/Hotel/Camera/ParallaxBackground.cs` 内容**整体替换**为：

```csharp
using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    [Header("Parallax")]
    public float parallaxFactor = 0.2f;

    [Header("Color Mask Parallax")]
    public Texture2D controlTexture;
    public Shader parallaxShader;
    [Range(0f, 0.1f)] public float greenParallax = 0.03f;
    [Range(0f, 0.1f)] public float redParallax = 0.015f;
    [Range(0f, 0.5f)] public float feather = 0.1f;

    [Header("Phase Backgrounds")]
    public Sprite dawnBackground;
    public Sprite daytimeBackground;
    public Sprite duskBackground;
    public Sprite nightBackground;

    [Header("Event Listener")]
    public PhaseEnteredEvent onPhaseEntered;

    private const string DefaultParallaxShaderName = "Custom/ColorMaskParallax";

    private static readonly int ControlTexId = Shader.PropertyToID("_ControlTex");
    private static readonly int MouseOffsetId = Shader.PropertyToID("_MouseOffset");
    private static readonly int GreenParallaxId = Shader.PropertyToID("_GreenParallax");
    private static readonly int RedParallaxId = Shader.PropertyToID("_RedParallax");
    private static readonly int FeatherId = Shader.PropertyToID("_Feather");

    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;
    private Vector3 lastCameraPos;
    private CameraController camController;

    private Material parallaxMaterial;
    private Material materialBeforeParallax;
    private bool parallaxActive;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sortingOrder = -100;
    }

    private void Start()
    {
        mainCamera = Camera.main;
        camController = mainCamera.GetComponent<CameraController>();
        lastCameraPos = mainCamera.transform.position;

        SetupParallaxMaterial();

        // Set initial background
        if (GamePhaseManager.Instance != null)
            SetBackgroundForPhase(GamePhaseManager.Instance.currentPhase);
    }

    private void SetupParallaxMaterial()
    {
        if (spriteRenderer == null || controlTexture == null)
            return;

        Shader shader = parallaxShader != null ? parallaxShader : Shader.Find(DefaultParallaxShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[ParallaxBackground] ColorMaskParallax shader not found; color mask parallax disabled.", this);
            return;
        }

        parallaxMaterial = new Material(shader);
        parallaxMaterial.name = "Runtime ColorMaskParallax (Background)";
        parallaxMaterial.SetTexture(ControlTexId, controlTexture);
        parallaxMaterial.SetFloat(GreenParallaxId, greenParallax);
        parallaxMaterial.SetFloat(RedParallaxId, redParallax);
        parallaxMaterial.SetFloat(FeatherId, feather);
        parallaxMaterial.SetVector(MouseOffsetId, Vector4.zero);

        materialBeforeParallax = spriteRenderer.sharedMaterial;
        spriteRenderer.sharedMaterial = parallaxMaterial;
        parallaxActive = true;
    }

    private void Update()
    {
        if (!parallaxActive || parallaxMaterial == null)
            return;
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        // Push the three material-panel params every frame so Play mode tweaks apply live.
        parallaxMaterial.SetFloat(GreenParallaxId, greenParallax);
        parallaxMaterial.SetFloat(RedParallaxId, redParallax);
        parallaxMaterial.SetFloat(FeatherId, feather);

        // Normalize mouse position relative to screen center: (mouse - center) / half, ~[-1, 1].
        Vector2 normalized = new Vector2(
            (Input.mousePosition.x / Screen.width - 0.5f) * 2f,
            (Input.mousePosition.y / Screen.height - 0.5f) * 2f);
        parallaxMaterial.SetVector(MouseOffsetId, normalized);
    }

    private void OnEnable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
    }

    private void OnDisable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        Vector3 cameraDelta = mainCamera.transform.position - lastCameraPos;
        transform.position += new Vector3(
            cameraDelta.x * parallaxFactor * -1f,
            cameraDelta.y * parallaxFactor * -1f,
            0f);

        lastCameraPos = mainCamera.transform.position;
        UpdateScale();
    }

    private void OnPhaseEntered(PhaseEnterData data)
    {
        SetBackgroundForPhase(data.phase);
    }

    private void SetBackgroundForPhase(GamePhase phase)
    {
        Sprite bg = null;
        switch (phase)
        {
            case GamePhase.Dawn:    bg = dawnBackground; break;
            case GamePhase.Day:     bg = daytimeBackground; break;
            case GamePhase.Dusk:    bg = duskBackground; break;
            case GamePhase.Night:   bg = nightBackground; break;
        }

        if (bg != null && spriteRenderer != null)
            spriteRenderer.sprite = bg;
    }

    private void UpdateScale()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null || mainCamera == null)
            return;

        float currentHeight = 2f * mainCamera.orthographicSize;
        float currentWidth = currentHeight * mainCamera.aspect;

        Vector2 mapSize = Vector2.zero;
        if (camController != null)
            mapSize = camController.mapSize;

        float parallaxRangeX = mapSize.x * parallaxFactor;
        float parallaxRangeY = mapSize.y * parallaxFactor;

        float requiredWidth = currentWidth + parallaxRangeX;
        float requiredHeight = currentHeight + parallaxRangeY;

        float spriteW = spriteRenderer.sprite.bounds.size.x;
        float spriteH = spriteRenderer.sprite.bounds.size.y;

        if (spriteW <= 0 || spriteH <= 0) return;

        float scaleX = requiredWidth / spriteW;
        float scaleY = requiredHeight / spriteH;
        float scale = Mathf.Max(scaleX, scaleY);

        transform.localScale = Vector3.one * scale;
    }

    private void OnDestroy()
    {
        if (spriteRenderer != null && materialBeforeParallax != null)
            spriteRenderer.sharedMaterial = materialBeforeParallax;

        if (parallaxMaterial != null)
        {
            Destroy(parallaxMaterial);
            parallaxMaterial = null;
        }
    }
}
```

- [ ] **Step 2: Unity 编译验证**

1. 返回 Unity，等待重新编译。Expected：Console **0 错误、0 警告**。
2. 在 Hierarchy 选中 `MainScene` 中名为 `Background` 的 GameObject（含 `ParallaxBackground` 组件与 SpriteRenderer，Sorting Order 为 -50），Inspector 的 `ParallaxBackground` 组件现在显示新分区 `Color Mask Parallax`：`Control Texture`（Texture2D）、`Parallax Shader`（Shader）、`Green Parallax`（0.03）、`Red Parallax`（0.015）、`Feather`（0.1）滑块。既有字段（`Parallax` 区 `parallaxFactor: 0.2`、`Phase Backgrounds` 四个 sprite、`Event Listener`）保持不变。

- [ ] **Step 3: 评审门**

将改动清单提交评审者复核：
- 改动文件：仅 `Assets/Scripts/Hotel/Camera/ParallaxBackground.cs`（修改）。
- 检查点：既有行为逐行保留（`parallaxFactor` 默认 0.2、LateUpdate transform 视差、`UpdateScale`、`SetBackgroundForPhase`、事件注册）；运行时只创建**一个**独立材质实例；每帧只用缓存材质 `SetVector`/`SetFloat`、无 `renderer.material`；控制图/shader/屏幕尺寸无效时安全返回（不报错、不创建材质）；`OnDestroy` 恢复并销毁；阶段背景切换只改 sprite、不碰材质字段。
- 通过标准：评审者确认后进入 Task 3；不通过则在本任务内修复后重新复核。

---

### Task 3: 生成控制图并设置导入（Bilinear + Clamp）

**Files:**
- Create: `Assets/Editor/GenerateParallaxControlMask.cs`
- Create: `Assets/Textures/ParallaxControlMask.png`（由上述菜单工具生成）
- Modify: `Assets/Textures/ParallaxControlMask.png.meta`（由 Inspector 导入设置写入，工具不直接写 meta）

**Interfaces:**
- Consumes: 无。
- Produces: 控制图 `Assets/Textures/ParallaxControlMask.png`（1024×512，UV v<0.35 绿色前景 / 0.35≤v<0.7 红色中景 / v≥0.7 透明后景，红绿不重叠），导入设置 = Default 纹理、Bilinear、Clamp、sRGB 关、压缩 None。Task 4 材质 `_ControlTex` 与 Task 5 组件 `controlTexture` 均引用它。

- [ ] **Step 1: 创建 Editor 生成工具**

1. 在 Unity Project 窗口右键 `Assets/` → Create → Folder，命名 `Editor`（若已存在跳过）。
2. 创建 `Assets/Editor/GenerateParallaxControlMask.cs`，内容**精确**为：

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GenerateParallaxControlMask
{
    private const string OutputPath = "Assets/Textures/ParallaxControlMask.png";
    private const int Width = 1024;
    private const int Height = 512;
    private const int RedStartY = 154;    // v = 1 - y/(H-1) → 0.35
    private const int GreenStartY = 333;  // v = 1 - y/(H-1) → 0.7

    [MenuItem("Tools/Parallax/Generate Control Mask")]
    public static void Generate()
    {
        Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[Width * Height];

        for (int y = 0; y < Height; y++)
        {
            Color c = new Color(0f, 0f, 0f, 0f); // 顶部 v>=0.7：透明 → 后景
            if (y >= GreenStartY)
                c = new Color(0f, 1f, 0f, 1f);   // 底部 v<0.35：绿 → 前景
            else if (y >= RedStartY)
                c = new Color(1f, 0f, 0f, 1f);   // 中部：红 → 中景

            for (int x = 0; x < Width; x++)
                pixels[y * Width + x] = c;
        }

        tex.SetPixels(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Directory.CreateDirectory("Assets/Textures");
        File.WriteAllBytes(OutputPath, png);
        Object.DestroyImmediate(tex);
        AssetDatabase.Refresh();

        Debug.Log($"[GenerateParallaxControlMask] Wrote {OutputPath} ({Width}x{Height}, green bottom / red middle / transparent top)");
    }
}
```

- [ ] **Step 2: 运行菜单生成 PNG**

1. 等待 Unity 编译完成（Console 0 错误）。
2. 菜单 `Tools` → `Parallax` → `Generate Control Mask`。
Expected：Console 输出 `[GenerateParallaxControlMask] Wrote Assets/Textures/ParallaxControlMask.png (1024x512, ...)`；Project 窗口出现 `Assets/Textures/ParallaxControlMask.png`。

- [ ] **Step 3: 人工设置导入参数**

1. 在 Project 窗口选中 `Assets/Textures/ParallaxControlMask.png`，Inspector 设置并点 **Apply**：
   - Texture Type = **Default**（非 Sprite）
   - sRGB (Color Texture) = **关**
   - Max Size = **1024**
   - Compression = **None**
   - Filter Mode = **Bilinear**
   - Wrap Mode = **Clamp**
2. 用文本编辑器打开 `Assets/Textures/ParallaxControlMask.png.meta` 核对（Expected 值）：`textureType: 0`、`sRGBTexture: 0`、`textureCompression: 0`、`filterMode: 1`、`wrapU: 1`、`wrapV: 1`、`wrapW: 1`（与项目既有纹理一致：Bilinear=1、Clamp=1）。
3. 选中该 PNG，Inspector 预览：上半透明、中部红色条、底部绿色条，无马赛克/压缩色块。

- [ ] **Step 4: 评审门**

将改动清单提交评审者复核：
- 改动文件：`Assets/Editor/GenerateParallaxControlMask.cs`（新建）、`Assets/Textures/ParallaxControlMask.png` + `.meta`（生成/导入）。
- 检查点：导入设置为 Bilinear + Clamp（规格不变式 7）；压缩 None 保证纯色精确；sRGB 关闭；红绿不重叠（规格 §6.1）；分段边界 v=0.35/0.7 精确。
- 通过标准：评审者确认后进入 Task 4；不通过则在本任务内修复后重新复核。

---

### Task 4: 创建并配置 BackgroundParallax 材质

**Files:**
- Create: `Assets/Materials/BackgroundParallax.mat`

**Interfaces:**
- Consumes: Task 1 的 shader `Custom/ColorMaskParallax`；Task 3 的控制图 `Assets/Textures/ParallaxControlMask.png`。
- Produces: 材质 `Assets/Materials/BackgroundParallax.mat`（`_ControlTex`=ParallaxControlMask、`_GreenParallax`=0.03、`_RedParallax`=0.015、`_Feather`=0.1）。Task 5 赋给 MainScene Background 的 SpriteRenderer 作编辑期预览，并作为运行时替换前的原始材质引用。

- [ ] **Step 1: 创建并配置材质**

1. 在 Unity Project 窗口右键 `Assets/` → Create → Folder，命名 `Materials`（若已存在跳过）。
2. 右键 `Assets/Materials` → Create → Material，命名 `BackgroundParallax`。
3. 在选中该材质的 Inspector：
   - Shader 下拉 → 搜索 `ColorMaskParallax` → 选 `Custom/ColorMaskParallax`。
   - `Control Mask (R=mid, G=front, A=0 bg)` 字段拖入 `Assets/Textures/ParallaxControlMask.png`。
   - `Green Parallax` = **0.03**；`Red Parallax` = **0.015**；`Feather` = **0.1**。

- [ ] **Step 2: 验证材质**

1. Inspector 应**只显示** `Sprite Texture`、`Control Mask`、`Green Parallax`、`Red Parallax`、`Feather`；**不显示** Mouse Offset / Tint / RendererColor / Flip。
2. 用文本编辑器打开 `Assets/Materials/BackgroundParallax.mat` 核对：
   - `m_Shader: {fileID: 4800000, guid: <ColorMaskParallax.shader 的 meta guid>, type: 3}`（该 guid 与 `Assets/Shaders/ColorMaskParallax.shader.meta` 的 guid 一致）；
   - `m_TexEnvs` 的 `_ControlTex.m_Texture.guid` 与 `Assets/Textures/ParallaxControlMask.png.meta` 的 guid 一致，且 `m_Scale: {x: 1, y: 1}`、`m_Offset: {x: 0, y: 0}`；
   - `m_Floats` 含 `_Feather: 0.1`、`_GreenParallax: 0.03`、`_RedParallax: 0.015`；
   - 无 `_MouseOffset` 相关序列化条目（保持默认 (0,0,0,0)）。
3. 若材质预览缩略图正常（无洋红），表明 shader 可渲染。

- [ ] **Step 3: 评审门**

将改动清单提交评审者复核：
- 改动文件：仅 `Assets/Materials/BackgroundParallax.mat`（新建）。
- 检查点：材质引用正确的 shader 与控制图；三参数值 0.03/0.015/0.1；面板隐藏鼠标偏移。
- 通过标准：评审者确认后进入 Task 5；不通过则在本任务内修复后重新复核。

---

### Task 5: MainScene Background 接线

**Files:**
- Modify: `Assets/Scenes/MainScene.unity`（Background 对象 fileID 1124948774，仅 Inspector 操作，禁止任何 UI 布局/其他对象改动）

**Interfaces:**
- Consumes: Task 1 shader、Task 2 组件新字段、Task 3 控制图、Task 4 材质。
- Produces: MainScene 中 `Background` 的 SpriteRenderer 使用 `BackgroundParallax` 材质；`ParallaxBackground` 组件绑定 `controlTexture` 与 `parallaxShader`。Task 6 在此接线基础上做全量人工验收。

- [ ] **Step 1: 绑定材质与组件字段**

1. 打开 `Assets/Scenes/MainScene.unity`，Hierarchy 中选中名为 `Background` 的 GameObject（含 `ParallaxBackground` 组件、SpriteRenderer Sorting Order -50、m_Sprite 为 DayTimeCloudy）。
2. 在 `SpriteRenderer` 的 `Material` 字段拖入 `Assets/Materials/BackgroundParallax.mat`。
3. 在 `ParallaxBackground` 组件：
   - `Control Texture` = `Assets/Textures/ParallaxControlMask.png`；
   - `Parallax Shader` = `Assets/Shaders/ColorMaskParallax.shader`；
   - `Green Parallax` = 0.03、`Red Parallax` = 0.015、`Feather` = 0.1（与材质一致）；
   - 其余字段（`parallaxFactor`、四个阶段 sprite、`onPhaseEntered`）**保持原值不动**。
4. 保存场景（Ctrl+S）。

- [ ] **Step 2: 验证编辑期渲染与 sprite 跟随**

1. 编辑模式下 Scene/Game 视图：背景正常显示（`_MouseOffset` 为 0 → 与未应用效果时视觉一致），无洋红、无报错。
2. 确认 `_MainTex` 遵循 sprite：在 Project 窗口选中 `BackgroundParallax.mat`，Inspector 顶部 Debug 模式（右上角菜单 → Debug）中 `Sprite Texture` 显示为当前 sprite 贴图 `DayTimeCloudy`（Texture）；Play 模式下（进入 Task 6 前先不做深度验证）临时在 Background 的 SpriteRenderer 拖入另一 sprite 后，材质 Debug 面板的 `Sprite Texture` 随之变化——此步只确认机制成立即可。

- [ ] **Step 3: 评审门**

将改动清单提交评审者复核：
- 改动文件：仅 `Assets/Scenes/MainScene.unity`（Background 对象接线）。
- 检查点：未触碰 CameraController/GamePhaseManager 等任何其他对象；未创建/修改 Prefab；材质只赋给 Background；`parallaxFactor` 等既有字段未改；主背景贴图**未**被固定为单独字段（`_MainTex` 仍由 sprite 驱动）。
- 通过标准：评审者确认后进入 Task 6；不通过则在本任务内修复后重新复核。

---

### Task 6: Play 模式全量人工验收（最终完整验证）

**Files:**
- 只读验证，不修改任何文件。

**Interfaces:**
- Consumes: Task 1–5 全部成果。

- [ ] **Step 1: 环境与编译检查**

1. 打开 MainScene 进入 Play（旧 Input Manager，`activeInputHandler: 0`）。
2. Expected：Console **0 错误、0 警告**，无 `Shader error`、无 `MissingReferenceException`、无 `Material` 泄漏警告；Game 视图正常显示背景。

- [ ] **Step 2: 验收场景 1+5 —— 中心零位移与后景静止**

1. 将鼠标移到屏幕正中心（Game 视图内）：全画面与鼠标居中时无任何偏移（规格 §5 验收 5）。
2. 保持鼠标在中心，缓慢移动鼠标：画面**顶部三分之一**（控制图透明区 = 后景）完全不移动；随后把鼠标移出 Game 视图再移回中心，确认画面回到初始位置。

- [ ] **Step 3: 验收场景 2 —— 前景/中景分层**

1. 鼠标从屏幕中心向右移：**底部三分之一**（绿色前景）向左位移，且位移量明显大于**中部三分之一**（红色中景）；两层边缘相对错开，形成前后层次。
2. 鼠标向左/上/下移动：位移方向按规格 §3.2 负偏移约定反向（左移→内容右移、上移→内容下移）。
3. Expected：无红绿同像素叠加放大（无第三层伪影，规格 §5 验收 4）。

- [ ] **Step 4: 验收场景 3 —— Feather 羽化过渡**

1. Play 模式下选中 `Background`，把 `ParallaxBackground` 组件 `Feather` 从 0.1 调到 **0**：绿/红层边界变为硬边切换。
2. 把 `Feather` 调到 **0.5**：边界变为柔和过渡，无撕裂、无跳变；过渡空间宽度不超出控制图 Bilinear 插值带（约 1 个纹素，规格 §2.3）。
3. 测试后把 `Feather` 调回 0.1。

- [ ] **Step 5: 验收场景 4+6 —— 红绿不重叠与中心回位**

1. 红/绿区域不重叠前提下（生成的控制图满足），位移表现为两层的简单加权叠加，无第三层伪影（规格 §5 验收 4、§6.1）。
2. 鼠标回到中心：全画面归位，与未应用效果时一致（规格 §5 验收 5）。

- [ ] **Step 6: 阶段背景切换（不破坏既有流程）**

1. 方法 A（确定性）：Play 模式下在 `Background` 的 `SpriteRenderer.Sprite` 字段拖入 `Assets/Resources/BackGround/Night2.png`：Expected 背景即时切换为 Night2 且视差效果继续工作（移动鼠标，三层位移仍按控制图作用）；随后切回 `DayTimeCloudy`。
2. 方法 B（完整流程）：按游戏正常流程推进阶段（`GamePhaseManager.AdvancePhase`，需评审/分配/事件完成）经过 Day→Dusk/Night→Dawn：每个阶段背景 sprite 正确显示，视差效果持续，无报错。
3. Expected：整个切换过程中 `_MainTex` 随 sprite 更新（主纹理未被固定为单独字段），材质面板三参数持续生效。

- [ ] **Step 7: 边缘余量/越界与安全降级**

1. 鼠标移到屏幕四角极端位置（`dx,dy ≈ ±1`）：画面位移到最大幅度（Green Parallax 0.03 → 最大 UV 偏移 0.03）；采样越界处按主纹理 Clamp 行为拉伸/贴边，**不出现**红/绿错乱、不出现黑色或洋红空洞（规格 §6.6 已知限制，位移越大拉伸越明显属预期）。
2. 安全降级：退出 Play，把 `Background` 组件 `Control Texture` 临时清空，重新进入 Play：Expected 背景使用原材质正常渲染（视差关闭），Console 无错误；退出 Play 后把 `Control Texture` 恢复为 ParallaxControlMask。
3. 退出 Play：Expected Console 无错误；Background 的 SpriteRenderer `Material` 恢复为 `BackgroundParallax`（编辑期值）；重新进入 Play 视差正常（OnDestroy 正确销毁运行时材质、无泄漏）。

- [ ] **Step 8: 最终评审门（完整验证收尾）**

将最终验证结果提交评审者全量复核：
- 改动文件仅限：`Assets/Shaders/ColorMaskParallax.shader`、`Assets/Scripts/Hotel/Camera/ParallaxBackground.cs`、`Assets/Editor/GenerateParallaxControlMask.cs`、`Assets/Textures/ParallaxControlMask.png`(+.meta)、`Assets/Materials/BackgroundParallax.mat`、`Assets/Scenes/MainScene.unity`（核对用，不做任何提交）。
- 检查点：Console 0 错误；规格 §5 五条验收全部通过（后景静止/分层/羽化/不重叠/中心零位移）；阶段背景切换正常；边缘余量符合已知限制说明；安全降级路径无报错；`parallaxFactor = 0.2` 与相机视差共存未改；未触碰 Renderer2D 默认材质与 HotelMap；未创建 asmdef/tests；未执行任何 git 操作。
- 通过标准：评审者确认后，本计划完成。

---

## Self-Review

- **规格覆盖**：§1.2 不变式 1（URP 2D/ShaderLab/桌面鼠标）→ Global Constraints + Task 1；不变式 2（单主纹理 + 控制图，绿=前景/红=中景/透明=后景）→ Task 1（`_MainTex` + `_ControlTex`）；不变式 3（方案 A 单采样 + Raw 权重 + smoothstep 羽化 + 重归一化 + 三位移插值）→ Task 1 片元代码；不变式 4（鼠标相对屏幕中心归一化）→ Task 2 `Update`；不变式 5（面板三参数：Green/Red/Feather 0~0.5 默认 0.1）→ Task 1 Properties + Task 4 材质；不变式 6（不双采样/不改相机/无输入管线）→ Global Constraints 范围门；不变式 7（Bilinear + Clamp）→ Task 3 导入设置。§2.2 Raw 权重公式 → Task 1 代码；§2.3 羽化顺序（先 smoothstep 后归一化）、零分母回退、`Feather=0` 硬边 → Task 1 代码注释与 Global Constraints。§3.1 归一化 `(mouse−center)/half` → Task 2 公式；§3.2 负偏移符号 → Task 1 `- _MouseOffset.xy * ...`；§3.3 Clamp 边缘与主纹理 Wrap 行为 → Task 6 Step 7。§4 参数初始值 0.03/0.015/0.1 → Task 1/2/4/5 全部一致。§5 五条验收 → Task 6 Step 2/3/4/5。§6 已知限制 → Global Constraints + Task 6 Step 7（边缘拉伸/空洞、红绿不重叠）。用户要求项：运行时独立材质、不改 Renderer2D 默认材质/HotelMap → Task 2 + Global Constraints；`_MainTex` 跟随 sprite 不固定字段 → Task 2（`SetBackgroundForPhase` 只改 sprite）+ Task 5 Step 2 + Task 6 Step 6；阶段背景切换共存 → Task 6 Step 6；保留 `parallaxFactor` 叠加 UV 视差 → Task 2（LateUpdate 原样保留）+ Global Constraints；无自动化测试、不建 asmdef/tests、Unity Editor + Console + PlayMode 人工验收 → 计划头约定 + Task 6；每任务评审门 + 最终完整验证 → Task 1–6 各评审门；无 git 步骤 → Global Constraints。无缺口。
- **占位符扫描**：全文无 TBD/TODO/「待定」/「类似 Task N」；每个代码步骤给出完整文件内容或精确操作序列；所有期望值（meta 字段、材质 m_Floats、归一化公式、边界阈值 154/333、参数初始值）均为具体数值。
- **接口与类型一致性**：shader 属性名（`_MainTex`/`_ControlTex`/`_GreenParallax`/`_RedParallax`/`_Feather`/`_MouseOffset`）在 Task 1 定义、Task 2 `Shader.PropertyToID`、Task 4 材质字段、Task 5 接线四处逐字一致；shader 名 `Custom/ColorMaskParallax` 在 Task 1 定义、Task 2 `DefaultParallaxShaderName`/`parallaxShader` 回退、Task 4 材质选择、Task 5 组件字段一致；组件字段名（`controlTexture`/`parallaxShader`/`greenParallax`/`redParallax`/`feather`）在 Task 2 定义与 Task 5/6 操作描述一致；`parallaxFactor = 0.2` 默认值在 Global Constraints 与 Task 2 代码、Task 5 Step 1（保持原值）一致；控制图路径 `Assets/Textures/ParallaxControlMask.png` 在 Task 3 生成、Task 4 引用、Task 5 接线、Task 6 恢复操作一致；材质路径 `Assets/Materials/BackgroundParallax.mat` 在 Task 4 创建、Task 5 赋值、Task 6 恢复验证一致。
- **范围声明复核**：本计划仅创建设计/实现计划文档（用户本轮指令只要求写计划，未要求实施）；实施阶段执行方不得触碰相机、URP 管线资产、Renderer2D 默认材质、HotelMap，不得创建 asmdef/tests，不得执行 git 操作。
