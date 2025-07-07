# QAirpress

QAirpress 是一款基于 C# 和 WPF 的桌面图片压缩工具，支持批量处理、格式转换、压缩质量自定义等多种高级功能，适合对图片体积和质量有要求的用户。

## 主要功能
- **批量图片压缩**：支持 JPG、PNG、GIF、BMP、TIFF、WEBP 等多种格式的图片批量压缩。
- **自定义压缩质量**：可通过滑块灵活调整压缩质量，实时预览压缩后大小。
- **格式转换**：可选将图片统一转换为 JPG 格式。
- **文件命名规则**：支持自定义压缩后文件的命名方式和后缀，或直接覆盖原文件。
- **EXIF 信息保留**：可选择是否保留图片的 EXIF 信息。
- **体积限制**：可设置压缩后图片的最大体积限制。
- **进度与大小显示**：压缩过程有进度条和文件大小显示，支持进度隐藏。
- **优化选项**：支持为 Web 优化、JPEG 高级参数等。
- **拖拽与文件夹选择**：支持拖拽图片或文件夹到主界面，自动递归查找图片。
- **设置窗口**：所有参数均可在设置窗口中灵活配置，并自动保存到 `settings.txt`。

## 安装与运行
1. 克隆本仓库到本地：
   ```bash
   git clone https://github.com/Qwejay/QAirpress.git
   ```
2. 使用 Visual Studio 2022 或更高版本打开 `QAirpress.sln` 解决方案文件。
3. 还原 NuGet 包（首次打开时会自动完成）。
4. 编译并运行项目（F5 或点击"启动"按钮）。

## 系统要求
- Windows 10 及以上操作系统
- .NET 6.0 或更高版本
- Visual Studio 2022（推荐）

## 目录结构
```
QAirpress/
├── App.xaml                # 应用程序入口及资源定义
├── App.xaml.cs             # 应用程序主逻辑
├── MainWindow.xaml         # 主窗口界面
├── MainWindow.xaml.cs      # 主窗口逻辑（图片压缩、拖拽、进度等核心功能）
├── SettingsWindow.xaml     # 设置窗口界面
├── SettingsWindow.xaml.cs  # 设置窗口逻辑（参数配置、保存、加载）
├── settings.txt            # 用户自定义配置文件
├── Properties/             # 项目属性与发布配置
├── QAirpress.csproj        # 项目文件
├── QAirpress.sln           # 解决方案文件
└── app.ico                 # 应用图标
```

## 许可证
本项目采用 MIT 许可证，详情请查阅 LICENSE 文件。 