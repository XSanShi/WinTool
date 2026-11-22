using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace WinTool.App.Infrastructure;

/// <summary>
/// 单实例（方案 §8.5 第 6 条）：Mutex 保证只有一个进程；命名管道让第二个进程能"叫醒"第一个。
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\WinTool.SingleInstance";
    private const string PipeName = "WinTool.Instance.Activate";

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();
    private readonly Action _onActivate;
    private Task? _listener;

    private SingleInstance(Mutex mutex, Action onActivate)
    {
        _mutex = mutex;
        _onActivate = onActivate;
    }

    /// <summary>
    /// 首个实例：返回 true 并启动监听；已有实例：把激活请求发过去、返回 false。
    /// </summary>
    public static bool TryAcquire(Action onActivate, out SingleInstance? instance)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            SignalExisting();
            instance = null;
            return false;
        }

        instance = new SingleInstance(mutex, onActivate);
        instance.StartListening();
        return true;
    }

    private static void SignalExisting()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine("show");
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            // 已有实例正在退出等情况：叫不醒就算了，不要因此弹错误框。
        }
    }

    private void StartListening()
    {
        _listener = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
                    using var reader = new StreamReader(server);
                    var line = await reader.ReadLineAsync(_cts.Token).ConfigureAwait(false);
                    if (line is "show")
                    {
                        _onActivate();
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 管道被抢占/客户端提前断开：继续等下一条。
                }
            }
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _listener?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        _cts.Dispose();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _mutex.Dispose();
    }
}

internal enum AclOutcome
{
    /// <summary>文件还不存在（首次运行尚未保存）。</summary>
    NotCreated,

    Applied,

    Failed,
}

/// <summary>
/// D1a 明文的补偿动作之一（方案 §6.2.3 第 3 条）：把 vault.json 的 ACL 收紧到"当前用户 + SYSTEM"，
/// 去掉从父目录继承来的 Users 权限。文件系统不支持（FAT / 网络盘）时只报告失败，不阻断保存。
/// </summary>
internal static class VaultAcl
{
    public static (AclOutcome Outcome, string? Error) Tighten(string path)
    {
        if (!File.Exists(path))
        {
            return (AclOutcome.NotCreated, null);
        }

        try
        {
            var file = new FileInfo(path);
            var security = file.GetAccessControl();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            var user = WindowsIdentity.GetCurrent().User;
            if (user is not null)
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    user, FileSystemRights.FullControl, AccessControlType.Allow));
            }

            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            file.SetAccessControl(security);
            return (AclOutcome.Applied, null);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                       or IOException
                                       or PlatformNotSupportedException
                                       or InvalidOperationException
                                       or NotSupportedException)
        {
            return (AclOutcome.Failed, ex.Message);
        }
    }
}
