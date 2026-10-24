using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace LocalKey
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--self-test") { Environment.Exit(SelfTest.Run(args[1])); return; }
            if (args.Length == 2 && args[0] == "--render-preview") { SelfTest.Render(args[1]); return; }
            if (args.Length == 2 && args[0] == "--test-target") { FillIntegration.Target(args[1]); return; }
            if (args.Length == 2 && args[0] == "--test-target-compat") { FillIntegration.Target(args[1], true); return; }
            if (args.Length == 2 && args[0] == "--integration-test") { Environment.Exit(FillIntegration.Run(args[1])); return; }
            if (args.Length == 2 && args[0] == "--browser-test") { Environment.Exit(FillIntegration.Browser(args[1])); return; }
            if (args.Length == 2 && args[0] == "--compat-test") { Environment.Exit(FillIntegration.Run(args[1], true)); return; }
            if (args.Length == 2 && args[0] == "--browser-compat-test") { Environment.Exit(FillIntegration.Browser(args[1], true)); return; }
            if (args.Length == 2 && args[0] == "--point-test") { Environment.Exit(PointIntegration.Run(args[1])); return; }
            bool created;
            using (Mutex single = new Mutex(true, "Local\\LocalKey-" + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value, out created))
            {
                if (!created) { MessageBox.Show("LocalKey 已在运行。若正在升级，请先右键托盘旧版盾牌图标，选择「退出 LocalKey」，再运行新版。", "LocalKey 0.2"); return; }
                try
                {
                    string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalKey");
                    using (Vault vault = new Vault(Path.Combine(directory, "vault.lkey")))
                    using (MainForm form = new MainForm(vault)) Application.Run(form);
                }
                catch { MessageBox.Show("LocalKey 无法启动。请检查本地文件权限和 .NET Framework 4.8。未记录账号密码或诊断日志。", "LocalKey"); }
                finally { single.ReleaseMutex(); }
            }
        }
    }
}
