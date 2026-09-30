# Markdown 查看器

<p><img src="md.png" width="110" alt="Markdown Viewer icon"></p>

一个轻量的 Windows 桌面 Markdown 查看器，基于 **C# WinForms + WebView2**，渲染引擎为本地内置的 markdown-it + highlight.js（GitHub 风格样式），完全离线可用。

## 功能

- 打开 / 查看 `.md` / `.markdown` / `.mdown` / `.txt` 文件
- 保存 / 另存为，统一保存为 UTF-8（无 BOM）；打开时自动识别 UTF-8 / ANSI 编码
- 文件关联注册：`MarkdownViewer.exe --register` 注册 `.md` / `.markdown` / `.mdown`（自动请求 UAC 提权，显示自定义文件图标），`--unregister` 取消并恢复原关联
- 无边框窗口：标题栏拖动、右下角拖拽缩放、最小化 / 最大化 / 关闭，1px 窗口边框颜色跟随系统亮暗主题
- GitHub 亮色 / 暗色两套渲染样式

## 构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)：

```bat
build.bat        :: 依赖框架构建（体积小，运行需安装 .NET 8 Desktop Runtime）
build.bat sc     :: 自包含构建（无需安装运行时，体积较大）
```

产物输出在 `publish\` 目录，分发时拷贝整个 `publish` 文件夹即可。

## 技术栈

| 组件 | 说明 |
|------|------|
| .NET 8 WinForms | 窗口、文件对话框、注册表文件关联 |
| WebView2 | 渲染界面（本地 wwwroot 通过虚拟主机 `https://app` 加载） |
| markdown-it + highlight.js | Markdown 渲染与代码高亮（内置，离线） |
| C# ↔ JS 桥接 | `WebMessageReceived` / `ExecuteScriptAsync`，负责打开、保存、窗口控制 |
