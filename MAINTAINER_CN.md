# Artisan 国服维护说明（CN-MAINTAINER v1）

## 文档目的

- 作为 Artisan 国服维护分支的唯一维护文档。
- `README.md` 面向用户；维护规则只写在本文件。

## 维护原则

- 分发以你的仓库为准：
  - 源码：`QiongHHHZZZ/Artisan`
  - 仓库清单：`QiongHHHZZZ/DalamudPlugins`
- 版本号必须可比较、可追溯、可复现。

## 版本规则（唯一规则）

- 版本真源：`Artisan/Artisan.csproj`
- 发版时必须保持：
  - `Version == AssemblyVersion == FileVersion`
- 版本格式：4 段纯数字 `A.B.C.D`
- 推荐第 4 段使用 `UUFF`：
  - `UU`：上游第 4 段（两位）
  - `FF`：本地修订号（`00-99`）

示例：

- 上游：`4.0.4.39`
- 纯上游同步：`4.0.4.3900`
- 本地修订：`4.0.4.3901`、`4.0.4.3902`
- 上游升级：`4.0.4.4000`

递增规则：

1. 上游不变：`FF + 1`
2. 上游升级：`UU` 更新，`FF` 重置为 `00`
3. 已发布版本必须严格递增，禁止回退

## 发版流程

1. **确定版本号**
   - 按本文件“版本规则”生成新版本。
2. **更新版本真源**
   - 修改 `Artisan/Artisan.csproj` 中 `Version/AssemblyVersion/FileVersion`。
3. **本地构建**
   - `dotnet build Artisan/Artisan.csproj -c Release -v minimal`
4. **发布源码仓库 Release**
   - 创建 tag：`v<版本号>`
   - 上传发布资产（`Artisan.zip`）
5. **更新 DalamudPlugins**
   - 修改 `pluginmaster.json` 中 `Artisan` 条目：
     - `AssemblyVersion`
     - `DownloadLinkInstall`
     - `DownloadLinkUpdate`
     - `RepoUrl`
6. **分发校验**
   - 确认 `pluginmaster` 链接可访问
   - 确认仓库 API 已返回新版本
7. **客户端验证**
   - Dalamud 手动检查更新并验证可安装/可加载

## 发布检查清单

- [ ] 版本号符合 4 段规则且单调递增
- [ ] `Version == AssemblyVersion == FileVersion`
- [ ] `dotnet build Artisan/Artisan.csproj -c Release -v minimal` 通过
- [ ] Release 资产可下载
- [ ] `pluginmaster` 中版本与下载链接已更新
- [ ] Dalamud 客户端手动检查更新通过

## 常见问题

- **源码版本和仓库版本不一致**
  - 以 `csproj` 为准，重新发布并同步 `pluginmaster`。
- **发版后客户端不更新**
  - 检查 `AssemblyVersion` 是否递增。
  - 检查 `DownloadLinkInstall/Update` 是否指向本次发布资产。
