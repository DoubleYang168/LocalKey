using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace LocalKey
{
    internal static class Native
    {
        internal const int WM_HOTKEY = 0x0312;
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] internal static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(POINT point);
        [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")] internal static extern bool GetGUIThreadInfo(uint thread, ref GUITHREADINFO info);
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] internal static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, string lParam);

        [StructLayout(LayoutKind.Sequential)] internal struct INPUT { public uint type; public InputUnion data; }
        [StructLayout(LayoutKind.Explicit)] internal struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT keyboard;
            [FieldOffset(0)] public MOUSEINPUT mouse;
            [FieldOffset(0)] public HARDWAREINPUT hardware;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT { public ushort virtualKey, scan; public uint flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct MOUSEINPUT { public int x, y; public uint mouseData, flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct HARDWAREINPUT { public uint message; public ushort low, high; }
        [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] internal struct GUITHREADINFO { public uint size, flags; public IntPtr active, focus, capture, menuOwner, moveSize, caret; public RECT rect; }
        [StructLayout(LayoutKind.Sequential)] internal struct LASTINPUTINFO { public uint size, time; }
        internal static string WindowTitle(IntPtr handle) { StringBuilder b = new StringBuilder(512); GetWindowText(handle, b, b.Capacity); return b.ToString(); }
        internal static bool ModifiersDown()
        { return new int[] { 0x10, 0x11, 0x12, 0x5B, 0x5C, 0x0D, 0x20, 0x01 }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0); }
        internal static string ModifierDiagnostic()
        { return String.Join(",", new int[] { 0x10, 0x11, 0x12, 0x5B, 0x5C, 0x0D, 0x20, 0x01 }.Where(k => (GetAsyncKeyState(k) & 0x8000) != 0).Select(k => k.ToString("X2"))); }
        internal static uint IdleMilliseconds()
        { LASTINPUTINFO info = new LASTINPUTINFO { size = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) }; return GetLastInputInfo(ref info) ? unchecked((uint)Environment.TickCount - info.time) : 0; }
    }

    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        internal event Action Pressed;
        internal bool Registered { get; private set; }
        internal HotkeyWindow()
        {
            CreateHandle(new CreateParams { Caption = "LocalKey.Hotkey" });
            Registered = Native.RegisterHotKey(Handle, 1, 0x4000 | 0x0002 | 0x0001, 0x20);
        }
        protected override void WndProc(ref Message m)
        { if (m.Msg == Native.WM_HOTKEY && Pressed != null) Pressed(); base.WndProc(ref m); }
        public void Dispose() { if (Registered) Native.UnregisterHotKey(Handle, 1); DestroyHandle(); }
    }

    internal enum FillMode { Password, Username, Both }

    internal sealed class PasswordFieldNotRecognizedException : InvalidOperationException
    {
        internal PasswordFieldNotRecognizedException() : base("未识别到密码框。可点击「定点填密码（3秒）」后再点击网页密码框，或使用「复制密码」粘贴。") { }
    }

    internal sealed class FillTarget
    {
        internal IntPtr Window;
        internal uint Process;
        internal IntPtr FocusHandle;
        internal AutomationElement Element;
        internal int[] RuntimeId;
        internal string Title;

        internal static FillTarget Capture()
        {
            IntPtr window = Native.GetForegroundWindow(); uint process;
            uint thread = Native.GetWindowThreadProcessId(window, out process);
            if (window == IntPtr.Zero || process == (uint)System.Diagnostics.Process.GetCurrentProcess().Id) return null;
            try
            {
                AutomationElement element = AutomationElement.FocusedElement;
                if (element == null || element.Current.ProcessId != process || !element.Current.IsKeyboardFocusable) return null;
                Native.GUITHREADINFO gui = new Native.GUITHREADINFO { size = (uint)Marshal.SizeOf(typeof(Native.GUITHREADINFO)) };
                if (!Native.GetGUIThreadInfo(thread, ref gui)) return null;
                return new FillTarget { Window = window, Process = process, FocusHandle = gui.focus, Element = element,
                    RuntimeId = element.GetRuntimeId(), Title = Native.WindowTitle(window) };
            }
            catch (Exception ex) { if (ex is ElementNotAvailableException || ex is InvalidOperationException || ex is UnauthorizedAccessException || ex is COMException) return null; throw; }
        }

        internal void Restore()
        {
            uint process;
            if (!Native.IsWindow(Window) || Native.GetWindowThreadProcessId(Window, out process) == 0 || process != Process)
                throw new InvalidOperationException("原来的窗口已经关闭，请回到输入框重新唤起。");
            if (Native.IsIconic(Window)) Native.ShowWindow(Window, 9);
            if (!Native.SetForegroundWindow(Window)) throw new InvalidOperationException("无法切回目标窗口，请回到输入框重新唤起。");
            Thread.Sleep(250);
            // Restoring a focus that the user or page changed could redirect a secret. Require the original identity.
            AssertFocus(RuntimeId, false);
        }

        private AutomationElement AssertFocus(int[] expected, bool password)
        {
            uint process;
            Native.GetWindowThreadProcessId(Window, out process);
            if (Native.GetForegroundWindow() != Window || process != Process)
            {
                InvalidOperationException changed = new InvalidOperationException("目标窗口发生变化，已停止填写。");
                uint currentProcess; Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out currentProcess);
                changed.Data["FocusDiagnostic"] = String.Format("foregroundPid={0}; targetPid={1}; targetExists={2}", currentProcess, Process, Native.IsWindow(Window));
                throw changed;
            }
            AutomationElement focused = AutomationElement.FocusedElement;
            if (focused == null || focused.Current.ProcessId != Process || !focused.Current.IsEnabled
                || !focused.Current.IsKeyboardFocusable || (expected != null && !expected.SequenceEqual(focused.GetRuntimeId())))
            {
                InvalidOperationException error = new InvalidOperationException("输入焦点发生变化，已停止填写。请重新选择输入框。");
                error.Data["FocusDiagnostic"] = focused == null ? "null" : String.Format("pid={0}/{1}; enabled={2}; focusable={3}; runtimeMatch={4}",
                    focused.Current.ProcessId, Process, focused.Current.IsEnabled, focused.Current.IsKeyboardFocusable,
                    expected == null || expected.SequenceEqual(focused.GetRuntimeId()));
                throw error;
            }
            if (password && !IsPasswordField(focused))
                throw new PasswordFieldNotRecognizedException();
            if (!password && focused.Current.ControlType != ControlType.Edit && focused.Current.ControlType != ControlType.ComboBox)
                throw new InvalidOperationException("当前焦点不是可识别的输入框，请点击表单输入框后重新唤起。");
            object valuePattern;
            if (focused.TryGetCurrentPattern(ValuePattern.Pattern, out valuePattern) && ((ValuePattern)valuePattern).Current.IsReadOnly)
                throw new InvalidOperationException("输入框为只读，无法填写。");
            return focused;
        }

        internal static bool IsPasswordField(AutomationElement element)
        {
            if (element.Current.IsPassword) return true;
            return false;
        }

        internal void Fill(Credential entry, FillMode mode)
        { Fill(entry, mode, false); }

        internal void Fill(Credential entry, FillMode mode, bool explicitlySelectedField)
        {
            if (explicitlySelectedField && mode != FillMode.Password)
                throw new InvalidOperationException("定点填写仅支持单个密码输入框。");
            string first = mode == FillMode.Password ? entry.Password : entry.Username;
            ValidateText(first);
            if (mode == FillMode.Both) ValidateText(entry.Password);
            DateTime until = DateTime.UtcNow.AddSeconds(3);
            while (Native.ModifiersDown() && DateTime.UtcNow < until) Thread.Sleep(20);
            if (Native.ModifiersDown()) throw new InvalidOperationException("请松开鼠标、Ctrl、Alt、Shift 等按键后再填写。");
            Restore();
            int[] current = RuntimeId;
            Type(first, current, mode == FillMode.Password && !explicitlySelectedField);
            if (mode == FillMode.Both)
            {
                AssertFocus(current, false);
                Key(0x09);
                // Browser focus notifications can lag behind Tab. Wait without advancing again.
                DateTime focusDeadline = DateTime.UtcNow.AddMilliseconds(900);
                AutomationElement next = null;
                do
                {
                    Thread.Sleep(50);
                    try { next = AssertFocus(null, true); }
                    catch (InvalidOperationException) { if (DateTime.UtcNow >= focusDeadline) throw; }
                } while (next == null);
                current = next.GetRuntimeId();
                if (RuntimeId.SequenceEqual(current)) throw new InvalidOperationException("Tab 没有切换到下一输入框，已停止填写密码。");
                Type(entry.Password, current, true);
            }
        }
        internal static void ValidateText(string text)
        {
            if (String.IsNullOrEmpty(text)) throw new InvalidOperationException("所选记录的待填内容为空。");
            if (text.Any(Char.IsControl)) throw new InvalidOperationException("内容包含换行或控制字符，无法用键盘安全填写。");
        }
        private void Type(string value, int[] identity, bool password)
        {
            // Send one Unicode scalar at a time so a window/focus change interrupts subsequent characters.
            // Existing text is preserved: the user can select text before invoking the shortcut to replace it.
            for (int i = 0; i < value.Length; i++)
            {
                AssertFocus(identity, password);
                if (Native.ModifiersDown())
                {
                    InvalidOperationException keyChanged = new InvalidOperationException("检测到按键操作，已停止填写；输入框可能已有部分内容。");
                    keyChanged.Data["InputDiagnostic"] = "modifiers=" + Native.ModifierDiagnostic() + "; sent=" + i;
                    throw keyChanged;
                }
                int count = Char.IsHighSurrogate(value[i]) && i + 1 < value.Length && Char.IsLowSurrogate(value[i + 1]) ? 2 : 1;
                Native.INPUT[] input = new Native.INPUT[count * 2];
                for (int j = 0; j < count; j++)
                {
                    input[j * 2] = Unicode(value[i + j], false);
                    input[j * 2 + 1] = Unicode(value[i + j], true);
                }
                Send(input);
                i += count - 1;
            }
        }
        private static Native.INPUT Unicode(char c, bool up)
        { return new Native.INPUT { type = 1, data = new Native.InputUnion { keyboard = new Native.KEYBDINPUT { scan = c, flags = 0x0004u | (up ? 0x0002u : 0u) } } }; }
        private static void Key(ushort key)
        { Send(new Native.INPUT[] { new Native.INPUT { type = 1, data = new Native.InputUnion { keyboard = new Native.KEYBDINPUT { virtualKey = key } } }, new Native.INPUT { type = 1, data = new Native.InputUnion { keyboard = new Native.KEYBDINPUT { virtualKey = key, flags = 2 } } } }); }
        private static void Send(Native.INPUT[] inputs)
        {
            if (Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Native.INPUT))) != inputs.Length)
                throw new InvalidOperationException("Windows 拒绝了模拟输入。管理员窗口或受保护窗口可能无法填写；输入框可能已有部分内容。");
        }
    }
}
