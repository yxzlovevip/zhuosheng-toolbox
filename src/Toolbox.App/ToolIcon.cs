using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Toolbox.App;

// A single 24-unit grid and stroke weight keeps every UI icon consistent at any DPI.
public sealed class ToolIcon : Control {
 public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(string),typeof(ToolIcon),new FrameworkPropertyMetadata("tools",FrameworkPropertyMetadataOptions.AffectsRender));
 public string Kind {get=>(string)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
 static readonly IReadOnlyDictionary<string,Geometry> Shapes=new Dictionary<string,string> {
  ["tools"]="M4,7 L20,7 Q21,7 21,8 L21,19 Q21,20 20,20 L4,20 Q3,20 3,19 L3,8 Q3,7 4,7 M8,7 L8,4 L16,4 L16,7 M3,12 L21,12 M10,11 L14,11 L14,15 L10,15 Z",
  ["grid"]="M4,4 L10,4 L10,10 L4,10 Z M14,4 L20,4 L20,10 L14,10 Z M4,14 L10,14 L10,20 L4,20 Z M14,14 L20,14 L20,20 L14,20 Z",
  ["network"]="M5,15 Q12,8 19,15 M2,10 Q12,1 22,10 M8,18 Q12,14 16,18 M11,21 L13,21",
  ["disk"]="M5,4 L19,4 L22,16 L22,20 L2,20 L2,16 Z M2,15 L22,15 M16,18 L16.1,18 M19,18 L19.1,18",
  ["restore"]="M4,9 A9,9 0 1 1 3,15 M4,3 L4,9 L10,9 M12,7 L12,12 L16,14",
  ["settings"]="M4,6 L20,6 M4,12 L20,12 M4,18 L20,18 M8,3 L8,9 M16,9 L16,15 M10,15 L10,21",
  ["help"]="M12,2 A10,10 0 1 1 11.99,2 M9,9 A3,3 0 1 1 13,12 L12,14 M12,18 L12.01,18",
  ["update"]="M4,9 A8,8 0 0 1 19,6 L21,9 M21,3 L21,9 L15,9 M20,15 A8,8 0 0 1 5,18 L3,15 M3,21 L3,15 L9,15",
  ["lock"]="M6,10 L18,10 Q20,10 20,12 L20,20 L4,20 L4,12 Q4,10 6,10 M8,10 L8,7 A4,4 0 0 1 16,7 L16,10 M12,14 L12,17",
  ["search"]="M10,3 A7,7 0 1 1 9.99,3 M15,15 L21,21",
  ["shield"]="M12,2 L21,6 L20,13 Q19,19 12,22 Q5,19 4,13 L3,6 Z M8,12 L11,15 L16,9",
  ["speed"]="M3,18 A10,10 0 1 1 21,18 M5,18 L19,18 M12,14 L17,8 M6,11 L7,12 M12,5 L12,7",
  ["startup"]="M14,3 Q20,2 21,3 Q22,10 15,16 L8,9 Q10,5 14,3 M8,9 L4,10 L2,15 L9,14 M15,16 L14,21 L9,22 L10,15 M5,18 L2,21 M15,7 L17,9",
  ["gpu"]="M5,5 L19,5 L19,19 L5,19 Z M9,9 L15,9 L15,15 L9,15 Z M8,2 L8,5 M16,2 L16,5 M8,19 L8,22 M16,19 L16,22 M2,8 L5,8 M2,16 L5,16 M19,8 L22,8 M19,16 L22,16",
  ["uninstall"]="M4,6 L20,6 M9,6 L9,3 L15,3 L15,6 M6,6 L7,21 L17,21 L18,6 M10,10 L10,17 M14,10 L14,17",
  ["package"]="M12,2 L22,7 L22,17 L12,22 L2,17 L2,7 Z M2,7 L12,12 L22,7 M12,12 L12,22 M7,4.5 L17,9.5",
  ["archive"]="M5,2 L19,2 L19,22 L5,22 Z M11,3 L13,3 M11,6 L13,6 M11,9 L13,9 M11,12 L13,12 M10,16 L14,16 L14,19 L10,19 Z",
  ["key"]="M8,3 A5,5 0 1 1 3,8 A5,5 0 0 1 8,3 M12,12 L21,21 M16,16 L19,13 M19,19 L22,16",
  ["browser"]="M3,4 L21,4 L21,20 L3,20 Z M3,9 L21,9 M6,6.5 L6.1,6.5 M9,6.5 L9.1,6.5 M9,13 L15,17 M15,13 L9,17",
  ["server"]="M3,3 L21,3 L21,10 L3,10 Z M3,14 L21,14 L21,21 L3,21 Z M6,6.5 L6.1,6.5 M6,17.5 L6.1,17.5 M12,6.5 L18,6.5 M12,17.5 L18,17.5",
  ["chart"]="M3,3 L3,21 L22,21 M7,17 L7,12 M12,17 L12,6 M17,17 L17,9",
  ["arrow"]="M4,12 L20,12 M14,6 L20,12 L14,18",
  ["warning"]="M12,3 L22,21 L2,21 Z M12,9 L12,14 M12,17 L12.01,17"
 }.ToDictionary(x=>x.Key,x=>Geometry.Parse(x.Value));
 protected override void OnRender(DrawingContext dc) {
  base.OnRender(dc);
  double size=Math.Min(ActualWidth,ActualHeight);
  if(size<=0)return;
  dc.PushTransform(new TranslateTransform((ActualWidth-size)/2,(ActualHeight-size)/2));
  dc.PushTransform(new ScaleTransform(size/24,size/24));
  var pen=new Pen(Foreground,1.65) {StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
  dc.DrawGeometry(null,pen,Shapes.TryGetValue(Kind??"tools",out var shape)?shape:Shapes["tools"]);
  dc.Pop();dc.Pop();
 }
}

public sealed class ToolIconConverter : IValueConverter {
 public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value?.ToString() switch {
  "firewall" or "defender" or "isolation" or "security" or "uac"=>"shield",
  "speed"=>"speed", "autoruns"=>"startup", "gpu" or "driver" or "hyperv"=>"gpu",
  "uninstall"=>"uninstall", "runtime"=>"package", "archive"=>"archive", "reinstall" or "pe"=>"restore",
  "activation"=>"key", "ipv6"=>"network", "updates"=>"update", "browser-cleanup"=>"browser",
  "smb-connection-test"=>"server", "diskgenius"=>"disk", "spacesniffer"=>"chart", _=>"tools"
 };
 public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
