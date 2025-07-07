using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Forms;
using WpfCheckBox = System.Windows.Controls.CheckBox;

namespace QAirpress
{
    public partial class SettingsWindow : Window
    {
        public string FileNamingPattern { get; private set; } = "原文件名_compressed";
        public string CustomSuffix { get; private set; } = "_compressed";
        public bool OverwriteOriginal { get; private set; } = false;
        public int DefaultQuality { get; private set; } = 75;
        public bool AutoOverwrite { get; private set; } = true; // 默认勾选
        public bool KeepExif { get; private set; } = false;
        
        // 新增功能属性
        public string SizeLimit { get; private set; } = "无限制";
        public bool ShowProgress { get; private set; } = true;
        public bool ShowFileSize { get; private set; } = true;
        public bool ConvertToJpg { get; private set; } = false;
        public bool OptimizeForWeb { get; private set; } = false;
        public bool JpegAdvanced { get; private set; } = false;

        public SettingsWindow()
        {
            try
            {
                InitializeComponent();
                this.Loaded += SettingsWindow_Loaded;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"初始化设置窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (QualitySlider != null)
                    QualitySlider.ValueChanged += QualitySlider_ValueChanged;
                LoadSettings();
                // 初始化滑块数值显示
                if (QualitySlider != null && QualityValueText != null)
                    QualityValueText.Text = $"{(int)QualitySlider.Value}%";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"加载设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSettings()
        {
            try
            {
                // 从配置文件加载设置
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
                                    FileNamingPattern = parts[1].Trim();
                                    break;
                                case "CustomSuffix":
                                    CustomSuffix = parts[1].Trim();
                                    break;
                                case "OverwriteOriginal":
                                    OverwriteOriginal = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "DefaultQuality":
                                    if (int.TryParse(parts[1].Trim(), out int quality))
                                        DefaultQuality = quality;
                                    break;
                                case "AutoOverwrite":
                                    AutoOverwrite = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "KeepExif":
                                    KeepExif = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "SizeLimit":
                                    SizeLimit = parts[1].Trim();
                                    break;
                                case "ShowProgress":
                                    ShowProgress = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "ShowFileSize":
                                    ShowFileSize = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "ConvertToJpg":
                                    ConvertToJpg = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "OptimizeForWeb":
                                    OptimizeForWeb = parts[1].Trim().ToLower() == "true";
                                    break;
                                case "JpegAdvanced":
                                    JpegAdvanced = parts[1].Trim().ToLower() == "true";
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

            try
            {
                // 更新UI - 添加空值检查
                // 设置文件命名模式
                UpdateNamingComboBox();
                
                // 设置自定义后缀
                if (CustomSuffixTextBox != null)
                    CustomSuffixTextBox.Text = CustomSuffix;
                
                // 设置默认质量（Slider）
                if (QualitySlider != null)
                {
                    QualitySlider.Value = DefaultQuality;
                    if (QualityValueText != null)
                        QualityValueText.Text = $"{DefaultQuality}%";
                }
                
                if (AutoOverwriteCheckBox != null)
                    AutoOverwriteCheckBox.IsChecked = AutoOverwrite;
                if (KeepExifCheckBox != null)
                    KeepExifCheckBox.IsChecked = KeepExif;
                
                // 设置文件大小限制
                if (SizeLimitComboBox != null)
                {
                    for (int i = 0; i < SizeLimitComboBox.Items.Count; i++)
                    {
                        if (SizeLimitComboBox.Items[i] is ComboBoxItem item && item.Content?.ToString() == SizeLimit)
                        {
                            SizeLimitComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
                
                // 设置进度显示选项
                if (ShowProgressCheckBox != null)
                    ShowProgressCheckBox.IsChecked = ShowProgress;
                if (ShowFileSizeCheckBox != null)
                    ShowFileSizeCheckBox.IsChecked = ShowFileSize;
                
                // 设置格式转换选项
                if (ConvertToJpgCheckBox != null)
                    ConvertToJpgCheckBox.IsChecked = ConvertToJpg;
                if (OptimizeForWebCheckBox != null)
                    OptimizeForWebCheckBox.IsChecked = OptimizeForWeb;

                if (JpegAdvancedCheckBox != null)
                    JpegAdvancedCheckBox.IsChecked = JpegAdvanced;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新UI失败: {ex.Message}");
            }
        }

        private void UpdateNamingComboBox()
        {
            try
            {
                if (NamingComboBox == null) return;
                
                // 根据当前设置选择对应的选项
                if (OverwriteOriginal)
                {
                    NamingComboBox.SelectedIndex = 5; // 直接覆盖原文件
                }
                else if (FileNamingPattern == "自定义后缀")
                {
                    NamingComboBox.SelectedIndex = 4; // 自定义后缀
                }
                else
                {
                    // 查找匹配的预设选项
                    for (int i = 0; i < NamingComboBox.Items.Count - 2; i++) // 排除最后两个选项
                    {
                        if (NamingComboBox.Items[i] is ComboBoxItem item && item.Content?.ToString() == FileNamingPattern)
                        {
                            NamingComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新命名模式失败: {ex.Message}");
            }
        }

        private void NamingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (NamingComboBox?.SelectedItem is ComboBoxItem item && item.Content != null)
                {
                    string selectedContent = item.Content.ToString();
                    
                    if (selectedContent == "自定义后缀")
                    {
                        if (CustomSuffixPanel != null)
                            CustomSuffixPanel.Visibility = Visibility.Visible;
                        FileNamingPattern = "自定义后缀";
                        OverwriteOriginal = false;
                    }
                    else if (selectedContent == "直接覆盖原文件")
                    {
                        if (CustomSuffixPanel != null)
                            CustomSuffixPanel.Visibility = Visibility.Collapsed;
                        FileNamingPattern = "原文件名";
                        OverwriteOriginal = true;
                    }
                    else
                    {
                        if (CustomSuffixPanel != null)
                            CustomSuffixPanel.Visibility = Visibility.Collapsed;
                        FileNamingPattern = selectedContent;
                        OverwriteOriginal = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新命名模式失败: {ex.Message}");
            }
        }

        private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (QualitySlider == null || QualityValueText == null) return;
            int value = (int)QualitySlider.Value;
            DefaultQuality = value;
            QualityValueText.Text = $"{value}%";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 更新设置
                if (AutoOverwriteCheckBox != null)
                    AutoOverwrite = AutoOverwriteCheckBox.IsChecked ?? true; // 默认true
                if (KeepExifCheckBox != null)
                    KeepExif = KeepExifCheckBox.IsChecked ?? false;
                if (CustomSuffixTextBox != null)
                    CustomSuffix = CustomSuffixTextBox.Text.Trim();
                
                // 更新文件大小限制
                if (SizeLimitComboBox?.SelectedItem is ComboBoxItem sizeItem)
                    SizeLimit = sizeItem.Content?.ToString() ?? "无限制";
                
                // 更新进度显示选项
                if (ShowProgressCheckBox != null)
                    ShowProgress = ShowProgressCheckBox.IsChecked ?? true;
                if (ShowFileSizeCheckBox != null)
                    ShowFileSize = ShowFileSizeCheckBox.IsChecked ?? true;
                
                // 更新格式转换选项
                if (ConvertToJpgCheckBox != null)
                    ConvertToJpg = ConvertToJpgCheckBox.IsChecked ?? false;
                if (OptimizeForWebCheckBox != null)
                    OptimizeForWeb = OptimizeForWebCheckBox.IsChecked ?? false;

                if (JpegAdvancedCheckBox != null)
                    JpegAdvanced = JpegAdvancedCheckBox.IsChecked ?? false;

                if (QualitySlider != null)
                    DefaultQuality = (int)QualitySlider.Value;

                // 保存到文件
                string[] settings = {
                    $"FileNamingPattern={FileNamingPattern}",
                    $"CustomSuffix={CustomSuffix}",
                    $"OverwriteOriginal={OverwriteOriginal}",
                    $"DefaultQuality={DefaultQuality}",
                    $"AutoOverwrite={AutoOverwrite}",
                    $"KeepExif={KeepExif}",
                    $"SizeLimit={SizeLimit}",
                    $"ShowProgress={ShowProgress}",
                    $"ShowFileSize={ShowFileSize}",
                    $"ConvertToJpg={ConvertToJpg}",
                    $"OptimizeForWeb={OptimizeForWeb}",
                    $"JpegAdvanced={JpegAdvanced}"
                };
                File.WriteAllLines("settings.txt", settings);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 