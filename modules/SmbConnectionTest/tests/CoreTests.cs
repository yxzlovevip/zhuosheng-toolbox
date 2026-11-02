using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Cz.NetworkTools;

internal static class CoreTests
{
    private static int passed, failed;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } catch (InvalidDataException) { return; } throw new Exception("应拒绝此输入/响应"); }
    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.GetType().Name + " " + ex.Message); }
    }
    private static byte[] Response(ushort dialect)
    {
        // Hand-built wire fixture: MS-SMB2 header + 65-byte negotiate response.
        byte[] b = new byte[dialect == 0x0311 ? 174 : 128];
        b[0]=0xfe; b[1]=0x53; b[2]=0x4d; b[3]=0x42; b[4]=64; b[14]=1; b[16]=1;
        b[64]=65; b[66]=3; b[68]=(byte)dialect; b[69]=(byte)(dialect >> 8);
        b[94]=1; b[98]=1; b[102]=1;
        if(dialect == 0x0311)
        {
            b[70]=1; b[124]=128;
            b[128]=1; b[130]=38; b[136]=1; b[138]=32; b[140]=1;
        }
        return b;
    }
    private static byte[] Frame(byte[] body)
    {
        byte[] f = new byte[body.Length+4]; f[1]=(byte)(body.Length>>16); f[2]=(byte)(body.Length>>8); f[3]=(byte)body.Length;
        Buffer.BlockCopy(body,0,f,4,body.Length); return f;
    }
    private static void WithServer(Action<NetworkStream> server, Action<TcpClient> client)
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback,0); listener.Start();
        Exception serverError=null;
        Thread thread = new Thread(delegate() {
            try { using(TcpClient peer=listener.AcceptTcpClient()) { peer.ReceiveTimeout=2500; peer.SendTimeout=2500; server(peer.GetStream()); } }
            catch(Exception ex) { serverError=ex; }
        }); thread.IsBackground=true; thread.Start();
        try { using(TcpClient peer=new TcpClient()) { peer.Connect((IPEndPoint)listener.LocalEndpoint); client(peer); } }
        finally { listener.Stop(); Check(thread.Join(4000),"本地测试服务器未退出"); }
        if(serverError!=null) throw serverError;
    }
    private static void ReadRequest(NetworkStream s)
    {
        byte[] h=new byte[4]; ReadAll(s,h); int n=(h[1]<<16)|(h[2]<<8)|h[3]; Check(n>=100 && n<4096,"请求帧长非法");
        byte[] body=new byte[n]; ReadAll(s,body);
        Check(body[0]==0xfe && body[4]==64 && body[12]==0 && body[64]==36,"请求不是 SMB2 NEGOTIATE");
        Check(body[66]==5 && body[108]==0x11 && body[109]==3,"未提出 SMB 3.1.1");
        Check(body[92]==112 && body[96]==1 && body[112]==1 && body[114]==38 && body[120]==1 && body[124]==1,"预认证上下文错误");
    }
    private static void ReadAll(Stream s,byte[] b) { int p=0; while(p<b.Length){int n=s.Read(b,p,b.Length-p); if(n==0)throw new EndOfStreamException(); p+=n;} }
    public static int Main()
    {
        Test("plain host uses selected port",delegate{var t=SmbTarget.Parse("nas.local",1445,""); Check(t.Host=="nas.local"&&t.Port==1445&&t.Share=="","目标不正确");});
        Test("SMB URL decodes share and explicit port",delegate{var t=SmbTarget.Parse("smb://nas.local:1445/%E7%94%B5%E5%BD%B1",445,"");Check(t.Host=="nas.local"&&t.Port==1445&&t.Share=="电影","URL未正确解析");});
        Test("UNC extracts share",delegate{var t=SmbTarget.Parse(@"\\nas\media",445,"");Check(t.Host=="nas"&&t.Share=="media","UNC未正确解析");});
        Test("IPv6 supports bracketed explicit port",delegate{var t=SmbTarget.Parse("[::1]:1445",445,"");Check(t.Host=="::1"&&t.Port==1445,"IPv6目标不正确: "+t.Host+":"+t.Port);});
        Test("IPv6 literal uses selected port",delegate{var t=SmbTarget.Parse("::1",445,"");Check(t.Host=="::1"&&t.Port==445,"IPv6目标不正确");});
        Test("separate share overrides pasted share",delegate{Check(SmbTarget.Parse("smb://nas/media",445,"资料").Share=="资料","共享名未覆盖");});
        Test("host colon port",delegate{Check(SmbTarget.Parse("nas:1445",445,"").Port==1445,"端口未提取");});
        Test("reject empty host",delegate{Reject(delegate{SmbTarget.Parse("",445,"");});});
        Test("reject web URL",delegate{Reject(delegate{SmbTarget.Parse("https://nas",445,"");});});
        Test("reject embedded credentials",delegate{Reject(delegate{SmbTarget.Parse("smb://user:secret@nas/media",445,"");});});
        Test("reject out of range port",delegate{Reject(delegate{SmbTarget.Parse("nas",65536,"");});});
        Test("reject malformed explicit port",delegate{Reject(delegate{SmbTarget.Parse("nas:abc",445,"");});});
        Test("reject traversal share",delegate{Reject(delegate{SmbTarget.Parse("nas",445,"media/../other");});});
        Test("reject query URL",delegate{Reject(delegate{SmbTarget.Parse("smb://nas/media?token=secret",445,"");});});
        Test("reject wildcard share",delegate{Reject(delegate{SmbTarget.Parse("nas",445,"*");});});
        foreach(ushort dialect in new ushort[]{0x0202,0x0210,0x0300,0x0302,0x0311})
        { ushort d=dialect; Test("accept valid dialect "+d.ToString("X4"),delegate{var r=SmbWire.ParseResponse(Response(d));Check(r.Dialect.Length>0&&r.SigningRequired,"合法响应应显示协议与签名要求");}); }
        Test("reject HTTP response",delegate{Reject(delegate{SmbWire.ParseResponse(System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n"));});});
        Test("reject truncated negotiate body",delegate{Reject(delegate{SmbWire.ParseResponse(new byte[70]);});});
        Test("reject server SMB error status",delegate{byte[] b=Response(0x0302); b[8]=0xbb;b[11]=0xc0;Reject(delegate{SmbWire.ParseResponse(b);});});
        Test("reject reply with wrong message ID",delegate{byte[] b=Response(0x0302);b[24]=1;Reject(delegate{SmbWire.ParseResponse(b);});});
        Test("reject request echoed as response",delegate{byte[] b=Response(0x0302);b[16]=0;Reject(delegate{SmbWire.ParseResponse(b);});});
        Test("reject unsupported dialect",delegate{Reject(delegate{SmbWire.ParseResponse(Response(0x02ff));});});
        Test("reject invalid security buffer bounds",delegate{byte[] b=Response(0x0302);b[120]=127;b[122]=8;Reject(delegate{SmbWire.ParseResponse(b);});});
        Test("reject SMB311 missing preauth context",delegate{byte[] b=Response(0x0311);b[70]=0;Reject(delegate{SmbWire.ParseResponse(b);});});
        Test("TCP fragmented frame is read completely",delegate{WithServer(delegate(NetworkStream s){ReadRequest(s);byte[] f=Frame(Response(0x0311));foreach(byte b in f)s.WriteByte(b);},delegate(TcpClient c){Check(SmbWire.Exchange(c,1500,CancellationToken.None).Dialect=="3.1.1","分段响应失败");});});
        Test("TCP close cannot pass SMB detection",delegate{WithServer(delegate(NetworkStream s){ReadRequest(s);},delegate(TcpClient c){Reject(delegate{SmbWire.Exchange(c,1000,CancellationToken.None);});});});
        Test("oversized frame is rejected",delegate{WithServer(delegate(NetworkStream s){ReadRequest(s);s.Write(new byte[]{0,16,0,0},0,4);},delegate(TcpClient c){Reject(delegate{SmbWire.Exchange(c,1000,CancellationToken.None);});});});
        Test("silent TCP server times out",delegate{WithServer(delegate(NetworkStream s){ReadRequest(s);Thread.Sleep(600);},delegate(TcpClient c){bool timeout=false;try{SmbWire.Exchange(c,150,CancellationToken.None);}catch(TimeoutException){timeout=true;} Check(timeout,"无响应应超时");});});
        Test("cancel stops pending SMB read",delegate{WithServer(delegate(NetworkStream s){ReadRequest(s);Thread.Sleep(600);},delegate(TcpClient c){using(var cancel=new CancellationTokenSource()){cancel.CancelAfter(100);bool stopped=false;try{SmbWire.Exchange(c,2000,cancel.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"取消未生效");}});});
        Test("runner marks TCP success separately from failed SMB",delegate{
            var listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
            var worker=new Thread(delegate(){using(var c=listener.AcceptTcpClient()){ReadRequest(c.GetStream());}});worker.IsBackground=true;worker.Start();
            try{var r=new DiagnosticRunner().Run(SmbTarget.Parse("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,""),null,CancellationToken.None);
                Check(r.Steps.Count==4&&r.Steps[1].State=="通过"&&r.Steps[2].State=="失败"&&r.Steps[3].State=="未检测","TCP成功不能当成SMB成功");
                Check(r.ToText().Contains("尚未验证")&&r.ToText().Contains("127.0.0.1"),"报告没有披露验证范围");
            }finally{listener.Stop();worker.Join(2000);}
        });
        Test("runner successful negotiation does not claim share access",delegate{
            var listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
            var worker=new Thread(delegate(){using(var c=listener.AcceptTcpClient()){var s=c.GetStream();ReadRequest(s);var f=Frame(Response(0x0302));s.Write(f,0,f.Length);}});worker.IsBackground=true;worker.Start();
            try{var r=new DiagnosticRunner().Run(SmbTarget.Parse("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,""),null,CancellationToken.None);
                Check(r.Steps[2].State=="通过"&&r.Steps[3].State=="未检测"&&r.Summary.Contains("尚未验证"),"未填写共享名不能声称共享可用");
            }finally{listener.Stop();worker.Join(2000);}
        });
        Test("runner skips Windows share check on custom port",delegate{
            var listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
            var worker=new Thread(delegate(){using(var c=listener.AcceptTcpClient()){var s=c.GetStream();ReadRequest(s);var f=Frame(Response(0x0302));s.Write(f,0,f.Length);}});worker.IsBackground=true;worker.Start();
            try{var r=new DiagnosticRunner().Run(SmbTarget.Parse("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,"media"),null,CancellationToken.None);
                Check(r.Steps[3].State=="未检测"&&r.Steps[3].Detail.Contains("445"),"自定义端口不能暗中测试445共享");
            }finally{listener.Stop();worker.Join(2000);}
        });
        Test("runner cancelled before start sends no network request",delegate{
            using(var cancel=new CancellationTokenSource()){cancel.Cancel();var r=new DiagnosticRunner().Run(SmbTarget.Parse("127.0.0.1",445,""),null,cancel.Token);Check(r.Summary.Contains("取消")&&r.Steps.Count==4&&r.Steps[0].State=="已取消","预先取消未正确报告");}
        });
        Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed"); return failed==0?0:1;
    }
}
