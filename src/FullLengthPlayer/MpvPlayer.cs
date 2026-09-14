using System.Globalization;
using System.Runtime.InteropServices;

namespace FullLengthPlayer;

// Owns one native player; all calls and event polling run on the UI thread.
internal sealed class MpvPlayer : IDisposable
{
    private IntPtr handle;
    public MpvPlayer(IntPtr window, bool verification = false, string? pcmFile = null, double start = 0, double length = 0)
    {
        handle = Native.mpv_create();
        if (handle == IntPtr.Zero) throw new InvalidOperationException("MPV could not be created.");
        try
        {
            Option("config", "no");
            Option("terminal", "no");
            Option("msg-level", "all=no");
            Option("ytdl", "no");
            Option("tls-verify", "yes");
            Option("network-timeout", "20");
            if (pcmFile != null)
            {
                Option("vid", "no"); Option("sid", "no");
                Option("ao", "pcm"); Option("ao-pcm-file", pcmFile);
                Option("ao-pcm-waveheader", "no"); Option("audio-format", "s16");
                Option("audio-samplerate", "8000"); Option("audio-channels", "mono");
                Option("untimed", "yes"); Option("keep-open", "no");
                Option("start", start.ToString(CultureInfo.InvariantCulture));
                Option("length", length.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                Option("wid", window.ToInt64().ToString(CultureInfo.InvariantCulture));
                Option("vo", "gpu"); Option("gpu-api", "d3d11");
                Option("hwdec", "auto-safe"); Option("keep-open", "yes");
                Option("input-default-bindings", "no"); Option("input-vo-keyboard", "no");
                Option("osc", "no");
                if (verification) Option("ao", "null"); // Hosted runner has no speakers.
            }
            Check(Native.mpv_initialize(handle));
        }
        catch { Dispose(); throw; }
    }
    private void Option(string name, string value)
    {
        int result = Native.mpv_set_option_string(handle, name, value);
        if (result < 0) throw new InvalidOperationException($"MPV option {name}={value}: {Error(result)}");
    }
    public void Set(string name, string value) => Check(Native.mpv_set_property_string(handle, name, value));
    // Pass a native array so commas, backslashes and UTF-8 header values stay literal.
    public void SetStringList(string name, string[] values)
    {
        var strings = new List<IntPtr>();
        IntPtr nodes = IntPtr.Zero, list = IntPtr.Zero;
        try
        {
            int stride = Marshal.SizeOf<Native.Node>();
            nodes = Marshal.AllocHGlobal(Math.Max(1, values.Length) * stride);
            for (int i = 0; i < values.Length; i++)
            {
                var text = Marshal.StringToCoTaskMemUTF8(values[i]); strings.Add(text);
                Marshal.StructureToPtr(new Native.Node { Value = text, Format = 1 }, nodes + i * stride, false);
            }
            list = Marshal.AllocHGlobal(Marshal.SizeOf<Native.NodeList>());
            Marshal.StructureToPtr(new Native.NodeList { Count = values.Length, Values = nodes }, list, false);
            var root = new Native.Node { Value = list, Format = 7 };
            Check(Native.mpv_set_property(handle, name, 6, ref root));
        }
        finally
        {
            foreach (var text in strings) Marshal.FreeCoTaskMem(text);
            if (nodes != IntPtr.Zero) Marshal.FreeHGlobal(nodes);
            if (list != IntPtr.Zero) Marshal.FreeHGlobal(list);
        }
    }
    public string? Get(string name)
    {
        var value = Native.mpv_get_property_string(handle, name);
        if (value == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUTF8(value); }
        finally { Native.mpv_free(value); }
    }
    public double Number(string name) => double.TryParse(Get(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
    public void Command(params string[] args)
    {
        var pointers = new IntPtr[args.Length + 1];
        try
        {
            for (int i = 0; i < args.Length; i++) pointers[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
            Check(Native.mpv_command(handle, pointers));
        }
        finally { foreach (var p in pointers) if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p); }
    }
    internal int FilesLoaded { get; private set; }
    internal bool Ended { get; private set; }
    public string? PollError()
    {
        string? error = null;
        for (int i = 0; i < 100; i++)
        {
            var ev = Marshal.PtrToStructure<Native.Event>(Native.mpv_wait_event(handle, 0));
            if (ev.Id == 0) break;
            if (ev.Id == 8) FilesLoaded++;
            if (ev.Id == 7 && ev.Data != IntPtr.Zero)
            {
                Ended = true;
                var end = Marshal.PtrToStructure<Native.EndFile>(ev.Data);
                if (end.Reason == 4) error = Error(end.Error);
            }
        }
        return error;
    }
    private static string Error(int code) => Marshal.PtrToStringUTF8(Native.mpv_error_string(code)) ?? $"MPV error {code}";
    private static void Check(int code) { if (code < 0) throw new InvalidOperationException(Error(code)); }
    public void Dispose() { if (handle != IntPtr.Zero) { Native.mpv_terminate_destroy(handle); handle = IntPtr.Zero; } }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Node { public IntPtr Value; public int Format; }
        [StructLayout(LayoutKind.Sequential)] internal struct NodeList { public int Count; public IntPtr Values, Keys; }
        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern int mpv_set_property(IntPtr h, [MarshalAs(UnmanagedType.LPUTF8Str)] string n, int format, ref Node data);
        private const string Dll = "libmpv-2.dll";
        [StructLayout(LayoutKind.Sequential)] internal struct Event { public int Id, Error; public ulong UserData; public IntPtr Data; }
        [StructLayout(LayoutKind.Sequential)] internal struct EndFile { public int Reason, Error; }
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr mpv_create();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int mpv_initialize(IntPtr h);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern void mpv_terminate_destroy(IntPtr h);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int mpv_set_option_string(IntPtr h, [MarshalAs(UnmanagedType.LPUTF8Str)] string n, [MarshalAs(UnmanagedType.LPUTF8Str)] string v);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int mpv_set_property_string(IntPtr h, [MarshalAs(UnmanagedType.LPUTF8Str)] string n, [MarshalAs(UnmanagedType.LPUTF8Str)] string v);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr mpv_get_property_string(IntPtr h, [MarshalAs(UnmanagedType.LPUTF8Str)] string n);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int mpv_command(IntPtr h, [In] IntPtr[] args);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr mpv_wait_event(IntPtr h, double timeout);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr mpv_error_string(int error);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern void mpv_free(IntPtr data);
    }
}
