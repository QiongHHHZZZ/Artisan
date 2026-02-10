# Release Checklist

> 用于确保 `Artisan` Release 与 `DalamudPlugins` 同步，避免“发了版本但客户端不更新”。

## 一、版本规划

- [ ] 确认本次版本号遵循 4 段规则（如 `4.0.4.3901`）
- [ ] 确认版本号相对上一版单调递增
- [ ] 更新 `Artisan/Artisan.csproj` 的 `<Version>`

## 二、构建与发布

- [ ] `dotnet build Artisan/Artisan.csproj -c Release`
- [ ] 产物存在：`Artisan/bin/Release/Artisan/latest.zip`
- [ ] 推送分支与 tag（如 `v4.0.4.3901`）
- [ ] Release 存在且包含资产 `Artisan.zip`

## 三、同步 DalamudPlugins

- [ ] 更新 `DalamudPlugins/pluginmaster.json`：
  - [ ] `AssemblyVersion`
  - [ ] `DownloadLinkInstall`
  - [ ] `DownloadLinkUpdate`
- [ ] 推送 `DalamudPlugins/main`

## 四、最终核对

- [ ] 运行：`powershell -ExecutionPolicy Bypass -File scripts/check-release-sync.ps1 -Version <版本号>`
- [ ] 输出检查全部通过
- [ ] 客户端手动“检查更新”验证一次

