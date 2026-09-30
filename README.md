# Markdown 查看器

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

<p><img src="md.png" width="110" alt="Markdown Viewer icon"></p>

一个轻量的 Windows 桌面 Markdown 查看器，基于 **C# WinForms + WebView2**，渲染引擎为本地内置的 markdown-it + highlight.js（GitHub 风格样式），完全离线可用。

**为什么做它**：互联网上大多数 Markdown 查看器都太大了，而我想要的只是双击就能舒服地看一个 `.md` 文件，于是自己写了这个——构建产物整个目录仅约 1.6 MB（依赖框架版，Win10/11 已自带 WebView2 运行时）。

**关于界面**：设计参考了千问办公，很喜欢它简洁的风格，本程序的无边框窗口 + 轻量工具栏就是照着这个感觉做的。

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
| [markdown-it](https://github.com/markdown-it/markdown-it) 13.0.1 | Markdown 渲染（内置 `wwwroot/`，离线，MIT） |
| [highlight.js](https://github.com/highlightjs/highlight.js) 11.9.0 | 代码高亮 + GitHub 亮暗主题样式（内置，离线，BSD-3-Clause） |
| C# ↔ JS 桥接 | `WebMessageReceived` / `ExecuteScriptAsync`，负责打开、保存、窗口控制 |

## 第三方声明

本程序内置打包了 [markdown-it](https://github.com/markdown-it/markdown-it)（MIT）与 [highlight.js](https://github.com/highlightjs/highlight.js)（BSD-3-Clause），许可证全文见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## 许可证

本项目以 [MIT](LICENSE) 许可证发布。
