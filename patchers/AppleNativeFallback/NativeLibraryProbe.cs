using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

public enum NativeProbeState { Available, Unavailable, Inconclusive }

public sealed class NativeProbeResult
{
    public NativeProbeState State { get; private set; }
    public string Detail { get; private set; }
    public NativeProbeResult(NativeProbeState state, string detail) { State = state; Detail = detail; }
}

public static class NativeLibraryProbe
{
    [DllImport("/usr/lib/libSystem.B.dylib")] private static extern IntPtr dlopen(string path, int flags);
    [DllImport("/usr/lib/libSystem.B.dylib")] private static extern IntPtr dlsym(IntPtr handle, string symbol);
    [DllImport("/usr/lib/libSystem.B.dylib")] private static extern int dlclose(IntPtr handle);
    [DllImport("/usr/lib/libSystem.B.dylib")] private static extern IntPtr dlerror();
    [DllImport("/usr/lib/libSystem.B.dylib")] private static extern int sysctlbyname(
        string name, ref int value, ref UIntPtr size, IntPtr newValue, UIntPtr newSize);

    public static bool IsNativeArm64Mac()
    {
        if (!File.Exists("/usr/bin/sw_vers") || IntPtr.Size != 8) return false;
        int translated = 0;
        var size = new UIntPtr(4);
        if (sysctlbyname("sysctl.proc_translated", ref translated, ref size, IntPtr.Zero, UIntPtr.Zero) == 0 && translated != 0)
            return false;
        int cpuType = 0;
        size = new UIntPtr(4);
        return sysctlbyname("hw.cputype", ref cpuType, ref size, IntPtr.Zero, UIntPtr.Zero) == 0 && cpuType == 0x0100000c;
    }

    public static NativeProbeResult Check(string gameRoot, string name)
    {
        string[] exports;
        if (name == "AppleCoreNativeMac") exports = new[] { "AppleCore_GetRuntimeEnvironment" };
        else if (name == "GameKitWrapper") exports = new[] { "DefaultNSErrorHandler_Set", "DefaultNSExceptionHandler_Set" };
        else return new NativeProbeResult(NativeProbeState.Inconclusive, "Unsupported native library name");
        try
        {
            return Evaluate(name, Candidates(gameRoot, name), exports, Loadable);
        }
        catch (Exception e)
        {
            return new NativeProbeResult(NativeProbeState.Inconclusive,
                "Native probe failed for " + name + ": " + e.Message + ". Original methods retained.");
        }
    }

    public static IEnumerable<string> Candidates(string gameRoot, string name)
    {
        // Bare names also cover the process's configured dynamic-library search path.
        yield return name;
        yield return name + ".dylib";
        yield return "lib" + name + ".dylib";
        var contents = Path.Combine(gameRoot, "valheim.app/Contents");
        foreach (var directory in new[] { gameRoot, Path.Combine(contents, "PlugIns"), Path.Combine(contents, "Frameworks") })
        {
            foreach (var filename in new[] { name, name + ".dylib", "lib" + name + ".dylib" })
                yield return Path.Combine(directory, filename);
            yield return Path.Combine(directory, name + ".bundle/Contents/MacOS/" + name);
            yield return Path.Combine(directory, name + ".framework/" + name);
        }
    }

    public static NativeProbeResult Evaluate(string name, IEnumerable<string> paths, string[] exports,
        Func<string, string[], string> loader)
    {
        // null from loader means success; a message describes an actual loader failure.
        var failures = new List<string>();
        foreach (var path in paths.Distinct())
        {
            if (Path.IsPathRooted(path) && !File.Exists(path)) continue;
            string error;
            try { error = loader(path, exports); }
            catch (Exception e)
            {
                return new NativeProbeResult(NativeProbeState.Inconclusive,
                    "Could not probe " + name + ": " + e.Message + ". Original methods retained.");
            }
            if (error == null) return new NativeProbeResult(NativeProbeState.Available,
                name + " loads and provides required exports. Original methods retained.");
            if (Path.IsPathRooted(path)) failures.Add(Path.GetFileName(path) + ": " + error);
        }
        return new NativeProbeResult(NativeProbeState.Unavailable, name + " unavailable" +
            (failures.Count == 0 ? " (no loadable library found)." : ": " + string.Join("; ", failures)));
    }

    private static string Loadable(string path, string[] exports)
    {
        dlerror();
        var handle = dlopen(path, 5); // Darwin RTLD_LAZY | RTLD_LOCAL; no game functions are called.
        if (handle == IntPtr.Zero)
        {
            var error = dlerror();
            return error == IntPtr.Zero ? "dlopen failed" : Marshal.PtrToStringAnsi(error);
        }
        try
        {
            foreach (var export in exports)
                if (dlsym(handle, export) == IntPtr.Zero) return "missing export " + export;
            return null;
        }
        finally { dlclose(handle); }
    }
}
