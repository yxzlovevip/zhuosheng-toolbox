using System;
namespace Cz.AceVirtualization {
    public enum RepairChoice { None, Hypervisor, Vbs, Combined, Unknown }
    public sealed class DetectionSnapshot {
        public string Cpu="Unknown", Dma="Unknown", Hypervisor="Unknown", Vbs="Unknown", Details="", LogPath="";
    }
    public static class RepairPlanner {
        public static RepairChoice Suggest(DetectionSnapshot s) {
            if(s.Hypervisor=="Unknown" || s.Vbs=="Unknown") return RepairChoice.Unknown;
            bool hyp=s.Hypervisor!="Off", vbs=s.Vbs=="Running"||s.Vbs=="Configured";
            return hyp ? (vbs ? RepairChoice.Combined : RepairChoice.Hypervisor) : (vbs ? RepairChoice.Vbs : RepairChoice.None);
        }
        public static bool NeedsFirmwareAdvice(DetectionSnapshot s) { return s.Dma!="On" || s.Cpu!="On"; }
        public static string ScriptName(RepairChoice action) {
            switch(action) {
                case RepairChoice.Hypervisor:return "Disable_hyp.bat";
                case RepairChoice.Vbs:return "Disable_vbs.bat";
                case RepairChoice.Combined:return "Enable_VMX.bat";
                default:throw new ArgumentException("请选择明确的修复操作。");
            }
        }
        public static string FormatState(string state) {
            switch(state) {
                case "On":case "Auto":return "已启用";
                case "Off":return "已关闭";
                case "Running":return "正在运行";
                case "Configured":return "已启用但未运行";
                case "NotConfigured":return "未配置（通常使用系统默认值）";
                default:return "未知";
            }
        }
    }
    public static class ReportParser {
        public static DetectionSnapshot Parse(string text) {
            var s=new DetectionSnapshot(); s.Details=text??"";
            foreach(string line in s.Details.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)) {
                int split=line.IndexOf('=');if(split<0)continue;
                string k=line.Substring(0,split),v=line.Substring(split+1).Trim();
                if(k=="CPU" && (v=="On"||v=="Off"))s.Cpu=v;
                if(k=="DMA" && (v=="On"||v=="Off"))s.Dma=v;
                if(k=="HYPERVISOR" && (v=="Auto"||v=="Off"||v=="NotConfigured"))s.Hypervisor=v;
                if(k=="VBS" && (v=="Running"||v=="Off"||v=="Configured"))s.Vbs=v;
            }
            return s;
        }
    }
}
