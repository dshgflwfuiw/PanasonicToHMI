// 包含了所有必需的 using 指令
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text; // 包含 CodePagesEncodingProvider
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace 松下变量转HMI
{
    public partial class Form1 : Form
    {
        private RichTextBox logBox;

        public Form1()
        {
            InitializeComponent();
            this.Text = "松下变量转HMI工具";
            try
            {
                this.Icon = new Icon(Path.Combine(Application.StartupPath, "myicon.ico"));
            }
            catch { }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            this.Width = 900;
            this.Height = 600;
            this.MinimumSize = new Size(700, 500);

            logBox = new RichTextBox
            {
                Dock = DockStyle.Bottom,
                Height = 250,
                ReadOnly = true,
                Font = new Font("Consolas", 9.75f),
                BorderStyle = BorderStyle.Fixed3D,
                WordWrap = false
            };
            this.Controls.Add(logBox);
        }

        // 日志方法，支持颜色
        private void LogMessage(string message, bool isError = false, bool isWarning = false)
        {
            if (logBox.InvokeRequired)
            {
                logBox.Invoke(new Action(() => LogMessage(message, isError, isWarning)));
            }
            else
            {
                logBox.SelectionStart = logBox.TextLength;
                logBox.SelectionLength = 0;

                logBox.SelectionColor = Color.Gray;
                logBox.AppendText($"{DateTime.Now:HH:mm:ss} - ");

                if (isError) { logBox.SelectionColor = Color.Red; logBox.AppendText("[ERROR] "); }
                else if (isWarning) { logBox.SelectionColor = Color.OrangeRed; logBox.AppendText("[WARN] "); }
                else { logBox.SelectionColor = Color.DarkGreen; logBox.AppendText("[INFO] "); }

                logBox.SelectionColor = logBox.ForeColor;
                logBox.AppendText(message + Environment.NewLine);

                logBox.ScrollToCaret();
            }
        }

        // 按钮点击事件
        private async void btnProcess_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "请选择PLC变量CSV文件";
                ofd.Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";
                ofd.RestoreDirectory = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string plcCsvFilePath = ofd.FileName;
                    string inputDirectory = Path.GetDirectoryName(plcCsvFilePath);
                    string inputFileNameWithoutExt = Path.GetFileNameWithoutExtension(plcCsvFilePath);
                    string outputFileName = $"{inputFileNameWithoutExt}_HMI变量.csv";
                    string hmiCsvFileOutputhPath = Path.Combine(inputDirectory, outputFileName);

                    logBox.Clear();
                    LogMessage($"已选择源文件: {plcCsvFilePath}");
                    LogMessage($"目标文件将保存为: {hmiCsvFileOutputhPath}");
                    LogMessage("转换开始...");
                    btnProcess.Enabled = false;
                    btnProcess.Text = "正在转换中，请稍候...";

                    // 调用CsvProcessor
                    var processor = new CsvProcessor();
                    bool success = await Task.Run(() =>
                        processor.ConvertPlcCsvToHmiCsv(plcCsvFilePath, hmiCsvFileOutputhPath, msg => LogMessage(msg))
                    );

                    if (success)
                    {
                        LogMessage("文件转换成功完成。");
                        MessageBox.Show($"文件转换成功！\n输出文件已保存至:\n{hmiCsvFileOutputhPath}", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        LogMessage("文件转换失败，详情请查看以上日志。", isError: true);
                        MessageBox.Show("文件转换失败，请查看日志获取详细信息。", "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    btnProcess.Enabled = true;
                    btnProcess.Text = "选择并转换文件";
                }
            }
        }

        // 窗口大小变化时按钮居中
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (btnProcess != null)
            {
                btnProcess.Left = (this.ClientSize.Width - btnProcess.Width) / 2;
            }
        }
    }
   
}
