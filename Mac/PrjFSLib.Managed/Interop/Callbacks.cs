using System.Runtime.InteropServices;

namespace PrjFSLib.Managed.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Callbacks
    {
        public EnumerateDirectoryCallback OnEnumerateDirectory;
        public GetFileStreamCallback OnGetFileStream;
        public NotifyPreDeleteEvent OnNotifyPreDelete;
    }
}
