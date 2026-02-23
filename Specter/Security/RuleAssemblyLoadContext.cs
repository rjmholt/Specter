#if CORECLR
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Specter.Security
{
    /// <summary>
    /// Isolated, collectible assembly load context for external rule assemblies.
    /// Resolves managed dependencies only from the directory containing the rule assembly.
    ///
    /// This class only exists on .NET Core / .NET 5+ (CORECLR). On .NET Framework 4.6.2,
    /// assembly loading falls back to Assembly.LoadFile() in the default AppDomain -- the
    /// ALC isolation is not available. The path validation and ownership checks still apply
    /// on both targets; only the isolation boundary differs.
    /// </summary>
    internal sealed class RuleAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly string _basePath;

        internal RuleAssemblyLoadContext(string assemblyDirectory)
            : base(name: $"SpecterRuleALC:{Path.GetFileName(assemblyDirectory)}", isCollectible: true)
        {
            _basePath = assemblyDirectory;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string candidate = Path.Combine(_basePath, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
            {
                return LoadFromAssemblyPath(candidate);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string candidate = Path.Combine(_basePath, unmanagedDllName);
            if (File.Exists(candidate))
            {
                return LoadUnmanagedDllFromPath(candidate);
            }

            string withExtension = candidate + NativeLibraryExtension;
            if (File.Exists(withExtension))
            {
                return LoadUnmanagedDllFromPath(withExtension);
            }

            return IntPtr.Zero;
        }

        private static string NativeLibraryExtension =>
            OperatingSystem.IsWindows() ? ".dll"
            : OperatingSystem.IsMacOS() ? ".dylib"
            : ".so";
    }
}
#endif
