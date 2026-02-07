// 引入 WPF 应用程序类
using System.Windows;

namespace PlcToHmi
{
    // 程序入口类
    public class Program
    {
        // [STAThread] 表示此程序的入口线程必须使用单线程单元模式（Single Thread Apartment）
        // 这是 WPF 程序的要求，因为 WPF 的 UI 控件必须在创建它们的线程上访问
        [STAThread]
        public static void Main()
        {
            try
            {
                // 创建 WPF 应用程序实例
                var app = new Application();
                // 创建主窗口实例
                var mainWindow = new MainWindow();
                // 运行应用程序，显示主窗口并开始消息循环
                // 程序会一直运行，直到主窗口关闭
                app.Run(mainWindow);
            }
            catch (Exception ex)
            {
                // 如果启动过程发生异常，显示错误信息
                MessageBox.Show(ex.ToString(), "启动异常");
            }
        }
    }
}