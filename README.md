# Commit and Tag Version (Unity Package)

这是一个专为 Unity 打造的零依赖、基于 C# 的自动化版本号管理和发版工具。它的核心灵感来源于前端社区流行的 [commit-and-tag-version](https://github.com/absolute-version/commit-and-tag-version)，但**完全脱离了 Node.js / NPM 环境**，作为原生 Unity UPM Package 运行。

它通过分析项目的 **约定式提交 (Conventional Commits)** 记录，自动帮你完成：
1. 计算下一个语义化版本号 (SemVer: Major/Minor/Patch)。
2. 将版本更新应用到 Unity `PlayerSettings`（同步 Android 和 iOS 构建编号）。
3. 生成并归类排版更新日志 (`CHANGELOG.md`)。
4. 自动在本地提交代码 (`git commit`) 并打上标签 (`git tag`)。

---

## 🚀 安装指南

### 方法一：通过本地路径导入 (Local Disk)
1. 在 Unity 编辑器顶部菜单栏点击 **Window** > **Package Manager**。
2. 点击左上角的 `+` 加号按钮。
3. 选择 **"Add package from disk..."**。
4. 找到并选择本插件目录下的 `package.json` 文件。

### 方法二：通过 Git URL 导入 (如果托管在外部仓库)
1. 在 Package Manager 中点击 `+`，选择 **"Add package from git URL..."**。
2. 输入本插件的 Git 仓库地址进行安装。

---

## 🛠️ 使用方法

### 1. 规范你的 Git Commit
该工具严格依赖 Conventional Commits 规范来判断版本变化的大小：

- `fix: 修复了登录界面的闪退问题` 🐛 （触发版本号 **Patch** 递增：1.0.0 -> 1.0.1）
- `feat: 增加多人联机模块` ✨ （触发版本号 **Minor** 递增：1.0.0 -> 1.1.0）
- `feat!: 重构核心网络架构，不兼容老服务器` 💥 或在内容中包含 `BREAKING CHANGE:` （触发 **Major** 主版本号递增：1.0.0 -> 2.0.0）

### 2. 在 Unity Editor 中可视化操作（日常发版）
1. 在顶部菜单栏点击 **Window > Versioning > Commit and Tag Version**。
2. 此面板会展示：
   - 当前读取到的最新 Git Tag。
   - 自上次发布以来的所有代码提交（带可视化分类图标）。
   - 根据历史计算出的**下个推荐版本号**。
3. 如果你想打预发布版，可以在框中填入特定字段（比如 `alpha`、`rc`）。
4. 点击底部巨大的 **Release** 按钮，一键搞定改版、发版、打 Tag。

### 3. 使用内部开发快照版本 (QA / 提测包)
如果你不想打正式的递增版本号，而只是想打一个带明确溯源信息的提测包给 QA：
- 在面板中勾选 **"Generate Internal Developer Version"** 选项。
- 面板自动将版本号组成 **`基准版本-当前分支名.自上个Tag相差的提交数+当前短Hash`**。（例如：`1.2.0-feature-login.15+a3b8c2d`）。
- 点击发布后将使用此特定标识生成并盖印。

### 4. CI/CD 无头模式自动流水线构建
如果你使用 Jenkins、GitLab CI、或 GitHub Actions，不需要打开 Unity 面板，工具提供了一行命令行自动发布的调用钩子：

```bash
# 执行正式版自动发布
Unity.exe -quit -batchmode -projectPath "你的工程路径" -executeMethod CommitAndTagVersion.Editor.AutoVersion.Run
```

**CI 中生成内部开发体验版：**
你可以利用环境变量强制插件打出带有分支信息的快照版本，这在做 Nightly Build 或者 Feature 分支自动化测试时非常有用：
```bash
# 在 PowerShell 或 Bash 中设置环境变量
# Windows: $env:COMMIT_TAG_DEVELOPER_VERSION="true"
# Bash: export COMMIT_TAG_DEVELOPER_VERSION="true"

Unity.exe -quit -batchmode -projectPath "你的工程路径" -executeMethod CommitAndTagVersion.Editor.AutoVersion.Run
```

---

## ⚙️ 后台自动修改了哪些文件？

1. **`ProjectSettings/ProjectSettings.asset`**
   - 自动更新了 `bundleVersion` 字段。
   - 自动使 Android 的 `bundleVersionCode` 数值 `+1`。
   - 自动使 iOS 的 `buildNumber` 数值 `+1`。
2. **`package.json`**（如果是库项目）
   - 修改了根目录 `package.json` 的 `version` 字段。
3. **`CHANGELOG.md`**
   - 读取、自动分类近期的 Bug 与功能，排版写入，自动前置。

## 注意事项

- 本工具运行时需要依赖外部 **`git` 系统命令**。请确保正在运行 Unity 或流水线的环境中已成功配置 Git，并且能被直接调用。
- 工具会自动执行 `git add .` 将由于生成 Changelog 和修改 Setting 造成的变动归入暂存区。如果在按下发版按钮前你工作区有未处理的其他大量散乱文件，建议先暂存或者搁置（Stash），保持发版 Commit 的纯净性。
