using System.Diagnostics;

namespace SysLens.Lighting;

/// <summary>
/// Renders every <see cref="LightTarget"/> on one background thread at <see cref="FramesPerSecond"/>, all on one
/// clock so effects on different devices stay in step.
/// </summary>
public sealed class LightingEngine : IDisposable
{
    public const int FramesPerSecond = 30;

    private static readonly TimeSpan FrameTime = TimeSpan.FromSeconds(1.0 / FramesPerSecond);

    private readonly Lock _lock = new();
    private readonly List<LightTarget> _targets = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Thread _thread;
    private volatile bool _stopping;

    public LightingEngine()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "SysLens lighting" };
        _thread.Start();
    }

    public void Add(LightTarget target)
    {
        lock (_lock)
            _targets.Add(target);
    }

    public void Remove(LightTarget target)
    {
        lock (_lock)
            _targets.Remove(target);
    }

    /// <summary>Stops rendering; no frame is pushed after this returns.</summary>
    public void Dispose()
    {
        _stopping = true;
        _thread.Join();
    }

    private void Run()
    {
        while (!_stopping)
        {
            var start = _clock.Elapsed;
            LightTarget[] targets;
            lock (_lock)
                targets = [.. _targets];

            foreach (var target in targets)
                target.Render(start.TotalSeconds);

            var wait = FrameTime - (_clock.Elapsed - start);
            if (wait > TimeSpan.Zero)
                Thread.Sleep(wait);
        }
    }
}

/// <summary>
/// One stream of frames: the latest is kept for the preview and, when there is a device, pushed to it.
/// </summary>
public sealed class LightTarget(float[] positions, LightDevice? device, EffectSettings settings)
{
    private EffectSettings _settings = settings;
    private Rgb[]? _latest;
    private Rgb[]? _pushed;
    private bool _couldPush;

    public LightDevice? Device { get; } = device;

    /// <summary>Read by the render thread every frame; set from any thread.</summary>
    public EffectSettings Settings
    {
        get => Volatile.Read(ref _settings);
        set => Volatile.Write(ref _settings, value);
    }

    /// <summary>The most recently rendered frame, or null before the first.</summary>
    public Rgb[]? LatestFrame => Volatile.Read(ref _latest);

    internal void Render(double seconds)
    {
        var frame = new Rgb[positions.Length];
        EffectRenderer.Render(Settings, positions, seconds, frame);
        Volatile.Write(ref _latest, frame);

        if (Device is null)
            return;

        // Unchanged frames are skipped, except right after the device comes under SysLens control,
        // since whatever drove it before has changed its LEDs.
        var canPush = Device.CanPush;
        if (canPush && (!_couldPush || _pushed is null || !frame.AsSpan().SequenceEqual(_pushed)))
        {
            Device.Push(frame);
            _pushed = frame;
        }
        _couldPush = canPush;
    }
}
