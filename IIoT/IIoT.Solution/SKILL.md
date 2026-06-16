---
name: iiot-system-arch
description: |
  IIoT/SCADA 산업용 데이터 수집 시스템 설계·개발 가이드.
  ★ 2026-06-15 확정: 4개 프로그램 분리 구조 + 증분 개발 방식(Step-by-Step Base-First).
  IIoT.Studio(설정) · IIoT.Collector(수집) · IIoT.Monitor(감지+모니터링) · IIoT.Manager(관리).

  핵심 원칙: 빈 프로젝트(Base)에서 시작, 기능 하나씩 추가하며 항상 빌드 가능 상태 유지.
  각 Step은 독립적으로 실행 가능한 최소 단위.
  매 Step 완료 후 반드시 컴파일·실행 확인 + 사용 설명 제공.

  포함 내용: 증분 Step 정의·컴파일 확인 절차·각 Step 사용 설명·
  lssLib v5 패턴·ConfigBundle DI·WPF 테마(IIoT.UI.Themes v1.5)·
  AbstractNode/Detector·SDT 압축·Code-Only UserControl 패턴·SignalR 웹 확장.

  다음 상황에서 반드시 이 스킬을 사용하라:
  - IIoT 코드 작성·클래스 설계·아키텍처 결정 시
  - 어느 Step에 있는지·다음 Step이 무엇인지 판단 시
  - lssLib EventBus·AsyncScheduler·CommandQueue·BufSchema 사용 시
  - 프로그램 간 연동(FSW·NamedPipe·SignalR·MQTT) 설계 시
  - AbstractNode·AbstractDetector·IProtocolDriver 설계 시
  - SDT 압축·SPC·OEE·가상Tag·시뮬레이터 구현 시
  - WPF 테마 통합·ThemePickerButton·XAML 오류 해결 시
  - ConfigBundle 패턴·AddSingleton/Transient DI 문제 시
  - Monitor 확장(AbstractDetector 상속·커스텀 감지기) 설계 시
  - SignalR 웹 뷰어·로컬 WPF 동시 연동 설계 시
---

# IIoT 시스템 아키텍처 스킬
## 증분 개발(Base-First) 방식

---

## ★ 핵심 원칙: 항상 실행 가능한 상태 유지

```
빈 창 → 테마 → 레이아웃 → 탭 → 트리 → 편집기 → 저장 → ...
  ↑ 각 단계마다 [컴파일 확인] → [실행 확인] → [사용 설명] → 다음 단계
```

**규칙:**
1. 각 Step 완료 후 **Clean → Rebuild → 실행** 확인 (에러 0개 필수)
2. 한 Step에 파일 **최대 3개** 추가 (많으면 더 잘게 분리)
3. Step 완료 기준 = 에러 0개 + 해당 기능이 화면에서 동작 확인
4. 이전 Step이 깨지면 **현재 Step 작업 중단 → 이전 Step 복구 우선**
5. 코드 생성 후 반드시 **[컴파일 체크리스트]** + **[사용 설명]** 함께 제공

---

## ★ 매 Step 완료 시 Claude가 제공해야 할 것

Step 코드를 생성한 뒤 반드시 아래 두 섹션을 추가로 작성한다:

### [컴파일 확인 체크리스트] 형식
```
## ✅ Step XXX 컴파일 확인 체크리스트

### 1단계: Visual Studio에서 빌드
  [ ] Build 메뉴 → Clean Solution
  [ ] Build 메뉴 → Rebuild Solution
  [ ] 오류 목록(Error List) 창 확인 → 오류(Error) 0개

### 2단계: 런타임 확인
  [ ] F5 (디버그 실행)
  [ ] [이 Step에서 확인해야 할 구체적 동작] 확인
  [ ] 예외(Exception) 없이 정상 종료

### 3단계: 예상 오류 대비
  - 만약 [특정 오류]가 발생하면 → [원인과 해결책]
  - 만약 [다른 오류]가 발생하면 → [원인과 해결책]
```

### [사용 설명] 형식
```
## 📖 Step XXX 사용 설명

### 이번 Step에서 추가된 기능
[기능 한 줄 요약]

### 화면 조작 방법
1. [UI 요소] 클릭/입력 → [결과]
2. [UI 요소] 클릭/입력 → [결과]
...

### 확인 포인트
- [이 기능이 정상 동작할 때 보이는 화면 상태]
- [데이터·파일·로그로 확인할 수 있는 것]

### 다음 Step 예고
다음 [Step 이름]에서는 [다음에 추가될 기능]을 구현합니다.
```

---

## 시스템 구조 한눈 요약

```
IIoT.Solution/  ← 단일 sln
├─ ① IIoT.Studio    (WPF)             — 설정 (JSON·NodeRed·장비트리·스케일)
├─ ② IIoT.Collector (WPF)             — 수집 (폴링·SDT 압축·DB 저장·FSW 재시작)
├─ ③ IIoT.Monitor   (WPF + ASP.NET)  — 감지+모니터링 (로컬WPF + 웹SignalR)
├─ ④ IIoT.Manager   (WPF)             — 오케스트레이션 (프로세스 관리)
├─ IIoT.Shared                         — 공유 모델·인터페이스
├─ IIoT.UI.Themes v1.5                — 7가지 테마
└─ IIoT.UI.Controls v1.0              — 공용 컴포넌트
```

### 프로그램 간 연결 구조

```
[IIoT.Studio]
    │ device.json + .signal 파일 발행
    ▼
[IIoT.Collector]  ←──── FSW .signal 감지 → 자동 재시작
    │ MQTT (TagValueEvent) 브로드캐스트
    │ SQLite (SDT 압축 저장)
    ▼
[IIoT.Monitor]   ←──── MQTT Subscribe (TagValueEvent)
    │ SignalR Hub (웹 뷰어)
    │ NamedPipe (알람 이벤트 → Manager)
    ▼
[IIoT.Manager]  ─────── NamedPipe → Start/Stop/HealthCheck → 전체 프로그램
```

### cross-process 이벤트 전달 패턴

```
★ in-process:     lssLib EventBus (동일 프로세스 내부만)
★ cross-process:  MQTT (Collector → Monitor 수집값 전달)
★ 웹 확장:        SignalR Hub (Monitor → 브라우저 실시간 푸시)
★ 프로세스 제어:  NamedPipe (Manager → 전체)
★ 설정 변경:      FileSystemWatcher + .signal 파일
```

---

## 전체 개발 Step 맵

```
[IIoT.Studio]
 Base-0  빈 WPF 프로젝트 + 테마 연결 → 창 뜨면 완료
 Base-1  메인 레이아웃 (헤더 + 탭바 + 본문 영역)
 Base-2  탭 전환 (버튼 클릭 → 패널 전환)
 S-01    장비 트리 (빈 TreeView + 그룹/장비/PLC/Tag 타입)
 S-02    그룹 편집기 (이름·설명 입력 + 저장)
 S-03    장비 편집기 (모델·제조사·위치 입력 + 저장)
 S-04    PLC 편집기 (통신 라이브러리 연결 + 폴링 주기)
 S-05    Tag 편집기 (레지스터 주소·데이터타입)
 S-06    스케일 라이브러리 (목록 + Raw→공학단위 편집)
 S-07    알람 라이브러리 (HH/H/L/LL 4단계 편집)
 S-08    통신 라이브러리 (Modbus/Serial/MQTT 편집)
 S-09    Tag에 스케일·알람·통신 연결
 S-10    device.json 저장 (.signal 발행)
 S-11    NodeRed 캔버스 (빈 캔버스 + 노드 팔레트)
 S-12    캔버스 노드 CRUD + 연결선
 S-13    collect.json 저장

[IIoT.Collector]  ← 순수 수집 전담 (감지·알람 없음)
 Base-0  빈 WPF 프로젝트 + 테마 연결
 Base-1  메인 레이아웃 (헤더 + 탭 구조)
 C-01    device.json 로드 → 메모리 모델
 C-02    VirtualDriver (사인파 시뮬레이터)
 C-03    AsyncScheduler 폴링 루프
 C-04    수집 현황 ListView (LiveTag 실시간 표시)
 C-05    스케일 변환 (Raw → 공학단위, ScaleConfig 적용)
 C-06    SDT 압축 + CommandQueue → SQLite 저장
 C-07    MQTT 브로드캐스트 (TagValueEvent → Monitor 전달)
 C-08    FSW .signal 감지 → 자동 재시작

[IIoT.Monitor]  ★ 신규 분리 프로그램
 Base-0  빈 WPF + ASP.NET Core 혼합 프로젝트 + 테마 연결
 Base-1  메인 레이아웃 (헤더 + 탭 구조)
 MO-01   MQTT Subscribe → TagValueEvent 수신
 MO-02   실시간 태그 현황 화면 (LiveTag ListView)
 MO-03   AbstractDetector 추상 클래스 + ThresholdDetector 구현
 MO-04   MonitorEngine (감지기 등록·실행 관리)
 MO-05   AlarmStateManager (알람 발행/ACK/복귀)
 MO-06   알람 뷰 (활성알람 ListView + ACK 버튼)
 MO-07   SignalR Hub (웹 브라우저 실시간 푸시)
 MO-08   웹 뷰어 (HTML/JS 단일 파일 — 태그현황 + 알람)
 MO-09   커스텀 감지기 확장 예제 (RateOfChangeDetector)

[IIoT.Manager]
 Base-0  빈 WPF 프로젝트 + 테마 연결
 M-01    프로세스 상태 표시 (Studio·Collector·Monitor 실행 여부)
 M-02    Start/Stop 버튼 → 프로세스 제어
 M-03    NamedPipe 헬스체크
 M-04    로그 뷰어 통합
```

---

## Step 상세 — IIoT.Studio

---

### Base-0: 빈 WPF + 테마

**추가 파일:**
```
IIoT.Studio.csproj      ← net8.0-windows, UseWPF, 테마 ProjectReference
App.xaml                ← MergedDictionaries 구조, BoolToVisibility 등록
App.xaml.cs             ← ThemeSettingsService.LoadAndApply(this)
MainWindow.xaml         ← 빈 Window, BgBrush 배경, ThemePickerButton
MainWindow.xaml.cs      ← 생성자만
```

**핵심 패턴:**
```xml
<!-- App.xaml — BoolToVisibility 반드시 MergedDictionaries 안에 -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary>
                <conv:BoolToVisibilityConverter x:Key="BoolToVisibility"/>
            </ResourceDictionary>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```
```csharp
// App.xaml.cs — OnStartup 최소 구조
protected override void OnStartup(StartupEventArgs e) {
    base.OnStartup(e);
    _themeSettings = new ThemeSettingsService();
    _themeSettings.LoadAndApply(this);           // 저장된 테마 복원
    _services = _ConfigureServices();
    _services.GetRequiredService<MainWindow>().Show();
}
protected override async void OnExit(ExitEventArgs e) {
    _themeSettings?.Dispose();                   // 이벤트 구독 해제 필수
    base.OnExit(e);
}
// ★ 반드시 AddSingleton (Transient → 이중 창 버그)
services.AddSingleton<MainWindow>(...);
```

**✅ 컴파일 확인 체크리스트:**
```
1단계: 빌드
  [ ] Clean Solution → Rebuild Solution → 오류 0개

2단계: 런타임
  [ ] F5 실행 → 테마가 적용된 창이 뜸 (BgBrush 배경색 확인)
  [ ] 우측 상단 ThemePickerButton 클릭 → 테마 목록 팝업 표시
  [ ] 테마 선택 → 즉시 색상 전환 확인
  [ ] 창 닫고 재실행 → 마지막 선택 테마가 유지됨 확인

3단계: 예상 오류 대비
  - XamlParseException: BoolToVisibility → MergedDictionaries 구조 확인
  - IIoT.UI.Themes 어셈블리 못 찾음 → csproj ProjectReference 경로 확인
  - ThemePickerButton 없음 → xmlns:uc assembly= 선언 확인
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: 테마 전환 가능한 빈 창

화면 조작 방법:
  1. 프로그램 실행 → 어두운 배경의 빈 창 표시
  2. 우측 상단 테마 버튼(🎨) 클릭 → 7가지 테마 목록 팝업
  3. 원하는 테마 클릭 → 즉시 전체 색상 전환
  4. 프로그램 종료 후 재실행 → 선택한 테마 자동 복원

확인 포인트:
  - %AppData%\IIoT\ui-settings.json 파일이 생성됨
  - 테마 전환 시 배경·텍스트·버튼 색상이 모두 동시에 바뀜

다음 Step 예고:
  Base-1에서는 헤더·탭바·본문 영역으로 구성된 메인 레이아웃을 추가합니다.
```

---

### Base-1: 메인 레이아웃

**추가 파일:**
```
ViewModels/StudioMainViewModel.cs  ← ObservableObject, SaveStatus만
```
**변경 파일:**
```
MainWindow.xaml    ← DockPanel: 헤더(56px) + 탭바(40px) + 본문(Grid)
MainWindow.xaml.cs ← DI 생성자로 DataContext 주입
```

**레이아웃 뼈대:**
```xml
<DockPanel>
    <Border DockPanel.Dock="Top" Height="56"
            Background="{DynamicResource SurfaceBrush}">
        <!-- 헤더: 타이틀 + ThemePickerButton -->
    </Border>
    <Border DockPanel.Dock="Top" Height="40"
            Background="{DynamicResource CardBrush}">
        <!-- 탭바: 버튼 5개 (임시) -->
    </Border>
    <Grid Background="{DynamicResource BgBrush}">
        <!-- 본문: 임시 TextBlock "탭 내용 준비 중" -->
    </Grid>
</DockPanel>
```

**✅ 컴파일 확인 체크리스트:**
```
1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개

2단계: 런타임
  [ ] F5 실행 → 헤더(56px) + 탭바(40px) + 본문 영역 3단 구조 확인
  [ ] 헤더에 "IIoT Studio" 타이틀 표시 확인
  [ ] ThemePickerButton이 헤더 우측에 위치 확인
  [ ] 창 크기 조절 시 본문이 자연스럽게 늘어남 확인

3단계: 예상 오류 대비
  - DataContext null → DI 등록 후 GetRequiredService<> 호출 확인
  - DynamicResource 리소스 없음 → ThemeManager.Apply()가 OnStartup에서 호출됐는지 확인
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: 3단 메인 레이아웃 (헤더·탭바·본문)

화면 조작 방법:
  - 아직 탭 전환은 동작하지 않음 (다음 Step에서 구현)
  - 헤더 하단 상태바에 SaveStatus 바인딩 텍스트 표시 확인

확인 포인트:
  - 헤더(진한 배경) / 탭바(카드 배경) / 본문(기본 배경) 3가지 색상 구분 확인
  - 창 최소화·최대화 시 레이아웃 유지 확인

다음 Step 예고:
  Base-2에서는 탭바 버튼 클릭 시 본문 패널이 전환되는 기능을 추가합니다.
```

---

### Base-2: 탭 전환

**변경 파일:**
```
ViewModels/StudioMainViewModel.cs
  ← SwitchTabCommand (string 파라미터)
  ← ActiveTabIndex, IsDeviceTab/IsCanvasTab/IsScaleTab/IsAlarmTab/IsCommTab
MainWindow.xaml
  ← 탭 버튼 5개에 Command + CommandParameter="0"~"4"
  ← 본문 Grid에 패널 5개 (색상 구분 임시 Rectangle + 탭명 TextBlock)
```

**핵심 패턴:**
```csharp
// ★ CommandParameter="0"은 string → RelayCommand<int> 타입 불일치 예외
// 반드시 string 파라미터 + TryParse
[RelayCommand]
private void SwitchTab(string tabParam) {
    if (!int.TryParse(tabParam, out var idx)) return;
    ActiveTabIndex = idx;
}

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsDeviceTab))]
[NotifyPropertyChangedFor(nameof(IsCanvasTab))]
[NotifyPropertyChangedFor(nameof(IsScaleTab))]
[NotifyPropertyChangedFor(nameof(IsAlarmTab))]
[NotifyPropertyChangedFor(nameof(IsCommTab))]
private int _activeTabIndex;

public bool IsDeviceTab => ActiveTabIndex == 0;
public bool IsCanvasTab => ActiveTabIndex == 1;
public bool IsScaleTab  => ActiveTabIndex == 2;
public bool IsAlarmTab  => ActiveTabIndex == 3;
public bool IsCommTab   => ActiveTabIndex == 4;
```

```xml
<!-- 임시 패널 — 실제 View는 이후 Step에서 교체 -->
<Rectangle Fill="#1A3A5C"
           Visibility="{Binding IsDeviceTab, Converter={StaticResource BoolToVisibility}}"/>
<TextBlock Text="장비 관리 탭"
           Visibility="{Binding IsDeviceTab, Converter={StaticResource BoolToVisibility}}"
           VerticalAlignment="Center" HorizontalAlignment="Center"
           FontSize="20" Foreground="{DynamicResource TextBrush}"/>
```

**✅ 컴파일 확인 체크리스트:**
```
1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개
  [ ] ArgumentException 없음 확인 (SwitchTab string 파라미터)

2단계: 런타임
  [ ] F5 실행 → 기본(장비 관리) 탭 표시
  [ ] "장비 관리" 버튼 클릭 → 탭1 배경색 패널로 전환
  [ ] "수집 흐름" 버튼 클릭 → 탭2 배경색 패널로 전환
  [ ] "스케일" / "알람 규칙" / "통신 설정" 버튼도 각각 전환 확인
  [ ] 각 탭 이름이 패널 중앙에 표시 확인

3단계: 예상 오류 대비
  - ArgumentException CommandParameter → SwitchTab(string) 시그니처 확인
  - 탭 전환 안 됨 → [NotifyPropertyChangedFor] 어트리뷰트 누락 확인
  - BoolToVisibility 리소스 없음 → MainWindow.Resources에 직접 등록 확인
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: 탭 전환 (5개 탭 패널)

화면 조작 방법:
  1. 상단 탭바에서 [장비 관리] 클릭 → 파란 배경 패널 표시
  2. [수집 흐름] 클릭 → 다른 색상 패널로 즉시 전환
  3. [스케일] / [알람 규칙] / [통신 설정] 각각 클릭 → 패널 전환 확인
  ※ 현재는 임시 색상 블록, 실제 내용은 이후 Step에서 추가됨

확인 포인트:
  - 5개 탭이 독립적으로 전환됨 (이전 탭이 사라지고 새 탭이 나타남)
  - 창 크기 변경 시 패널이 전체 영역을 채움

다음 Step 예고:
  S-01에서는 장비 관리 탭에 실제 TreeView를 추가합니다.
  그룹/장비/PLC/Tag를 트리 형태로 관리할 수 있게 됩니다.
```

---

### S-01: 장비 트리

**추가 파일:**
```
ViewModels/DeviceTree/DeviceTreeViewModel.cs
  ← AbstractTreeNode (Name, Description, IconGlyph, Badge, Children)
  ← GroupTreeNode / DeviceTreeNode / PlcTreeNode / TagTreeNode
  ← RootNodes, SelectedNode
  ← IsNoneSelected / IsGroupSelected / IsDeviceSelected / IsPlcSelected / IsTagSelected
  ← GroupEditor / DeviceEditor / PlcEditor / TagEditor (as 캐스팅 프로퍼티)
  ← SelectNode(object?), AddGroupCommand, AddDeviceCommand, DeleteSelectedCommand

Views/DeviceTree/DeviceTreeView.xaml + .cs
  ← 좌(280px): TreeView + 추가 버튼 + 검색 TextBox
  ← 우(*): 미선택 안내 StackPanel (IsNoneSelected)
  ← TreeView_SelectedItemChanged → vm.SelectNode(e.NewValue)
```

**핵심 패턴:**
```xml
<!-- UserControl.Resources 위치: 반드시 <UserControl> 직계 자식 -->
<!-- Grid나 Border 안에 넣으면 MC3015 오류 -->
<UserControl xmlns:conv="...IIoT.UI.Themes.Controls...">
    <UserControl.Resources>
        <conv:BoolToVisibilityConverter x:Key="BoolToVisibility"/>
    </UserControl.Resources>
    <Grid>...</Grid>
</UserControl>
```
```csharp
// TreeView.SelectedItem → TwoWay 바인딩 WPF 미지원
// 코드비하인드 이벤트로 직접 호출
private void TreeView_SelectedItemChanged(object sender,
    RoutedPropertyChangedEventArgs<object> e) {
    if (DataContext is DeviceTreeViewModel vm) vm.SelectNode(e.NewValue);
}

// ★ [NotifyPropertyChangedFor] 필수 — nameof() 구독 방식 금지
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsNoneSelected))]
[NotifyPropertyChangedFor(nameof(IsGroupSelected))]
[NotifyPropertyChangedFor(nameof(IsDeviceSelected))]
[NotifyPropertyChangedFor(nameof(IsPlcSelected))]
[NotifyPropertyChangedFor(nameof(IsTagSelected))]
[NotifyPropertyChangedFor(nameof(GroupEditor))]
[NotifyPropertyChangedFor(nameof(DeviceEditor))]
[NotifyPropertyChangedFor(nameof(PlcEditor))]
[NotifyPropertyChangedFor(nameof(TagEditor))]
private AbstractTreeNode? _selectedNode;
```

**✅ 컴파일 확인 체크리스트:**
```
1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개
  [ ] MC3015(UserControl.Resources 위치) 없음 확인

2단계: 런타임
  [ ] F5 실행 → [장비 관리] 탭 클릭 → 좌우 분리된 DeviceTreeView 표시
  [ ] [＋그룹] 버튼 클릭 → 트리에 "그룹 1" 항목 추가됨
  [ ] [＋장비] 버튼 클릭 → 선택된 그룹 아래 "장비 1" 추가됨
  [ ] 트리 항목 클릭 → 우측에 "트리에서 노드를 선택하세요" 메시지 대신 노드 타입 표시
  [ ] 항목 없을 때 우측에 안내 메시지 표시 확인

3단계: 예상 오류 대비
  - TreeView_SelectedItemChanged 없음 → DeviceTreeView.xaml.cs에 메서드 추가
  - 우측 패널 전환 안 됨 → [NotifyPropertyChangedFor] 누락 확인
  - MC3015 → UserControl.Resources를 Grid 밖(UserControl 직계)으로 이동
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: 장비 구조 트리 (그룹/장비/PLC/Tag 계층)

화면 조작 방법:
  1. [장비 관리] 탭 클릭
  2. 좌측 상단 [＋그룹] 버튼 → 트리에 "그룹 1" 추가
     (그룹: 공장·라인·사이트 같은 논리적 묶음)
  3. 그룹 선택 후 [＋장비] 버튼 → 그룹 아래 "장비 1" 추가
     (장비: 압출기·사출기 등 실제 설비)
  4. 트리 항목 클릭 → 우측에 선택된 노드 타입 표시
  5. 아이콘 의미:
     📁 그룹 / 🏭 장비 / 🔧 PLC / 🏷 Tag

확인 포인트:
  - 트리가 계층 구조로 펼침/접힘 동작
  - 항목 선택 시 우측 패널이 변경됨
  - 항목 미선택 시 "트리에서 노드를 선택하세요" 안내 표시

다음 Step 예고:
  S-02에서는 그룹 노드를 선택했을 때 우측에 이름·설명을 편집하는
  그룹 편집기를 추가합니다.
```

---

### S-02 ~ S-05: 편집기

**추가 파일 (한 번에 1개씩 추가, 각각 컴파일 확인):**
```
Views/DeviceTree/GroupEditorView.xaml + .cs    (S-02)
Views/DeviceTree/DeviceEditorView.xaml + .cs   (S-03)
Views/DeviceTree/PlcEditorView.xaml + .cs      (S-04)
Views/DeviceTree/TagEditorView.xaml + .cs      (S-05)
```

**핵심 패턴:**
```xml
<!-- DeviceTreeView.xaml 우측 — ContentControl 대신 Visibility 방식 -->
<!-- DataTemplate 안에서 local: 사용 시 WPF 컴파일 오류 회피 목적 -->
<Grid Grid.Column="2">
    <StackPanel Visibility="{Binding IsNoneSelected, Converter={StaticResource BoolToVisibility}}">
        <TextBlock Text="트리에서 노드를 선택하세요" .../>
    </StackPanel>
    <local:GroupEditorView
        DataContext="{Binding GroupEditor}"
        Visibility="{Binding IsGroupSelected, Converter={StaticResource BoolToVisibility}}"/>
    <local:DeviceEditorView
        DataContext="{Binding DeviceEditor}"
        Visibility="{Binding IsDeviceSelected, Converter={StaticResource BoolToVisibility}}"/>
    <local:PlcEditorView
        DataContext="{Binding PlcEditor}"
        Visibility="{Binding IsPlcSelected, Converter={StaticResource BoolToVisibility}}"/>
    <local:TagEditorView
        DataContext="{Binding TagEditor}"
        Visibility="{Binding IsTagSelected, Converter={StaticResource BoolToVisibility}}"/>
</Grid>
```
```xml
<!-- 편집기 내부 ScrollViewer — MC3089 방지: 자식 2개 → Grid로 래핑 -->
<ScrollViewer>
    <Grid>
        <StackPanel Visibility="{Binding ...미선택...}">안내 텍스트</StackPanel>
        <StackPanel Visibility="{Binding ...선택...}">편집 폼</StackPanel>
    </Grid>
</ScrollViewer>
```

**✅ 컴파일 확인 체크리스트 (각 편집기마다 반복):**
```
1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개

2단계: 런타임 (S-02 예시 — 나머지도 동일 방식)
  [ ] 트리에서 그룹(📁) 클릭 → 우측에 GroupEditorView 표시
  [ ] "그룹 이름" TextBox에 텍스트 입력 가능
  [ ] 다른 노드 타입 선택 시 다른 편집기로 전환됨
  [ ] 노드 미선택 시 안내 메시지 복귀

3단계: 예상 오류 대비
  - MC3089 → 편집기 내 ScrollViewer에 Grid 래핑 추가
  - DataContext null → GroupEditor 프로퍼티 반환값 확인
```

**📖 사용 설명 (S-02 예시):**
```
이번 Step에서 추가된 기능: 그룹 편집기

화면 조작 방법:
  1. 트리에서 그룹(📁) 클릭 → 우측에 그룹 편집 폼 표시
  2. "그룹 이름" 입력란에 이름 입력 (예: "A동 라인1")
  3. "설명" 입력란에 설명 입력 (선택 사항)
  ※ 저장 기능은 S-10에서 추가됨 (현재는 메모리에만 존재)

확인 포인트:
  - 그룹 선택 시 편집기 표시, 장비/PLC/Tag 선택 시 다른 편집기로 전환
  - 트리 클릭 시마다 편집기가 즉시 해당 노드 정보로 업데이트

S-03: 장비 편집기 — 모델명·제조사·설치 위치 입력
S-04: PLC 편집기 — 통신 라이브러리 연결, 폴링 주기 설정
S-05: Tag 편집기 — 레지스터 주소, 데이터 타입, 단위 설정
```

---

### S-06 ~ S-08: 라이브러리

**추가 파일:**
```
ViewModels/Library/LibraryViewModels.cs
  ← ScaleLibraryViewModel (Scales, SelectedScale, IsNoneSelected/IsScaleSelected)
  ← AlarmLibraryViewModel (AlarmRules, SelectedRule, HH/H/L/LL 필드)
  ← CommLibraryViewModel  (CommConfigs, SelectedConfig, IsModbusTcp/IsSerial/IsMqtt)

Views/Library/ScaleLibraryView.xaml + .cs    (S-06)
Views/Library/AlarmLibraryView.xaml + .cs    (S-07)
Views/Library/CommLibraryView.xaml + .cs     (S-08)
```

**핵심 패턴:**
```csharp
// CommLibraryViewModel — 프로토콜 전환 시 UI 폼 전환
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsNoneSelected))]
[NotifyPropertyChangedFor(nameof(IsConfigSelected))]
[NotifyPropertyChangedFor(nameof(IsModbusTcp))]
[NotifyPropertyChangedFor(nameof(IsSerial))]
[NotifyPropertyChangedFor(nameof(IsMqtt))]
private CommConfigViewModel? _selectedConfig;

public bool IsModbusTcp => SelectedConfig?.Protocol == "ModbusTcp";
public bool IsSerial     => SelectedConfig?.Protocol == "Serial";
public bool IsMqtt       => SelectedConfig?.Protocol == "Mqtt";
```

**✅ 컴파일 확인 체크리스트 (S-06 예시):**
```
1단계: 빌드 → 오류 0개

2단계: 런타임
  [ ] [스케일] 탭 클릭 → 좌우 분할 ScaleLibraryView 표시
  [ ] [＋ 추가] 버튼 → 좌측 목록에 "스케일 1" 항목 추가
  [ ] 항목 클릭 → 우측에 Raw 범위 / 공학단위 범위 편집 폼 표시
  [ ] Raw Min·Max / Eng Min·Max 입력 → 하단 변환식 미리보기 업데이트
  [ ] [✕] 버튼 → 항목 삭제

S-07 추가 확인:
  [ ] [알람 규칙] 탭 → AlarmLibraryView 표시
  [ ] HH/H/L/LL 4개 임계값 및 메시지 입력 가능
  [ ] 각 레벨별 색상 구분 확인 (HH=빨강, H=주황, L=파랑, LL=보라)

S-08 추가 확인:
  [ ] [통신 설정] 탭 → CommLibraryView 표시
  [ ] 프로토콜 콤보박스에서 ModbusTcp 선택 → IP/포트 입력 폼 표시
  [ ] Serial 선택 → COM포트/보드레이트 폼으로 전환
  [ ] MQTT 선택 → 브로커/ClientId 폼으로 전환
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: 스케일·알람·통신 라이브러리

[스케일 라이브러리 (S-06)]
  용도: PLC Raw 값(0~4095)을 공학단위(0~100 bar)로 변환하는 규칙 정의
  조작: [스케일] 탭 → [＋추가] → Raw Min/Max·공학단위 Min/Max·단위 입력
  예시: Raw 0~4095 → 0~10 bar, 소수점 2자리

[알람 라이브러리 (S-07)]
  용도: 임계값 초과 시 발생할 알람 조건 정의 (HH/H/L/LL 4단계)
  조작: [알람 규칙] 탭 → [＋추가] → 각 레벨 임계값·알람 메시지 입력
  예시: HH=95°C(온도 위험), H=90°C(온도 주의), L=10°C(온도 낮음), LL=5°C(온도 위험 낮음)

[통신 라이브러리 (S-08)]
  용도: PLC와 통신하는 연결 정보 정의
  조작: [통신 설정] 탭 → [＋추가] → 프로토콜 선택 → 세부 설정 입력
  예시: ModbusTcp / IP 192.168.1.10 / 포트 502 / Unit ID 1

※ 실제 저장은 S-10에서 구현, 현재는 메모리에만 존재

다음 Step 예고:
  S-09에서 Tag에 스케일·알람·통신 라이브러리를 연결합니다.
```

---

### S-09: 라이브러리 연결

**변경 파일:**
```
ViewModels/DeviceTree/DeviceTreeViewModel.cs
  ← PlcTreeNode.AvailableCommLibraries 추가 (CommLibraryViewModel 목록)
  ← TagTreeNode.AvailableScales / AvailableAlarms 추가
Views/DeviceTree/PlcEditorView.xaml
  ← 통신 라이브러리 ComboBox 추가 (DisplayMemberPath="Name")
Views/DeviceTree/TagEditorView.xaml
  ← 스케일 규칙 ComboBox + 알람 규칙 ComboBox 추가
```

**✅ 컴파일 확인 체크리스트:**
```
2단계: 런타임
  [ ] PLC 편집기 → "통신 라이브러리" 드롭다운에 S-08에서 추가한 항목 표시
  [ ] Tag 편집기 → "스케일 규칙" 드롭다운에 S-06에서 추가한 항목 표시
  [ ] Tag 편집기 → "알람 규칙" 드롭다운에 S-07에서 추가한 항목 표시
  [ ] 드롭다운 선택 → 연결 관계 메모리에 반영 확인
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: 장비 구조와 라이브러리 연결

화면 조작 방법:
  1. [통신 설정] 탭에서 "Modbus-라인1" 통신 설정 추가
  2. [스케일] 탭에서 "압력 스케일" 규칙 추가
  3. [알람 규칙] 탭에서 "압력 알람" 규칙 추가
  4. [장비 관리] 탭 → 트리에서 PLC 선택
     → 우측 편집기의 "통신 라이브러리" 드롭다운에서 "Modbus-라인1" 선택
  5. Tag 선택 → "스케일 규칙"에서 "압력 스케일" / "알람 규칙"에서 "압력 알람" 선택

확인 포인트:
  - 드롭다운에 라이브러리 탭에서 추가한 항목들이 모두 표시됨
  - 연결 후 탭 이동·복귀해도 선택 상태 유지

다음 Step 예고:
  S-10에서 전체 설정을 device.json 파일로 저장합니다.
```

---

### S-10: device.json 저장

**추가 파일:**
```
Core/Config/JsonWriteService.cs
  ← 원자적 저장: .tmp → File.Replace → .bak
  ← WriteSignal(fileName): .signal 파일 생성
Core/Config/JsonConfigLoader.cs
  ← LoadAll(): scale/alarm/comm 라이브러리 JSON 로드
Core/Config/ConfigInitializer.cs
  ← EnsureConfigFiles(dir): 앱 시작 시 기본 파일 생성
  ← private sealed record ConfigFileSpec (CS9051 주의)
Shared/Config/ConfigBundle.cs
  ← DI 번들 (object 타입 보관 + Get<T>() 타입 안전 접근)
```

**핵심 패턴:**
```csharp
// ★ ConfigInitializer: file record 금지 → CS9051 발생
// private sealed record 사용
private sealed record ConfigFileSpec(string FileName, string DefaultContent);

// ★ JsonServices.cs — 단일 파일에 namespace 1개 (CS8954 방지)
// JsonWriteService + JsonConfigLoader를 하나의 파일에 쓸 때
// 두 번째 namespace 선언 절대 금지
namespace IIoT.Studio.Core.Config;
// ↑ 파일 전체에 이 하나만

// ★ 원자적 쓰기 패턴
private static void _AtomicWrite(string path, string content) {
    var tmp = path + ".tmp";
    var bak = path + ".bak";
    File.WriteAllText(tmp, content, Encoding.UTF8);
    if (File.Exists(path)) File.Replace(tmp, path, bak);
    else File.Move(tmp, path);
}
```

**✅ 컴파일 확인 체크리스트:**
```
1단계: 빌드 → 오류 0개
  [ ] CS9051(file record) 없음
  [ ] CS8954(namespace 2개) 없음

2단계: 런타임
  [ ] 장비 구조 구성 후 [💾 전체 저장] 버튼 클릭
  [ ] [실행파일경로]\Config\device.json 파일 생성 확인
  [ ] [실행파일경로]\Config\device.json.signal 파일 생성 확인
  [ ] device.json 내용이 JSON 형식으로 올바르게 저장됨 확인
  [ ] 프로그램 재시작 후 기존 설정 복원 확인 (로드 구현 시)
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: 설정 파일 저장 및 Collector 재시작 신호

화면 조작 방법:
  1. 장비 트리 구성 + 라이브러리 설정 완료
  2. 헤더 우측 [💾 전체 저장] 버튼 클릭
  3. 헤더 상태바에 "저장 완료 (HH:MM:SS)" 메시지 표시
  4. "● 미저장" 황색 뱃지 사라짐

확인 포인트 (파일 탐색기):
  - Config\device.json → 장비 트리 전체 JSON 구조
  - Config\scale-library.json → 스케일 규칙 목록
  - Config\alarm-library.json → 알람 규칙 목록
  - Config\comm-library.json → 통신 설정 목록
  - Config\device.json.signal → Collector가 감지해 재시작할 트리거 파일
    (Collector 실행 중이면 자동으로 삭제되며 Collector가 재시작됨)

다음 Step 예고:
  S-11에서 NodeRed 스타일 수집 흐름 캔버스를 추가합니다.
```

---

### S-11 ~ S-13: NodeRed 캔버스

**추가 파일:**
```
Core/Canvas/AbstractNode.cs   ← NodeId, Label, X, Y, Width, Height, Category
Core/Canvas/NodePort.cs       ← PortId, Direction(Input/Output), OwnerNodeId
Core/Canvas/NodeConnection.cs ← ConnectionId, Source/Target NodeId+PortId
ViewModels/Canvas/CanvasViewModel.cs
  ← Nodes, Connections, Palette (팔레트 항목 목록)
  ← Scale/OffsetX/OffsetY (줌·패닝)
  ← AddNodeCommand(string nodeType), DeleteSelectedNodeCommand
  ← SerializeToJson(), DeserializeFromJson(string json)
Views/Canvas/CanvasView.xaml + .cs
  ← 좌(200px): 팔레트(노드 종류 버튼 목록)
  ← 우(*): 캔버스(ItemsControl + ScaleTransform)
  ← PreviewMouseDown/Up (중간 버튼 패닝)
Core/Config/CollectConfigService.cs
  ← SaveCanvas(CanvasViewModel) → collect.json + .signal
```

**핵심 패턴:**
```csharp
// ★ Border에는 MouseMiddleButtonDown/Up 이벤트 없음 → CS1061
// PreviewMouseDown/Up + ChangedButton == Middle 로 교체
CanvasBorder.PreviewMouseDown += (s, e) => {
    if (e.ChangedButton != MouseButton.Middle && !_spaceDown) return;
    _isPanning = true;
    _panStart  = e.GetPosition(CanvasBorder);
    CanvasBorder.CaptureMouse();
};
CanvasBorder.PreviewMouseUp += (s, e) => {
    if (e.ChangedButton != MouseButton.Middle && !_spaceDown) return;
    _isPanning = false;
    CanvasBorder.ReleaseMouseCapture();
};
```

**✅ 컴파일 확인 체크리스트:**
```
1단계: 빌드 → 오류 0개
  [ ] CS1061(MouseMiddleButton) 없음

2단계: 런타임
  [ ] [수집 흐름] 탭 클릭 → 캔버스 화면 표시
  [ ] 좌측 팔레트에서 "Modbus Input" 버튼 클릭 → 캔버스에 노드 카드 추가
  [ ] 노드 카드 마우스 드래그 → 위치 이동
  [ ] 마우스 휠 → 캔버스 줌 인/아웃
  [ ] 중간 버튼 드래그 or Space+드래그 → 캔버스 패닝
  [ ] 노드 선택 후 Delete 키 → 노드 삭제
  [ ] [💾 전체 저장] → Config\collect.json 생성 확인
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: NodeRed 스타일 수집 흐름 편집기

화면 조작 방법:
  [노드 추가]
  1. [수집 흐름] 탭 클릭
  2. 좌측 팔레트에서 노드 종류 선택 (예: "Modbus Input")
  3. 버튼 클릭 → 캔버스 중앙에 노드 카드 추가

  [노드 이동]
  4. 노드 카드를 마우스로 드래그 → 원하는 위치로 이동

  [캔버스 탐색]
  5. 마우스 휠 → 줌 인/아웃 (30%~300%)
  6. 마우스 중간 버튼 누른 채 드래그 or Space + 마우스 드래그 → 패닝

  [노드 연결]
  7. 노드 우측의 출력 포트 → 다른 노드 좌측 입력 포트로 드래그 → 연결선 생성

  [저장]
  8. [💾 전체 저장] 클릭 → collect.json 저장 + Collector 재시작 신호 전송

노드 종류:
  🔌 Modbus Input  — PLC Modbus TCP 레지스터 읽기
  📡 TCP Input     — 원시 TCP 데이터 수신
  🔧 Buffer Parser — 바이너리 데이터 파싱 (BufSchema)
  🔀 Scale Filter  — Raw→공학단위 변환 (MapTo)
  📤 DB Output     — SQLite 저장
  📤 MQTT Output   — MQTT 브로커 발행

다음 Step 예고:
  이제 IIoT.Collector 개발을 시작합니다.
  C-01에서 Studio에서 저장한 device.json을 읽어 수집 설정을 로드합니다.
```

---

## Step 상세 — IIoT.Collector

---

### Base-0: 빈 WPF + 테마

Studio Base-0과 동일 패턴. csproj에 IIoT.Shared 참조 추가.

**✅ 컴파일 확인:** Studio Base-0과 동일 체크리스트

**📖 사용 설명:**
```
Studio와 독립적으로 실행 가능한 Collector 창이 뜹니다.
(아직 수집 기능 없음 — 이후 Step에서 추가)
```

---

### C-01: device.json 로드

**추가 파일:**
```
Core/DeviceConfig.cs   ← PlcConfig(PlcId, CommType, Host, Port, PollMs)
                          TagConfig(TagId, Address, DataType, PollMs)
Core/ConfigLoader.cs   ← LoadDeviceConfig(path) → IReadOnlyList<PlcConfig>
```

**✅ 컴파일 확인 체크리스트:**
```
2단계: 런타임
  [ ] F5 실행
  [ ] 출력(Output) 창에 "device.json 로드 완료 — X개 PLC, Y개 Tag" 로그 확인
  [ ] Studio에서 저장한 PLC/Tag 구조가 올바르게 파싱됨 확인
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: Studio 설정 파일 읽기

사용 방법:
  1. Studio에서 장비 구조 구성 + 저장 (device.json 생성)
  2. Collector 실행 → 자동으로 같은 Config 폴더의 device.json 로드
  3. 로그 창에서 로드된 PLC/Tag 수 확인

확인 포인트:
  - 로그: "device.json 로드 완료 — 2개 PLC, 10개 Tag"
  - Studio와 동일한 Config 폴더를 가리키는지 경로 확인
```

---

### C-02 ~ C-03: VirtualDriver + 폴링

**추가 파일:**
```
Protocols/IProtocolDriver.cs   ← BatchReadAsync(TagConfig[]) → TagValue[]
Protocols/VirtualDriver.cs     ← 사인파 시뮬레이터 TagValue 생성
Core/CollectionEngine.cs
  ← AsyncScheduler.ScheduleRecurring() 폴링
  ← TagValue → EventBus.Publish<TagValueUpdatedEvent>()
```

**핵심 패턴:**
```csharp
// ★ while-true 루프 금지 — AsyncScheduler 필수
AsyncScheduler.Instance.ScheduleRecurring(
    id:       $"poll:{plc.PlcId}",
    interval: TimeSpan.FromMilliseconds(plc.PollMs),
    action:   async ct => {
        var values = await _driver.BatchReadAsync(_tags, ct);
        foreach (var v in values)
            EventBus.Instance.Publish(new TagValueUpdatedEvent(v));
    });
```

**✅ 컴파일 확인 체크리스트:**
```
2단계: 런타임
  [ ] F5 실행
  [ ] 로그에 1초마다 "Tag[xxx] = 3.14 (Good)" 형태 로그 출력
  [ ] 값이 사인파 형태로 변동 (0~100 범위)
  [ ] 프로그램 정상 종료 (AsyncScheduler 정리 확인)
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: 가상 드라이버로 수집 시뮬레이션

사용 방법:
  1. Collector 실행 → 자동으로 폴링 시작 (실제 PLC 없어도 동작)
  2. 출력 창에서 각 Tag의 수집값 확인
  3. 값은 사인파(0~100)로 변동 — 실제 드라이버 교체 전 테스트 용도

확인 포인트:
  - 로그가 설정된 폴링 주기(기본 1000ms)마다 출력됨
  - Tag ID별로 다른 위상의 사인파 값 생성
```

---

### C-04: 수집 현황 UI

**추가 파일:**
```
Models/LiveTagViewModel.cs          ← TagId, DisplayValue, Unit, Quality, UpdatedAt
ViewModels/CollectorMainViewModel.cs ← LiveTags(ObservableCollection), 구독 관리
Views/LiveTagView.xaml + .cs        ← DataGrid/ListView (TagValueCell 사용)
```

**핵심 패턴:**
```csharp
// ★ EventBus 구독 → UI 스레드 업데이트 필수
_sub = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(e => {
    Application.Current.Dispatcher.InvokeAsync(() => {
        var vm = _liveTags.FirstOrDefault(t => t.TagId == e.Value.TagId)
                 ?? _AddLiveTag(e.Value.TagId);
        vm.Update(e.Value);
    });
});

// ★ UserControl Unloaded 시 구독 해제 필수 (메모리 누수 방지)
Unloaded += (_, _) => _sub?.Dispose();
```

**✅ 컴파일 확인 체크리스트:**
```
2단계: 런타임
  [ ] Collector 실행 → [수집 현황] 탭에 Tag 행 자동 추가
  [ ] 1초마다 값 업데이트 (값 변경 시 셀 깜박임 확인)
  [ ] TagId / 값 / 단위 / 품질 / 업데이트 시각 표시 확인
  [ ] 품질 표시: Good(녹색) / Bad(빨강) 구분
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: 실시간 수집 현황 화면

화면 조작 방법:
  1. Collector 실행 → [수집 현황] 탭 자동 표시
  2. 각 Tag의 현재 값이 테이블로 표시됨
     - TagID: 태그 식별자
     - 값: 현재 수집값 (1초마다 갱신)
     - 단위: 스케일 변환 후 단위 (예: bar, °C)
     - 품질: Good/Bad/Uncertain
     - 갱신 시각: 마지막 수집 시각

확인 포인트:
  - 값 변경 시 해당 셀이 잠깐 강조 표시 (플래시 효과)
  - Studio에서 설정한 모든 Tag가 행으로 표시됨
```

---

### C-05: 스케일 변환

**추가 파일:**
```
Core/ScaleConverter.cs  ← ApplyScale(TagValue, ScaleConfig) → TagValue (공학단위)
```

**핵심 패턴:**
```csharp
// ★ Linear / Expression 두 모드 지원
public static TagValue ApplyScale(TagValue raw, ScaleConfig cfg) => cfg.Mode switch {
    ScaleMode.Linear     => raw with { Value = cfg.Slope * raw.Value + cfg.Offset, Unit = cfg.Unit },
    ScaleMode.Expression => raw with { Value = _Eval(cfg.Expression, raw.Value), Unit = cfg.Unit },
    _                    => raw
};
```

**✅ 컴파일 확인 체크리스트:**
```
2단계: 런타임
  [ ] 수집 현황 UI에서 Raw값과 변환값(공학단위) 함께 표시 확인
  [ ] Linear 모드: (Raw × Slope + Offset) 계산 정확도 확인
  [ ] 단위 문자열이 Tag 행에 올바르게 표시됨 확인
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: Raw 값 → 공학단위 자동 변환

사용 방법:
  1. Studio에서 Tag에 스케일 라이브러리 연결 후 저장
  2. Collector 재시작 → 수집 현황에 변환된 값과 단위 표시
     예) PLC Raw 0~4000 → 0.0~10.0 bar 변환

확인 포인트:
  - 수집 현황 ListView: "값: 5.23 bar" 형태 표시
  - Raw값과 변환값 모두 컬럼에 표시 가능
```

---

### C-06 ~ C-07: SDT 압축 + MQTT 브로드캐스트

**추가 파일:**
```
Core/SwingingDoorCompressor.cs  ← ShouldStore(TagValue) → bool
Storage/TagHistoryDb.cs         ← SQLite INSERT (CommandQueue 경유)
Core/MqttPublisher.cs           ← MQTTnet → TagValueEvent JSON 발행
```

**핵심 패턴:**
```csharp
// ★ SDT → CommandQueue → DB (순서 보장)
if (_compressor.ShouldStore(tagValue)) {
    CommandQueue.Instance.Enqueue(LambdaCommand.Create(
        async ct => await _db.InsertAsync(tagValue, ct),
        CommandPriority.Normal));
}

// ★ MQTT 브로드캐스트 (Monitor가 Subscribe)
await _mqtt.PublishAsync(
    topic:   $"iiot/tags/{tagValue.TagId}",
    payload: JsonSerializer.Serialize(tagValue));
```

**✅ 컴파일 확인 체크리스트:**
```
C-06 런타임:
  [ ] Collector 실행 → 수집값이 SQLite DB에 기록됨
  [ ] DB 파일 확인 (DB Browser for SQLite 등으로)
  [ ] 1분 후 저장 건수 → SDT 압축으로 이론치의 5~15%만 저장

C-07 런타임:
  [ ] MQTT 브로커(예: Mosquitto localhost:1883) 실행 확인
  [ ] MQTT Explorer 등으로 "iiot/tags/#" 구독 → 수집값 수신 확인
  [ ] Monitor 실행 시 MQTT로 수집값 수신 확인
```

**📖 사용 설명:**
```
[SDT 압축 (C-06)]
  효과: 1초 주기 10개 Tag → 86,400건/일 중 변동분만 저장 (약 5~10%)
  확인: DB 파일 크기가 이론 최대치보다 90% 이상 작음

[MQTT 브로드캐스트 (C-07)]
  용도: Monitor 프로그램이 수집값을 실시간으로 받아 감지 처리
  토픽: iiot/tags/{TagId} — 각 Tag별 별도 토픽
  페이로드: TagValue JSON (TagId, Value, Unit, Quality, Timestamp)
  ★ Monitor와 cross-process 연동의 핵심 채널
```

---

### C-08: FSW 자동재시작

**추가 파일:**
```
Core/ConfigReloadWatcher.cs  ← FSW *.signal 감지 → CollectionEngine 재시작
```

**핵심 패턴:**
```csharp
// ★ FSW: *.signal 파일만 감시
new FileSystemWatcher(_configDir, "*.signal") {
    EnableRaisingEvents = true
}.Created += async (_, e) => {
    File.Delete(e.FullPath);    // signal 파일 즉시 삭제
    await _RestartAsync();      // CollectionEngine + MqttPublisher 재시작
};
```

**✅ 컴파일 확인 체크리스트:**
```
2단계: 런타임
  [ ] Collector 실행 상태에서 Studio [💾 전체 저장]
  [ ] Config 폴더에 .signal 파일 생성 → 즉시 삭제됨 확인
  [ ] Collector 로그: "설정 변경 감지 → 재시작" 메시지 확인
  [ ] 재시작 후 새 설정으로 수집 + MQTT 발행 재개 확인
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: 설정 변경 자동 반영

흐름: Studio [저장] → .signal 파일 생성 → Collector FSW 감지
      → 수집·MQTT 중단 → 새 device.json 로드 → 재개 (약 2~3초)
확인: Studio에서 Tag 추가 저장 → Collector가 자동으로 새 Tag 수집·발행 시작
```

---

## Step 상세 — IIoT.Monitor ★ 신규

---

### IIoT.Monitor 설계 원칙

```
① WPF + ASP.NET Core 혼합 (단일 프로세스)
   - WPF MainWindow: 로컬 모니터링 UI
   - Kestrel 내장: SignalR Hub → 웹 브라우저 실시간 푸시

② MQTT Subscribe → in-process EventBus 변환
   - MQTTnet으로 수신 → 내부 EventBus.Publish → UI/감지기 배포
   - cross-process 문제 해결: MQTT가 브릿지 역할

③ AbstractDetector 확장 설계
   - 상속으로 커스텀 감지기 추가 가능
   - 플러그인 방식: MonitorEngine에 Register(detector) 한 줄

④ 웹 뷰어: 별도 프레임워크 없는 HTML 단일 파일
   - SignalR JS 클라이언트만 사용
   - C#·JS 어느 언어로든 연동 가능 (표준 SignalR 프로토콜)
```

**csproj 핵심:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <!-- ASP.NET Core 내장을 위한 패키지 -->
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.SignalR" Version="*"/>
    <PackageReference Include="MQTTnet" Version="*"/>
  </ItemGroup>
</Project>
```

---

### Monitor Base-0: 빈 WPF + ASP.NET 혼합 프로젝트

**추가 파일:**
```
IIoT.Monitor.csproj     ← net8.0-windows, UseWPF=true, SignalR + MQTTnet 패키지
App.xaml + App.xaml.cs  ← ThemeSettingsService 연동
MainWindow.xaml + .cs   ← 빈 창 + 테마
Core/MonitorHost.cs     ← WebApplication.CreateBuilder → Kestrel 내장 시작
```

**핵심 패턴:**
```csharp
// ★ WPF 앱 안에서 Kestrel 내장 실행
public class MonitorHost {
    private WebApplication? _app;
    public async Task StartAsync() {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSignalR();
        _app = builder.Build();
        _app.MapHub<MonitorHub>("/monitor");
        await _app.StartAsync();   // 블로킹 아님 — WPF와 공존
    }
}
```

**✅ 컴파일 확인 체크리스트:**
```
1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개
  [ ] NU1201 없음 (net8.0-windows TFM 확인)

2단계: 런타임
  [ ] F5 실행 → 테마 적용된 창 표시
  [ ] 로그: "Kestrel started on http://localhost:5200" 확인
  [ ] 브라우저에서 http://localhost:5200 접속 → 404 (아직 라우트 없음, 정상)
```

---

### MO-01: MQTT Subscribe → EventBus 변환

**추가 파일:**
```
Core/MqttReceiver.cs  ← MQTTnet Subscribe("iiot/tags/#")
                         → JsonSerializer.Deserialize<TagValue>
                         → EventBus.Publish<TagValueUpdatedEvent>()
```

**핵심 패턴:**
```csharp
// ★ MQTT 수신 → in-process EventBus 변환 브릿지
_client.ApplicationMessageReceivedAsync += e => {
    var json = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
    var tagValue = JsonSerializer.Deserialize<TagValue>(json);
    EventBus.Instance.Publish(new TagValueUpdatedEvent(tagValue!));
    return Task.CompletedTask;
};
```

**✅ 컴파일 확인 체크리스트:**
```
2단계: 런타임
  [ ] Collector 실행 (MQTT 발행 중)
  [ ] Monitor 실행 → 로그: "MQTT 연결 완료 — iiot/tags/# 구독 중"
  [ ] Monitor 로그에 수집값 수신 메시지 출력 확인
```

---

### MO-02: 실시간 태그 현황 화면

**추가 파일:**
```
Models/LiveTagViewModel.cs            ← TagId, DisplayValue, Unit, Quality, UpdatedAt
ViewModels/MonitorMainViewModel.cs    ← LiveTags(ObservableCollection)
Views/LiveTagView.xaml + .cs          ← DataGrid (TagValueCell 사용)
```

**핵심 패턴:**
```csharp
// ★ EventBus 구독 → UI 스레드 업데이트
_sub = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(e => {
    Application.Current.Dispatcher.InvokeAsync(() => {
        var vm = LiveTags.FirstOrDefault(t => t.TagId == e.Value.TagId)
                 ?? _AddLiveTag(e.Value.TagId);
        vm.Update(e.Value);
    });
});
Unloaded += (_, _) => _sub?.Dispose();  // ★ 구독 해제 필수
```

---

### MO-03 ~ MO-06: 감지기 + 알람

**추가 파일:**
```
Core/AbstractDetector.cs    ← abstract DetectAsync(TagValue) → DetectResult?
                               DetectResult(TagId, Level, Message, Value)
Core/ThresholdDetector.cs   ← HH/H/L/LL 임계값 비교 구현
Core/MonitorEngine.cs       ← Register(detector), ProcessAsync(TagValue)
Core/AlarmStateManager.cs   ← Active/Acked/Recovered 상태 관리
                               EventBus.Publish<AlarmEvent>() 발행
Models/AlarmRecord.cs       ← AlarmId, TagId, Level, Message, OccurredAt, AckedAt
Views/AlarmView.xaml + .cs  ← 활성 알람 ListView + [ACK] 버튼
```

**AbstractDetector 확장 패턴:**
```csharp
// ★ 커스텀 감지기 추가 방법 — MonitorEngine.Register() 한 줄
public abstract class AbstractDetector {
    public string DetectorId { get; init; } = string.Empty;
    public abstract Task<DetectResult?> DetectAsync(TagValue value, CancellationToken ct);
}

// 예) 변화율 감지기 커스텀 구현
public class RateOfChangeDetector : AbstractDetector {
    private TagValue? _prev;
    public double MaxRatePerSec { get; init; }
    public override Task<DetectResult?> DetectAsync(TagValue v, CancellationToken ct) {
        if (_prev is null) { _prev = v; return Task.FromResult<DetectResult?>(null); }
        var rate = Math.Abs(v.Value - _prev.Value)
                   / (v.Timestamp - _prev.Timestamp).TotalSeconds;
        _prev = v;
        return Task.FromResult(rate > MaxRatePerSec
            ? new DetectResult(v.TagId, AlarmLevel.H, $"변화율 초과: {rate:F2}/s", v.Value)
            : null);
    }
}

// 등록: MonitorEngine.Instance.Register(new RateOfChangeDetector { ... });
```

**✅ MO-06 컴파일 확인 체크리스트:**
```
2단계: 런타임
  [ ] 수집값이 임계값 초과 → [알람] 탭 자동 추가
  [ ] 알람 레벨별 색상 (HH=빨강, H=주황, L=파랑, LL=보라)
  [ ] [ACK] 버튼 → "확인됨" 상태로 변경
  [ ] 헤더 알람 카운터 실시간 변동
```

---

### MO-07 ~ MO-08: SignalR Hub + 웹 뷰어

**추가 파일:**
```
Hubs/MonitorHub.cs      ← IMonitorClient 인터페이스
                           SendTagValue / SendAlarm 메서드
Core/SignalRBridge.cs   ← EventBus 구독 → Hub.Clients.All.SendTagValue()
wwwroot/index.html      ← SignalR JS 클라이언트 단일 파일
                           태그 현황 테이블 + 알람 목록
```

**핵심 패턴:**
```csharp
// ★ SignalR Bridge — EventBus → 웹 브라우저 푸시
public class SignalRBridge {
    private readonly IHubContext<MonitorHub, IMonitorClient> _hub;
    private IDisposable? _sub;

    public void Start() {
        _sub = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(async e =>
            await _hub.Clients.All.SendTagValue(e.Value));
    }
}

// ★ 웹 클라이언트 (HTML 단일 파일, 프레임워크 무관)
// <script src="https://cdnjs.cloudflare.com/.../signalr.min.js"></script>
// const conn = new signalR.HubConnectionBuilder()
//     .withUrl("http://localhost:5200/monitor").build();
// conn.on("SendTagValue", (tag) => { /* 테이블 업데이트 */ });
```

**✅ MO-08 컴파일 확인 체크리스트:**
```
2단계: 런타임
  [ ] Monitor 실행 → Kestrel 시작 확인
  [ ] 브라우저 http://localhost:5200 접속 → 웹 뷰어 화면 표시
  [ ] 웹 뷰어에서 수집값 실시간 갱신 확인 (1초마다)
  [ ] 알람 발생 시 웹 뷰어에도 알람 표시 확인
  [ ] WPF 창과 웹 뷰어가 동시에 동일 데이터 표시 확인
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: 로컬 WPF + 웹 브라우저 동시 모니터링

사용 방법:
  [로컬 WPF 뷰어]
  - Monitor 실행 → 창에서 바로 실시간 태그·알람 확인

  [웹 뷰어]
  - 브라우저 → http://localhost:5200
  - 동일 데이터가 실시간으로 표시됨
  - 동일 네트워크 내 다른 PC에서도 접속 가능
    (http://{Monitor PC IP}:5200)

확인 포인트:
  - 여러 브라우저 탭 동시 접속 → 모두 동일하게 업데이트됨
  - WPF 창 최소화해도 웹 뷰어는 계속 동작

다음 Step 예고:
  MO-09에서는 AbstractDetector를 상속한 커스텀 감지기 예제를 추가합니다.
```

---

### MO-09: 커스텀 감지기 확장 예제

**추가 파일:**
```
Detectors/RateOfChangeDetector.cs  ← 변화율 감지 (AbstractDetector 상속)
Detectors/SpikeDetector.cs         ← 스파이크 이상값 감지
```

**📖 사용 설명:**
```
커스텀 감지기 추가 절차:
  1. AbstractDetector 상속 클래스 작성
  2. App.xaml.cs에서 MonitorEngine.Instance.Register(new MyDetector())
  3. Monitor 실행 → 새 감지 로직 즉시 적용

지원하는 감지 패턴 예시:
  - ThresholdDetector: HH/H/L/LL 절대값 임계
  - RateOfChangeDetector: 단위시간당 변화율 초과
  - SpikeDetector: 통계적 이상값 (평균±3σ)
  - (커스텀) 원하는 감지 로직 자유 구현 가능
```

---

## Step 상세 — IIoT.Manager

---

### Base-0 ~ M-04

**추가 파일 순서:**
```
Base-0: App.xaml/cs + MainWindow (빈 창 + 테마)
M-01:   Core/ProcessInfo.cs + ViewModels/ProcessViewModel.cs
        + Views/ProcessStatusView.xaml  ← Studio·Collector·Monitor 상태 카드 (3개)
M-02:   Core/ProcessManager.cs  ← Process.Start/Kill
M-03:   Core/HealthCheckService.cs  ← NamedPipe 핑/퐁
M-04:   LogViewer 통합
```

**관리 대상 프로그램:**
```
① IIoT.Studio.exe    — 설정 프로그램
② IIoT.Collector.exe — 수집 프로그램
③ IIoT.Monitor.exe   — 모니터링 프로그램
```

**✅ M-02 컴파일 확인 체크리스트:**
```
2단계: 런타임
  [ ] Manager 실행 → Studio·Collector·Monitor 상태 카드 3개 표시
  [ ] [시작] 버튼 클릭 → 각 .exe 프로세스 실행됨 확인 (작업 관리자)
  [ ] 상태 카드: 정지(회색) → 실행 중(녹색)으로 변경
  [ ] [정지] 버튼 클릭 → 프로세스 종료 확인
  [ ] 각 창을 직접 닫으면 → Manager 상태 카드 자동으로 정지로 변경
```

**📖 사용 설명:**
```
이번 Step에서 추가된 기능: 전체 시스템 통합 관리

화면 조작 방법:
  1. Manager 실행 → Studio·Collector·Monitor 상태 카드 표시
     - 🟢 실행 중 / 🔴 정지 / 🟡 오류
  2. [▶ 시작] 버튼 → 해당 프로그램 실행
  3. [⏹ 정지] 버튼 → 해당 프로그램 정상 종료
  4. [🔄 재시작] 버튼 → 정지 후 재시작

  M-03 추가 후:
  5. 각 프로그램의 응답 시간(ms) 표시
  6. 응답 없음 → 자동으로 오류(🟡) 상태 표시 + 재시작 시도

확인 포인트:
  - Manager 하나에서 전체 3개 프로그램 제어 가능
  - 권장 실행 순서: Studio → Collector → Monitor
  - Monitor가 없어도 Collector는 독립 동작 (MQTT 발행만 없어짐)
```

---

## 공통 규칙 (모든 Step에 적용)

### DI 필수 규칙
```csharp
// ❌ 절대 금지: Transient → 이중 창 버그
services.AddTransient<MainWindow>(...);
// ✅ 필수: Singleton
services.AddSingleton<MainWindow>(sp =>
    new MainWindow(sp.GetRequiredService<StudioMainViewModel>()));
```

### WPF 필수 규칙
```
① DynamicResource 필수 (StaticResource → 테마 전환 시 깨짐)
② Trigger Setter도 DynamicResource 필수
③ BoolToVisibility: Window/UserControl.Resources에 직접 등록
   (App.xaml 의존 금지 — 테마 전환 시 소멸 가능)
④ UserControl.Resources: <UserControl> 직계 자식 (Grid/Border 내부 절대 불가)
⑤ ScrollViewer 자식 2개 → Grid로 래핑 필수 (MC3089)
⑥ ComboBox → PropCombo 스타일 필수 (시스템 컬러 우회)
⑦ TextBlock 원시텍스트 + Run 혼용 금지 → StackPanel 분리
```

### C# 필수 규칙
```
① [NotifyPropertyChangedFor] 사용 (nameof() PropertyChanged 수동 구독 금지)
② file record 금지 → private sealed record (CS9051)
③ 단일 파일 내 namespace 1개만 (CS8954)
④ ConfigInitializer.EnsureConfigFiles(dir) — 파라미터 1개
⑤ CommandParameter="0"(string) → ViewModel int.TryParse 처리
⑥ TreeView.SelectedItem → 코드비하인드 vm.SelectNode() 호출
⑦ TargetFramework: net8.0-windows (lssLib.Messaging 참조 시 net8.0 불가)
```

### lssLib 필수 규칙
```
① EventBus → in-process 전용 (cross-process 절대 금지)
② AsyncScheduler → while-true 루프 대체 필수
③ CommandQueue → DB 저장 순서 보장에 사용
④ SDT SwingDoor 압축 → C-06부터 필수 적용
⑤ cross-process 이벤트 → MQTT (MQTTnet) 사용 (EventBus 대체 불가)
```

### 파일 헤더 & 섹션 구분자
```csharp
// ══════════════════════════════════════════════════════════
//  [프로젝트명] · [파일명].cs
//  역할: [한 줄 역할 설명]
//  생성: YYYY-MM-DD
// ══════════════════════════════════════════════════════════

public class ExampleClass {
    // §1 ─ 필드 ──────────────────────────────────────────────
    // §2 ─ 생성자 ─────────────────────────────────────────────
    // §3 ─ 공개 메서드 ────────────────────────────────────────
    // §4 ─ 내부 메서드 ────────────────────────────────────────
}
```

---

## 현재 Step 진행 상황 판단 방법

새 대화 시작 시 Claude는 다음 순서로 현재 위치를 파악한다:

1. Git 최신 파일 목록 확인 → 어느 Step까지 완료됐는지 판단
2. 마지막 완료 Step의 **컴파일 확인 체크리스트** 충족 여부 확인
3. 다음 Step의 **추가 파일 목록**만 생성 (기존 파일 불필요 수정 금지)
4. 코드 생성 후 반드시 **[컴파일 확인 체크리스트]** + **[사용 설명]** 작성
5. 체크리스트 항목 모두 확인됐다는 응답 후 다음 Step 진행

---

## 오류 발생 시 대응 우선순위

```
1순위: 오류 코드 → 아래 빠른 참조 표에서 원인·해결책 확인
2순위: 해당 Step의 "핵심 패턴" 섹션 참조
3순위: 이전 Step 상태로 복구 (현재 Step 변경사항 롤백)
```

**오류 빠른 참조:**

| 오류 | 원인 | 해결 |
|------|------|------|
| `MC3089` ScrollViewer 자식 | ScrollViewer에 자식 2개 | Grid로 래핑 |
| `MC3015` UserControl.Resources | Border 안에 Resources 삽입 | UserControl 직계로 이동 |
| `CS9051` file record | file 한정자를 public 멤버에 사용 | private sealed record |
| `CS8954` namespace 2개 | 단일 파일에 namespace 2번 | namespace 1개로 통합 |
| `CS1061` MouseMiddleButton | Border에 이벤트 없음 | PreviewMouseDown + ChangedButton |
| `CS1061` TreeView_SelectedItemChanged | cs 파일에 메서드 누락 | 코드비하인드에 메서드 추가 |
| `CS1061` SaveAsync/LoadAsync | ViewModel에 메서드 없음 | MainViewModel 자체 처리 |
| `ArgumentException` CommandParameter | string→int 직접 캐스팅 불가 | SwitchTab(string) + TryParse |
| `XamlParseException` BoolToVisibility | App.xaml 리소스 소멸 | Window/UserControl.Resources 직접 등록 |
| `NU1201` TFM 불일치 | net8.0 → net8.0-windows 참조 | TargetFramework net8.0-windows |
| `CS0246` 타입 없음 | using 또는 파일 누락 | using 추가 또는 파일 생성 |
| `XamlParseException` 일반 | XAML 파싱 오류 | 오류 메시지의 줄 번호 확인 |

---

## Ver History

| 버전 | 날짜 | 변경 내용 |
|------|------|-----------|
| v1.0 | — | 초기 4개 프로그램 구성 |
| v2.0 | 2026-06-10 | B안 확정: ConfigApp·CollectorRuntime·Monitor 3+3 구조 |
| v2.3 | 2026-06-10 | lssLib 확정 API·WPF 패턴·버그 수정 이력 추가 |
| v3.0 | 2026-06-13 | 구조 단순화 확정 (6→3개 통합) |
| v4.0 | 2026-06-14 | 증분 개발(Base-First) 방식 전면 재편, Step 맵 정의 |
| v4.1 | 2026-06-14 | 매 Step 컴파일 확인 체크리스트 추가 |
| | | 매 Step 사용 설명(조작 방법·확인 포인트·예고) 추가 |
| | | Claude 응답 형식 표준화 (코드+체크리스트+사용설명 3세트) |
| **v4.2** | **2026-06-15** | **IIoT.Monitor 독립 프로그램으로 분리 (3개→4개 구조)** |
| | | **Collector: 순수 수집 전담 (감지·알람 제거, MQTT 발행 추가)** |
| | | **Monitor: WPF+ASP.NET 혼합, AbstractDetector 확장, SignalR 웹 뷰어** |
| | | **cross-process 이벤트 채널: MQTT (Collector→Monitor)** |
| | | **Manager: 관리 대상 3개로 확장 (Studio·Collector·Monitor)** |
| | | **시스템 구조도·Step 맵·lssLib 규칙 업데이트** |
