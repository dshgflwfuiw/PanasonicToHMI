// 包含了所有必需的 using 指令
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PanasonicToHmi
{
    public partial class Form1 : Form
    {
        private RichTextBox logBox;
        private string currentCsvFilePath;
        private List<string> errorRows = new List<string>();
        private int totalProcessedRows;
        private int successRows;

        public Form1()
        {
            InitializeComponent();
            this.Text = "PLC 变量转换器";
            try
            {
                this.Icon = new Icon(Path.Combine(Application.StartupPath, "myicon.ico"));
            }
            catch { }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            this.Width = 900;
            this.Height = 650;
            this.MinimumSize = new Size(700, 550);

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
            
            cmbBrandTemplate.SelectedIndexChanged += CmbBrandTemplate_SelectedIndexChanged;
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
                ofd.Title = "请选择 PLC 变量 CSV 文件";
                ofd.Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";
                ofd.RestoreDirectory = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    await ProcessCsvFile(ofd.FileName);
                }
            }
        }

        // 品牌模板切换事件
        private void CmbBrandTemplate_SelectedIndexChanged(object sender, EventArgs e)
        {
            LogMessage($"已切换品牌模板：{cmbBrandTemplate.SelectedItem}");
        }

        // 拖拽进入事件 - 视觉优化
        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                isDragOver = true;
                this.BackColor = Color.LightBlue;
                btnProcess.BackColor = Color.LightGreen;
                btnProcess.Text = "松开鼠标以处理文件";
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        // 拖拽离开事件 - 恢复原状
        private void Form1_DragLeave(object sender, EventArgs e)
        {
            isDragOver = false;
            this.BackColor = SystemColors.Control;
            btnProcess.BackColor = SystemColors.ButtonFace;
            btnProcess.Text = "选择 CSV 文件并处理";
        }

        // 拖拽放下事件 - 处理文件
        private async void Form1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && files[0].EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    Form1_DragLeave(sender, e);
                    await ProcessCsvFile(files[0]);
                }
                else
                {
                    LogMessage("请拖拽 CSV 文件到窗口", isWarning: true);
                    Form1_DragLeave(sender, e);
                }
            }
        }

        // 导出错误行按钮点击事件
        private void btnExportErrors_Click(object sender, EventArgs e)
        {
            if (errorRows.Count == 0)
            {
                MessageBox.Show("没有错误行可导出", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "保存错误行文件";
                sfd.Filter = "CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt";
                sfd.FileName = $"错误行_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllLines(sfd.FileName, errorRows, Encoding.UTF8);
                        LogMessage($"错误行已导出到：{sfd.FileName}");
                        MessageBox.Show($"成功导出 {errorRows.Count} 条错误记录", "成功", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"导出错误行失败：{ex.Message}", isError: true);
                        MessageBox.Show($"导出失败：{ex.Message}", "错误", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // 处理 CSV 文件的主方法
        private async Task ProcessCsvFile(string plcCsvFilePath)
        {
            string inputDirectory = Path.GetDirectoryName(plcCsvFilePath);
            string inputFileNameWithoutExt = Path.GetFileNameWithoutExtension(plcCsvFilePath);
            string outputFileName = $"{inputFileNameWithoutExt}_HMI 变量.csv";
            string hmiCsvFileOutputhPath = Path.Combine(inputDirectory, outputFileName);

            logBox.Clear();
            errorRows.Clear();
            totalProcessedRows = 0;
            successRows = 0;
            
            LogMessage($"已选择源文件：{plcCsvFilePath}");
            LogMessage($"目标文件将保存为：{hmiCsvFileOutputhPath}");
            LogMessage("转换开始...");
            
            btnProcess.Enabled = false;
            btnProcess.Text = "正在转换中，请稍候...";
            progressBar.Visible = true;
            lblStatus.Visible = true;
            btnExportErrors.Visible = false;
            btnExportErrors.Enabled = false;
            progressBar.Value = 0;

            string selectedBrand = cmbBrandTemplate.SelectedItem?.ToString() ?? "威纶通 (Weinview)";
            string brandCode = GetBrandCode(selectedBrand);
            
            LogMessage($"使用品牌模板：{selectedBrand}");

            var processor = new CsvProcessor();
            bool success = await Task.Run(() =>
                processor.ConvertPlcCsvToHmiCsv(plcCsvFilePath, hmiCsvFileOutputhPath, 
                    msg => LogMessage(msg),
                    progress => UpdateProgress(progress),
                    errorRow => AddErrorRow(errorRow),
                    (total, successCount) => UpdateStats(total, successCount),
                    brandCode)
            );

            if (success)
            {
                LogMessage($"文件转换成功完成！共处理 {totalProcessedRows} 行，成功 {successRows} 行");
                if (errorRows.Count > 0)
                {
                    LogMessage($"有 {errorRows.Count} 行出错，可点击\"导出错误行\"按钮查看", isWarning: true);
                    btnExportErrors.Enabled = true;
                    btnExportErrors.Visible = true;
                    btnExportErrors.Text = $"导出错误行 ({errorRows.Count})";
                }
                MessageBox.Show($"文件转换成功！\n输出文件已保存至:\n{hmiCsvFileOutputhPath}\n\n成功：{successRows} 行\n失败：{errorRows.Count} 行", 
                    "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                LogMessage("文件转换失败，详情请查看以上日志。", isError: true);
                if (errorRows.Count > 0)
                {
                    btnExportErrors.Enabled = true;
                    btnExportErrors.Visible = true;
                    btnExportErrors.Text = $"导出错误行 ({errorRows.Count})";
                }
                MessageBox.Show("文件转换失败，请查看日志获取详细信息。", "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            progressBar.Visible = false;
            lblStatus.Visible = false;
            btnProcess.Enabled = true;
            btnProcess.Text = "选择并转换文件";
        }

        // 获取品牌代码
        private string GetBrandCode(string brandName)
        {
            return brandName switch
            {
                "威纶通 (Weinview)" => "Panasonic FP/KW",
                "西门子 (Siemens)" => "Siemens",
                "三菱 (Mitsubishi)" => "Mitsubishi",
                "欧姆龙 (Omron)" => "Omron",
                "台达 (Delta)" => "Delta",
                _ => "Panasonic FP/KW"
            };
        }

        // 更新进度条
        private void UpdateProgress(int percent)
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.Invoke(new Action(() => UpdateProgress(percent)));
            }
            else
            {
                progressBar.Value = Math.Min(100, Math.Max(0, percent));
                lblStatus.Text = $"处理进度：{percent}%";
            }
        }

        // 添加错误行
        private void AddErrorRow(string errorRow)
        {
            if (errorRows.InvokeRequired)
            {
                errorRows.Invoke(new Action(() => AddErrorRow(errorRow)));
            }
            else
            {
                errorRows.Add(errorRow);
            }
        }

        // 更新统计信息
        private void UpdateStats(int total, int success)
        {
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(new Action(() => UpdateStats(total, success)));
            }
            else
            {
                totalProcessedRows = total;
                successRows = success;
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
