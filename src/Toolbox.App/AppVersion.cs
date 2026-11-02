namespace Toolbox.App;
public static class AppVersion {
 public static Version Current {get;}=Normalize(typeof(AppVersion).Assembly.GetName().Version??new Version(1,0,0));
 public static string Display=>Current.ToString(3);
 public static Version Normalize(Version version)=>new(version.Major,version.Minor,Math.Max(0,version.Build));
}
