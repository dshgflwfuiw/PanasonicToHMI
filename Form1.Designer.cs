namespace PanasonicToHmi;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.Button btnProcess;
    private System.Windows.Forms.ProgressBar progressBar;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.ComboBox cmbBrandTemplate;
    private System.Windows.Forms.Label lblBrand;
    private System.Windows.Forms.Button btnExportErrors;
    private bool isDragOver = false;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        btnProcess = new Button();
        progressBar = new ProgressBar();
        lblStatus = new Label();
        cmbBrandTemplate = new ComboBox();
        lblBrand = new Label();
        btnExportErrors = new Button();
        SuspendLayout();
        // 
        // btnProcess
        // 
        btnProcess.Location = new Point(31, 30);
        btnProcess.Name = "btnProcess";
        btnProcess.Size = new Size(821, 46);
        btnProcess.TabIndex = 0;
        btnProcess.Text = "选择CSV文件并处理";
        btnProcess.UseVisualStyleBackColor = true;
        btnProcess.Click += btnProcess_Click;
        // 
        // lblBrand
        // 
        lblBrand.AutoSize = true;
        lblBrand.Location = new Point(31, 90);
        lblBrand.Name = "lblBrand";
        lblBrand.Size = new Size(80, 23);
        lblBrand.Text = "品牌模板:";
        // 
        // cmbBrandTemplate
        // 
        cmbBrandTemplate.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbBrandTemplate.Items.AddRange(new object[] { "威纶通 (Weinview)", "西门子 (Siemens)", "三菱 (Mitsubishi)", "欧姆龙 (Omron)", "台达 (Delta)" });
        cmbBrandTemplate.Location = new Point(120, 87);
        cmbBrandTemplate.Name = "cmbBrandTemplate";
        cmbBrandTemplate.Size = new Size(200, 31);
        cmbBrandTemplate.TabIndex = 1;
        // 
        // progressBar
        // 
        progressBar.Location = new Point(31, 130);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(821, 23);
        progressBar.TabIndex = 2;
        progressBar.Visible = false;
        // 
        // lblStatus
        // 
        lblStatus.AutoSize = true;
        lblStatus.Location = new Point(31, 160);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(0, 23);
        lblStatus.TabIndex = 3;
        lblStatus.Text = "";
        lblStatus.Visible = false;
        // 
        // btnExportErrors
        // 
        btnExportErrors.Enabled = false;
        btnExportErrors.Location = new Point(31, 190);
        btnExportErrors.Name = "btnExportErrors";
        btnExportErrors.Size = new Size(200, 35);
        btnExportErrors.TabIndex = 4;
        btnExportErrors.Text = "导出错误行";
        btnExportErrors.UseVisualStyleBackColor = true;
        btnExportErrors.Visible = false;
        btnExportErrors.Click += btnExportErrors_Click;
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(10F, 23F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(889, 518);
        Controls.Add(btnExportErrors);
        Controls.Add(lblStatus);
        Controls.Add(progressBar);
        Controls.Add(cmbBrandTemplate);
        Controls.Add(lblBrand);
        Controls.Add(btnProcess);
        AllowDrop = true;
        DragEnter += Form1_DragEnter;
        DragLeave += Form1_DragLeave;
        DragDrop += Form1_DragDrop;
        Name = "Form1";
        Text = "PanasonicToHmi工具";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}
