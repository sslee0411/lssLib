# IIoT.Solution 개발 핸드오프 파일
**작성일: 2026-07-09 | 버전: v8.2 | 현재 위치: IIoT.Manager MG-Base-0 (빌드 확인 대기)**

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
```

> ⚠️ **SKILL.md 버전 참고**: 솔루션 루트의 `IIoT.Solution\SKILL.md`(v4.x대)와
> Claude 스킬 캐시본(v6.3)은 모두 구버전. 최신 이력(v7.x~)은 스킬 설정에
> 업로드된 원본 기준이므로, 진행 상태 판단은 **이 핸드오프 파일을 최우선**으로 할 것.

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
  MG-01      프로세스 상태 표시 (Studio·Collector·Monitor)
  MG-02      Start/Stop → 프로세스 제어
  MG-03      NamedPipe 헬스체크
  MG-04      로그 뷰어 통합
  MG-05      대시보드 (전체 요약)
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

---

*다음 세션 시작 시 이 파일(D:\lssLib\IIoT_HANDOFF.md)을 먼저 읽고,
 IIoT.Manager MG-Base-0부터 진행할 것*
*SKILL.md는 항상 함께 참조하되, 진행 상태·착수 순서는 이 핸드오프 파일이 우선*
