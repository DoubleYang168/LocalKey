using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LocalKey
{
    internal static class SelfTest
    {
        internal static int Run(string reportPath)
        {
            List<string> results = new List<string>();
            string directory = Path.Combine(Path.GetTempPath(), "LocalKey-selftest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "vault.lkey");
            int code = 0;
            try
            {
                Check(Marshal.SizeOf(typeof(Native.INPUT)) == (IntPtr.Size == 8 ? 40 : 28), "SendInput structure layout", results);
                Credential dummy = new Credential { Title = "虚构测试账号", Username = "demo@example.invalid", Password = "Test-中文-密码-42🙂!", Site = "https://example.invalid", Notes = "仅用于自动测试" };
                byte[] first, second;
                using (Vault vault = new Vault(path))
                {
                    vault.Create();
                    vault.Entries.Add(dummy.Copy()); vault.Save(); first = File.ReadAllBytes(path);
                    vault.Save(); second = File.ReadAllBytes(path);
                    Check(!first.SequenceEqual(second), "DPAPI uses fresh randomized encryption on every save", results);
                    Check(File.Exists(path + ".bak"), "encrypted backup created", results);
                    string bytes = Encoding.UTF8.GetString(second);
                    Check(!bytes.Contains(dummy.Username) && !bytes.Contains(dummy.Password) && !bytes.Contains(dummy.Title), "no plaintext entry fields in file", results);
                    vault.Lock(); Check(!vault.Unlocked && vault.Entries.Count == 0, "lock clears session and entries", results);
                    vault.Unlock();
                    Check(vault.Entries.Count == 1 && vault.Entries[0].Password == dummy.Password && vault.Entries[0].Username == dummy.Username, "Unicode encrypted roundtrip", results);
                    byte[] corrupt = (byte[])second.Clone(); corrupt[corrupt.Length - 8] ^= 1; File.WriteAllBytes(path, corrupt);
                    bool tamperRejected = false;
                    try { vault.Unlock(); } catch (System.Security.Cryptography.CryptographicException) { tamperRejected = true; }
                    Check(tamperRejected && !vault.Unlocked, "DPAPI tampering rejected and session stays closed", results);
                    byte[] malformed = (byte[])second.Clone(); malformed[8] ^= 1; File.WriteAllBytes(path, malformed);
                    bool formatRejected = false;
                    try { vault.Unlock(); } catch (InvalidDataException) { formatRejected = true; }
                    Check(formatRejected && !vault.Unlocked, "invalid envelope length rejected", results);
                    File.Copy(path + ".bak", path, true); vault.Unlock();
                    Check(vault.Entries[0].Password == dummy.Password, "encrypted backup recovery", results);
                    vault.Entries.Add(vault.Entries[0].Copy());
                    byte[] before = File.ReadAllBytes(path);
                    bool invalidRejected = false;
                    try { vault.Save(); } catch (InvalidDataException) { invalidRejected = true; }
                    Check(invalidRejected && before.SequenceEqual(File.ReadAllBytes(path)), "failed save preserves original file", results);
                    vault.Entries.RemoveAt(vault.Entries.Count - 1);
                    vault.Entries[0].Password = "edited-dummy-value"; vault.Save(); vault.Lock(); vault.Unlock();
                    Check(vault.Entries.Count == 1 && vault.Entries[0].Password == "edited-dummy-value", "edited entry persists after reopening", results);
                    vault.Entries.Clear(); vault.Save(); vault.Lock(); vault.Unlock();
                    Check(vault.Entries.Count == 0, "deleted entry stays deleted after reopening", results);
                }
                bool controlsRejected = false;
                try { FillTarget.ValidateText("hello\nworld"); } catch (InvalidOperationException) { controlsRejected = true; }
                Check(controlsRejected, "control characters rejected by autofill", results);
                FillTarget.ValidateText("中文-🙂-!@#$%");
                Check(true, "Unicode autofill input validation", results);
                HashSet<string> generated = new HashSet<string>();
                for (int i = 0; i < 200; i++)
                { string value = EntryDialog.GeneratePassword(); if (value.Length != 24 || !generated.Add(value)) throw new Exception("Random password generator failed."); }
                Check(true, "random password generation: 200 unique values of 24 characters", results);
                ClipboardChecks(results);
            }
            catch (Exception ex) { results.Add("FAIL: " + ex.GetType().Name + ": " + ex.Message); code = 1; }
            finally
            {
                // Delete only files created under the unique test directory; never touch the user's vault.
                foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
                File.WriteAllLines(reportPath, results, new UTF8Encoding(false));
            }
            return code;
        }
        private static void Check(bool condition, string name, List<string> report)
        { if (!condition) throw new Exception(name); report.Add("PASS: " + name); }
        internal static void Render(string directory)
        {
            Directory.CreateDirectory(directory);
            string unique = Path.Combine(Path.GetTempPath(), "LocalKey-render-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(unique);
            using (Vault vault = new Vault(Path.Combine(unique, "preview.lkey")))
            using (MainForm form = new MainForm(vault, true))
            {
                PrepareRender(form);
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(directory, "welcome.png")); }
                vault.Entries.Add(new Credential { Title = "示例 · 邮箱", Username = "demo@example.invalid", Password = "dummy" });
                vault.Entries.Add(new Credential { Title = "示例 · 工作后台", Username = "demo-user", Password = "dummy" });
                System.Reflection.MethodInfo refresh = typeof(MainForm).GetMethod("RefreshEntries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                refresh.Invoke(form, null); Application.DoEvents();
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(directory, "manager.png")); }
            }
            using (EntryDialog form = new EntryDialog(new Credential { Title = "示例账号", Username = "demo@example.invalid", Password = "dummy-password", Site = "example.invalid", Notes = "预览使用虚构账号" }))
            {
                PrepareRender(form);
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(directory, "entry.png")); }
            }
            foreach (string file in Directory.GetFiles(unique)) File.Delete(file);
            Directory.Delete(unique);
        }
        private static void PrepareRender(Form form)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000); form.ShowInTaskbar = false;
            form.Show(); Application.DoEvents(); form.PerformLayout();
        }
        private sealed class FakeClipboard : IClipboardBackend
        {
            public uint Sequence { get; private set; }
            internal string Text;
            public void Write(string value) { Text = value; Sequence++; }
            public void Clear() { Text = ""; Sequence++; }
        }
        private static void ClipboardChecks(List<string> results)
        {
            FakeClipboard backend = new FakeClipboard(); DateTime now = DateTime.UtcNow;
            using (ClipboardLease lease = new ClipboardLease(backend))
            {
                lease.Copy("dummy-secret", now); lease.Tick(now.AddSeconds(19));
                Check(backend.Text == "dummy-secret", "clipboard lease remains available before 20 seconds (fake backend)", results);
                lease.Tick(now.AddSeconds(20));
                Check(backend.Text == "", "owned clipboard is cleared at expiry (fake backend)", results);
                lease.Copy("dummy-secret", now); backend.Write("user-copied-other-text"); lease.Tick(now.AddSeconds(30));
                Check(backend.Text == "user-copied-other-text", "clipboard expiry preserves later user content (fake backend)", results);
                lease.Copy("dummy-secret", now); lease.ClearOwned();
                Check(backend.Text == "", "session clear removes only owned clipboard (fake backend)", results);
            }
            DataObject data = WindowsClipboard.CreateData("dummy-secret");
            Check((string)data.GetData(DataFormats.UnicodeText) == "dummy-secret"
                && BitConverter.ToInt32(((MemoryStream)data.GetData("CanIncludeInClipboardHistory")).ToArray(), 0) == 0
                && BitConverter.ToInt32(((MemoryStream)data.GetData("CanUploadToCloudClipboard")).ToArray(), 0) == 0,
                "clipboard data requests exclusion from Windows history and cloud sync", results);
        }
    }
}
