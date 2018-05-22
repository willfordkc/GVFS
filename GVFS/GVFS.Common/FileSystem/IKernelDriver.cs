using GVFS.Common.Tracing;

namespace GVFS.Common.FileSystem
{
    public interface IKernelDriver
    {
        string DriverLogFolderName { get; }

        string FlushDriverLogs();
        bool TryPrepareFolderForCallbacks(string folderPath, out string error);
        bool IsReady(JsonTracer tracer, string enlistmentRoot, out string error);
    }
}
