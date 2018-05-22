using System.Security.Principal;

namespace GVFS.Windows
{
    public partial class WindowsPlatform
    {
        public static bool IsElevatedImplementation()
        {
            using (WindowsIdentity id = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}
