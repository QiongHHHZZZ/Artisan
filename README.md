<!-- Repository Header Begin -->
<div align="center">
<img src="https://github.com/PunishXIV/Artisan/blob/main/PunishImages/artisan-icon.png?raw=true" alt="Artisan IconUrl" width="15%">
<br>
<img src="https://github.com/PunishXIV/Artisan/blob/050a58be7b0ce94c959c17e43dabecb65e38a55c/PunishImages/artisan.png" width="30%" />

Crafting Automation Plugin/Helper for FFXIV

[![image](https://discordapp.com/api/guilds/1001823907193552978/embed.png?style=banner2)](https://discord.gg/Zzrcc8kmvy)

Repo Url: 

`https://love.puni.sh/ment.json`
</div>

## 版本发布规则（防漏更）

为保证 Dalamud 版本比较稳定、并能区分“上游同步”和“本地修订”，统一使用 4 段版本号：

- 格式：`主版本.次版本.补丁.构建`
- 约定：第 4 段采用“上游尾号 + 两位本地修订”

示例（以上游 `4.0.4.39` 为例）：

- 上游同步：`4.0.4.3900`
- 本地修订 1：`4.0.4.3901`
- 本地修订 2：`4.0.4.3902`
- 上游升级到 `4.0.4.40`：`4.0.4.4000`

注意：

- 必须单调递增，避免客户端不触发更新。
- 不使用 5 段版本号（如 `4.0.4.39.1`）。

## 发布流程（建议）

完整步骤见 `RELEASE_CHECKLIST.md`。

快速流程：

1. 更新 `Artisan/Artisan.csproj` 中的 `<Version>`。
2. 打 tag（如 `v4.0.4.3901`）并推送。
3. 创建/更新 Release，上传 `Artisan.zip`。
4. 同步 `DalamudPlugins/pluginmaster.json` 的版本与下载链接。
5. 运行 `scripts/check-release-sync.ps1` 做最终核对。

## 自动化

仓库已提供 tag 自动发布工作流：

- 推送 `v*` tag 后会自动构建并上传 `Artisan.zip` 到 GitHub Release。
- 仍建议在发布后执行一次 `scripts/check-release-sync.ps1`，确认与 `DalamudPlugins` 已一致。
