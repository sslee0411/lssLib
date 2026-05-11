// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Watcher/ConfigFileWatcher.cs
//  역할: FileSystemWatcher 래퍼 — 디바운스(300ms) + 다중 파일 지원
// ══════════════════════════════════════════════════════════════════════════
using System.IO;

namespace lssLib.Config.Watcher;

/// <summary>
/// 설정 파일 변경 감지기.
/// </summary>
/// <remarks>
/// <see cref="FileSystemWatcher"/> 를 래핑하며, 연속 변경 이벤트를 300ms 디바운스로 병합합니다.
/// <para>여러 파일을 동시에 감시할 수 있습니다.</para>
/// <example><code>
/// var watcher = new ConfigFileWatcher();
/// watcher.FileChanged += path => Console.WriteLine($"변경됨: {path}");
/// watcher.Watch("app.ini");
/// watcher.Watch("override.json");
///
/// // 앱 종료 시
/// watcher.Dispose();
/// </code></example>
/// </remarks>
public sealed class ConfigFileWatcher : IDisposable
{
    #region §1 ─ 필드

    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
    private readonly Dictionary<string, Timer> _debounce = new();
    private readonly object _lock = new();
    private readonly TimeSpan _debounceDelay;
    private bool _disposed;

    #endregion

    #region §2 ─ 이벤트

    /// <summary>
    /// 설정 파일이 변경되었을 때 발생합니다.
    /// </summary>
    /// <remarks>
    /// 인자: 변경된 파일의 전체 경로 (절대 경로).
    /// 디바운스(300ms) 이후 단 1회 발생합니다.
    /// </remarks>
    public event Action<string>? FileChanged;

    #endregion

    #region §3 ─ 생성자

    /// <summary>
    /// <see cref="ConfigFileWatcher"/> 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="debounceDelay">디바운스 지연 시간. 기본 300ms.</param>
    public ConfigFileWatcher(TimeSpan? debounceDelay = null)
    {
        _debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(300);
    }

    #endregion

    #region §4 ─ 감시 제어

    /// <summary>
    /// 지정 파일을 감시 목록에 추가합니다.
    /// </summary>
    /// <param name="filePath">감시할 파일 전체 경로.</param>
    /// <exception cref="FileNotFoundException">파일이 존재하지 않는 경우.</exception>
    public void Watch(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var fullPath = Path.GetFullPath(filePath);
        var dir = Path.GetDirectoryName(fullPath)
                       ?? throw new ArgumentException("디렉터리를 확인할 수 없습니다.", nameof(filePath));
        var fileName = Path.GetFileName(fullPath);

        lock (_lock)
        {
            if (_watchers.ContainsKey(fullPath)) return;   // 이미 감시 중

            var fsw = new FileSystemWatcher(dir, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            fsw.Changed += (_, e) => OnRawChanged(e.FullPath);
            fsw.Created += (_, e) => OnRawChanged(e.FullPath);

            _watchers[fullPath] = fsw;
        }
    }

    /// <summary>
    /// 지정 파일을 감시 목록에서 제거합니다.
    /// </summary>
    public void Unwatch(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        lock (_lock)
        {
            if (_watchers.TryGetValue(fullPath, out var fsw))
            {
                fsw.EnableRaisingEvents = false;
                fsw.Dispose();
                _watchers.Remove(fullPath);
            }
            if (_debounce.TryGetValue(fullPath, out var timer))
            {
                timer.Dispose();
                _debounce.Remove(fullPath);
            }
        }
    }

    /// <summary>
    /// 현재 감시 중인 파일 목록을 반환합니다.
    /// </summary>
    public IReadOnlyList<string> WatchedFiles
    {
        get
        {
            lock (_lock)
                return _watchers.Keys.ToList();
        }
    }

    /// <summary>
    /// 감시를 일시 중단합니다.
    /// </summary>
    public void Pause()
    {
        lock (_lock)
            foreach (var fsw in _watchers.Values)
                fsw.EnableRaisingEvents = false;
    }

    /// <summary>
    /// 감시를 재개합니다.
    /// </summary>
    public void Resume()
    {
        lock (_lock)
            foreach (var fsw in _watchers.Values)
                fsw.EnableRaisingEvents = true;
    }

    #endregion

    #region §5 ─ 내부 디바운스 처리

    private void OnRawChanged(string fullPath)
    {
        lock (_lock)
        {
            // 기존 타이머 리셋 (연속 변경 병합)
            if (_debounce.TryGetValue(fullPath, out var existing))
            {
                existing.Change(_debounceDelay, Timeout.InfiniteTimeSpan);
                return;
            }

            // 새 타이머 생성
            var timer = new Timer(_ =>
            {
                FileChanged?.Invoke(fullPath);
                lock (_lock)
                {
                    if (_debounce.TryGetValue(fullPath, out var t))
                    {
                        t.Dispose();
                        _debounce.Remove(fullPath);
                    }
                }
            }, null, _debounceDelay, Timeout.InfiniteTimeSpan);

            _debounce[fullPath] = timer;
        }
    }

    #endregion

    #region §6 ─ IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var fsw in _watchers.Values) fsw.Dispose();
            foreach (var t in _debounce.Values) t.Dispose();
            _watchers.Clear();
            _debounce.Clear();
        }
    }

    #endregion
}