using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace SysLens.Lighting.Aura;

/// <summary>SysLens's side of an <see cref="AuraHost"/> process.</summary>
internal sealed class AuraHostClient : IDisposable
{
    // The SDK's first Enumerate loads every vendor HAL; with some plugins installed that takes most of a minute.
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly AnonymousPipeServerStream _toHost;
    private readonly AnonymousPipeServerStream _fromHost;
    private readonly BinaryWriter _writer;
    private readonly BinaryReader _reader;
    private readonly Lock _writeLock = new();
    private readonly SemaphoreSlim _commands = new(1, 1);
    private volatile bool _inControl;
    private volatile string _stopReason = "its pipe closed";
    private int _stopped;

    private AuraHostClient(Process process, AnonymousPipeServerStream toHost, AnonymousPipeServerStream fromHost)
    {
        _process = process;
        _toHost = toHost;
        _fromHost = fromHost;
        _writer = new BinaryWriter(toHost);
        _reader = new BinaryReader(fromHost);
        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) =>
        {
            _stopReason = $"exit code {_process.ExitCode}";
            OnStopped();
        };
    }

    /// <summary>Raised once, on any thread, when the host process ends without <see cref="Dispose"/>.</summary>
    public event Action? Stopped;

    public bool IsAlive => Volatile.Read(ref _stopped) == 0;

    /// <summary>True between a successful <see cref="TakeControlAsync"/> and the release.</summary>
    public bool InControl => _inControl && IsAlive;

    /// <summary>Why the host stopped, for display; read after <see cref="Stopped"/>.</summary>
    public string StopReason => _stopReason;

    /// <exception cref="Win32Exception">The host process could not be started.</exception>
    public static AuraHostClient Start()
    {
        var toHost = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        var fromHost = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(AuraProtocol.HostArgument);
            start.ArgumentList.Add(toHost.GetClientHandleAsString());
            start.ArgumentList.Add(fromHost.GetClientHandleAsString());
            var process = Process.Start(start)!;

            // The host holds the only client ends now, so either side sees end-of-stream when the other exits.
            toHost.DisposeLocalCopyOfClientHandle();
            fromHost.DisposeLocalCopyOfClientHandle();
            return new AuraHostClient(process, toHost, fromHost);
        }
        catch
        {
            toHost.Dispose();
            fromHost.Dispose();
            throw;
        }
    }

    /// <summary>Lists the devices without changing their lighting.</summary>
    public Task<List<AuraDeviceInfo>> EnumerateAsync() =>
        RunAsync(AuraProtocol.Enumerate, AuraProtocol.Devices, AuraProtocol.ReadDevices);

    /// <summary>Takes every Aura device from Armoury Crate and lists them again.</summary>
    public async Task<List<AuraDeviceInfo>> TakeControlAsync()
    {
        var devices = await RunAsync(AuraProtocol.TakeControl, AuraProtocol.Devices, AuraProtocol.ReadDevices);
        _inControl = true;
        return devices;
    }

    /// <summary>Hands every Aura device back to Armoury Crate.</summary>
    public async Task ReleaseAsync()
    {
        // Frames stop before the release goes out, so none arrives after it.
        _inControl = false;
        await RunAsync(AuraProtocol.Release, AuraProtocol.Done, _ => true);
    }

    /// <summary>Queues one frame for a device; dropped unless SysLens is in control.</summary>
    public void SendFrame(int index, Rgb[] frame)
    {
        if (!InControl)
            return;

        var rgb = new byte[frame.Length * 3];
        for (var i = 0; i < frame.Length; i++)
        {
            rgb[3 * i] = frame[i].R;
            rgb[3 * i + 1] = frame[i].G;
            rgb[3 * i + 2] = frame[i].B;
        }

        lock (_writeLock)
        {
            try
            {
                _writer.Write(AuraProtocol.Frame);
                _writer.Write(index);
                _writer.Write(rgb.Length);
                _writer.Write(rgb);
                _writer.Flush();
            }
            catch (IOException)
            {
                OnStopped();
            }
            catch (ObjectDisposedException)
            {
                // Disposed while the render thread was mid-frame.
            }
        }
    }

    /// <summary>Closes the pipe, which makes the host release the devices and exit; kills it if it does not.</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _stopped, 1);
        _inControl = false;
        lock (_writeLock)
            _toHost.Dispose();
        if (!_process.WaitForExit(ExitTimeout))
            Kill();
        _fromHost.Dispose();
        _process.Dispose();
    }

    private async Task<T> RunAsync<T>(byte command, byte expectedReply, Func<BinaryReader, T> readBody)
    {
        if (!IsAlive)
            throw new AuraException($"the Aura host is not running ({StopReason})");

        await _commands.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                lock (_writeLock)
                {
                    _writer.Write(command);
                    _writer.Flush();
                }

                var reply = _reader.ReadByte();
                if (reply == AuraProtocol.Failed)
                    throw new AuraException(_reader.ReadString());
                if (reply != expectedReply)
                    throw new AuraException($"the Aura host sent reply {reply} to command {command}");
                return readBody(_reader);
            }).WaitAsync(CommandTimeout);
        }
        catch (TimeoutException)
        {
            Kill();
            throw new AuraException("the Aura host stopped responding");
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            OnStopped();
            throw new AuraException($"the Aura host stopped ({StopReason})");
        }
        finally
        {
            _commands.Release();
        }
    }

    private void OnStopped()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
            return;
        _inControl = false;
        Stopped?.Invoke();
    }

    private void Kill()
    {
        try
        {
            _process.Kill();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Already exited.
        }
    }
}
