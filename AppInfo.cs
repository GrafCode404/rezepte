using System.Reflection;

namespace RezepteWeb;

public static class AppInfo
{
    public static string Version =>
        typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "dev";
}
