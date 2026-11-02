using System.IO;
using System.Reflection;
using Toolbox.App;

string root=Path.Combine(Path.GetTempPath(),"cz-theme-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int passed=0,failed=0;
void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);passed++;}catch(Exception error){Console.WriteLine("FAIL "+name+": "+error.Message);failed++;}}
void Save(string path,string mode){
 var method=typeof(ThemeManager).GetMethod("WritePreference",BindingFlags.Static|BindingFlags.NonPublic)??throw new Exception("Theme saving has no failure-reporting operation.");
 try{method.Invoke(null,[path,mode]);}catch(TargetInvocationException error) when(error.InnerException!=null){throw error.InnerException;}
}
Check("saves a preference in a dedicated path",()=>{
 string path=Path.Combine(root,"nested","theme.txt");Save(path,"dark");
 if(File.ReadAllText(path)!="dark")throw new Exception("Preference was not written.");
});
Check("an occupied preference reports failure and preserves the saved value",()=>{
 string path=Path.Combine(root,"locked.txt");File.WriteAllText(path,"system");
 using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)){
  bool rejected=false;try{Save(path,"light");}catch(InvalidOperationException error){rejected=error.Message.Contains("保存");}
  if(!rejected)throw new Exception("Failed writes must reach the UI.");
 }
 if(File.ReadAllText(path)!="system")throw new Exception("The existing preference changed after failure.");
});
Console.WriteLine($"Theme tests: {passed} passed, {failed} failed");
// Only the uniquely-created test directory is removed, after all handles close.
Directory.Delete(root,true);return failed==0?0:1;
