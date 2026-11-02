using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Cz.AceVirtualization {
    public sealed class MainForm:Form {
        readonly IAceService service;
        readonly Button detect=new Button{Name="DetectButton",Text="重新检测",AutoSize=true,Height=34};
        readonly Button repair=new Button{Name="RepairButton",Text="执行所选修复",AutoSize=true,Height=34};
        readonly Button log=new Button{Name="LogButton",Text="查看日志",AutoSize=true,Height=34,Enabled=false};
        readonly ComboBox choice=new ComboBox{Name="RepairChoice",DropDownStyle=ComboBoxStyle.DropDownList,Width=300};
        readonly Label cpu=new Label{Name="CpuState",AutoSize=true,Text="CPU 虚拟化：未检测"};
        readonly Label dma=new Label{Name="DmaState",AutoSize=true,Text="内核 DMA 保护：未检测"};
        readonly Label hyp=new Label{Name="HypervisorState",AutoSize=true,Text="Hypervisor：未检测"};
        readonly Label vbs=new Label{Name="VbsState",AutoSize=true,Text="VBS：未检测"};
        readonly Label summary=new Label{Name="Summary",AutoSize=true,Dock=DockStyle.Fill,Padding=new Padding(10),Text="准备检测；尚未修改系统。",BackColor=Color.FromArgb(241,244,248)};
        readonly TextBox guidance=new TextBox{Name="Guidance",Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BackColor=Color.White};
        readonly TextBox output=new TextBox{Name="Output",Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=Color.FromArgb(248,249,251)};
        bool busy,repairing;
        string lastLog="";
        static readonly RepairChoice[] actions={RepairChoice.Combined,RepairChoice.Hypervisor,RepairChoice.Vbs};
        public MainForm():this(new PayloadRunner()){}
        public MainForm(IAceService service) {
            if(service==null)throw new ArgumentNullException("service");this.service=service;
            Text="ACE 虚拟化环境检测修复 · 卓盛工具箱";Font=new Font("Microsoft YaHei UI",9F);
            AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(920,860);MinimumSize=new Size(820,760);
            StartPosition=FormStartPosition.CenterScreen;BackColor=Color.White;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=9,Padding=new Padding(22)};
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,45));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,52));Controls.Add(root);
            root.Controls.Add(new Label{Text="ACE 环境检测与修复",AutoSize=true,Font=new Font(Font.FontFamily,20F,FontStyle.Bold)},0,0);
            root.Controls.Add(new Label{Text="打开后只检测；保留原工具的四项检测与三种修复。修复后需重启并重新检测。",Dock=DockStyle.Fill,AutoSize=true,ForeColor=Color.DimGray},0,1);
            var states=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=2};
            states.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));states.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            states.Controls.Add(dma,0,0);states.Controls.Add(cpu,1,0);states.Controls.Add(hyp,0,1);states.Controls.Add(vbs,1,1);root.Controls.Add(states,0,2);
            root.Controls.Add(summary,0,3);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Padding=new Padding(0,9,0,0)};
            choice.Items.AddRange(new object[]{"组合修复：关闭 Hypervisor 和 VBS","单项修复：关闭 Hypervisor","单项修复：关闭 VBS"});choice.SelectedIndex=0;
            buttons.Controls.Add(choice);buttons.Controls.Add(repair);buttons.Controls.Add(detect);buttons.Controls.Add(log);root.Controls.Add(buttons,0,4);
            root.Controls.Add(new Label{Text="修复会更改启动配置及相关系统保护，可能影响 Docker、WSL、虚拟机和 Windows Hello。封装不会自动重启。",AutoSize=true,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(145,72,25)},0,5);
            guidance.Text=GuidanceText(false);root.Controls.Add(guidance,0,6);
            root.Controls.Add(new Label{Text="检测／操作输出（保留本次错误与日志位置）",AutoSize=true},0,7);root.Controls.Add(output,0,8);
            Shown+=async delegate{await CheckAsync();};
            detect.Click+=async delegate{await CheckAsync();};
            repair.Click+=async delegate{if(choice.SelectedIndex>=0)await RepairAsync(actions[choice.SelectedIndex]);};
            log.Click+=delegate{try{if(File.Exists(lastLog))Process.Start(new ProcessStartInfo(lastLog){UseShellExecute=true});}catch(Exception e){summary.Text="日志无法打开："+e.Message;}};
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy){e.Cancel=true;summary.Text=repairing?"修复仍在执行，请等待完成，避免中断启动配置写入。":"检测仍在进行，请等待完成。";}};
        }
        static string GuidanceText(bool bios) {
            return (bios?"【BIOS / UEFI 建议】\r\nDMA 或 CPU 虚拟化未确认启用。进入主板 BIOS，检查 Intel VT-x/VMX、VT-d 或 AMD SVM/AMD-V、IOMMU；具体以主板说明为准。BAT 不能替你开启 BIOS 虚拟化。\r\n\r\n":"【BIOS / UEFI】\r\n如游戏仍提示虚拟化异常，检查主板中的 VT-d/IOMMU、VMX/SVM 设置。\r\n\r\n")+
                "【原工具的准备事项】\r\n如正在使用 Docker 或 WSL，请先确认是否确实需要关闭 Hypervisor/VBS。原工具要求执行相关修复前改为本地账户并删除 PIN 登录；维护后再按需恢复 Microsoft 账户与 PIN。请先确认能够使用账户密码登录。\r\n\r\n"+
                "【VBS／组合修复后的 UEFI 提示】\r\n可能先进入系统启动前的确认界面。出现“Do you want to disable Credential Guard?”时按 F3（部分笔记本 Fn+F3），选择退出后按 Enter；出现“Do you want to disable Virtualization Based Security?”时按 F3，再按 Enter。以实际界面为准，完成后会继续启动。\r\n\r\n"+
                "【适用范围】\r\n保留原工具功能，不保证解决所有 ACE 提示。DG Readiness 3.6 的微软文档列出 Windows 10 / Server 2016；当前 Windows 11 的实际修复及重启流程尚待隔离环境验收。";
        }
        void SetBusy(bool value,bool isRepair) {
            busy=value;repairing=isRepair;detect.Enabled=!value;repair.Enabled=!value;choice.Enabled=!value;log.Enabled=!value&&File.Exists(lastLog);
        }
        async Task CheckAsync() {
            if(busy)return;SetBusy(true,false);summary.Text="正在检测 DMA、CPU 虚拟化、Hypervisor 和 VBS，未执行修复；报告可能需要约一分钟。";
            cpu.Text="CPU 虚拟化：检测中";dma.Text="内核 DMA 保护：检测中";hyp.Text="Hypervisor：检测中";vbs.Text="VBS：检测中";
            try {
                DetectionSnapshot s=await Task.Run(()=>service.Check());
                cpu.Text="CPU 虚拟化："+RepairPlanner.FormatState(s.Cpu);dma.Text="内核 DMA 保护："+RepairPlanner.FormatState(s.Dma);
                hyp.Text="Hypervisor："+RepairPlanner.FormatState(s.Hypervisor);vbs.Text="VBS："+RepairPlanner.FormatState(s.Vbs);
                guidance.Text=GuidanceText(RepairPlanner.NeedsFirmwareAdvice(s));output.Text=s.Details;lastLog=s.LogPath;
                RepairChoice suggested=RepairPlanner.Suggest(s);int index=Array.IndexOf(actions,suggested);if(index>=0)choice.SelectedIndex=index;
                summary.Text=suggested==RepairChoice.None?"检测完成：未发现需执行的系统修复项；仍请核对 BIOS 与目标游戏实际表现。":suggested==RepairChoice.Unknown?"检测完成，部分状态未知。三种修复功能均保留；请先查看输出定位原因，再自行选择操作。":"检测完成，已选中对应修复；尚未执行。请先阅读操作影响和准备事项。";
            } catch(Exception e){cpu.Text="CPU 虚拟化：未知";dma.Text="内核 DMA 保护：未知";hyp.Text="Hypervisor：未知";vbs.Text="VBS：未知";summary.Text="检测失败："+e.Message;output.Text=e.ToString();}
            finally{SetBusy(false,false);}
        }
        async Task RepairAsync(RepairChoice action) {
            if(busy)return;SetBusy(true,true);summary.Text="正在执行所选原功能，请等待；不会自动重启。";
            try {
                ScriptResult r=await Task.Run(()=>service.Repair(action));lastLog=r.LogPath;output.Text=r.Output+"\r\n日志："+r.LogPath;
                summary.Text=r.CommandCompleted?(r.RequiresRestart?"脚本已退出，返回需重启（3010）。保存工作后手动重启并重新检测；进程退出不证明修复已生效。":"脚本已退出（0）。请查看输出并重新检测，最终以系统及目标游戏为准。"):("操作未完成，退出码 "+r.ExitCode+"。请查看输出和日志；此前已执行的步骤不会自动回退。");
            } catch(Exception e){summary.Text="修复失败："+e.Message;output.Text=e.ToString();}
            finally{SetBusy(false,false);}
        }
    }
}
