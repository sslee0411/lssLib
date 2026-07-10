# IIoT.Solution 개발 핸드오프 파일
**작성일: 2026-07-09 | 버전: v9.1 | 현재 위치: IIoT.Manager MG-05 (빌드 확인 대기)**

---

## 📌 프로젝트 요구사항 원문

프로그램은 설정, 수집, 모니터링, 매니저(+ 후속 HMI/Sequence)로 구성

### 1. 설정 프로그램 (IIoT.Studio) → **✅ 100% 완료**
```
1-1. JSON 기반 자료형·데이터 구조·통신구조 설정
1-2. NodeRed 형식 설정 캔버스
1-3. Node = 추상Class 상속 구조
1-4. 스케일 설정 및 관리 포함
1-5. 장비 구조(그룹·장비·PLC) 트리 관리
```

### 2. 수집 프로그램 (IIoT.Collector) → **✅ 100% 완료**
```
2-1. 설정 파일 업데이트 후 자동 재시작
2-2. 데이터 흐름 시각화
2-3. 전체 수집 데이터 관리
2-4. 스케일 설정 및 관리 포함
```

### 3. 모니터링 프로그램 (IIoT.Monitor) → **✅ Step 맵 + 실무강화 전체 완료**
```
3-1. 수집 데이터 기반 이상작업 진행
3-2. 상속·확장으로 커스텀 가능한 구조
3-3. 로컬(C#)+웹 양쪽에서 보이는 확장성
```

### 4. 매니저 프로그램 (IIoT.Manager) → **⭕ 미착수 (다음 시작점)**
```
4-1. 각 프로그램 전체 관리
4-2. 세부: 실행/종료/재시작 제어, 상태 모니터링, 헬스체크,
     로그뷰어 통합, 요약 대시보드, 자동복구, 설정배포, 스케줄관리
```

> ⚠️ **주의**: 4-2-7(설정배포관리)/4-2-8(스케줄관리)은 아직 SKILL.md Step 맵에
> 세부 Step으로 미등록 — Manager 착수 시 MG-06/MG-07로 신규 추가 필요.

### 5. 생산현황판 (IIoT.HMI, 신규 확정) → **⭕ Manager 완료 후 착수**
```
모터·컨베이어 등 장비가 Tag/SignalR/DB 연동으로 애니메이션 표시되고
화면에서 직접 제어(ForceWrite) 가능한 생산현황판. 별도 프로그램으로 분리 확정.
```

### 6. 시퀀스 제어 (IIoT.Sequence) → **⭕ HMI 이후 착수**

### 7. Studio 보류 4개 항목 → **⭕ IIoT.Sequence 완료 후 착수**
```
가상 Tag · N포트 노드 · Function노드 · 프로토콜 편집
```

**★ 전체 착수 순서: Manager → HMI → Sequence → Studio 보류 4개 항목**

---

## 🗂 솔루션 구조

```
D:\lssLib\IIoT\IIoT.Solution\
├── Contracts\IIoT.Contracts\          ← 플러그인 계약 레이어
├── Plugins\ (ModbusTcp / Mitsubishi / Virtual)
├── Studio\IIoT.Studio\                ← ✅ 100%
├── Collector\IIoT.Collector\          ← ✅ 100%
├── Monitor\IIoT.Monitor\              ← ✅ 100% (Step맵 + 실무강화)
├── Manager\IIoT.Manager\              ← ⭕ 미착수 (다음 시작점 — 폴더 자체 미생성 확인됨)
├── HMI\IIoT.HMI\                      ← ⭕ Manager 이후 (별도 프로그램 확정)
├── Sequence\IIoT.Sequence\            ← ⭕ HMI 이후
└── UI\Themes\IIoT.UI.Themes\          ← WPF 공통 테마 (7개 테마: DarkNavy/
                                          NeonCyber/WarmAmber/ArcticFrost/
                                          TerminalGreen/CarbonElite/SteelLight)
```

**★ 솔루션 파일 구조 (2026-07-09 로컬 소스 실측 확인)**
```
프로그램별 개별 .sln 구조:
  Studio\IIoT.Studio.sln / Collector\IIoT.Collector.sln / Monitor\IIoT.Monitor.sln
  Contracts\IIoT.Contracts.sln / Plugins\IIoT.Driver.sln
  UI\Themes\IIoT.UI.Themes.sln / UI\IIoT.UI.Controls\Iiot.Controls.sln
루트 IIoT.Solution.sln: UI.Themes + Iiot.Controls + lssLib 계열(18 프로젝트)
→ Manager 착수 시 Manager\IIoT.Manager.sln + Manager\IIoT.Manager\IIoT.Manager.csproj
  형태로 동일 패턴 신규 생성
```

---

## 📊 전체 진행률

```
공통 기반              ████████████ 100%
IIoT.Studio            ████████████ 100%  (보류 4건은 Sequence 이후)
IIoT.Collector         ████████████ 100%  (C-EX-11만 후속 보류)
IIoT.Monitor           ████████████ 100%  (Step맵 + MN-EX 실무강화 8건 전부)
IIoT.Manager           ░░░░░░░░░░░░   0%  ← 다음 세션 시작점
IIoT.HMI               ░░░░░░░░░░░░   0%  (Manager 이후)
IIoT.Sequence           ░░░░░░░░░░░░   0%  (HMI 이후)
```

---

## ✅ IIoT.Monitor 완료 내역 상세

### Step 맵 (MN-Base-0 ~ MN-06)
```
MN-Base-0  빈 WPF + ASP.NET Core FrameworkReference 선반영 + 테마
MN-Base-1  메인 레이아웃 (헤더+탭바+본문)
MN-01      Collector 등록 관리 (monitor.json Collectors[])
MN-01B     다중 HubConnection 연결 관리자 + CollectorId 자동 동기화
MN-02      실시간 Tag 현황 (Collector→PLC 계층 그룹핑)
MN-02B     UI 디자인 개선(B스타일: 카드·필배지) + [대시보드] 탭(D스타일) 신규
MN-03      알람 현황 (Collector별 구분 + ACK)
MN-04      AbstractDetector 커스텀 확장 패턴 (Detector/Responder 분리 설계)
MN-05      Monitor 자체 SignalR Hub 내장 (웹 브라우저 연동, 포트 7879)
MN-06      실시간 차트 (Collector·PLC·Tag 필터 + OxyPlot)
```

### 실무강화 (MN-EX-01 ~ 08, 전체 완료)
```
MN-EX-01  알람 사운드 + Windows 트레이 알림 (TrayNotificationService)
MN-EX-02  알람 이력 SQLite 저장 (AlarmHistoryService, 90일 보존)
MN-EX-03  트레이 상주 + 최소화 (최소화 시 작업표시줄 숨김)
MN-EX-04  연결상태 요약 배지 (탭바 우측 고정 "Collector N/M 연결됨")
MN-EX-05  Tag 즐겨찾기/핀 고정 (⭐, monitor.json 영구 저장)
MN-EX-06  알람 필터/검색 (Collector·레벨·상태 + 자유검색)
MN-EX-07  현재값 스냅샷 CSV 내보내기
MN-EX-08  재연결 알림 억제 (Reconnecting 최대 4회 중 1회만 알림)
```

### 최종 UI 구조
```
헤더(56px): 타이틀 + ThemePickerButton
탭바(48px): [태그현황][알람][Collector관리][대시보드][차트][📋 로그] 좌측정렬
            + "Collector N/M 연결됨" 배지 우측정렬
하단 로그 패널: "📋 로그" 토글 시 GridSplitter로 크기조절 가능한 고정 패널
              (모든 탭에서 함께 열어둘 수 있음 — 탭 아님)
콘텐츠 탭 인덱스: 0=태그현황,1=알람,2=Collector관리,3=대시보드,4=차트
```

---

## 🏗 IIoT.Monitor 핵심 아키텍처

### 프로젝트 구조
```
Monitor/IIoT.Monitor/
├── App.xaml(.cs)              ← DI 컨테이너 구성, 시작/종료 처리
├── MainWindow.xaml(.cs)        ← 탭 호스트, 트레이 최소화 연동
├── Core/
│   ├── Config/                 MonitorSettings.cs, MonitorSettingsLoader
│   ├── Connection/              CollectorConnection.cs, CollectorConnectionManager.cs
│   ├── Aggregation/             LiveTagAggregator.cs, AlarmAggregator.cs
│   ├── Detection/                AbstractDetector.cs, DetectorHost.cs (+Detectors/Responders)
│   ├── Converters/               UiConverters.cs (탭 필배지, 상태색상, 즐겨찾기 아이콘)
│   ├── Notification/             TrayNotificationService.cs
│   ├── Storage/                  AlarmHistoryService.cs (SQLite)
│   ├── Favorites/                FavoriteTagService.cs
│   └── Export/                   SnapshotCsvExportService.cs
├── SignalR/                     MonitorHub.cs, MonitorHostService.cs (자체 웹 Hub)
├── Models/                      CollectorEndpoint, LiveTagRow, AlarmRow, DeviceSnapshotDto
├── ViewModels/                  각 탭 ViewModel + MonitorMainViewModel
└── Views/                       CollectorManage, LiveTag, Alarm, Dashboard, Chart, Log
```

### DI 등록 순서 (App.xaml.cs `_ConfigureServices`)
```
FavoriteTagService → SnapshotCsvExportService → LiveTagAggregator
→ AlarmAggregator → DetectorHost → TrayNotificationService
→ AlarmHistoryService → CollectorConnectionManager → MonitorSettingsLoader
→ CollectorManageViewModel/View → MonitorMainViewModel → DashboardViewModel/View
→ MonitorHostService → ChartViewModel/View → LogPanelView → MainWindow
```
※ .NET DI는 지연 해석이라 등록 순서 자체는 무관하지만, 의존관계 주석은 유지 중.

### 초기화 흐름
```
App.OnStartup():
  ① 테마 로드 ② LogManager.Start() ③ DI 빌드
  ④ DetectorHost 예시 등록(RateOfChangeDetector+LogResponder)
  ⑤ TrayNotificationService.Initialize() + AlarmAggregator 이벤트 연결
  ⑥ MainWindow.Show()

MainWindow.Loaded (비동기):
  ① AlarmHistoryService.InitializeAsync() (SQLite 오픈)
  ② MonitorHostService.StartAsync() (자체 웹 Hub 기동, 포트 7879)

CollectorManageView.Loaded:
  → CollectorManageViewModel.InitializeAsync()
    → monitor.json 로드 → CollectorConnectionManager.SyncFromEndpointsAsync()
    → 각 Collector 별 CollectorConnection.StartAsync()
      (REST CollectorId 자동동기화 → SignalR Hub 접속 → TagValue/AlarmChanged 구독)

App.OnExit():
  ① CollectorConnectionManager.DisposeAsync() (5초 타임아웃)
  ② MonitorHostService.DisposeAsync() (5초 타임아웃)
  ③ TrayNotificationService.Dispose()
  ④ AlarmHistoryService.DisposeAsync() (5초 타임아웃)
```

### monitor.json 스키마
```json
{
  "Collectors": [
    { "Id": "...", "Name": "...", "Host": "localhost", "Port": 7878, "Enabled": true }
  ],
  "FavoriteTagKeys": ["collectorId:plcId:tagId", "..."],
  "Web": { "Enabled": true, "Port": 7879 }
}
```

---

## 🐞 누적 버그 수정 이력 (Monitor 세션, 총 13건)

| # | 오류 | 원인 | 수정 |
|---|---|---|---|
| 1 | 빌드 중단 | FrameworkReference + SignalR.Client 버전 충돌 | FrameworkReference 임시 제거 → MN-05 시점 재도입 |
| 2 | 패키지 다운그레이드 | Microsoft.Extensions.DependencyInjection 8.0.0 고정이 낮음 | 8.0.1 상향, 이후 명시버전 완전 제거 |
| 3 | CS0246 HttpClient | using System.Net.Http 누락 | using 추가 |
| 4 | 테마 미적용 | DataGrid에 Style="{DynamicResource IIoTGrid}" 누락 | 적용 + RowStyle |
| 5 | 입력 화면 편집 불가 | IIoTGrid 스타일 자체 IsReadOnly=True 기본값 | 로컬 IsReadOnly="False" 재정의 |
| 6 | 연결 안 됨(1) | Host/Port 수정해도 기존 HubConnection URL 고정 | StartedHubUrl 비교 후 재생성 |
| 7 | 연결 안 됨(2) | 중복 CollectorId → ToDictionary 예외로 전체 동기화 조용히 중단 | GroupBy 방어 + 오류 목록 반환·노출 |
| 8 | XamlParseException | GroupStyle.HeaderTemplate {Binding Name}이 TwoWay 시도 (CollectionViewGroupInternal 읽기전용) | Mode=OneWay 명시 |
| 9 | 버튼 테마 깨짐 | 비활성(IsEnabled=False) 시각 처리 없음 | DangerBtn/SecondaryBtn 정식 스타일 적용 |
| 10 | 디버깅 종료 안 됨(1) | CollectorConnectionManager 미정리 | OnExit에 DisposeAsync 추가 |
| 11 | 디버깅 종료 안 됨(2, 근본원인) | OnExit 블로킹 대기 + Aggregator의 Dispatcher.Invoke(동기) 교착 | Dispatcher.BeginInvoke로 전환 + 5초 타임아웃 |
| 12 | CS0104 다수 | UseWindowsForms=true가 System.Windows.Forms/System.Drawing을 전역 using으로 추가 | Forms는 `<Using Remove>`, Drawing 충돌은 using 별칭으로 해결 |
| 13 | CS1061/CS0103 | using Microsoft.AspNetCore.Hosting/Http 누락 (UseUrls/Results) | using 추가 |

---

## 📐 WPF 핵심 규칙 (Monitor 세션 신규 확정분)

```
DataGrid.IsReadOnly는 컬럼별 IsReadOnly보다 항상 우선 → 입력 화면은 반드시
  로컬 IsReadOnly="False" 명시 (IIoTGrid 스타일 기본값 True 주의)
GroupStyle.HeaderTemplate에서 그룹 Name 바인딩은 반드시 Mode=OneWay
백그라운드 스레드 → UI 마샬링은 Dispatcher.BeginInvoke 사용 (Invoke 금지 —
  OnExit 블로킹 대기와 맞물려 교착상태 유발)
UseWindowsForms=true 사용 시 `<Using Remove="System.Windows.Forms" />` 필수
  (System.Drawing 충돌은 파일 단위 using 별칭으로 대응)
DI 리소스(HubConnection/HttpClient/DB연결 등)를 보유한 싱글턴은 반드시
  App.OnExit()에 Dispose 호출 세트로 등록 (등록만 하고 정리 누락 주의)
async 커맨드/동기화 메서드는 반드시 try/catch로 감싸고 오류를 로그+UI
  상태 텍스트에 노출 (조용히 삼켜지면 디버깅 불가)
```

---

## 🛠 세션 운영 규칙 (2026-07-09 확정)

```
① 코드 납품: 부분 수정 목록 금지 → 전체 파일 최종본을 실제 소스 경로
   (D:\lssLib\IIoT\IIoT.Solution\...)에 직접 저장 + 경로 명시
② 기존 파일 수정 시 변경 없는 부분(주석·참조 포함)은 그대로 유지
③ 매 Step 완료 시 [컴파일 확인 체크리스트] + [사용 설명] 제공
④ 판단 필요 사항은 구현 전 사용자 확인
⑤ 세션 종료 전 이 핸드오프 파일(D:\lssLib\IIoT_HANDOFF.md) 갱신 + Git 커밋 권장
   → 세션이 닫혀도 소스+진행상황 손실 없음 (세션 컨텍스트는 소멸됨)
⑥ 응답 마지막에 "✅ 작업 완료" 표시
⑦ 파일 삭제가 필요한 경우: 삭제 원인을 먼저 설명하고 사용자 허락을
   받은 후에만 진행 (임의 삭제 금지 — 2026-07-09 사용자 지시)
```

> ⚠️ **SKILL.md 버전 참고**: 솔루션 루트의 `IIoT.Solution\SKILL.md`(v4.x대)와
> Claude 스킬 캐시본(v6.3)은 모두 구버전. 최신 이력(v7.x~)은 스킬 설정에
> 업로드된 원본 기준이므로, 진행 상태 판단은 **이 핸드오프 파일을 최우선**으로 할 것.

---

## 🔧 2차 정리 예정 항목 (전체 업데이트 단계에서 일괄 수정)

> 개별 Step 진행 중에는 건드리지 않고, Manager~Sequence 완료 후
> **전체 정리(리팩터링) 단계**에서 일괄 진행할 항목 목록.

```
① Monitor MonitorMainViewModel.cs 위치 정렬
   현재: Monitor\IIoT.Monitor\ViewModels\MonitorMainViewModel.cs (규칙 예외)
   목표: 프로젝트 루트로 이동 + namespace IIoT.Monitor
   규칙: "메인 ViewModel 루트 레벨 고정" — Studio·Collector·Manager 는 준수 중
   보류 사유: Monitor 는 완료 프로그램 — 진행 중 회귀 위험 방지
   수정 범위: 파일 이동 + namespace 변경 + App.xaml.cs/MainWindow.xaml.cs using 정리
② (추가 발견 시 여기에 누적)
```

---

## 🔜 다음 세션 시작 지점

**IIoT.Manager — MG-Base-0부터 신규 착수**
(Manager 폴더 미생성 상태 확인됨 — 2026-07-09 로컬 소스 실측)

```
예정 작업 순서:
  MG-Base-0  빈 WPF + 테마                    ← 🔄 코드 생성 완료 (2026-07-09, 빌드 확인 대기)
             생성 파일 6개:
             Manager\IIoT.Manager.sln
             Manager\IIoT.Manager\IIoT.Manager.csproj
             Manager\IIoT.Manager\AssemblyInfo.cs
             Manager\IIoT.Manager\App.xaml(.cs)
             Manager\IIoT.Manager\MainWindow.xaml(.cs)
  MG-01      프로세스 상태 표시 (Studio·Collector·Monitor)  ← 🔄 코드 생성 완료 (빌드 확인 대기)
             신규: Models\ManagedProcessInfo.cs / ViewModels\ProcessCardViewModel.cs
                  ViewModels\ManagerMainViewModel.cs / Views\ProcessStatus\ProcessStatusView.xaml(.cs)
             수정: MainWindow.xaml(.cs) — ProcessStatusHost + 하단 상태바 / App.xaml.cs — DI 등록
             구조: 2초 DispatcherTimer → ProcessCardViewModel.Refresh()
                  (Process.GetProcessesByName, 상태점 Green/Red/Yellow DataTrigger)
  MG-02      Start/Stop → 프로세스 제어  ← 🔄 코드 생성 완료 (빌드 확인 대기)
             신규: Core\Config\ManagerSettings.cs (manager.json DTO+로더 — monitor.json 패턴)
                  Core\ProcessManager.cs (Start / StopAsync[정상종료→5초→Kill] / RestartAsync)
             수정: Models\ManagedProcessInfo.cs (ExePath 추가, json DTO 겸용)
                  ViewModels\ProcessCardViewModel.cs (커맨드 3종 + IsBusy + LastError,
                    규칙⑬ NotifyCanExecuteChangedFor 적용)
                  ViewModels\ManagerMainViewModel.cs (설정 로드 InitializeAsync — Loaded 호출)
                  Views\ProcessStatus\ProcessStatusView.xaml (SuccessBtn/DangerBtn/SecondaryBtn)
                  MainWindow.xaml.cs / App.xaml.cs
             설정: Config\manager.json — Processes[] { Id,Name,Description,ProcessName,ExePath }
                  ExePath 상대경로 = Manager 실행폴더 기준 (기본값: 각 프로그램 Debug 출력)
  MG-03      NamedPipe 헬스체크  ← 🔄 코드 생성 완료 (빌드 확인 대기, B안 확정)
             프로토콜: 파이프 "IIoT.Health.{ProcessName}" / "ping" → "pong|{상태문구}"
             신규: Contracts\Health\HealthPipeServer.cs (의존성 없음 — onLog 콜백)
                  Manager Core\HealthCheckService.cs (핑 클라이언트, 1초 한도, ms 측정)
             Manager 수정: ManagedProcessInfo(AutoRestart 추가) / ProcessCardViewModel
                  (RefreshAsync 전환: 프로세스검사→핑→연속3회 실패+AutoRestart 시 자동재시작,
                   응답없음 상태에서도 정지/재시작 가능) / ManagerMainViewModel(재진입 가드)
                  / ProcessStatusView.xaml(응답시간·상태문구) / App.xaml.cs / csproj·sln(Contracts)
             ★ 대상 3개 프로그램 수정 (재빌드 필수):
                  Studio App.xaml.cs / Collector App.xaml.cs (pong에 FlowEngine.IsRunning)
                  / Monitor App.xaml.cs + csproj·sln (Contracts 참조 신규)
             AutoRestart 기본 false — manager.json 에서 프로그램별 활성화
  MG-04      로그 뷰어 통합  ← 🔄 코드 생성 완료 (빌드 확인 대기)
             방식: 각 프로그램 {exe폴더}\Log\yyyy_MM\dd\All.txt 파일 테일링(1초 폴링,
                  핸들 미보관, 최초 발견 시 끝으로 이동 — 과거 이력 미출력)
             신규: Models\LogRow.cs / Core\LogTailService.cs
                  ViewModels\LogViewerViewModel.cs (최대 2000행, ICollectionView 필터)
                  Views\LogViewer\LogViewerView.xaml(.cs) (소스필터·검색·일시정지·지우기·자동스크롤)
             수정: MainWindow.xaml — 탭바 신설 [⚙프로세스][📋로그] (Visibility 토글,
                  TabBtn0/1 DataTrigger 필 스타일) / ManagerMainViewModel — ActiveTabIndex
                  + SwitchTab(규칙⑤ TryParse) + LogTail.Start() / MainWindow.xaml.cs / App.xaml.cs
             UI: 카드 폭 260→340 확대 (사용자 요청)
  MG-05      대시보드 (전체 요약)  ← 🔄 코드 생성 완료 (빌드 확인 대기)
             구성: 요약 칩 4개(전체/실행/응답없음/정지) + 프로그램 현황 미니 목록(상태점+ms)
                  + 최근 이벤트 이력(최대 200건) + 시스템 정보(가동시간·설정경로)
             신규: Models\EventRow.cs / Core\EventHistoryService.cs (Record → 로그에도 기록)
                  ViewModels\DashboardViewModel.cs (2초 집계 타이머 — Monitor 패턴)
                  Views\Dashboard\DashboardView.xaml(.cs)
             수정: ProcessCardViewModel — 이벤트 기록(수동 시작/정지/재시작, 자동복구,
                  상태변경 OnStateChanged partial) / ManagerMainViewModel — 탭 2 추가
                  / MainWindow.xaml(.cs) — [📊 대시보드] 탭 + DashboardHost / App.xaml.cs
             ※ MG-04 로그탭 UI 를 표준 LogPanelView 패턴으로 정렬 (사용자 요청):
               34px 툴바(GhostBtn·PropCombo·PropInput) + 시각/레벨/프로그램/Source/내용
               컬럼 + 레벨별 행 색상 + lssLib.Log TXT 라인 파서(LogRow.Parse)
               + 크기 롤링(All_2.txt…) 최신 파일 자동 추적
  MG-06(신규 추가 필요)  설정 배포 관리 (요구사항 4-2-7)
  MG-07(신규 추가 필요)  스케줄 관리 (요구사항 4-2-8)
```

Manager 완료 후 → **IIoT.HMI**(생산현황판, 별도 프로그램) →
**IIoT.Sequence** → **Studio 보류 4개 항목** 순으로 이어집니다.

---

## Ver History (요약 — 전체 이력은 SKILL.md 참조)
| 버전 범위 | 내용 |
|---|---|
| v6.7~v7.4 | Studio·Collector 완료 (이전 세션) |
| v7.5~v7.22 | Monitor MN-Base~MN-06 Step맵 전체 완료 + 각종 버그 수정 |
| v7.23~v7.38 | Monitor 실무강화 MN-EX-01~08 전체 완료, IIoT.HMI 신규 확정,
              Studio 보류 4건 착수 시점(Sequence 이후) 확정, 문서 정리 |
| v8.0 (2026-07-08) | 핸드오프 파일 재작성 — 다음 시작점 IIoT.Manager MG-Base-0 확정 |
| v8.1 (2026-07-09) | D:\lssLib 로컬 저장본 신규 생성. 로컬 소스 실측 검증 반영
              (프로그램별 개별 .sln 구조 / Manager 폴더 미생성 확인),
              세션 운영 규칙 ①~⑥ 신설, SKILL.md 버전 불일치 주의사항 추가 |
| v8.2 (2026-07-09) | MG-Base-0 코드 생성 완료 (6개 파일, 빌드 확인 대기).
              Monitor 패턴 준수: RootNamespace 명시 / StartupUri 없음 /
              AddSingleton MainWindow / FrameworkReference 미포함(버그 #1 교훈) /
              DI 패키지 8.0.1 명시(버그 #2 교훈) |
| v8.3 (2026-07-09) | MG-01 코드 생성 완료 — 프로세스 상태 카드 3종 + 2초 갱신 타이머.
              Process 객체 Dispose 처리, 오류 시 로그+카드 노출(조용히 삼키기 금지),
              DataTrigger 상태점 색상(DynamicResource), 하단 상태바 추가 |
| v8.4 (2026-07-09) | MG-02 코드 생성 완료 — manager.json + ProcessManager +
              시작/정지/재시작 버튼. 정지는 CloseMainWindow(정상종료, 대상 앱
              OnExit 정리 보장) → 5초 → Kill(트리포함). ProcessManager 는 핸들
              미보관(OnExit 정리 불필요, Manager 종료 후에도 대상 프로세스 유지) |
| v8.5 (2026-07-09) | MG-03 코드 생성 완료 — NamedPipe 헬스체크(B안).
              HealthPipeServer 를 Contracts\Health 에 신설(공용), 3개 프로그램
              탑재(구버전 빌드 실행 시 🟡 응답없음으로 표시됨 — 재빌드 필요).
              응답시간(ms)·내부상태 표시, 연속 3회 실패+AutoRestart 시 자동복구 |
| v8.6 (2026-07-09) | ManagerMainViewModel 을 ViewModels\ → 프로젝트 루트로 이동
              + namespace IIoT.Manager (규칙 "메인 VM 루트 레벨 고정" 정렬 —
              Studio·Collector 준수 / Monitor 만 ViewModels 하위 예외로 유지).
              App.xaml.cs·MainWindow.xaml.cs using 정리 |
| v8.7 (2026-07-09) | "🔧 2차 정리 예정 항목" 섹션 신설 — Monitor 메인 VM 위치
              정렬을 전체 정리 단계 항목 ①로 등록 (개별 Step 중 미수정 방침) |
| v8.8 (2026-07-09) | MG-03 빌드 오류 수정 — HealthPipeServer.cs CS0246
              (StreamReader/Writer): using System.IO 누락 추가 (버그 #3 동일
              패턴). Manager CS0006 은 Contracts 실패 연쇄 — 자동 해소.
              운영 규칙 ⑦ 신설: 파일 삭제는 원인 설명 + 사용자 허락 후 진행 |
| v8.9 (2026-07-09) | HealthCheckService.cs 동일 CS0246 수정 + 신규 파일 전수
              점검(누락 0건). ★ 규칙 확정: 이 솔루션의 WPF(net8.0-windows)
              프로젝트에서 StreamReader/Writer·File·Path·Directory 사용 파일은
              ImplicitUsings 에 의존하지 말고 반드시 "using System.IO;" 명시 |
| v9.0 (2026-07-09) | MG-04 코드 생성 완료 — 통합 로그 뷰어 (파일 테일링 방식,
              LogTailService 1초 폴링·핸들 미보관). Manager 에 탭바 신설
              (프로세스/로그, Visibility 토글로 숨김 탭도 수신 지속).
              카드 폭 340 확대. ★ 운영 규칙 추가: 다음 Step 예고 시
              진행 내용(무엇을 만들지) 설명을 함께 제시 (사용자 요청) |
| v9.1 (2026-07-09) | ① 로그탭 표준 LogPanelView UI 정렬 (사용자 요청) — 툴바·
              컬럼·레벨색 동일, LogRow.Parse 라인 파서, 롤링 파일 추적
              ② MG-05 코드 생성 완료 — 대시보드 (요약 칩·프로그램 현황·
              최근 이벤트·시스템 정보, EventHistoryService 신설).
              SKILL.md 원안 Step 맵(MG-Base-0~05) 코드 작성 완료 —
              다음: 신규 MG-06(설정 배포)·MG-07(스케줄 관리) |

---

*다음 세션 시작 시 이 파일(D:\lssLib\IIoT_HANDOFF.md)을 먼저 읽고,
 IIoT.Manager MG-Base-0부터 진행할 것*
*SKILL.md는 항상 함께 참조하되, 진행 상태·착수 순서는 이 핸드오프 파일이 우선*
