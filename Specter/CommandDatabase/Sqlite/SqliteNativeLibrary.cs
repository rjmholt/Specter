using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Specter.CommandDatabase.Sqlite
{
    /// <summary>
    /// Ensures the native e_sqlite3 library can be found when the Specter
    /// assembly is loaded inside a host process (such as PowerShell) whose
    /// deps.json does not include the SQLitePCLRaw RID-specific probe paths.
    /// Must be called before any <see cref="Microsoft.Data.Sqlite.SqliteConnection"/>
    /// is created.
    /// </summary>
    internal static class SqliteNativeLibrary
    {
#if NET
        private static readonly Lazy<bool> s_initialized = new Lazy<bool>(() =>
        {
            Assembly? providerAssembly = FindProviderAssembly();
            if (providerAssembly is not null)
            {
                NativeLibrary.SetDllImportResolver(providerAssembly, ResolveNativeLibrary);
            }

            return true;
        });
#endif

        public static void EnsureLoaded()
        {
#if NET
            _ = s_initialized.Value;
#endif
        }

#if NET
        private static Assembly? FindProviderAssembly()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "SQLitePCLRaw.provider.e_sqlite3")
                {
                    return asm;
                }
            }

            // The provider assembly may not be loaded yet. Force-load it so we
            // can register the resolver before the static ctor of SqliteConnection
            // triggers Batteries_V2.Init().
            string? assemblyDir = Path.GetDirectoryName(typeof(SqliteNativeLibrary).Assembly.Location);
            if (assemblyDir is null)
            {
                return null;
            }

            string providerPath = Path.Combine(assemblyDir, "SQLitePCLRaw.provider.e_sqlite3.dll");
            if (File.Exists(providerPath))
            {
                return Assembly.LoadFrom(providerPath);
            }

            return null;
        }

        private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (!libraryName.Contains("e_sqlite3"))
            {
                return IntPtr.Zero;
            }

            string? assemblyDir = Path.GetDirectoryName(typeof(SqliteNativeLibrary).Assembly.Location);
            if (assemblyDir is null)
            {
                return IntPtr.Zero;
            }

            // Try the exact RID first, then fall back to broader RIDs
            string rid = RuntimeInformation.RuntimeIdentifier;
            string[] ridsToTry = BuildRidFallbacks(rid);

            foreach (string candidate in ridsToTry)
            {
                string nativeDir = Path.Combine(assemblyDir, "runtimes", candidate, "native");
                if (!Directory.Exists(nativeDir))
                {
                    continue;
                }

                foreach (string libName in GetPlatformLibraryNames())
                {
                    string fullPath = Path.Combine(nativeDir, libName);
                    if (NativeLibrary.TryLoad(fullPath, out IntPtr handle))
                    {
                        return handle;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private static string[] BuildRidFallbacks(string rid)
        {
            // e.g. osx-arm64 -> try osx-arm64, osx, then common cross-platform
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.OSArchitecture == Architecture.Arm64
                    ? new[] { rid, "osx-arm64", "osx-x64" }
                    : new[] { rid, "osx-x64", "osx-arm64" };
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return RuntimeInformation.OSArchitecture == Architecture.Arm64
                    ? new[] { rid, "linux-arm64", "linux-x64", "linux-musl-arm64", "linux-musl-x64" }
                    : new[] { rid, "linux-x64", "linux-arm64", "linux-musl-x64", "linux-musl-arm64" };
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return RuntimeInformation.OSArchitecture == Architecture.Arm64
                    ? new[] { rid, "win-arm64", "win-x64", "win-x86" }
                    : new[] { rid, "win-x64", "win-x86", "win-arm64" };
            }

            return new[] { rid };
        }

        private static string[] GetPlatformLibraryNames()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new[] { "e_sqlite3.dll" };
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new[] { "libe_sqlite3.dylib", "e_sqlite3.dylib" };
            }

            return new[] { "libe_sqlite3.so", "e_sqlite3.so" };
        }
#endif
    }
}
