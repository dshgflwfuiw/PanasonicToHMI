// 引入系统命名空间，提供基础类型和工具
using System;
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
                var stream = Application.GetResourceStream(new Uri("pack://application:,,,/myicon.ico"));
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
            // Dispatcher.Invoke 确保在主线程中更新界面
            // 如果直接从后台线程修改界面会报错
            Dispatcher.Invoke(() =>
            {
                // 获取当前时间，格式为 时:分:秒
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                // 根据消息类型确定日志级别标签
                string level = isError ? "ERROR" : (isWarning ? "WARN" : "INFO");
                // 根据消息类型确定颜色（错误红色，警告橙色，正常绿色）
                string color = isError ? "#E74C3C" : (isWarning ? "#F39C12" : "#27AE60");
                
                // 在日志文本框中添加时间戳（灰色）
                logText.Inlines.Add(new System.Windows.Documents.Run($"{timestamp} - ") { Foreground = System.Windows.Media.Brushes.Gray });
                // 添加日志级别标签（彩色）
                logText.Inlines.Add(new System.Windows.Documents.Run($"[{level}] ") { Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)) });
                // 添加消息内容（默认颜色）
                logText.Inlines.Add(new System.Windows.Documents.Run(message + "\n"));
            });
        }

        // 按钮点击事件：选择CSV文件并开始转换
        // sender: 触发事件的控件（即按钮本身）
        // e: 事件参数
        private async void btnProcess_Click(object sender, RoutedEventArgs e)
        {
            // 创建文件选择对话框
            OpenFileDialog ofd = new OpenFileDialog
            {
                // 对话框标题
                Title = "请选择PLC变量CSV文件",
                // 文件过滤器：只显示 CSV 文件和所有文件
                Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                // 记住上次打开的目录
                RestoreDirectory = true
            };

            // 显示对话框并检查用户是否点击了"打开"按钮
            if (ofd.ShowDialog() == true)
            {
                // await 等待文件转换完成，不会卡死界面
                await ProcessFile(ofd.FileName);
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
                // 检查是否有文件且文件后缀是 csv
                if (files.Length > 0 && files[0].EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    // 处理第一个文件
                    await ProcessFile(files[0]);
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

        // 处理文件转换的核心方法
        // plcCsvFilePath: 输入的 CSV 文件完整路径
        private async Task ProcessFile(string plcCsvFilePath)
        {
            // 获取选择的PLC品牌
            string selectedBrand = "Panasonic";
            string brandName = "松下";
            
            if (cmbPlcBrand.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                selectedBrand = selectedItem.Tag.ToString() ?? "Panasonic";
                brandName = selectedItem.Content.ToString() ?? "松下";
            }

            // 获取输入文件所在目录
            string? inputDirectory = Path.GetDirectoryName(plcCsvFilePath);
            // 检查目录是否有效
            if (inputDirectory == null)
            {
                // 记录错误日志
                LogMessage("无法获取文件目录路径。", isError: true);
                return;
            }
            // 获取不含扩展名的文件名（例如：test.csv -> test）
            string inputFileNameWithoutExt = Path.GetFileNameWithoutExtension(plcCsvFilePath);
            // 构造输出文件名（原文件名 + 品牌名 + _HMI变量.csv）
            string outputFileName = $"{inputFileNameWithoutExt}_{brandName}_HMI变量.csv";
            // 拼接输出文件的完整路径
            string hmiCsvFilePath = Path.Combine(inputDirectory, outputFileName);

            // 清空日志区域
            logText.Inlines.Clear();
            // 记录选择的PLC品牌
            LogMessage($"选择的PLC品牌: {brandName} ({selectedBrand})");
            // 记录输入文件路径
            LogMessage($"已选择源文件: {plcCsvFilePath}");
            // 记录输出文件路径
            LogMessage($"目标文件将保存为: {hmiCsvFilePath}");
            // 记录转换开始
            LogMessage("转换开始...");
            
            // 禁用按钮，防止重复点击
            btnProcess.IsEnabled = false;
            // 修改按钮文字提示用户正在处理
            btnProcess.Content = "正在转换中，请稍候...";

            // 根据选择的品牌创建对应的处理器实例
            bool success = false;
            if (selectedBrand == "Panasonic")
            {
                var processor = new PanasonicCsvProcessor();
                success = await Task.Run(() =>
                    processor.ConvertPlcCsvToHmiCsv(plcCsvFilePath, hmiCsvFilePath, msg => LogMessage(msg))
                );
            }
            else if (selectedBrand == "Mitsubishi")
            {
                var processor = new MitsubishiCsvProcessor();
                success = await Task.Run(() =>
                    processor.ConvertPlcCsvToHmiCsv(plcCsvFilePath, hmiCsvFilePath, msg => LogMessage(msg))
                );
            }

            // 检查转换是否成功
            if (success)
            {
                // 记录成功日志
                LogMessage("文件转换成功完成。");
                // 弹出成功提示框
                MessageBox.Show($"文件转换成功！\n输出文件已保存至:\n{hmiCsvFilePath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // 记录失败日志
                LogMessage("文件转换失败，详情请查看以上日志。", isError: true);
                // 弹出失败提示框
                MessageBox.Show("文件转换失败，请查看日志获取详细信息。", "失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
            logText.Inlines.Clear();
        }
    }
}