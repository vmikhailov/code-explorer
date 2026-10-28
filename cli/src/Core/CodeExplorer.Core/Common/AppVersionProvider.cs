using System.Reflection;

namespace CodeExplorer.Common;

public static class AppVersionProvider
{
    public static string GetAppVersion()
    {
        var asm = typeof(AppVersionProvider).Assembly;
        var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVer))
        {
            return infoVer.Split('+')[0];
        }

        var ver = asm.GetName().Version;
        return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
    }
}
