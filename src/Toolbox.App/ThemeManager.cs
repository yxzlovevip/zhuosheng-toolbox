using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
namespace Toolbox.App;
public static class ThemeManager {
 public static string Mode {get;private set;}="system";
 static string SettingsPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"InternalToolbox","theme.txt");
 public static void Initialize() {
  try {string text=File.ReadAllText(SettingsPath).Trim(); if(text is "light" or "dark" or "system") Mode=text;} catch(IOException) {} catch(UnauthorizedAccessException) {}
  Apply();
  SystemEvents.UserPreferenceChanged+=(s,e)=>{if(Mode=="system") Application.Current?.Dispatcher.BeginInvoke(Apply);};
 }
 public static void Set(string mode,bool persist=true,bool reportFailure=false) {
  if(mode is not ("light" or "dark" or "system")) return;
  Mode=mode; Apply();
  if(persist) try {WritePreference(SettingsPath,mode);} catch(InvalidOperationException) when(!reportFailure) {}
 }
 internal static void WritePreference(string path,string mode) {
  string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try {Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(temporary,mode);File.Move(temporary,path,true);}
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {throw new InvalidOperationException("主题偏好保存失败，请稍后重试。",ex);}
  finally {try {if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
 }
 public static void Apply() {
  bool dark=Mode=="dark";
  if(Mode=="system") {using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); dark=key?.GetValue("AppsUseLightTheme") is int value&&value==0;}
  string[] names=["WindowBackground","Surface","TextPrimary","TextMuted","BorderBrush","Accent","AccentText","SidebarBackground","NavText","NavSelected","Subtle","WarningText","InputBackground","Selection"];
  string[] values=dark?["#0B1222","#121E33","#F0F3FF","#AFBDD7","#304362","#7050ED","#FFFFFF","#0F1A2E","#AFBDD7","#29284F","#1B2942","#F0CC8E","#0C1729","#29284F"]:["#EFF2FC","#FFFFFF","#17223B","#536480","#CBD4E9","#6542DC","#FFFFFF","#F8F9FF","#536480","#EEE8FC","#F2F4FC","#825715","#FFFFFF","#EEE8FC"];
  for(int i=0;i<names.Length;i++) {var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i])); brush.Freeze(); Application.Current.Resources[names[i]]=brush;}
  var details=dark?new Dictionary<string,string> {
   ["IconBackground"]="#29284F",["SelectionBorder"]="#AC92FF",["WarningBackground"]="#30291D",["WarningBorder"]="#5C472B",["WarningText"]="#F0CC8E",
   ["SidebarBackground"]="#0F1A2E",["NavSelected"]="#29284F",["Selection"]="#29284F"
  }:new Dictionary<string,string> {
   ["IconBackground"]="#F0F1F3",["SelectionBorder"]="#7955E7",["WarningBackground"]="#FFF8EB",["WarningBorder"]="#EDDFC4",["WarningText"]="#825715",
   ["SidebarBackground"]="#F8F9FF",["NavSelected"]="#EEE8FC",["Selection"]="#F0F1F3"
  };
  foreach(var (name,value) in details) {var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));brush.Freeze();Application.Current.Resources[name]=brush;}
 }
}

