using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace LocalKey
{
    [DataContract]
    public sealed class Credential
    {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Title = "";
        [DataMember] public string Username = "";
        [DataMember] public string Password = "";
        [DataMember] public string Site = "";
        [DataMember] public string Notes = "";
        public Credential Copy() { return (Credential)MemberwiseClone(); }
    }
    [DataContract]
    internal sealed class VaultDocument
    {
        [DataMember] public int Version = 1;
        [DataMember] public List<Credential> Entries = new List<Credential>();
    }
    public sealed class Vault : IDisposable
    {
        private const int MaxFileBytes = 8 * 1024 * 1024;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("LKDP0001");
        // Application namespace, not an encryption key. Windows protects the user key.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("LocalKey/CurrentUser/v1");
        private readonly string path;
        public List<Credential> Entries { get; private set; }
        public bool Unlocked { get; private set; }
        public bool Exists { get { return File.Exists(path); } }
        public string FilePath { get { return path; } }
        public Vault(string path) { this.path = Path.GetFullPath(path); Entries = new List<Credential>(); }
        public void Create()
        {
            if (Exists) throw new IOException("密码库已经存在。");
            Lock(); Unlocked = true;
            try { Save(); } catch { Lock(); throw; }
        }
        public void Unlock()
        {
            Lock(); byte[] all = null, encrypted = null, plain = null;
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists || info.Length > MaxFileBytes || info.Length < 28) throw new InvalidDataException("密码库大小或格式无效。");
                all = File.ReadAllBytes(path);
                if (!Magic.SequenceEqual(all.Take(8))) throw new InvalidDataException("不支持的密码库格式。");
                int length = BitConverter.ToInt32(all, 8);
                if (length <= 0 || length != all.Length - 12) throw new InvalidDataException("密码库结构无效。");
                encrypted = new byte[length]; Buffer.BlockCopy(all, 12, encrypted, 0, length);
                plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                VaultDocument document;
                using (MemoryStream stream = new MemoryStream(plain)) document = (VaultDocument)new DataContractJsonSerializer(typeof(VaultDocument)).ReadObject(stream);
                ValidateDocument(document); Entries = document.Entries; Unlocked = true;
            }
            finally { Wipe(all); Wipe(encrypted); Wipe(plain); if (!Unlocked) Lock(); }
        }
        public void Save()
        {
            if (!Unlocked) throw new InvalidOperationException("密码库尚未打开。");
            VaultDocument document = new VaultDocument { Entries = Entries }; ValidateDocument(document);
            byte[] plain = null, encrypted = null;
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (MemoryStream stream = new MemoryStream())
                { new DataContractJsonSerializer(typeof(VaultDocument)).WriteObject(stream, document); plain = stream.ToArray(); }
                encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
                if (encrypted.Length + 12 > MaxFileBytes) throw new InvalidDataException("密码库已达到大小上限。");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (BinaryWriter writer = new BinaryWriter(stream))
                { writer.Write(Magic); writer.Write(encrypted.Length); writer.Write(encrypted); writer.Flush(); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true); else File.Move(temp, path);
            }
            finally { Wipe(plain); Wipe(encrypted); if (File.Exists(temp)) File.Delete(temp); }
        }
        public void Lock()
        {
            Unlocked = false;
            foreach (Credential entry in Entries) { entry.Password = ""; entry.Username = ""; entry.Notes = ""; }
            Entries.Clear();
        }
        public void Dispose() { Lock(); }
        private static void ValidateDocument(VaultDocument doc)
        {
            if (doc == null || doc.Version != 1 || doc.Entries == null || doc.Entries.Count > 10000) throw new InvalidDataException("密码库内容无效。");
            HashSet<string> ids = new HashSet<string>();
            foreach (Credential e in doc.Entries)
                if (e == null || String.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id) || String.IsNullOrWhiteSpace(e.Title)
                    || e.Title.Length > 200 || e.Username == null || e.Username.Length > 4096 || e.Password == null || e.Password.Length > 4096
                    || e.Site == null || e.Site.Length > 2000 || e.Notes == null || e.Notes.Length > 10000) throw new InvalidDataException("账号记录格式无效。");
        }
        internal static byte[] RandomBytes(int length)
        { byte[] data = new byte[length]; using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(data); return data; }
        private static void Wipe(byte[] data) { if (data != null) Array.Clear(data, 0, data.Length); }
    }
}
