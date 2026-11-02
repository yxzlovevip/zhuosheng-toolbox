using Toolbox.Core;

var projectRoot = Path.GetFullPath(args.Single());
using var stream = File.OpenRead(Path.Combine(projectRoot, "assets", "catalog.json"));
var catalog = ToolCatalog.Load(stream);
var item = catalog.Items.Single(i => i.Id == "browser-cleanup");
if (!item.NeedsConfirmation || !item.Name.Contains("收藏夹") || item.Risk != "高影响")
    throw new Exception("主软件必须准确披露收藏夹删除并要求确认。");
if (!catalog.Search("浏览器", "重装维护").Any(i => i.Id == item.Id))
    throw new Exception("主软件搜索与分类找不到新工具。");
var auth = new AuthGate();
if (!auth.TryUnlock((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")))) throw new Exception("测试解锁失败。");
var launcher = new ToolLauncher(projectRoot, auth, () => true);
var path = await launcher.ValidateAsync(item);
var start = ToolLauncher.CreateStartInfo(path);
if (!start.FileName.EndsWith("浏览器配置与推广清理.exe", StringComparison.Ordinal) || start.ArgumentList.Count != 0 || start.Arguments.Length != 0)
    throw new Exception("主软件入口必须打开 EXE 并保留交互菜单，不得自动传 /S。");
var appPath = Path.Combine(projectRoot, "modules", "BrowserCleanup", "integration-preview", "CZToolbox.dll");
var app = System.Reflection.Assembly.LoadFile(appPath);
using var embedded = app.GetManifestResourceStream("catalog.json") ?? throw new Exception("构建后的主软件缺少工具清单。");
var compiledItem = ToolCatalog.Load(embedded).Items.Single(i => i.Id == item.Id);
if (compiledItem.Sha256 != item.Sha256 || compiledItem.RelativePath != item.RelativePath || !compiledItem.NeedsConfirmation)
    throw new Exception("主软件构建没有包含最新的浏览器工具条目。");
Console.WriteLine("PASS: source and compiled app catalog, impact confirmation metadata, search, real EXE integrity, interactive launch parameters. No process launched.");
