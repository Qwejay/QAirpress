using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Forms;
using ImageMagick;
using System.Threading;
using System.Windows.Threading;
using System.Threading.Tasks.Dataflow;
using System.Windows.Media.Animation;

namespace QAirpress
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> _selectedFiles = new List<string>();
        private readonly string[] _supportedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
        private bool _isCompressing = false;
        private CancellationTokenSource? _cancellationTokenSource;
        private DispatcherTimer? _statusResetTimer;
        private string _defaultStatus = "就绪";
        private int _currentQuality = 75; // 默认中等质量
        
        // 设置相关
        private string _fileNamingPattern = "原文件名_compressed";
        private string _customSuffix = "_compressed";
        private bool _overwriteOriginal = false;
        private bool _autoOverwrite = true; // 默认勾选
        private bool _keepExif = false;
        
        // 新增功能设置
        private string _sizeLimit = "无限制";
        private bool _showProgress = true;
        private bool _showFileSize = true;
        private bool _convertToJpg = false;
        private bool _optimizeForWeb = false;
        private bool _jpegAdvanced = false;

        private Dictionary<string, long?> _estimatedSizeCache = new Dictionary<string, long?>();

        private DispatcherTimer _loadingDotTimer;
        private int _loadingDotFrame = 0;
        private DispatcherTimer _debounceTimer;
        private bool _isEstimating = false;

        // 新增：遮罩Border字段
        private Border ProgressMaskField;
        // 新增：进度条和按钮文字字段
        private Border ProgressFillField;
        private TextBlock CompressButtonTextField;
        // 新增：进度条底层Border字段
        private Border ProgressFullField;

        public MainWindow()
        {
            InitializeComponent();
            InitializeUI();
            LoadSettings();
            if (QualitySlider != null)
                QualitySlider.ValueChanged += QualitySlider_ValueChanged;
            if (QualitySlider != null && QualityValueText != null)
                QualityValueText.Text = $"{_currentQuality}%";

            // 启动loading动画定时器
            _loadingDotTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _loadingDotTimer.Tick += (s, e) =>
            {
                _loadingDotFrame = (_loadingDotFrame + 1) % 4;
                RenderFileList();
            };
            _loadingDotTimer.Start();

            // 滑块防抖定时器
            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                _ = DebouncedEstimateAllCompressedSizesAsync();
            };

            // 在构造函数或Window_Loaded中初始化字段
            this.Loaded += Window_Loaded;
            // 绑定字段
            this.ProgressMaskField = (Border)this.FindName("ProgressMask");
            this.ProgressFillField = (Border)this.FindName("ProgressFill");
            this.CompressButtonTextField = (TextBlock)this.FindName("CompressButtonText");
            this.ProgressFullField = (Border)this.FindName("ProgressFull");
        }

        private void InitializeUI()
        {
            // 正确绑定WPF拖放事件
            this.DragEnter += MainWindow_DragEnter;
            this.Drop += MainWindow_Drop;
        }

        private void MainWindow_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void MainWindow_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                ProcessDroppedFiles(files);
            }
        }

        private void SelectFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "选择图片文件",
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.tiff;*.webp|所有文件|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ProcessDroppedFiles(openFileDialog.FileNames);
            }
        }

        private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择包含图片的文件夹"
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ProcessDroppedFiles(new[] { folderDialog.SelectedPath });
            }
        }

        private void ProcessDroppedFiles(string[] files)
        {
            var newFiles = new List<string>();
            foreach (var path in files)
            {
                if (Directory.Exists(path))
                {
                    // 递归查找所有支持的图片文件
                    var imgs = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(f => _supportedExtensions.Contains(System.IO.Path.GetExtension(f).ToLower()));
                    newFiles.AddRange(imgs);
                }
                else if (File.Exists(path) && _supportedExtensions.Contains(System.IO.Path.GetExtension(path).ToLower()))
                {
                    newFiles.Add(path);
                }
            }
            // 去重
            newFiles = newFiles.Distinct().Where(f => !_selectedFiles.Contains(f)).ToList();
            if (newFiles.Count == 0)
            {
                ShowStatus("未发现新图片", true);
                return;
            }
            _selectedFiles.AddRange(newFiles);
            foreach (var f in newFiles) _estimatedSizeCache[f] = null;
            RenderFileList();
            ShowStatus($"已添加{newFiles.Count}个", false);
            // 新增：自动估算新图片压缩后大小
            _ = DebouncedEstimateAllCompressedSizesAsync();
        }

        private void RenderFileList()
        {
            FileListPanel.Children.Clear();
            if (_selectedFiles.Count == 0) return;
            for (int i = 0; i < _selectedFiles.Count; i++)
            {
                string file = _selectedFiles[i];
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var fileName = new TextBlock
                {
                    Text = System.IO.Path.GetFileName(file),
                    FontSize = 13,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51)),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                // 原始大小
                var fileSize = new TextBlock
                {
                    Text = $"{FormatFileSize(new FileInfo(file).Length)}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 120)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };

                // 预计压缩后大小
                TextBlock? estimatedText = null;
                if (_estimatedSizeCache.TryGetValue(file, out var estimated) && estimated != null && estimated > 0)
                {
                    estimatedText = new TextBlock
                    {
                        Text = $"→ {FormatFileSize(estimated.Value)}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(144, 202, 249)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                }
                else if (_estimatedSizeCache.TryGetValue(file, out estimated) && estimated == -1)
                {
                    estimatedText = new TextBlock
                    {
                        Text = "→ 估算失败",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                }
                else
                {
                    estimatedText = new TextBlock
                    {
                        Text = "→ 估算中...",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 220, 240)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                }

                var infoPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                infoPanel.Children.Add(fileName);
                infoPanel.Children.Add(fileSize);
                infoPanel.Children.Add(estimatedText);
                Grid.SetColumn(infoPanel, 0);

                // 极简删除按钮（SVG Path）
                var delBtn = new System.Windows.Controls.Button
                {
                    Style = (Style)FindResource("IconButton"),
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(8, 0, 0, 0),
                    Tag = i,
                    ToolTip = "移除该图片"
                };
                // SVG Path内容
                var viewbox = new System.Windows.Controls.Viewbox { Width = 16, Height = 16 };
                var canvas = new System.Windows.Controls.Canvas { Width = 16, Height = 16 };
                var line1 = new System.Windows.Shapes.Line
                {
                    X1 = 4, Y1 = 4, X2 = 12, Y2 = 12,
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102)),
                    StrokeThickness = 1.5,
                    StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
                    StrokeEndLineCap = System.Windows.Media.PenLineCap.Round
                };
                var line2 = new System.Windows.Shapes.Line
                {
                    X1 = 12, Y1 = 4, X2 = 4, Y2 = 12,
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102)),
                    StrokeThickness = 1.5,
                    StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
                    StrokeEndLineCap = System.Windows.Media.PenLineCap.Round
                };
                canvas.Children.Add(line1);
                canvas.Children.Add(line2);
                viewbox.Child = canvas;
                delBtn.Content = viewbox;
                int currentIndex = i;
                delBtn.Click += async (s, e) => {
                    // 重新获取索引，防止因列表刷新导致索引错乱
                    var btn = s as System.Windows.Controls.Button;
                    if (btn?.Tag is int idx && idx >= 0 && idx < _selectedFiles.Count)
                    {
                        string delFile = _selectedFiles[idx];
                        _selectedFiles.RemoveAt(idx);
                        _estimatedSizeCache.Remove(delFile);
                        await Task.Run(() => { GC.Collect(); GC.WaitForPendingFinalizers(); });
                        RenderFileList();
                        ShowStatus("已移除", false);
                    }
                };
                Grid.SetColumn(delBtn, 2);

                row.Children.Add(infoPanel);
                row.Children.Add(delBtn);
                FileListPanel.Children.Add(row);
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            if (order >= 2) // MB及以上显示一位小数
                return $"{len:F1} {sizes[order]}";
            else
                return $"{(int)Math.Ceiling(len)} {sizes[order]}";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ProgressMaskField.Width = CompressButton.ActualWidth;
            ProgressFullField.Background = System.Windows.Media.Brushes.Transparent;
            CompressButton.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F7FA"));
        }

        private async void CompressButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiles.Count == 0 || _isCompressing)
                return;

            _isCompressing = true;
            _cancellationTokenSource = new CancellationTokenSource();
            CompressButtonTextField.Text = "压缩中";
            // 遮罩归满
            var scale = ProgressMaskField.RenderTransform as ScaleTransform;
            if (scale == null)
            {
                scale = new ScaleTransform(1, 1);
                ProgressMaskField.RenderTransform = scale;
            }
            scale.ScaleX = 1;
            AnimateMask(1, 1); // 立即归满
            // 触发进度条动画
            if (ProgressMaskField?.Resources["ProgressAnimation"] is Storyboard sb)
            {
                sb.Begin(ProgressMaskField);
            }
            CompressButton.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F7FA"));
            RenderFileList();
            try
            {
                await CompressFilesAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                ShowStatus("取消", true);
            }
            catch (Exception)
            {
                ShowStatus("错误", true);
            }
            finally
            {
                _isCompressing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                CompressButtonTextField.Text = "开始";
                // 恢复为默认色
                ProgressFullField.Background = System.Windows.Media.Brushes.Transparent;
                CompressButton.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F7FA"));
                RenderFileList();
            }
        }

        private void AnimateMask(double from, double to)
        {
            // 使用ScaleTransform的ScaleX属性进行动画（从1到0）
            var anim = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.7 }
            };
            var scale = ProgressMaskField.RenderTransform as ScaleTransform;
            if (scale == null)
            {
                scale = new ScaleTransform(1, 1);
                ProgressMaskField.RenderTransform = scale;
            }
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        }

        private async Task CompressFilesAsync(CancellationToken cancellationToken)
        {
            int quality = _currentQuality;
            long totalOriginalSize = 0;
            long totalCompressedSize = 0;
            int processedCount = 0;
            int failedCount = 0;
            int total = _selectedFiles.Count;
            // 遮罩归满
            var scale = ProgressMaskField.RenderTransform as ScaleTransform;
            if (scale == null)
            {
                scale = new ScaleTransform(1, 1);
                ProgressMaskField.RenderTransform = scale;
            }
            scale.ScaleX = 1;
            AnimateMask(1, 1); // 立即归满
            await Task.Delay(50); // 确保动画生效
            for (int i = 0; i < _selectedFiles.Count; i++)
            {
                string file = _selectedFiles[i];
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    ShowStatus("压缩中", false);
                    var result = await Task.Run(() => CompressSingleFile(file, quality), cancellationToken);
                    totalOriginalSize += result.OriginalSize;
                    totalCompressedSize += result.CompressedSize;
                    processedCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failedCount++;
                    Console.WriteLine($"压缩文件 {file} 时出错: {ex.Message}");
                }
                // 动画更新遮罩ScaleX（1-进度百分比）
                double percent = processedCount / (double)total;
                AnimateMask((ProgressMaskField.RenderTransform as ScaleTransform)?.ScaleX ?? 1, 1 - percent);
                await Task.Delay(80); // 动画更流畅
            }
            // 显示结果
            if (processedCount > 0)
            {
                double compressionRatio = (double)(totalOriginalSize - totalCompressedSize) / totalOriginalSize * 100;
                string resultMessage = $"完成 {processedCount}个 节省{compressionRatio:F1}%";
                if (failedCount > 0)
                {
                    resultMessage += $" 失败{failedCount}个";
                }
                ShowStatus(resultMessage, false, 4);
            }
            else
            {
                ShowStatus("失败", true);
            }
        }

        private (long OriginalSize, long CompressedSize) CompressSingleFile(string inputPath, int quality)
        {
            var fileInfo = new FileInfo(inputPath);
            long originalSize = fileInfo.Length;
            
            string outputPath = GetOutputPath(inputPath);
            
            using (var image = new MagickImage(inputPath))
            {
                // 设置压缩质量
                image.Quality = quality;
                
                // 根据设置决定是否保留EXIF
                if (!_keepExif)
                {
                    image.Strip();
                }
                
                if (_jpegAdvanced) {
                    image.SetAttribute("jpeg:sampling-factor", "4:4:4");
                    image.Settings.Interlace = Interlace.Jpeg;
                    if (quality < 50) {
                        image.Settings.SetDefine(MagickFormat.Jpeg, "dct-method", "float");
                        image.Settings.SetDefine(MagickFormat.Jpeg, "smooth", "5");
                    }
                }
                
                // 写入压缩后的文件
                image.Write(outputPath);
            }
            
            var compressedFileInfo = new FileInfo(outputPath);
            return (originalSize, compressedFileInfo.Length);
        }

        private string GetOutputPath(string inputPath)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(inputPath);
            string extension = System.IO.Path.GetExtension(inputPath);
            string outputDir = System.IO.Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
            
            string outputFileName;
            if (_overwriteOriginal)
            {
                // 直接覆盖原文件
                outputFileName = fileName;
            }
            else if (_fileNamingPattern == "自定义后缀")
            {
                // 使用自定义后缀
                outputFileName = fileName + _customSuffix;
            }
            else
            {
                // 使用预设模式
                outputFileName = _fileNamingPattern
                    .Replace("原文件名", fileName)
                    .Replace("_compressed", "_compressed")
                    .Replace("_压缩", "_compressed")
                    .Replace("_mini", "_mini")
                    .Replace("_opt", "_opt");
            }
            
            string fullOutputPath = System.IO.Path.Combine(outputDir, outputFileName + extension);
            
            // 如果不是直接覆盖原文件，且不自动覆盖，则添加序号
            if (!_overwriteOriginal && !_autoOverwrite && File.Exists(fullOutputPath))
            {
                int counter = 1;
                while (File.Exists(fullOutputPath))
                {
                    string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(outputFileName);
                    fullOutputPath = System.IO.Path.Combine(outputDir, $"{nameWithoutExt}_{counter}{extension}");
                    counter++;
                }
            }
            
            return fullOutputPath;
        }

        // 极简状态栏通知
        private void ShowStatus(string msg, bool isError, int seconds = 3)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(() => ShowStatus(msg, isError, seconds));
                return;
            }
            StatusText.Text = msg;
            StatusText.Foreground = isError ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(255,82,82)) : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0,122,255));
            StatusText.Opacity = 1.0;
            // 临时去除自动消失动画和定时器
            //_statusResetTimer?.Stop();
            //if (seconds > 0) { ... }
        }

        private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (QualitySlider == null || QualityValueText == null) return;
            int value = (int)QualitySlider.Value;
            _currentQuality = value;
            QualityValueText.Text = $"{value}%";
            // 低质量警告改为状态栏提示
            if (value < 50)
                ShowStatus("低质量会有明显色块", true, 0);
            else
                ShowStatus("就绪", false, 0);
            // 滑动时只显示"估算中..."
            foreach (var file in _selectedFiles)
                _estimatedSizeCache[file] = null;
            RenderFileList();
            // 重置防抖定时器
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private async void QualitySlider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (QualitySlider != null)
                QualitySlider.IsEnabled = false;
            await DebouncedEstimateAllCompressedSizesAsync();
            if (QualitySlider != null)
                QualitySlider.IsEnabled = true;
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists("settings.txt"))
                {
                    string[] lines = File.ReadAllLines("settings.txt");
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            switch (parts[0].Trim())
                            {
                                case "FileNamingPattern":
                                    _fileNamingPattern = parts[1].Trim();
                                    break;
                                case "CustomSuffix":
                                    _customSuffix = parts[1].Trim();
                                    break;
                                case "OverwriteOriginal":
                                    _overwriteOriginal = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "DefaultQuality":
                                    if (int.TryParse(parts[1].Trim(), out int quality))
                                    {
                                        _currentQuality = quality;
                                    }
                                    break;
                                case "AutoOverwrite":
                                    _autoOverwrite = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "KeepExif":
                                    _keepExif = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "SizeLimit":
                                    _sizeLimit = parts[1].Trim();
                                    break;
                                case "ShowProgress":
                                    _showProgress = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "ShowFileSize":
                                    _showFileSize = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "ConvertToJpg":
                                    _convertToJpg = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "OptimizeForWeb":
                                    _optimizeForWeb = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "JpegAdvanced":
                                    _jpegAdvanced = parts[1].Trim().ToLower() == "true";
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载设置失败: {ex.Message}");
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.Owner = this;
                
                if (settingsWindow.ShowDialog() == true)
                {
                    _fileNamingPattern = settingsWindow.FileNamingPattern;
                    _customSuffix = settingsWindow.CustomSuffix;
                    _overwriteOriginal = settingsWindow.OverwriteOriginal;
                    _currentQuality = settingsWindow.DefaultQuality;
                    _autoOverwrite = settingsWindow.AutoOverwrite;
                    _keepExif = settingsWindow.KeepExif;
                    _sizeLimit = settingsWindow.SizeLimit;
                    _showProgress = settingsWindow.ShowProgress;
                    _showFileSize = settingsWindow.ShowFileSize;
                    _convertToJpg = settingsWindow.ConvertToJpg;
                    _optimizeForWeb = settingsWindow.OptimizeForWeb;
                    _jpegAdvanced = settingsWindow.JpegAdvanced;
                    
                    ShowStatus("设置已保存", false);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"设置失败: {ex.Message}", true);
            }
        }

        private async Task EstimateAllCompressedSizesAsync()
        {
            var files = _selectedFiles.ToArray();
            var quality = _currentQuality;
            int maxDegree = Math.Max(2, Environment.ProcessorCount);
            var tasks = files.Select(file => Task.Run(async () => {
                try
                {
                    using (var image = new MagickImage(file))
                    {
                        image.Quality = quality;
                        if (!_keepExif)
                            image.Strip();
                        if (_jpegAdvanced) {
                            image.SetAttribute("jpeg:sampling-factor", "4:4:4");
                            image.Settings.Interlace = Interlace.Jpeg;
                            if (quality < 50) {
                                image.Settings.SetDefine(MagickFormat.Jpeg, "dct-method", "float");
                                image.Settings.SetDefine(MagickFormat.Jpeg, "smooth", "5");
                            }
                        }
                        using (var ms = new MemoryStream())
                        {
                            image.Write(ms);
                            _estimatedSizeCache[file] = ms.Length;
                        }
                    }
                }
                catch
                {
                    _estimatedSizeCache[file] = -1;
                }
                // UI线程刷新
                await Dispatcher.InvokeAsync(RenderFileList);
            }));
            foreach (var file in files) _estimatedSizeCache[file] = null;
            RenderFileList(); // 先显示全部"估算中"
            await Task.WhenAll(tasks);
        }

        private async Task DebouncedEstimateAllCompressedSizesAsync()
        {
            _isEstimating = true;
            await EstimateAllCompressedSizesAsync();
            _isEstimating = false;
        }

        private void ClearFilesButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedFiles.Clear();
            _estimatedSizeCache.Clear();
            RenderFileList();
            ShowStatus("已清空", false);
        }
    }
}