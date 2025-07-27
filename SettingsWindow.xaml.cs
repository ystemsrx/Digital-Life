using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;

namespace pet
{
    public partial class SettingsWindow : Window
    {
        // 默认值常量
        private const double DEFAULT_PET_SPEED = 0.3;
        private const double DEFAULT_ROTATION_SPEED = 0.001;
        private const double DEFAULT_WANDER_STRENGTH = 0.07;
        private const double DEFAULT_WALL_REPULSION_STRENGTH = 1000.0;
        private const int DEFAULT_NUM_POINTS = 2000;  // 恢复默认值
        private const int DEFAULT_TARGET_FPS = 90;    // 设置默认帧率为90
        private const double DEFAULT_OPACITY = 0.3;   // 默认透明度 30%
        private const byte DEFAULT_COLOR_R = 220;     // 默认颜色 R
        private const byte DEFAULT_COLOR_G = 220;     // 默认颜色 G
        private const byte DEFAULT_COLOR_B = 220;     // 默认颜色 B

        // 主窗口引用
        private MainWindow mainWindow;

        // 当前设置
        private AppSettings currentSettings;

        public SettingsWindow(MainWindow parent)
        {
            InitializeComponent();
            mainWindow = parent;

            // 加载当前设置
            currentSettings = SettingsManager.LoadSettings();

            // 延迟加载，确保所有控件都已初始化
            this.Loaded += SettingsWindow_Loaded;

            // 添加窗口拖拽功能
            this.MouseLeftButtonDown += SettingsWindow_MouseLeftButtonDown;
        }

        private void SettingsWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 允许拖拽窗口
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentValues();
        }

        private void LoadCurrentValues()
        {
            try
            {
                // 从设置对象加载值并设置到滑块
                if (SpeedSlider != null) SpeedSlider.Value = currentSettings.PetSpeed;
                if (RotationSlider != null) RotationSlider.Value = currentSettings.RotationSpeed;
                if (WanderSlider != null) WanderSlider.Value = currentSettings.WanderStrength;
                if (RepulsionSlider != null) RepulsionSlider.Value = currentSettings.WallRepulsionStrength;
                if (PointsSlider != null) PointsSlider.Value = currentSettings.NumPoints;
                if (FpsSlider != null) FpsSlider.Value = currentSettings.TargetFps;
                if (OpacitySlider != null) OpacitySlider.Value = currentSettings.Opacity;

                // 设置颜色选择器
                if (ColorPicker != null) ColorPicker.SelectedColor = System.Windows.Media.Color.FromRgb(
                    currentSettings.PetColor.R, currentSettings.PetColor.G, currentSettings.PetColor.B);

                // 设置开机自启动复选框
                if (StartupCheckBox != null) StartupCheckBox.IsChecked = currentSettings.StartWithWindows;

                // 设置自动性能优化复选框
                if (AutoOptimizeCheckBox != null) AutoOptimizeCheckBox.IsChecked = currentSettings.AutoPerformanceOptimization;

                // 更新显示值
                UpdateDisplayValues();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"加载设置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDisplayValues()
        {
            try
            {
                if (SpeedTextBox != null && SpeedSlider != null)
                    SpeedTextBox.Text = SpeedSlider.Value.ToString("F1");
                if (RotationValue != null && RotationSlider != null)
                    RotationValue.Text = RotationSlider.Value.ToString("F4");
                if (WanderValue != null && WanderSlider != null)
                    WanderValue.Text = WanderSlider.Value.ToString("F2");
                if (RepulsionValue != null && RepulsionSlider != null)
                    RepulsionValue.Text = RepulsionSlider.Value.ToString("F0");
                if (PointsTextBox != null && PointsSlider != null)
                    PointsTextBox.Text = PointsSlider.Value.ToString("F0");
                if (FpsTextBox != null && FpsSlider != null)
                    FpsTextBox.Text = FpsSlider.Value.ToString("F0");
                if (OpacityValue != null && OpacitySlider != null)
                    OpacityValue.Text = (OpacitySlider.Value * 100).ToString("F0") + "%";
            }
            catch (Exception ex)
            {
                // 忽略显示更新错误，避免程序崩溃
                System.Diagnostics.Debug.WriteLine($"更新显示值时出错: {ex.Message}");
            }
        }

        private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (sender is Slider slider)
                {
                    // 实时更新显示值
                    UpdateDisplayValues();

                    // 实时应用到宠物
                    ApplySettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"滑块值改变时出错: {ex.Message}");
            }
        }

        private void OnColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            try
            {
                // 实时应用颜色变化
                ApplySettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"颜色改变时出错: {ex.Message}");
            }
        }

        private void OnStartupCheckChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (StartupCheckBox != null)
                {
                    currentSettings.StartWithWindows = StartupCheckBox.IsChecked ?? false;
                    // 实时应用开机自启动设置
                    SettingsManager.SetStartupEnabled(currentSettings.StartWithWindows);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"设置开机自启动时出错: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                // 恢复复选框状态
                if (StartupCheckBox != null)
                {
                    StartupCheckBox.IsChecked = SettingsManager.IsStartupEnabled();
                }
            }
        }

        private void ApplySettings()
        {
            try
            {
                // 检查所有滑块是否存在
                if (SpeedSlider == null || RotationSlider == null || WanderSlider == null ||
                    RepulsionSlider == null || PointsSlider == null || FpsSlider == null || OpacitySlider == null)
                {
                    return; // 如果任何滑块不存在，直接返回
                }

                // 更新设置对象
                currentSettings.PetSpeed = SpeedSlider.Value;
                currentSettings.RotationSpeed = RotationSlider.Value;
                currentSettings.WanderStrength = WanderSlider.Value;
                currentSettings.WallRepulsionStrength = RepulsionSlider.Value;
                currentSettings.NumPoints = (int)PointsSlider.Value;
                currentSettings.TargetFps = (int)FpsSlider.Value;
                currentSettings.Opacity = OpacitySlider.Value;

                // 获取颜色
                var selectedColor = ColorPicker?.SelectedColor ?? System.Windows.Media.Color.FromRgb(DEFAULT_COLOR_R, DEFAULT_COLOR_G, DEFAULT_COLOR_B);
                currentSettings.PetColor = new PetColor { R = selectedColor.R, G = selectedColor.G, B = selectedColor.B };

                // 将设置应用到主窗口
                mainWindow.UpdatePetSettings(
                    petSpeed: currentSettings.PetSpeed,
                    rotationSpeed: currentSettings.RotationSpeed,
                    wanderStrength: currentSettings.WanderStrength,
                    wallRepulsionStrength: currentSettings.WallRepulsionStrength,
                    numPoints: currentSettings.NumPoints,
                    targetFps: currentSettings.TargetFps,
                    opacity: currentSettings.Opacity,
                    petColor: currentSettings.PetColor,
                    autoOptimize: currentSettings.AutoPerformanceOptimization
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用设置时出错: {ex.Message}");
            }
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建默认设置
                currentSettings = new AppSettings();

                // 恢复默认值
                if (SpeedSlider != null) SpeedSlider.Value = DEFAULT_PET_SPEED;
                if (RotationSlider != null) RotationSlider.Value = DEFAULT_ROTATION_SPEED;
                if (WanderSlider != null) WanderSlider.Value = DEFAULT_WANDER_STRENGTH;
                if (RepulsionSlider != null) RepulsionSlider.Value = DEFAULT_WALL_REPULSION_STRENGTH;
                if (PointsSlider != null) PointsSlider.Value = DEFAULT_NUM_POINTS;
                if (FpsSlider != null) FpsSlider.Value = DEFAULT_TARGET_FPS;
                if (OpacitySlider != null) OpacitySlider.Value = DEFAULT_OPACITY;
                if (ColorPicker != null) ColorPicker.SelectedColor = System.Windows.Media.Color.FromRgb(DEFAULT_COLOR_R, DEFAULT_COLOR_G, DEFAULT_COLOR_B);
                if (StartupCheckBox != null) StartupCheckBox.IsChecked = false;
                if (AutoOptimizeCheckBox != null) AutoOptimizeCheckBox.IsChecked = false;

                // 更新显示值
                UpdateDisplayValues();

                // 应用设置
                ApplySettings();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"重置设置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存设置
                SettingsManager.SaveSettings(currentSettings);
                this.Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"保存设置时出错: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 通知主窗口设置窗口已关闭
            mainWindow.OnSettingsWindowClosed();
            base.OnClosed(e);
        }

        // 文本框值变化处理
        private void OnTextBoxValueChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                try
                {
                    if (textBox.Name == "SpeedTextBox" && double.TryParse(textBox.Text, out double speedValue))
                    {
                        if (speedValue >= 0.1 && speedValue <= 2.0 && SpeedSlider != null)
                        {
                            SpeedSlider.Value = speedValue;
                        }
                    }
                    else if (textBox.Name == "PointsTextBox" && int.TryParse(textBox.Text, out int pointsValue))
                    {
                        if (pointsValue >= 2000 && pointsValue <= 20000 && PointsSlider != null)
                        {
                            PointsSlider.Value = pointsValue;
                        }
                    }
                    else if (textBox.Name == "FpsTextBox" && int.TryParse(textBox.Text, out int fpsValue))
                    {
                        if (fpsValue >= 30 && fpsValue <= 120 && FpsSlider != null)
                        {
                            FpsSlider.Value = fpsValue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"文本框值变化处理错误: {ex.Message}");
                }
            }
        }

        // 自动性能优化复选框变化处理
        private void OnAutoOptimizeCheckChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is CheckBox checkBox)
                {
                    currentSettings.AutoPerformanceOptimization = checkBox.IsChecked ?? false;
                    ApplySettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"自动优化设置变化处理错误: {ex.Message}");
            }
        }
    }
}