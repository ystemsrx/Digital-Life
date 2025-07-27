using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Forms;
using System.Drawing;

namespace pet
{

    /// <summary>
    /// Desktop Pet Fish - High Performance Version with WriteableBitmap
    /// </summary>
    public partial class MainWindow : Window
    {
        // Win32 API constants for click-through behavior
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        // System tray components
        private NotifyIcon? notifyIcon;
        private ContextMenuStrip? contextMenu;

        // Win32 API imports
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        // Pet configuration parameters (now mutable for settings)
        private int numPoints = 5000;  // 调整默认值到最低
        private int targetFps = 120;   // 调整默认值到最高
        private double petSpeed = 0.3;
        private double rotationSpeed = 0.001;
        private double wanderStrength = 0.07;
        private double wallRepulsionStrength = 1000.0;
        private double opacity = 0.8;  // 新增透明度参数
        private PetColor petColor = new PetColor { R = 220, G = 220, B = 220 };  // 新增颜色参数
        private const double BUFFER_DISTANCE = 200.0;
        private const double MARGIN = 10.0;

        // 应用设置
        private AppSettings appSettings = null!;

        // Settings window
        private SettingsWindow? settingsWindow;

        // Pet state variables
        private double petX;
        private double petY;
        private double petAngle;
        private double petOrientationAngle;
        private double t = 0;
        private double tStep = Math.PI / 240.0;

        // Pre-calculated arrays for performance
        private double[] xArray = null!;
        private double[] yArray = null!;

        // High-performance WPF rendering using DrawingVisual
        private DrawingVisual drawingVisual = null!;
        private RenderTargetBitmap renderBitmap = null!;

        // Pre-calculated points for batch drawing
        private System.Windows.Point[] fishPoints = null!;
        private int validPointCount = 0;

        // Random generator
        private Random random = null!;

        // Screen dimensions
        private int screenWidth;
        private int screenHeight;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            InitializePet();
            InitializeSystemTray();
        }

        private void LoadSettings()
        {
            try
            {
                // 加载设置
                appSettings = SettingsManager.LoadSettings();

                // 应用设置到宠物参数
                petSpeed = appSettings.PetSpeed;
                rotationSpeed = appSettings.RotationSpeed;
                wanderStrength = appSettings.WanderStrength;
                wallRepulsionStrength = appSettings.WallRepulsionStrength;
                numPoints = appSettings.NumPoints;
                targetFps = appSettings.TargetFps;
                opacity = appSettings.Opacity;
                petColor = appSettings.PetColor;

                // 更新目标帧时间
                targetFrameTime = TimeSpan.FromMilliseconds(1000.0 / targetFps);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
                // 使用默认设置
                appSettings = new AppSettings();
            }
        }

        private void InitializePet()
        {
            // Get screen dimensions
            screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            // Initialize pet position and angle
            petX = screenWidth / 2.0;
            petY = screenHeight / 2.0;
            random = new Random();
            petAngle = random.NextDouble() * 2 * Math.PI;
            petOrientationAngle = petAngle;

            // Pre-calculate arrays for fish shape
            InitializeFishArrays();

            // Initialize high-performance WPF rendering
            drawingVisual = new DrawingVisual();
            renderBitmap = new RenderTargetBitmap(screenWidth, screenHeight, 96, 96, PixelFormats.Pbgra32);
            fishPoints = new System.Windows.Point[numPoints];

            PetImage.Source = renderBitmap;

            // Use CompositionTarget.Rendering for better performance
            CompositionTarget.Rendering += OnRendering;
        }

        private void InitializeSystemTray()
        {
            // Create context menu
            contextMenu = new ContextMenuStrip();
            var settingsMenuItem = new ToolStripMenuItem("设置", null, OnSettingsClick);
            var exitMenuItem = new ToolStripMenuItem("退出", null, OnExitClick);
            contextMenu.Items.Add(settingsMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitMenuItem);

            // Create notify icon
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = CreateTrayIcon();
            notifyIcon.Text = "Desktop Pet Fish";
            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.Visible = true;

            // Handle window state changes
            this.StateChanged += MainWindow_StateChanged;
        }

        private Icon CreateTrayIcon()
        {
            // Create a simple icon for the system tray
            var bitmap = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(System.Drawing.Color.Transparent);
                graphics.FillEllipse(System.Drawing.Brushes.LightBlue, 2, 2, 12, 12);
                graphics.DrawEllipse(System.Drawing.Pens.DarkBlue, 2, 2, 12, 12);
            }
            return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            // Hide window when minimized
            if (this.WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            // Clean up and exit
            notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Make window click-through
            MakeWindowClickThrough();

            Console.WriteLine("Desktop Pet Fish started!");
            Console.WriteLine("Right-click the tray icon to exit.");
        }

        private void MakeWindowClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }

        private DateTime lastRenderTime = DateTime.MinValue;
        private TimeSpan targetFrameTime = TimeSpan.FromMilliseconds(1000.0 / 80);

        private void OnRendering(object? sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            if (now - lastRenderTime >= targetFrameTime)
            {
                UpdatePetState();
                DrawPetOptimized();
                lastRenderTime = now;
            }
        }

        private void UpdatePetState()
        {
            // Add random wandering to pet angle
            petAngle += (random.NextDouble() - 0.5) * 2 * wanderStrength;

            // Move pet based on orientation angle
            petX += petSpeed * Math.Cos(petOrientationAngle);
            petY += petSpeed * Math.Sin(petOrientationAngle);

            // Check boundaries and apply repulsion forces
            CheckBounds();

            // Smoothly rotate towards target angle
            double angleDiff = petAngle - petOrientationAngle;
            // Normalize angle difference to [-π, π]
            angleDiff = ((angleDiff + Math.PI) % (2 * Math.PI)) - Math.PI;
            petOrientationAngle += angleDiff * rotationSpeed;

            // Update animation time
            t += tStep;
        }

        private void CheckBounds()
        {
            // Calculate distances to boundaries
            double distLeft = petX;
            double distRight = screenWidth - petX;
            double distTop = petY;
            double distBottom = screenHeight - petY;

            // Calculate repulsion forces using exponential function
            double repulsionX = 0;
            double repulsionY = 0;

            // Left boundary repulsion with exponential growth
            if (distLeft < BUFFER_DISTANCE)
            {
                double normalizedDist = Math.Max(0.01, distLeft / BUFFER_DISTANCE); // Prevent division by zero
                double force = CalculateExponentialRepulsion(normalizedDist) * wallRepulsionStrength;
                repulsionX += force;
            }

            // Right boundary repulsion with exponential growth
            if (distRight < BUFFER_DISTANCE)
            {
                double normalizedDist = Math.Max(0.01, distRight / BUFFER_DISTANCE); // Prevent division by zero
                double force = CalculateExponentialRepulsion(normalizedDist) * wallRepulsionStrength;
                repulsionX -= force;
            }

            // Top boundary repulsion with exponential growth
            if (distTop < BUFFER_DISTANCE)
            {
                double normalizedDist = Math.Max(0.01, distTop / BUFFER_DISTANCE); // Prevent division by zero
                double force = CalculateExponentialRepulsion(normalizedDist) * wallRepulsionStrength;
                repulsionY += force;
            }

            // Bottom boundary repulsion with exponential growth
            if (distBottom < BUFFER_DISTANCE)
            {
                double normalizedDist = Math.Max(0.01, distBottom / BUFFER_DISTANCE); // Prevent division by zero
                double force = CalculateExponentialRepulsion(normalizedDist) * wallRepulsionStrength;
                repulsionY -= force;
            }

            // Blend repulsion forces with current direction using adaptive blending
            if (repulsionX != 0 || repulsionY != 0)
            {
                double currentDx = Math.Cos(petAngle);
                double currentDy = Math.Sin(petAngle);

                // Calculate repulsion magnitude for adaptive blending
                double repulsionMagnitude = Math.Sqrt(repulsionX * repulsionX + repulsionY * repulsionY);
                double normalizedRepulsion = Math.Min(1.0, repulsionMagnitude / wallRepulsionStrength);

                // Adaptive blend factor: stronger repulsion = more aggressive turning
                double blendFactor = 0.05 + normalizedRepulsion * 0.15; // Range: 0.05 to 0.2

                double newDx = currentDx + repulsionX * blendFactor;
                double newDy = currentDy + repulsionY * blendFactor;

                petAngle = Math.Atan2(newDy, newDx);
            }

            // Soft boundary constraints with exponential pushback near edges
            double softMargin = MARGIN * 3; // Larger soft margin for smoother behavior

            if (petX < softMargin)
            {
                double pushback = (softMargin - petX) / softMargin;
                petX += pushback * pushback * petSpeed * 2; // Quadratic pushback
            }
            else if (petX > screenWidth - softMargin)
            {
                double pushback = (petX - (screenWidth - softMargin)) / softMargin;
                petX -= pushback * pushback * petSpeed * 2; // Quadratic pushback
            }

            if (petY < softMargin)
            {
                double pushback = (softMargin - petY) / softMargin;
                petY += pushback * pushback * petSpeed * 2; // Quadratic pushback
            }
            else if (petY > screenHeight - softMargin)
            {
                double pushback = (petY - (screenHeight - softMargin)) / softMargin;
                petY -= pushback * pushback * petSpeed * 2; // Quadratic pushback
            }

            // Hard boundary constraints as final safety net
            petX = Math.Max(MARGIN, Math.Min(screenWidth - MARGIN, petX));
            petY = Math.Max(MARGIN, Math.Min(screenHeight - MARGIN, petY));
        }

        /// <summary>
        /// Calculate exponential repulsion force based on normalized distance to boundary
        /// </summary>
        /// <param name="normalizedDistance">Distance to boundary normalized to [0,1], where 0 is at boundary and 1 is at buffer distance</param>
        /// <returns>Repulsion force multiplier in range [0,1]</returns>
        private double CalculateExponentialRepulsion(double normalizedDistance)
        {
            // Exponential decay function: force = e^(-k * distance)
            // When distance = 0 (at boundary), force = 1 (maximum)
            // When distance = 1 (at buffer), force ≈ 0.05 (minimum)
            double k = 3.0; // Controls steepness of exponential curve
            double force = Math.Exp(-k * normalizedDistance);

            // Ensure force is in valid range [0, 1]
            return Math.Max(0.0, Math.Min(1.0, force));
        }

        private void DrawPetOptimized()
        {
            // Pre-calculate common values
            double cosO = Math.Cos(petOrientationAngle - Math.PI / 2);
            double sinO = Math.Sin(petOrientationAngle - Math.PI / 2);

            // Calculate fish shape points
            validPointCount = 0;
            for (int i = 0; i < numPoints; i++)
            {
                double x = xArray[i];
                double y = yArray[i];

                // Complex mathematical formula for fish shape
                double k = (4 + Math.Sin(y * 2 - t) * 3) * Math.Cos(x / 29);
                double e = y / 8 - 13;
                double d = Math.Sqrt(k * k + e * e);
                double q = 3 * Math.Sin(k * 2) + 0.3 / (k + double.Epsilon) +
                          Math.Sin(y / 25) * k * (9 + 4 * Math.Sin(e * 9 - d * 3 + t * 2));
                double c = d - t;
                double localU = q + 30 * Math.Cos(c) + 200;
                double localV = q * Math.Sin(c) + 39 * d - 220;

                // Center and rotate
                double centeredU = localU - 200;
                double centeredV = -localV + 220;
                double rotatedU = centeredU * cosO - centeredV * sinO;
                double rotatedV = centeredU * sinO + centeredV * cosO;

                // Transform to screen coordinates
                double screenU = rotatedU + petX;
                double screenV = rotatedV + petY;

                // Store valid points
                if (screenU >= 0 && screenU < screenWidth && screenV >= 0 && screenV < screenHeight)
                {
                    fishPoints[validPointCount] = new System.Windows.Point(screenU, screenV);
                    validPointCount++;
                }
            }

            // Draw using WPF DrawingVisual
            using (DrawingContext dc = drawingVisual.RenderOpen())
            {
                // Clear with transparent background
                dc.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, new Rect(0, 0, screenWidth, screenHeight));

                // Draw fish points efficiently with dynamic color and opacity
                var color = System.Windows.Media.Color.FromRgb(petColor.R, petColor.G, petColor.B);
                color.A = (byte)(opacity * 255); // Apply opacity
                var brush = new SolidColorBrush(color);
                brush.Freeze(); // Improve performance

                for (int i = 0; i < validPointCount; i++)
                {
                    dc.DrawRectangle(brush, null, new Rect(fishPoints[i].X, fishPoints[i].Y, 1, 1));
                }
            }

            // Render to bitmap
            renderBitmap.Clear();
            renderBitmap.Render(drawingVisual);
        }

        private void InitializeFishArrays()
        {
            xArray = new double[numPoints];
            yArray = new double[numPoints];

            for (int i = 0; i < numPoints; i++)
            {
                xArray[i] = numPoints - i;
                yArray[i] = (numPoints - i) / 235.0;
            }
        }

        private void OnSettingsClick(object? sender, EventArgs e)
        {
            if (settingsWindow == null || !settingsWindow.IsVisible)
            {
                settingsWindow = new SettingsWindow(this);
                settingsWindow.Show();
            }
            else
            {
                settingsWindow.Activate();
            }
        }

        public void OnSettingsWindowClosed()
        {
            settingsWindow = null;
        }

        // Getter methods for settings window
        public double GetPetSpeed() => petSpeed;
        public double GetRotationSpeed() => rotationSpeed;
        public double GetWanderStrength() => wanderStrength;
        public double GetWallRepulsionStrength() => wallRepulsionStrength;
        public int GetNumPoints() => numPoints;
        public int GetTargetFps() => targetFps;
        public double GetOpacity() => opacity;
        public PetColor GetPetColor() => petColor;

        public void UpdatePetSettings(double petSpeed, double rotationSpeed, double wanderStrength,
                                     double wallRepulsionStrength, int numPoints, int targetFps,
                                     double opacity, PetColor petColor)
        {
            this.petSpeed = petSpeed;
            this.rotationSpeed = rotationSpeed;
            this.wanderStrength = wanderStrength;
            this.wallRepulsionStrength = wallRepulsionStrength;
            this.opacity = opacity;
            this.petColor = petColor;

            // Update FPS
            this.targetFps = targetFps;
            this.targetFrameTime = TimeSpan.FromMilliseconds(1000.0 / targetFps);

            // Update points if changed
            if (this.numPoints != numPoints)
            {
                this.numPoints = numPoints;
                InitializeFishArrays();
                fishPoints = new System.Windows.Point[numPoints];
            }

            // 同时更新设置对象
            if (appSettings != null)
            {
                appSettings.PetSpeed = petSpeed;
                appSettings.RotationSpeed = rotationSpeed;
                appSettings.WanderStrength = wanderStrength;
                appSettings.WallRepulsionStrength = wallRepulsionStrength;
                appSettings.NumPoints = numPoints;
                appSettings.TargetFps = targetFps;
                appSettings.Opacity = opacity;
                appSettings.PetColor = petColor;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;

            // Close settings window if open
            settingsWindow?.Close();

            // 保存设置
            try
            {
                if (appSettings != null)
                {
                    SettingsManager.SaveSettings(appSettings);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }

            // Clean up system tray resources
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            contextMenu?.Dispose();

            base.OnClosed(e);
        }
    }
}