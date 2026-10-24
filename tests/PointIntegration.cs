using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace LocalKey
{
    internal static class PointIntegration
    {
        internal static int Run(string report)
        {
            string directory = Path.Combine(Path.GetTempPath(), "LocalKey-point-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); string result = Path.Combine(directory, "target.txt");
            IntPtr original = Native.GetForegroundWindow(); Process child = null;
            try
            {
                child = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--test-target-compat \"" + result + "\"")
                    { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
                AutomationElement top = null;
                for (int i = 0; i < 80 && top == null; i++)
                {
                    top = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, child.Id));
                    if (top == null) Thread.Sleep(100);
                }
                if (top == null) throw new Exception("Controlled point target unavailable");
                IntPtr window = new IntPtr(top.Current.NativeWindowHandle);
                ((TransformPattern)top.GetCurrentPattern(TransformPattern.Pattern)).Move(100, 120);
                Native.ShowWindow(window, 9);
                AutomationElement user = top.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Test username"));
                AutomationElement secret = top.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Test password"));
                ((ValuePattern)user.GetCurrentPattern(ValuePattern.Pattern)).SetValue("demo@example.invalid");
                using (Vault vault = new Vault(Path.Combine(directory, "test.lkey")))
                using (MainForm form = new MainForm(vault, true))
                {
                    form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-14000, -14000); form.ShowInTaskbar = false;
                    form.Show(); FillIntegration.Pump(100);
                    vault.Entries.Add(new Credential { Title = "Dummy test entry", Username = "demo@example.invalid", Password = "Demo-中文-42🙂!" });
                    Call(form, "RefreshEntries");
                    Call(form, "BeginPointPassword");
                    FillIntegration.Pump(3400);
                    if (((ValuePattern)secret.GetCurrentPattern(ValuePattern.Pattern)).Current.Value != "") throw new Exception("Countdown typed without a fresh click");
                    Call(form, "BeginPointPassword");
                    Native.SetForegroundWindow(window); secret.SetFocus(); FillIntegration.Pump(200);
                    System.Windows.Point point = secret.GetClickablePoint();
                    FillIntegration.ClickOwnedWindow(window, new Point((int)point.X, (int)point.Y));
                    DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                    while (!child.HasExited && DateTime.UtcNow < deadline) FillIntegration.Pump(50);
                    if (!child.HasExited) throw new Exception("Point countdown did not deliver the dummy password");
                    if (!File.ReadAllText(result).StartsWith("PASS:", StringComparison.Ordinal)) throw new Exception("Controlled target did not confirm values");
                }
                File.WriteAllText(report, "PASS: countdown without a fresh click sent nothing\r\nPASS: real new click selected an unmarked edit; countdown filled exact Unicode password; captcha untouched; no Enter sent");
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex.ToString()); return 1; }
            finally
            {
                if (child != null) { if (!child.HasExited) child.Kill(); child.Dispose(); }
                if (Native.IsWindow(original)) Native.SetForegroundWindow(original);
                foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
        private static void Call(MainForm form, string method)
        { typeof(MainForm).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null); }
    }
}
