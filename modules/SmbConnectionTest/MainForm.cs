using System;
using System.Drawing;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Cz.NetworkTools
{
    public sealed class MainForm : Form
    {
        private readonly TextBox address = new TextBox { Name = "AddressInput", Dock = DockStyle.Fill, AccessibleName = "NAS 地址", MaxLength = 2048 };
        private readonly NumericUpDown port = new NumericUpDown { Name = "PortInput", Minimum = 1, Maximum = 65535, Value = 445, Dock = DockStyle.Fill, AccessibleName = "SMB 端口" };
        private readonly TextBox share = new TextBox { Name = "ShareInput", Dock = DockStyle.Fill, AccessibleName = "共享名，可选", MaxLength = 1024 };
        private readonly Button start = new Button { Name = "StartButton", Text = "一键测试", AutoSize = true, Height = 38 };
        private readonly Button cancel = new Button { Name = "CancelButton", Text = "取消", AutoSize = true, Enabled = false, Height = 38 };
        private readonly Button copy = new Button { Name = "CopyButton", Text = "复制检测报告", AutoSize = true, Enabled = false, Height = 38 };
        private readonly Label summary = new Label { Name = "SummaryLabel", Text = "填写 NAS 地址后，点击“一键测试”。", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(12), BackColor = Color.FromArgb(240, 242, 245) };
        private readonly Label[] states = new Label[4], details = new Label[4];
        private CancellationTokenSource cancellation;
        private DiagnosticReport report;
        private bool closing;
        public MainForm()
        {
            Text = "SMB 连接测试 · 网络工具";
            Font = new Font("Microsoft YaHei UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(930, 790); MinimumSize = new Size(820, 730);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White; AutoScroll = true;
            var root = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(24, 20, 24, 20) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);
            root.Controls.Add(new Label { Text = "SMB 连接测试", AutoSize = true, Font = new Font(Font.FontFamily, 20F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 7) });
            root.Controls.Add(new Label { Text = "检查 NAS 连接卡在哪一步：地址 → 端口 → SMB 协商 → 共享访问。", AutoSize = true, ForeColor = Color.FromArgb(75, 81, 90), Margin = new Padding(0, 0, 0, 17) });
            var inputs = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Margin = new Padding(0) };
            inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16));
            inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            inputs.Controls.Add(InputLabel("NAS 地址"), 0, 0); inputs.Controls.Add(InputLabel("端口"), 2, 0);
            inputs.Controls.Add(address, 0, 1); inputs.Controls.Add(port, 2, 1);
            var hint = SmallLabel("例如 nas.local、192.168.1.10、smb://服务器/共享名；也可粘贴 \\\\服务器\\共享名。");
            inputs.Controls.Add(hint, 0, 2); inputs.SetColumnSpan(hint, 3);
            inputs.Controls.Add(InputLabel("共享名（可选）"), 0, 3);
            inputs.Controls.Add(share, 0, 4); inputs.SetColumnSpan(share, 3);
            var shareHint = SmallLabel("例如 media 或 电影；留空只检测 SMB 服务。粘贴地址中的共享名会自动使用。");
            inputs.Controls.Add(shareHint, 0, 5); inputs.SetColumnSpan(shareHint, 3);
            root.Controls.Add(inputs);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 13, 0, 12), WrapContents = true };
            start.BackColor = Color.FromArgb(30, 35, 42); start.ForeColor = Color.White;
            foreach (Button button in new[] { start, cancel, copy })
            {
                button.FlatStyle = FlatStyle.Flat; button.Padding = new Padding(13, 5, 13, 5); button.Margin = new Padding(0, 0, 10, 0);
                actions.Controls.Add(button);
            }
            root.Controls.Add(actions); root.Controls.Add(summary);
            string[] names = { "地址解析", "端口连接", "SMB 协商", "共享访问" };
            for (int i = 0; i < 4; i++)
            {
                var row = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0, 12, 0, 0), Padding = new Padding(0, 0, 0, 9) };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
                row.Controls.Add(new Label { Text = (i + 1) + "  " + names[i], AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 0, 0, 6) }, 0, 0);
                states[i] = new Label { Name = "State" + i, Text = "待检测", AutoSize = true, ForeColor = Color.DimGray, Anchor = AnchorStyles.Right, Margin = new Padding(0) };
                row.Controls.Add(states[i], 2, 0);
                details[i] = new Label { Name = "Detail" + i, Text = i == 3 ? "填写共享名后，使用 Windows 已有凭据只读检查。" : "等待开始。", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0), ForeColor = Color.FromArgb(75, 81, 90), MinimumSize = new Size(0, 32) };
                row.Controls.Add(details[i], 0, 1); row.SetColumnSpan(details[i], 3);
                root.Controls.Add(row);
            }
            string account;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent()) account = identity.Name;
            var footer = SmallLabel("只读检测，不修改 NAS 文件或系统设置，不收集或保存密码。\r\n共享检查使用当前程序账户：" + account + "。管理员运行时，已有共享连接可能与桌面账户不同。\r\n支持 SMB 2.0.2 至 3.1.1 协商；不检测 SMB1，不测试文件内容、写入权限或实际加密会话。");
            footer.Margin = new Padding(0, 13, 0, 0); root.Controls.Add(footer);
            start.Click += StartClick; cancel.Click += delegate { if (cancellation != null) { cancel.Enabled = false; cancellation.Cancel(); summary.Text = "正在取消检测……"; } };
            copy.Click += delegate
            {
                if (report == null) return;
                try { Clipboard.SetText(report.ToText()); copy.Text = "报告已复制"; }
                catch (System.Runtime.InteropServices.ExternalException) { summary.Text = "剪贴板暂时被占用，请稍后重试复制。"; }
            };
            AcceptButton = start;
            FormClosing += delegate { closing = true; if (cancellation != null) cancellation.Cancel(); };
        }
        private Label InputLabel(string text) { return new Label { Text = text, AutoSize = true, Margin = new Padding(0, 4, 0, 6) }; }
        private Label SmallLabel(string text) { return new Label { Text = text, AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(95, 101, 110), Margin = new Padding(0, 5, 0, 3) }; }
        private void StartClick(object sender, EventArgs e)
        {
            SmbTarget target;
            try { target = SmbTarget.Parse(address.Text, (int)port.Value, share.Text); }
            catch (ArgumentException ex) { summary.Text = ex.Message; address.Focus(); return; }
            address.Text = target.Host; port.Value = target.Port; share.Text = target.Share;
            report = null; copy.Enabled = false; copy.Text = "复制检测报告";
            address.Enabled = port.Enabled = share.Enabled = start.Enabled = false; cancel.Enabled = true;
            summary.Text = "正在检测 " + target.Endpoint + "……";
            for (int i = 0; i < 4; i++) { states[i].Text = "待检测"; states[i].ForeColor = Color.DimGray; details[i].Text = "等待前序检测。"; }
            var currentCancellation = new CancellationTokenSource(); cancellation = currentCancellation;
            Action<CheckResult> progress = delegate(CheckResult step)
            {
                if (closing || IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke(new Action(delegate { if (!closing && cancellation == currentCancellation) ApplyStep(step); })); }
                catch (InvalidOperationException) { /* Window was closed while a result was being queued. */ }
            };
            Task.Run(delegate { return new DiagnosticRunner().Run(target, progress, currentCancellation.Token); }).ContinueWith(delegate(Task<DiagnosticReport> task)
            {
                if (closing || IsDisposed || !IsHandleCreated) { currentCancellation.Dispose(); return; }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        try
                        {
                            cancellation = null;
                            if (task.IsFaulted) { var ignored = task.Exception; summary.Text = "检测程序遇到异常，请重新打开工具后重试。"; }
                            else
                            {
                                report = task.Result;
                                foreach (CheckResult step in report.Steps) ApplyStep(step);
                                summary.Text = report.Summary; copy.Enabled = true;
                            }
                        }
                        finally
                        {
                            currentCancellation.Dispose();
                            if (!closing) { address.Enabled = port.Enabled = share.Enabled = start.Enabled = true; cancel.Enabled = false; }
                        }
                    }));
                }
                catch (InvalidOperationException) { currentCancellation.Dispose(); }
            });
        }
        private void ApplyStep(CheckResult step)
        {
            states[step.Index].Text = step.State;
            states[step.Index].ForeColor = step.State == "通过" ? Color.FromArgb(20, 115, 65) : step.State == "失败" ? Color.FromArgb(177, 42, 42) : Color.DimGray;
            details[step.Index].Text = step.Detail;
        }
    }
}
