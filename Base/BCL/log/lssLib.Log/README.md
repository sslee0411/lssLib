# lssLib.Log

**비동기 로그 관리 라이브러리** · .NET 8.0 · C# 12 · WPF 뷰어 포함

---

## 개요

임베디드·산업용·업무 앱에서 공통으로 사용할 수 있는  
**레벨별 로그 기록 / 파일 롤링 / 비동기 큐 처리** 기능을  
단일 싱글톤(`LogManager`)으로 제공하는 공용 라이브러리입니다.

```
로그 생성 (AddLog)
  → Channel<T> 비동기 큐
    → 파일 저장 (TXT / CSV / Both)
    → VS 출력창 출력
    → LogAdded 이벤트 → UI 컨트롤(LogViewerControl)
```

---

## 솔루션 구성

```
lssLib.Log.sln
│
├── lssLib.Log                    ── 로그 클래스 라이브러리 (namespace: lssLib.Log)
│   ├── LogLevel.cs                  로그 심각도 열거형
│   ├── LogData.cs                   로그 데이터 모델
│   ├── LogFileFormat.cs             파일 출력 형식 열거형
│   ├── LogConfig.cs                 설정 클래스
│   └── LogManager.cs                싱글톤 매니저 (핵심)
│
└── log                           ── WPF 데모 프로젝트 (namespace: log)
    ├── LogViewerControl.xaml        실시간 로그 뷰어 UserControl
    ├── LogViewerControl.xaml.cs     코드비하인드 (필터·내보내기·표시건수)
    ├── MainWindow.xaml              메인 윈도우
    └── MainWindow.xaml.cs           LogManager 초기화 + 테스트 버튼
```

---

## 개발 환경

| 항목 | 버전 |
|---|---|
| .NET | 8.0-windows |
| C# | 12 (latest) |
| WPF | .NET 8.0-windows (UseWPF=true) |
| Nullable | enable |
| ImplicitUsings | enable |
| 외부 패키지 | 없음 (BCL만 사용) |

---

## 빠른 시작

```csharp
// 1. LogConfig 구성
var config = new LogConfig
{
    LogRootPath     = @"C:\MyApp\Log",
    ValidDays       = 7,
    FileFormat      = LogFileFormat.Txt,
    MinimumLevel    = LogLevel.Debug,
    MaxDisplayCount = 1000
};

// 2. LogManager 시작
LogManager.Instance.Start(config);

// 3. 로그 추가
LogManager.Instance.Info("Network", "서버 연결 성공");
LogManager.Instance.Error("Database", "쿼리 실패: " + ex.Message);

// 4. 앱 종료 시
await LogManager.Instance.StopAsync();
```

---

## 파일 구조별 설명

---

### LogLevel.cs — 로그 심각도 열거형

```csharp
namespace lssLib.Log

public enum LogLevel
{
    Debug = 0,   // 상세 디버그. 개발 중에만 사용 권장
    Info  = 1,   // 일반 정보. 운영 환경 기본 레벨
    Warn  = 2,   // 주의. 즉각 조치 불필요하지만 모니터링 필요
    Error = 3,   // 오류. 기능 일부 정상 동작 안 함
    Fatal = 4    // 치명. 서비스 중단 수준
}
```

**레벨 선택 가이드**

| 레벨 | 사용 시점 | 예시 |
|---|---|---|
| Debug | 변수 값, 함수 진입/종료 추적 | `"소켓 버퍼 크기: 4096"` |
| Info | 주요 비즈니스 이벤트 | `"사용자 로그인 성공"` |
| Warn | 성능 저하, 재시도 발생 | `"응답 지연 800ms"` |
| Error | 예외 발생, 기능 실패 | `"DB 쿼리 실패: timeout"` |
| Fatal | 프로세스 종료 필요 수준 | `"메모리 부족으로 서비스 중단"` |

---

### LogData.cs — 로그 데이터 모델

```csharp
public class LogData
```

**프로퍼티**

| 프로퍼티 | 타입 | 설명 | 예시 |
|---|---|---|---|
| `Level` | `LogLevel` | 심각도 레벨 | `LogLevel.Info` |
| `LevelText` | `string` | 레벨 문자열 (읽기 전용) | `"INFO"` |
| `Date` | `string` | 날짜+시간 조합 (읽기 전용) | `"2025_03_22 14:30:25.123"` |
| `YearMonth` | `string` | 년월 (폴더 분류 기준) | `"2025_03"` |
| `Day` | `string` | 일 (폴더 분류 기준) | `"22"` |
| `Time` | `string` | 시간 (ms 포함) | `"14:30:25.123"` |
| `Source` | `string` | 발생 출처, 파일명 분류 기준 | `"Network"` |
| `Contents` | `string` | 실제 로그 메시지 | `"연결 성공"` |

**생성자**

```csharp
// 현재 시각 자동 입력 - 일반적으로 이 생성자만 사용
public LogData(LogLevel level, string source, string contents)

// 예시
var data = new LogData(LogLevel.Info, "Network", "연결 성공");
// data.Date    → "2025_03_22 14:30:25.123"
// data.Source  → "Network"
// data.ToString() → "2025_03_22 14:30:25.123  [INFO ]  Network           연결 성공"
```

---

### LogFileFormat.cs — 파일 출력 형식

```csharp
public enum LogFileFormat
{
    Txt  = 0,   // .txt 고정폭 텍스트 (기본값, 사람이 읽기 좋음)
    Csv  = 1,   // .csv (Excel·DB Import 분석 용도)
    Both = 2    // .txt + .csv 동시 저장
}
```

**TXT 출력 예시**

```
2025_03_22 14:30:25.123  [INFO ]  Network           서버 연결 성공
2025_03_22 14:30:26.456  [WARN ]  Database          응답 지연 (800ms)
2025_03_22 14:30:27.789  [ERROR]  Auth              인증 토큰 만료
```

**CSV 출력 예시**

```
날짜,시간(HH:mm:ss.fff),레벨,출처,내용
"2025_03_22","14:30:25.123","INFO","Network","서버 연결 성공"
"2025_03_22","14:30:26.456","WARN","Database","응답 지연 (800ms)"
```

> CSV는 신규 파일 생성 시 헤더를 자동으로 한 번만 기록합니다.  
> 내용 안의 큰따옴표는 `""` 로 이스케이프 처리됩니다.

---

### LogConfig.cs — 설정 클래스

```csharp
public class LogConfig
```

**전체 설정 항목**

| 프로퍼티 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `LogRootPath` | `string` | `실행파일경로\Log` | 로그 파일 저장 루트 경로 |
| `ValidDays` | `int` | `7` | 보관 일수 (1~1095일, 초과 시 예외) |
| `CheckHour` | `int` | `0` | 만료 로그 삭제 기준 시각 (0~23시) |
| `MaxFileSizeBytes` | `long` | `10MB` | 파일 롤링 기준 크기 |
| `FileFormat` | `LogFileFormat` | `Txt` | 파일 출력 형식 |
| `EnableFileOutput` | `bool` | `true` | 파일 저장 여부 |
| `EnableConsoleOutput` | `bool` | `true` | VS 출력창 출력 여부 |
| `MinimumConsoleLevel` | `LogLevel` | `Debug` | 출력창 최소 레벨 |
| `MinimumLevel` | `LogLevel` | `Debug` | 큐 진입 최소 레벨 |
| `ChannelCapacity` | `int` | `0` | 비동기 큐 최대 용량 (0=무제한) |
| `MaxDisplayCount` | `int` | `1000` | UI 화면 최대 표시 건수 |

**ValidDays 유효성 검사**

```csharp
// 1~1095 범위를 벗어나면 ArgumentOutOfRangeException 발생
try
{
    var config = new LogConfig { ValidDays = 2000 };  // 예외 발생
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine(ex.Message);
    // "보관 일수는 1일 이상 1095일(3년) 이하여야 합니다. (입력값: 2000)"
}
```

**환경별 권장 설정**

```csharp
// 개발 환경
var devConfig = new LogConfig
{
    MinimumLevel        = LogLevel.Debug,   // 모든 레벨 기록
    MinimumConsoleLevel = LogLevel.Debug,   // 출력창에도 전부 표시
    EnableFileOutput    = true,
    ValidDays           = 3
};

// 운영 환경
var prodConfig = new LogConfig
{
    MinimumLevel        = LogLevel.Info,    // Debug 완전 차단 (성능 향상)
    MinimumConsoleLevel = LogLevel.Warn,    // 출력창은 Warn 이상만
    EnableFileOutput    = true,
    FileFormat          = LogFileFormat.Both,
    ValidDays           = 30,
    MaxFileSizeBytes    = 50L * 1024 * 1024   // 50MB 롤링
};
```

---

### LogManager.cs — 싱글톤 매니저

```csharp
public sealed class LogManager
```

#### 싱글톤 접근

```csharp
// Lazy<T> 기반 thread-safe 싱글톤
// 처음 Instance 에 접근하는 순간 단 한 번 생성
LogManager.Instance.Start(config);
```

#### Start / Stop

```csharp
/// <summary>LogManager 시작. 내부에서 두 개의 Task 를 구동한다.</summary>
/// ProcessQueueAsync  : Channel 큐에서 데이터를 꺼내 파일·콘솔·UI 처리
/// ValidDayManagerAsync: 1시간마다 만료 로그 폴더 검사 및 삭제
public void Start(LogConfig config = null)

// 예시
LogManager.Instance.Start(new LogConfig { ValidDays = 14 });
```

```csharp
/// <summary>비동기 정지. 큐에 남은 항목을 모두 처리한 후 종료.</summary>
/// 동작 순서:
///   1. Channel.Writer.Complete() → 큐 마감 (새 항목 추가 불가)
///   2. ProcessQueueAsync 가 남은 항목 소비 후 루프 종료
///   3. _cts.Cancel() → ValidDayManagerAsync 즉시 종료
///   4. Task.WhenAll() 로 두 Task 모두 종료 대기
/// ※ await 없이 종료 시 큐 잔여 로그 유실 가능
public async Task StopAsync()

// WPF 종료 시 사용 예시
private async void Window_Closing(object sender, CancelEventArgs e)
{
    await LogManager.Instance.StopAsync();
}
```

```csharp
/// <summary>동기 정지. 앱 종료 시 async 불가 상황에서 사용.</summary>
public void Stop()
```

#### 로그 추가

```csharp
/// <summary>로그 추가 (현재 시각 자동 입력)</summary>
/// level < MinimumLevel 인 경우 즉시 반환 (채널에 넣지 않음)
public void AddLog(LogLevel level, string source, string contents)

// 레벨별 단축 메서드
public void Debug(string source, string msg)
public void Info (string source, string msg)
public void Warn (string source, string msg)
public void Error(string source, string msg)
public void Fatal(string source, string msg)

// 사용 예시
LogManager.Instance.Debug("Scheduler", "다음 실행: 14:30:00");
LogManager.Instance.Info ("Network",   "연결 성공 (latency=12ms)");
LogManager.Instance.Warn ("Cache",     "캐시 히트율 저하: 61%");
LogManager.Instance.Error("Database",  "쿼리 실패: " + ex.Message);
LogManager.Instance.Fatal("MainApp",   "치명적 오류 - 프로세스 종료");
```

#### LogAdded 이벤트

```csharp
/// <summary>
/// 로그 항목이 처리될 때 발생.
/// ※ 호출 스레드: 백그라운드 Task → UI 접근 시 Dispatcher 필요
/// </summary>
public event Action<LogData> LogAdded;

// ① 람다로 구독
LogManager.Instance.LogAdded += data =>
{
    Dispatcher.InvokeAsync(() =>
        MyListBox.Items.Insert(0, $"[{data.LevelText}] {data.Contents}")
    );
};

// ② 메서드로 구독
LogManager.Instance.LogAdded += OnLogAdded;

private void OnLogAdded(LogData data)
{
    Dispatcher.InvokeAsync(() => TxtLastLog.Text = data.ToString());
}

// ③ 구독 해제 (Unloaded / Dispose 시점에 반드시 호출)
LogManager.Instance.LogAdded -= OnLogAdded;

// ④ Error 이상만 별도 알림 처리
LogManager.Instance.LogAdded += data =>
{
    if (data.Level >= LogLevel.Error)
        Dispatcher.InvokeAsync(() => ShowAlertDialog(data.Contents));
};
```

#### AOP 공통 래퍼

내부 구조: `public 래퍼 4개` → `private 코어 2개(ExecuteCore / ExecuteCoreAsync)`  
`try/catch/finally + 로그 패턴`은 코어에만 존재하며, `[CallerMemberName]`으로 호출 함수명이 자동 주입됩니다.

```csharp
// ① 반환값 없음 · 동기
public static void Execute(
    Action tryAction,
    Action catchAction    = null,
    Action finallyAction  = null,
    LogLevel logLevel     = LogLevel.Info,
    string   category     = "SYSTEM",
    [CallerMemberName] string source = "")

// 사용 예시
LogManager.Execute(
    tryAction:    () => Socket.Connect(),
    catchAction:  () => Reconnect(),
    finallyAction: () => UpdateStatusUI(),
    category: "Network"
);
// 출력: [Network] Start → [Network] End (or Error)
// source 는 [CallerMemberName] 으로 호출 함수명 자동 주입

// ② 반환값 없음 · 비동기
public static async Task ExecuteAsync(
    Func<Task> tryAction,
    Action catchAction    = null,
    Action finallyAction  = null,
    LogLevel logLevel     = LogLevel.Info,
    string   category     = "SYSTEM",
    [CallerMemberName] string source = "")

await LogManager.ExecuteAsync(
    tryAction: async () => await DB.SaveAsync(data),
    catchAction: () => ShowError("저장 실패"),
    category: "Database"
);

// ③ 반환값 있음 · 동기 (성공: tryFunc 결과, 실패: catchFunc 결과)
public static T Execute<T>(
    Func<T> tryFunc,
    Func<T> catchFunc     = null,
    Action  finallyAction = null,
    LogLevel logLevel     = LogLevel.Info,
    string   category     = "SYSTEM",
    [CallerMemberName] string source = "")

bool isConnected = LogManager.Execute(
    tryFunc:   () => Socket.TryConnect(),
    catchFunc: () => false,             // 예외 시 false 반환
    category:  "Network"
);

// ④ 반환값 있음 · 비동기
public static async Task<T> ExecuteAsync<T>(
    Func<Task<T>> tryFunc,
    Func<T> catchFunc     = null,
    Action  finallyAction = null,
    LogLevel logLevel     = LogLevel.Info,
    string   category     = "SYSTEM",
    [CallerMemberName] string source = "")

List<User> users = await LogManager.ExecuteAsync(
    tryFunc:   async () => await DB.GetUsersAsync(),
    catchFunc: () => new List<User>(),  // 예외 시 빈 목록 반환
    category:  "Database"
);
```

---

## 파일 저장 구조

```
LogRootPath\
└── yyyy_MM\          ← 년월 폴더 (예: 2025_03)
    └── dd\           ← 일 폴더   (예: 22)
        ├── All.txt         전체 로그 (모든 Source 포함)
        ├── All_2.txt       롤링 (MaxFileSizeBytes 초과 시 자동 생성)
        ├── Network.txt     Source="Network" 로그만 분류
        ├── Database.txt    Source="Database" 로그만 분류
        └── ...
```

**롤링 규칙**: `All.txt` → `All_2.txt` → `All_3.txt` → ...  
파일이 `MaxFileSizeBytes` 를 초과하는 순간 다음 번호 파일로 자동 전환됩니다.

**만료 삭제**: `ValidDayManagerAsync` 가 1시간마다 검사하여  
`CheckHour` 에 해당하는 시각(하루 1회)에 `ValidDays` 이상 지난 일 폴더를 삭제합니다.  
빈 년월 폴더도 함께 정리됩니다.

---

## LogViewerControl — WPF 뷰어 UserControl

### XAML 배치

```xml
<!-- MainWindow.xaml -->
<Window ...
        xmlns:local="clr-namespace:log">
    <Grid>
        <local:LogViewerControl/>
    </Grid>
</Window>
```

### 기능

| 기능 | 설명 |
|---|---|
| 레벨 필터 | ComboBox로 특정 레벨만 표시 (ALL / DEBUG / INFO / WARN / ERROR / FATAL) |
| 출처 필터 | Source 텍스트 포함 검색 (대소문자 무시) |
| 내용 검색 | Source + Contents 동시 검색 |
| 자동 스크롤 | 최신 항목(맨 위)으로 자동 이동 (체크박스로 토글) |
| 지우기 | 화면 목록 전체 삭제 (파일 삭제 아님) |
| 내보내기 | 현재 필터 결과를 TXT 또는 CSV 로 저장 (SaveFileDialog) |
| 최대 건수 | `MaxDisplayCount` 초과 시 오래된 항목 자동 제거 |

### 런타임 최대 건수 변경

```csharp
// LogViewerControl 인스턴스를 x:Name 으로 참조하거나
// 코드비하인드에서 직접 호출
myLogViewer.SetMaxDisplayCount(500);
```

### 초기화 순서 (중요)

```csharp
public LogViewerControl()
{
    // ① ICollectionView 먼저 초기화 (null 방지)
    _logView        = CollectionViewSource.GetDefaultView(_allLogs);
    _logView.Filter = ApplyFilter;

    // ② XAML 파싱 (ComboBox 이벤트 발화 가능)
    InitializeComponent();

    // ③ ListView 에 뷰 연결
    LvLog.ItemsSource = _logView;
}
```

> `InitializeComponent()` 보다 먼저 `_logView` 를 초기화하지 않으면  
> ComboBox `IsSelected="True"` 파싱 시 `NullReferenceException` 이 발생합니다.

---

## 실제 운영 패턴

### 패턴 1 — 앱 전역 싱글톤 설정

```csharp
// App.xaml.cs 또는 MainWindow 생성자
public App()
{
    var config = new LogConfig
    {
        LogRootPath         = System.IO.Path.Combine(
                                  AppDomain.CurrentDomain.BaseDirectory, "Log"),
        ValidDays           = 30,
        FileFormat          = LogFileFormat.Both,
        MinimumLevel        = LogLevel.Info,          // 운영: Debug 차단
        MinimumConsoleLevel = LogLevel.Warn,          // 출력창: Warn 이상만
        MaxFileSizeBytes    = 20L * 1024 * 1024,      // 20MB 롤링
        ChannelCapacity     = 0,                      // 무제한 큐
        MaxDisplayCount     = 2000
    };
    LogManager.Instance.Start(config);
}
```

### 패턴 2 — 모듈별 Source 분류

```csharp
// 각 모듈에서 고정 Source 명칭 사용
// → Log\2025_03\22\Network.txt 로 자동 분류
private const string LOG_SOURCE = "Network";

public void Connect()
{
    LogManager.Instance.Info(LOG_SOURCE, $"연결 시도: {_host}");
    try
    {
        _socket.Connect(_host, _port);
        LogManager.Instance.Info(LOG_SOURCE, "연결 성공");
    }
    catch (Exception ex)
    {
        LogManager.Instance.Error(LOG_SOURCE, $"연결 실패: {ex.Message}");
    }
}
```

### 패턴 3 — AOP 래퍼로 일관된 로그 패턴

```csharp
// 비즈니스 로직과 로그 코드를 분리
// source 는 [CallerMemberName] 이 "SaveOrder" 자동 주입
public bool SaveOrder(Order order)
{
    return LogManager.Execute(
        tryFunc:   () =>
        {
            _db.Insert(order);
            return true;
        },
        catchFunc: () => false,
        category:  "OrderService"
    );
    // 로그 출력:
    // [INFO ] SaveOrder [OrderService] Start
    // [INFO ] SaveOrder [OrderService] End
    // 예외 시:
    // [ERROR] SaveOrder [OrderService] Error - ...
}
```

### 패턴 4 — 앱 종료 시 안전 처리

```csharp
private async void Window_Closing(object sender, CancelEventArgs e)
{
    // e.Cancel = true 로 창 닫기를 일시 중지하고
    // 큐 소진 후 재종료하는 패턴 (선택적)
    e.Cancel = true;

    LogManager.Instance.Info("App", "종료 처리 중...");
    await LogManager.Instance.StopAsync();   // 큐 잔여 로그 모두 파일에 저장

    Application.Current.Shutdown();
}
```

---

## 다른 프로젝트에서 참조하는 방법

### 1. 프로젝트 참조 추가

Visual Studio → 솔루션 탐색기 → 참조할 프로젝트 우클릭  
→ 프로젝트 참조 추가 → `lssLib.Log` 선택

### 2. using 추가

```csharp
using lssLib.Log;
```

### 3. WPF 프로젝트에서 LogViewerControl 사용 시

```xml
<!-- xmlns 에 어셈블리 명시 -->
xmlns:log="clr-namespace:log;assembly=log"

<log:LogViewerControl/>
```

---

## 주의 사항

| 항목 | 내용 |
|---|---|
| `StopAsync()` 필수 | 종료 시 미호출 시 큐 잔여 로그 유실 가능 |
| `LogAdded` 구독 해제 | `UserControl.Unloaded` 또는 `Dispose` 에서 반드시 해제 |
| `Dispatcher.InvokeAsync` | `LogAdded` 핸들러 안에서 UI 접근 시 반드시 필요 |
| `ValidDays` 범위 | 1~1095일. 초과 시 `ArgumentOutOfRangeException` |
| `ChannelCapacity` | 0=무제한(안전), 양수=오래된 로그 유실 가능 |
| `MinimumLevel` | 이 레벨 미만은 채널에 진입 불가 (파일·화면 어디에도 안 남음) |

---

## 추후 확장 예정

- `Helpers` 영역의 `EnsureDirectory` → `lssLib.Utils.FileUtility` 이관
- `Helpers` 영역의 `SanitizeFileName` → `lssLib.Utils.StringUtility` 이관
- `lssLib.Protocols` 모듈과 연동한 프로토콜 로그 분류

---

*lssLib.Log · .NET 8.0 · WPF · BCL only*
