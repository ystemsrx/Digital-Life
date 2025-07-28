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
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;

        // Win32 API imports
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        // Pet configuration parameters (now mutable for settings)
        private int numPoints = 2000;  // 恢复默认值
        private int targetFps = 90;    // 设置默认帧率为90
        private double petSpeed = 0.3;
        private double rotationSpeed = 0.001;
        private double wanderStrength = 0.07;
        private double wallRepulsionStrength = 1000.0;
        private double opacity = 0.3;  // 新增透明度参数
        private PetColor petColor = new PetColor { R = 220, G = 220, B = 220 };  // 新增颜色参数
        private const double BUFFER_DISTANCE = 200.0;
        private const double MARGIN = 10.0;

        // 应用设置
        private AppSettings appSettings;

        // Settings window
        private SettingsWindow settingsWindow;

        // Pet state variables
        private double petX;
        private double petY;
        private double petAngle;
        private double petOrientationAngle;
        private double t = 0;
        private double tStep = Math.PI / 240.0;

        // Pre-calculated arrays for performance
        private double[] xArray;
        private double[] yArray;

        // 优化的渲染系统
        private WriteableBitmap writeableBitmap;
        private byte[] pixelBuffer;
        private int stride;
        private Int32Rect dirtyRect;

        // Pre-calculated points for batch drawing
        private System.Windows.Point[] fishPoints;
        private int validPointCount = 0;

        // 性能监控
        private int frameCount = 0;
        private DateTime lastFpsUpdate = DateTime.Now;
        private double currentFps = 0;

        // Random generator
        private Random random;

        // Screen dimensions
        private int screenWidth;
        private int screenHeight;

        // 渲染优化标志
        private bool needsFullRedraw = true;
        private double lastPetX, lastPetY;

        // 性能模式设置
        private bool useRegionalUpdate = false; // 默认关闭区域更新，避免轨迹问题
        private bool autoPerformanceOptimization = false; // 默认关闭自动性能优化

        [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            InitializePet();
            InitializeSystemTray();
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
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
                autoPerformanceOptimization = appSettings.AutoPerformanceOptimization;

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
            lastPetX = petX;
            lastPetY = petY;
            random = new Random();
            petAngle = random.NextDouble() * 2 * Math.PI;
            petOrientationAngle = petAngle;

            // Pre-calculate arrays for fish shape
            InitializeFishArrays();

            // Initialize optimized WriteableBitmap rendering
            InitializeOptimizedRendering();

            // Use CompositionTarget.Rendering for better performance
            CompositionTarget.Rendering += OnRendering;
        }

        private void InitializeOptimizedRendering()
        {
            // 使用WriteableBitmap替代RenderTargetBitmap以获得更好的性能
            writeableBitmap = new WriteableBitmap(screenWidth, screenHeight, 96, 96, PixelFormats.Bgra32, null);
            PetImage.Source = writeableBitmap;

            // 计算stride和初始化像素缓冲区
            stride = writeableBitmap.PixelWidth * (writeableBitmap.Format.BitsPerPixel / 8);
            pixelBuffer = new byte[stride * writeableBitmap.PixelHeight];

            // 初始化脏矩形
            dirtyRect = new Int32Rect(0, 0, screenWidth, screenHeight);

            // 预分配点数组
            fishPoints = new System.Windows.Point[numPoints];
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
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

        [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
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

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            // Hide window when minimized
            if (this.WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            // Clean up and exit
            if (notifyIcon != null)
                notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Make window click-through
            MakeWindowClickThrough();

            Console.WriteLine("Desktop Pet Fish started!");
            Console.WriteLine("Right-click the tray icon to exit.");
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private void MakeWindowClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }

        private DateTime lastRenderTime = DateTime.MinValue;
        private TimeSpan targetFrameTime = TimeSpan.FromMilliseconds(1000.0 / 90);

        private void OnRendering(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            if (now - lastRenderTime >= targetFrameTime)
            {
                UpdatePetState();

                // 使用完全清除模式确保没有轨迹残留
                DrawPetOptimizedV2();

                UpdateFpsCounter();
                lastRenderTime = now;
            }
        }

        private DateTime lastAutoOptimize = DateTime.MinValue;

        private void UpdateFpsCounter()
        {
            frameCount++;
            var elapsed = DateTime.Now - lastFpsUpdate;
            if (elapsed.TotalSeconds >= 1.0)
            {
                currentFps = frameCount / elapsed.TotalSeconds;
                frameCount = 0;
                lastFpsUpdate = DateTime.Now;

                // 只有在启用自动性能优化时才检查
                if (autoPerformanceOptimization && (DateTime.Now - lastAutoOptimize).TotalSeconds >= 5.0)
                {
                    AutoOptimizePerformance();
                    lastAutoOptimize = DateTime.Now;
                }

                // 可选：在调试时输出FPS信息
                #if DEBUG
                System.Diagnostics.Debug.WriteLine($"FPS: {currentFps:F1}, Points: {validPointCount}");
                #endif
            }
        }

        private void UpdatePetState()
        {
            // 保存上一帧位置用于优化渲染
            lastPetX = petX;
            lastPetY = petY;

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

            // 检查是否需要完全重绘（位置变化较大时）
            double distanceMoved = Math.Sqrt((petX - lastPetX) * (petX - lastPetX) + (petY - lastPetY) * (petY - lastPetY));
            needsFullRedraw = distanceMoved > 5.0; // 如果移动距离超过5像素则完全重绘
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

        private void DrawPetOptimizedV2()
        {
            // 完全清除WriteableBitmap，确保没有任何残留
            writeableBitmap.Lock();
            try
            {
                // 使用unsafe代码直接清除后备缓冲区
                unsafe
                {
                    IntPtr backBuffer = writeableBitmap.BackBuffer;
                    int bufferSize = writeableBitmap.PixelHeight * writeableBitmap.BackBufferStride;

                    // 快速清零整个缓冲区
                    byte* ptr = (byte*)backBuffer.ToPointer();
                    for (int i = 0; i < bufferSize; i++)
                    {
                        ptr[i] = 0;
                    }
                }

                // Pre-calculate common values
                double cosO = Math.Cos(petOrientationAngle - Math.PI / 2);
                double sinO = Math.Sin(petOrientationAngle - Math.PI / 2);

                // Calculate fish shape points with optimized math
                validPointCount = 0;
                int minX = screenWidth, maxX = 0, minY = screenHeight, maxY = 0;

                // 预计算常用值以减少重复计算
                double t2 = t * 2;

                for (int i = 0; i < numPoints; i++)
                {
                    double x = xArray[i];
                    double y = yArray[i];

                    // 优化的数学公式计算
                    double y2MinusT = y * 2 - t;
                    double sinY2MinusT = Math.Sin(y2MinusT);
                    double k = (4 + sinY2MinusT * 3) * Math.Cos(x / 29);
                    double e = y / 8 - 13;
                    double d = Math.Sqrt(k * k + e * e);

                    // 简化复杂计算
                    double k2 = k * 2;
                    double sinK2 = Math.Sin(k2);
                    double yDiv25 = y / 25;
                    double sinYDiv25 = Math.Sin(yDiv25);
                    double e9MinusD3PlusT2 = e * 9 - d * 3 + t2;
                    double sinE9 = Math.Sin(e9MinusD3PlusT2);

                    double q = 3 * sinK2 + 0.3 / (k + double.Epsilon) + sinYDiv25 * k * (9 + 4 * sinE9);
                    double c = d - t;
                    double localU = q + 30 * Math.Cos(c) + 200;
                    double localV = q * Math.Sin(c) + 39 * d - 220;

                    // Center and rotate
                    double centeredU = localU - 200;
                    double centeredV = -localV + 220;
                    double rotatedU = centeredU * cosO - centeredV * sinO;
                    double rotatedV = centeredU * sinO + centeredV * cosO;

                    // Transform to screen coordinates
                    int screenU = (int)(rotatedU + petX);
                    int screenV = (int)(rotatedV + petY);

                    // Store valid points and track bounding box
                    if (screenU >= 0 && screenU < screenWidth && screenV >= 0 && screenV < screenHeight)
                    {
                        fishPoints[validPointCount] = new System.Windows.Point(screenU, screenV);
                        validPointCount++;

                        // 更新边界框
                        if (screenU < minX) minX = screenU;
                        if (screenU > maxX) maxX = screenU;
                        if (screenV < minY) minY = screenV;
                        if (screenV > maxY) maxY = screenV;
                    }
                }

                // 直接写入后备缓冲区
                DrawPointsToBackBuffer();

                // 标记整个区域为脏区域
                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, screenWidth, screenHeight));
            }
            finally
            {
                writeableBitmap.Unlock();
            }
        }

        private void DrawPointsToBackBuffer()
        {
            // 预计算颜色值
            byte colorB = petColor.B;
            byte colorG = petColor.G;
            byte colorR = petColor.R;
            byte colorA = (byte)(opacity * 255);

            // 直接写入WriteableBitmap的后备缓冲区
            unsafe
            {
                IntPtr backBuffer = writeableBitmap.BackBuffer;
                int backBufferStride = writeableBitmap.BackBufferStride;

                for (int i = 0; i < validPointCount; i++)
                {
                    int x = (int)fishPoints[i].X;
                    int y = (int)fishPoints[i].Y;

                    if (x >= 0 && x < screenWidth && y >= 0 && y < screenHeight)
                    {
                        // 计算像素位置
                        IntPtr pixelPtr = backBuffer + y * backBufferStride + x * 4;
                        byte* pixel = (byte*)pixelPtr.ToPointer();

                        // 写入BGRA值
                        pixel[0] = colorB; // Blue
                        pixel[1] = colorG; // Green
                        pixel[2] = colorR; // Red
                        pixel[3] = colorA; // Alpha
                    }
                }
            }
        }



        // 新增：区域更新渲染方法，只更新鱼所在的区域
        private void DrawPetWithRegionalUpdate()
        {
            // 计算更大的清除区域以确保完全清除轨迹
            int fishSize = 400; // 增大清除区域
            int clearX = Math.Max(0, (int)lastPetX - fishSize);
            int clearY = Math.Max(0, (int)lastPetY - fishSize);
            int clearWidth = Math.Min(screenWidth - clearX, fishSize * 2);
            int clearHeight = Math.Min(screenHeight - clearY, fishSize * 2);

            // 清除上一帧鱼所在的区域
            ClearRegion(clearX, clearY, clearWidth, clearHeight);

            // 同时清除当前鱼位置的区域（预防性清除）
            int currentClearX = Math.Max(0, (int)petX - fishSize);
            int currentClearY = Math.Max(0, (int)petY - fishSize);
            int currentClearWidth = Math.Min(screenWidth - currentClearX, fishSize * 2);
            int currentClearHeight = Math.Min(screenHeight - currentClearY, fishSize * 2);

            if (currentClearX != clearX || currentClearY != clearY)
            {
                ClearRegion(currentClearX, currentClearY, currentClearWidth, currentClearHeight);
            }

            // Pre-calculate common values
            double cosO = Math.Cos(petOrientationAngle - Math.PI / 2);
            double sinO = Math.Sin(petOrientationAngle - Math.PI / 2);

            // Calculate fish shape points
            validPointCount = 0;
            int minX = screenWidth, maxX = 0, minY = screenHeight, maxY = 0;

            double t2 = t * 2;

            for (int i = 0; i < numPoints; i++)
            {
                double x = xArray[i];
                double y = yArray[i];

                // 数学公式计算（与之前相同）
                double y2MinusT = y * 2 - t;
                double sinY2MinusT = Math.Sin(y2MinusT);
                double k = (4 + sinY2MinusT * 3) * Math.Cos(x / 29);
                double e = y / 8 - 13;
                double d = Math.Sqrt(k * k + e * e);

                double k2 = k * 2;
                double sinK2 = Math.Sin(k2);
                double yDiv25 = y / 25;
                double sinYDiv25 = Math.Sin(yDiv25);
                double e9MinusD3PlusT2 = e * 9 - d * 3 + t2;
                double sinE9 = Math.Sin(e9MinusD3PlusT2);

                double q = 3 * sinK2 + 0.3 / (k + double.Epsilon) + sinYDiv25 * k * (9 + 4 * sinE9);
                double c = d - t;
                double localU = q + 30 * Math.Cos(c) + 200;
                double localV = q * Math.Sin(c) + 39 * d - 220;

                double centeredU = localU - 200;
                double centeredV = -localV + 220;
                double rotatedU = centeredU * cosO - centeredV * sinO;
                double rotatedV = centeredU * sinO + centeredV * cosO;

                int screenU = (int)(rotatedU + petX);
                int screenV = (int)(rotatedV + petY);

                if (screenU >= 0 && screenU < screenWidth && screenV >= 0 && screenV < screenHeight)
                {
                    fishPoints[validPointCount] = new System.Windows.Point(screenU, screenV);
                    validPointCount++;

                    if (screenU < minX) minX = screenU;
                    if (screenU > maxX) maxX = screenU;
                    if (screenV < minY) minY = screenV;
                    if (screenV > maxY) maxY = screenV;
                }
            }

            // 绘制新的鱼到像素缓冲区
            DrawPointsToPixelBuffer();

            // 更新更大的区域以确保完全清除轨迹
            if (validPointCount > 0)
            {
                // 计算需要更新的总区域（包括清除区域和新绘制区域）
                int updateX = Math.Max(0, Math.Min(clearX, minX - 20));
                int updateY = Math.Max(0, Math.Min(clearY, minY - 20));
                int updateWidth = Math.Min(screenWidth - updateX,
                    Math.Max(clearX + clearWidth, maxX + 20) - updateX);
                int updateHeight = Math.Min(screenHeight - updateY,
                    Math.Max(clearY + clearHeight, maxY + 20) - updateY);

                // 确保更新区域不会超出屏幕边界
                updateWidth = Math.Max(0, Math.Min(updateWidth, screenWidth - updateX));
                updateHeight = Math.Max(0, Math.Min(updateHeight, screenHeight - updateY));

                if (updateWidth > 0 && updateHeight > 0)
                {
                    var updateRect = new Int32Rect(updateX, updateY, updateWidth, updateHeight);
                    writeableBitmap.WritePixels(updateRect, pixelBuffer, stride,
                        updateX * 4 + updateY * stride);
                }
            }
            else
            {
                // 如果没有有效点，至少更新清除区域
                if (clearWidth > 0 && clearHeight > 0)
                {
                    var clearRect = new Int32Rect(clearX, clearY, clearWidth, clearHeight);
                    writeableBitmap.WritePixels(clearRect, pixelBuffer, stride,
                        clearX * 4 + clearY * stride);
                }
            }
        }

        private void DrawPointsToPixelBuffer()
        {
            // 预计算颜色值
            byte colorB = petColor.B;
            byte colorG = petColor.G;
            byte colorR = petColor.R;
            byte colorA = (byte)(opacity * 255);

            // 直接写入像素缓冲区
            for (int i = 0; i < validPointCount; i++)
            {
                int x = (int)fishPoints[i].X;
                int y = (int)fishPoints[i].Y;

                if (x >= 0 && x < screenWidth && y >= 0 && y < screenHeight)
                {
                    int pixelIndex = y * stride + x * 4; // BGRA32格式，每像素4字节

                    // 写入BGRA值
                    pixelBuffer[pixelIndex] = colorB;     // Blue
                    pixelBuffer[pixelIndex + 1] = colorG; // Green
                    pixelBuffer[pixelIndex + 2] = colorR; // Red
                    pixelBuffer[pixelIndex + 3] = colorA; // Alpha
                }
            }
        }

        private void ClearRegion(int x, int y, int width, int height)
        {
            // 清除指定区域的像素
            for (int row = y; row < y + height && row < screenHeight; row++)
            {
                for (int col = x; col < x + width && col < screenWidth; col++)
                {
                    int pixelIndex = row * stride + col * 4;
                    pixelBuffer[pixelIndex] = 0;     // Blue
                    pixelBuffer[pixelIndex + 1] = 0; // Green
                    pixelBuffer[pixelIndex + 2] = 0; // Red
                    pixelBuffer[pixelIndex + 3] = 0; // Alpha (透明)
                }
            }
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

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private void OnSettingsClick(object sender, EventArgs e)
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
        public bool GetUseRegionalUpdate() => useRegionalUpdate;
        public bool GetAutoPerformanceOptimization() => autoPerformanceOptimization;

        // 性能优化预设
        public void ApplyPerformancePreset(string preset)
        {
            switch (preset.ToLower())
            {
                case "high_performance":
                    // 高性能模式：最低GPU负载
                    UpdatePetSettings(petSpeed, rotationSpeed, wanderStrength, wallRepulsionStrength,
                        800, 30, opacity, petColor); // 800点，30FPS
                    break;

                case "balanced":
                    // 平衡模式：中等质量和性能
                    UpdatePetSettings(petSpeed, rotationSpeed, wanderStrength, wallRepulsionStrength,
                        1500, 45, opacity, petColor); // 1500点，45FPS
                    break;

                case "high_quality":
                    // 高质量模式：最佳视觉效果
                    UpdatePetSettings(petSpeed, rotationSpeed, wanderStrength, wallRepulsionStrength,
                        3000, 60, opacity, petColor); // 3000点，60FPS
                    break;
            }
        }

        // 添加一个方法来检测当前性能并自动调整
        public void AutoOptimizePerformance()
        {
            if (currentFps < 20)
            {
                // 性能很差，使用高性能模式
                ApplyPerformancePreset("high_performance");
                System.Diagnostics.Debug.WriteLine("自动切换到高性能模式");
            }
            else if (currentFps < 40)
            {
                // 性能一般，使用平衡模式
                ApplyPerformancePreset("balanced");
                System.Diagnostics.Debug.WriteLine("自动切换到平衡模式");
            }
            // 如果FPS >= 40，保持当前设置
        }

        public void UpdatePetSettings(double petSpeed, double rotationSpeed, double wanderStrength,
                                     double wallRepulsionStrength, int numPoints, int targetFps,
                                     double opacity, PetColor petColor, bool autoOptimize = false)
        {
            this.petSpeed = petSpeed;
            this.rotationSpeed = rotationSpeed;
            this.wanderStrength = wanderStrength;
            this.wallRepulsionStrength = wallRepulsionStrength;
            this.opacity = opacity;
            this.petColor = petColor;
            this.autoPerformanceOptimization = autoOptimize;

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

            // 标记需要完全重绘
            needsFullRedraw = true;

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
                appSettings.AutoPerformanceOptimization = autoOptimize;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
        protected override void OnClosed(EventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;

            // Close settings window if open
            if (settingsWindow != null)
                settingsWindow.Close();

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

            // 清理渲染资源
            if (writeableBitmap != null)
            {
                writeableBitmap = null;
            }
            pixelBuffer = null;
            fishPoints = null;

            // Clean up system tray resources
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            if (contextMenu != null)
                contextMenu.Dispose();

            base.OnClosed(e);
        }
    }
}