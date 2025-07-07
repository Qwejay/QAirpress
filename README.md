# QAirpress

QAirpress 是一个基于 C# 和 WPF 技术开发的桌面应用程序，旨在为用户提供高效、便捷的空气质量管理与监控体验。

## 主要功能
- 空气质量数据的实时监控与展示
- 支持多种数据源的接入
- 友好的用户界面，易于操作
- 设置窗口，支持自定义参数

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
├── MainWindow.xaml.cs      # 主窗口逻辑
├── SettingsWindow.xaml     # 设置窗口界面
├── SettingsWindow.xaml.cs  # 设置窗口逻辑
├── Properties/             # 项目属性与发布配置
├── QAirpress.csproj        # 项目文件
├── QAirpress.sln           # 解决方案文件
└── settings.txt            # 配置文件
```

## 许可证
本项目采用 MIT 许可证，详情请查阅 LICENSE 文件。 