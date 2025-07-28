using System;
using System.IO;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace pet
{
    /// <summary>
    /// 宠物颜色结构
    /// </summary>
    public struct PetColor
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
    }

    /// <summary>
    /// 应用设置类
    /// </summary>
    public class AppSettings
    {
        // 运动参数
        public double PetSpeed { get; set; } = 0.3;
        public double RotationSpeed { get; set; } = 0.001;
        public double WanderStrength { get; set; } = 0.07;
        public double WallRepulsionStrength { get; set; } = 1000.0;
        public int NumPoints { get; set; } = 2000;  // 恢复默认值
        public int TargetFps { get; set; } = 90;    // 设置默认帧率为90
        public double Opacity { get; set; } = 0.3;
        public PetColor PetColor { get; set; } = new PetColor { R = 220, G = 220, B = 220 };

        // 开机自启动设置
        public bool StartWithWindows { get; set; } = false;

        // 自动性能优化设置
        public bool AutoPerformanceOptimization { get; set; } = false;
    }

    /// <summary>
    /// 设置管理器 - 负责设置的保存、加载和开机自启动管理
    /// </summary>
    public static class SettingsManager
    {
        private const string APP_NAME = "DesktopPetFish";
        private const string SETTINGS_FILE_NAME = "settings.json";
        private const string REGISTRY_KEY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        
        // 使用 ProgramData 目录，所有用户都可以访问
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
            APP_NAME);
        
        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, SETTINGS_FILE_NAME);

        /// <summary>
        /// 保存设置到文件
        /// </summary>
        /// <param name="settings">要保存的设置</param>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                // 确保目录存在
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                // 序列化设置为JSON
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                
                // 写入文件
                File.WriteAllText(SettingsFilePath, json);
                
                // 更新开机自启动设置
                UpdateStartupRegistry(settings.StartWithWindows);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
                // 可以选择显示错误消息给用户
                System.Windows.MessageBox.Show($"保存设置失败: {ex.Message}", "错误", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 从文件加载设置
        /// </summary>
        /// <returns>加载的设置，如果失败则返回默认设置</returns>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);

                    if (settings != null)
                    {
                        // 验证开机自启动状态与注册表是否一致
                        settings.StartWithWindows = IsStartupEnabled();
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
            }

            // 返回默认设置
            var defaultSettings = new AppSettings();
            defaultSettings.StartWithWindows = IsStartupEnabled();
            return defaultSettings;
        }

        /// <summary>
        /// 更新注册表中的开机自启动设置
        /// </summary>
        /// <param name="enable">是否启用开机自启动</param>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static void UpdateStartupRegistry(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            // 获取当前执行文件的路径
                            // 对于 .NET 8.0 和单文件发布，直接使用 AppContext.BaseDirectory
                            string exePath = Path.Combine(System.AppContext.BaseDirectory, System.AppDomain.CurrentDomain.FriendlyName);
                            if (!exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                exePath += ".exe";
                            }

                            // 如果是 .dll 文件（.NET Core/5+），需要使用 dotnet 运行
                            if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            {
                                // 查找对应的 .exe 文件
                                string exeFile = Path.ChangeExtension(exePath, ".exe");
                                if (File.Exists(exeFile))
                                {
                                    exePath = exeFile;
                                }
                                else
                                {
                                    // 如果没有 .exe 文件，使用 dotnet 命令
                                    exePath = $"dotnet \"{exePath}\"";
                                }
                            }

                            key.SetValue(APP_NAME, exePath);
                        }
                        else
                        {
                            // 删除注册表项
                            if (key.GetValue(APP_NAME) != null)
                            {
                                key.DeleteValue(APP_NAME);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新开机自启动设置失败: {ex.Message}");
                throw new InvalidOperationException($"无法更新开机自启动设置: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否已启用开机自启动
        /// </summary>
        /// <returns>如果启用了开机自启动则返回 true</returns>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, false))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(APP_NAME);
                        return value != null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查开机自启动状态失败: {ex.Message}");
            }
            
            return false;
        }

        /// <summary>
        /// 设置开机自启动
        /// </summary>
        /// <param name="enable">是否启用</param>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static void SetStartupEnabled(bool enable)
        {
            try
            {
                UpdateStartupRegistry(enable);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"设置开机自启动失败: {ex.Message}", "错误", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取设置文件路径（用于调试）
        /// </summary>
        /// <returns>设置文件的完整路径</returns>
        public static string GetSettingsFilePath()
        {
            return SettingsFilePath;
        }
    }
}
