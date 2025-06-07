using System.Windows.Forms;
using System;
namespace 松下变量转HMI;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [System.STAThread]
    static void Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "启动异常");
        }
    }    
}