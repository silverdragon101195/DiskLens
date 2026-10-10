using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace SysLens.Monitoring;

/// <summary>
/// Reads the frame rate RivaTuner Statistics Server measures, from its shared memory. RTSS keeps an entry per
/// 3D application it hooks; the one it last saw in the foreground is reported, or the one rendering the most
/// frames when that is SysLens itself or has stopped rendering.
/// </summary>
internal sealed class RtssReader : IDisposable
{
    private const string MapName = "RTSSSharedMemoryV2";
    private const uint Signature = 0x52545353; // "RTSS"
    private const uint MinVersion = 0x00020000;

    // Header fields, as laid out in the RTSS SDK's RTSSSharedMemory.h.
    private const int VersionField = 4;
    private const int AppEntrySizeField = 8;
    private const int AppArrayOffsetField = 12;
    private const int AppArraySizeField = 16;
    private const int LastForegroundAppField = 64;
    private const int LastForegroundProcessIdField = 68;

    // Application entry fields.
    private const int ProcessIdField = 0;
    private const int NameField = 4;
    private const int NameLength = 260;
    private const int Time0Field = 268;
    private const int Time1Field = 272;
    private const int FramesField = 276;
    private const int FrameTimeField = 280;
    private const int MinEntrySize = 284;

    // RTSS counts frames over periods of about a second; an entry not counted for longer has stopped rendering.
    private const int StaleMilliseconds = 2500;
    private const int OpenRetryMilliseconds = 5000;

    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private MemoryMappedFile? _file;
    private MemoryMappedViewAccessor? _view;
    private long _nextOpenAttempt;

    /// <summary>True once RTSS's shared memory has been found; it stays mapped from then on.</summary>
    public bool IsAvailable => _view is not null;

    /// <summary>The frame rate of the 3D application in front, or null when none is rendering.</summary>
    public RtssFrame? Read()
    {
        if (!TryOpen())
            return null;

        var view = _view!;
        if (view.ReadUInt32(0) != Signature || view.ReadUInt32(VersionField) < MinVersion)
            return null;

        var entrySize = view.ReadUInt32(AppEntrySizeField);
        var arrayOffset = view.ReadUInt32(AppArrayOffsetField);
        var entryCount = view.ReadUInt32(AppArraySizeField);
        if (entrySize < MinEntrySize || arrayOffset + (long)entryCount * entrySize > view.Capacity)
            return null;

        var foregroundIndex = view.ReadUInt32(LastForegroundAppField);
        var foregroundProcessId = view.ReadUInt32(LastForegroundProcessIdField);
        var now = (uint)Environment.TickCount;

        Candidate? foreground = null;
        Candidate? busiest = null;
        for (uint i = 0; i < entryCount; i++)
        {
            var entry = arrayOffset + (long)i * entrySize;
            var processId = view.ReadUInt32(entry + ProcessIdField);
            if (processId == 0 || processId == _ownProcessId)
                continue;

            var time0 = view.ReadUInt32(entry + Time0Field);
            var time1 = view.ReadUInt32(entry + Time1Field);
            var frames = view.ReadUInt32(entry + FramesField);
            // Tick counts wrap, and RTSS can stamp time1 just after the tick count above was read.
            var age = unchecked((int)(now - time1));
            var span = unchecked(time1 - time0);
            if (frames == 0 || span == 0 || age > StaleMilliseconds)
                continue;

            var candidate = new Candidate(entry, 1000f * frames / span, view.ReadUInt32(entry + FrameTimeField) / 1000f);
            if (i == foregroundIndex && processId == foregroundProcessId)
                foreground = candidate;
            if (busiest is null || candidate.Framerate > busiest.Framerate)
                busiest = candidate;
        }

        return (foreground ?? busiest) is { } pick
            ? new RtssFrame(ReadName(view, pick.Entry), pick.Framerate, pick.FrametimeMs)
            : null;
    }

    public void Dispose()
    {
        _view?.Dispose();
        _file?.Dispose();
    }

    private bool TryOpen()
    {
        if (_view is not null)
            return true;
        if (Environment.TickCount64 < _nextOpenAttempt)
            return false;

        _nextOpenAttempt = Environment.TickCount64 + OpenRetryMilliseconds;
        try
        {
            _file = MemoryMappedFile.OpenExisting(MapName, MemoryMappedFileRights.Read);
            _view = _file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // RTSS is not running.
            _file?.Dispose();
            _file = null;
            return false;
        }
    }

    /// <summary>The file name of the entry's executable; RTSS stores the full path in the ANSI code page.</summary>
    private static unsafe string ReadName(MemoryMappedViewAccessor view, long entry)
    {
        var bytes = new byte[NameLength];
        view.ReadArray(entry + NameField, bytes, 0, NameLength);
        var length = Array.IndexOf(bytes, (byte)0);
        fixed (byte* name = bytes)
            return Path.GetFileName(Marshal.PtrToStringAnsi((nint)name, length < 0 ? NameLength : length));
    }

    private sealed record Candidate(long Entry, float Framerate, float FrametimeMs);
}

/// <param name="App">The executable's file name, such as "game.exe".</param>
public sealed record RtssFrame(string App, float Framerate, float FrametimeMs);
