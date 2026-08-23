# MainMenu DeveloperPanel BGM 切换设计规格

## 1. 需求概述
在主菜单打开/关闭 DeveloperPanel（制作人员名单/鸣谢列表）时，平滑切换专属 Credits BGM 并在关闭时无缝恢复先前的 BGM。同时保持既有 UI 音效交互正常触发。

## 2. 详细设计

### 2.1 UI 面板交互音效
- **打开面板**：触发现有的 `PanelOpen` UI 音效。
- **关闭面板**：触发现有的 `PanelClose` UI 音效。
- 无论是否配置 Credits BGM，UI 面板音效均正常播放，不受 BGM 逻辑影响。

### 2.2 AudioManager 扩展
- **Inspector 配置**：新增专用的 `Credits BGM`（`AudioClip`）序列化引用字段。
- **公开接口**：
  - `OpenCreditsBgm()`：开启制作人员名单专属 BGM 流程。
  - `CloseCreditsBgm()`：关闭制作人员名单并恢复原先 BGM 流程。
- **交叉淡化逻辑**：
  - 打开面板时：若配置了有效的 Credits BGM 且与当前播放曲目不同，暂存当前正在播放的 BGM，并在 2 秒内通过双 `AudioSource` 无缝交叉淡化（Cross-fade）切换至 Credits BGM。
  - 关闭面板时：在 2 秒内交叉淡化恢复先前暂存的 BGM 曲目。
  - 若未配置 Credits BGM 或配置为空：保持当前 BGM 继续播放，不切歌。
  - 普通常规 BGM 切换时长维持原有的 1 秒不变。

### 2.3 边界与约束
- **音量与 EQ**：不得干扰全局/BGM 音量设置、普通 SFX 以及 UI EQ 等现有音频参数。
- **侵蚀度 BGM 隔离**：不包含侵蚀度（Erosion）相关 BGM 自动切换逻辑，该需求后续单独设计与实现。

## 3. 涉及文件
- `Assets/Scripts/Audio/AudioManager.cs`（音频管理核心与 BGM 切换逻辑）
- `Assets/Scripts/UI/MainMenu/MainMenuManager.cs` 或对应 DeveloperPanel 触发脚本（面板开关事件挂接）
- 关联 Inspector 资产配置

## 4. 验证标准
1. 打开 DeveloperPanel 时，正常播放 `PanelOpen` UI 音效。
2. 若配置了 Credits BGM，BGM 在 2 秒内平滑淡化切换至 Credits BGM，无音量突变与破音。
3. 关闭 DeveloperPanel 时，正常播放 `PanelClose` UI 音效，且 BGM 在 2 秒内平滑恢复打开前的原曲目。
4. 若未配置 Credits BGM，打开/关闭面板时 UI 音效正常，原 BGM 不被打断或重置。
5. 常规场景/状态下的 BGM 切换时间仍保持 1 秒。
6. BGM 音量调节、全局静音/混音、UI EQ 与普通 SFX 功能保持不受任何负面影响。
