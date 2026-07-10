// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/LogTailService.cs
//  역할: 각 프로그램의 로그 파일 테일링 → 신규 라인 이벤트 발행
//        대상: Manager 자신 + manager.json Processes[] 의 {exe폴더}\Log
//        lssLib.Log 파일 구조: {LogRoot}\yyyy_MM\dd\All.txt (일자별 롤오버)
//  MG-04: 신규
//  설계 메모:
//    - 매 틱(1초)마다 파일을 열고 읽고 닫는다 (핸들 미보관 —
//      대상 프로그램의 파일 잠금·삭제와 충돌하지 않도록 FileShare 전체 허용)
//    - 최초 발견 파일은 끝으로 이동(과거 이력 미출력) — 이후 증분만 읽음
//    - 파일 길이가 줄어들면(롤오버/삭제 후 재생성) offset 0 으로 리셋
//    - DispatcherTimer(UI 스레드) → 이벤트 구독자는 마샬링 불필요
//  개선(2026-07-09, 사용자 요청): ① 크기 롤링 대응 — All.txt 외 All_2.txt 등
//        최신(마지막 수정) 파일을 테일 대상으로 자동 선택
//        ② 라인 파싱(LogRow.Parse) 후 LogRow 이벤트로 발행 (표준 UI 컬럼 분리)
//  생성: 2026-07-09 / 수정: 2026-07-09
// ══════════════════════════════════════════════════════════

using IIoT.Manager.Core.Config;
using IIoT.Manager.Models;
using System.IO;
using System.Text;
using System.Windows.Threading;

namespace IIoT.Manager.Core;

/// <summary>
/// 로그 파일 테일링 서비스 (DI 싱글턴).
/// Start() 는 manager.json 로드 이후(ManagerMainViewModel.InitializeAsync)에 호출된다.
/// </summary>
public sealed class LogTailService : IDisposable
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly ManagerSettingsLoader _settingsLoader;
    private readonly DispatcherTimer       _timer;

    /// <summary>테일 대상: (표시 이름, Log 루트 폴더)</summary>
    private readonly List<(string Source, string LogRoot)> _targets = [];

    /// <summary>파일 경로별 읽기 오프셋 (마지막 개행까지 처리한 위치)</summary>
    private readonly Dictionary<string, long> _offsets = new();

    private bool _started;

    // §2 ─ 이벤트 ─────────────────────────────────────────────

    /// <summary>신규 로그 라인 수신 (파싱된 LogRow). UI 스레드에서 발생.</summary>
    public event Action<LogRow>? LineReceived;

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public LogTailService(ManagerSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            // ★ 규칙: 타이머 핸들러는 try/catch — 파일 IO 예외로 타이머가 죽지 않도록
            try   { _PollAll(); }
            catch (Exception ex)
            {
                lssLib.Log.LogManager.Instance.Warn("LogTail", $"테일링 오류: {ex.Message}");
            }
        };
    }

    // §4 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// 테일 대상을 구성하고 폴링을 시작한다 (manager.json 로드 후 호출, 재호출 무시).
    /// </summary>
    public void Start()
    {
        if (_started) return;
        _started = true;

        // ① Manager 자신
        _targets.Add(("IIoT.Manager",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log")));

        // ② manager.json 등록 프로그램들 — {exe 폴더}\Log
        foreach (var p in _settingsLoader.Settings.Processes)
        {
            var exePath = Path.IsPathRooted(p.ExePath)
                ? p.ExePath
                : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, p.ExePath));

            var dir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(dir)) continue;

            _targets.Add((p.Name, Path.Combine(dir, "Log")));
        }

        _timer.Start();
        lssLib.Log.LogManager.Instance.Info("LogTail",
            $"로그 테일링 시작 — 대상 {_targets.Count}개");
    }

    public void Dispose() => _timer.Stop();

    // §5 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>모든 대상의 오늘자 최신 All*.txt 증분을 읽어 이벤트로 발행한다.</summary>
    private void _PollAll()
    {
        var now    = DateTime.Now;
        var subDir = Path.Combine(now.ToString("yyyy_MM"), now.ToString("dd"));

        foreach (var (source, logRoot) in _targets)
        {
            var dayDir = Path.Combine(logRoot, subDir);
            if (!Directory.Exists(dayDir)) continue;

            // ★ 크기 롤링 대응: All.txt / All_2.txt / All_3.txt … 중
            //   마지막 수정 파일이 현재 기록 중인 파일
            string? path = null;
            var latest = DateTime.MinValue;
            foreach (var f in Directory.EnumerateFiles(dayDir, "All*.txt"))
            {
                var w = File.GetLastWriteTime(f);
                if (w > latest) { latest = w; path = f; }
            }
            if (path is null) continue;

            try
            {
                _TailFile(source, path);
            }
            catch (IOException)
            {
                // 대상 프로그램이 쓰는 중 순간 충돌 — 다음 틱에 재시도 (무해)
            }
        }
    }

    /// <summary>파일의 오프셋 이후 신규 라인을 읽는다 (마지막 개행까지만 처리).</summary>
    private void _TailFile(string source, string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                      FileShare.ReadWrite | FileShare.Delete);

        // 최초 발견 → 끝으로 이동 (과거 이력은 표시하지 않음)
        if (!_offsets.TryGetValue(path, out var offset))
        {
            _offsets[path] = fs.Length;
            return;
        }

        // 롤오버/재생성 감지 (길이 감소) → 처음부터
        if (fs.Length < offset) offset = 0;
        if (fs.Length == offset) return;

        fs.Seek(offset, SeekOrigin.Begin);
        var buf = new byte[fs.Length - offset];
        var read = fs.Read(buf, 0, buf.Length);
        if (read <= 0) return;

        // 마지막 개행까지만 처리 — 쓰다 만 라인은 다음 틱에
        int lastNl = Array.LastIndexOf(buf, (byte)'\n', read - 1);
        if (lastNl < 0) return;

        _offsets[path] = offset + lastNl + 1;

        var text = Encoding.UTF8.GetString(buf, 0, lastNl + 1);
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length > 0)
                LineReceived?.Invoke(LogRow.Parse(source, trimmed));
        }
    }
}
