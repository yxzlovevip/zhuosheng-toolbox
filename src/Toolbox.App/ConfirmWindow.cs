using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Toolbox.Core;
namespace Toolbox.App;
public sealed class ConfirmWindow : Window {
 public ConfirmWindow(ToolItem item) {
  Title="操作确认 — "+item.Name; Width=600; Height=520; MinHeight=430; MinWidth=500; WindowStartupLocation=WindowStartupLocation.CenterOwner; FontFamily=new("Microsoft YaHei UI"); FontSize=14; SetResourceReference(BackgroundProperty,"Surface"); SetResourceReference(ForegroundProperty,"TextPrimary");
  var grid=new Grid {Margin=new Thickness(26)}; grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)}); grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
  var content=new StackPanel(); content.Children.Add(new TextBlock {Text=item.Name,FontSize=23,FontWeight=FontWeights.Bold});
  var hint=new TextBlock {Text="影响范围",Margin=new(0,10,0,20)}; hint.SetResourceReference(TextBlock.ForegroundProperty,"TextMuted"); content.Children.Add(hint);
  content.Children.Add(new TextBlock {Text=item.Impact,TextWrapping=TextWrapping.Wrap,LineHeight=25});
  content.Children.Add(new TextBlock {Text="操作前准备\n"+item.Preparation+"\n\n重启："+item.Restart,TextWrapping=TextWrapping.Wrap,LineHeight=25,Margin=new(0,18,0,10)});
  if(item.Id=="uac") content.Children.Add(new TextBlock {Text="下一步只会打开注册表编辑器的导入确认。你仍可以在 Windows 提示中选择“否”。",TextWrapping=TextWrapping.Wrap,LineHeight=24});
  var scroll=new ScrollViewer {Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}; grid.Children.Add(scroll);
  var footer=new StackPanel {Margin=new(0,18,0,0)}; var check=new CheckBox {Content="已了解影响，继续操作",Margin=new(0,0,0,17)}; footer.Children.Add(check);
  var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
  var cancel=new Button {Content="取消",IsCancel=true}; cancel.Click+=(s,e)=>{DialogResult=false;};
  var confirm=new Button {Content="确认并打开",IsEnabled=false,Style=(Style)Application.Current.FindResource("Primary")}; check.Checked+=(s,e)=>confirm.IsEnabled=true; check.Unchecked+=(s,e)=>confirm.IsEnabled=false;
  confirm.Click+=(s,e)=>DialogResult=true; buttons.Children.Add(cancel); buttons.Children.Add(confirm); footer.Children.Add(buttons); Grid.SetRow(footer,1); grid.Children.Add(footer); Content=grid;
 }
}
