using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.CSharp.RuntimeBinder;

namespace SysLens.Lighting.Aura;

/// <summary>
/// The ASUS Aura SDK, run in a child SysLens process started with <see cref="AuraProtocol.HostArgument"/>.
/// The SDK loads every vendor's lighting HAL into the calling process, and a failing HAL can end that process,
/// so it never runs inside the SysLens window. COM calls run on this process's STA UI thread. A reader thread takes
/// commands and keeps only the newest frame per device, so SysLens never waits on a slow device. When SysLens
/// closes its pipe, or dies, the devices go back to Armoury Crate and this process exits.
/// </summary>
internal sealed class AuraHost
{
    private const string ProgId = "aura.sdk.1";

    private readonly Application _app;
    private readonly Dispatcher _dispatcher;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    private readonly Dictionary<int, byte[]> _pendingFrames = [];
    private bool _applyQueued;

    // Touched on the UI thread only.
    private dynamic? _sdk;
    private List<HostDevice> _devices = [];
    private bool _inControl;

    private AuraHost(Application app, Stream input, Stream output)
    {
        _app = app;
        _dispatcher = app.Dispatcher;
        _reader = new BinaryReader(input);
        _writer = new BinaryWriter(output);
    }

    public static void Run(Application app, string inputHandle, string outputHandle)
    {
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        AuraHost host;
        try
        {
            host = new AuraHost(app,
                new AnonymousPipeClientStream(PipeDirection.In, inputHandle),
                new AnonymousPipeClientStream(PipeDirection.Out, outputHandle));
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            // Not started by SysLens with valid pipe handles.
            app.Shutdown(1);
            return;
        }

        new Thread(host.ReadCommands) { IsBackground = true, Name = "Aura host commands" }.Start();
    }

    private void ReadCommands()
    {
        try
        {
            while (true)
            {
                switch (_reader.ReadByte())
                {
                    case AuraProtocol.Enumerate:
                        Reply(() => _devices = EnumerateDevices());
                        break;
                    case AuraProtocol.TakeControl:
                        Reply(() =>
                        {
                            Sdk.SwitchMode();
                            _inControl = true;
                            // Device objects from before SwitchMode are not used for output.
                            _devices = EnumerateDevices();
                        });
                        break;
                    case AuraProtocol.Release:
                        Reply(ReleaseControl, listDevices: false);
                        break;
                    case AuraProtocol.Frame:
                        var index = _reader.ReadInt32();
                        QueueFrame(index, _reader.ReadBytes(_reader.ReadInt32()));
                        break;
                    default:
                        return;
                }
            }
        }
        catch (IOException)
        {
            // SysLens closed the pipe or exited.
        }
        finally
        {
            _dispatcher.Invoke(() =>
            {
                try
                {
                    ReleaseControl();
                }
                catch (Exception ex) when (IsSdkError(ex))
                {
                    // Exiting releases the SDK as well.
                }
                _app.Shutdown();
            });
        }
    }

    /// <summary>Runs a command on the UI thread and writes its reply: the device list or done, or the error.</summary>
    private void Reply(Action command, bool listDevices = true)
    {
        string? error = null;
        _dispatcher.Invoke(() =>
        {
            try
            {
                command();
            }
            catch (Exception ex) when (IsSdkError(ex))
            {
                error = ex.Message;
            }
        });

        if (error is not null)
        {
            _writer.Write(AuraProtocol.Failed);
            _writer.Write(error);
        }
        else if (listDevices)
            AuraProtocol.WriteDevices(_writer, [.. _devices.Select(d => d.Info)]);
        else
            _writer.Write(AuraProtocol.Done);
        _writer.Flush();
    }

    private dynamic Sdk => _sdk ??= Activator.CreateInstance(Type.GetTypeFromProgID(ProgId)
        ?? throw new InvalidOperationException("the ASUS Aura SDK is not installed (Armoury Crate installs it)"))!;

    private List<HostDevice> EnumerateDevices()
    {
        var collection = Sdk.Enumerate(0u);
        int count = collection.Count;
        var devices = new List<HostDevice>(count);
        for (var i = 0; i < count; i++)
        {
            var device = collection[i];
            var lights = device.Lights;
            int lightCount = lights.Count;
            var lightObjects = new object[lightCount];
            for (var j = 0; j < lightCount; j++)
                lightObjects[j] = lights[j];

            var info = new AuraDeviceInfo((string)device.Name, (uint)device.Type, lightCount, (int)(uint)device.Width, (int)(uint)device.Height);
            devices.Add(new HostDevice(info, device, lightObjects));
        }
        return devices;
    }

    private void ReleaseControl()
    {
        lock (_pendingFrames)
            _pendingFrames.Clear();
        if (!_inControl)
            return;
        _inControl = false;
        Sdk.ReleaseControl(0u);
    }

    private void QueueFrame(int index, byte[] rgb)
    {
        lock (_pendingFrames)
        {
            _pendingFrames[index] = rgb;
            if (_applyQueued)
                return;
            _applyQueued = true;
        }
        _dispatcher.BeginInvoke(ApplyFrames);
    }

    private void ApplyFrames()
    {
        KeyValuePair<int, byte[]>[] frames;
        lock (_pendingFrames)
        {
            frames = [.. _pendingFrames];
            _pendingFrames.Clear();
            _applyQueued = false;
        }

        if (!_inControl)
            return;
        foreach (var (index, rgb) in frames)
        {
            if (index < _devices.Count)
                _devices[index].Apply(rgb);
        }
    }

    /// <summary>Errors the SDK or a vendor HAL can raise through COM late binding.</summary>
    private static bool IsSdkError(Exception ex) =>
        ex is ExternalException or RuntimeBinderException or InvalidOperationException or InvalidCastException;

    private sealed class HostDevice(AuraDeviceInfo info, dynamic device, object[] lights)
    {
        public AuraDeviceInfo Info { get; } = info;

        /// <param name="rgb">Three bytes per LED: red, green, blue.</param>
        public void Apply(byte[] rgb)
        {
            try
            {
                var count = Math.Min(lights.Length, rgb.Length / 3);
                for (var i = 0; i < count; i++)
                {
                    dynamic light = lights[i];
                    // The SDK packs a colour as 0x00BBGGRR.
                    light.Color = (uint)(rgb[3 * i] | rgb[3 * i + 1] << 8 | rgb[3 * i + 2] << 16);
                }
                device.Apply();
            }
            catch (Exception ex) when (IsSdkError(ex))
            {
                // One device failing must not stop the others; the next frame tries again.
            }
        }
    }
}
