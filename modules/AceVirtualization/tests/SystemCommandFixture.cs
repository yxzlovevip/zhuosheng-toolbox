using System;
using System.IO;
internal static class SystemCommandFixture {
    public static int Main(string[] args) {
        string name=Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]).ToLowerInvariant();
        File.AppendAllText(Environment.GetEnvironmentVariable("CZ_ACE_FIXTURE_LOG"),name+" "+string.Join(" ",args)+Environment.NewLine);
        if(name=="bcdedit"&&args.Length>0&&args[0]=="/enum") { Console.WriteLine("hypervisorlaunchtype "+(Environment.GetEnvironmentVariable("CZ_ACE_FIXTURE_HYP")??"Auto"));return 0; }
        if(name=="bcdedit"&&Environment.GetEnvironmentVariable("CZ_ACE_FIXTURE_FAIL")=="hyp")return 5;
        if(name=="powershell"&&Environment.GetEnvironmentVariable("CZ_ACE_FIXTURE_FAIL")=="vbs")return 5;
        return 0;
    }
}
