using Mono.Cecil;
using Mono.Cecil.Cil;

if(args.Length==3&&args[0]=="--sharex") {
 using var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
 using var sharex=AssemblyDefinition.ReadAssembly(args[1],new ReaderParameters{AssemblyResolver=resolver});
 if(sharex.Name.Name!="ShareX"||sharex.Name.HasPublicKey)throw new InvalidOperationException("Unexpected ShareX assembly identity.");
 var program=sharex.MainModule.Types.Single(t=>t.FullName=="ShareX.Program");
 var method=program.Methods.Single(m=>m.Name=="UpdatePersonalPath");
 var access=method.Body.Instructions.Where(i=>i.Operand is MethodReference m&&m.Name=="get_PersonalPath").ToArray();
 if(access.Length!=2||access[0].Operand is not MethodReference getter)throw new InvalidOperationException("Unexpected ShareX personal-path logic.");
 // Ignore an installed copy's registry path only in this portable build. The
 // bundled PersonalPath.cfg then selects our isolated directory without editing HKCU.
 if(getter.HasThis){access[0].OpCode=OpCodes.Pop;access[0].Operand=null;method.Body.GetILProcessor().InsertAfter(access[0],Instruction.Create(OpCodes.Ldstr,""));}
 else{access[0].OpCode=OpCodes.Ldstr;access[0].Operand="";}
 var migration=method.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.Name=="MigratePersonalPathConfig");migration.OpCode=OpCodes.Nop;migration.Operand=null;
 string temporary=args[2]+".patching";sharex.Write(temporary);File.Move(temporary,args[2],true);Console.WriteLine("PASS ShareX portable path isolation overrides installed-copy registry preferences.");
 return;
}
if(args.Length!=2)throw new ArgumentException("Expected pristine File Converter directory and destination directory.");
using var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(args[0],"FileConverterExtension.dll"));
if(assembly.Name.Name!="FileConverterExtension")throw new InvalidOperationException("Unexpected File Converter assembly identity.");
var module=assembly.MainModule;
var helper=module.Types.Single(t=>t.FullName=="FileConverterExtension.PathHelpers");
var path=helper.Methods.Single(m=>m.Name=="get_FileConverterPath");
if(!path.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="get_FileConverterRegistryKey"))throw new InvalidOperationException("Portable patch requires the pristine registry-dependent getter.");
var reflectionAssembly=new TypeReference("System.Reflection","Assembly",module,module.TypeSystem.CoreLibrary);
var current=new MethodReference("GetExecutingAssembly",reflectionAssembly,reflectionAssembly){HasThis=false};
var location=new MethodReference("get_Location",module.TypeSystem.String,reflectionAssembly){HasThis=true};
var pathType=new TypeReference("System.IO","Path",module,module.TypeSystem.CoreLibrary);
var directory=new MethodReference("GetDirectoryName",module.TypeSystem.String,pathType){HasThis=false};directory.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
var combine=new MethodReference("Combine",module.TypeSystem.String,pathType){HasThis=false};
combine.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));combine.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
path.Body=new MethodBody(path);var il=path.Body.GetILProcessor();
il.Emit(OpCodes.Call,current);il.Emit(OpCodes.Callvirt,location);il.Emit(OpCodes.Call,directory);il.Emit(OpCodes.Ldstr,"FileConverter.exe");il.Emit(OpCodes.Call,combine);il.Emit(OpCodes.Ret);
var data=helper.Methods.Single(m=>m.Name=="get_GetUserDataFolderPath");
var segment=data.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldstr&&Equals(i.Operand,"FileConverter"));
segment.Operand="InternalToolbox\\OpenSource\\FileConverter";
// This is a locally modified build, not an upstream-signed binary. Remove the
// upstream strong name and its EXE reference together instead of leaving an invalid signature.
void RemoveStrongName(AssemblyDefinition target){target.Name.PublicKey=[];target.Name.HasPublicKey=false;target.MainModule.Attributes&=~ModuleAttributes.StrongNameSigned;}
RemoveStrongName(assembly);
assembly.Write(Path.Combine(args[1],"FileConverterExtension.dll"));
using var application=AssemblyDefinition.ReadAssembly(Path.Combine(args[0],"FileConverter.exe"));
var reference=application.MainModule.AssemblyReferences.Single(r=>r.Name=="FileConverterExtension");reference.PublicKeyToken=[];reference.HasPublicKey=false;
RemoveStrongName(application);application.Write(Path.Combine(args[1],"FileConverter.exe"));
Console.WriteLine("PASS File Converter portable adaptation: executable-relative resources; isolated user settings; no registry installation required.");
