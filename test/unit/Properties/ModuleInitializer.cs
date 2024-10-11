using System.Runtime.CompilerServices;

namespace GitSync.GitLab.Tests.Properties;

static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize() =>
        ClipboardAccept.Enable();
}
