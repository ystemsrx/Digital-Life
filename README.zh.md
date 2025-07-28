[English](README.md) | [简体中文](README.zh.md)

---

# 虚拟数字生命

由数学函数模拟的数字生命

## ✨ 养一个数字生命在桌面上

![鱼动画](assets/fish.gif)

## 🚀 快速开始

### 安装

1. 从[发布页面](https://github.com/ystemsrx/Digital-Life/releases)下载最新版本
2. 将文件解压到您想要的位置
3. 运行 `pet.exe` 启动应用程序
4. 右键点击系统托盘图标访问设置

### 从源码构建

```bash
git clone https://github.com/yourusername/desktop-pet-fish.git
cd desktop-pet-fish
dotnet build
dotnet run
```

## ⚙️ 配置

右键点击系统托盘图标并选择"设置"来访问设置窗口。

### 可用设置

| 设置项 | 描述 | 范围 |
|--------|------|------|
| **宠物速度** | 行进速度 | 0.1 - 2.0 |
| **旋转速度** | 改变方向的速度 | 0.001 - 0.01 |
| **漫游强度** | 运动模式的随机性 | 0.01 - 0.2 |
| **壁面排斥力** | 推离屏幕边缘的力度 | 100 - 5000 |
| **视觉质量** | 渲染的粒子数量 | 1000 - 10000 |
| **帧率** | 动画流畅度（FPS） | 30 - 120 |
| **透明度** | 透明度级别 | 0.1 - 1.0 |
| **颜色** | 颜色自定义 | RGB 颜色选择器 |

## 🎮 使用方法

- **启动**：运行应用程序 - 将置顶出现在您的桌面上，不会影响鼠标操作
- **设置**：右键点击系统托盘图标 → "设置"
- **退出**：右键点击系统托盘图标 → "退出"
- **开机自启**：在设置中启用随Windows启动

## 🛠️ 技术细节

- **框架**：WPF (.NET 8.0)
- **渲染**：使用 WriteableBitmap 进行高性能图形渲染
- **架构**：MVVM 模式，支持设置持久化
- **依赖项**：
  - Extended.Wpf.Toolkit
  - Newtonsoft.Json
  - System.Drawing.Common

## 📁 项目结构

```
pet/
├── MainWindow.xaml(.cs)     # 主应用程序窗口和鱼类逻辑
├── SettingsWindow.xaml(.cs) # 配置界面
├── SettingsManager.cs       # 设置持久化和管理
├── App.xaml(.cs)           # 应用程序入口点
└── pet.csproj              # 项目配置
```

## 🤝 贡献

欢迎贡献！请随时提交 Pull Request。

## 📝 许可证

本项目采用 MIT 许可证 - 详情请参阅 [LICENSE](LICENSE) 文件。

## 🐛 已知问题

- 高粒子数量可能会影响旧系统的性能

---

**享受您的新桌面伙伴！🐟✨**
