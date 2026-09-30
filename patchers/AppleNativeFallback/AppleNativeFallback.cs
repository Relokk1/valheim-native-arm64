using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class AppleNativeFallback
{
    public const string Version = "0.1.0";
    public static IEnumerable<string> TargetDLLs
    {
        get { return new[] { "Apple.Core.dll", "Apple.GameKit.dll" }; }
    }

    public static void Patch(AssemblyDefinition assembly)
    {
        try
        {
            if (!NativeLibraryProbe.IsNativeArm64Mac())
            {
                Log("Skipped: this fallback targets native ARM64 macOS only.");
                return;
            }
            Apply(assembly, name => NativeLibraryProbe.Check(Paths.GameRootPath, name),
                () => ParseProductVersion(RunCommand("/usr/bin/sw_vers", "-productVersion")), Log);
        }
        catch (Exception error)
        {
            Log("Skipped " + assembly.Name.Name + ": " + error.GetType().Name + ": " + error.Message);
        }
    }

    // Probe/version delegates keep the safety checks testable without launching Unity.
    public static bool Apply(AssemblyDefinition assembly, Func<string, NativeProbeResult> probe,
        Func<System.Version> productVersion, Action<string> log)
    {
        var module = assembly.MainModule;
        bool core = assembly.Name.Name == "Apple.Core";
        if (!core && assembly.Name.Name != "Apple.GameKit") return false;
        var expected = core ? "c4ac282d-8b9c-4283-b952-e41bfb919e9c" : "c92debd5-87ae-4131-a65e-d26d423ed9f0";
        if (module.Mvid != Guid.Parse(expected))
        {
            log("Skipped " + module.Name + ": unsupported assembly MVID " + module.Mvid + ". No methods changed.");
            return false;
        }
        var result = probe(core ? "AppleCoreNativeMac" : "GameKitWrapper");
        log(result.Detail);
        if (result.State != NativeProbeState.Unavailable) return false;

        // Build and validate all replacements before assigning any method bodies.
        var replacements = new List<KeyValuePair<MethodDefinition, MethodBody>>();
        if (core)
        {
            var version = productVersion();
            if (version == null || version.Major < 1 || version.Minor < 0)
                throw new InvalidOperationException("Invalid macOS product version");
            var environment = RequiredType(module, "Apple.Core.RuntimeEnvironment");
            var osType = RequiredType(module, "Apple.Core.RuntimeOperatingSystem");
            var constructor = environment.Methods.Single(m => m.IsConstructor && !m.IsStatic &&
                m.Parameters.Count == 4 && m.Parameters[0].ParameterType.FullName == osType.FullName &&
                m.Parameters.Skip(1).All(p => p.ParameterType.FullName == "System.Int32"));
            var os = osType.Fields.Single(f => f.Name == "macOS" && f.HasConstant);
            var getter = RequiredType(module, "Apple.Core.Availability").Methods.Single(m =>
                m.Name == "get_RuntimeEnvironment" && m.IsStatic && m.Parameters.Count == 0 &&
                m.ReturnType.FullName == environment.FullName && m.HasBody);
            var body = new MethodBody(getter) { MaxStackSize = 4 };
            var il = body.GetILProcessor();
            il.Emit(OpCodes.Ldc_I4, Convert.ToInt32(os.Constant));
            il.Emit(OpCodes.Ldc_I4, version.Major);
            il.Emit(OpCodes.Ldc_I4, version.Minor);
            il.Emit(OpCodes.Ldc_I4, Math.Max(0, version.Build));
            il.Emit(OpCodes.Newobj, constructor);
            il.Emit(OpCodes.Ret);
            replacements.Add(new KeyValuePair<MethodDefinition, MethodBody>(getter, body));
        }
        else
        {
            foreach (var name in new[] { "DefaultNSErrorHandler", "DefaultNSExceptionHandler" })
            {
                var method = RequiredType(module, "Apple.GameKit." + name).Methods.Single(m =>
                    m.Name == "Init" && m.IsStatic && m.Parameters.Count == 0 &&
                    m.ReturnType.FullName == "System.Void" && m.HasBody);
                var body = new MethodBody(method);
                body.GetILProcessor().Emit(OpCodes.Ret);
                replacements.Add(new KeyValuePair<MethodDefinition, MethodBody>(method, body));
            }
        }
        foreach (var pair in replacements) pair.Key.Body = pair.Value;
        log(core ? "Patched Apple.Core OS query using the actual macOS product version." :
            "Skipped two GameKit startup handler registrations; their native library is unavailable.");
        return true;
    }

    public static System.Version ParseProductVersion(string text)
    {
        System.Version value;
        if (!System.Version.TryParse(text.Trim(), out value) || value.Major < 1 || value.Minor < 0)
            throw new InvalidOperationException("Cannot determine macOS product version");
        return value;
    }

    private static TypeDefinition RequiredType(ModuleDefinition module, string name)
    {
        var type = module.GetType(name);
        if (type == null) throw new InvalidOperationException("Unexpected assembly layout: missing " + name);
        return type;
    }

    private static string RunCommand(string executable, string arguments)
    {
        using (var process = Process.Start(new ProcessStartInfo(executable, arguments)
        { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }))
        {
            if (!process.WaitForExit(5000))
            {
                process.Kill();
                throw new InvalidOperationException(Path.GetFileName(executable) + " timed out");
            }
            if (process.ExitCode != 0) throw new InvalidOperationException(Path.GetFileName(executable) + " failed");
            return process.StandardOutput.ReadToEnd().Trim();
        }
    }

    private static void Log(string message)
    {
        Console.WriteLine("[AppleNativeFallback " + Version + "] " + message);
    }
}
