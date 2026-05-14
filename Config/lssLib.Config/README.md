# lssLib.Config

> **lssLib 생태계 설정 관리 모듈** · `.NET 8.0` · `C# 12` · `WPF 데모 포함`

---

## 개요

임베디드·산업용·업무 앱에서 공통으로 필요한  
**INI / JSON / XML 설정 파일 읽기·쓰기**, **AES-256-GCM 암호화**,  
**런타임 변경 감지(FileWatcher)**, **스키마 검증**, **트랜잭션(Undo/Redo)**,  
**버전 마이그레이션**, **환경별 프로파일**, **장비 트리 관리**를  
단일 모듈로 제공합니다.

```
설정 파일 (INI / JSON / XML)
       │
       ▼
  ConfigManager  ─── AES-256-GCM 암호화 (ConfigEncryptor)
       │          ─── FileWatcher 변경 감지 (ConfigFileWatcher)
       │          ─── 트랜잭션 / Undo / Redo (ConfigTransaction)
       │          ─── 스키마 검증 (ConfigValidator)
       ▼
  ConfigStore  (ConcurrentDictionary 인메모리 저장소)
       │
       ├── Migration   (버전 간 자동 변환)
       ├── Profile     (Base → Env → Local 오버라이드 계층)
       └── Tree        (그룹 → 장비 → 센서 / 태그 계층 트리)
```

---

## 솔루션 구조

```
lssLib.Config.sln
│
├── lssLib.Config/                          핵심 클래스 라이브러리
│   ├── lssLib.Config.csproj               net8.0-windows · System.Text.Json 8.0.0
│   │
│   ├── ConfigFormat.cs                    열거형: Ini / Json / Xml
│   ├── ConfigEntry.cs                     record: Section + Key + Value + IsEncrypted
│   ├── ConfigStore.cs                     ConcurrentDictionary 인메모리 저장소
│   └── ConfigManager.cs                   Lazy<T> 싱글톤 통합 진입점 (v2)
│
│   ├── Encryption/
│   │   └── ConfigEncryptor.cs             AES-256-GCM + PBKDF2-SHA256 (100,000회)
│   │                                      ENC: 접두사 저장 포맷
│   │
│   ├── Watcher/
│   │   └── ConfigFileWatcher.cs           FileSystemWatcher 래퍼
│   │                                      디바운스 300ms · 다중 파일 지원
│   │
│   ├── Tree/
│   │   ├── NodeType.cs                    열거형: Root / Group / Device / Sensor / Tag
│   │   ├── ConfigNode.cs                  트리 노드 (Properties · DFS · DeepClone)
│   │   └── ConfigTree.cs                  CRUD + JSON / XML 저장·로드
│   │
│   ├── Validation/
│   │   ├── ConfigValueType.cs             열거형: 14종 값 유형
│   │   ├── ConfigFieldRule.cs             단일 필드 검증 규칙 값 객체
│   │   ├── ConfigSchema.cs                체이닝 빌더 (Require / Optional / Custom)
│   │   └── ConfigValidator.cs             검증 실행 + ValidationResult + Exception
│   │
│   ├── Transaction/
│   │   └── ConfigTransaction.cs           Commit / Rollback
│   │                                      UndoRedoStack (최대 50단계)
│   │
│   ├── Migration/
│   │   ├── MigrationRule.cs               Rename / Move / Delete / Add / Transform
│   │   └── ConfigMigration.cs             BFS 경로 탐색 + 순차 적용 + MigrationReport
│   │
│   └── Profile/
│       └── ConfigProfile.cs               ConfigProfileManager
│                                          (Base → Env → Local 3단계 오버라이드)
│
└── lssLib.Config.Demo/                     WPF 데모 애플리케이션
    ├── lssLib.Config.Demo.csproj           net8.0-windows · lssLib.Config 참조
    ├── App.xaml / App.xaml.cs             공통 스타일 (버튼·TextBox·GroupBox)
    ├── MainWindow.xaml / .cs              7탭 컨테이너 + 하단 상태바
    │
    └── Views/
        ├── BasicRwView                    탭① INI / JSON / XML 읽기·쓰기
        ├── EncryptionView                 탭② AES-256-GCM 암호화 설정값
        ├── WatcherView                    탭③ FileWatcher 런타임 변경 감지
        ├── TreeView                       탭④ 파일 선택 → 트리 렌더링 + 미리보기
        ├── ValidationView                 탭⑤ ConfigSchema 스키마 검증
        ├── TransactionView                탭⑥ Commit / Rollback / Undo / Redo
        └── MigrationView                  탭⑦ 버전 마이그레이션 + 프로파일 전환
```

---

## 개발 환경

| 항목 | 값 |
|---|---|
| .NET | 8.0-windows |
| C# | 12 (latest) |
| WPF | .NET 8.0-windows (`UseWPF=true`) |
| Nullable | enable |
| ImplicitUsings | enable |
| 외부 패키지 | `System.Text.Json 8.0.0` |

---

## 빠른 시작

```csharp
using lssLib.Config;
using lssLib.Config.Validation;

// ── 1. 암호화 키 설정 (선택)
ConfigManager.Instance.SetPassword("my-secret-pass");

// ── 2. 파일 로드 (다중 병합 — 뒤가 앞을 덮어씀)
ConfigManager.Instance.Load("config/base.json");
ConfigManager.Instance.Load("config/production.json");

// ── 3. 값 읽기
string host = ConfigManager.Instance.Get("Network", "Host") ?? "localhost";
int    port = ConfigManager.Instance.GetInt("Network", "Port", 502);
bool   dbg  = ConfigManager.Instance.GetBool("App", "Debug", false);

// ── 4. 스키마 검증
var schema = new ConfigSchema()
    .Require("Network", "Host",  ConfigValueType.IpAddress)
    .Require("Network", "Port",  ConfigValueType.Port)
    .Optional("App",    "Debug", ConfigValueType.Bool, defaultValue: "false");

ConfigManager.Instance.Validate(schema).ThrowIfInvalid();

// ── 5. 트랜잭션으로 값 쓰기
using var tx = ConfigManager.Instance.BeginTransaction();
tx.Set("Network", "Host", "10.0.0.1");
tx.Set("Network", "Port", "1502");
tx.Commit();

// ── 6. Undo / Redo
ConfigManager.Instance.Undo();
ConfigManager.Instance.Redo();

// ── 7. 파일 저장
ConfigManager.Instance.Save();

// ── 8. 런타임 변경 감지
ConfigManager.Instance.StartWatch();
ConfigManager.Instance.ConfigChanged += (path, store) =>
    Dispatcher.InvokeAsync(() => ReloadUI(store));
```

---

## 모듈별 API

### ConfigManager (싱글톤)

```csharp
// 싱글톤 접근
ConfigManager.Instance

// 독립 인스턴스 (테스트·데모)
var cfg = ConfigManager.CreateNew();

// 파일 R/W
void  Load    (string path, ConfigFormat? fmt = null, bool optional = false)
void  Save    (string? path = null, ConfigFormat? fmt = null)
Task  LoadAsync(...)
Task  SaveAsync(...)

// 값 읽기
string? Get       (string section, string key)
string  GetOr     (string section, string key, string fallback)
int     GetInt    (string section, string key, int fallback = 0)
double  GetDouble (string section, string key, double fallback = 0.0)
bool    GetBool   (string section, string key, bool fallback = false)

// 값 쓰기
void Set     (string section, string key, string value, bool isEncrypted = false)
bool Remove  (string section, string key)
void Clear   ()

// 트랜잭션
ConfigTransaction            BeginTransaction ()
IReadOnlyList<ChangeRecord>? Undo             ()
IReadOnlyList<ChangeRecord>? Redo             ()
bool CanUndo  { get; }
bool CanRedo  { get; }
int  UndoDepth{ get; }

// 검증
ValidationResult Validate       (ConfigSchema schema, bool applyDefaults = true)
void             ValidateOrThrow(ConfigSchema schema)

// FileWatcher
void StartWatch  (string? filePath = null)
void StopWatch   ()
event Action<string, ConfigStore>? ConfigChanged
event Action<IReadOnlyList<ChangeRecord>>? TransactionCommitted
```

---

### Encryption — AES-256-GCM

```csharp
using lssLib.Config.Encryption;

// 키 설정
ConfigEncryptor.SetPassword("my-password");     // PBKDF2-SHA256 (100,000회)
ConfigEncryptor.SetKey(byte[] key32);           // 원시 32바이트 키

// 암호화 / 복호화
string cipher = ConfigEncryptor.Encrypt("secret");
string plain  = ConfigEncryptor.Decrypt(cipher);

// 파일 저장 포맷 (ENC: 접두사)
string stored   = ConfigEncryptor.ToStoredValue("password");   // "ENC:Base64..."
string restored = ConfigEncryptor.FromStoredValue(stored);

// 암호화 설정 저장 예시
ConfigManager.Instance.Set("DB", "Password", "secret", isEncrypted: true);
ConfigManager.Instance.Save("app.json");
// 파일: { "DB": { "Password": { "value": "ENC:...", "encrypted": true } } }
```

암호문 포맷: `Base64( salt[16] + nonce[12] + tag[16] + ciphertext )`  
동일 평문을 두 번 암호화하면 다른 암호문이 생성됩니다 (랜덤 salt).

---

### Validation — 스키마 검증

```csharp
using lssLib.Config.Validation;

// 스키마 정의
var schema = new ConfigSchema()
    // 필수 필드
    .Require("Network", "Host",     ConfigValueType.IpAddress)
    .Require("Network", "Port",     ConfigValueType.Port)
    .Require("Network", "Timeout",  ConfigValueType.Int,    range: (100, 30_000))
    .Require("Network", "Protocol", ConfigValueType.Enum,
        allowedValues: new[]{ "Modbus", "EtherNet/IP", "OPC-UA" })
    // 선택 필드 (없으면 기본값 자동 적용)
    .Optional("App", "Debug",    ConfigValueType.Bool,   defaultValue: "false")
    .Optional("App", "LogLevel", ConfigValueType.Enum,
        defaultValue: "Info",
        allowedValues: new[]{ "Debug","Info","Warn","Error","Fatal" })
    // 커스텀 검증
    .Custom("App", "ApiKey", v => v.Length >= 16 ? null : "ApiKey 최소 16자");

// 검증 실행
ValidationResult result = ConfigManager.Instance.Validate(schema);

if (!result.IsValid)
    foreach (var err in result.Errors)
        Console.WriteLine(err);  // [Network] Port = "99999" — 최댓값(65535)보다 큽니다

// 실패 시 예외
ConfigManager.Instance.ValidateOrThrow(schema);  // ConfigValidationException
```

**지원 값 유형 (ConfigValueType)**

| 유형 | 설명 |
|---|---|
| `String` | 임의 문자열 |
| `NonEmptyString` | 비어있지 않은 문자열 |
| `Int` / `Long` | 정수 (범위 검증 가능) |
| `Double` | 부동소수점 (범위 검증 가능) |
| `Bool` | true/false/1/0/yes/no/on/off |
| `IpAddress` | IPv4 주소 형식 |
| `Port` | 1 ~ 65535 |
| `SemVer` | 시맨틱 버전 (예: 1.2.3) |
| `DirectoryPath` / `FilePath` | 경로 형식 |
| `Regex` | 정규식 패턴 매칭 |
| `Enum` | 허용 값 목록 중 하나 |
| `Guid` | GUID 형식 |
| `Cron` | Cron 표현식 |

---

### Transaction — 트랜잭션 / Undo / Redo

```csharp
// 트랜잭션 (using 블록 이탈 시 미커밋이면 자동 Rollback)
using var tx = ConfigManager.Instance.BeginTransaction();
tx.Set   ("Network", "Host", "10.0.0.1");
tx.Set   ("Network", "Port", "1502");
tx.Remove("Legacy",  "OldKey");
tx.Commit();   // 모든 변경을 한 번에 적용 + TransactionCommitted 이벤트 발생

// Rollback — 커밋 없이 변경 취소
tx.Rollback();

// Undo / Redo (최대 50단계)
var undone  = ConfigManager.Instance.Undo();   // 마지막 커밋 되돌리기
var redone  = ConfigManager.Instance.Redo();   // 다시 적용
bool canU   = ConfigManager.Instance.CanUndo;
bool canR   = ConfigManager.Instance.CanRedo;
int  depth  = ConfigManager.Instance.UndoDepth;

// 트랜잭션 커밋 이벤트
ConfigManager.Instance.TransactionCommitted += changes =>
{
    foreach (var ch in changes)
        Console.WriteLine($"[{ch.Section}] {ch.Key}  {ch.OldValue} → {ch.NewValue}");
};
```

---

### Migration — 버전 마이그레이션

```csharp
using lssLib.Config.Migration;

// 규칙 등록
ConfigMigration.Register("1.0", "2.0", rules =>
{
    rules.Rename("Network", "ServerIP",  "Host");      // 키 이름 변경
    rules.Move  ("DB",      "Password",  "Credentials", "DbPassword", isEncrypted: true);
    rules.Delete("Legacy",  "OldFlag");                // 키 삭제
    rules.Add   ("App",     "LogLevel",  "Info");      // 키 추가 (없으면만)
    rules.Transform("Network", "Port", v =>            // 값 변환
        (int.Parse(v) + 100).ToString());
});

ConfigMigration.Register("2.0", "3.0", rules =>
{
    rules.Add("Monitor", "Enabled",  "true");
    rules.Add("Monitor", "Interval", "5000");
});

// 마이그레이션 실행 (BFS 경로 자동 탐색)
MigrationReport report = ConfigMigration.Migrate(
    store,
    currentVersion: "1.0",
    targetVersion:  "3.0");   // 1.0 → 2.0 → 3.0 순서로 자동 적용

Console.WriteLine(report);
// 마이그레이션 완료: 1.0 → 3.0  (2단계 / 8개 규칙 적용)

// 자동 감지 (Meta.Version 키 참조)
ConfigMigration.MigrateAuto(store, targetVersion: "3.0");
```

---

### Profile — 환경별 프로파일

```csharp
using lssLib.Config.Profile;

// 프로파일 정의 (Base → Env → Local 3단계 오버라이드)
var profiles = new ConfigProfileManager();

profiles.Define("development",
    baseFile:  "config/base.json",
    envFile:   "config/development.json",
    localFile: "config/local.json",       // optional — 없으면 무시
    description: "개발 환경 — 상세 로그, 로컬 DB");

profiles.Define("production",
    baseFile:  "config/base.json",
    envFile:   "config/production.json",
    description: "운영 환경 — 최소 로그, 운영 DB");

// 프로파일 활성화
ConfigStore store = profiles.Activate("development");
ConfigManager.Instance.Store.Merge(store);

// 환경 변수에서 자동 활성화 (APP_ENV=production)
ConfigStore store2 = profiles.ActivateFromEnv("APP_ENV", fallback: "development");

// 전환 이벤트
profiles.ProfileSwitched += (name, store) =>
    Console.WriteLine($"프로파일 전환: {name}  항목={store.Count}개");
```

---

### Tree — 장비 트리

```csharp
using lssLib.Config.Tree;

var tree = new ConfigTree();

// 노드 추가 (계층: Root → Group → Device → Sensor / Tag)
var line1 = tree.AddGroup("Line-1", "Building-A");
var plc   = tree.AddDevice(line1, "PLC-001", ip: "192.168.1.10", port: "502");
var temp  = tree.AddSensor(plc,   "TempSensor",  address: "40001", unit: "°C");
var tag   = tree.AddTag   (plc,   "Run_Coil",    address: "M0.0",  dataType: "Bool");

// 프로퍼티
plc.SetProperty("protocol", "Modbus TCP");
string? ip = plc.GetProperty("ip");

// 탐색
ConfigNode? node    = tree.FindById("xxx");
ConfigNode? byName  = tree.FindByName("PLC-001");
IEnumerable<ConfigNode> devices = tree.FindAll(NodeType.Device);
IEnumerable<ConfigNode> all     = tree.Flatten();

// 이동 / 제거
tree.Move(temp, line1);     // 다른 부모로 이동
tree.Remove(tag);

// JSON / XML 저장·로드
tree.SaveJson("devices.json");
tree.LoadJson("devices.json");
tree.SaveXml ("devices.xml");
tree.LoadXml ("devices.xml");

// 인메모리 직렬화 (파일 저장 없이)
string json = tree.ToJson();
string xml  = tree.ToXml();
tree.FromJson(json);

// 변경 이벤트
tree.NodeChanged += (node, action) =>
    Console.WriteLine($"[{action}] {node.Type}: {node.Name}");
```

**NodeType 계층**

```
Root
 └── Group    (공장 / 라인 / 사이트)
      └── Device  (PLC / HMI / 서버 / 컨트롤러)
           ├── Sensor  (온도 / 압력 / 유량 등 아날로그·디지털)
           └── Tag     (PLC 메모리 주소 / OPC-UA NodeId)
```

---

## WPF 데모 탭 구성

| 탭 | 주요 시나리오 |
|---|---|
| 📄 **기본 R/W** | INI / JSON / XML 저장·로드, GetInt / GetBool / GetDouble 타입 변환 |
| 🔐 **암호화** | 패스워드 설정 → 암호화 항목 추가 → 파일에서 `ENC:` 접두사 확인 → 복호화 복원 |
| 👁 **변경 감지** | FileWatcher 시작·중단, 1초 간격 10회 자동 증가 테스트 |
| 🌳 **장비 트리** | **파일 선택 다이얼로그 → 트리 렌더링 + 파일 원본 미리보기**<br>최근 파일 목록, JSON 정렬, 클립보드 복사, 앱 시작 자동 로드 |
| ✅ **스키마 검증** | 네트워크 / DB / 앱 설정 프리셋, 오류값 삽입, ThrowIfInvalid, 기본값 자동 적용 |
| 🔄 **트랜잭션** | Begin → Set → Commit, Rollback, Undo / Redo, 3단계 자동 시나리오 |
| 🔀 **마이그레이션·프로파일** | v1.0 → v3.0 자동 변환, Development / Production / Staging 프로파일 전환 |

---

## 파일 저장 포맷 예시

### INI

```ini
# lssLib.Config  generated: 2024-04-01 14:30:00

[Network]
Host = 192.168.1.100
Port = 502
Timeout = 5000

[Credentials]
DbPassword = ENC:Base64encodedCipherText==
```

### JSON

```json
{
  "Network": {
    "Host": "192.168.1.100",
    "Port": "502",
    "Timeout": "5000"
  },
  "Credentials": {
    "DbPassword": {
      "value": "ENC:Base64encodedCipherText==",
      "encrypted": true
    }
  }
}
```

### XML

```xml
<?xml version="1.0" encoding="utf-8"?>
<Config generated="2024-04-01 14:30:00">
  <Section name="Network">
    <Entry key="Host"    value="192.168.1.100" encrypted="false"/>
    <Entry key="Port"    value="502"           encrypted="false"/>
  </Section>
  <Section name="Credentials">
    <Entry key="DbPassword" value="ENC:Base64==" encrypted="true"/>
  </Section>
</Config>
```

---

## lssLib 생태계 내 위치

```
lssLib.Binary     ──► lssLib.Extensions  ──► lssLib.Utils
(BufferParser          (CrcExtensions         (Guard
 BufSchema)             ScaleExtensions         StringExt
                        TextExtensions)         FileExt)
       │                      │                    │
       └──────────────────────┴────────────────────┤
                                                   ▼
                                           lssLib.Retry
                                           (CircuitBreaker
                                            RateLimiter)
                                                   │
                                                   ▼
                                           lssLib.Messaging
                                           (EventBus
                                            CommandQueue
                                            AsyncScheduler)
                                                   │
                                                   ▼
                                           lssLib.Config       ← 이 모듈
                                           (ConfigManager
                                            Validation
                                            Transaction
                                            Migration
                                            Profile
                                            Tree)
                                                   │
                                                   ▼
                                           lssLib.Net
                                           (NetDeviceRegistry
                                            ← ConfigTree 노드
                                              자동 매핑 예정)
```

---

## 설계 원칙

| 원칙 | 내용 |
|---|---|
| **싱글톤** | `Lazy<T>` 기반 thread-safe 싱글톤. `CreateNew()` 로 독립 인스턴스 생성 가능 |
| **BCL + System.Text.Json** | 최소 외부 의존성. JSON 직렬화 1개 패키지만 사용 |
| **암호화** | AES-256-GCM + PBKDF2(100,000회). 동일 평문도 매번 다른 암호문 (랜덤 salt) |
| **FileWatcher** | 300ms 디바운스로 연속 이벤트 병합. 다중 파일 동시 감시 |
| **트랜잭션** | `IDisposable` — `using` 블록 이탈 시 미커밋이면 자동 Rollback |
| **Undo/Redo** | 최대 50단계. 새 커밋 시 Redo 스택 초기화 |
| **검증 분리** | `ConfigSchema` 선언 → `ConfigValidator` 실행 분리. 스키마 재사용 가능 |
| **마이그레이션 BFS** | 버전 경로를 BFS로 자동 탐색. v1.0 → v3.0 도 중간 단계 자동 적용 |
| **프로파일 계층** | Base → Env → Local 순서. 뒤에 오는 파일이 앞을 덮어씀 |
| **트리 직렬화** | `ConfigTree.ToJson()` / `FromJson()` — 파일 저장 없이 인메모리 직렬화 가능 |

---

## 빌드 요구사항

```bash
# .NET 8.0 SDK 이상 필요
dotnet build lssLib.Config.sln --no-restore

# 데모 실행
dotnet run --project lssLib.Config.Demo
```

---

## 주의 사항

| 항목 | 내용 |
|---|---|
| `ConfigEncryptor.SetPassword()` | 파일 저장 전 반드시 호출. 키 없이 저장하면 암호화 항목이 평문으로 저장됨 |
| `FileWatcher` UI 접근 | `ConfigChanged` 이벤트는 백그라운드 스레드 → `Dispatcher.InvokeAsync` 필요 |
| `ConfigMigration` 정적 레지스트리 | 앱당 1회만 등록. 재등록 전 `ConfigMigration.ClearAll()` 호출 |
| `ConfigTransaction` Dispose | `using` 없이 사용 시 반드시 `Commit()` 또는 `Rollback()` 호출 |
| `ChangeRecord` | `public` — `TransactionCommitted` 이벤트 파라미터로 외부 접근 가능 |
| `ConfigTree.NodeChanged` | 배치 작업 시 이벤트 다량 발생 주의. 필요시 구독 해제 후 작업 |

---

*lssLib.Config v2.0 · .NET 8.0 · WPF · BCL + System.Text.Json*
