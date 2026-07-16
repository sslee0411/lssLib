# IIoT.Solution 개발 핸드오프 파일
**작성일: 2026-07-16 | 버전: v11.12 | 다음 세션 시작점: ① HM-07 빌드·런타임 확인 → ② HM-08 착수**

> 새 세션 시작 시 이 파일을 가장 먼저 읽을 것.
> SKILL.md 는 함께 참조하되, **진행 상태·착수 순서는 이 핸드오프가 최우선**
> (스킬 캐시본/솔루션 루트 SKILL.md 는 구버전 — v6.3/v4.x, HMI Step 맵 미반영).

---

## 📌 프로젝트 개요

C# .NET 8 / WPF / lssLib v5 기반 산업용 IIoT/SCADA 플랫폼.
위치: `D:\lssLib\IIoT\IIoT.Solution\` (프로그램별 개별 .sln 구조)

```
프로그램            상태
IIoT.Studio         ✅ 100% (설정 편집기 — 보류 4건은 Sequence 이후)
IIoT.Collector      ✅ 100% (수집+감지+저장, SignalR Hub 7878 — C-EX-13 빌드 확인 완료, C-EX-11 후속 보류)
IIoT.Monitor        ✅ 100% (실시간 모니터링, 자체 Hub 7879, MN-EX 8건 전부)
IIoT.Manager        ✅ 100% (코드+통합 빌드+런타임 확인 완료 — 2026-07-16)
IIoT.HMI            🔄 Base-0~2 + HM-01~06 빌드 확인 완료, HM-07 코드완료(빌드대기) (생산현황판)
IIoT.Sequence       ⭕ HMI 이후
공통: Contracts(플러그인 계약+Health) · Plugins(ModbusTcp/Mitsubishi/Virtual)
     · UI.Themes(7테마) · UI.Controls
★ 전체 개발 순서 (2단계 확정 — 2026-07-16, 사용자 확인):
  [1차: 기본구조 확립] Manager(완료) → HMI(진행중) → Sequence
    → 5개 프로그램 모두 "기본 골격 + 핵심 기능"을 우선 갖춘다(보류/강화 항목은 뒤로 미룸)
  [2차: 강화 순환] 1차로 갖춰진 전체 시스템(Studio~Sequence 연동 구조)을 바탕으로
    Studio 솔루션으로 회귀하여 보류 4건(가상Tag·N포트·Function노드·프로토콜편집) 등을
    강화 → 이후 전체 시스템을 다시 순환하며 필요한 프로그램을 강화하는 방식으로 진행
    (Studio 강화 시 HMI/Sequence 실사용 경험이 반영되어 설계가 더 정확해지는 순서상 이점)
```

---

## ✅ IIoT.Manager 완료 내역 (통합 빌드·런타임 확인 완료 — 2026-07-16)

경로: `Manager\IIoT.Manager.sln` + `Manager\IIoT.Manager\`
참조: lssLib.Log · lssLib.DB(+Sqlite) · IIoT.UI.Themes · IIoT.Contracts
패키지: CommunityToolkit.Mvvm 8.4.2 / DI 8.0.1 / OxyPlot.Wpf 2.2.0 / UseWindowsForms

### Step 맵
```
MG-Base-0  빈 WPF + 테마                              ✅ 빌드 확인 완료
MG-01   프로세스 상태 카드 (2초 감지, 이름 기반)       ✅ 빌드 확인 완료
MG-02   Start/Stop/재시작 + manager.json              ✅ 빌드 확인 완료
        (정지: CloseMainWindow → 5초 → Kill 트리)
MG-03   NamedPipe 헬스체크 (B안)                      ✅ 빌드 확인 완료
        파이프 "IIoT.Health.{ProcessName}" / "ping"→"pong|{상태}"
        HealthPipeServer 는 Contracts\Health\ (공용) — Studio·Collector·
        Monitor 3개 App.xaml.cs 에 탑재됨
        AutoRestart: 연속 3회 실패 시 자동 재시작 (기본 false)
MG-04   통합 로그 뷰어                                 ✅ 빌드 확인 완료
        {exe}\Log\yyyy_MM\dd\All*.txt 테일링(1초, 핸들 미보관, 롤링 추적)
        표준 LogPanelView UI (시각/레벨/프로그램/Source/내용 + 레벨색)
MG-05   대시보드 (요약칩·프로그램현황·최근이벤트·시스템정보) ✅ 빌드 확인 완료
MG-06   설정 배포 (요구사항 4-2-7)                     ✅ 빌드 확인 완료
        소스(Studio Config) → 대상 Config: 백업→복사→.signal 발행
MG-07   스케줄 관리 (요구사항 4-2-8)                   ✅ 빌드 확인 완료
        요일+HH:mm 자동 시작/정지/재시작, 30초 검사, 중복실행 방지
        ※ Stop 스케줄 + AutoRestart=true 조합 주의 (헬스체크가 되살림)
```

### MG-EX 실무강화 (12/12 빌드 확인 완료)
```
A그룹(안정 운영) — 전체 완료 ✅ 빌드 확인 완료
 EX-01 트레이 상주+최소화  EX-02 경고 이벤트 알림(사운드+풍선)
 EX-03 Windows 자동실행(HKCU Run)+AutoStart 순차 기동(지연 옵션)
 EX-04 이벤트 이력 SQLite (Data\manager.db, 90일 보존, _WaitWithTimeout 정리)
B그룹(진단·가시성) — 전체 완료 ✅ 빌드 확인 완료
 EX-05 CPU/메모리 모니터링(임계 경고 5분 쿨다운)  EX-06 응답시간 스파크라인(OxyPlot)
 EX-07 로그 과거일자 조회 + CSV 내보내기(UTF-8 BOM)
C그룹(배포 운영) — 전체 완료 ✅ 빌드 확인 완료
 EX-08 배포 롤백(백업 시점 콤보+직전상태 자동백업)  EX-09 배포 전 diff 비교
 EX-10 배포 후 자동 재시작 옵션
D그룹 — ⭕ 보류 (HMI/Sequence 이후): EX-11 웹 상태 페이지  EX-12 원격 관리
```

### 구조 요약
```
탭: [⚙프로세스][📋로그][📊대시보드][🚀배포][⏰스케줄] (인덱스 0~4, Visibility 토글)
메인 VM: ManagerMainViewModel — 프로젝트 루트 (규칙: 메인 VM 루트 고정)
Core\: ProcessManager·HealthCheckService·LogTailService·EventHistoryService·
      ScheduleService·ConfigDeployService·StartupRegistrationService·
      Config\ManagerSettings·Notification\TrayService·Storage\EventHistoryDbService
manager.json (Config\): Processes[]{Id,Name,Description,ProcessName,ExePath,
      AutoRestart,AutoStart,AutoStartDelaySec} + Deploy{SourceConfigDir,Files[]}
      + Schedules[]{Id,Enabled,ProcessId,Action,Time,Days[]} + Resource{Cpu/MemWarn}
경고 알림 규칙: 수동조작(IsBusy) 없는 "실행중→정지/응답없음" 전이·자동복구·
      스케줄/배포 실패·리소스 임계 초과 → Warning(트레이 풍선+사운드+DB)
```

---

## ✅ lssLib.SignalR 모듈 (빌드·런타임 확인 완료 — 2026-07-16)

경로: `D:\lssLib\Base\BCL\SignalR\` (Net_Ver5 구조 준용: 라이브러리+Demo+sln)
```
lssLib.SignalR (net8.0 + FrameworkReference AspNetCore — 전이됨):
  SignalRHostConfig/ClientConfig · SignalRHostService<THub>(Kestrel+MapHub,
  IHubContext 노출) · SignalRClientConnection(자동재연결 0/2/10/30초, 상태이벤트,
  On/Invoke 헬퍼) · BroadcastHub(토픽 Pub/Sub+Ping, 수신메서드 "Receive",
  TrafficLogged 정적 훅 — 서버측 트래픽 관찰용)
Demo: 1 셀프테스트(외부서버 불필요) / 2 서버만(트래픽 표시) / 3 클라이언트만 — 확인 완료
용도: HMI Tag 구독 기반(예정) · MG-EX-11 기반 · Collector/Monitor 공통화 후보(2차 정리)
```

---

## 🆕 IIoT.HMI — Step 맵 설계 (확정 — 2026-07-16)

### 설계 확정 사항 (사용자 확인 완료)
```
① ForceWrite 원격 제어: Collector에 Hub 메서드 추가 (C-EX-13 선행 Step, 아래 상세)
② 레이아웃 캔버스: Studio 캔버스 인프라 재사용 (프리폼 배치 — PortsLayer 제외,
   NodesLayer 방식만 이식하여 아이콘 자유 배치·드래그·줌/팬 구현)
③ Collector 연결 범위: Monitor와 동일하게 다중 Collector 지원
   (CollectorConnectionManager/CollectorConnection 그대로 이식 또는 공용화 검토)
```

### 실제 코드 기반 확인된 재사용 자산
```
Collector/IIoT.Collector/SignalR/IIoTHub.cs
  ← 현재 서버→클라 Push: "TagValue"/"AlarmChanged" (IIoTHubPusher)
  ← 현재 클라→서버 호출: AcknowledgeAlarm(alarmKey) 만 존재 (ForceWrite 없음)
Collector/IIoT.Collector/Core/Engine/ForceWriteService.cs (C-15)
  ← WriteAsync(plcId,tagId,value,apiKey) — 검증(Enabled/ApiKey/Tag존재/활성/형식)
    후 FlowEngine.WriteTagAsync() 위임 + AuditLogService 기록. 원격 Hub 메서드만
    추가하면 그대로 재사용 가능.
Collector GET /api/devices (C-EX-01-7)
  ← DeviceInstance/TagInstance 트리 스냅샷 (CollectorId·연결상태·Tag값·알람상태 포함)
Monitor/IIoT.Monitor/Core/Connection/CollectorConnection.cs · CollectorConnectionManager.cs (MN-01B)
  ← REST 스냅샷 1회 조회 + SignalR HubConnection 자동재연결 + TagValue/AlarmChanged
    구독 + AcknowledgeAsync() — HMI가 그대로 이식할 기반 코드
Studio 캔버스 인프라 (S-11~S-13B)
  ← CanvasNode/CanvasConnection/PortsLayer 절대좌표 배치 패턴 중 NodesLayer(카드
    배치·드래그·줌/팬) 부분만 재사용, 포트/연결선 로직은 HMI에 불필요(제외)
```

### C-EX-13: Collector ForceWrite Hub 메서드 추가 — ✅ 빌드 확인 완료 (2026-07-16)
```
변경 파일 (전체 최종본 반영 완료):
  Collector/IIoT.Collector/SignalR/IIoTHub.cs
    ← ForceWriteService 필드+생성자 주입 추가
    ← public Task<ForceWriteResult> ForceWrite(plcId, tagId, value, apiKey="")
      → _forceWriteService.WriteAsync() 위임 (AcknowledgeAlarm과 동일 패턴)
    ← IIoTHubPusher.PushForceWriteResultAsync(payload) 추가
      (클라이언트 JS: connection.on("ForceWriteResult", (data) => {...}))
  Collector/IIoT.Collector/SignalR/SignalRHostService.cs
    ← ForceWriteService 필드+생성자 주입 추가 (기존 App.xaml.cs 에 이미
      services.AddSingleton<ForceWriteService>() 등록되어 있어 DI 자동 주입됨)
    ← StartAsync() 에서 builder.Services.AddSingleton(_forceWriteService) 추가
      (AlarmStateManager 클로저 재사용 패턴과 동일 원칙)
  Collector/IIoT.Collector/SignalR/SignalRPushService.cs
    ← TagForceWriteEvent 구독 추가 (FlowEngine.WriteTagAsync 에서 이미 발행 중이던
      이벤트 — 로컬 UI 강제쓰기든 원격 Hub 호출이든 동일 경로로 자동 Push됨)
    ← 모든 연결 클라이언트(다른 HMI 화면·Monitor 등)에 "ForceWriteResult" Push

클라이언트 호출 예: await conn.InvokeAsync<ForceWriteResult>("ForceWrite", plcId, tagId, value, apiKey);

컴파일 확인 체크리스트:
  [ ] Collector 빌드 → 오류 0개
  [ ] Collector 자체 UI(StatusView ForceWriteDialog)는 기존과 동일하게 동작 (회귀 없음)
  [ ] SignalR Demo 또는 브라우저 콘솔에서 conn.invoke("ForceWrite", plcId, tagId, value, "")
      → ForceWriteResult{IsSuccess,Error} 정상 응답 확인
  [ ] 강제쓰기 발생 시 다른 연결 클라이언트에서 conn.on("ForceWriteResult", ...) 로
      Push 수신 확인 (설정된 apiKey 없을 시 "" 로 호출)
```

### IIoT.HMI Step 맵
```
━━━ 기반 구조 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
HM-Base-0  빈 WPF + 테마 (HMI\IIoT.HMI.sln 신규 — Manager/Studio 패턴 동일)  ✅ 빌드 확인 완료
HM-Base-1  메인 레이아웃 (헤더+탭바+본문)                                    ✅ 빌드 확인 완료
           탭: [현황판][레이아웃 편집][Collector 관리][알람][로그]
HM-Base-2  탭 전환 5개 (현황판·레이아웃 편집·알람·로그는 placeholder)        ✅ 빌드 확인 완료

━━━ Collector 연동 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
HM-01   CollectorConnection/CollectorConnectionManager 이식               ✅ 빌드 확인 완료
        (Monitor MN-01B 코드 기반 — 다중 Collector, REST 스냅샷+SignalR 구독.
         단, 집계기(LiveTagAggregator 등)는 아직 없음 — TagValueReceived/
         AlarmChanged 이벤트로만 재발행, HM-04/08에서 구독자 추가 예정)
HM-02   Collector 관리 탭 (등록/편집/삭제 — Monitor CollectorManage 패턴)   ✅ 빌드 확인 완료

━━━ 레이아웃 캔버스 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
HM-03   레이아웃 캔버스 기반 구조 (Studio NodesLayer 이식 — 포트/연결선 제외) ✅ 빌드 확인 완료
        자유 배치·드래그·줌/팬 (그리드 스냅은 후속 검토 — 현재 자유좌표만 지원)
HM-04   장비 아이콘 팔레트 + 베이스 컨트롤 상속 구조 (모터/컨베이어/탱크/밸브)  ✅ 빌드 확인 완료
HM-05   아이콘 ↔ Tag 바인딩 (DeviceInstance/TagInstance 실시간 값 연결)      ✅ 빌드 확인 완료
HM-06   애니메이션 엔진 (회전=RawValue 비례, 색상=알람/연결상태, 흐름효과)    ✅ 빌드 확인 완료
HM-07   레이아웃 저장·불러오기 (hmi-layout.json, 다중 화면 페이지)          ✅ 코드완료(빌드대기)
        + Z-레벨 우선순위 지정(카드 겹침 순서, 사용자 요청 추가) 포함

━━━ 알람·제어 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
HM-08   알람 오버레이 (아이콘 배지 + 상세 팝업 + ACK — AcknowledgeAlarm 재사용) ⏳
HM-09   ForceWrite 제어 다이얼로그 (아이콘 더블클릭 → 값 입력 →              ⏳
        SignalR Invoke("ForceWrite") — C-EX-13 선행 필수)
HM-10   다중 화면 관리 (레이아웃 페이지 탭/트리)                             ⏳

━━━ 확장성 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
HM-11   웹 브라우저 표시 확장 (자체 SignalR Hub + wwwroot —                 ⏳
        Collector C-11/wwwroot 패턴 재사용, 로컬(C#)+웹 동시 지원 요구사항)
HM-12   보안 (ForceWrite API Key 입력 확인 — Collector Security.ForceWriteApiKey 재사용) ⏳

━━━ 후속 (HMI 1차 마감 후 검토) ━━━━━━━━━━━━━━━━━━━━━━━━━━━
HM-EX   히스토리 트렌드 오버레이 / 화면 캡처·PDF 리포트 / 다중 모니터 지원  ⭕ 보류
```

### 착수 순서
```
① C-EX-13 (Collector ForceWrite Hub 메서드 추가) — ✅ 완료
② HM-Base-0 → HM-Base-2 (빈 WPF + 레이아웃 + 탭 전환) — ✅ 코드 완료(빌드 대기)
③ HM-01~02 (Collector 연동) — ✅ 코드 완료(빌드 대기)
④ HM-03 (레이아웃 캔버스 기반 구조) — ✅ 코드 완료(빌드 대기)
④-1 HM-04 (장비 아이콘 팔레트 + DeviceControlBase 상속 구조) — ✅ 빌드 확인 완료
④-2 HM-05 (아이콘 ↔ Tag 바인딩) — ✅ 빌드 확인 완료
④-3 HM-06 (애니메이션 엔진) — ✅ 빌드 확인 완료
④-4 HM-07 (레이아웃 저장·불러오기 + Z-레벨 우선순위) — ✅ 코드 완료(빌드 대기)
⑤ HM-08~10 (알람+제어+다중화면) — 다음 착수 대상
⑥ HM-11~12 (웹 확장+보안) — Manager MG-EX-11(보류)과 함께 검토 가능
```

### HM-03 구현 내역 (코드 완료 — 2026-07-16, 빌드 확인 대기)

```
파일 목록 (전체 신규):
  Core/Layout/LayoutNode.cs       — AbstractLayoutNode(포트 없는 프리폼 노드 기반)
        + GenericIconNode(placeholder) + LayoutNodeFactory
        (Studio AbstractCanvasNode 에서 InputPorts/OutputPorts 를 제외한 버전)
  Core/Layout/HexColorConverter.cs — "#RRGGBB" 문자열 → Color 변환기 (Studio 이식)
  ViewModels/LayoutCanvasViewModel.cs — Nodes 컬렉션 + 선택/삭제 + 줌·패닝
        (Studio CanvasViewModel 에서 AddConnection/RefreshConnections/드래그
         미리보기 등 포트·연결선 관련 로직 전부 제외)
  Views/LayoutCanvas/LayoutCanvasView.xaml(.cs) — 좌측 팔레트 + 우측 캔버스
        (Studio CanvasView 에서 Layer1 ConnectionsLayer· Layer2 DragPreviewPath·
         Layer4 PortsLayer 를 전부 제외하고 Layer3 NodesLayer 만 이식)

MainWindow: 탭1(레이아웃 편집) placeholder → LayoutCanvasHost(ContentControl)로 교체
App.xaml.cs: LayoutCanvasViewModel/LayoutCanvasView DI 등록 추가 (hmi.json 의존성 없음)

★ 범위 결정: 팔레트에는 현재 "🔷 아이콘"(GenericIconNode) 1종류만 존재 —
  모터·컨베이어·탱크·밸브 등 실제 장비 아이콘과 Tag 값 바인딩은 HM-04/HM-05에서 추가.
  그리드 스냅은 미구현(자유좌표 배치만 지원) — 필요 시 후속 검토.

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개

### 2단계: 런타임
  [ ] [🎨 레이아웃 편집] 탭 클릭 → 좌측 팔레트 + 우측 빈 캔버스 표시
  [ ] 팔레트 "🔷 아이콘" 클릭 → 캔버스에 아이콘 카드 추가됨
  [ ] 카드 드래그 → 자유롭게 이동 확인
  [ ] 카드 클릭 → 선택 테두리(강조색) 표시 + 툴바 "🗑 선택 삭제" 버튼 활성화
  [ ] 마우스 휠 → 캔버스 확대/축소 (툴바 % 표시 갱신)
  [ ] Space 키 누른 채 드래그(또는 마우스 가운데버튼 드래그) → 캔버스 패닝
  [ ] 🔍＋/🔍－/⟳ 초기화 버튼 정상 동작
  [ ] Delete 키 → 선택된 카드 삭제

## 📖 사용 설명

화면 조작 방법:
  1. [🎨 레이아웃 편집] 탭 클릭
  2. 좌측 팔레트에서 "🔷 아이콘" 클릭 → 캔버스에 카드 추가 (자동 배치)
  3. 카드를 마우스로 드래그해 원하는 위치로 이동
  4. 휠 스크롤로 확대/축소, Space+드래그(또는 가운데버튼)로 화면 이동
  5. 카드 선택 후 Delete 키 또는 [🗑 선택 삭제] 버튼으로 제거

확인 포인트:
  - 아이콘 개수가 하단 상태바에 실시간 반영
  - 현재는 저장 기능 없음(HM-07에서 hmi-layout.json 저장 추가 예정) — 탭 이동/재시작 시 초기화됨

다음 Step 예고:
  HM-04에서는 모터·컨베이어·탱크·밸브 등 실제 장비 아이콘 팔레트를 추가하고,
  DeviceInstance/TagInstance 실시간 값과 아이콘을 연결하는 바인딩을 구현합니다.

### HM-04 구현 내역 (코드 완료 — 2026-07-16, 빌드 확인 대기)

```
★ 사용자 요청 반영: 장비가 추후에도 계속 추가될 수 있으므로, 아이콘 카드를
  "베이스 컨트롤 상속" 구조로 설계 — 신규 장비는 베이스 컨트롤(DeviceControlBase)을
  상속하는 것만으로 확장 가능하다.

파일 목록 (전체 신규):
  Views/DeviceControls/DeviceControlBase.xaml(.cs)
    ← 모든 장비 아이콘 카드의 공통 베이스(abstract UserControl).
      카드 프레임·선택 강조 테두리·카테고리 색상 바·아이콘 글리프·라벨 렌더링을
      전부 여기서 1번만 정의(DataContext=AbstractLayoutNode 파생 모델 바인딩).
  Views/DeviceControls/GenericIconControl.cs   — DeviceControlBase 상속 (GenericIconNode 전용)
  Views/DeviceControls/MotorControl.cs         — DeviceControlBase 상속 (MotorNode 전용)
  Views/DeviceControls/ConveyorControl.cs      — DeviceControlBase 상속 (ConveyorNode 전용)
  Views/DeviceControls/TankControl.cs          — DeviceControlBase 상속 (TankNode 전용)
  Views/DeviceControls/ValveControl.cs         — DeviceControlBase 상속 (ValveNode 전용)
      (5개 컨트롤 모두 현재는 베이스 그대로 사용하는 빈 클래스 — 향후 장비별
       회전 애니메이션 등 고유 시각효과가 필요해지면(HM-06) 해당 클래스에만
       추가하면 되고, 공통 카드 프레임은 손댈 필요 없음)

변경 파일 (전체 최종본 반영 완료):
  Core/Layout/LayoutNode.cs
    ← MotorNode/ConveyorNode/TankNode/ValveNode 4종 모델 추가
      (AbstractLayoutNode 상속, NodeType/DisplayLabel/IconGlyph/CategoryColor 재정의)
    ← LayoutNodeFactory.Create() switch + PaletteItems 에 4종 등록
  Views/LayoutCanvas/LayoutCanvasView.xaml
    ← 기존 단일 "IconNodeTemplate"(명시적 DataTemplate)을 제거하고, 노드 모델
      타입(DataType) ↔ 장비 컨트롤 타입을 매핑하는 암시적 DataTemplate 5개로 교체
      (예: DataType="{x:Type layout:MotorNode}" → <dc:MotorControl/>)
    ← NodesLayer ItemsControl 의 명시적 ItemTemplate 속성 제거
      (모델의 실제 타입에 따라 WPF가 자동으로 알맞은 DataTemplate을 선택 —
       드래그·줌·팬 코드(LayoutCanvasView.xaml.cs)는 무수정, ContentPresenter.Content
       가 여전히 AbstractLayoutNode 이므로 기존 히트테스트 로직 그대로 동작)
    ← 팔레트 안내 문구 갱신 (HM-04 완료 반영, HM-05 예고로 변경)
  ViewModels/LayoutCanvasViewModel.cs — 주석 갱신만(로직 변경 없음)

★ 확장 방법 (향후 신규 장비 추가 시 — 3단계):
  1) Core/Layout/LayoutNode.cs 에 AbstractLayoutNode 파생 모델 클래스 추가
  2) Views/DeviceControls/ 에 DeviceControlBase 상속 컨트롤 클래스 추가(빈 클래스 1줄)
  3) LayoutCanvasView.xaml Resources 에 DataTemplate 1개 추가(모델↔컨트롤 매핑)
     + LayoutNodeFactory.Create()/PaletteItems 에 등록
  → 캔버스 배치·드래그·줌/팬·선택/삭제 등 기존 메커니즘은 전혀 수정할 필요 없음.

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개
  [ ] DeviceControlBase 는 abstract — <dc:DeviceControlBase/> 로 직접 XAML 배치 시
      InvalidOperationException 발생함(정상 — 반드시 파생 클래스만 사용)

### 2단계: 런타임
  [ ] [🎨 레이아웃 편집] 탭 → 좌측 팔레트에 5개 항목(아이콘/모터/컨베이어/탱크/밸브) 표시
  [ ] 각 팔레트 버튼 클릭 → 캔버스에 해당 장비 카드가 고유 글리프·색상 바로 추가됨
  [ ] 카드 드래그/선택/삭제, 휠 줌, Space+드래그 패닝 — HM-03과 동일하게 정상 동작
      (베이스 컨트롤 교체 후에도 회귀 없음 확인)
  [ ] 여러 장비를 섞어 배치해도 각자 올바른 색상 바/글리프로 렌더링되는지 확인

## 📖 사용 설명

화면 조작 방법:
  1. [🎨 레이아웃 편집] 탭 클릭
  2. 좌측 팔레트에서 모터/컨베이어/탱크/밸브/아이콘 중 원하는 항목 클릭 → 캔버스에 카드 추가
  3. 이후 조작(드래그 이동/줌/팬/선택/삭제)은 HM-03과 동일

확인 포인트:
  - 장비 타입별로 카드 상단 색상 바와 아이콘 글리프가 다르게 표시됨
  - 아직 실시간 Tag 값 연동은 없음(HM-05에서 추가 예정) — 현재는 배치용 카드만 제공

다음 Step 예고:
  HM-05에서는 DeviceInstance/TagInstance 실시간 값을 아이콘 카드와 바인딩하여
  실제 장비 상태(가동/정지/알람 등)를 화면에 반영합니다.

### HM-05 구현 내역 (코드 완료 — 2026-07-16, 빌드 확인 대기)

```
★ 버그 발견 및 수정 (HM-01 이후 잠복): Collector GET /api/devices 는 DeviceInstance/
  TagInstance 를 JsonNamingPolicy.CamelCase 로 그대로 직렬화하므로 Tag 식별자
  JSON 필드명은 "tagId" 인데, HMI의 기존 TagSnapshotDto 는 "Id" 로 선언되어 있어
  실제로는 한 번도 값이 채워지지 않고 있었다(HM-01/02 에는 개수 확인 용도로만
  쓰여 드러나지 않음). HM-05 에서 Tag 선택 기능을 만들며 발견 → "TagId" 로 수정.
  ※ Monitor(MN-01B)에도 동일한 이식 코드가 있어 같은 결함 가능성 있음 —
    2차 정리 시 Monitor 도 함께 확인 필요(아래 후속·보류 항목에 반영).

변경/신규 파일 (전체 최종본 반영 완료):
  Models/DeviceSnapshotDto.cs
    ← TagSnapshotDto.Id → TagId 로 수정(위 버그 수정)
    ← RawValue/EngValue/Unit 필드 추가(Tag 바인딩 시 최초값 프리뷰용)
    ← Quality 는 의도적으로 추가하지 않음(Collector 응답에서 enum이 정수로
      직렬화되어 string 필드로 받으면 역직렬화 예외 발생 위험 — 실시간 Quality는
      SignalR TagValue Push(문자열)로만 반영)
  Core/Connection/CollectorConnection.cs
    ← FetchSnapshotAsync() 공개 메서드 추가(기존 _TrySyncCollectorIdAsync 내부
      로직을 재사용하도록 리팩터링 — 동작 변경 없음)
  Core/Connection/CollectorConnectionManager.cs
    ← GetConnectedEndpoints() / GetSnapshotAsync(collectorId) 추가
      (속성 패널의 Collector→Device→Tag 선택기가 사용)
  Core/Layout/LayoutNode.cs
    ← AbstractLayoutNode 에 BoundCollectorId/BoundPlcId/BoundTagId/BoundTagName·
      ValueText/ValueQuality·IsBound(계산) 추가 — 모든 장비 타입 공통이므로
      베이스에 위치(HM-07 레이아웃 저장 시 그대로 직렬화 대상)
  Views/DeviceControls/DeviceControlBase.xaml
    ← 카드에 ValueText 표시줄 추가(IsBound=True 일 때만 표시) — 베이스에서
      1번만 처리하므로 5개 장비 컨트롤 전부에 자동 반영됨
  ViewModels/LayoutCanvasViewModel.cs
    ← CollectorConnectionManager 주입(생성자) + TagValueReceived 구독
    ← AvailableCollectors/AvailableDevices/AvailableTags + PickedCollector/
      PickedDevice/PickedTag 계단식 선택 프로퍼티(OnXxxChanged 훅으로 하위
      목록 자동 로드 + 기존 바인딩 복원)
    ← ApplyBindingCommand/ClearBindingCommand
    ← _OnTagValueReceived: SignalR 콜백(비 UI 스레드) → Dispatcher.BeginInvoke로
      마샬링 후 일치하는 노드의 ValueText/ValueQuality 갱신(마샬링 규칙 준수)
  Views/LayoutCanvas/LayoutCanvasView.xaml
    ← 우측 "속성 — Tag 바인딩" 패널 추가(Grid.Column 2개 신설, HasSelection일
      때만 표시) — Collector/Device/Tag 콤보박스 3단 + 바인딩 적용/해제 버튼 +
      현재 값 프리뷰

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개

### 2단계: 런타임 (Collector 1대 이상 가동 + [Collector 관리] 탭에서 연결된 상태 필요)
  [ ] [🎨 레이아웃 편집] 탭 → 카드 하나 선택 → 우측에 "속성 — Tag 바인딩" 패널 표시
  [ ] Collector 콤보박스에 현재 연결된 Collector 목록 표시
  [ ] Collector 선택 → Device 콤보박스에 해당 Collector의 PLC/Device 목록 표시
  [ ] Device 선택 → Tag 콤보박스에 해당 Device의 Tag 목록 표시
  [ ] Tag 선택 후 [🔗 바인딩 적용] → 카드 하단에 값 표시줄 나타남(최초 "값 대기 중..."
      또는 스냅샷 초기값), 속성 패널 "현재 값"에도 동일하게 표시
  [ ] Collector 에서 실제 값이 갱신되면(폴링 주기) 카드의 값 표시줄이 실시간 갱신되는지 확인
  [ ] 카드 선택 해제 후 다시 선택 → 기존 바인딩(Collector/Device/Tag)이 콤보박스에
      자동 복원되는지 확인
  [ ] [🔓 바인딩 해제] → 값 표시줄 사라짐("-"), 재선택 시 콤보박스 초기화 확인
  [ ] 서로 다른 여러 카드에 각각 다른 Tag 를 바인딩해도 값이 섞이지 않고 각자
      올바르게 갱신되는지 확인

## 📖 사용 설명

화면 조작 방법:
  1. [🔌 Collector 관리] 탭에서 Collector 가 연결됨 상태인지 먼저 확인
  2. [🎨 레이아웃 편집] 탭 → 캔버스의 장비 카드 클릭(선택)
  3. 우측 속성 패널에서 Collector → Device/PLC → Tag 순서로 선택
  4. [🔗 바인딩 적용] 클릭 → 카드 하단에 실시간 값이 표시되기 시작
  5. 다른 카드를 선택하면 그 카드의 바인딩 상태가 다시 표시됨(카드마다 독립적)
  6. 바인딩을 바꾸려면 [🔓 바인딩 해제] 후 다시 선택·적용

확인 포인트:
  - 카드의 값 표시줄은 SignalR "TagValue" Push 수신 시마다 실시간 갱신
  - 아직 레이아웃 저장 기능이 없으므로(HM-07) 탭 이동/재시작 시 바인딩도 함께 초기화됨
  - 알람 상태 색상 반영은 HM-08에서 추가 예정(현재는 값 텍스트만 표시)

다음 Step 예고:
  HM-06에서는 회전(모터)·흐름(컨베이어)·수위(탱크) 등 RawValue 에 비례하는
  애니메이션 엔진과, 연결상태/알람에 따른 카드 색상 변화를 구현합니다.

### HM-06 구현 내역 (코드 완료 — 2026-07-16, 빌드 확인 대기)

```
설계 원칙: 애니메이션은 장비 타입마다 다르므로 DeviceControlBase(공통 카드
프레임)는 건드리지 않고, 각 장비 컨트롤(MotorControl 등)의 코드비하인드에서만
구현한다. 이를 위해 DeviceControlBase 에 최소한의 "확장 지점"만 추가했다:
 ① OnDeviceControlLoaded() 가상 메서드 — 파생 클래스가 Loaded 시점에 재정의
 ② IconText(x:Name) — 회전/흔들림 등 RenderTransform 적용 대상
 ③ LevelTrack/LevelFill(x:Name) — 수위 등 "채움 비율" 게이지(기본 Collapsed,
   TankControl 외 다른 장비도 재사용 가능한 예비 확장 지점)
 ④ 상태 점(StatusDot 역할의 Ellipse) — Tag Quality 기반 색상, 모든 장비 공통이라
   베이스에서 1번만 처리(장비별 확장 지점 아님)
파생 클래스들은 여전히 별도 XAML이 없는 순수 C# 클래스이므로(DeviceControlBase만
x:Class 보유) 이 요소들에 안전하게 접근 가능 — IComponentConnector 충돌 없음.

변경/신규 파일 (전체 최종본 반영 완료):
  Core/Layout/LayoutNode.cs
    ← AbstractLayoutNode 에 EngValue(double?) 추가 — ValueText 는 사람이 읽는
      문자열이라 애니메이션 계산에 부적합해 숫자값을 별도 보관
  ViewModels/LayoutCanvasViewModel.cs
    ← ApplyBinding()/_OnTagValueReceived() 에서 EngValue 도 함께 갱신(한 줄씩 추가)
  Core/Converters/UiConverters.cs
    ← TagQualityColorConverter 추가 — Quality 문자열("Good"/"Bad"/"Timeout"/
      "Disconnected") → 카드 상태 점 색상(Green/Yellow/Yellow/Red, 그 외 Text2)
  Views/DeviceControls/DeviceControlBase.xaml(.cs)
    ← IconText 에 x:Name 부여, LevelTrack/LevelFill(수위 게이지, 기본 Collapsed),
      상태 점(Ellipse, ValueQuality 바인딩) 추가
    ← OnDeviceControlLoaded() 가상 메서드 추가(Loaded 시 1회 호출)
  Views/DeviceControls/MotorControl.cs
    ← RotateTransform 을 IconText 에 적용 — |EngValue| 비례 연속 회전(Good 품질+
      값≠0 일 때만 회전, 그 외 정지)
  Views/DeviceControls/ConveyorControl.cs
    ← TranslateTransform(X) 을 IconText 에 적용 — |EngValue| 비례 좌우 왕복(흐름 효과)
  Views/DeviceControls/TankControl.cs
    ← LevelTrack.Visibility/LevelFill.Width 를 EngValue(0~100% 로 해석) 에 비례해 갱신
      (SizeChanged 시 재계산 — 최초 레이아웃 패스의 ActualWidth=0 문제 방어)
  Views/DeviceControls/ValveControl.cs
    ← IconText.Foreground 를 EngValue>0 여부로 전환(열림=Green, 닫힘=기본색)

★ 범위 결정: 현재는 IconGlyph(이모지) 자체를 애니메이션시키는 방식이다(회전/
  흔들림/색상 전환) — 실제 벡터 형상으로 그린 아이콘 교체는 별도 후속 항목으로
  이미 기록되어 있음("장비 아이콘 실형상 UI 컨트롤화", 후속·보류 항목 참조).
  두 작업은 독립적으로 진행 가능(형상 교체 시에도 OnDeviceControlLoaded() 훅과
  애니메이션 로직은 그대로 재사용 가능하도록 설계함).

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개

### 2단계: 런타임 (Tag 바인딩된 카드 필요 — HM-05 체크리스트로 먼저 바인딩)
  [ ] 모터 카드에 값이 0이 아닌 숫자 Tag 바인딩 → 아이콘 글리프가 연속 회전하는지 확인
  [ ] 바인딩된 값이 크게 갱신될수록 회전 속도가 빨라지는지 확인(폴링 주기 내에서 체감)
  [ ] 값이 0이 되거나 Quality 가 Bad/Timeout/Disconnected 로 바뀌면 회전이 멈추는지 확인
  [ ] 컨베이어 카드 → 값 바인딩 시 아이콘이 좌우로 왕복(흐름 효과)하는지 확인
  [ ] 탱크 카드 → 값 바인딩 시 카드 하단에 수위 막대(LevelTrack)가 나타나고, 값에
      비례해 채움 폭(LevelFill)이 달라지는지 확인(0~100 범위로 클램프됨)
  [ ] 밸브 카드 → 값이 0보다 크면 아이콘이 강조색(녹색), 0 이하/미바인딩이면
      기본색으로 표시되는지 확인
  [ ] 카드 우상단에 작은 상태 점이 나타나고 Quality 에 따라 색이 바뀌는지 확인
      (Good=녹색, Bad/Timeout=노랑, Disconnected=빨강, 미바인딩=점 숨김)
  [ ] 여러 카드를 동시에 애니메이션시켜도(모터+컨베이어+탱크 혼합) UI가 끊기지
      않는지 확인(카드 수가 많을 때 체감 성능 확인)

## 📖 사용 설명

화면 조작 방법:
  1. HM-05 절차대로 모터/컨베이어/탱크/밸브 카드에 실제 숫자 값을 가진 Tag를 바인딩
  2. 값이 갱신될 때마다(Collector 폴링 주기) 카드가 자동으로 애니메이션 반응
  3. 별도 조작 없이 바인딩만 하면 애니메이션은 자동으로 시작/정지됨

확인 포인트:
  - 애니메이션은 순수 시각 효과이며 실제 장비 제어와는 무관(읽기 전용 표시)
  - 알람 배지·상세 팝업·ACK 는 아직 없음(HM-08에서 추가 예정) — 현재 상태 점은
    "1차 시각 신호"일 뿐임

다음 Step 예고:
  HM-07에서는 지금까지 만든 레이아웃(카드 배치+Tag 바인딩)을 hmi-layout.json
  으로 저장하고 재실행 시 복원하는 기능과, 여러 화면(페이지) 관리를 구현합니다.

### HM-07 구현 내역 (코드 완료 — 2026-07-16, 빌드 확인 대기)

```
★ 사용자 요청 추가 반영: "컨트롤러 간 Z-레벨 우선순위 지정" 기능을 HM-07과
  함께 구현 — 카드 겹침 순서를 사용자가 직접 조정할 수 있다.

① Z-레벨(겹침 순서) 우선순위
  Core/Layout/LayoutNode.cs
    ← AbstractLayoutNode 에 ZIndex(int, 기본값 0) 추가 — 모든 장비 타입 공통이므로
      베이스에 위치. 새 카드 추가 시 항상 기존 최댓값+1 로 맨 위에 배치됨.
  ViewModels/LayoutCanvasViewModel.cs
    ← BringToFrontCommand/SendToBackCommand/BringForwardCommand/SendBackwardCommand
      추가 — 각각 SelectedNode.ZIndex 를 (최댓값+1)/(최솟값-1)/(+1)/(-1) 로 조정.
      HasSelection 이 CanExecute 조건(선택된 카드가 있을 때만 활성화).
  Views/LayoutCanvas/LayoutCanvasView.xaml
    ← 툴바에 [⬆ 맨앞][▲ 앞으로][▼ 뒤로][⬇ 맨뒤] 버튼 4개 추가(선택 시에만 표시)
    ← NodesLayer.ItemContainerStyle 에 Panel.ZIndex 바인딩 추가(Canvas.Left/Top
      옆에 한 줄 추가) — 값이 클수록 위에 그려짐

② 레이아웃 저장·불러오기 + 다중 화면(페이지)
  Core/Config/HmiLayoutSettings.cs (신규)
    ← LayoutNodeDto(NodeType/Label/X/Y/ZIndex/BoundCollectorId/BoundPlcId/
      BoundTagId/BoundTagName — 실시간 값은 저장 안 함) · LayoutPageDto(Id/Name/
      Nodes) · HmiLayoutFile(ActivePageId/Pages) · HmiLayoutLoader(hmi-layout.json
      로드/저장, HmiSettingsLoader 와 동일 패턴 — 파일 없으면 "기본 화면" 1개 생성)
  ViewModels/LayoutCanvasViewModel.cs
    ← HmiLayoutLoader 주입(생성자) + InitializeAsync()(View.Loaded 에서 호출 —
      hmi-layout.json 로드 후 마지막 활성 화면 복원)
    ← Pages(ObservableCollection<LayoutPageViewModel>)/ActivePage — 화면 전환 시
      OnActivePageChanging(이전 화면 Nodes→_pageNodeCache 스냅샷 저장)/
      OnActivePageChanged(새 화면 스냅샷→Nodes 복원) 훅으로 자동 전환
    ← AddPageCommand/DeletePageCommand(마지막 1개는 삭제 불가)/SaveLayoutCommand
      (전체 화면을 hmi-layout.json 에 저장)
    ← LayoutPageViewModel(Id/Name, ObservableObject) — 페이지 콤보박스 항목
      (LayoutPageDto 와 별도 — 이름 편집 시 UI 즉시 갱신되도록)
  Views/LayoutCanvas/LayoutCanvasView.xaml
    ← 캔버스 상단에 "화면 관리 바" 신설(Row 0 추가) — 화면 선택 콤보 + 이름
      편집 텍스트박스 + [➕ 새 화면][🗑 화면 삭제][💾 레이아웃 저장] 버튼
  Views/LayoutCanvas/LayoutCanvasView.xaml.cs
    ← Loaded 핸들러에서 _vm.InitializeAsync() 호출 추가
  App.xaml.cs
    ← HmiLayoutLoader DI 등록 추가(LayoutCanvasViewModel 보다 먼저)

★ 범위 결정: 저장 대상은 "배치 좌표·Z순서·Tag 바인딩 식별자"뿐이며, 실시간 값
  (ValueText/EngValue/ValueQuality)은 저장하지 않는다 — 재실행 시 Collector
  재연결 후 SignalR TagValue Push로 다시 채워지는 값이기 때문. 복원 직후에는
  바인딩된 카드에 "값 대기 중..."이 표시된다.

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개

### 2단계: 런타임 — Z-레벨
  [ ] 카드 2개를 겹치게 배치 → 나중에 추가한 카드가 항상 위에 보이는지 확인
  [ ] 아래에 깔린 카드를 선택 → [⬆ 맨앞] 클릭 → 해당 카드가 위로 올라오는지 확인
  [ ] [⬇ 맨뒤] → 다시 맨 아래로 내려가는지 확인
  [ ] [▲ 앞으로]/[▼ 뒤로] → 한 단계씩 순서가 바뀌는지 확인(카드 3개 이상으로 테스트)

### 3단계: 런타임 — 레이아웃 저장/불러오기
  [ ] [🎨 레이아웃 편집] 탭 → 카드 여러 개 배치 + 일부는 Tag 바인딩까지 완료
  [ ] [💾 레이아웃 저장] 클릭 → {HMI 실행파일}\Config\hmi-layout.json 생성 확인
  [ ] 프로그램 재시작 → [🎨 레이아웃 편집] 탭 진입 시 저장했던 배치·Z순서·바인딩이
      그대로 복원되는지 확인(바인딩된 카드는 "값 대기 중..." 표시 후 Collector
      재연결되면 실제 값으로 갱신)
  [ ] [➕ 새 화면] → 새 화면으로 전환되며 캔버스가 비는지 확인
  [ ] 화면 이름 텍스트박스에서 이름 변경 → 화면 선택 콤보박스에도 즉시 반영되는지 확인
  [ ] 두 화면을 오가며 각각 다른 카드 배치를 유지하는지 확인(전환 시 섞이지 않음)
  [ ] [🗑 화면 삭제] → 마지막 남은 화면 1개일 때는 버튼이 비활성화되는지 확인
  [ ] [💾 레이아웃 저장] 후 재시작 → 마지막으로 편집 중이던 화면이 자동으로 열리는지 확인

## 📖 사용 설명

화면 조작 방법 (Z-레벨):
  1. 카드 선택 → 툴바의 [⬆ 맨앞]/[▲ 앞으로]/[▼ 뒤로]/[⬇ 맨뒤] 버튼으로 겹침 순서 조정

화면 조작 방법 (레이아웃 저장/다중 화면):
  1. 카드 배치 및 Tag 바인딩 작업 후 상단 [💾 레이아웃 저장] 클릭
  2. 여러 화면이 필요하면 [➕ 새 화면]으로 추가 화면 생성, 화면 선택 콤보박스로 전환
  3. 화면 이름은 콤보박스 옆 텍스트박스에서 바로 수정 가능
  4. 더 이상 필요 없는 화면은 [🗑 화면 삭제](최소 1개는 유지되어야 함)

확인 포인트:
  - hmi-layout.json 은 {HMI 실행파일}\Config\ 폴더에 저장됨(hmi.json 과 동일 위치)
  - 저장을 누르지 않으면 변경사항은 다음 실행 시 사라짐(자동 저장 아님)
  - 실시간 값은 저장되지 않으므로 재실행 후 Collector 연결이 되어야 값이 다시 채워짐

다음 Step 예고:
  HM-08에서는 알람 오버레이(아이콘 배지+상세 팝업+ACK)를 구현하여, 지금까지
  "1차 시각 신호"였던 상태 점을 실제 알람 확인/처리 기능으로 확장합니다.

### HM-Base-0~2 + HM-01~02 구현 내역 (코드 완료 — 2026-07-16, 빌드 확인 대기)

```
솔루션: HMI\IIoT.HMI.sln (신규) + HMI\IIoT.HMI\IIoT.HMI.csproj
        (net8.0-windows·UseWPF, FrameworkReference 없음 — HM-01은 SignalR.Client가
         순수 클라이언트 라이브러리라 불필요. HM-11(자체 Hub 호스팅) 시점에 추가 예정)
        참조: lssLib.Log · IIoT.UI.Themes · IIoT.Contracts(HealthPipeServer)
        패키지: CommunityToolkit.Mvvm 8.4.2 · Microsoft.Extensions.DependencyInjection 8.0.1 ·
               Microsoft.AspNetCore.SignalR.Client 8.0.*

파일 목록 (전체 신규):
  App.xaml / App.xaml.cs           — 테마·LogManager·HealthPipeServer("IIoT.HMI")·DI 부트스트랩
  MainWindow.xaml / .xaml.cs       — 헤더+탭바(5개, Manager MG-04 DataTrigger 필 스타일)+본문
  HmiMainViewModel.cs              — ActiveTabIndex/SwitchTabCommand (탭 0~4)
  Models/CollectorEndpoint.cs      — hmi.json Collectors[] 1항목 (Monitor MN-01 이식)
  Models/DeviceSnapshotDto.cs      — GET /api/devices 응답 DTO (최소 필드, Monitor MN-01B 이식)
  Core/Config/HmiSettings.cs       — hmi.json 로더/세이버 (Monitor MonitorSettings 이식)
  Core/Connection/CollectorConnection.cs        — Collector 1개 연결 (REST 스냅샷+SignalR, Monitor MN-01B/02/03 이식)
  Core/Connection/CollectorConnectionManager.cs — 다중 Collector 동기화 (Monitor 이식,
        단 LiveTagAggregator/AlarmAggregator/TrayNotificationService 의존 제거 —
        TagValueReceived/AlarmChanged 범용 이벤트로 재발행하도록 단순화)
  Core/Converters/UiConverters.cs  — ConnectionStatusColorConverter (Monitor MN-02B 이식)
  ViewModels/CollectorManageViewModel.cs        — [Collector 관리] 탭 VM (Monitor 이식)
  Views/CollectorManage/CollectorManageView.xaml(.cs) — Collector DataGrid CRUD 화면 (Monitor 이식)

탭 구성 (MainWindow): 0=현황판(placeholder) 1=레이아웃 편집(placeholder)
  2=Collector 관리(✅ 실제 화면 — HM-02) 3=알람(placeholder) 4=로그(placeholder)

★ 설계 확정 반영 확인:
  ① ForceWrite → C-EX-13에서 Collector Hub 메서드로 이미 추가됨 (HM-09에서 사용 예정)
  ② 캔버스 재사용 → 아직 미착수 (HM-03에서 Studio NodesLayer 이식 예정)
  ③ 다중 Collector 지원 → CollectorConnectionManager 로 구현 완료 (Dictionary<CollectorId,Connection>)

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] HMI\IIoT.HMI.sln 로 Visual Studio 열기 → Clean → Rebuild → 오류 0개
  [ ] using System.IO/System.Linq 누락(CS0246) 없음 확인

### 2단계: 런타임
  [ ] F5 실행 → 테마 적용된 창 + 헤더("IIoT.HMI 생산현황판") + 탭바 5개 표시
  [ ] 탭 클릭 시 각각 전환 확인 (현황판/레이아웃 편집/알람/로그 = 색상 placeholder,
      Collector 관리 = 실제 DataGrid 화면)
  [ ] [Collector 관리] 탭 → [＋ Collector 추가] 클릭 → 목록에 항목 추가 + hmi.json 저장 확인
      (Config\hmi.json 생성 확인)
  [ ] 등록한 Collector의 Host/Port 를 실행 중인 Collector(예: localhost:7878)로 지정 후 저장
      → 연결상태 컬럼이 "연결 중..." → "연결됨"(녹색)으로 전환되는지 확인
  [ ] Collector 종료 → 연결상태가 "재연결 중..."(황색)으로 전환되는지 확인
  [ ] 창 종료 → 프로세스가 지연 없이 정상 종료되는지 확인 (SignalR HubConnection 정리)

### 3단계: 예상 오류 대비
  - CS0246(System.Linq/System.IO) → 해당 파일에 using 명시적 추가
  - NU1605/다운그레이드 오류(SignalR.Client) → Microsoft.Extensions.DependencyInjection
    버전을 오류 메시지 요구 버전 이상으로 재고정
  - IIoTGrid/PrimaryBtn 등 스타일 못 찾음 → IIoT.UI.Themes 프로젝트 참조 확인

## 📖 사용 설명

이번 단계에서 추가된 기능: IIoT.HMI 신규 프로그램 뼈대 + Collector 다중 연결 관리

화면 조작 방법:
  1. IIoT.HMI 실행 → 헤더 우측 테마 버튼으로 테마 전환 가능 (7개 테마)
  2. 상단 탭바에서 [🔌 Collector 관리] 클릭
  3. [＋ Collector 추가] → 목록에 새 항목 추가됨 (Host=localhost, Port=7878 기본값)
  4. Host/Port 를 실제 가동 중인 Collector 주소로 수정 → [💾 저장]
  5. 연결상태 컬럼에서 연결 진행 상황 실시간 확인 (미연결→연결 중...→연결됨)
  6. 나머지 탭(현황판/레이아웃 편집/알람/로그)은 아직 준비 중 placeholder 화면임

확인 포인트:
  - {HMI 실행파일}\Config\hmi.json 에 Collectors[] 저장됨
  - 여러 Collector를 등록하면 각각 독립적으로 연결 상태가 표시됨(다중 지원 확인)

다음 Step 예고:
  HM-03에서는 Studio의 NodesLayer 캔버스 인프라를 이식해 장비 아이콘을
  자유롭게 배치할 수 있는 레이아웃 편집 캔버스를 구현합니다.

---

## 🛠 세션 운영 규칙 (준수 필수)

```
① 코드 납품: 부분 수정 목록 금지 → 전체 파일 최종본을 실제 소스 경로에
   직접 저장 + 경로 명시
② 기존 파일 수정 시 변경 없는 부분(주석·참조) 그대로 유지
③ 매 Step 완료 시 [컴파일 확인 체크리스트] + [사용 설명] 제공
④ 판단 필요 사항은 구현 전 사용자 확인
⑤ 세션 종료 전 이 핸드오프 갱신 + Git 커밋 권장
⑥ 응답 마지막에 "✅ 작업 완료" 표시
⑦ 파일 삭제: 원인 설명 + 사용자 허락 후에만 진행
⑧ 다음 Step 예고 시 진행 내용(무엇을 만들지) 설명 포함
⑨ Step 진행 시 단계 표시 (예: "진행 단계: MG-04 [1/3] …")
```

---

## 🐞 핵심 버그 규칙 (재발 방지 — 코드 작성 시 필수 적용)

```
★ 이번 세션 신규 확정:
 - WPF(net8.0-windows) 프로젝트에서 StreamReader/Writer·File·Path·Directory
   사용 파일은 반드시 "using System.IO;" 명시 (ImplicitUsings 미의존 —
   CS0246 실제 발생 2건). System.Linq 도 동일 방침으로 명시 중.
★ Monitor 세션 누적 (요약):
 - DI: MainWindow 반드시 AddSingleton (Transient → 이중 창) / StartupUri 금지
 - UI 마샬링: Dispatcher.BeginInvoke (Invoke 금지 — OnExit 교착, 버그 #11)
 - OnExit: 리소스 보유 싱글턴은 _WaitWithTimeout(5초) 세트로 정리
 - UseWindowsForms=true 시 <Using Remove="System.Windows.Forms"/> +
   Forms using 은 단독 파일에서만 (CS0104, 버그 #12)
 - FrameworkReference(AspNetCore) 선반영 금지 — 필요 Step 에서 도입 (버그 #1)
   Microsoft.Extensions.* 명시 버전 금지 (FrameworkReference 제공 버전 사용)
 - IIoTGrid 기본 IsReadOnly=True → 입력 화면은 로컬 재정의 (버그 #5)
 - GroupStyle HeaderTemplate {Binding Name}은 Mode=OneWay (버그 #8)
 - [RelayCommand(CanExecute=…)] 트리거 프로퍼티에 [NotifyCanExecuteChangedFor] 필수
 - DisplayMemberPath+ItemTemplate 병용 금지 / Grid Padding 불가(Border 사용)
 - ComboBox=PropCombo, TextBox=PropInput, 버튼=테마 정식 스타일(Success/Danger/
   Secondary/Ghost/PrimaryBtn) / DynamicResource 필수 (Trigger Setter 포함)
 - static 이벤트 구독은 반드시 해제 (누수)
 - Process/파일 핸들은 매 사용 후 Dispose (미보관 원칙)
★ HMI 착수 전 신규 확인 사항 (2026-07-16):
 - CollectorConnection(MN-01B)의 HttpClient/HubConnection 콜백은 UI 스레드가
   아니므로 Endpoint 등 관찰 가능 상태 변경은 반드시 Dispatcher.Invoke 로 마샬링
   (Monitor에서 이미 확정된 _SetStatus/_SetId 헬퍼 패턴 그대로 재사용)
 - IIoTHub 인스턴스는 요청마다 생성·소멸 — 필드에 상태 보관 금지 (C-EX-13 추가 시 동일 적용)
전체 오류 참조표: SKILL.md "오류 빠른 참조" 절 참조
```

---

## 🔧 후속·보류 항목

```
[2차 정리 — Manager~Sequence 완료 후 일괄]
 ① Monitor MonitorMainViewModel.cs → 루트 이동 + namespace 정렬 (규칙 예외 해소)
 ② Collector/Monitor SignalR 코드의 lssLib.SignalR 공통화 검토
    (HMI의 CollectorConnection 이식도 이 공통화 검토에 포함하여 함께 판단)
 ③ 이벤트 이력 DB(manager.db) 조회 UI (MG-EX-07 통합 검토)
 ④ Monitor Models/DeviceSnapshotDto.cs 필드명 점검 (HM-05에서 HMI측 동일 파일에서
    "TagSnapshotDto.Id" 가 실제 Collector 응답 필드 "tagId" 와 불일치해 값이 채워지지
    않는 잠복 버그를 발견·수정함 — Monitor 도 MN-01B 이식본이라 같은 결함일 가능성)
[보류]
 MG-EX-11 웹 상태 페이지 / MG-EX-12 원격 관리 (HMI/Sequence 이후)
 C-EX-11 (Collector 후속) / Studio 보류 4건 (가상Tag·N포트·Function·프로토콜편집)
 HM-EX (히스토리 트렌드 오버레이 / 캡처·리포트 / 다중 모니터 지원 — HMI 1차 마감 후)
 HM-04-EX 장비 아이콘 실제 형상화 (아래 "⭐ 신규 후속 기능: 장비 아이콘 실형상 UI 컨트롤화" 참조)
```

### ⭐ 신규 후속 기능 (기록만, 착수 안 함): 설정(Settings) UI 편집 화면

```
배경: 실제 코드 확인 결과 Collector/Manager/Monitor 모두 "환경설정" 탭이 없고,
      settings.json/manager.json/monitor.json 상당수 섹션이 파일 직접 편집으로만 가능함.

① 개별 프로그램 — 로컬 "⚙ 환경설정" 탭 신설
 IIoT.Collector (settings.json) — 현재 탭[수집현황/알람/수집흐름/트렌드/장비/로그]에 추가 필요:
   Storage(Provider·SdtExcDevPercent·StatIntervalSec·WatchPath·SQLite.DbPath·
           InfluxDB 전체·Mqtt 전체)
   SignalR(Enabled·Port·AllowedOrigins)
   Retry(Enabled·IntervalsSec·MaxRetries)
   Notification(Smtp 전체·Webhook 전체)
   ForceWrite(Enabled·WarnOnActiveAlarm)
   Filter(SpikeFilterEnabled·SpikeMaxDeltaPercent·DeadbandEnabled·DeadbandPercent)
   VirtualTag(Enabled·IntervalMs)
   Security(ForceWriteApiKey·ApiKey) — 입력 시 마스킹 처리
   Retention(Enabled·RetentionDays·RunAtTime)
   Backup(Enabled·RunAtTime·MaxBackupCount·BackupDir)
   CollectorId (현재 자동생성만 — 수동 변경 UI 없음)
 IIoT.Manager (manager.json) — Resource(CpuWarnPercent·MemoryWarnMb) UI 없음
   (Processes·Deploy·Schedules 는 이미 프로세스/배포/스케줄 탭에서 편집 가능 — 대상 아님)
 IIoT.Monitor (monitor.json) — Web(Enabled·Port, 자체 SignalR Hub) UI 없음
   (Collectors 는 이미 [Collector 관리] 탭에서 편집 가능 — 대상 아님)
 IIoT.HMI — 향후 Step 설계 시 자체 설정 항목 발생하면 함께 반영

② Manager 관리자 화면 — 원격 통합 설정 조회/편집 (선택 확장, 착수 시 방식 확정 필요)
 신규 탭(가칭 [🔧 설정관리])에서 각 프로그램의 settings.json 을 원격으로 보고 수정
 방식 후보: (a) MG-06 배포 인프라(백업→복사→.signal) 확장
           (b) 각 프로그램에 REST 설정 조회/저장 엔드포인트 추가
               (Collector GET /api/devices 패턴 준용)

③ 공통 설계 원칙
 저장 시 유효성 검사(포트 중복·임계값 범위·필수값)
 재시작 필요 설정 변경 시 안내 배지 표시
 각 프로그램의 기존 XxxSettingsLoader(로드/저장 로직) 그대로 재사용, UI만 추가
 민감정보(SMTP 비밀번호·API Key)는 화면 표시 시 마스킹, 저장 시에만 평문 반영

착수 시점: 미정 — 사용자 요청 시 별도 Step 맵으로 설계 후 착수 (현재는 기록만)
```

### ⭐ 신규 후속 기능 (기록만, 착수 안 함): 장비 아이콘 실형상 UI 컨트롤화 (HM-04 후속)

```
배경: 2026-07-16 HM-04 빌드 확인 5단계 진행 중 사용자 요청 — 현재 모터/컨베이어/
      탱크/밸브 카드는 DeviceControlBase 공통 렌더링으로 IconGlyph(이모지 문자,
      예: ⚙ ➡ 🛢 🚰)를 큰 텍스트로 표시하는 "단순 박스" 수준. 실제 장비 형태를
      알아볼 수 있는 벡터/도형 기반 UI로 교체 필요.

범위:
 - Views/DeviceControls/MotorControl.cs 등 4개 장비 컨트롤 각각을 "빈 상속
   클래스"에서 "실제 아이콘을 그리는 클래스"로 확장
   (모터=회전자+하우징 형태, 컨베이어=벨트+롤러, 탱크=원통+게이지, 밸브=배관+핸들 등
   Path/Geometry 또는 Viewbox+Canvas 벡터 드로잉)
 - DeviceControlBase 확장 필요: 현재는 XAML에서 {Binding IconGlyph} 텍스트를
   직접 렌더링하는 구조이므로, 파생 클래스가 아이콘 영역을 재정의할 수 있도록
   "아이콘 슬롯"(예: ContentPresenter + 파생 클래스별 XAML/DrawingGroup 리소스,
   또는 파생 클래스 전용 UserControl+XAML 인스턴스로 전환)을 먼저 설계해야 함
   — 카드 프레임·선택강조·색상바·라벨 등 공통 부분은 그대로 유지.
 - HM-06(애니메이션 엔진)과 밀접 — 회전(모터)·흐름(컨베이어)·수위(탱크)·개폐(밸브)
   애니메이션을 붙이려면 벡터 기반 아이콘이 선행되어야 유리하므로, 착수 시
   HM-06과 함께 설계하는 방안을 우선 검토.

착수 시점: 미정 — HM-05(Tag 바인딩) 이후 또는 HM-06(애니메이션 엔진)과 통합 설계 시
           사용자 요청에 따라 별도 Step으로 착수 (현재는 기록만, 미착수)
```

---

## 🔜 다음 세션 진행 순서

### ① Manager + lssLib.SignalR 통합 빌드 확인 — ✅ 완료 (2026-07-16, 사용자 직접 빌드·런타임 검토)

### ② IIoT.HMI Step 맵 설계 — ✅ 완료 (2026-07-16, 위 "IIoT.HMI — Step 맵 설계" 절 참조)

### ③ C-EX-13 — ✅ 빌드 확인 완료 (2026-07-16)
```
IIoTHub.cs / SignalRHostService.cs / SignalRPushService.cs 3개 파일 전체 최종본 반영 완료,
사용자 Collector 빌드·런타임 확인 완료.
```

### ④ HM-Base-0~2 + HM-01~05 — ✅ 빌드 확인 완료 (2026-07-16, 사용자 직접 검증)
```
HMI\IIoT.HMI.sln + Collector C-EX-13 포함 전체 빌드·런타임 확인 완료.
HM-04: 장비 아이콘을 DeviceControlBase 상속 구조로 구현 — 신규 장비는 베이스
컨트롤 상속만으로 확장 가능(사용자 요청 반영, 위 "HM-04 구현 내역" 참조).
HM-05: Collector→Device→Tag 3단 선택 속성 패널 + 실시간 값 바인딩. 진행 중
TagSnapshotDto 필드명 잠복 버그(Id→TagId) 발견 및 수정(위 "HM-05 구현 내역" 참조,
Monitor 동일 결함 가능성은 후속·보류 항목에 등록).
```

### ⑤ HM-06 (애니메이션 엔진) — ✅ 빌드 확인 완료 (2026-07-16, 사용자 직접 검증)
```
Core/Layout/LayoutNode.cs(EngValue 추가) · ViewModels/LayoutCanvasViewModel.cs
(EngValue 갱신 2곳) · Core/Converters/UiConverters.cs(TagQualityColorConverter) ·
Views/DeviceControls/DeviceControlBase.xaml(.cs)(IconText x:Name·LevelTrack/
LevelFill·상태 점·OnDeviceControlLoaded 훅) · MotorControl/ConveyorControl/
TankControl/ValveControl.cs(각 장비 전용 애니메이션 구현) — 전체 9개 파일
신규/수정 완료, 사용자 빌드·런타임 확인 완료.
```

### ⑥ HM-07 (레이아웃 저장·불러오기 + Z-레벨 우선순위) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-16)
```
★ 사용자 요청 추가: Z-레벨(카드 겹침 순서) 우선순위 지정 기능을 HM-07과 함께 구현.
Core/Layout/LayoutNode.cs(ZIndex 추가) · Core/Config/HmiLayoutSettings.cs(신규 —
LayoutNodeDto/LayoutPageDto/HmiLayoutFile/HmiLayoutLoader) ·
ViewModels/LayoutCanvasViewModel.cs(Z-레벨 커맨드 4개 + Pages/ActivePage/
InitializeAsync/AddPage/DeletePage/SaveLayout + LayoutPageViewModel) ·
Views/LayoutCanvas/LayoutCanvasView.xaml(Z-레벨 툴바 버튼 4개 + 화면 관리 바 신설) ·
LayoutCanvasView.xaml.cs(InitializeAsync 호출 추가) · App.xaml.cs(HmiLayoutLoader
DI 등록) — 전체 6개 파일 신규/수정 완료. 사용자 빌드·런타임 확인 필요
(위 "HM-07 구현 내역" 절 체크리스트 참조).
완료 확인 후 → HM-08 (알람 오버레이) 착수. ← 다음 시작점
```

---

## Ver History (요약)
| 버전 | 내용 |
|---|---|
| ~v7.38 | Studio·Collector·Monitor 완료 (이전 세션들 — 상세는 구버전 핸드오프/SKILL.md) |
| v8.0~v8.9 | Manager MG-Base-0~03 + 운영 규칙 확립 + CS0246(System.IO) 규칙 확정 |
| v9.0~v9.3 | MG-04~07 완료 (로그뷰어 표준 UI·대시보드·배포·스케줄) + MG-EX 후보 12건 등록 |
| v9.4~v10.3 | MG-EX-01~10 완료 (A·B·C그룹 — 상주·알림·자동기동·이력DB·리소스·추세·로그검색·롤백·diff·배포후재시작) |
| v10.4 | lssLib.SignalR 모듈 신설 (Base\BCL\SignalR — 라이브러리+Demo+sln) + TrafficLogged 훅 |
| v11.0 | 새 세션용 핸드오프 전면 재작성 (이력 압축) — 시작점: 통합 빌드 확인 → HMI |
| v11.1 | Manager + lssLib.SignalR 통합 빌드·런타임 확인 완료 (사용자 직접 검증, 2026-07-16) |
| v11.2 | IIoT.HMI Step 맵 설계 확정 (2026-07-16) — 확정 사항: ① Collector에 ForceWrite |
| | | Hub 메서드 추가(C-EX-13 선행) ② Studio 캔버스 인프라 재사용(프리폼 배치) |
| | | ③ Monitor와 동일 다중 Collector 지원. HM-Base-0~HM-12 + C-EX-13 Step 맵 확정 |
| v11.3 | C-EX-13 코드 완료 (2026-07-16, 빌드 확인 대기) — IIoTHub.cs: ForceWrite 원격 메서드 |
| | | 추가(ForceWriteService 위임) + PushForceWriteResultAsync / SignalRHostService.cs: |
| | | ForceWriteService DI 등록 / SignalRPushService.cs: TagForceWriteEvent 전체 Push |
| v11.4 | 설정(Settings) UI 편집 화면 — 후속 기능으로 기록 (2026-07-16, 착수 안 함) — |
| | | Collector/Manager/Monitor 모두 "환경설정" 탭 없음 확인, Collector settings.json |
| | | 10개 섹션 등 UI 부재 목록화. Manager 원격 통합 설정관리 탭 아이디어 기록 |
| **v11.5** | **HM-Base-0~2 + HM-01~02 코드 완료 (2026-07-16, 빌드 확인 대기)** |
| | | **HMI\IIoT.HMI.sln 신규 생성 (Contracts·UI.Themes·lssLib.Log 참조)** |
| | | **App/MainWindow/HmiMainViewModel: 테마+탭바 5개(현황판·레이아웃 편집·** |
| | | **Collector 관리·알람·로그) — Collector 관리 외 4개는 placeholder** |
| | | **HM-01: CollectorEndpoint·DeviceSnapshotDto·HmiSettings(hmi.json)·** |
| | | **CollectorConnection·CollectorConnectionManager (Monitor MN-01B 이식,** |
| | | **집계기 의존 제거 후 TagValueReceived/AlarmChanged 이벤트로 단순화)** |
| | | **HM-02: CollectorManageViewModel/View — Collector 등록·편집·삭제·연결상태 표시** |
| | | **다음 세션 시작점: C-EX-13 + HMI 빌드 확인 → HM-03(레이아웃 캔버스) 착수** |
| **v11.6** | **HM-03 코드 완료 (2026-07-16, 빌드 확인 대기)** |
| | | **Core/Layout/LayoutNode.cs(AbstractLayoutNode+GenericIconNode+Factory) ·** |
| | | **HexColorConverter · LayoutCanvasViewModel · LayoutCanvasView —** |
| | | **Studio CanvasView 중 NodesLayer(카드 배치·드래그·줌/팬)만 이식,** |
| | | **포트/연결선(PortsLayer·ConnectionsLayer) 전부 제외** |
| | | **MainWindow 탭1(레이아웃 편집) placeholder → 실제 캔버스 화면으로 교체** |
| | | **다음 세션 시작점: C-EX-13 + HMI 빌드 확인 → HM-04(장비 아이콘 팔레트) 착수** |
| **v11.7** | **HM-04 코드 완료 (2026-07-16, 빌드 확인 대기) — 사용자 요청: 장비 추가** |
| | | **확장성 반영, 베이스 컨트롤 상속 구조로 설계** |
| | | **Views/DeviceControls/DeviceControlBase(abstract UserControl, 카드 프레임·** |
| | | **선택강조·색상바·글리프·라벨 공통 렌더링) + GenericIcon/Motor/Conveyor/** |
| | | **Tank/ValveControl(전부 베이스 상속 — 확장 지점만 보유)** |
| | | **Core/Layout/LayoutNode.cs: MotorNode/ConveyorNode/TankNode/ValveNode 4종** |
| | | **모델 추가 + Factory/PaletteItems 등록** |
| | | **LayoutCanvasView.xaml: 명시적 IconNodeTemplate → DataType 기반 암시적** |
| | | **DataTemplate 5개로 교체(모델↔컨트롤 자동 매핑), 드래그/줌/팬 로직 무수정** |
| | | **신규 장비 확장 방법 3단계 문서화(모델 추가→컨트롤 상속→DataTemplate 등록)** |
| | | **다음 세션 시작점: C-EX-13 + HMI 빌드 확인 → HM-05(아이콘↔Tag 바인딩) 착수** |
| **v11.8** | **HM-04 빌드 확인 중 후속 기능 기록 (2026-07-16, 착수 안 함)** |
| | | **장비 아이콘 실형상 UI 컨트롤화 — 현재 IconGlyph 텍스트 렌더링을 모터/** |
| | | **컨베이어/탱크/밸브별 실제 형상(벡터/도형) UI로 교체하는 작업을 후속** |
| | | **기능으로 등록(DeviceControlBase 아이콘 슬롯 확장 필요, HM-06과 통합** |
| | | **설계 검토). 착수 시점 미정 — 사용자 요청 시 별도 Step으로 진행** |
| **v11.9** | **전체 개발 순서 2단계 확정 (2026-07-16, 사용자 확인) — [1차: 기본구조]** |
| | | **Manager→HMI→Sequence 로 5개 프로그램 기본 골격 우선 확립, [2차: 강화 순환]** |
| | | **이후 Studio 로 회귀해 보류 4건 등을 강화 → 전체 시스템 순환 강화 방식으로 진행** |
| **v11.10** | **HM-05 코드 완료 (2026-07-16, 빌드 확인 대기) — 아이콘↔Tag 바인딩** |
| | | **★ 버그 발견·수정: TagSnapshotDto.Id → TagId (Collector 응답 필드명** |
| | | **불일치로 HM-01 이후 값이 채워지지 않던 잠복 결함, Monitor 동일 결함** |
| | | **가능성 후속·보류 항목에 등록)** |
| | | **CollectorConnection.FetchSnapshotAsync()/ConnectionManager.GetSnapshot** |
| | | **Async() 추가 — 속성 패널의 Collector→Device→Tag 조회 지원** |
| | | **AbstractLayoutNode: BoundCollectorId/PlcId/TagId/TagName·ValueText·** |
| | | **ValueQuality·IsBound 추가(모든 장비 타입 공통, HM-07 저장 대상)** |
| | | **LayoutCanvasViewModel: 3단 계단식 선택기 + ApplyBinding/ClearBinding +** |
| | | **TagValueReceived 실시간 구독(Dispatcher.BeginInvoke 마샬링)** |
| | | **LayoutCanvasView.xaml: 우측 "속성 — Tag 바인딩" 패널 신설(선택 시 표시)** |
| | | **DeviceControlBase: 값 표시줄 추가(베이스 1곳 수정으로 5개 컨트롤 전부 반영)** |
| | | **다음 세션 시작점: C-EX-13 + HMI 빌드 확인 → HM-06(애니메이션 엔진) 착수** |
| **v11.11** | **C-EX-13 + HMI(Base-0~2, HM-01~05) 빌드·런타임 확인 완료(사용자 직접** |
| | | **검증) + HM-06(애니메이션 엔진) 코드 완료(빌드 확인 대기)** |
| | | **HM-06: EngValue(모델)·TagQualityColorConverter·DeviceControlBase 확장** |
| | | **지점(OnDeviceControlLoaded/IconText/LevelTrack·LevelFill/상태 점) +** |
| | | **Motor(회전)/Conveyor(흐름)/Tank(수위 게이지)/Valve(개폐 색상) 개별 구현** |
| | | **다음 세션 시작점: HM-06 빌드 확인 → HM-07(레이아웃 저장·불러오기) 착수** |
| **v11.12** | **HM-06 빌드·런타임 확인 완료(사용자 직접 검증) + HM-07 코드 완료** |
| | | **(빌드 확인 대기) — 레이아웃 저장·불러오기 + Z-레벨 우선순위(사용자 요청 추가)** |
| | | **Z-레벨: AbstractLayoutNode.ZIndex + BringToFront/SendToBack/BringForward/** |
| | | **SendBackward 커맨드 + 툴바 버튼 4개 + Panel.ZIndex 바인딩** |
| | | **저장/다중화면: Core/Config/HmiLayoutSettings.cs(신규, hmi-layout.json) +** |
| | | **LayoutCanvasViewModel Pages/ActivePage/InitializeAsync/AddPage/DeletePage/** |
| | | **SaveLayout + 화면 관리 바 UI. 실시간 값은 저장 대상에서 제외(배치+바인딩만)** |
| | | **다음 세션 시작점: HM-07 빌드 확인 → HM-08(알람 오버레이) 착수** |

---

*다음 세션: 이 파일을 먼저 읽고 → HM-07 빌드·런타임 확인 → HM-08 진행*
