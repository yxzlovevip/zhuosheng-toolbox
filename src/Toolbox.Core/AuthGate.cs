using System.Security.Cryptography;
namespace Toolbox.Core;

public sealed class AuthGate(TimeProvider? time=null) {
 readonly TimeProvider clock=time??TimeProvider.System;
 readonly byte[] salt=Convert.FromBase64String("5ckpb6Fwx9UvPcpk3l8jyg==");
 readonly byte[] expected=Convert.FromBase64String("h4kNanlFcFBwz2zOg13LTMBM4krkQDHbCgRR4qfu4U4=");
 int failures;
 DateTimeOffset retryAt;
 public bool IsUnlocked {get;private set;}
 public int RemainingSeconds=>Math.Max(0,(int)Math.Ceiling((retryAt-clock.GetUtcNow()).TotalSeconds));
 public bool TryUnlock(string? password) {
  if(RemainingSeconds>0) return false;
  bool accepted=false;
  if(password is {Length:>0 and <=128}) {
   byte[] actual=Rfc2898DeriveBytes.Pbkdf2(password,salt,150000,HashAlgorithmName.SHA256,32);
   accepted=CryptographicOperations.FixedTimeEquals(actual,expected);
   CryptographicOperations.ZeroMemory(actual);
  }
  if(accepted) {IsUnlocked=true; failures=0; return true;}
  if(++failures>=5) {retryAt=clock.GetUtcNow().AddSeconds(30); failures=0;}
  return false;
 }
 public void Lock()=>IsUnlocked=false;
}
