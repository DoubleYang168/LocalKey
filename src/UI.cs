using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LocalKey
{
    internal static class Theme
    {
        internal static readonly Color Ink = Color.FromArgb(24, 39, 61);
        internal static readonly Color Accent = Color.FromArgb(20, 113, 112);
        internal static readonly Color Muted = Color.FromArgb(101, 113, 128);
        internal static readonly Color Paper = Color.FromArgb(247, 248, 250);
        internal static Button Button(string text, EventHandler click, bool primary)
        {
            Button button = new Button { Text = text, AutoSize = false, Height = 38, Width = 110,
                FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Color.White,
                ForeColor = primary ? Color.White : Ink, Cursor = Cursors.Hand, Margin = new Padding(4), TabStop = true };
            button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(216, 223, 230);
            button.Click += click; return button;
        }
        internal static Label Label(string text, float size, Color color)
        { return new Label { Text = text, Font = new Font("Microsoft YaHei UI", size), ForeColor = color, AutoSize = true, Margin = new Padding(0, 5, 0, 7) }; }
        internal static TextBox TextBox(bool secret)
        { return new TextBox { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 11), UseSystemPasswordChar = secret, Margin = new Padding(0, 3, 0, 10), MaxLength = 4096 }; }
    }

    internal sealed class EntryDialog : Form
    {
        private readonly TextBox title, username, password, site, notes;
        private readonly Credential original;
        internal Credential Result { get; private set; }
        internal EntryDialog(Credential entry)
        {
            original = entry;
            Text = entry == null ? "新建账号 · LocalKey" : "编辑账号 · LocalKey";
            Font = new Font("Microsoft YaHei UI", 10); ForeColor = Theme.Ink; BackColor = Theme.Paper;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(520, 600);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26), ColumnCount = 1, RowCount = 13 };
            title = AddField(layout, "名称 *", false); title.MaxLength = 200;
            username = AddField(layout, "账号 / 邮箱", false);
            password = AddField(layout, "密码", true);
            FlowLayoutPanel tools = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 38, AutoSize = true, Margin = new Padding(0) };
            CheckBox reveal = new CheckBox { Text = "显示密码", AutoSize = true, Margin = new Padding(0, 9, 12, 0) };
            reveal.CheckedChanged += delegate { password.UseSystemPasswordChar = !reveal.Checked; };
            Button generate = Theme.Button("生成随机密码", delegate { password.Text = GeneratePassword(); }, false); generate.Width = 140;
            tools.Controls.Add(reveal); tools.Controls.Add(generate); layout.Controls.Add(tools);
            site = AddField(layout, "网站 / 应用（用于自己识别）", false); site.MaxLength = 2000;
            notes = AddField(layout, "备注", false); notes.Multiline = true; notes.Height = 62; notes.MaxLength = 10000;
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            Button save = Theme.Button("保存", SaveEntry, true);
            Button cancel = Theme.Button("取消", delegate { DialogResult = DialogResult.Cancel; }, false);
            actions.Controls.Add(save); actions.Controls.Add(cancel); layout.Controls.Add(actions);
            Controls.Add(layout); CancelButton = cancel;
            if (entry != null) { title.Text = entry.Title; username.Text = entry.Username; password.Text = entry.Password; site.Text = entry.Site; notes.Text = entry.Notes; }
        }
        private static TextBox AddField(TableLayoutPanel layout, string caption, bool secret)
        { layout.Controls.Add(Theme.Label(caption, 9, Theme.Muted)); TextBox field = Theme.TextBox(secret); layout.Controls.Add(field); return field; }
        private void SaveEntry(object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace(title.Text)) { MessageBox.Show(this, "请给账号起一个名称。", "LocalKey"); title.Focus(); return; }
            Result = new Credential { Id = original == null ? Guid.NewGuid().ToString("N") : original.Id,
                Title = title.Text.Trim(), Username = username.Text, Password = password.Text, Site = site.Text.Trim(), Notes = notes.Text };
            DialogResult = DialogResult.OK;
        }
        internal static string GeneratePassword()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%*-_=+?";
            char[] output = new char[24]; int pos = 0; int limit = 256 - 256 % alphabet.Length;
            while (pos < output.Length)
            {
                byte[] random = Vault.RandomBytes(32);
                foreach (byte b in random) { if (b < limit) { output[pos++] = alphabet[b % alphabet.Length]; if (pos == output.Length) break; } }
                Array.Clear(random, 0, random.Length);
            }
            return new string(output);
        }
        protected override void Dispose(bool disposing)
        { if (disposing) { password.Clear(); username.Clear(); notes.Clear(); } base.Dispose(disposing); }
    }

    internal sealed class MainForm : Form
    {
        private readonly Vault vault;
        private readonly HotkeyWindow hotkey;
        private readonly NotifyIcon tray;
        private readonly Timer timer;
        private readonly Panel locked, manager;
        private readonly TextBox search;
        private readonly ListView list;
        private readonly Label status, targetLabel, countLabel;
        private readonly Button fillPassword, fillUsername, fillBoth, pointPassword, copyUsername, copyPassword;
        private readonly ClipboardLease clipboard = new ClipboardLease(new WindowsClipboard());
        private readonly Timer pointTimer;
        private CountdownForm countdown;
        private Credential pointEntry;
        private FillTarget pointTarget;
        private DateTime pointDeadline, captureAfter;
        private bool mouseArmed, mouseWasDown;
        private FillTarget target;
        private DateTime touched = DateTime.UtcNow;
        private bool exiting;
        internal MainForm(Vault vault, bool preview = false)
        {
            this.vault = vault;
            Text = "LocalKey 0.2 · 本地密码库";
            ClientSize = new Size(650, 780); MinimumSize = new Size(650, 780);
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Microsoft YaHei UI", 10);
            ForeColor = Theme.Ink; BackColor = Theme.Paper; KeyPreview = true;
            Icon = SystemIcons.Shield;

            TableLayoutPanel outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 20, 26, 16), ColumnCount = 1, RowCount = 4 };
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            FlowLayoutPanel header = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
            header.Controls.Add(new Label { Text = "LocalKey", AutoSize = true, Font = new Font("Segoe UI", 25, FontStyle.Bold), ForeColor = Theme.Ink, Margin = new Padding(0) });
            header.Controls.Add(Theme.Label("本地密码库   /   Ctrl + Alt + Space 随时唤起", 9, Theme.Muted));
            outer.Controls.Add(header, 0, 0);
            outer.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(220, 226, 232), Margin = new Padding(0) }, 0, 1);
            Panel body = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 18, 0, 0) };
            locked = new Panel { Dock = DockStyle.Fill };
            TableLayoutPanel gate = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(24, 28, 24, 0) };
            gate.Controls.Add(Theme.Label("本地密码库已收起", 18, Theme.Ink));
            Label hint = Theme.Label("使用当前 Windows 用户加密，无需另设主密码。", 10, Theme.Muted);
            hint.MaximumSize = new Size(500, 0); gate.Controls.Add(hint);
            Button open = Theme.Button("打开密码库", delegate { OpenVault(); }, true); open.Width = 160;
            gate.Controls.Add(open);
            locked.Controls.Add(gate);

            manager = new Panel { Dock = DockStyle.Fill, Visible = false };
            TableLayoutPanel content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            search = Theme.TextBox(false); search.MaxLength = 500;
            search.AccessibleName = "搜索账号名称、账号或网站";
            search.HandleCreated += delegate { Native.SendMessage(search.Handle, 0x1501, new IntPtr(1), "搜索名称、账号、网站或备注…"); };
            search.TextChanged += delegate { Touch(); RefreshEntries(); }; content.Controls.Add(search);
            FlowLayoutPanel toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            toolbar.Controls.Add(Theme.Button("+ 新建账号", delegate { Edit(false); }, true));
            toolbar.Controls.Add(Theme.Button("编辑", delegate { Edit(true); }, false));
            toolbar.Controls.Add(Theme.Button("删除", Delete, false));
            toolbar.Controls.Add(Theme.Button("收起到托盘", delegate { LockVault(); Hide(); }, false)); content.Controls.Add(toolbar);
            countLabel = Theme.Label("输入名称、账号或网站搜索", 9, Theme.Muted); content.Controls.Add(countLabel);
            list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false,
                HideSelection = false, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = Theme.Ink };
            list.Columns.Add("账号名称", 240); list.Columns.Add("账号 / 邮箱", 285);
            list.DoubleClick += delegate { Edit(true); };
            list.SelectedIndexChanged += delegate { UpdateFillButtons(); }; content.Controls.Add(list);
            targetLabel = Theme.Label("", 9, Theme.Muted); targetLabel.AutoSize = false; targetLabel.Dock = DockStyle.Fill;
            targetLabel.Padding = new Padding(0, 12, 0, 0); content.Controls.Add(targetLabel);
            FlowLayoutPanel fillActions = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            fillPassword = Theme.Button("填密码 ↵", delegate { Fill(FillMode.Password); }, true);
            fillUsername = Theme.Button("填账号", delegate { Fill(FillMode.Username); }, false);
            fillBoth = Theme.Button("账号 → 密码", delegate { Fill(FillMode.Both); }, false); fillBoth.Width = 150;
            fillActions.Controls.Add(fillPassword); fillActions.Controls.Add(fillUsername); fillActions.Controls.Add(fillBoth); content.Controls.Add(fillActions);
            FlowLayoutPanel recovery = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            pointPassword = Theme.Button("定点填密码（3秒）", delegate { BeginPointPassword(); }, false); pointPassword.Width = 178;
            copyUsername = Theme.Button("复制账号", delegate { CopyField(false); }, false);
            copyPassword = Theme.Button("复制密码", delegate { CopyField(true); }, false);
            recovery.Controls.Add(pointPassword); recovery.Controls.Add(copyUsername); recovery.Controls.Add(copyPassword); content.Controls.Add(recovery);
            content.Controls.Add(Theme.Label("网页识别失败时：选中账号 → 定点填密码 → 3秒内点击密码框。\n复制内容在20秒后尝试清除；不会自动提交。", 8, Theme.Muted));
            manager.Controls.Add(content); body.Controls.Add(manager); body.Controls.Add(locked); outer.Controls.Add(body, 0, 2);
            status = Theme.Label("", 9, Theme.Muted); status.AutoSize = false; status.Dock = DockStyle.Fill; status.Padding = new Padding(0, 10, 0, 0); outer.Controls.Add(status, 0, 3);
            Controls.Add(outer);

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("打开密码库", null, delegate { Present(null); });
            menu.Items.Add("收起并清除缓存", null, delegate { LockVault(); Hide(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出 LocalKey", null, delegate { exiting = true; Close(); });
            tray = new NotifyIcon { Text = "LocalKey · Ctrl+Alt+Space", Icon = SystemIcons.Shield, Visible = true, ContextMenuStrip = menu };
            tray.DoubleClick += delegate { Present(null); };
            hotkey = new HotkeyWindow();
            hotkey.Pressed += delegate
            {
                if (pointEntry != null) { CancelPointPassword(); Present(null); return; }
                if (OwnedForms.Length != 0) return;
                FillTarget captured = FillTarget.Capture();
                if (captured == null && Visible && ContainsFocus) { Hide(); return; }
                Present(captured);
            };
            timer = new Timer { Interval = 1000 };
            timer.Tick += delegate
            {
                try { clipboard.Tick(DateTime.UtcNow); } catch (System.Runtime.InteropServices.ExternalException) { }
                if (vault.Unlocked && (DateTime.UtcNow - touched > TimeSpan.FromMinutes(5) || Native.IdleMilliseconds() >= 300000)) LockVault();
            };
            timer.Start();
            pointTimer = new Timer { Interval = 30 };
            pointTimer.Tick += PointTick;
            Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
            KeyDown += OnKeyDown;
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };
            Shown += delegate
            {
                if (!hotkey.Registered && !preview) MessageBox.Show(this, "Ctrl+Alt+Space 被其他程序占用，无法注册。请先解除冲突后重启 LocalKey；仍可通过托盘管理密码库。", "快捷键未注册");
                OpenVault();
            };
            status.Text = "Windows 用户加密  ·  无需主密码  ·  数据只存本机"; UpdateTarget();
        }
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { CancelPointPassword(); Hide(); e.Handled = true; e.SuppressKeyPress = true; }
            else if (e.Alt && e.KeyCode == Keys.P) { BeginPointPassword(); e.Handled = true; e.SuppressKeyPress = true; }
            else if (e.Control && e.Shift && e.KeyCode == Keys.C) { CopyField(true); e.Handled = true; e.SuppressKeyPress = true; }
            else if (e.Control && e.Shift && e.KeyCode == Keys.U) { CopyField(false); e.Handled = true; e.SuppressKeyPress = true; }
            else if (search.Focused && (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up))
            {
                if (list.Items.Count > 0)
                {
                    int index = list.SelectedIndices.Count == 0 ? 0 : list.SelectedIndices[0];
                    index = Math.Max(0, Math.Min(list.Items.Count - 1, index + (e.KeyCode == Keys.Down ? 1 : -1)));
                    list.Items[index].Selected = true; list.Items[index].EnsureVisible();
                }
                e.Handled = true; e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true; e.SuppressKeyPress = true;
                if (!vault.Unlocked) OpenVault();
                else if (target != null && Selected() != null) Fill(e.Control ? FillMode.Both : FillMode.Password);
            }
            else if (e.Control && e.KeyCode == Keys.L) { LockVault(); e.Handled = true; }
        }
        private void Touch() { touched = DateTime.UtcNow; }
        private void Present(FillTarget captured)
        {
            target = captured; status.ForeColor = Theme.Muted; UpdateTarget();
            Show(); WindowState = FormWindowState.Normal; Activate();
            if (vault.Unlocked) { Touch(); search.Focus(); search.SelectAll(); }
            else OpenVault();
        }
        private void OpenVault()
        {
            try
            {
                if (!vault.Unlocked) { if (vault.Exists) vault.Unlock(); else vault.Create(); }
                locked.Visible = false; manager.Visible = true; manager.BringToFront();
                Touch(); RefreshEntries(); UpdateTarget(); search.Focus();
                status.ForeColor = Theme.Muted; status.Text = "Windows 用户加密  ·  无需主密码  ·  数据只存本机";
            }
            catch (Exception ex) { status.Text = SafeError(ex); MessageBox.Show(this, status.Text, "无法打开密码库"); }
        }
        private void RefreshEntries()
        {
            if (list == null || !vault.Unlocked) return;
            string selected = Selected() == null ? null : Selected().Id;
            string query = search.Text.Trim(); list.BeginUpdate(); list.Items.Clear();
            foreach (Credential entry in vault.Entries.OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase))
            {
                if (query.Length != 0 && new string[] { entry.Title, entry.Username, entry.Site, entry.Notes }.All(x => x.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0)) continue;
                ListViewItem item = new ListViewItem(new string[] { entry.Title, entry.Username }) { Tag = entry };
                list.Items.Add(item); if (entry.Id == selected) item.Selected = true;
            }
            if (list.SelectedItems.Count == 0 && list.Items.Count > 0) list.Items[0].Selected = true;
            countLabel.Text = vault.Entries.Count == 0 ? "还没有账号，点击「新建账号」开始。" : String.Format("{0} 条记录  /  共 {1} 条", list.Items.Count, vault.Entries.Count);
            list.EndUpdate(); UpdateFillButtons();
        }
        private Credential Selected() { return list == null || list.SelectedItems.Count == 0 ? null : (Credential)list.SelectedItems[0].Tag; }
        private void Edit(bool existing)
        {
            if (!vault.Unlocked) return; Touch();
            Credential selected = Selected(); if (existing && selected == null) return;
            using (EntryDialog dialog = new EntryDialog(existing ? selected : null))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || !vault.Unlocked) return;
                int index = existing ? vault.Entries.IndexOf(selected) : -1;
                Credential replacement = dialog.Result;
                if (existing) vault.Entries[index] = replacement; else vault.Entries.Add(replacement);
                try { vault.Save(); Touch(); status.Text = "已加密保存到本机。"; }
                catch (Exception ex)
                { if (existing) vault.Entries[index] = selected; else vault.Entries.Remove(replacement); status.Text = SafeError(ex); MessageBox.Show(this, status.Text, "保存失败"); }
                RefreshEntries();
            }
        }
        private void Delete(object sender, EventArgs e)
        {
            Credential selected = Selected(); if (!vault.Unlocked || selected == null) return; Touch();
            if (MessageBox.Show(this, "删除「" + selected.Title + "」？上一次保存的加密备份可能仍保留此记录。", "删除账号", MessageBoxButtons.OKCancel) != DialogResult.OK || !vault.Unlocked) return;
            int index = vault.Entries.IndexOf(selected); vault.Entries.RemoveAt(index);
            try { vault.Save(); status.Text = "记录已删除。"; }
            catch (Exception ex) { vault.Entries.Insert(index, selected); status.Text = SafeError(ex); }
            RefreshEntries();
        }
        private void UpdateTarget()
        {
            targetLabel.Text = target == null ? "管理模式：可复制账号/密码，或用「定点填密码」点击选框。\n自动填写：在目标输入框按 Ctrl+Alt+Space 唤起。"
                : "即将填写到：" + (String.IsNullOrWhiteSpace(target.Title) ? "未命名窗口" : target.Title) + "\n填写后不会自动提交。";
            UpdateFillButtons();
        }
        private void UpdateFillButtons()
        {
            if (fillPassword == null) return;
            bool selected = vault.Unlocked && Selected() != null;
            fillPassword.Enabled = fillUsername.Enabled = fillBoth.Enabled = selected && target != null;
            if (pointPassword != null) pointPassword.Enabled = copyUsername.Enabled = copyPassword.Enabled = selected;
        }
        private void Fill(FillMode mode)
        {
            Credential entry = Selected(); FillTarget captured = target;
            if (!vault.Unlocked || entry == null || captured == null) return;
            Touch(); Hide();
            try { captured.Fill(entry, mode); target = null; }
            catch (Exception ex)
            {
                ShowFillFailure(ex);
            }
        }
        private void ShowFillFailure(Exception ex)
        {
            target = null; Show(); Activate(); UpdateTarget();
            status.ForeColor = Color.FromArgb(155, 73, 22);
            status.Text = ex is InvalidOperationException ? ex.Message : "无法访问输入框。请用「定点填密码」重新选框，或复制密码粘贴。";
            status.Text += "\n若已有部分内容，请先检查，避免重复输入。";
        }
        private void CopyField(bool secret)
        {
            Credential entry = Selected(); if (!vault.Unlocked || entry == null) return;
            string value = secret ? entry.Password : entry.Username;
            if (String.IsNullOrEmpty(value)) { status.Text = secret ? "当前记录没有密码。" : "当前记录没有账号。"; return; }
            try
            {
                clipboard.Copy(value, DateTime.UtcNow); Touch(); status.ForeColor = Theme.Muted;
                status.Text = (secret ? "密码" : "账号") + "已复制，请到目标输入框按 Ctrl+V。\n20秒后尝试清除；如果你复制了其他内容，不会清除新内容。";
            }
            catch (System.Runtime.InteropServices.ExternalException) { status.Text = "剪贴板暂被占用，请稍后重试。"; }
        }
        private void BeginPointPassword()
        {
            Credential entry = Selected(); if (!vault.Unlocked || entry == null || pointEntry != null) return;
            try { FillTarget.ValidateText(entry.Password); }
            catch (InvalidOperationException ex) { status.Text = ex.Message; return; }
            Touch(); pointEntry = entry.Copy(); pointTarget = null;
            pointDeadline = DateTime.UtcNow.AddSeconds(3); captureAfter = DateTime.MaxValue;
            mouseArmed = (Native.GetAsyncKeyState(0x01) & 0x8000) == 0; mouseWasDown = false;
            Hide(); countdown = new CountdownForm(); countdown.UpdateSeconds(3, false); countdown.Show(); pointTimer.Start();
        }
        private void PointTick(object sender, EventArgs e)
        {
            if (pointEntry == null) return;
            if ((Native.GetAsyncKeyState(0x1B) & 0x8000) != 0) { CancelPointPassword(); return; }
            DateTime now = DateTime.UtcNow;
            bool down = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0;
            if (!mouseArmed) { if (!down) mouseArmed = true; }
            else if (down) { mouseWasDown = true; pointTarget = null; captureAfter = DateTime.MaxValue; }
            else if (mouseWasDown) { mouseWasDown = false; captureAfter = now.AddMilliseconds(100); }
            if (now >= captureAfter)
            {
                captureAfter = DateTime.MaxValue;
                try { pointTarget = FillTarget.Capture(); } catch { pointTarget = null; }
            }
            countdown.UpdateSeconds(Math.Max(0, (int)Math.Ceiling((pointDeadline - now).TotalSeconds)), pointTarget != null);
            if (now < pointDeadline) return;
            if ((down || captureAfter != DateTime.MaxValue) && now < pointDeadline.AddSeconds(1)) return;
            Credential pending = pointEntry;
            FillTarget selectedTarget = pointTarget;
            pointEntry = null; pointTarget = null; StopCountdown();
            try
            {
                if (selectedTarget == null) throw new InvalidOperationException("没有检测到有效的新点击，未填写。请重新点「定点填密码」，再点击目标密码框。");
                selectedTarget.Fill(pending, FillMode.Password, true);
            }
            catch (Exception ex) { ShowFillFailure(ex); }
            finally { pending.Password = ""; pending.Username = ""; pending.Notes = ""; }
        }
        private void StopCountdown()
        { pointTimer.Stop(); if (countdown != null) { countdown.Dispose(); countdown = null; } }
        private void CancelPointPassword()
        {
            if (pointEntry != null) { pointEntry.Password = ""; pointEntry.Username = ""; pointEntry.Notes = ""; }
            pointEntry = null; pointTarget = null; StopCountdown();
        }
        private void LockVault()
        {
            CancelPointPassword();
            try { clipboard.ClearOwned(); } catch (System.Runtime.InteropServices.ExternalException) { }
            foreach (Form dialog in OwnedForms) { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); }
            vault.Lock(); list.Items.Clear(); search.Clear();
            manager.Visible = false; locked.Visible = true; locked.BringToFront(); target = null;
            status.Text = "缓存已清除，再次打开无需主密码。";
        }
        private void OnSessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock || e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLogoff)
            { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)delegate { LockVault(); }); }
        }
        private static string SafeError(Exception ex)
        {
            if (ex is System.Security.Cryptography.CryptographicException) return "当前 Windows 用户无法解密此密码库，或文件已损坏。";
            if (ex is System.IO.InvalidDataException) return "密码库格式或内容无效。请保留原文件并检查加密备份。";
            if (ex is UnauthorizedAccessException || ex is System.IO.IOException) return "无法读写本地密码库，请检查文件权限、磁盘空间及备份。";
            return "操作失败，原文件将保留。请检查密码库文件与运行环境。";
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
                CancelPointPassword(); pointTimer.Dispose(); clipboard.Dispose();
                timer.Stop(); timer.Dispose(); hotkey.Dispose(); tray.Visible = false; tray.Dispose(); vault.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
