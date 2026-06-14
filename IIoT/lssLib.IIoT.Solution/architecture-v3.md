# IIoT 통합 구조 설계 (v3.0 — 2026-06-13 확정)

## 1. 통합 근거 — 현재 소스 분석 결과

### 1-1. DeviceManager + ConfigApp 완전 중복
`ConfigAppMainViewModel`이 DeviceTreeViewModel·JsonConfigLoader·JsonWriteService·
ScaleLibraryVM·AlarmLibraryVM·CommLibraryVM 등 DeviceManager 핵심을 그대로 재구현.
→ DeviceManager 삭제, ConfigApp → **IIoT.Studio**로 통합 흡수 가능.

### 1-2. CollectorRuntime + Monitor cross-process EventBus 버그
`MonitorEngine`이 `EventBus.Subscribe<TagValueUpdatedEvent>`를 구독하나,
lssLib EventBus는 in-process 전용 — 다른 프로세스 이벤트 수신 불가.
→ 두 프로그램을 **IIoT.Collector** 하나로 통합 시 동일 프로세스 EventBus로 해결.

### 1-3. ViewModel 생성자 과부하
```csharp
// 현재 ConfigAppMainViewModel — 8개 파라미터
new ConfigAppMainViewModel(
    tree, loader, writer, scale, alarm, comm, canvas, collect)
// → ConfigBundle 번들 패턴으로 2개로 축소
```

### 1-4. AddTransient<MainWindow> 버그 (2곳)
Manager/App.xaml.cs, DeviceManager/App.xaml.cs 모두 `AddTransient<MainWindow>`
→ 매 Resolve마다 새 Window 인스턴스 → 이중 창 버그.
→ 모두 `AddSingleton<MainWindow>`으로 수정 필요.

---

## 2. 최종 통합 구조

```
lssLib.IIoT.Solution/               ← 단일 솔루션
│
├── IIoT.Studio/                     ★ 설정 통합 (구 DeviceManager + ConfigApp)
│   ├── App.xaml.cs
│   ├── MainWindow.xaml
│   ├── ViewModels/
│   │   ├── StudioMainViewModel.cs   (ConfigAppMainViewModel 개명·간소화)
│   │   ├── DeviceTree/
│   │   ├── Canvas/
│   │   └── Library/
│   ├── Core/Config/
│   │   ├── JsonWriteService.cs
│   │   ├── JsonConfigLoader.cs
│   │   ├── ConfigWatcher.cs         (.signal 발행)
│   │   └── CollectConfigService.cs
│   └── IIoT.Studio.csproj
│
├── IIoT.Collector/                  ★ 수집+감지 통합 (구 CollectorRuntime + Monitor)
│   ├── App.xaml.cs
│   ├── MainWindow.xaml
│   ├── Core/
│   │   ├── CollectionEngine.cs      (수집 엔진)
│   │   ├── MonitorEngine.cs         (감지 엔진 — 동일 프로세스 EventBus 구독)
│   │   ├── ConfigReloadWatcher.cs   (.signal FSW 감지)
│   │   └── AlarmStateManager.cs
│   ├── Storage/TagHistoryDb.cs
│   ├── Protocols/                   (IProtocolDriver 구현체)
│   └── IIoT.Collector.csproj
│
├── IIoT.Manager/                    ★ 오케스트레이션 (현행 유지)
│   ├── Core/ProcessManager.cs
│   └── IIoT.Manager.csproj
│
├── UI/
│   ├── Themes/IIoT.UI.Themes/       (테마 라이브러리)
│   └── IIoT.UI.Controls/            (공용 컴포넌트)
│       ├── NumericBox.cs
│       ├── StatusIndicator.cs
│       ├── LabeledField.cs
│       └── TagValueCell.cs
│
├── IIoT.Shared/                     ★ 신규 — 공유 모델·인터페이스
│   ├── Models/
│   │   ├── TagValue.cs
│   │   ├── LiveTagValue.cs
│   │   └── AlarmRecord.cs
│   ├── Contracts/
│   │   ├── IProtocolDriver.cs
│   │   └── ITagHistoryDb.cs
│   └── Config/ConfigBundle.cs
│
└── IIoT.Solution.sln                ← 단일 sln
```

---

## 3. ConfigBundle 상세 설계

```csharp
// IIoT.Shared/Config/ConfigBundle.cs
// ══════════════════════════════════════════════════════════
//  IIoT.Shared · Config/ConfigBundle.cs
//  역할: Studio ViewModel DI 번들 — 서비스 8개를 1개로 묶음
// ══════════════════════════════════════════════════════════
namespace IIoT.Shared.Config;

public sealed class ConfigBundle
{
    // §1 ─ 설정 서비스 ──────────────────────────────────────
    public JsonConfigLoader      Loader  { get; init; } = null!;
    public JsonWriteService      Writer  { get; init; } = null!;
    public CollectConfigService  Collect { get; init; } = null!;

    // §2 ─ 라이브러리 ViewModel ────────────────────────────
    public ScaleLibraryViewModel Scale   { get; init; } = null!;
    public AlarmLibraryViewModel Alarm   { get; init; } = null!;
    public CommLibraryViewModel  Comm    { get; init; } = null!;
    public CanvasViewModel       Canvas  { get; init; } = null!;
}

// IIoT.Studio/App.xaml.cs — DI 등록 예시
private static IServiceProvider _ConfigureServices()
{
    var services = new ServiceCollection();

    // 기반 서비스
    services.AddSingleton<JsonWriteService>(_ => new JsonWriteService(ConfigDir));
    services.AddSingleton<JsonConfigLoader>(_ => new JsonConfigLoader(ConfigDir));
    services.AddSingleton<CollectConfigService>(_ => new CollectConfigService(ConfigDir));
    services.AddSingleton<ScaleLibraryViewModel>(sp =>
        new ScaleLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
    services.AddSingleton<AlarmLibraryViewModel>(sp =>
        new AlarmLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
    services.AddSingleton<CommLibraryViewModel>(sp =>
        new CommLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
    services.AddSingleton<CanvasViewModel>();

    // ★ ConfigBundle — 번들로 묶기
    services.AddSingleton<ConfigBundle>(sp => new ConfigBundle {
        Loader  = sp.GetRequiredService<JsonConfigLoader>(),
        Writer  = sp.GetRequiredService<JsonWriteService>(),
        Collect = sp.GetRequiredService<CollectConfigService>(),
        Scale   = sp.GetRequiredService<ScaleLibraryViewModel>(),
        Alarm   = sp.GetRequiredService<AlarmLibraryViewModel>(),
        Comm    = sp.GetRequiredService<CommLibraryViewModel>(),
        Canvas  = sp.GetRequiredService<CanvasViewModel>(),
    });

    // DeviceTree + 메인 ViewModel
    services.AddSingleton<DeviceTreeViewModel>();
    services.AddSingleton<StudioMainViewModel>(sp => new StudioMainViewModel(
        sp.GetRequiredService<DeviceTreeViewModel>(),
        sp.GetRequiredService<ConfigBundle>()           // ← 2개 파라미터
    ));

    // ★ Singleton 필수 (Transient 금지)
    services.AddSingleton<MainWindow>(sp =>
        new MainWindow(sp.GetRequiredService<StudioMainViewModel>()));

    return services.BuildServiceProvider();
}
```

---

## 4. IIoT.Collector 통합 핵심 — EventBus 정상화

```csharp
// IIoT.Collector/App.xaml.cs
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    _themeSettings = new ThemeSettingsService();
    _themeSettings.LoadAndApply(this);

    LogManager.Instance.Start(new LogConfig { ... });
    Directory.CreateDirectory(ConfigDir);

    // ★ 두 엔진이 동일 프로세스 — EventBus 정상 동작
    var collectionEngine = new CollectionEngine(ConfigDir);
    var monitorEngine    = new MonitorEngine(ConfigDir);

    // CollectionEngine이 발행 → MonitorEngine이 동일 프로세스에서 수신 (정상)
    // 이전: CollectorRuntime(별도 프로세스) → Monitor(별도 프로세스) X
    // 현재: 동일 프로세스 내 EventBus.Publish/Subscribe ✅

    _vm = new MainViewModel(collectionEngine, monitorEngine);

    // FSW: .signal 파일 감지 → 재시작
    _watcher = new ConfigReloadWatcher(ConfigDir);
    _watcher.ReloadRequested += async _ => {
        await collectionEngine.RestartAsync();
        await monitorEngine.RestartAsync();
    };
    _watcher.Start();

    // ★ Singleton 필수
    new MainWindow(_vm).Show();
}
```

---

## 5. IIoT.Shared 구조

```csharp
// Models/TagValue.cs
public sealed record TagValue(
    string      TagId,
    double      Value,
    DateTime    Timestamp,
    TagQuality  Quality);

public enum TagQuality { Good, Uncertain, Bad }

// Models/LiveTagValue.cs (UI 바인딩용 ObservableObject)
public sealed partial class LiveTagValue : ObservableObject
{
    [ObservableProperty] private string   _tagId   = "";
    [ObservableProperty] private string   _tagName = "";
    [ObservableProperty] private double   _value;
    [ObservableProperty] private string   _unit    = "";
    [ObservableProperty] private TagQuality _quality = TagQuality.Unknown;

    public string DisplayValue =>
        DecimalPlaces >= 0 ? Value.ToString("F" + DecimalPlaces) : "—";
    public int DecimalPlaces { get; init; } = 2;

    // IIoT.UI.Controls.StatusIndicator 연동
    public IndicatorStatus QualityStatus => Quality switch {
        TagQuality.Good      => IndicatorStatus.Good,
        TagQuality.Uncertain => IndicatorStatus.Warn,
        TagQuality.Bad       => IndicatorStatus.Bad,
        _                    => IndicatorStatus.Unknown,
    };
}

// Contracts/IProtocolDriver.cs
public interface IProtocolDriver : IAsyncDisposable
{
    string DriverId      { get; }
    bool   IsConnected   { get; }
    Task ConnectAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TagValue>> ReadTagsAsync(IReadOnlyList<string> tagIds, CancellationToken ct = default);
    Task WriteTagAsync(string tagId, object value, CancellationToken ct = default);
}
```

---

## 6. 단순화 적용 체크리스트

### 즉시 (1일)
- [ ] `Manager/App.xaml.cs`: `AddTransient<MainWindow>` → `AddSingleton<MainWindow>`
- [ ] `DeviceManager/App.xaml.cs`: 동일 수정
- [ ] `UI/IIoT.UI.Controls/Iiot.Controls.csproj`: ProjectReference 경로 확인
      `..\Themes\IIoT.UI.Themes\IIoT.UI.Themes.csproj`

### 단기 (1~2주)
- [ ] IIoT.Shared 프로젝트 신설 (TagValue·IProtocolDriver·ConfigBundle)
- [ ] ConfigBundle 클래스 구현
- [ ] StudioMainViewModel 생성자 리팩토링 (8→2 파라미터)
- [ ] DeviceManager sln에서 제거 (ConfigApp이 완전 흡수 확인 후)
- [ ] ConfigApp → IIoT.Studio 이름 변경

### 중기 (2~4주)
- [ ] IIoT.Collector 신규 프로젝트 생성
- [ ] CollectionEngine 코드 이식
- [ ] MonitorEngine 코드 이식 (EventBus 구독 유지 — 동일 프로세스)
- [ ] CollectorRuntime, Monitor sln 제거
- [ ] 단일 IIoT.Solution.sln 생성 (Studio+Collector+Manager+Shared+UI)

### 마무리 (1주)
- [ ] 각 프로그램에 IIoT.UI.Controls csproj 참조 추가
- [ ] TagEditorView, SensorEditorView → LabeledField 교체
- [ ] CollectorRuntime LiveTag 목록 → TagValueCell 교체
- [ ] Monitor 알람 목록 → StatusIndicator 교체
- [ ] 전체 빌드 확인 (Clean → Rebuild)
