using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LNXcounter.Services;

/// <summary>
/// Plays the official YouTube upload of a jam track's song. It does not download anything: a tiny off-screen WebView2 window
/// loads YouTube's own embedded player (a local page talks to it through the IFrame API) and the app only sends play, pause,
/// seek and volume commands.
/// </summary>
public class JamPlayer : Notify
{
    readonly Action<int> saveVolume;
    Window? host;
    WebView2? web;
    bool starting;

    public JamPlayer(int volume, Action<int> saveVolume)
    {
        this.volume = Math.Clamp(volume, 0, 100);
        this.saveVolume = saveVolume;
    }

    // ----- state the popup binds to -----
    string? videoId; bool playing, loading, failed; double pos, dur; int volume; string message = "";

    public string? VideoId => videoId;
    public bool IsPlaying { get => playing; private set { if (Set(ref playing, value)) Raise(nameof(PlayGlyph)); } }
    public bool IsLoading { get => loading; private set => Set(ref loading, value); }
    public bool Failed { get => failed; private set => Set(ref failed, value); }
    public string Message { get => message; private set => Set(ref message, value); }
    public string PlayGlyph => playing ? "❚❚" : "▶";

    /// <summary>Set while the user drags the progress bar, so the incoming time updates do not fight the thumb.</summary>
    public bool Scrubbing { get; set; }

    public double Pos { get => pos; private set { if (Set(ref pos, value)) Raise(nameof(TimeText)); } }
    public double Dur { get => dur; private set { if (Set(ref dur, value)) { Raise(nameof(TimeText)); Raise(nameof(DurMax)); } } }
    public double DurMax => Math.Max(dur, 1);
    public string TimeText => $"{Fmt(pos)} / {Fmt(dur)}";

    public int Volume
    {
        get => volume;
        set
        {
            value = Math.Clamp(value, 0, 100);
            if (!Set(ref volume, value)) return;
            Raise(nameof(VolumeGlyph));
            Script($"player && player.setVolume({value});");
            saveVolume(value);
        }
    }
    public string VolumeGlyph => volume == 0 ? "🔇" : volume < 40 ? "🔈" : volume < 75 ? "🔉" : "🔊";

    static string Fmt(double s) { var t = TimeSpan.FromSeconds(Math.Max(0, s)); return $"{(int)t.TotalMinutes}:{t.Seconds:00}"; }

    // ----- controls -----
    public async Task ToggleAsync(string id)
    {
        if (videoId == id && web?.CoreWebView2 != null && !failed)
        {
            if (playing || loading) Script("player.pauseVideo();"); else Script("player.playVideo();");
            return;
        }
        await StartAsync(id);
    }

    // The browser behind the preview is only kept alive while it is useful: it is released a short while after the song stops
    // (or a few minutes after a pause, remembering the position), which frees all of its memory.
    DispatcherTimer? idle;
    double resumeAt;
    bool ended;

    void UpdateIdle()
    {
        if (web == null) return;
        if (playing || loading) { idle?.Stop(); return; }
        idle ??= new DispatcherTimer();
        idle.Interval = TimeSpan.FromSeconds(videoId == null || ended ? 20 : 180);
        idle.Tick -= OnIdle; idle.Tick += OnIdle;
        idle.Stop(); idle.Start();
    }

    void OnIdle(object? s, EventArgs e)
    {
        idle?.Stop();
        if (playing || loading || starting) return;
        Release();
    }

    void Release()
    {
        if (web == null) return;
        resumeAt = videoId != null && !ended ? pos : 0;
        try { web.Dispose(); } catch { }
        try { host?.Close(); } catch { }
        web = null; host = null;
        Log("released");
        MemoryTrim.Schedule(1500);
    }

    async Task StartAsync(string id)
    {
        if (starting) return;
        starting = true;
        try
        {
            double from = videoId == id && !ended ? resumeAt : 0;
            resumeAt = 0; ended = false;
            Failed = false; Message = ""; IsLoading = true; Pos = from; Dur = 0; videoId = id;
            idle?.Stop();
            Log("start " + id);
            await EnsureWebAsync();
            Log("web ready, navigating");
            web!.CoreWebView2.Navigate($"https://lnx.player/player.html?v={Uri.EscapeDataString(id)}&vol={volume}&t={(int)from}");
        }
        catch (Exception e)
        {
            Log("start failed: " + e);
            IsLoading = false; IsPlaying = false; Failed = true;
            Message = e is WebView2RuntimeNotFoundException ? "Preview needs the Microsoft Edge WebView2 runtime." : "Could not start the preview: " + e.Message;
        }
        finally { starting = false; }
    }

    public void Seek(double seconds) { if (dur > 0) { Pos = seconds; Script($"player && player.seekTo({seconds.ToString("0.##", CultureInfo.InvariantCulture)}, true);"); } }

    public void Stop()
    {
        if (videoId == null) return;
        videoId = null; IsPlaying = false; IsLoading = false; Failed = false; Message = ""; Pos = 0; Dur = 0;
        try { web?.CoreWebView2?.Navigate("about:blank"); } catch { }
        UpdateIdle();
    }

    void Script(string js)
    {
        try { _ = web?.CoreWebView2?.ExecuteScriptAsync(js); } catch { }
    }

    // ----- WebView plumbing -----
    // The page is only used to play a song, so the browser is run as light as possible: no GPU process, one renderer, no site isolation,
    // no extensions or background services. This cuts its memory use by more than half.
    const string BrowserArgs = "--autoplay-policy=no-user-gesture-required --disable-gpu --disable-site-isolation-trials " +
        "--disable-features=IsolateOrigins,site-per-process,Translate,OptimizationHints --renderer-process-limit=1 " +
        "--disable-extensions --disable-background-networking --disable-sync --no-first-run";

    async Task EnsureWebAsync()
    {
        if (web?.CoreWebView2 != null) return;
        var dir = Path.Combine(Store.Dir, "web");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "player.html"), PlayerHtml);

        host = new Window
        {
            Width = 260, Height = 160, WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false, Focusable = false,
            Left = -20000, Top = -20000, ResizeMode = ResizeMode.NoResize,
        };
        web = new WebView2();
        host.Content = web;
        host.Show();

        var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Store.Dir, "webview2"),
            new CoreWebView2EnvironmentOptions(BrowserArgs));
        await web.EnsureCoreWebView2Async(env);
        web.CoreWebView2.SetVirtualHostNameToFolderMapping("lnx.player", dir, CoreWebView2HostResourceAccessKind.Allow);
        web.CoreWebView2.WebMessageReceived += OnMessage;
        web.CoreWebView2.NavigationCompleted += (_, a) => Log("nav done " + a.IsSuccess + " " + a.WebErrorStatus);
        web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        web.CoreWebView2.Settings.IsStatusBarEnabled = false;
    }

    void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.TryGetWebMessageAsString());
            var r = doc.RootElement;
            switch (r.GetProperty("t").GetString())
            {
                case "time":
                    if (!Scrubbing) Pos = r.GetProperty("c").GetDouble();
                    Dur = r.GetProperty("d").GetDouble();
                    break;
                case "state":
                    int s = r.GetProperty("s").GetInt32();      // -1 unstarted, 0 ended, 1 playing, 2 paused, 3 buffering, 5 cued
                    IsPlaying = s == 1;
                    IsLoading = s == 3 || s == -1 || s == 5;
                    ended = s == 0;
                    if (ended) { IsPlaying = false; IsLoading = false; Pos = 0; }
                    UpdateIdle();
                    break;
                case "log":
                    Log(r.GetProperty("m").GetString() ?? "");
                    break;
                case "error":
                    IsPlaying = false; IsLoading = false; Failed = true;
                    Message = "YouTube would not play this one inside the app.";
                    break;
            }
        }
        catch (Exception ex) { Log("OnMessage failed: " + ex); }
    }

    static readonly bool Debug = Environment.GetEnvironmentVariable("LNX_DEBUG") == "1";
    static void Log(string m) { if (!Debug) return; try { File.AppendAllText(Path.Combine(Store.Dir, "jam.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + m + Environment.NewLine); } catch { } }

    public static string WatchUrl(string id) => "https://www.youtube.com/watch?v=" + id;

    const string PlayerHtml = @"<!doctype html><html><head><meta charset='utf-8'></head>
<body style='margin:0;background:#000;overflow:hidden'>
<div id='p'></div>
<script>
function post(o) { try { window.chrome.webview.postMessage(JSON.stringify(o)); } catch (e) {} }
window.onerror = function (m) { post({ t: 'log', m: 'js error: ' + m }); };
post({ t: 'log', m: 'page loaded ' + location.href });
</script>
<script>function apiFail() { post({ t: 'log', m: 'iframe_api failed to load' }); }</script>
<script src='https://www.youtube.com/iframe_api' onerror='apiFail()'></script>
<script>
var q = new URLSearchParams(location.search), player = null, ready = false;
var started = false;
function startPlayer() {
  if (started) return; started = true;
  post({ t: 'log', m: 'api ready' });
  player = new YT.Player('p', {
    width: '240', height: '135', videoId: q.get('v'),
    playerVars: { autoplay: 1, controls: 0, playsinline: 1, rel: 0, start: parseInt(q.get('t') || '0'), origin: location.origin },
    events: {
      onReady: function (e) { ready = true; e.target.setVolume(parseInt(q.get('vol') || '60')); e.target.playVideo(); },
      onStateChange: function (e) { post({ t: 'state', s: e.data }); },
      onError: function (e) { post({ t: 'error', c: e.data }); }
    }
  });
}
window.onYouTubeIframeApiReady = startPlayer;
var poll = setInterval(function () { if (window.YT && typeof YT.Player === 'function') { clearInterval(poll); startPlayer(); } }, 150);
setInterval(function () { if (ready && player.getDuration) post({ t: 'time', c: player.getCurrentTime(), d: player.getDuration() }); }, 400);
</script></body></html>";
}
