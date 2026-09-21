using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace DrillIntel.Data
{
    internal static class DataServiceInitializer
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            try
            {
                AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var path = Path.Combine(baseDir, $"{assemblyName.Name}.dll");
                    if (File.Exists(path))
                        return context.LoadFromAssemblyPath(path);

                    var libsDir = Path.Combine(baseDir, "libs", $"{assemblyName.Name}.dll");
                    if (File.Exists(libsDir))
                        return context.LoadFromAssemblyPath(libsDir);

                    return null;
                };

                AssemblyLoadContext.Default.ResolvingUnmanagedDll += (assembly, unmanagedDllName) =>
                {
                    if (string.Equals(unmanagedDllName, "e_sqlite3", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(unmanagedDllName, "libe_sqlite3", StringComparison.OrdinalIgnoreCase))
                    {
                        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        var rid = Environment.Is64BitProcess ? "win-x64" : "win-x86";

                        var candidates = new[]
                        {
                            Path.Combine(baseDir, "runtimes", rid, "native", "e_sqlite3.dll"),
                            Path.Combine(baseDir, "e_sqlite3.dll"),
                            Path.Combine(baseDir, "libs", "runtimes", rid, "native", "e_sqlite3.dll"),
                            Path.Combine(baseDir, "libs", "e_sqlite3.dll")
                        };

                        foreach (var path in candidates)
                        {
                            if (File.Exists(path))
                                return NativeLibrary.Load(path);
                        }
                    }
                    return IntPtr.Zero;
                };

                InitializeNativeProvider();
            }
            catch
            {
                // Best effort
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeNativeProvider()
        {
            try
            {
                SQLitePCL.Batteries_V2.Init();
            }
            catch
            {
                // Ignore if already initialized
            }
        }
    }
}

