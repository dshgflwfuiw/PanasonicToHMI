// 引入系统命名空间，提供基础类型和工具
using System;
// 集合和并行任务相关
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// 文件操作相关的类
using System.IO;
// 文本编码相关的类
using System.Text;
// 异步任务处理相关的类
using System.Windows;
// WPF界面控件命名空间
using System.Windows.Controls;
// 打开文件对话框
using Microsoft.Win32;

namespace PlcToHmi
{
    // MainWindow 是主窗口类，继承自 Window 基类
    // partial 表示这是一个分部类，代码分布在多个文件中
    // 另一部分代码由 XAML 文件自动生成
    public partial class MainWindow : Window
    {
        // 构造函数：窗口初始化时会自动执行
        public MainWindow()
        {
            // InitializeComponent 是自动生成的方法，负责加载 XAML 界面
            InitializeComponent();
            
            // 尝试设置窗口图标
            try
            {
                // 从嵌入的资源中加载图标
                var stream = Application.GetResourceStream(new Uri("pack://application:,,,/favicon.ico"));
                if (stream != null && stream.Stream != null)
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream.Stream;
                    bitmap.EndInit();
                    Icon = bitmap;
                }
            }
            catch { }
            
            // 注册编码提供器，支持中文编码（GBK等）
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        // 自定义日志输出方法：向界面的日志区域添加带颜色的文本
        // 参数 message: 日志消息内容
        // 参数 isError: 是否是错误消息（默认 false）
        // 参数 isWarning: 是否是警告消息（默认 false）
        private void LogMessage(string message, bool isError = false, bool isWarning = false)
        {
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string level = isError ? "ERROR" : (isWarning ? "WARN" : "INFO");
                string logLine = $"{timestamp} - [{level}] {message}{Environment.NewLine}";
                logText.AppendText(logLine);
                logText.ScrollToEnd();
            });
        }

        // 按钮点击事件：选择CSV文件并开始转换
        // sender: 触发事件的控件（即按钮本身）
        // e: 事件参数
        private async void btnProcess_Click(object sender, RoutedEventArgs e)
        {
            // 创建文件选择对话框（允许多选）
            OpenFileDialog ofd = new OpenFileDialog
            {
                // 对话框标题
                Title = "请选择PLC变量CSV文件",
                // 文件过滤器：只显示 CSV 文件和所有文件
                Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                // 允许多文件选择
                Multiselect = true,
                // 记住上次打开的目录
                RestoreDirectory = true
            };

            // 显示对话框并检查用户是否点击了"打开"按钮
            if (ofd.ShowDialog() == true)
            {
                // await 等待文件转换完成，不会卡死界面
                await ProcessFile(ofd.FileNames);
            }
        }

        // 拖放事件：当用户把文件拖放到指定区域时触发
        private async void Border_Drop(object sender, DragEventArgs e)
        {
            // 检查拖放的数据是否包含文件
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 获取拖放的文件列表
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                // 过滤出 CSV 文件
                var csvFiles = files.Where(f => f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (csvFiles.Length > 0)
                {
                    // 处理所有选中的 CSV 文件，合并为一个输出
                    await ProcessFile(csvFiles);
                }
                else
                {
                    // 提示用户只能拖拽 CSV 文件
                    MessageBox.Show("请拖拽CSV文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // 拖拽进入事件：当用户拖拽文件进入拖放区域时触发
        private void Border_DragEnter(object sender, DragEventArgs e)
        {
            // 检查拖放的数据是否包含文件
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 设置拖放效果为复制
                e.Effects = DragDropEffects.Copy;
                // 获取边框控件引用
                if (sender is Border border)
                {
                    // 改变边框颜色为蓝色，表示可以放置
                    border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219));
                    // 改变背景色为浅蓝色，表示可以放置
                    border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 244, 248));
                }
            }
            else
            {
                // 如果拖放的不是文件，不允许放置
                e.Effects = DragDropEffects.None;
            }
        }

        // 拖拽离开事件：当用户拖拽文件离开拖放区域时触发
        private void Border_DragLeave(object sender, DragEventArgs e)
        {
            // 获取边框控件引用并恢复原始颜色
            if (sender is Border border)
            {
                // 恢复边框颜色为灰色
                border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224));
                // 恢复背景色为浅灰白色
                border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 250));
            }
        }

        // 处理文件转换的核心方法（支持多个输入文件，生成单个输出）
        // plcCsvFilePaths: 输入的 CSV 文件完整路径数组
        private async Task ProcessFile(string[] plcCsvFilePaths)
        {
            // 获取选择的PLC品牌
            string selectedBrand = "Panasonic";
            string brandName = "松下";
            
            if (cmbPlcBrand.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                selectedBrand = selectedItem.Tag.ToString() ?? "Panasonic";
                brandName = selectedItem.Content.ToString() ?? "松下";
            }

            // 获取输入文件所在目录（使用第一个文件所在目录）
            string? inputDirectory = Path.GetDirectoryName(plcCsvFilePaths[0]);
            // 检查目录是否有效
            if (inputDirectory == null)
            {
                // 记录错误日志
                LogMessage("无法获取文件目录路径。", isError: true);
                return;
            }
            // 获取不含扩展名的第一个文件名（用于输出文件命名）
            string inputFileNameWithoutExt = Path.GetFileNameWithoutExtension(plcCsvFilePaths[0]);
            // 构造输出文件名（第一个源文件名 + _Merged_ + 品牌名 + _HMI变量.csv）
            string outputFileName = $"{inputFileNameWithoutExt}_Merged_{brandName}_HMI变量.csv";
            // 拼接输出文件的完整路径
            string hmiCsvFilePath = Path.Combine(inputDirectory, outputFileName);

            // 清空日志区域
            logText.Clear();
            // 记录选择的PLC品牌
            LogMessage($"选择的PLC品牌: {brandName} ({selectedBrand})");
            // 记录输入文件路径（列出所有已选择文件）
            LogMessage($"已选择源文件 ({plcCsvFilePaths.Length}) : {string.Join(", ", plcCsvFilePaths.Select(f => Path.GetFileName(f)))}");
            // 记录输出文件路径
            LogMessage($"目标文件将保存为: {hmiCsvFilePath}");
            // 记录转换开始
            LogMessage("转换开始...");
            
            // 禁用按钮，防止重复点击
            btnProcess.IsEnabled = false;
            // 修改按钮文字提示用户正在处理
            btnProcess.Content = "正在转换中，请稍候...";

            // 根据选择的品牌创建对应的处理器实例（一个实例用于处理所有输入文件）
            PlcCsvProcessorBase? processor = null;
            if (selectedBrand == "Panasonic")
            {
                processor = new PanasonicCsvProcessor();
            }
            else if (selectedBrand == "Mitsubishi")
            {
                processor = new MitsubishiCsvProcessor();
            }
           else if (selectedBrand == "Xinjie")
           {
               processor = new XinjieCsvProcessor();
           }
            else if (selectedBrand == "Delta")
            {
                processor = new DeltaCsvProcessor();
            }
            else if (selectedBrand == "Keyence")
            {
                processor = new KeyenceCsvProcessor();
            }
            else if (selectedBrand == "Huichuan")
            {
                processor = new HuichuanCsvProcessor();
            }

            bool success = false;
            if (processor == null)
            {
                LogMessage($"未找到对应的处理器: {selectedBrand}", isError: true);
            }
            else
            {
                // 处理每个文件并汇总结果
                var allRows = new List<string[]>();
                foreach (var file in plcCsvFilePaths)
                {
                    LogMessage($"解析文件: {file}");
                    var rows = await Task.Run(() => processor.ParsePlcCsvToHmiRows(file, msg => LogMessage(msg)));
                    if (rows != null && rows.Count > 0)
                    {
                        allRows.AddRange(rows);
                        LogMessage($"已将 {rows.Count} 行添加到汇总结果。");
                    }
                    else
                    {
                        LogMessage($"文件 {file} 未返回可用数据，已跳过。", isWarning: true);
                    }
                }

                if (allRows.Count == 0)
                {
                    LogMessage("未从任何输入文件中提取到有效数据，未生成输出文件。", isError: true);
                }
                else
                {
                    // 写入单个输出文件，添加表头
                    var outputRows = new List<string[]>();
                    outputRows.Add(new[] { "名称", "品牌", "起始符号", "起始地址", "", "数据类型" });
                    outputRows.AddRange(allRows);

                    try
                    {
                        using (StreamWriter sw = new(hmiCsvFilePath, false, new UTF8Encoding(true)))
                        {
                            foreach (var row in outputRows)
                                sw.WriteLine(string.Join(",", row.Select(s => $"\"{s}\"")));
                        }
                        success = true;
                        LogMessage("文件转换成功完成。");
                        MessageBox.Show($"文件转换成功！\n输出文件已保存至:\n{hmiCsvFilePath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"写入输出文件失败: {ex.Message}", isError: true);
                        MessageBox.Show("写入输出文件失败，请查看日志获取详细信息。", "失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            // 恢复按钮可用状态
            btnProcess.IsEnabled = true;
            // 恢复按钮文字
            btnProcess.Content = "选择文件开始转换";
        }

        // 清除日志按钮点击事件
        private void btnClearLog_Click(object sender, RoutedEventArgs e)
        {
            // 清空日志文本框的所有内容
            logText.Clear();
        }
    }
}
