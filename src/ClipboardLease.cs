using System;
using System.IO;
using System.Windows.Forms;

namespace LocalKey
{
    internal interface IClipboardBackend
    {
        uint Sequence { get; }
        void Write(string value);
        void Clear();
    }
    internal sealed class WindowsClipboard : IClipboardBackend
    {
        public uint Sequence { get { return Native.GetClipboardSequenceNumber(); } }
        internal static DataObject CreateData(string value)
        {
            DataObject data = new DataObject();
            data.SetData(DataFormats.UnicodeText, false, value);
            data.SetData("CanIncludeInClipboardHistory", false, new MemoryStream(BitConverter.GetBytes(0)));
            data.SetData("CanUploadToCloudClipboard", false, new MemoryStream(BitConverter.GetBytes(0)));
            return data;
        }
        public void Write(string value) { Clipboard.SetDataObject(CreateData(value), true, 3, 80); }
        public void Clear() { Clipboard.Clear(); }
    }
    internal sealed class ClipboardLease : IDisposable
    {
        private readonly IClipboardBackend backend;
        private uint sequence;
        private DateTime expires;
        private bool owns;
        internal ClipboardLease(IClipboardBackend backend) { this.backend = backend; }
        internal void Copy(string value, DateTime now)
        {
            backend.Write(value); sequence = backend.Sequence;
            owns = true; expires = now.AddSeconds(20);
        }
        internal void Tick(DateTime now) { if (owns && now >= expires) ClearOwned(); }
        internal void ClearOwned()
        {
            if (!owns) return;
            // Never clear a value that another app/user copied after ours.
            if (backend.Sequence != sequence) { owns = false; return; }
            backend.Clear(); owns = false;
        }
        public void Dispose() { try { ClearOwned(); } catch (System.Runtime.InteropServices.ExternalException) { } }
    }
}
