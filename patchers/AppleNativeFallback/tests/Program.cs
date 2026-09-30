using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Reflection;
using MA = Mono.Cecil.MethodAttributes;
using TA = Mono.Cecil.TypeAttributes;
using FA = Mono.Cecil.FieldAttributes;

var count = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
NativeProbeResult Probe(NativeProbeState state) => new(state, "fixture");
Dictionary<string,string> Bodies(AssemblyDefinition a) => a.MainModule.Types.SelectMany(t=>t.Methods)
    .Where(m=>m.HasBody).ToDictionary(m=>m.FullName,m=>string.Join(";",m.Body.Instructions.Select(i=>i.ToString())));
bool Same(Dictionary<string,string> before, AssemblyDefinition a) => before.OrderBy(x=>x.Key).SequenceEqual(Bodies(a).OrderBy(x=>x.Key));
void ExpectFailure(Action action, string name) { try { action(); } catch (InvalidOperationException) { Check(true,name); return; } throw new Exception(name); }

foreach (var core in new[] { true, false }) {
    using var a = Fixture(core); var before = Bodies(a); var logs = new List<string>();
    Check(AppleNativeFallback.Apply(a, _=>Probe(NativeProbeState.Unavailable), ()=>new Version(27,0,1), logs.Add), "applies supported " + a.Name.Name);
    var changed = before.Keys.Where(k=>before[k]!=Bodies(a)[k]).ToArray();
    Check(changed.Length == (core ? 1 : 2), "only expected methods change: " + a.Name.Name);
    using var bytes = new MemoryStream(); a.Write(bytes);
    using var reread = AssemblyDefinition.ReadAssembly(new MemoryStream(bytes.ToArray()));
    Check(reread.MainModule.Mvid == a.MainModule.Mvid, "serialization preserves identity");
    var loaded = Assembly.Load(bytes.ToArray());
    if (core) {
        var result = loaded.GetType("Apple.Core.Availability")!.GetProperty("RuntimeEnvironment")!.GetValue(null)!;
        Check((int)result.GetType().GetField("Major")!.GetValue(result)! == 27 &&
              (int)result.GetType().GetField("Patch")!.GetValue(result)! == 1, "managed getter returns actual version without native SDK");
    } else {
        foreach(var n in new[]{"DefaultNSErrorHandler","DefaultNSExceptionHandler"})
            loaded.GetType("Apple.GameKit."+n)!.GetMethod("Init")!.Invoke(null,null);
        Check(true,"both replaced callbacks execute without native SDK");
    }
    using var unknown = Fixture(core); unknown.MainModule.Mvid=Guid.NewGuid(); before=Bodies(unknown);
    Check(!AppleNativeFallback.Apply(unknown, _=>throw new Exception("probe should not run"), ()=>throw new Exception(), logs.Add) && Same(before,unknown), "unknown build skips without mutation");
    foreach(var state in new[]{NativeProbeState.Available,NativeProbeState.Inconclusive}) {
        using var skipped=Fixture(core); before=Bodies(skipped);
        Check(!AppleNativeFallback.Apply(skipped,_=>Probe(state),()=>throw new Exception(),logs.Add) && Same(before,skipped), state+" leaves original methods intact");
    }
}
using(var malformed=Fixture(false)) {
    malformed.MainModule.GetType("Apple.GameKit.DefaultNSExceptionHandler").Methods.Clear(); var before=Bodies(malformed);
    ExpectFailure(()=>AppleNativeFallback.Apply(malformed,_=>Probe(NativeProbeState.Unavailable),()=>new Version(27,0),Console.WriteLine), "unexpected layout rejected");
    Check(Same(before,malformed),"validation failure cannot partially patch GameKit");
}
using(var a=Fixture(true)) {
    var before=Bodies(a);
    ExpectFailure(()=>AppleNativeFallback.Apply(a,_=>Probe(NativeProbeState.Unavailable),()=>throw new InvalidOperationException("OS query failed"),Console.WriteLine),"OS query failure rejected");
    Check(Same(before,a),"OS query failure preserves original getter");
}
Check(AppleNativeFallback.ParseProductVersion(" 14.7\n").Build == -1,"two-part product versions supported");
ExpectFailure(()=>AppleNativeFallback.ParseProductVersion("Darwin 24"),"invalid product version rejected");
var candidates=NativeLibraryProbe.Candidates("/tmp/game","AppleCoreNativeMac").ToArray();
Check(candidates.Contains("/tmp/game/valheim.app/Contents/PlugIns/AppleCoreNativeMac.bundle/Contents/MacOS/AppleCoreNativeMac"),"Unity bundle executable included");
Check(NativeLibraryProbe.Evaluate("fixture",new[]{"fixture"},new[]{"export"},(_,__)=>null).State==NativeProbeState.Available,"loadable native library detected");
Check(NativeLibraryProbe.Evaluate("fixture",new[]{"fixture"},new[]{"export"},(_,__)=>"wrong architecture").State==NativeProbeState.Unavailable,"architecture/load failure enables fallback");
Check(NativeLibraryProbe.Evaluate("fixture",new[]{"fixture"},new[]{"export"},(_,__)=>"missing export").State==NativeProbeState.Unavailable,"missing exports detected");
Check(NativeLibraryProbe.Evaluate("fixture",new[]{"fixture"},new[]{"export"},(_,__)=>throw new IOException()).State==NativeProbeState.Inconclusive,"unexpected probe error is conservative");
var nativeRoot=Environment.GetEnvironmentVariable("APPLE_FALLBACK_NATIVE_FIXTURES");
if (nativeRoot != null) {
    Check(NativeLibraryProbe.IsNativeArm64Mac(),"current process is native ARM64 macOS");
    Check(NativeLibraryProbe.Check(Path.Combine(nativeRoot,"available"),"AppleCoreNativeMac").State==NativeProbeState.Available,"real ARM64 native loader success");
    Check(NativeLibraryProbe.Check(Path.Combine(nativeRoot,"wrong-arch"),"AppleCoreNativeMac").State==NativeProbeState.Unavailable,"real x86_64-only library rejected");
    Check(NativeLibraryProbe.Check(Path.Combine(nativeRoot,"missing-export"),"AppleCoreNativeMac").State==NativeProbeState.Unavailable,"real library missing required export rejected");
    Check(NativeLibraryProbe.Check(Path.Combine(nativeRoot,"absent"),"AppleCoreNativeMac").State==NativeProbeState.Unavailable,"real missing-library detection");
}
Console.WriteLine(count + " checks passed. No Unity/game process launched.");

static AssemblyDefinition Fixture(bool core) {
    var a=AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(core?"Apple.Core":"Apple.GameKit",new Version(1,0)),core?"Apple.Core.dll":"Apple.GameKit.dll",ModuleKind.Dll);
    var m=a.MainModule; m.Mvid=Guid.Parse(core?"c4ac282d-8b9c-4283-b952-e41bfb919e9c":"c92debd5-87ae-4131-a65e-d26d423ed9f0");
    MethodDefinition Method(TypeDefinition t,string name, TypeReference result, bool isStatic=true) {
        var x=new MethodDefinition(name,MA.Public|MA.HideBySig|(isStatic?MA.Static:0),result); t.Methods.Add(x); return x;
    }
    if(core) {
        var os=new TypeDefinition("Apple.Core","RuntimeOperatingSystem",TA.Public|TA.Sealed,m.ImportReference(typeof(Enum))); m.Types.Add(os);
        os.Fields.Add(new FieldDefinition("value__",FA.Public|FA.SpecialName|FA.RTSpecialName,m.TypeSystem.Int32));
        os.Fields.Add(new FieldDefinition("macOS",FA.Public|FA.Static|FA.Literal|FA.HasDefault,os){Constant=2});
        var env=new TypeDefinition("Apple.Core","RuntimeEnvironment",TA.Public|TA.Sealed|TA.SequentialLayout,m.ImportReference(typeof(ValueType))); m.Types.Add(env);
        var fields=new[]{new FieldDefinition("RuntimeOperatingSystem",FA.Public,os),new FieldDefinition("Major",FA.Public,m.TypeSystem.Int32),new FieldDefinition("Minor",FA.Public,m.TypeSystem.Int32),new FieldDefinition("Patch",FA.Public,m.TypeSystem.Int32)};
        foreach(var f in fields) env.Fields.Add(f);
        var ctor=Method(env,".ctor",m.TypeSystem.Void,false); ctor.Attributes|=MA.SpecialName|MA.RTSpecialName;
        foreach(var f in fields) ctor.Parameters.Add(new ParameterDefinition(f.FieldType));
        var il=ctor.Body.GetILProcessor();
        for(int i=0;i<4;i++){ il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg,ctor.Parameters[i]); il.Emit(OpCodes.Stfld,fields[i]); } il.Emit(OpCodes.Ret);
        var availability=new TypeDefinition("Apple.Core","Availability",TA.Public|TA.Abstract|TA.Sealed,m.TypeSystem.Object); m.Types.Add(availability);
        var getter=Method(availability,"get_RuntimeEnvironment",env); getter.Attributes|=MA.SpecialName;
        il=getter.Body.GetILProcessor(); for(int i=0;i<4;i++) il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Newobj,ctor); il.Emit(OpCodes.Ret);
        availability.Properties.Add(new PropertyDefinition("RuntimeEnvironment",Mono.Cecil.PropertyAttributes.None,env){GetMethod=getter});
    } else {
        foreach(var name in new[]{"DefaultNSErrorHandler","DefaultNSExceptionHandler"}) {
            var t=new TypeDefinition("Apple.GameKit",name,TA.Public,m.TypeSystem.Object); m.Types.Add(t);
            var il=Method(t,"Init",m.TypeSystem.Void).Body.GetILProcessor(); il.Emit(OpCodes.Nop); il.Emit(OpCodes.Ret);
        }
    }
    var extra=new TypeDefinition("Fixture","Unrelated",TA.Public,m.TypeSystem.Object); m.Types.Add(extra);
    var other=Method(extra,"Untouched",m.TypeSystem.Int32).Body.GetILProcessor(); other.Emit(OpCodes.Ldc_I4,42); other.Emit(OpCodes.Ret);
    return a;
}
