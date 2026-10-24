using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace LocalKey
{
    internal static class FillIntegration
    {
        private static Form chooser;
        private const string Username = "demo@example.invalid";
        private const string Password = "Demo-中文-42🙂!";
        // A separate child process hosts only fake controls. Real vault data is never read.
        internal static void Target(string report, bool unmarked = false)
        {
            using (Form form = new Form { Text = "LocalKey controlled test target", ClientSize = new Size(400, 160),
                StartPosition = FormStartPosition.Manual, Location = new Point(100, 120), TopMost = true, ShowInTaskbar = false })
            using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 100 })
            {
                TextBox username = new TextBox { Location = new Point(20, 20), Width = 300, TabIndex = 0, AccessibleName = "Test username" };
                TextBox password = new TextBox { Location = new Point(20, 60), Width = 300, TabIndex = 1, UseSystemPasswordChar = !unmarked, AccessibleName = "Test password" };
                TextBox captcha = new TextBox { Location = new Point(20, 100), Width = 300, TabIndex = 2, AccessibleName = "Test captcha" };
                form.Controls.Add(username); form.Controls.Add(password); form.Controls.Add(captcha);
                bool submitted = false; form.KeyPreview = true;
                form.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) submitted = true; };
                form.Shown += delegate { username.Focus(); };
                DateTime end = DateTime.UtcNow.AddSeconds(30);
                timer.Tick += delegate
                {
                    if (username.Text == Username && password.Text == Password)
                    {
                        File.WriteAllText(report, submitted || captcha.Text != "" ? "FAIL: submitted or modified captcha" : "PASS: exact Unicode values; no Enter sent; captcha untouched");
                        timer.Stop(); form.Close();
                    }
                    else if (DateTime.UtcNow > end)
                    { File.WriteAllText(report, "FAIL: target timed out"); timer.Stop(); form.Close(); }
                };
                timer.Start(); Application.Run(form);
            }
        }
        internal static int Run(string report, bool unmarked = false)
        {
            IntPtr original = Native.GetForegroundWindow();
            string result = report + ".target"; Process child = null;
            try
            {
                if (File.Exists(result)) File.Delete(result);
                child = Process.Start(new ProcessStartInfo(Application.ExecutablePath, (unmarked ? "--test-target-compat" : "--test-target") + " \"" + result + "\"") { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
                IntPtr window = IntPtr.Zero;
                for (int i = 0; i < 80; i++)
                {
                    child.Refresh(); window = child.MainWindowHandle;
                    if (window == IntPtr.Zero)
                    {
                        AutomationElement root = AutomationElement.RootElement;
                        AutomationElement childWindow = root.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, child.Id));
                        if (childWindow != null) window = new IntPtr(childWindow.Current.NativeWindowHandle);
                    }
                    if (window != IntPtr.Zero) break; Thread.Sleep(100);
                }
                if (window == IntPtr.Zero) throw new Exception("Cannot find controlled test target");
                AutomationElement top = AutomationElement.FromHandle(window);
                AutomationElement user = top.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Test username"));
                if (user == null) throw new Exception("UI Automation cannot find test username");
                Native.ShowWindow(window, 9); user.SetFocus(); Native.SetForegroundWindow(window); Thread.Sleep(250);
                System.Windows.Point userPoint = user.GetClickablePoint();
                ClickOwnedWindow(window, new Point((int)userPoint.X, (int)userPoint.Y));
                if (Native.GetForegroundWindow() != window) throw new Exception("Cannot activate controlled test target");
                FillTarget captured = FillTarget.Capture();
                if (captured == null) throw new Exception("Cannot capture test focus");
                AutomationElement secret = top.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Test password"));
                secret.SetFocus(); Thread.Sleep(100);
                bool changedFocusRejected = false;
                try { captured.Fill(new Credential { Password = "must-not-be-typed" }, FillMode.Password); }
                catch (InvalidOperationException) { changedFocusRejected = true; }
                if (!changedFocusRejected) throw new Exception("Focus identity change was not rejected");
                user.SetFocus(); ClickOwnedWindow(window, new Point((int)userPoint.X, (int)userPoint.Y)); captured = FillTarget.Capture();
                if (captured == null) throw new Exception("Cannot recapture test focus");
                bool registered;
                captured = CaptureWithHotkey(captured, out registered);
                if (captured == null) throw new Exception("Global hotkey did not capture target focus");
                SimulateChooser();
                if (unmarked)
                {
                    bool rejected = false;
                    try { captured.Fill(new Credential { Username = Username, Password = Password }, FillMode.Both); }
                    catch (PasswordFieldNotRecognizedException) { rejected = true; }
                    if (!rejected || ((ValuePattern)secret.GetCurrentPattern(ValuePattern.Pattern)).Current.Value != "")
                        throw new Exception("Automatic fill did not reject the unmarked field");
                    captured = FillTarget.Capture(); if (captured == null) throw new Exception("Cannot capture explicitly selected unmarked edit");
                    SimulateChooser(); captured.Fill(new Credential { Password = Password }, FillMode.Password, true);
                }
                else captured.Fill(new Credential { Username = Username, Password = Password }, FillMode.Both);
                if (!child.WaitForExit(5000)) throw new Exception("Target did not receive expected Unicode text");
                string outcome = File.ReadAllText(result);
                File.WriteAllText(report, (registered ? "PASS: real Ctrl+Alt+Space captured the test field" : "SKIP: global hotkey occupied; no shortcut sent to running app")
                    + "\r\nPASS: changed focus rejected before typing\r\n" + (unmarked ? "PASS: automatic fill rejected unmarked edit; explicit single-field fill succeeded\r\n" : "") + outcome);
                return outcome.StartsWith("PASS:", StringComparison.Ordinal) ? 0 : 1;
            }
            catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex.ToString() + "\r\n" + ex.Data["FocusDiagnostic"] + "\r\n" + ex.Data["InputDiagnostic"]); return 1; }
            finally
            {
                if (child != null) { if (!child.HasExited) child.Kill(); child.Dispose(); }
                if (File.Exists(result)) File.Delete(result);
                if (Native.IsWindow(original)) Native.SetForegroundWindow(original);
            }
        }
        internal static int Browser(string report, bool unmarked = false)
        {
            IntPtr original = Native.GetForegroundWindow(); Process browser = null;
            string stage = "launch";
            string temporary = Path.Combine(Path.GetTempPath(), "LocalKey-edge-test-" + Guid.NewGuid().ToString("N"));
            string testToken = Guid.NewGuid().ToString("N");
            string profile = Path.Combine(temporary, "profile");
            Directory.CreateDirectory(temporary);
            try
            {
                string html = "<!doctype html><meta charset='utf-8'><title>LocalKey browser test " + testToken + "</title>"
                    + "<label>Test username<input id='u' aria-label='Test username' autocomplete='off'></label>"
                    + "<label>Test password<input id='p' type='" + (unmarked ? "text" : "password") + "' aria-label='Test password' autocomplete='off'></label>"
                    + "<input id='captcha' aria-label='Test captcha'>"
                    + "<script>let submitted=false; document.onkeydown=e=>{if(e.key==='Enter'){submitted=true;document.title='FAIL: submitted';}};"
                    + "document.oninput=()=>{if(!submitted&&document.getElementById('u').value==='" + Username
                    + "'&&document.getElementById('p').value==='" + Password + "'&&document.getElementById('captcha').value==='')document.title='LocalKey browser test PASS " + testToken + "';};</script>";
                string page = Path.Combine(temporary, "form.html"); File.WriteAllText(page, html, System.Text.Encoding.UTF8);
                string edge = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft\\Edge\\Application\\msedge.exe");
                if (!File.Exists(edge)) throw new Exception("Microsoft Edge is unavailable");
                browser = Process.Start(new ProcessStartInfo(edge,
                    "--no-first-run --no-default-browser-check --force-renderer-accessibility --user-data-dir=\"" + profile
                    + "\" --app=\"" + new Uri(page).AbsoluteUri + "\" --window-size=700,400") { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Normal });
                AutomationElement top = null, user = null;
                for (int i = 0; i < 80; i++)
                {
                    top = null;
                    AutomationElementCollection candidates = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
                    foreach (AutomationElement candidate in candidates)
                    {
                        if (!candidate.Current.Name.Contains(testToken)) continue;
                        if (top != null) throw new Exception("More than one controlled test window matched");
                        top = candidate;
                    }
                    if (top != null) user = top.FindFirst(TreeScope.Descendants, new AndCondition(
                        new PropertyCondition(AutomationElement.NameProperty, "Test username"),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)));
                    if (user != null) break; Thread.Sleep(100);
                }
                if (top == null || user == null) throw new Exception("Edge test page did not expose its input field");
                if (browser.Id != top.Current.ProcessId) { browser.Dispose(); browser = Process.GetProcessById(top.Current.ProcessId); }
                IntPtr window = new IntPtr(top.Current.NativeWindowHandle);
                Native.ShowWindow(window, 9); user.SetFocus(); Native.SetForegroundWindow(window); Thread.Sleep(250);
                System.Windows.Point userPoint = user.GetClickablePoint();
                ClickOwnedWindow(window, new Point((int)userPoint.X, (int)userPoint.Y));
                if (Native.GetForegroundWindow() != window) throw new Exception("Cannot activate controlled Edge window");
                FillTarget captured = FillTarget.Capture();
                if (captured == null) throw new Exception("Cannot capture Edge input focus");
                stage = "switch to controlled chooser";
                SimulateChooser();
                stage = "fill username and password";
                if (unmarked)
                {
                    bool refused = false;
                    try { captured.Fill(new Credential { Username = Username, Password = Password }, FillMode.Both); }
                    catch (PasswordFieldNotRecognizedException) { refused = true; }
                    if (!refused) throw new Exception("Automatic browser fill accepted an unmarked field");
                    AutomationElement focused = top.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.NameProperty, "Test password"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)));
                    if (((ValuePattern)focused.GetCurrentPattern(ValuePattern.Pattern)).Current.Value != "") throw new Exception("Unmarked field was modified before explicit selection");
                    focused.SetFocus(); Thread.Sleep(100);
                    captured = FillTarget.Capture(); if (captured == null) throw new Exception("Cannot capture selected browser edit");
                    SimulateChooser(); captured.Fill(new Credential { Password = Password }, FillMode.Password, true);
                }
                else captured.Fill(new Credential { Username = Username, Password = Password }, FillMode.Both);
                Thread.Sleep(500);
                if (!Native.WindowTitle(window).Contains("LocalKey browser test PASS")) throw new Exception("Edge page did not confirm the exact Unicode values");
                File.WriteAllText(report, unmarked ? "PASS: Edge unmarked edit rejected by automatic mode; explicit single-field Unicode fill succeeded; captcha untouched; no auto-submit"
                    : "PASS: Microsoft Edge HTML inputs: username + Tab + protected Unicode password; captcha untouched; no auto-submit");
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(report, "FAIL at " + stage + ": " + ex.ToString() + "\r\n" + ex.Data["FocusDiagnostic"]); return 1; }
            finally
            {
                if (browser != null)
                {
                    if (!browser.HasExited) { browser.CloseMainWindow(); if (!browser.WaitForExit(5000)) browser.Kill(); }
                    browser.Dispose();
                }
                if (Native.IsWindow(original)) Native.SetForegroundWindow(original);
                string full = Path.GetFullPath(temporary);
                string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (full.StartsWith(parent, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("LocalKey-edge-test-", StringComparison.Ordinal))
                { try { Directory.Delete(full, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            }
        }
        internal static void SimulateChooser()
        {
            if (chooser == null) chooser = new Form { Text = "LocalKey test chooser", ShowInTaskbar = false,
                TopMost = true, StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(200, 100) };
            chooser.Show(); chooser.Activate(); Application.DoEvents();
            ClickOwnedWindow(chooser.Handle, chooser.PointToScreen(new Point(80, 60)));
            chooser.Hide(); Pump(200);
        }
        internal static void ClickOwnedWindow(IntPtr window, Point location)
        {
            Native.POINT original;
            Native.GetCursorPos(out original);
            Native.POINT target = new Native.POINT { x = location.X, y = location.Y };
            try
            {
                Native.SetCursorPos(target.x, target.y);
                if (Native.GetAncestor(Native.WindowFromPoint(target), 2) != window) throw new Exception("Controlled click target is covered; click cancelled");
                SendMouse(2); Pump(100); SendMouse(4); Pump(120);
            }
            finally
            {
                Native.POINT current; Native.GetCursorPos(out current);
                if (current.x == target.x && current.y == target.y) Native.SetCursorPos(original.x, original.y);
            }
        }
        private static void SendMouse(uint flag)
        {
            Native.INPUT[] input = new Native.INPUT[] { new Native.INPUT { type = 0, data = new Native.InputUnion { mouse = new Native.MOUSEINPUT { flags = flag } } } };
            if (Native.SendInput(1, input, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.INPUT))) != 1) throw new Exception("Test mouse input refused");
        }
        internal static void Pump(int milliseconds)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(milliseconds);
            do { Application.DoEvents(); Thread.Sleep(10); } while (DateTime.UtcNow < end);
        }
        private static FillTarget CaptureWithHotkey(FillTarget existing, out bool registered)
        {
            using (HotkeyWindow listener = new HotkeyWindow())
            {
                registered = listener.Registered;
                if (!registered) return existing;
                FillTarget result = null;
                listener.Pressed += delegate { result = FillTarget.Capture(); };
                ushort[] keys = new ushort[] { 0x11, 0x12, 0x20, 0x20, 0x12, 0x11 };
                Native.INPUT[] input = keys.Select((key, index) => new Native.INPUT { type = 1,
                    data = new Native.InputUnion { keyboard = new Native.KEYBDINPUT { virtualKey = key, flags = index < 3 ? 0u : 2u } } }).ToArray();
                if (Native.SendInput((uint)input.Length, input, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.INPUT))) != input.Length)
                    throw new Exception("Cannot inject controlled shortcut test");
                DateTime deadline = DateTime.UtcNow.AddSeconds(2);
                while (result == null && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(20); }
                return result;
            }
        }
    }
}
