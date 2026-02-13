using System.Diagnostics;
using System.Runtime.InteropServices;
using Recode.Core.Services.Power;

namespace Recode.Infrastructure.Services.Power;

public class PowerService : IPowerService
{
    public void Shutdown()
    {
        Process.Start("shutdown", "/s /t 0");
    }

    public void Sleep()
    {
        SetSuspendState(false, false, false);
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
}
