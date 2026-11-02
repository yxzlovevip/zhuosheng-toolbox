using System;
using System.IO;
using Cz.AceVirtualization;

internal static class CoreTests
{
    static int count;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
    static DetectionSnapshot State(bool hypervisor, bool vbs, bool dma, bool cpu)
    { return new DetectionSnapshot { Hypervisor = hypervisor ? "Auto" : "Off", Vbs = vbs ? "Running" : "Off", Dma = dma ? "On" : "Off", Cpu = cpu ? "On" : "Off" }; }
    public static int Main()
    {
        try {
            Check(RepairPlanner.Suggest(State(true,true,true,true)) == RepairChoice.Combined, "Hypervisor 与 VBS 同时启用时，必须保留组合修复");
            Check(RepairPlanner.Suggest(State(true,false,true,true)) == RepairChoice.Hypervisor, "只需关闭 Hypervisor 时不得遗漏单项修复");
            Check(RepairPlanner.Suggest(State(false,true,true,true)) == RepairChoice.Vbs, "只需关闭 VBS 时不得遗漏单项修复");
            Check(RepairPlanner.Suggest(State(false,false,true,true)) == RepairChoice.None, "正常状态不能建议修改");
            Check(RepairPlanner.Suggest(new DetectionSnapshot()) == RepairChoice.Unknown, "检测失败不能误判为正常或自动修改");
            Check(RepairPlanner.NeedsFirmwareAdvice(State(false,false,false,true)), "DMA 关闭必须保留 BIOS 建议");
            Check(RepairPlanner.NeedsFirmwareAdvice(State(false,false,true,false)), "CPU 虚拟化关闭必须保留 BIOS 建议");
            Check(RepairPlanner.Suggest(State(true,true,false,false)) == RepairChoice.Combined, "BIOS 异常不能隐藏系统组合修复功能");
            Check(RepairPlanner.ScriptName(RepairChoice.Hypervisor) == "Disable_hyp.bat", "Hypervisor 操作映射错误");
            Check(RepairPlanner.ScriptName(RepairChoice.Vbs) == "Disable_vbs.bat", "VBS 操作映射错误");
            Check(RepairPlanner.ScriptName(RepairChoice.Combined) == "Enable_VMX.bat", "组合操作映射错误");
            bool rejected=false; try { RepairPlanner.ScriptName((RepairChoice)999); } catch(ArgumentException) { rejected=true; }
            Check(rejected, "不得接受未知操作并转为任意脚本路径");
            var parsed=ReportParser.Parse("CPU=On\r\nDMA=Unknown\r\nHYPERVISOR=Off\r\nVBS=Running\r\nDETAIL=read-only");
            Check(parsed.Cpu=="On" && parsed.Dma=="Unknown" && parsed.Hypervisor=="Off" && parsed.Vbs=="Running", "检测输出字段解析错误");
            Check(ReportParser.Parse("unexpected output").Vbs=="Unknown", "无效输出不得成为关闭状态");
            Check(ReportParser.Parse("CPU=unexpected").Cpu=="Unknown", "无效状态必须保持未知");
            Check(RepairPlanner.Suggest(new DetectionSnapshot{Hypervisor="Off",Vbs="Configured"})==RepairChoice.Vbs,"已启用但未运行的 VBS 不能伪装成已关闭");
            Check(RepairPlanner.FormatState("Unknown")=="未知", "未知状态不能以正常显示");
            Console.WriteLine("RESULT "+count+" passed, 0 failed"); return 0;
        } catch(Exception ex) { Console.WriteLine("FAIL "+ex.Message); return 1; }
    }
}
