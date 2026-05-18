
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace lssLib.Log
{
    /// <summary>
    /// 로그 저장/관리 싱글톤 매니저
    /// ─────────────────────────────────────────────────────
    ///  • 레벨별 로그 (Debug / Info / Warn / Error / Fatal)
    ///  • 파일 롤링 (일별 폴더 + 크기별 번호 롤링)
    ///  • Debug 출력창 + 파일 동시 출력
    ///  • Channel<T> 기반 비동기 큐 처리 (단일 컨슈머)
    ///  • 파일 출력: All.txt + {Source}.txt 분류 저장
    ///  • LogAdded 이벤트 → UI 컨트롤 연동
    ///  • AOP 공통 래퍼 (Execute / ExecuteAsync, 반환형 有/無)
    /// ─────────────────────────────────────────────────────
    /// [기본 사용 예시]
    ///   // 시작
    ///   LogManager.Instance.Start(new LogConfig { ValidDays = 14 });
    ///
    ///   // 레벨별 로그 추가
    ///   LogManager.Instance.Debug("Network", "소켓 연결 시도");
    ///   LogManager.Instance.Info ("Network", "연결 성공");
    ///   LogManager.Instance.Warn ("Network", "응답 지연 (800ms)");
    ///   LogManager.Instance.Error("Database", "쿼리 실패: " + ex.Message);
    ///   LogManager.Instance.Fatal("Database", "DB 연결 불가 - 서비스 중단");
    ///
    ///   // 종료
    ///   await LogManager.Instance.StopAsync();
    ///
    /// ─────────────────────────────────────────────────────
    /// [AOP 래퍼 사용 예시]
    ///
    ///   // ① 반환값 없음 · 동기
    ///   //    - source(함수명)는 CallerMemberName이 자동 주입
    ///   LogManager.Execute(
    ///       tryAction:    () => Socket.Connect(),
    ///       catchAction:  () => Reconnect(),
    ///       finallyAction: () => UpdateUI(),
    ///       category: "Network"
    ///   );
    ///
    ///   // ② 반환값 없음 · 비동기
    ///   await LogManager.ExecuteAsync(
    ///       tryAction:    async () => await DB.QueryAsync(),
    ///       catchAction:  () => ReturnEmpty(),
    ///       category: "Database"
    ///   );
    ///
    ///   // ③ 반환값 있음 · 동기  (성공 시 결과값, 실패 시 기본값 반환)
    ///   bool isConnected = LogManager.Execute(
    ///       tryFunc:   () => Socket.TryConnect(),
    ///       catchFunc: () => false,
    ///       category:  "Network"
    ///   );
    ///
    ///   // ④ 반환값 있음 · 비동기
    ///   List&lt;User&gt; users = await LogManager.ExecuteAsync(
    ///       tryFunc:   async () => await DB.GetUsersAsync(),
    ///       catchFunc: () => new List&lt;User&gt;(),
    ///       category:  "Database"
    ///   );
    /// </summary>
    public sealed class LogManager
    {
        // ════════════════════════════════════════════════════
        //  Singleton (Lazy - thread-safe)
        //  1._lazy.Value에 처음 접근하는 순간에만 객체를 생성
        //  2.여러 스레드가 동시에 접근하더라도 Lazy<T> 내부에서 락을 사용하여 다중 스레드 안전 보장
        // ════════════════════════════════════════════════════
        #region Singleton
        private static readonly Lazy<LogManager> _lazy= new Lazy<LogManager>(() => new LogManager());

        public static LogManager Instance => _lazy.Value;

        private LogManager() { }
        #endregion

        // ════════════════════════════════════════════════════
        //  이벤트
        // ════════════════════════════════════════════════════
        #region Events
        /// <summary>
        /// 로그 항목이 처리될 때 발생.
        /// UI 컨트롤(LogViewerControl)이 구독하여 화면에 표시.
        /// ※ 호출 스레드: 내부 Task → UI 측에서 Dispatcher 처리 필요
        ///
        /// ─────────────────────────────────────────────────
        /// [사용 예시]
        ///
        ///   // ① 구독 - 람다
        ///   LogManager.Instance.LogAdded += data =>
        ///   {
        ///       Dispatcher.InvokeAsync(() =>
        ///       {
        ///           MyListBox.Items.Insert(0, $"{data.Date} [{data.LevelText}] {data.Contents}");
        ///       });
        ///   };
        ///
        ///   // ② 구독 - 메서드 연결
        ///   LogManager.Instance.LogAdded += OnLogAdded;
        ///
        ///   private void OnLogAdded(LogData data)
        ///   {
        ///       // 백그라운드 스레드에서 호출되므로 UI 접근 시 Dispatcher 필요
        ///       Dispatcher.InvokeAsync(() =>
        ///       {
        ///           // data.Date      → "2025_03_22 14:30:25.123"
        ///           // data.LevelText → "INFO"
        ///           // data.Source    → "Network"
        ///           // data.Contents  → "연결 성공"
        ///           TxtLastLog.Text = data.ToString();
        ///       });
        ///   }
        ///
        ///   // ③ 구독 해제 (메모리 누수 방지 - Unloaded 또는 Dispose 시점에 반드시 해제)
        ///   LogManager.Instance.LogAdded -= OnLogAdded;
        ///
        ///   // ④ Error 이상 레벨만 별도 처리
        ///   LogManager.Instance.LogAdded += data =>
        ///   {
        ///       if (data.Level >= LogLevel.Error)
        ///           Dispatcher.InvokeAsync(() => ShowAlert(data.Contents));
        ///   };
        /// ─────────────────────────────────────────────────
        /// </summary>
        public event Action<LogData> LogAdded;
        #endregion

        // ════════════════════════════════════════════════════
        //  필드
        // ════════════════════════════════════════════════════
        #region Fields
        private LogConfig _config = new LogConfig();
        
        /// <summary>
        /// 로그 항목을 비동기적으로 처리하기 위한 채널. 고성능 비동기 큐(Queue)
        /// 생산자-소비자 패턴: 한쪽에서는 로그를 계속 넣고(WriteAsync), 
        ///                     다른 한쪽에서는 하나씩 꺼내서(ReadAsync) 처리하는 구조를 만들기 매우 쉽다.
        ///  예시 - 통신에 사용하면 좋은 구조 일꺼같음
        /// // 1. 채널 생성 (무제한 또는 용량 제한 설정 가능)
        /// var channel = Channel.CreateUnbounded<LogData>();
        /// // 2. 로그 던지기 (생산자)
        /// await channel.Writer.WriteAsync(new LogData { Message = "시스템 시작" });
        /// // 3. 로그 처리하기 (소비자 - 별도 루프에서 실행)
        /// await foreach (var log in channel.Reader.ReadAllAsync())
        /// {
        /// Console.WriteLine(log.Message);
        /// }
        /// </summary>
        private Channel<LogData> _channel;

        /// <summary>
        /// 비동기 작업이나 스레드를 안전하게 중단시키기 위해 사용하는 "정지 신호기"
        /// 프로그램이 종료될 때 그 무한 루프를 깔끔하게 끝내기 위해 반드시 필요
        /// 핵심역활
        /// 신호 발생기 (CTS): "이제 그만해!"라고 신호를 보내는 주체입니다. (_cts.Cancel())
        /// 신호 전달자 (Token): 작업 중인 메서드에 전달되는 실제 티켓입니다. (_cts.Token)
        /// 예시  - 무한 루프에서 CTS 사용하기, 로그를 처리하는 백그라운드 루프
        ///public async Task ProcessLogsAsync()
        ///{
        ///    try
        ///    {
        ///        // 토큰이 취소될 때까지 계속 실행
        ///        await foreach (var log in _channel.Reader.ReadAllAsync(_cts.Token))
        ///        {
        ///            // 로그 처리 로직 (파일 쓰기 등)
        ///        }
        ///    }
        ///    catch (OperationCanceledException)
        ///    {
        ///        // Cancel()이 호출되면 이쪽으로 들어와서 안전하게 종료됨
        ///    }
        ///}
        /// // 종료 버튼이나 프로그램 종료 시 호출
        ///public void Stop()
        ///{
        ///    _cts.Cancel(); // "그만!" 신호를 보냄
        ///}
        /// </summary>
        private CancellationTokenSource _cts;
        private Task _processTask;
        private Task _validDayTask;
        #endregion

        // ════════════════════════════════════════════════════
        //  상태
        // ════════════════════════════════════════════════════
        /// <summary>현재 실행 중 여부</summary>
        public bool IsRunning { get; private set; } = false;

        /// <summary>현재 설정</summary>
        public LogConfig Config => _config;

        // ════════════════════════════════════════════════════
        //  시작 / 정지
        // ════════════════════════════════════════════════════
        #region Start / Stop
        /// <summary>로그 매니저 시작</summary>
        /// <param name="config">설정 (null 이면 기본값 사용)</param>
        public void Start(LogConfig config = null)
        {
            if (IsRunning) return;

            if (config != null) _config = config;

            // 채널 생성
            // BoundedChannelOptions(_config.ChannelCapacity): 큐에 담을 수 있는 최대 로그 개수(예: 10,000개)를 정합
            // FullMode = BoundedChannelFullMode.DropOldest: 큐가 가득 찼을 때 가장 오래된 로그를 버리고새 로그를 추가하도록 설정
            // SingleReader = true: 큐에서 로그를 읽는 소비자(컨슈머)가 하나뿐임을 명시하여 내부 최적화 가능
            // UnboundedChannelOptions: 들어오는 대로 다 받습니다.
            //                          로그 유실이 없어야 하는 아주 중요한 시스템에서 쓰지만,
            //                          로그가 너무 폭주하면 메모리 부족(OutOfMemory)으로 프로그램이 죽을 위험이 있음.
            // SingleReader = true: 마찬가지로 읽기 성능을 최적화
            _channel = _config.ChannelCapacity > 0
                ? Channel.CreateBounded<LogData>(new BoundedChannelOptions(_config.ChannelCapacity) 
                { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true })
                : Channel.CreateUnbounded<LogData>(
                    new UnboundedChannelOptions { SingleReader = true });

            EnsureDirectory(_config.LogRootPath);

            _cts = new CancellationTokenSource();
            _processTask = Task.Run(() => ProcessQueueAsync(_cts.Token));
            _validDayTask = Task.Run(() => ValidDayManagerAsync(_cts.Token));

            IsRunning = true;
        }

        /// <summary>로그 매니저 비동기 정지. 큐에 남은 항목 소비 후 종료.</summary>
        public async Task StopAsync()
        {
            if (!IsRunning) return;
            IsRunning = false;

            _channel.Writer.Complete();   // 큐 마감 신호
            _cts.Cancel();                // ValidDayTask 종료 신호

            await Task.WhenAll(_processTask, _validDayTask).ConfigureAwait(false);
            _cts.Dispose();
        }

        /// <summary>동기 정지 (앱 종료 시 사용)</summary>
        public void Stop() => StopAsync().GetAwaiter().GetResult();
        #endregion

        // ════════════════════════════════════════════════════
        //  로그 추가
        // ════════════════════════════════════════════════════
        #region AddLog
        /// <summary>로그 추가 (현재 시각 자동 입력)</summary>
        /// <param name="level">심각도 레벨</param>
        /// <param name="source">발생 출처 (파일명 분류 기준: Network, Database 등)</param>
        /// <param name="contents">로그 내용</param>
        public void AddLog(LogLevel level, string source, string contents)
        {
            if (!IsRunning || level < _config.MinimumLevel) return;
            _channel.Writer.TryWrite(new LogData(level, source, contents));
        }

        // ─── 레벨별 단축 메서드 ──────────────────────────────
        public void Debug(string source, string msg) => AddLog(LogLevel.Debug, source, msg);
        public void Info(string source, string msg) => AddLog(LogLevel.Info, source, msg);
        public void Warn(string source, string msg) => AddLog(LogLevel.Warn, source, msg);
        public void Error(string source, string msg) => AddLog(LogLevel.Error, source, msg);
        public void Fatal(string source, string msg) => AddLog(LogLevel.Fatal, source, msg);
        #endregion

        // ════════════════════════════════════════════════════
        //  비동기 큐 처리 (단일 컨슈머)
        // ════════════════════════════════════════════════════
        #region Queue Processing
        private async Task ProcessQueueAsync(CancellationToken ct)
        {
            try
            {
                await foreach (LogData data in _channel.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        if (_config.EnableFileOutput) WriteToFile(data);
                        WriteToConsole(data);

                        // UI 이벤트 발행 (구독자가 Dispatcher 처리)
                        LogAdded?.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LogManager] 처리 오류: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException) { /* 정상 종료 */ }
        }
        #endregion

        // ════════════════════════════════════════════════════
        //  파일 출력 (일별 폴더 + 크기 롤링)
        //   구조: LogRootPath / yyyy_MM / dd / All.txt  or  All.csv
        //                                    / {Source}.txt  or  {Source}.csv
        // ════════════════════════════════════════════════════
        #region File Output
        private void WriteToFile(LogData data)
        {
            string dayDir = Path.Combine(_config.LogRootPath, data.YearMonth, data.Day);
            EnsureDirectory(dayDir);

            bool writeTxt = _config.FileFormat == LogFileFormat.Txt ||
                            _config.FileFormat == LogFileFormat.Both;
            bool writeCsv = _config.FileFormat == LogFileFormat.Csv ||
                            _config.FileFormat == LogFileFormat.Both;

            // All 파일 (전체 로그)
            if (writeTxt) AppendTxt(dayDir, "All", data);
            if (writeCsv) AppendCsv(dayDir, "All", data);

            // {Source} 파일 (출처별 분류 로그)
            if (!string.IsNullOrWhiteSpace(data.Source))
            {
                string safeName = SanitizeFileName(data.Source);
                if (writeTxt) AppendTxt(dayDir, safeName, data);
                if (writeCsv) AppendCsv(dayDir, safeName, data);
            }
        }

        // ── TXT 출력 ─────────────────────────────────────────
        // 출력 형식: yyyy_MM_dd HH:mm:ss.fff  [LEVEL]  Source          Contents
        private void AppendTxt(string dir, string baseName, LogData data)
        {
            string path = GetRollingFilePath(dir, baseName, "txt");
            string line = $"{data.Date}  [{data.LevelText,-5}]  {data.Source,-16}  {data.Contents}";

            using var sw = new StreamWriter(path, append: true, Encoding.UTF8);
            sw.WriteLine(line);
        }

        // ── CSV 출력 ─────────────────────────────────────────
        // 날짜 컬럼: yyyy_MM_dd  /  시간 컬럼: HH:mm:ss.fff (ms 명시)
        private void AppendCsv(string dir, string baseName, LogData data)
        {
            string path = GetRollingFilePath(dir, baseName, "csv");
            bool isNew = !File.Exists(path) || new FileInfo(path).Length == 0;

            // Date = "yyyy_MM_dd HH:mm:ss.fff" → 날짜/시간 분리
            string[] parts = data.Date.Split(' ');
            string datePart = parts.Length > 0 ? parts[0] : data.Date;   // yyyy_MM_dd
            string timePart = parts.Length > 1 ? parts[1] : string.Empty; // HH:mm:ss.fff

            using var sw = new StreamWriter(path, append: true, Encoding.UTF8);

            // 신규 파일이면 CSV 헤더 먼저 기록
            if (isNew)
                sw.WriteLine("날짜,시간(HH:mm:ss.fff),레벨,출처,내용");

            // 내용 중 큰따옴표·쉼표·줄바꿈 이스케이프
            sw.WriteLine(
                $"\"{EscapeCsv(datePart)}\"," +
                $"\"{EscapeCsv(timePart)}\"," +
                $"\"{EscapeCsv(data.LevelText)}\"," +
                $"\"{EscapeCsv(data.Source)}\"," +
                $"\"{EscapeCsv(data.Contents)}\"");
        }

        /// <summary>CSV 셀 내 큰따옴표를 두 번 반복하여 이스케이프</summary>
        private static string EscapeCsv(string value)
            => (value ?? string.Empty).Replace("\"", "\"\"");

        // ── 롤링 파일 경로 결정 ──────────────────────────────
        /// <summary>
        /// 파일 크기 초과 시 번호를 붙여 새 파일로 롤링.
        /// All.txt → All_2.txt → ...  /  All.csv → All_2.csv → ...
        /// </summary>
        private string GetRollingFilePath(string dir, string baseName, string ext)
        {
            string candidate = Path.Combine(dir, $"{baseName}.{ext}");
            if (!File.Exists(candidate) ||
                new FileInfo(candidate).Length < _config.MaxFileSizeBytes)
                return candidate;

            int index = 2;
            while (true)
            {
                candidate = Path.Combine(dir, $"{baseName}_{index}.{ext}");
                if (!File.Exists(candidate) ||
                    new FileInfo(candidate).Length < _config.MaxFileSizeBytes)
                    return candidate;
                index++;
            }
        }
        #endregion

        // ════════════════════════════════════════════════════
        //  콘솔(Debug 출력창) 출력
        // ════════════════════════════════════════════════════
        #region Console Output
        /// <summary>
        /// Config 설정에 따라 Visual Studio 출력창에 로그를 출력한다.
        ///  • EnableConsoleOutput = false → 출력 안 함
        ///  • MinimumConsoleLevel        → 해당 레벨 미만은 출력 안 함
        /// </summary>
        private void WriteToConsole(LogData data)
        {
            if (!_config.EnableConsoleOutput) return;
            if (data.Level < _config.MinimumConsoleLevel) return;

            System.Diagnostics.Debug.WriteLine(data.ToString());
        }
        #endregion

        // ════════════════════════════════════════════════════
        //  유효일 관리 (1시간마다 검사, 설정 시각에 1회 실행)
        // ════════════════════════════════════════════════════
        #region Valid Day Manager
        private async Task ValidDayManagerAsync(CancellationToken ct)
        {
            int flagCount = 0;

            while (!ct.IsCancellationRequested)
            {
                try
                {   // Task.Delay(TimeSpan.FromHours(1), ct)
                    // TimeSpan.FromHours(1): 정확히 1시간을 기다리라는 설정
                    //.ConfigureAwait(false) : 
                    // 성능과 데드락 방지: 비동기 작업이 끝난 후, 원래 이 코드를 실행했던 스레드(예: UI 스레드)로 굳이 돌아가지 않겠다는 선언
                    // 로그 저장 같은 백그라운드 작업은 UI를 직접 건드리지 않으므로,
                    // 아무 스레드에서나 실행되게 함으로써 성능을 높이고 화면이 멈추는(Freezing) 현상을 방지합니다.
                    await Task.Delay(TimeSpan.FromHours(1), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }

                int hour = DateTime.Now.Hour;

                if (hour != _config.CheckHour)
                {
                    flagCount = 0;
                    continue;
                }

                if (flagCount == 0)
                {
                    DeleteExpiredLogs();
                    flagCount++;
                }
            }
        }

        /// <summary>
        /// 로그 루트 폴더 내의 년월/일 폴더를 검사하여, 현재 날짜로부터 ValidDays 이상 지난 로그 폴더를 삭제한다.
        /// </summary>
        private void DeleteExpiredLogs()
        {
            if (!Directory.Exists(_config.LogRootPath)) return;

            var today = DateTime.Today;
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            foreach (string monthDir in Directory.GetDirectories(_config.LogRootPath))
            {
                foreach (string dayDir in Directory.GetDirectories(monthDir))
                {
                    try
                    {
                        string dateStr = $"{Path.GetFileName(monthDir)}_{Path.GetFileName(dayDir)}";
                        if (!DateTime.TryParseExact(dateStr, "yyyy_MM_dd", culture,
                                System.Globalization.DateTimeStyles.None, out DateTime logDate))
                            continue;

                        if ((today - logDate).Days >= _config.ValidDays)
                        {
                            Directory.Delete(dayDir, recursive: true);
                            AddLog(LogLevel.Info, "LogManager", $"만료 로그 삭제: {dayDir}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LogManager] 삭제 오류: {ex.Message}");
                    }
                }

                // 빈 년월 폴더 정리
                try
                {
                    if (Directory.GetFileSystemEntries(monthDir).Length == 0)
                        Directory.Delete(monthDir);
                }
                catch { /* 무시 */ }
            }
        }
        #endregion

        // ════════════════════════════════════════════════════
        //  AOP 공통 래퍼 (CallerMemberName 자동 주입)
        //
        //  ┌─ public API (4개) ────────────────┐
        //  │  Execute          반환값 없음 · 동기              │
        //  │  ExecuteAsync     반환값 없음 · 비동기            │
        //  │  Execute<T>       반환값 있음 · 동기              │
        //  │  ExecuteAsync<T>  반환값 있음 · 비동기            │
        //  └───────┬────────----------─────┘
        //                  │ 위임 (thin wrapper)
        //  ┌─ private Core (2개) ───────────────┐
        //  │  ExecuteCore<T>       동기 핵심 로직 (1회만 작성)  │
        //  │  ExecuteCoreAsync<T>  비동기 핵심 로직 (1회만 작성)│
        //  └────────────--─────────────┘
        //
        //  void 케이스는 T = object, null 반환으로 통일
        //  → try/catch/finally + 로그 패턴이 코어에만 존재
        // ════════════════════════════════════════════════════
        #region AOP Wrappers

        // ── public: 반환값 없음 · 동기 ──────────────────────
        public static void Execute(
            Action tryAction,
            Action catchAction = null,
            Action finallyAction = null,
            LogLevel logLevel = LogLevel.Info,
            string category = "SYSTEM",
            [CallerMemberName] string source = "")
        {
            ExecuteCore<object>(
                tryFunc: () => { 
                    tryAction(); 
                    return null; 
                },
                catchFunc: 
                    catchAction != null ? () => {
                        catchAction();
                        return null;
                    } :   null,
                finallyAction: finallyAction,
                logLevel: logLevel,
                category: category,
                source: source);
        }

        // ── public: 반환값 없음 · 비동기 ────────────────────
        public static async Task ExecuteAsync(
            Func<Task> tryAction,
            Action catchAction = null,
            Action finallyAction = null,
            LogLevel logLevel = LogLevel.Info,
            string category = "SYSTEM",
            [CallerMemberName] string source = "")
        {
            await ExecuteCoreAsync<object>(
                tryFunc: async () => { 
                    await tryAction().ConfigureAwait(false); 
                    return null; 
                },
                catchFunc: 
                    catchAction != null ? () => { 
                        catchAction(); 
                        return null; 
                    } : null,
                finallyAction: finallyAction,
                logLevel: logLevel,
                category: category,
                source: source).ConfigureAwait(false);
        }

        // ── public: 반환값 있음 · 동기 ──────────────────────
        public static T Execute<T>(
            Func<T> tryFunc,
            Func<T> catchFunc = null,
            Action finallyAction = null,
            LogLevel logLevel = LogLevel.Info,
            string category = "SYSTEM",
            [CallerMemberName] string source = "")
        {
            return ExecuteCore<T>(
                tryFunc: tryFunc,
                catchFunc: catchFunc,
                finallyAction: finallyAction,
                logLevel: logLevel,
                category: category,
                source: source);
        }

        // ── public: 반환값 있음 · 비동기 ────────────────────
        public static async Task<T> ExecuteAsync<T>(
            Func<Task<T>> tryFunc,
            Func<T> catchFunc = null,
            Action finallyAction = null,
            LogLevel logLevel = LogLevel.Info,
            string category = "SYSTEM",
            [CallerMemberName] string source = "")
        {
            return await ExecuteCoreAsync<T>(
                tryFunc: tryFunc,
                catchFunc: catchFunc,
                finallyAction: finallyAction,
                logLevel: logLevel,
                category: category,
                source: source).ConfigureAwait(false);
        }

        // ════════════════════════════════════════════════════
        //  AOP Core - 실제 로직은 여기에만 존재
        // ════════════════════════════════════════════════════

        // ── private 동기 코어 ────────────────────────────────
        /// <summary>
        /// 동기 AOP 핵심 로직.
        /// Start 로그 → try/catch/finally → End 로그 패턴을 단 한 곳에서 관리.
        /// void 케이스는 T=object, null 반환으로 호출.
        /// </summary>
        private static T ExecuteCore<T>(
            Func<T> tryFunc,
            Func<T> catchFunc,
            Action finallyAction,
            LogLevel logLevel,
            string category,
            string source)
        {
            Instance.AddLog(logLevel, source, $"[{category}] Start");
            T result = default;
            try
            {
                result = tryFunc();
            }
            catch (Exception ex)
            {
                Instance.AddLog(LogLevel.Error, source, $"[{category}] Error - {ex.Message}");
                result = catchFunc != null ? catchFunc() : default;
            }
            finally
            {
                try { finallyAction?.Invoke(); }
                catch (Exception ex)
                {
                    Instance.AddLog(LogLevel.Error, source,
                        $"[{category}] Finally Error - {ex.Message}");
                }
                Instance.AddLog(logLevel, source, $"[{category}] End");
            }
            return result;
        }

        // ── private 비동기 코어 ──────────────────────────────
        /// <summary>
        /// 비동기 AOP 핵심 로직.
        /// void 케이스는 T=object, null 반환 Task로 호출.
        /// </summary>
        private static async Task<T> ExecuteCoreAsync<T>(
            Func<Task<T>> tryFunc,
            Func<T> catchFunc,
            Action finallyAction,
            LogLevel logLevel,
            string category,
            string source)
        {
            Instance.AddLog(logLevel, source, $"[{category}] Start");
            T result = default;
            try
            {
                result = await tryFunc().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Instance.AddLog(LogLevel.Error, source, $"[{category}] Error - {ex.Message}");
                result = catchFunc != null ? catchFunc() : default;
            }
            finally
            {
                try { finallyAction?.Invoke(); }
                catch (Exception ex)
                {
                    Instance.AddLog(LogLevel.Error, source,
                        $"[{category}] Finally Error - {ex.Message}");
                }
                Instance.AddLog(logLevel, source, $"[{category}] End");
            }
            return result;
        }
        #endregion


        // ════════════════════════════════════════════════════
        //  유틸리티
        //  ※ 추후 공용 유틸리티 모듈로 분리 예정
        //     - 파일/디렉토리 관련  → FileUtility 모듈
        //     - 문자열 처리 관련    → StringUtility 모듈
        // ════════════════════════════════════════════════════
        #region Helpers

        /// <summary>
        /// [FileUtility 이관 예정]
        /// 지정 경로의 디렉토리가 없으면 생성한다.
        /// 중간 경로가 없어도 한 번에 생성됨 (CreateDirectory 재귀 생성 지원)
        /// </summary>
        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        /// <summary>
        /// [StringUtility 이관 예정]
        /// 파일명으로 사용할 수 없는 문자( \ / : * ? " &lt; &gt; | 등)를
        /// 언더스코어(_)로 치환하여 안전한 파일명을 반환한다.
        /// 입력값이 비어있으면 "Unknown" 반환.
        /// 예) "Net/work" → "Net_work"
        /// </summary>
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
        #endregion
    }
}