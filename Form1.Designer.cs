namespace 松下变量转HMI;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.Button btnProcess;

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
        // Form1
        // 
        AutoScaleDimensions = new SizeF(10F, 23F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(889, 518);
        Controls.Add(btnProcess);
        Name = "Form1";
        Text = "Form1";
        ResumeLayout(false);
    }

    #endregion
}
