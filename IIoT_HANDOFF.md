# IIoT.Solution 개발 핸드오프 파일
**작성일: 2026-07-20 | 버전: v11.35 | 다음 세션 시작점: ① HM-Base-0~HM-21 + HMI 환경설정 탭 + HM-23(신규 장비 5종) + HM-22(Manager 원격 설정) 전부 사용자 로컬 Windows 빌드·런타임 확인(정적 검증은 전부 완료 — 불일치 0건) → ② 설정(Settings) UI 트랙 전체 완료(로컬 5개 프로그램 + Manager 원격 통합) → ③ 다음 신규 기능은 사용자 지시 대기**

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
IIoT.HMI            🔄 Base-0~2 + HM-01~07 빌드 확인 완료, HM-08~12 코드완료(빌드대기) (생산현황판)
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
HM-07   레이아웃 저장·불러오기 (hmi-layout.json, 다중 화면 페이지)          ✅ 빌드 확인 완료
        + Z-레벨 우선순위 지정(카드 겹침 순서, 사용자 요청 추가) 포함

━━━ 알람·제어 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
HM-08   알람 오버레이 (아이콘 배지 + 상세 팝업 + ACK — AcknowledgeAlarm 재사용) ✅ 코드완료(빌드대기)
HM-09   ForceWrite 제어 다이얼로그 (아이콘 더블클릭 → 값 입력 →              ✅ 코드완료(빌드대기)
        SignalR Invoke("ForceWrite") — C-EX-13 선행 필수)
HM-10   다중 화면 관리 (HM-07의 콤보박스+이름편집 UI → 탭 바로 교체,          ✅ 코드완료(빌드대기)
        사용자 확인 완료 — "탭 바 형태로 교체" 옵션 선택)

━━━ 확장성 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
HM-11   웹 브라우저 표시 확장 (자체 SignalR Hub + wwwroot —                 ✅ 코드완료(빌드대기)
        Collector C-11/wwwroot 패턴 재사용, 1차 범위=읽기 전용 표시)
HM-12   보안 (화면 잠금 모드 + 활성 알람 중 강제쓰기 경고 + 세션 API Key 캐시,     ✅ 코드완료(빌드대기)
        사용자가 3가지 항목 모두 확인·선택)

━━━ 정리 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
HM-13   현황판 탭 제거·통합 (HM-03~12 가 전부 [레이아웃 편집] 탭에 구현되어         ✅ 코드완료(빌드대기)
        이미 생산현황판 역할을 겸함 — 별도 placeholder 탭 중복 제거, 사용자 확인 완료)
        탭 4개로 재정렬: [레이아웃 편집(=현황판)][Collector 관리][알람][로그]
        ※ "알람 탭 전체 목록/이력" 옵션은 이번엔 미선택 — 후속 요청 시 별도 Step
HM-14   알람 탭 실시간 목록 구현 (Monitor MN-03/MN-EX-06 이식 — AlarmAggregator+   ✅ 코드완료(빌드대기)
        필터/검색 툴바+ACK, Collector별 그룹핑, 최신순) — 사용자가 "실시간 목록만"
        범위 선택(SQLite 이력 저장은 미포함)
HM-15   로그 탭 실제 화면 구현 (Studio/Collector/Monitor 공통 LogPanelView 패턴  ✅ 코드완료(빌드대기)
        이식 — LogManager.Instance.LogAdded 구독, 레벨/Source 필터, 지우기)
        ★ 이 Step으로 4개 탭(레이아웃 편집·Collector 관리·알람·로그) 전부 placeholder
        없이 실제 화면으로 채워짐

━━━ 확장 (2026-07-19, 사용자 확정 — "1차 마감 보류, 후보 전체 착수") ━━━━━━
★ HMI 1차 마감(HM-EX 검토) 시점에 후보 7건을 모두 제시했고, 사용자가 전부
  착수를 선택 — "1차 마감 확정" 대신 아래 순서로 확장 Step을 이어서 진행한다.
  (작은/독립적 항목 → 큰/의존성 있는 항목 순서로 배치, 매 Step 완료 후
  사용자 확인 거쳐 다음으로 진행하는 기존 진행 방식 그대로 적용)
HM-16   알람 이력 SQLite 영구 저장 (Monitor AlarmHistoryService 패턴 이식 —      ✅ 코드완료(빌드대기)
        재시작해도 과거 알람 유지, 90일 보존. ★ Monitor 도 저장 전용이며 조회
        UI가 없음 — 동일 범위로 이식, 조회 UI는 별도 요청 시 추가 검토)
HM-17   실시간 트렌드 창 (레이아웃 편집 탭에서 Tag 바인딩 카드 우클릭 → OxyPlot  ✅ 코드완료(빌드대기)
        라인차트 창, Monitor MN-06 패턴 이식. ★ 조사 결과 Collector의 시계열
        저장소는 조회(읽기) API가 전혀 없어 "과거 이력 조회"는 범위 밖으로
        확정 — 사용자가 "실시간 트렌드만(권장)" 선택, 창을 연 시점부터만 표시)
HM-18   화면 캡처 PNG (현재 레이아웃 캔버스를 PNG 이미지로 저장 — WPF 내장       ✅ 코드완료(빌드대기)
        RenderTargetBitmap 만 사용, 새 의존성 없음). ★ PDF 리포트는 어느 프로그램
        에도 선례가 없어 사용자가 "PNG만(권장)" 선택, PDF는 범위 밖으로 확정)
HM-19   다중 모니터 지원 (같은 LayoutCanvasViewModel 을 공유하는 두 번째        ✅ 코드완료(빌드대기)
        LayoutCanvasView 를 독립 창(SecondaryDisplayWindow)으로 띄워 다른
        모니터로 옮길 수 있음 — 두 창에 동일 레이아웃이 실시간 동기화됨)
HM-20   장비 아이콘 실형상 UI 컨트롤화 (HM-04-EX — 모터/컨베이어/탱크/밸브       ✅ 코드완료(빌드대기)
        컨트롤을 이모지 텍스트 대신 벡터 도형으로 교체. ★ 코드로 직접 그린
        도형이라 실제 화면에서 비율·색상 확인 필요 — 시각적 조정은 빌드 후 요청 시 진행)
        HM-20b(사용자 피드백 반영): 탱크 수위 게이지를 막대형→차량 속도계
        스타일(다이얼+눈금+회전 바늘)로 전면 교체, 컨베이어를 화물 왕복 방식→
        롤러 실제 회전+벨트 점선 스크롤 방식으로 전면 교체(모터/밸브는 대상 아님)
HM-21   웹에서 ACK/ForceWrite 지원 (HM-11-EX — HmiWebHub 클라이언트 호출 메서드  ✅ 코드완료(빌드대기)
        추가, HM-12 IsForceWriteLocked 재사용 + API Key 검증 그대로 유지)
HM-22   설정(Settings) UI 편집 화면 (task #5 — Collector/Manager/Monitor/HMI    ⭕ 예정
        각 프로그램 설정 탭 신설 + Manager 원격 통합 설정 화면, 4개 솔루션에
        걸친 최대 범위 과제라 마지막 순서)
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
④-4 HM-07 (레이아웃 저장·불러오기 + Z-레벨 우선순위) — ✅ 빌드 확인 완료
④-5 HM-08 (알람 오버레이) — ✅ 코드 완료(빌드 대기)
④-6 HM-09 (ForceWrite 제어 다이얼로그) — ✅ 코드 완료(빌드 대기)
④-7 HM-10 (다중 화면 관리 — 탭 바 UI 교체) — ✅ 코드 완료(빌드 대기)
④-8 HM-11 (웹 브라우저 표시 확장) — ✅ 코드 완료(빌드 대기)
④-9 HM-12 (보안 — 화면 잠금+알람 경고+세션 API Key 캐시) — ✅ 코드 완료(빌드 대기)
④-10 HM-13 (정리 — 현황판 탭 제거·통합, 탭 4개로 재정렬) — ✅ 코드 완료(빌드 대기)
④-11 HM-14 (알람 탭 실시간 목록 — Monitor MN-03/MN-EX-06 이식) — ✅ 코드 완료(빌드 대기)
④-12 HM-15 (로그 탭 — Studio/Collector/Monitor 공통 LogPanelView 이식) — ✅ 코드 완료(빌드 대기)
⑤ HM-Base-0~HM-15 전체 빌드 확인(사용자 진행 중) → HMI 1차 마감 여부 검토
   → 사용자 결정: "1차 마감 보류, HM-EX 후보 7건 전체 착수" (2026-07-19)
⑥ HM-16 (알람 이력 SQLite) — ✅ 코드 완료(빌드 대기) → HM-17 (실시간 트렌드 창) —
   ✅ 코드 완료(빌드 대기) → HM-18 (화면 캡처 PNG) — ✅ 코드 완료(빌드 대기) →
   HM-19 (다중 모니터) — ✅ 코드 완료(빌드 대기) → HM-20/HM-20b (장비 아이콘
   실형상화 — 모터/밸브 HM-20 1차로 완료, 탱크/컨베이어는 HM-20b 재작업까지
   완료) — ✅ 코드 완료(빌드 대기) → HM-21 (웹 ACK/ForceWrite) — ✅ 코드
   완료(빌드 대기) → HM-22 (설정 UI 편집 화면) — ⭕ 예정
   순서로 진행 — 위 "확장" 절 참조
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

### HM-08 구현 내역 (코드 완료 — 2026-07-19, 빌드 확인 대기)

```
설계 원칙: 알람 배지·팝업도 HM-06 애니메이션과 동일하게 "모든 장비 타입 공통"
기능이므로 DeviceControlBase(공통 카드 프레임)에서 1번만 구현 — 5개 장비 컨트롤
전부에 자동 반영된다. Collector의 AlarmChanged 이벤트/AcknowledgeAlarm 메서드는
HM-01부터 이미 존재했으나(CollectorConnectionManager) 이번 Step에서 처음 실제
소비된다.

★ WPF 제약 대응(설계 결정): Popup(AlarmPopup)은 별도의 시각 트리 루트로
  렌더링되어 RelativeSource(AncestorType=UserControl) 바인딩과 DataContext
  자동 상속이 Popup 내부 요소에는 신뢰성 있게 동작하지 않는다(WPF 공통 제약).
  → Popup 자체의 DataContext 는 ElementName 바인딩(Popup 바깥 요소 참조,
    Popup 경계 문제 없음)으로 명시 지정.
  → Popup 내부 ACK 버튼은 Command 바인딩 대신 코드비하인드 Click 핸들러+
    VisualTreeHelper 기반 상위 탐색(this 기준, 정상 시각 트리에 있음)으로 대체.

변경/신규 파일 (전체 최종본 반영 완료):
  Core/Layout/LayoutNode.cs
    ← AbstractLayoutNode 에 HasActiveAlarm/AlarmKey/AlarmLevel/AlarmStatusText/
      AlarmMessage/AlarmTimeText 6개 필드 추가 — 모든 장비 타입 공통이므로 베이스에
      위치(레이아웃 저장 대상 아님 — 실시간 상태이므로 HmiLayoutSettings 직렬화 제외)
  Core/Converters/UiConverters.cs
    ← AlarmLevelColorConverter 추가 — AlarmLevel 문자열("HH"/"LL"→Red,
      "H"/"L"→Yellow, 그 외→Text2) → 배지 색상
  ViewModels/LayoutCanvasViewModel.cs
    ← 생성자에서 _connectionManager.AlarmChanged 구독 추가
    ← _OnAlarmChanged(collectorId, payload): SignalR 콜백(비 UI 스레드) →
      Dispatcher.BeginInvoke 마샬링 → BoundCollectorId/PlcId/TagId 일치 노드 탐색 →
      status="Recovered" 면 알람 필드 전부 초기화, 그 외에는 HasActiveAlarm=true +
      Key/Level/Status/Message/Time 갱신
    ← _CanAcknowledgeAlarm/[RelayCommand] AcknowledgeAlarmAsync(node) 추가
      (AcknowledgeAlarmCommand 로 생성됨 — Async 접미사는 소스 생성기가 제거) →
      _connectionManager.AcknowledgeAlarmAsync(node.BoundCollectorId, node.AlarmKey)
      호출("발생 출처로만 전송" 원칙 — Monitor MN-03/Collector C-EX-12 재사용)
  Views/DeviceControls/DeviceControlBase.xaml
    ← AlarmColor 컨버터 리소스 등록
    ← AlarmBadge(Button, 카드 좌상단, "⚠" 글리프, AlarmLevel 색상, Acked 시 반투명,
      HasActiveAlarm 일 때만 표시, Click="_OnAlarmBadgeClick")
    ← AlarmPopup(Popup, DataContext=ElementName 바인딩으로 명시 지정, Bottom 배치,
      StaysOpen=False) — TagName/메시지/레벨+상태/시각 표시 + AckButton
      (Click="_OnAckButtonClick", AlarmStatusText="Active" 일 때만 표시)
  Views/DeviceControls/DeviceControlBase.xaml.cs
    ← _OnAlarmBadgeClick: AlarmPopup.IsOpen 토글
    ← _OnAckButtonClick: VisualTreeHelper 로 상위 UserControl(LayoutCanvasView)
      탐색 → DataContext(LayoutCanvasViewModel).AcknowledgeAlarmCommand 실행
    ← _FindAncestorUserControl: 범용 상위 UserControl 탐색 헬퍼(특정 View 타입
      하드코딩 없음 — DeviceControlBase 재사용성 유지)

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개

### 2단계: 런타임 (Collector 에서 임의 Tag 에 알람 발생 필요 — 알람 설정된 Tag의
    임계값을 넘는 값을 Force Write 하거나 실제 값 변화로 트리거)
  [ ] Tag 바인딩된 카드에 알람 발생 시 카드 좌상단에 "⚠" 배지가 나타나는지 확인
      (레벨에 따라 HH/LL=빨강, H/L=노랑)
  [ ] 배지 클릭 → 상세 팝업(Tag명/메시지/레벨+상태/발생 시각) 표시 확인
  [ ] 팝업의 [✓ 확인(ACK)] 버튼 클릭 → 알람 상태가 "Acked" 로 전환되고 배지가
      반투명해지는지 확인, ACK 버튼이 사라지는지 확인
  [ ] 알람이 해소(Recovered)되면 배지가 사라지는지 확인
  [ ] 팝업 바깥을 클릭하면 팝업이 자동으로 닫히는지 확인(StaysOpen=False)
  [ ] 여러 카드에 동시에 알람이 발생해도 각자 독립적으로 배지/팝업이 동작하는지 확인
  [ ] ACK 요청이 알람을 발생시킨 Collector 로만 전송되는지 확인(다중 Collector
      환경에서 회귀 없음 — "발생 출처로만 전송" 원칙)

## 📖 사용 설명

화면 조작 방법:
  1. Tag 바인딩된 카드에 알람이 발생하면 좌상단에 "⚠" 배지가 자동으로 나타남
  2. 배지 클릭 → 알람 상세 정보(메시지·레벨·상태·발생 시각) 팝업 표시
  3. 미확인(Active) 상태면 팝업에 [✓ 확인(ACK)] 버튼이 표시됨 → 클릭하여 확인 처리
  4. 확인(Acked) 후에는 배지가 반투명하게 표시되어 "확인됨"을 구분할 수 있음
  5. 알람이 해소되면 배지가 자동으로 사라짐

확인 포인트:
  - 배지·팝업은 DeviceControlBase 공통 구현이라 모터/컨베이어/탱크/밸브/아이콘
    카드 전부에서 동일하게 동작함(장비별 추가 작업 불필요)
  - 알람 상태는 레이아웃 저장 대상이 아님(hmi-layout.json 에 포함 안 됨) —
    실시간 알람 스트림으로만 반영되는 값이기 때문

다음 Step 예고:
  HM-09에서는 아이콘 더블클릭 시 값 입력 다이얼로그를 띄워 SignalR
  Invoke("ForceWrite")로 원격 강제쓰기를 수행하는 제어 기능을 구현합니다.

### HM-09 구현 내역 (코드 완료 — 2026-07-19, 빌드 확인 대기)

```
설계 원칙: Collector 는 이미 C-EX-13(2026-07-16 빌드 확인 완료)에서 IIoTHub.
ForceWrite(plcId,tagId,value,apiKey) 원격 메서드를 제공하고 있으므로, HMI 쪽은
① 이 Hub 메서드를 호출하는 얇은 래퍼(CollectorConnection/Manager) ②
Collector 자체 UI(ForceWriteDialog+StatusView 패턴)를 그대로 이식한 입력
다이얼로그 ③ 캔버스에서 카드를 더블클릭하면 ②를 여는 트리거, 3단으로만
구성하면 된다 — 검증(기능 활성화·API Key·Tag 존재/활성·값 형식)은 전부
Collector 측 ForceWriteService(C-15)에 위임되어 HMI 쪽에는 별도 검증 로직이
없다("Hub 는 위임만 한다" 원칙 그대로 클라이언트에도 적용).

신규 파일:
  Models/ForceWriteResult.cs
    ← Collector Core/Engine/ForceWriteService.cs 의
      "record ForceWriteResult(bool IsSuccess, string? Error)" 와 동일한 필드
      구조의 클라이언트측 DTO. SignalR JsonHubProtocol 기본 직렬화(camelCase)를
      그대로 왕복하므로 별도 JsonPropertyName 지정 없이 매핑된다.
  Views/LayoutCanvas/ForceWriteDialog.xaml(.cs)
    ← IIoT.Collector Views/Status/ForceWriteDialog.xaml(.cs) 이식(네임스페이스만
      변경, 동일 UI/동작) — Tag명·PLC정보 표시 + 값 입력 + API Key(PasswordBox,
      설정된 경우에만 필요) + 경고문 + 취소/쓰기 버튼. [쓰기] 클릭 시
      ResultValue/ResultApiKey 를 채우고 DialogResult=true 반환.

변경 파일 (전체 최종본 반영 완료):
  Core/Connection/CollectorConnection.cs
    ← ForceWriteAsync(plcId,tagId,value,apiKey) 추가 — Hub 미연결 시 자체적으로
      실패 반환, 그 외에는 _hub.InvokeAsync<ForceWriteResult>("ForceWrite", ...)
      결과를 그대로 반환(AcknowledgeAsync 와 동일한 예외 처리 패턴)
  Core/Connection/CollectorConnectionManager.cs
    ← ForceWriteAsync(collectorId,plcId,tagId,value,apiKey) 추가 — "발생 출처로만
      전송" 원칙 그대로 적용(AcknowledgeAlarmAsync 와 동일 패턴), 연결 없으면
      즉시 ForceWriteResult(false, 사유) 반환
  ViewModels/LayoutCanvasViewModel.cs
    ← ForceWriteAsync(node,value,apiKey) 공개 메서드 추가 — 커맨드가 아니라 일반
      메서드인 이유: 다이얼로그 표시/결과 MessageBox 는 View(코드비하인드) 책임,
      ViewModel은 Collector 위임만 담당(관심사 분리)
  Views/LayoutCanvas/LayoutCanvasView.xaml.cs
    ← OnCanvasMouseDown 에 더블클릭(e.ClickCount==2) 분기 추가 — 드래그 시작
      로직보다 먼저 검사해 더블클릭 시 드래그가 시작되지 않도록 분리
    ← _OpenForceWriteDialogAsync(node) 추가 — 미바인딩 카드는 안내 메시지만
      표시하고 종료, 바인딩된 카드는 ForceWriteDialog 표시 → 확인 시 ViewModel.
      ForceWriteAsync() 호출 → 결과를 MessageBox 로 표시(Collector StatusView.
      xaml.cs ForceWriteButton_Click 과 동일 패턴)

★ 범위 결정: API Key 입력은 Collector 자체 UI와 동일하게 "매 강제쓰기마다
  다이얼로그에서 직접 입력" 방식을 그대로 재사용한다 — HMI 자체에 API Key를
  저장/기억하는 기능은 두지 않는다(평문 저장 회피). Security 정책 전반(예:
  Key 재사용성 개선, 화면별 권한 등)은 HM-12에서 별도 검토 예정.

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개

### 2단계: 런타임 (Collector 의 settings.json ForceWrite.Enabled=true 필요,
    Security.ForceWriteApiKey 설정 시 해당 값도 미리 확인해 둘 것)
  [ ] Tag 바인딩되지 않은 카드를 더블클릭 → "Tag가 바인딩되어 있지 않습니다" 안내
      메시지만 표시되고 다이얼로그는 뜨지 않는지 확인
  [ ] Tag 바인딩된 카드를 더블클릭 → ForceWriteDialog 표시(Tag명·PLC정보 확인)
  [ ] 값 입력 후 [쓰기] 클릭(API Key 미설정 시 빈 칸으로 두어도 통과되는지 확인)
      → "쓰기 성공" MessageBox 표시 확인
  [ ] 실제 PLC(또는 Virtual 드라이버) 값이 반영되는지 Collector 쪽에서 확인
  [ ] Security.ForceWriteApiKey 가 설정된 경우: API Key 를 틀리게 입력하면
      "API Key 가 올바르지 않습니다" 형태의 실패 메시지가 표시되는지 확인
  [ ] 비활성 Tag/존재하지 않는 Tag 등 Collector 측 검증 실패 케이스에서도
      다이얼로그가 죽지 않고 실패 메시지가 정상 표시되는지 확인
  [ ] [취소] 클릭 시 아무 요청도 전송되지 않는지 확인
  [ ] 더블클릭 후에도 카드가 드래그되어 이동하지 않는지 확인(더블클릭이 드래그로
      오인되지 않는지 회귀 확인)

## 📖 사용 설명

화면 조작 방법:
  1. Tag 바인딩된 카드를 더블클릭
  2. 표시된 다이얼로그에서 쓸 값(Raw 값 기준) 입력
  3. Collector 에 API Key 가 설정되어 있다면 API Key 도 함께 입력
  4. [쓰기] 클릭 → 결과(성공/실패)가 즉시 안내됨

확인 포인트:
  - 값은 Raw 값 기준으로 입력(스케일 역변환 없음 — Collector 다이얼로그와 동일 안내문)
  - API Key 는 HMI 에 저장되지 않으며 매번 직접 입력해야 함
  - 강제쓰기 발생은 Collector 에서 다른 연결 클라이언트(다른 HMI 화면·Monitor 등)
    에도 "ForceWriteResult" Push 로 즉시 알려지지만(C-EX-13), 이번 Step에서는
    이 Push 를 구독해 화면에 반영하는 기능은 포함하지 않음(필요 시 후속 검토)

다음 Step 예고:
  HM-10에서는 다중 화면(페이지) 관리를 탭/트리 형태로 개선할 필요가 있는지
  검토합니다 — HM-07의 화면 관리 바(콤보박스+이름 편집)로 이미 상당 부분
  충족되어 있어, 착수 시 추가 개선 필요성부터 재확인할 예정입니다.

### HM-10 구현 내역 (코드 완료 — 2026-07-19, 빌드 확인 대기)

```
★ 사용자 확인: HM-10 착수 전 "HM-07 콤보박스로 기능적으로 충분 vs 탭 바로 교체
  vs 트리 패널 신설" 3안을 제시했고, 사용자가 "탭 바 형태로 교체(권장)"를 선택함
  (화면이 여러 개일 때 한눈에 보고 클릭 한 번으로 전환 가능하도록).

설계 원칙: 완전히 새로운 컨트롤을 만들지 않고 Pages/ActivePage(HM-07)의 기존
데이터 구조와 커맨드(AddPage/DeletePage/SaveLayout)는 그대로 유지한 채, "화면을
어떻게 선택/이름 편집하는가"의 UI 부분만 콤보박스+텍스트박스 → 탭 바로 교체했다.
탭 클릭(단일)=화면 전환, 탭 더블클릭=이름 편집 모드(인라인 TextBox) — MainWindow
의 5탭 필(pill) 스타일(AccFaintBrush/AccBrush, HM-Base-2)과 동일한 강조색을
재사용해 앱 전체의 탭 시각 언어를 통일했다.

변경 파일 (전체 최종본 반영 완료):
  ViewModels/LayoutCanvasViewModel.cs
    ← LayoutPageViewModel 에 IsActive(bool, 탭 강조 표시)/IsEditingName(bool,
      더블클릭 시 인라인 이름 편집 전환) 2개 필드 추가
    ← OnActivePageChanged 에서 Pages 전체를 순회하며 IsActive 갱신(정확히 1개만
      true) 하는 로직 추가
    ← SelectPage(LayoutPageViewModel? page) 공개 메서드 추가 — SelectNode(node)
      와 동일한 패턴(커맨드가 아닌 일반 메서드)으로 View 의 탭 클릭 핸들러가 호출
  Views/LayoutCanvas/LayoutCanvasView.xaml
    ← Row 0 의 ComboBox+TextBox 를 ItemsControl(ItemsSource=Pages,
      ItemTemplate=PageTabTemplate) 기반 가로 탭 바로 교체, ScrollViewer 로 감싸
      화면이 많아져도 좌우 스크롤로 전부 접근 가능(➕/🗑/💾 버튼은 우측에 고정)
    ← PageTabTemplate(신규, UserControl.Resources) — Border(활성 시
      AccFaintBrush 배경)+TextBlock(활성 시 AccBrush 글자색, 편집 모드 시 숨김)+
      TextBox(편집 모드 시에만 표시, BoolToVisibility 컨버터로 토글)
  Views/LayoutCanvas/LayoutCanvasView.xaml.cs
    ← PageTab_MouseLeftButtonDown — ClickCount==2 면 IsEditingName=true 설정 후
      Dispatcher.BeginInvoke 로 지연시켜 인라인 TextBox 에 포커스(레이아웃 갱신
      후 Visibility 가 Visible 로 바뀐 다음에 포커스를 줘야 하므로), 단일 클릭이면
      _vm.SelectPage(page) 호출
    ← PageTabNameBox_LostFocus — 포커스 잃으면 IsEditingName=false(편집 확정)
    ← PageTabNameBox_KeyDown — Enter=포커스 해제(LostFocus 유도), Esc=편집 취소

★ 범위 결정: 화면 삭제는 여전히 "현재 활성 화면 삭제"(🗑 화면 삭제 버튼) 방식을
  유지한다 — 탭마다 개별 ✕ 닫기 버튼은 추가하지 않음(간결함 우선, 과도한 확장
  자제). 트리 패널(라인별/구역별 그룹핑)은 이번 Step 범위에 포함하지 않음 —
  필요성이 실제로 대두되면 별도 후속 항목으로 검토.

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개

### 2단계: 런타임
  [ ] [🎨 레이아웃 편집] 탭 → 화면 관리 바가 콤보박스가 아닌 탭 바 형태로
      표시되는지 확인(기본 화면 1개가 탭으로 보임)
  [ ] [➕ 새 화면] 클릭 → 새 탭이 추가되고 자동으로 활성화(강조색)되는지 확인
  [ ] 탭 클릭(단일) → 해당 화면으로 전환되고 강조색이 클릭한 탭으로 이동하는지 확인
  [ ] 탭 더블클릭 → 이름이 인라인 TextBox 로 바뀌고 포커스+전체 선택되는지 확인
  [ ] 이름 수정 후 Enter → 편집 모드 종료 + 탭에 새 이름이 반영되는지 확인
  [ ] 이름 수정 중 Esc → 편집 모드만 종료(입력 중이던 값은 Text 바인딩상 이미
      반영되었을 수 있음 — 되돌리기 기능은 없음, 필요 시 후속 검토)
  [ ] 탭 이름 편집 후 다른 곳 클릭(포커스 아웃) → 자동으로 편집 모드 종료되는지 확인
  [ ] 화면을 5개 이상 추가 → 탭 바에 가로 스크롤이 생기고 ➕/🗑/💾 버튼은 계속
      우측에 고정 표시되는지 확인
  [ ] [🗑 화면 삭제] → 현재 활성 탭이 삭제되고 다른 탭으로 자동 전환되는지 확인
      (마지막 1개 남으면 버튼 비활성화 — HM-07과 동일)
  [ ] [💾 레이아웃 저장] 후 재시작 → 탭 이름·순서·활성 화면이 그대로 복원되는지 확인
  [ ] 테마 전환(7종) 시 탭 강조색(AccFaintBrush/AccBrush)이 모든 테마에서 깨지지
      않고 정상 반영되는지 확인(DynamicResource 사용 확인)

## 📖 사용 설명

화면 조작 방법:
  1. 화면 관리 바의 탭을 클릭하면 해당 화면으로 즉시 전환됨
  2. 탭을 더블클릭하면 이름을 바로 수정할 수 있음(Enter=확정, Esc=취소, 포커스
     아웃 시 자동 확정)
  3. [➕ 새 화면]/[🗑 화면 삭제]/[💾 레이아웃 저장] 버튼은 이전과 동일하게 동작

확인 포인트:
  - 데이터 구조·저장 파일(hmi-layout.json)·커맨드는 HM-07과 완전히 동일 —
    이번 Step은 화면 전환/이름편집 "UI 표현 방식"만 바꾼 것
  - 탭 강조 스타일은 MainWindow 상단 5탭(HM-Base-2)과 동일한 색상 키를 재사용해
    시각적으로 통일됨

다음 Step 예고:
  HM-11에서는 자체 SignalR Hub + wwwroot 를 호스팅하여 웹 브라우저에서도
  동일한 화면을 볼 수 있도록 확장합니다(Collector C-11/wwwroot 패턴 재사용).

### HM-11 구현 내역 (코드 완료 — 2026-07-19, 빌드 확인 대기)

```
★ 리서치 기반 구현: Collector C-11(SignalRHostService.cs+wwwroot/index.html)과
  Monitor MN-05(MonitorHostService.cs, FrameworkReference 재도입 FIX)의 실제
  코드를 먼저 조사한 뒤, 두 프로그램의 검증된 패턴을 그대로 조합해 구현했다
  (새 접근 방식을 고안하지 않음 — 이미 두 번 검증된 패턴 재사용).

① FrameworkReference 재도입 (HM-Base-0 에서 의도적으로 미뤄뒀던 부분)
  IIoT.HMI.csproj
    ← <FrameworkReference Include="Microsoft.AspNetCore.App" /> 추가
    ← ★ Monitor MN-05 FIX 그대로 적용: Microsoft.Extensions.DependencyInjection
      의 "8.0.1 명시 버전 고정" PackageReference 를 완전히 제거 — HMI 는 이제
      ①Collector 접속 클라이언트(SignalR.Client) + ②웹 브라우저용 자체 Hub
      호스팅을 동시에 수행하므로, Monitor 가 겪었던 것과 동일한 NU1605
      다운그레이드 충돌 위험이 있다. 8.0.1 고정을 유지한 채 FrameworkReference
      를 추가하면 재발 가능 — 반드시 함께 제거해야 한다(Monitor Ver History
      MN-05 참조).
    ← <Content Include="wwwroot\**\*"> 복사 항목 추가(Collector C-11 패턴)

② 설정 — Core/Config/HmiSettings.cs
    ← HmiSettings.Web(WebHostSettings) 추가 — Enabled(기본 true)/Port(기본 7880,
      Collector 7878·Monitor 7879 와 겹치지 않게 선택). Monitor MonitorSettings
      의 WebHostSettings 와 동일 구조.

③ 신규 파일 (Core/Web/)
  Core/Web/WebNodeDto.cs
    ← 웹 페이지가 카드 1개를 그리는 데 필요한 경량 DTO(구조+실시간 상태 결합).
      AbstractLayoutNode(WPF ObservableObject)를 직접 직렬화하지 않고 이 DTO 로
      변환 — REST(/api/layout)와 SignalR("NodesChanged" Push) 양쪽에서 동일하게 사용.
  Core/Web/HmiWebHub.cs
    ← 읽기 전용 표시 전용 Hub(빈 클래스, 클라이언트 호출 메서드 없음). ACK/
      ForceWrite 는 이번 Step 범위 밖(아래 "★ 범위 결정" 참조).
  Core/Web/HmiWebHostService.cs (DI 싱글턴)
    ← Collector SignalRHostService.cs 패턴 그대로 준용: WebApplication 빌드→
      CORS(allow-all)+AddSignalR→UseUrls(포트)+UseWebRoot(wwwroot)→UseCors+
      UseDefaultFiles+UseStaticFiles→MapHub("/hmi-hub")+MapGet("/health")+
      MapGet("/api/layout")→별도 non-pool Thread("HMI-WebHost")에서 블로킹
      _app.Run() 실행(Task.Run 아님 — ASP.NET Core 는 전용 스레드 필요)
    ← StartAsync() 시작 시 _settingsLoader.LoadAsync() 를 자체적으로 먼저
      호출 — hmi.json 로드가 [Collector 관리] 탭 Loaded 시점(HM-01)에 이루어
      지므로 이 서비스가 그보다 먼저 시작되어도 최신 설정을 보장한다
    ← _WireRelay(): LayoutCanvasViewModel.Nodes.CollectionChanged + 개별 노드의
      PropertyChanged 를 구독해 "dirty 플래그"만 세운다(Monitor MonitorHostService
      의 _WireRelay relay 패턴과 동일 원칙)
    ← 500ms 주기 브로드캐스트 루프 — dirty 일 때만 전체 노드 스냅샷을
      "NodesChanged" 로 Push(코일레싱 — 폴링 사이클마다 직렬화/Push 반복 방지)
    ← _BuildSnapshotAsync(): Dispatcher.InvokeAsync 로 UI 스레드에서 스냅샷을
      만든다(Nodes 는 WPF ObservableCollection — 비 UI 스레드에서 직접 열거하면
      스레드 안전성 문제 위험. 프로젝트의 ".Invoke 금지" 규칙은 블로킹 .Invoke()
      에 대한 것이므로 비-블로킹 await 가능한 InvokeAsync() 를 사용해 규칙의
      취지를 지키면서 안전하게 마샬링)

④ 신규 wwwroot/index.html
    ← Collector wwwroot/index.html 과 동일 스타일(다크 테마, 순수 CSS/JS,
      SignalR CDN 1개만 의존, 별도 프레임워크 없음)
    ← 최초 GET /api/layout 로 스냅샷 조회 후 render(), 이후 "NodesChanged" 수신
      시마다 전체 재렌더(증분 갱신 대신 — 카드 수가 많지 않은 HMI 특성상 충분히
      가볍고 구현이 단순함)
    ← 카드 렌더링: 카테고리 색상 바 + 아이콘 글리프 + 라벨 + 값 텍스트(바인딩
      시) + Quality 상태 점 + 알람 배지(⚠, 레벨별 색상) — WPF DeviceControlBase
      카드와 동일한 시각 요소를 웹에서도 재현(단, 애니메이션·팝업·ACK 는 없음)

⑤ 변경 파일
  App.xaml.cs
    ← HmiWebHostService DI 등록(LayoutCanvasViewModel 뒤)
    ← MainWindow.Show() 이후 win.Loaded 에서 HmiWebHostService.StartAsync() 호출
      (★ HMI 최초로 "win.Loaded 오케스트레이션" 패턴 도입 — 이전까지는 각 View
      가 각자 Loaded 에서 독립 초기화했으나, 웹 서버 시작은 특정 View 에 속하지
      않으므로 App 레벨에서 직접 기동. Collector App.xaml.cs 의 win.Loaded 패턴 참고)
    ← OnExit 에 HmiWebHostService.DisposeAsync() 5초 타임아웃 정리 추가
      (CollectorConnectionManager 와 동일 방식)

★ 범위 결정(1차 — 읽기 전용):
  - ACK/ForceWrite 는 웹에서 제공하지 않는다(HmiWebHub 는 클라이언트 호출
    메서드가 없는 빈 Hub). 필요해지면 별도 후속 Step 으로 검토(아래 "🔧 후속·
    보류 항목"에 등록).
  - 웹은 WPF 의 "현재 활성 화면(ActivePage) 1개"만 미러링한다 — 웹에서 독립적
    으로 화면(페이지)을 선택하는 기능은 없다. 필요해지면 Pages 목록도 함께
    REST/Push 로 노출하는 방식으로 후속 확장 가능.
  - 카드 애니메이션(회전/흐름/수위/개폐, HM-06)과 알람 상세 팝업/ACK(HM-08),
    ForceWrite 다이얼로그(HM-09)는 웹에 없다 — 값/상태를 "보여주기"만 한다.

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드 (★ 이번 Step은 FrameworkReference 추가로 인한 위험이 가장 큼)
  [ ] Clean → Rebuild → 오류 0개
  [ ] NU1605(다운그레이드 오류) 발생 시: 오류 메시지가 요구하는 최소 버전을
      확인해 해당 패키지를 PackageReference 로 그 버전 이상 명시 재고정
      (Monitor MN-05 FIX 사례와 동일 — 이번에는 애초에 명시 고정을 제거했으므로
      발생 가능성은 낮지만, 만약 발생하면 이 절차를 따를 것)
  [ ] CS0246 'WebApplication'/'Results' 등 찾을 수 없음 → FrameworkReference
      누락 여부 재확인
  [ ] wwwroot\index.html 이 빌드 출력 폴더(bin\Debug\net8.0-windows\wwwroot\)에
      복사되었는지 확인(Content Include 누락 시 웹 접속 404)

### 2단계: 런타임
  [ ] IIoT.HMI 실행 → 로그에 "웹 표시 서버 시작 — http://localhost:7880" 출력 확인
  [ ] 브라우저에서 http://localhost:7880 접속 → 다크 테마 페이지 로드, 헤더에
      "연결됨"(녹색 점) 표시 확인
  [ ] WPF [🎨 레이아웃 편집] 탭의 현재 활성 화면 카드가 웹 페이지에도 동일한
      위치·아이콘·라벨로 표시되는지 확인
  [ ] WPF 에서 카드를 드래그해 위치를 옮기면 약 0.5초 이내에 웹 페이지에도
      반영되는지 확인(500ms 코일레싱 브로드캐스트)
  [ ] WPF 에서 Tag 바인딩된 카드의 값이 갱신되면 웹 페이지의 값 텍스트/상태
      점도 함께 갱신되는지 확인
  [ ] WPF 에서 알람이 발생하면 웹 페이지의 카드에도 ⚠ 배지가 나타나는지 확인
      (배지에 마우스 올리면 메시지 툴팁 표시)
  [ ] WPF 에서 화면(페이지) 탭을 전환하면 웹 페이지도 그 화면의 카드로
      바뀌는지 확인(웹은 항상 WPF 의 현재 활성 화면만 미러링)
  [ ] 브라우저 새로고침 → 최초 스냅샷(GET /api/layout)이 즉시 반영되는지 확인
  [ ] WPF 프로그램 종료 → 5초 이내에 프로세스가 정상 종료되는지 확인(Kestrel
      graceful shutdown 대기 포함)
  [ ] hmi.json 에서 Web.Enabled=false 로 설정 후 재시작 → 웹 서버가 시작되지
      않고 로그에 "웹 표시 기능 비활성화" 만 출력되는지 확인(WPF 앱 자체는
      정상 동작)

## 📖 사용 설명

화면 조작 방법:
  1. IIoT.HMI 실행 시 자동으로 웹 표시 서버가 함께 시작됨(기본 포트 7880)
  2. 같은 네트워크의 다른 PC/모바일 브라우저에서 http://{HMI PC IP}:7880 접속
  3. WPF 쪽에서 조작하는 화면(카드 배치·값·알람)이 자동으로 웹에도 반영됨
  4. 웹 페이지는 읽기 전용 — 클릭/드래그로 조작되지 않음(추후 확장 여지)

확인 포인트:
  - 포트/활성화 여부는 {HMI 실행파일}\Config\hmi.json 의 Web.Enabled/Web.Port
    로 조정(직접 편집 — 아직 UI 편집 화면 없음, "설정 UI" 후속 항목 참조)
  - 웹 화면은 WPF 의 현재 활성 화면 1개만 미러링하며, 애니메이션·팝업·ACK·
    ForceWrite 는 제공하지 않음(1차 범위 — 위 "★ 범위 결정" 참조)

다음 Step 예고:
  HM-12에서는 ForceWrite API Key 입력 확인 등 보안 강화를 검토합니다
  (Collector Security.ForceWriteApiKey 재사용).

### HM-12 구현 내역 (코드 완료 — 2026-07-19, 빌드 확인 대기)

```
★ 사용자 확인: HM-12 범위가 핸드오프상 "Security 정책 전반"으로만 열려 있어
  구체 항목을 물었고, 사용자가 3가지 모두 선택함 — ① 화면 잠금 모드(권장)
  ② 활성 알람 중 강제쓰기 경고 ③ 세션 내 API Key 임시 기억.

① 화면 잠금 모드
  Core/Config/HmiSettings.cs
    ← ForceWriteSecuritySettings(DefaultLocked, 기본 true) 추가 — 앱 시작 시
      잠금 기본 상태만 설정(토글 자체는 파일에 저장 안 함)
  ViewModels/LayoutCanvasViewModel.cs
    ← HmiSettingsLoader 생성자 주입 추가(화면 잠금 기본값 조회용)
    ← IsForceWriteLocked(기본 true) + LockButtonLabel(계산 프로퍼티, 🔒/🔓 텍스트) +
      ToggleForceWriteLockCommand 추가. InitializeAsync() 에서 hmi.json 의
      ForceWriteSecurity.DefaultLocked 를 초기값으로 적용(다른 View 의 Loaded
      순서와 무관하도록 자체적으로 LoadAsync 재호출 — HmiWebHostService 와 동일 패턴)
  Views/LayoutCanvas/LayoutCanvasView.xaml
    ← 툴바(Row 1)에 잠금 토글 버튼 추가(선택 여부와 무관하게 항상 표시)
  Views/LayoutCanvas/LayoutCanvasView.xaml.cs
    ← _OpenForceWriteDialogAsync() 최상단에 IsForceWriteLocked 체크 추가 —
      잠금 상태면 안내 메시지만 표시하고 다이얼로그를 열지 않음
  ★ 범위 결정: 잠금은 ForceWriteDialog 오픈만 차단한다(드래그/삭제/바인딩 등
    다른 편집 동작은 잠금과 무관 — 사용자가 승인한 옵션 설명 그대로).

② 활성 알람 중 강제쓰기 경고
  Views/LayoutCanvas/ForceWriteDialog.xaml
    ← AlarmWarningPanel(Grid.Row=4, 신규, 기본 Collapsed) 추가 — 경고 문구
      TextBlock + "위험을 인지했으며 계속 진행합니다" CheckBox
    ← [쓰기] 버튼에 x:Name="OkButton" 부여(코드비하인드에서 IsEnabled 제어 위해)
    ← Window Height 320→380(경고 패널 표시 시에도 여유 있게)
  Views/LayoutCanvas/ForceWriteDialog.xaml.cs
    ← 생성자에 hasActiveAlarm/alarmMessage 매개변수(기본값 false/"") 추가 —
      true 면 AlarmWarningPanel 표시 + OkButton.IsEnabled=false(체크박스 체크 전까지)
    ← ChkAlarmAck_CheckedChanged 핸들러 추가 — 체크 상태에 따라 OkButton.IsEnabled 갱신
  Views/LayoutCanvas/LayoutCanvasView.xaml.cs
    ← ForceWriteDialog 생성 시 node.HasActiveAlarm/node.AlarmMessage 전달

③ 세션 내 API Key 임시 기억
  ViewModels/LayoutCanvasViewModel.cs
    ← _apiKeyCache(Dictionary<string,string>, 메모리 전용 — hmi.json/디스크에는
      절대 저장 안 함) 추가. ForceWriteAsync() 가 성공한 호출의 apiKey 만
      CollectorId 기준으로 캐싱(실패한 값은 캐싱 안 함 — 다음 시도를 방해하지
      않도록). GetCachedApiKey(collectorId) 공개 메서드로 조회
  Views/LayoutCanvas/ForceWriteDialog.xaml.cs
    ← PrefillApiKey(apiKey) 공개 메서드 추가 — PasswordBox.Password 는 보안상
      XAML Binding 불가(WPF 표준 제약)라 코드비하인드에서만 설정 가능
  Views/LayoutCanvas/LayoutCanvasView.xaml.cs
    ← dialog.PrefillApiKey(_vm.GetCachedApiKey(node.BoundCollectorId)) 호출 —
      다이얼로그 표시 직전에 캐시된 값이 있으면 미리 채워 넣음

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개

### 2단계: 런타임 — 화면 잠금
  [ ] [🎨 레이아웃 편집] 탭 → 툴바에 "🔒 잠금(더블클릭 차단)" 버튼이 기본 표시되는지 확인
  [ ] 잠금 상태에서 Tag 바인딩된 카드 더블클릭 → 안내 메시지만 뜨고 ForceWriteDialog
      가 열리지 않는지 확인
  [ ] 잠금 버튼 클릭 → "🔓 해제(강제쓰기 가능)"로 바뀌는지 확인, 이후 더블클릭 시
      다이얼로그가 정상적으로 열리는지 확인
  [ ] hmi.json 에서 ForceWriteSecurity.DefaultLocked=false 로 설정 후 재시작 →
      시작 시 해제 상태로 시작하는지 확인

### 3단계: 런타임 — 활성 알람 중 강제쓰기 경고
  [ ] 알람 없는 정상 Tag 카드 더블클릭 → 기존과 동일하게 경고 패널 없이 [쓰기]
      버튼이 바로 활성화되어 있는지 확인(회귀 없음)
  [ ] 알람이 걸려 있는 Tag 카드 더블클릭(잠금 해제 상태) → 빨간 경고 문구 +
      체크박스가 표시되고 [쓰기] 버튼이 비활성화 상태인지 확인
  [ ] 체크박스 체크 → [쓰기] 버튼이 활성화되는지 확인, 체크 해제 시 다시
      비활성화되는지 확인

### 4단계: 런타임 — 세션 내 API Key 임시 기억
  [ ] Security.ForceWriteApiKey 가 설정된 Collector 대상 카드에 API Key 입력 후
      강제쓰기 성공 → 같은 Collector 의 다른 카드를 더블클릭했을 때 API Key
      입력란에 이전 값이 자동으로 채워져 있는지 확인
  [ ] API Key 를 틀리게 입력해 강제쓰기 실패 → 그 값은 캐싱되지 않았는지 확인
      (다음에 다시 열었을 때 빈 칸이거나 이전 성공값이 유지되는지)
  [ ] 프로그램 재시작 → 캐시가 초기화되어 API Key 입력란이 다시 비어 있는지 확인
      (디스크에 저장되지 않음 확인)

## 📖 사용 설명

화면 조작 방법:
  1. [🎨 레이아웃 편집] 탭 툴바의 잠금 버튼으로 강제쓰기 다이얼로그 오픈을
     허용/차단할 수 있음(기본은 잠김 — 안전 우선)
  2. 알람이 걸린 Tag 를 강제쓰기하려 하면 위험 경고와 함께 추가 확인이 필요함
  3. 같은 Collector 에 여러 번 강제쓰기할 때는 최초 성공 이후 API Key 를
     다시 입력하지 않아도 됨(이번 실행 세션에서만 유지, 재시작 시 초기화)

확인 포인트:
  - 잠금 기본값은 {HMI 실행파일}\Config\hmi.json 의 ForceWriteSecurity.DefaultLocked
    로 조정(직접 편집 — 아직 UI 편집 화면 없음)
  - API Key 세션 캐시는 메모리에만 존재하며 어떤 파일에도 저장되지 않음

다음 Step 예고:
  HM-Base-0~HM-12 전체를 사용자가 빌드·런타임 확인하면 IIoT.HMI 의 1차
  기본구조가 완성됩니다. 이후 HM-EX(히스토리 트렌드/캡처·리포트/다중 모니터)
  후속 검토 여부를 판단하거나, 전체 개발 순서(2단계 확정)에 따라 IIoT.Sequence
  로 넘어갈 수 있습니다.

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
 HM-11-EX 웹에서 ACK/ForceWrite 지원 + 웹 자체 화면(페이지) 선택 기능
   (2026-07-19, HM-11 1차 범위를 읽기 전용으로 한정하며 등록 — 착수 시
    HmiWebHub 에 클라이언트 호출 메서드 추가 + 보안(HM-12) 검토 선행 필요)
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

## ✅ C-SET-01: IIoT.Collector 환경설정 탭 (코드 완료 — 2026-07-20, 빌드 확인 대기)

```
배경: "설정(Settings) UI 편집 화면" 후속 기능(위 "⭐ 신규 후속 기능: 설정 UI 편집
화면" 절, task #5) 중 ① 개별 프로그램 로컬 탭을 Collector 부터 우선 착수
(사용자 확인 완료, 2026-07-20). Manager 원격 통합 설정관리 화면(②)은 HM-22 로
그대로 유지 — 개별 프로그램 로컬 탭이 전부 갖춰진 뒤 착수 예정.

경로: Collector\IIoT.Collector\
추가 파일:
  ViewModels/SettingsViewModel.cs
    ← CollectorSettings(settings.json) 11개 섹션을 좌측 네비게이션 + 우측 폼으로 편집
      (일반/저장소·SQLite·InfluxDB·MQTT/SignalR/재연결/알림·SMTP·Webhook/
       강제쓰기/이상값필터/가상Tag/보안/데이터보존/DB백업)
    ← ActiveSectionIndex(0~10) — 기존 MainViewModel.SwitchTab 패턴 그대로 재사용
    ← IsInfluxDbProvider(bool) — Storage.Provider(string) 섹션 전환용 래퍼
      (S-25 IsDisabled=!IsEnabled 와 동일 "양쪽 bool 노출" 기법)
    ← RetryIntervalsSecText / AllowedOriginsText — int[]/string[] 텍스트 변환 래퍼
    ← SaveCommand: _ValidateAll() 통과 시에만 CollectorSettingsLoader.SaveAsync() 호출
      (포트 범위·HH:mm 형식·필수값 등 검사, 실패 시 저장 중단 + 오류 메시지 표시)
    ← ReloadCommand: 디스크 settings.json 다시 로드(편집 취소)
    ← RegenerateCollectorIdCommand: 새 CollectorId 발급(저장 전까지는 미반영)
  Views/Settings/SettingsView.xaml(.cs)
    ← 상단 "재시작 필요" 안내 배너(항상 표시) + 좌측 11개 섹션 네비게이션(GhostBtn,
      기존 탭바와 동일 패턴) + 우측 스크롤 폼 + 하단 저장/다시불러오기 바
    ← ModernPasswordBox: 이 화면 전용 로컬 스타일(PropInput 과 동일 외형) —
      PasswordBox 는 Password 프로퍼티 바인딩 불가하므로 공용 테마 대신
      이 파일에 한정해 정의(공용 IIoT.UI.Themes 라이브러리 미변경)
    ← 코드비하인드에서 PasswordBox 5개(InfluxDB Token · MQTT 비밀번호 ·
      SMTP 비밀번호 · ForceWrite API Key · REST API Key)를
      ViewModel.Settings 하위 필드와 직접 동기화(_suppressSync 가드로
      Initialize/Reload 시 재귀 방지)

변경 파일 (기존 코드는 그대로 두고 신규 부분만 추가):
  Core/Config/CollectorSettings.cs
    ← CollectorSettingsLoader.SaveAsync() 추가 (기존 LoadAsync 와 동일 옵션으로 직렬화)
    ← CollectorSettingsLoader.GenerateNewCollectorId() 추가 (기존 private
      _GenerateCollectorId() 를 감싸는 public static 래퍼 — 기존 메서드 미변경)
  MainWindow.xaml / MainWindow.xaml.cs / MainViewModel.cs
    ← 탭바에 "⚙ 환경설정" 버튼(인덱스 5) 추가, SettingsViewHost(ContentControl) 추가
    ← IsSettingsTab(ActiveTabIndex==5) 추가 (기존 IsDeviceTab 패턴과 동일)
  App.xaml.cs
    ← SettingsViewModel/SettingsView DI 등록, MainWindow 팩토리 인자 추가
    ← win.Loaded: CollectorSettingsLoader.LoadAsync() 직후 SettingsViewModel.Initialize() 호출

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개
  [ ] PasswordBox 스타일(ModernPasswordBox)이 SettingsView.xaml 내부에서만 참조되는지 확인
      (공용 IIoT.UI.Themes 프로젝트는 수정하지 않았음)

### 2단계: 런타임
  [ ] F5 실행 → 탭바 "⚙ 환경설정" 클릭 → 좌측 11개 섹션 목록 + 우측 폼 표시
  [ ] 좌측 섹션 클릭 시마다 우측 폼이 해당 섹션으로 전환
  [ ] "일반" 섹션: CollectorId 표시, [🎲 재발급] 클릭 → 새 값으로 즉시 교체(아직 미저장 상태)
  [ ] "저장소" 섹션: "InfluxDB 사용" 체크 시 SQLite 카드 숨김 + InfluxDB 카드 표시(반대도 동일)
  [ ] InfluxDB Token / MQTT 비밀번호 / SMTP 비밀번호 / 강제쓰기 API Key / REST API Key
      입력란이 모두 마스킹(PasswordBox)으로 표시되는지 확인
  [ ] 포트 필드에 범위를 벗어난 값(예: 99999) 입력 후 [💾 저장] → 저장 거부 + 오류 메시지 표시
  [ ] 정상 값으로 [💾 저장] → 하단에 "저장 완료 (HH:mm:ss) — 재시작 필요" 메시지
  [ ] {실행파일경로}\Config\settings.json 파일 내용이 화면에서 입력한 값대로 갱신됐는지 확인
  [ ] [↻ 다시 불러오기] → 저장 전 임시로 고친 값이 디스크 값으로 되돌아감
  [ ] Collector 재시작 → 환경설정 탭에 마지막 저장값이 그대로 복원되는지 확인

## 📖 사용 설명

화면 조작 방법:
  1. Collector 실행 → 탭바 [⚙ 환경설정] 클릭
  2. 좌측 목록에서 편집할 섹션 선택(일반/저장소/SignalR/재연결/알림/강제쓰기/
     이상값필터/가상Tag/보안/데이터보존/DB백업)
  3. 값 수정 후 우측 하단 [💾 저장] 클릭 — 유효성 오류가 있으면 저장이 거부되고
     화면 하단에 어떤 값이 잘못됐는지 표시됩니다
  4. 저장 성공 시 "재시작해야 적용됨" 안내가 함께 표시됩니다 — Collector 를
     재시작해야 실제 수집 동작에 반영됩니다(파일 감시 자동 재시작(C-08)과는 무관 —
     .signal 은 device.json/collect.json 변경만 감지하며 settings.json 은
     해당하지 않음)
  5. 실수로 값을 고쳤다면 저장하지 않은 상태에서 [↻ 다시 불러오기]로 취소 가능

확인 포인트:
  - CollectorId 재발급은 화면에만 반영되고 [저장]을 눌러야 파일에 기록됩니다
  - 저장소 섹션의 SQLite/InfluxDB 카드는 "InfluxDB 사용" 체크박스 하나로 전환됩니다
  - InfluxDB Token 등 민감정보는 화면에서 항상 마스킹 표시됩니다

다음 진행:
  Manager(Resource 1개 항목만 — 범위가 작아 빠르게 완료 가능) → Monitor(Web
  1개 항목만) 순으로 동일 패턴(좌측 네비게이션 + 우측 폼)의 로컬 환경설정 탭을
  이어서 추가. 4개 프로그램 로컬 탭이 모두 끝나면 HM-22(Manager 원격 통합
  설정관리 화면) 착수.

---

## ✅ C-SET-01 후속: IIoT.Manager 환경설정 탭 (코드 완료 — 2026-07-20, 빌드 확인 대기)

```
배경: Collector C-SET-01 과 동일한 개별 프로그램 로컬 설정탭 트랙의 2번째.
Manager 는 manager.json 의 Processes/Deploy/Schedules 가 이미 각자 탭
([프로세스]/[배포]/[스케줄])에서 편집 가능하므로, UI 가 없던 Resource
섹션(CpuWarnPercent·MemoryWarnMb) 1개만 신규 탭으로 추가하면 된다 —
Collector 대비 범위가 작아 좌측 섹션 네비게이션 없이 단일 카드로 구성.

★ 확인된 사실(설계에 반영): ManagerSettingsLoader.Settings.Resource 객체는
ManagerMainViewModel.InitializeAsync() 에서 각 ProcessCardViewModel 생성자에
"참조로" 그대로 전달된다(ProcessCardViewModel.cs 의 _resource 필드). 따라서
이번 화면에서 CpuWarnPercent/MemoryWarnMb 값을 바꾸면 [저장] 을 누르기 전이라도
이미 실행 중인 카드의 경고 감시에 즉시 반영된다 — Collector 와 달리 재시작
불필요(사용 설명에 명시). 단, [↻ 다시 불러오기] 는 LoadAsync() 로 완전히 새
ManagerSettings 객체를 생성하므로 이 경우에는 재시작 전까지 카드가 이전 값을
참조하는 상태로 남는다(드문 경로라 우선 기록만, 필요 시 후속 개선).

경로: Manager\IIoT.Manager\
추가 파일:
  ViewModels/SettingsViewModel.cs — Settings(=ManagerSettingsLoader.Settings) 노출,
    SaveCommand(0 이상 검증 후 ManagerSettingsLoader.SaveAsync() 호출 — 기존에
    이미 있던 메서드라 신규 추가 불필요) / ReloadCommand
  Views/Settings/SettingsView.xaml(.cs) — DeployView.xaml 과 동일한 SectionCard
    스타일의 단일 카드(CPU%/메모리MB 2개 필드) + 저장/다시불러오기 + 상태 텍스트

변경 파일 (기존 코드는 그대로 두고 신규 부분만 추가):
  MainWindow.xaml / MainWindow.xaml.cs — 탭바에 "⚙ 환경설정"(인덱스 5, TabBtn5
    스타일) + SettingsHost(ContentControl) 추가 (기존 TabBtn0~4 패턴과 동일)
  ManagerMainViewModel.cs — IsSettingsTab(ActiveTabIndex==5) 추가,
    InitializeAsync() 에서 manager.json 로드 후 SettingsViewModel.Initialize() 호출
  App.xaml.cs — SettingsViewModel/SettingsView DI 등록, MainWindow 팩토리 인자 추가

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개

### 2단계: 런타임
  [ ] F5 실행 → 탭바 "⚙ 환경설정" 클릭 → CPU%/메모리MB 입력 카드 표시
  [ ] 값 변경 후 [💾 저장] → 하단에 "저장 완료" 메시지 + {실행파일경로}\Config\
      manager.json 의 Resource 값이 갱신됐는지 확인
  [ ] 음수 입력 후 저장 시도 → 저장 거부 + 오류 메시지 표시
  [ ] 저장 없이 값만 바꾼 상태에서 [프로세스] 탭으로 이동해 CPU 사용량이 높은
      프로그램을 실행 중이면, 낮춘 임계값 기준으로 경고가 즉시 발생하는지 확인
      (재시작 불필요 — 참조 공유 특성)
  [ ] [↻ 다시 불러오기] → 편집 취소되어 디스크 값으로 복원되는지 확인

## 📖 사용 설명

화면 조작 방법:
  1. Manager 실행 → 탭바 [⚙ 환경설정] 클릭
  2. CPU 사용률 경고(%) / 메모리 사용량 경고(MB) 값 입력 (0 이하 = 검사 안 함)
  3. [💾 저장] 클릭 — 이 값은 저장 여부와 무관하게 화면에서 바꾸는 즉시 이미
     실행 중인 프로세스 카드의 경고 감시에도 반영됩니다(파일에는 저장을 눌러야
     기록됨 — Manager 재시작 후에도 유지하려면 반드시 저장 필요)
  4. 실수로 값을 고쳤다면 [↻ 다시 불러오기]로 취소 가능

다음 진행: Monitor(Web Enabled/Port 1개 항목) 환경설정 탭.

---

## ✅ C-SET-01 후속: IIoT.Monitor 환경설정 탭 (코드 완료 — 2026-07-20, 빌드 확인 대기)

```
배경: Manager 와 마찬가지로 범위가 작다 — monitor.json 의 Collectors[]/
FavoriteTagKeys 는 이미 [Collector 관리] 탭/즐겨찾기 UI에서 편집 가능하므로,
Web(자체 SignalR Hub Enabled/Port — 브라우저 연동) 1개 섹션만 신규 탭으로 추가.

경로: Monitor\IIoT.Monitor\
추가 파일:
  ViewModels/SettingsViewModel.cs — Settings(=MonitorSettingsLoader.Settings) 노출,
    InitializeAsync()(monitor.json 로드 후 반영) / SaveCommand(포트 범위 검증
    후 MonitorSettingsLoader.SaveAsync() 호출 — 기존에 이미 있던 메서드) / ReloadCommand
    ★ CollectorManageView.Loaded 에서도 같은 로더로 monitor.json 을 로드하지만,
      두 Loaded 핸들러의 실행 순서를 가정하지 않기 위해 이 화면이 스스로도
      다시 로드한다(동일 파일 재읽기라 안전).
  Views/Settings/SettingsView.xaml(.cs) — CollectorManageView.xaml.cs 와 동일한
    "DI 생성자 + Loaded 시 InitializeAsync()" 패턴. Manager SettingsView 와 동일한
    SectionCard 스타일 단일 카드(Enabled 체크박스 + Port 입력) 구성.

변경 파일 (기존 코드는 그대로 두고 신규 부분만 추가):
  ViewModels/MonitorMainViewModel.cs — IsSettingsTab(ActiveTabIndex==5) 추가
  MainWindow.xaml / MainWindow.xaml.cs — 탭바에 "⚙ 환경설정"(인덱스 5, 기존
    TabBg/TabFg 컨버터 재사용) + SettingsHost(ContentControl) 추가
  App.xaml.cs — SettingsViewModel/SettingsView DI 등록, MainWindow 팩토리 인자 추가

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개

### 2단계: 런타임
  [ ] F5 실행 → 탭바 "⚙ 환경설정" 클릭 → 웹 Hub 카드(체크박스+포트) 표시
  [ ] 포트에 범위를 벗어난 값(예: 99999) 입력 후 [💾 저장] → 저장 거부 + 오류 메시지
  [ ] 정상 값으로 저장 → {실행파일경로}\Config\monitor.json 의 Web 섹션 갱신 확인
  [ ] [↻ 다시 불러오기] → 편집 취소되어 디스크 값으로 복원되는지 확인
  [ ] [Collector 관리] 탭 정상 동작(회귀 없음) 확인 — 같은 MonitorSettingsLoader 공유

## 📖 사용 설명

화면 조작 방법:
  1. Monitor 실행 → 탭바 [⚙ 환경설정] 클릭
  2. "자체 웹 Hub 활성화" 체크 + 포트 입력(Collector 의 7878과 겹치지 않게 주의)
  3. [💾 저장] — Monitor 재시작 후에 실제로 반영됩니다(상단 배너로 안내)
  4. 실수로 값을 고쳤다면 [↻ 다시 불러오기]로 취소 가능

다음 진행: Collector·Manager·Studio·Monitor 4개 프로그램 로컬 설정탭 전부 완료.
HMI 는 아직 자체 설정(hmi.json 등)이 얇아 착수 전 범위를 사용자와 재확인 필요
(HM-Base-0~HM-21 빌드 확인이 우선순위 더 높음). 이후 HM-22(Manager 원격 통합
설정관리 화면) 착수.

---

## ✅ C-SET-01 후속: IIoT.Studio 환경설정 탭 (코드 완료 — 2026-07-20, 빌드 확인 대기)

```
배경: 사용자 요청으로 개별 프로그램 로컬 설정탭 트랙에 Studio 도 추가(원래 계획엔
Collector/Manager/Monitor/HMI 4개만 있었음). 단, Studio 는 Collector/Manager 와
달리 자체 settings.json 이 아예 없었다 — Studio 의 "설정"은 지금까지 전부
device.json/collect.json(장비·수집흐름) 이었고 이는 이미 각자 탭에서 편집 가능.
그래서 이번에 신규로 만든 studio-settings.json 은 Studio 프로그램 자신의 동작
(로그 레벨/보존일수, Undo 히스토리 단계 수, 저장 이력 개수)만 다룬다 —
지금까지 App.xaml.cs/DeviceTreeViewModel/MainViewModel 에 하드코딩돼 있던 값들.

★ 설계 난제와 해결(다른 프로그램과 다른 점): Collector/Manager 는 설정을
MainWindow.Loaded 이후 비동기로 읽어도 되지만, Studio 는 LogManager.Start()
(DI 빌드 전, 매우 이른 시점)와 DeviceTreeViewModel(Undo 히스토리 maxSize)/
MainViewModel(저장 이력 개수)의 생성자가 전부 DI 그래프 구성 시점(OnStartup 중,
동기)에 값을 필요로 한다. 그래서 StudioSettingsLoader 에 동기 LoadSync()를
추가해 테마 적용 직후·LogManager.Start() 호출 전에 먼저 읽고, 이렇게 이미
로드된 인스턴스를 DI 컨테이너에 그대로 등록(services.AddSingleton(studioSettings))
해서 이후 생성되는 모든 ViewModel 이 참조할 수 있게 했다. 환경설정 화면의
[다시 불러오기]/[저장] 버튼은 별도의 비동기 LoadAsync()/SaveAsync() 를 사용.

경로: Studio\IIoT.Studio\
추가 파일:
  Core/Config/StudioSettings.cs — StudioSettings(Log/Editor 2섹션) + StudioSettingsLoader
    (LoadSync 동기 전용 + LoadAsync/SaveAsync 비동기 — 용도 분리)
  ViewModels/SettingsViewModel.cs — Settings 노출(생성자에서 바로 loader.Settings 대입,
    별도 Initialize() 불필요 — 이미 LoadSync() 완료 상태), SaveCommand(유효성 검사) /
    ReloadCommand
  Views/Settings/SettingsView.xaml(.cs) — Manager SettingsView 와 동일한 SectionCard
    스타일 폼(로그 카드 + 편집기 카드) + 저장/다시불러오기 + 상태 텍스트.
    ★ DataContext 는 다른 Studio 서브 화면(DeviceTreeView 등)과 동일하게
      MainWindow.xaml 에서 DataContext="{Binding Settings}" 로 직접 주입 —
      Collector/Manager 의 ContentControl+코드비하인드 DI 주입 패턴과 다름
      (SettingsView 는 매개변수 없는 생성자만 가짐)

변경 파일 (기존 코드는 그대로 두고 신규 부분만 추가):
  App.xaml.cs
    ← studioSettings 필드 추가, OnStartup 맨 앞(테마 직후)에서 LoadSync() 호출
    ← LogManager.Instance.Start() 인자를 하드코딩값 → studioSettings.Settings.Log 값으로 교체
    ← _ConfigureServices(StudioSettingsLoader) 로 시그니처 변경, 로드된 인스턴스 등록
  ViewModels/DeviceTreeViewModel.cs
    ← _history 필드 초기화식(= new(maxSize: 50)) → 생성자 본문 대입으로 이동
      (필드 초기화식은 생성자 매개변수 참조 불가) + StudioSettingsLoader 생성자 파라미터 추가
  MainViewModel.cs
    ← StudioSettingsLoader/SettingsViewModel 생성자 파라미터 추가, Settings 서브 VM 프로퍼티 추가
    ← _AddHistory() 하드코딩 10 → _studioSettings.Settings.Editor.SaveHistoryMaxCount
    ← IsSettingsTab(ActiveTabIndex==6) 추가 — 5는 로그 토글 전용으로 이미 예약돼 있어 6 사용
  MainWindow.xaml — 탭바에 "⚙ 환경설정"(CommandParameter="6") + SettingsView 본문 추가

## ✅ 컴파일 확인 체크리스트

### 1단계: 빌드
  [ ] Clean → Rebuild → 오류 0개
  [ ] DeviceTreeViewModel/MainViewModel 생성자 시그니처 변경에 따라 다른 파일에서
      직접 new DeviceTreeViewModel(...)/new MainViewModel(...) 호출하는 곳이
      없는지 확인(App.xaml.cs 의 DI 팩토리 외에는 없어야 정상)

### 2단계: 런타임
  [ ] F5 실행 → 정상 시작(기존 화면 전부 회귀 없음) 확인
  [ ] 최초 실행 시 {실행파일경로}\Config\studio-settings.json 자동 생성 확인
  [ ] 탭바 "⚙ 환경설정" 클릭 → 로그/편집기 카드 표시
  [ ] 로그 레벨 콤보박스에 Debug/Info/Warn/Error/Fatal 5개 표시
  [ ] Undo 최대 단계 수를 예: 5 로 변경 후 저장 → 재시작 → 노드 6번 편집 후
      Ctrl+Z 6번 시도 → 5번째 이후는 더 이상 되돌려지지 않는지 확인
      (studio-settings.json 값이 실제 CommandHistory 에 반영됐는지 검증)
  [ ] 저장 이력 개수를 예: 3 으로 변경 후 저장 → 재시작 → [📝 메모 저장] 5회
      반복 → SaveMemoDialog 이력 목록이 최근 3개까지만 유지되는지 확인
  [ ] 로그 보존 일수/최대 표시 건수 값 유효성 검사(100 미만 입력 시 저장 거부) 확인
  [ ] [↻ 다시 불러오기] → 편집 취소 확인

## 📖 사용 설명

화면 조작 방법:
  1. Studio 실행 → 탭바 [⚙ 환경설정] 클릭
  2. 로그 레벨(파일/패널)·보존일수·최대표시건수, 실행취소 최대 단계 수,
     저장 이력 최대 개수를 편집
  3. [💾 저장] — 이 화면의 모든 설정은 Studio 시작 시 1회만 적용되므로
     반드시 재시작해야 실제로 반영됩니다(상단 배너로 항상 안내)
  4. 실수로 값을 고쳤다면 [↻ 다시 불러오기]로 취소 가능

다음 진행: Monitor(Web Enabled/Port 1개 항목) 환경설정 탭.

---

## ✅ C-SET-01 후속: IIoT.HMI 환경설정 탭 (코드 완료 — 2026-07-20, 빌드 확인 대기)

```
배경: Collector·Manager·Studio·Monitor 4개 프로그램 로컬 설정탭이 모두 완료된 후,
남은 HMI 는 hmi.json 이 아직 얇아(Collectors/Web/ForceWriteSecurity 뿐) 착수 전
범위 확인이 필요했음(직전 세션에서 보류). 사용자 확인(2026-07-20) 결과:
  · Web(자체 SignalR Hub Enabled/Port) — 포함
  · ForceWriteSecurity(화면 잠금 기본값) — 포함
  · Log(로그 레벨/보존일수/최대표시건수) — 포함(Studio 패턴과 동일하게 신규 추가)
  · Collectors[] — 제외([Collector 관리] 탭에서 이미 CRUD 가능하므로 중복)

★ HMI 는 Studio 와 동일한 구조적 난제를 가짐: Log 설정이 LogManager.Instance.Start()
(DI 빌드 전, 완전 동기 컨텍스트)에 필요하므로 HmiSettingsLoader 에도 Studio의
StudioSettingsLoader 와 동일한 이중 로더 패턴(LoadSync 동기 + LoadAsync/SaveAsync
비동기)을 도입함. 단, HMI 의 나머지 화면들(CollectorManageView/LayoutCanvasView 등)은
전부 Monitor 식 "DI 생성자 주입 + ContentControl 호스트 + Loaded 시 자체 로드" 패턴을
따르므로, SettingsView 도 Studio(서브 VM 직접 바인딩)가 아닌 Monitor 패턴을 그대로
재사용함(HMI 기존 관례 우선).

Core/Config/HmiSettings.cs (기존 파일 확장, 기존 코드 미변경):
  · HmiSettings 에 LogSettings Log 필드 추가(MinimumLevel/MinimumConsoleLevel/
    ValidDays/MaxDisplayCount, 기본값 Studio 와 동일)
  · HmiSettingsLoader 에 LoadSync() 신규 추가(OnStartup 맨 앞 전용, 동기 파일 I/O)
  · _opts 에 Converters={new JsonStringEnumConverter()} 추가 — LogLevel 을
    문자열로 hmi.json 에 저장(기존 LoadAsync/SaveAsync 는 그대로 재사용)

ViewModels/SettingsViewModel.cs (신규): Monitor SettingsViewModel.cs 이식.
  InitializeAsync() 에서 자체적으로 HmiSettingsLoader.LoadAsync() 재호출(
  CollectorManageView.Loaded 와의 실행 순서 비의존). SaveCommand 유효성 검사:
  Web.Port 1~65535, Log.ValidDays≥1, Log.MaxDisplayCount≥100. ReloadCommand 포함.

Views/Settings/SettingsView.xaml(.cs) (신규): Monitor SettingsView 이식 +
  Studio 의 ObjectDataProvider(LogLevel enum) 콤보 패턴 결합. 섹션 카드 3개
  (웹 Hub / 화면 잠금 / 로그), 좌측 네비게이션 없음(Manager/Monitor 와 동일 원칙).

MainWindow.xaml/.xaml.cs — "⚙ 환경설정" 탭 추가(인덱스 4, TabBtn4), SettingsHost
  ContentControl, 생성자 6번째 인자로 SettingsView 추가.
HmiMainViewModel.cs — IsSettingsTab => ActiveTabIndex==4 추가.
App.xaml.cs —
  · OnStartup 맨 앞(테마 적용 직후)에서 new HmiSettingsLoader().LoadSync() 호출,
    이어서 LogManager.Instance.Start() 의 ValidDays/MinimumLevel/MinimumConsoleLevel/
    MaxDisplayCount 인자를 하드코딩값 대신 이 로더의 Log 설정으로 교체
  · _ConfigureServices(HmiSettingsLoader settingsLoader) 로 시그니처 변경,
    services.AddSingleton(settingsLoader) 로 이미 로드된 인스턴스를 그대로 등록
    (기존 CollectorConnectionManager/LayoutCanvasViewModel 등 소비자와 동일 싱글턴 공유)
  · services.AddSingleton<SettingsViewModel>()/<SettingsView>() 추가,
    MainWindow 팩토리 인자에 SettingsView 추가
```

## ✅ 컴파일 확인 체크리스트

```
□ HmiSettings.cs: LogSettings 클래스 · HmiSettingsLoader.LoadSync() 신규 메서드 —
  기존 Settings/LoadAsync/SaveAsync 프로퍼티·메서드는 시그니처 변경 없음
□ App.xaml.cs: _ConfigureServices() → _ConfigureServices(HmiSettingsLoader) 시그니처
  변경 — 호출부(OnStartup)도 함께 수정됐는지 확인. using IIoT.HMI.Views.Settings; 추가
□ SettingsViewModel.cs: [RelayCommand] private async Task SaveAsync()/ReloadAsync()
  → SaveCommand/ReloadCommand 로 노출(Async 접미사 자동 제거, 기존 4개 프로그램과
  동일 확인된 패턴)
□ SettingsView.xaml: xmlns:log="clr-namespace:lssLib.Log;assembly=lssLib.Log" +
  xmlns:sys="clr-namespace:System;assembly=mscorlib" 네임스페이스 선언 확인
□ MainWindow.xaml.cs: 생성자 6번째 매개변수(SettingsView) ↔ App.xaml.cs MainWindow
  팩토리 6번째 인자 순서·타입 일치 확인(정적 감사로 이미 1차 확인 완료)
□ HmiMainViewModel.cs: ActiveTabIndex 의 [NotifyPropertyChangedFor(nameof(
  IsSettingsTab))] 추가 확인
```

## 📖 사용 설명

```
화면 조작 방법:
  1. HMI 실행 → 탭바 [⚙ 환경설정] 클릭
  2. 웹 Hub 활성화 여부/포트, 화면 잠금 시작 기본값, 로그 레벨(파일/패널)·
     보존일수·최대표시건수를 편집
  3. [💾 저장] — 웹 Hub/로그 설정은 HMI 시작 시 1회만 적용되므로 재시작 필요
     (상단 배너로 안내). 화면 잠금 기본값도 다음 시작부터 반영됨
  4. 실수로 값을 고쳤다면 [↻ 다시 불러오기]로 취소 가능

이로써 Collector·Manager·Studio·Monitor·HMI 5개 프로그램 전부 로컬 환경설정 탭
코드 완료. 다음 진행: HM-22(Manager 원격 통합 설정관리 화면) 착수.
```

---

## 🔍 HM-Base-0~HM-21 정적 사전 점검 (2026-07-20, 샌드박스에 .NET SDK 없어 실제 빌드 대체)

```
★ 환경 제약: 이 세션(Linux 샌드박스)에는 dotnet SDK 가 설치되어 있지 않음
  (`dotnet --version` → command not found). WPF(net8.0-windows)는 Windows 전용이므로
  Clean → Rebuild → F5 등 실제 빌드·런타임 검증은 사용자 로컬 Windows 머신에서만
  가능하다. 이번 세션에서는 그 대체로 HMI 프로젝트의 DI 등록 ↔ 실제 생성자 시그니처를
  1:1 정적 대조하는 코드 감사를 수행했다(가장 흔한 실사용 실패 원인인
  "InvalidOperationException: Unable to resolve service..." 런타임 오류를 미리 걸러내기 위함).

대조 결과 — 전부 일치, 불일치 0건:
  · MainWindow(HmiMainViewModel, CollectorManageView, LayoutCanvasView, AlarmView,
    LogPanelView) ↔ App.xaml.cs MainWindow 팩토리 5개 인자 순서·타입 정확히 일치
  · CollectorManageViewModel(HmiSettingsLoader, CollectorConnectionManager) ↔ 둘 다 등록됨
  · LayoutCanvasViewModel(CollectorConnectionManager, HmiLayoutLoader, HmiSettingsLoader)
    ↔ 3개 전부 등록됨(등록 순서도 생성자 의존 순서와 일치)
  · HmiWebHostService(HmiSettingsLoader, LayoutCanvasViewModel, CollectorConnectionManager)
    ↔ 3개 전부 등록됨
  · AlarmAggregator(CollectorConnectionManager) / AlarmViewModel(AlarmAggregator,
    CollectorConnectionManager) ↔ 전부 등록됨
  · CollectorManageView/LayoutCanvasView/AlarmView 각각 대응 ViewModel 1개만 요구 ↔ 일치
  · LogPanelView() / HmiSettingsLoader() / HmiLayoutLoader() / AlarmHistoryService() /
    HmiMainViewModel() — 전부 매개변수 없는 생성자, DI 등록 문제 없음
  · ForceWriteDialog/TrendWindow/SecondaryDisplayWindow(HM-09/17/19) — DI 미등록이 맞음
    (LayoutCanvasView.xaml.cs 안에서 `new`로 직접 생성, 노드 정보나 이미 주입된
    LayoutCanvasViewModel 인스턴스만 인자로 받음 — DI 컨테이너 의존성 없어 문제 없음)

결론: DI 그래프 자체의 논리적 결함은 발견되지 않음. 단, 이는 "생성자 의존성이
      전부 해소 가능한가"만 확인한 것이며, XAML 바인딩 오타·NullReferenceException·
      실제 PLC 통신 등 런타임에서만 드러나는 문제는 이 정적 점검으로 잡을 수 없다.
      아래 통합 체크리스트로 실제 빌드·조작 검증은 여전히 필요하다.
```

### 통합 수동 검증 체크리스트 (HM-Base-0~HM-21, 개별 Step 절 15개를 1개로 병합)

```
순서대로 Clean → Rebuild → F5 로 진행. ★ 표시는 핸드오프에 이미 기록된 고위험 항목.

□ HM-Base-0~2: 빈 창 + 테마 적용 + HmiMainViewModel 탭 전환(4개 탭) 정상 동작
□ HM-01~02: hmi.json 로드, [Collector 관리] 탭에서 Collector 추가/삭제/저장 정상
□ HM-03/07: [레이아웃 편집] 탭 진입, 화면(페이지) 목록 로드, 마지막 활성 화면 복원
□ HM-04/05: 장비 아이콘 팔레트 → 캔버스 드래그 배치, 속성 패널에서
  Collector→Device→Tag 3단 선택 후 실시간 값이 카드에 반영되는지 확인
□ HM-06: Motor/Valve/Tank/Conveyor 애니메이션(회전/레벨/점선 스크롤) 육안 확인
□ HM-08: 활성 알람 시 카드에 알람 배지·색상 오버레이 표시 확인
□ ★HM-09: [강제쓰기] 다이얼로그 — 활성 알람 있을 때 경고 문구 노출,
  API Key 세션 캐시(PrefillApiKey) 정상 동작, 실제 PLC 영향 있으므로
  테스트 환경에서 먼저 검증
□ HM-10: 화면 탭 추가/이름변경(더블클릭)/삭제, 화면별 노드 캐시 분리 확인
□ ★HM-11: 자체 웹 서버(Kestrel+SignalR) 기동 — win.Loaded 이후 브라우저로
  접속 확인(FrameworkReference 추가로 인한 빌드 실패 위험이 가장 큰 Step)
□ HM-12: 화면 잠금(🔒) 켜짐 시 강제쓰기 차단 문구 노출 확인
□ HM-14~15: [알람]/[로그] 탭 실시간 갱신 확인
□ HM-16: 앱 재시작 후에도 alarm_history.db(SQLite)에 이전 알람 이력 남아있는지 확인
□ HM-17: 노드 우클릭 → 트렌드 창 비모달로 복수 개 동시 오픈 가능 확인
□ HM-18: 캔버스 PNG 캡처 저장 확인
□ HM-19: 보조 창(다른 모니터로 이동) 오픈 후, 메인 창 닫으면 보조 창도 함께
  종료되는지 확인(ShutdownMode=OnMainWindowClose 검증)
□ HM-20/20b: Motor/Valve(1차)·Tank/Conveyor(2차) 실형상 애니메이션 세부
  (바늘 회전 중심, 벨트 스크롤 방향) 육안 확인
□ ★HM-21: 웹 화면에서 알람 배지 클릭→ACK, 카드 클릭→ForceWrite 모달 —
  물리 PLC에 실제 쓰기가 발생하는 기능이므로 반드시 테스트 환경에서 우선 확인
```

---

## ✅ HM-23: 실사용 장비 컨트롤 5종 추가 (코드 완료 — 2026-07-20, 빌드 확인 대기)

```
배경: 사용자 요청 — "HMI 솔루션의 컨트롤러를 실제 HMI 에서 사용하는 컨트롤러를
만들어서 추가해줘". 기존 Motor/Valve/Tank/Conveyor 4종에 이어, 실제 현장 HMI
화면에서 흔히 쓰이는 장비/표시기 5종을 추가로 선정(사용자 확인: 펌프·신호등·
게이지는 필수 지정, 나머지는 "그외 필요한 컨트롤러 리스트업 후 추가" 요청에
따라 스위치·히터를 제안해 함께 진행).

확장 절차는 HM-04(최초 4종) 때 확정된 3단계 그대로 재사용(무수정):
  1) Core/Layout/LayoutNode.cs 에 AbstractLayoutNode 파생 모델 추가
  2) Views/DeviceControls/ 에 DeviceControlBase 상속 컨트롤 추가(벡터 아이콘 +
     OnDeviceControlLoaded() 애니메이션)
  3) LayoutCanvasView.xaml Resources 에 DataTemplate 1개 추가(모델↔컨트롤 매핑)
  + LayoutNodeFactory.Create()/PaletteItems 등록(신규 장비 팔레트 노출)

신규 장비 5종:
  · PumpNode/PumpControl(펌프) — Motor 와 동일하게 EngValue 절대값 비례 회전이지만,
    볼류트 케이싱(원형 하우징)+토출배관 스텁+4엽 임펠러로 형태를 구분. 정지/회전
    판정(EngValue abs>0.01, Quality Good)은 MotorControl 과 동일 관례.
  · SignalTowerNode/SignalTowerControl(신호등) — 설비 자체가 아닌 "상태 표시기".
    EngValue 를 상태 코드로 해석: 0=전체소등, 1=녹색 점등(정상), 2=황색 점멸(경고),
    3 이상=적색 점멸(고장). 3색 적층 램프 + 받침대 폴 벡터 도형, 점멸은 Opacity
    AutoReverse 애니메이션(450ms).
  · GaugeNode/GaugeControl(게이지) — TankControl 다이얼(수위 전용 단색 아치)과
    달리 압력·온도·유량 등 범용 계측값(0~100)을 위한 것으로, 다이얼 배경에
    녹색(0~70%)/황색(70~90%)/적색(90~100%) 위험구간 밴드 3개 호를 그려 바늘이
    어느 구간에 있는지 한눈에 보이게 함. 바늘 회전은 TankControl 과 동일 원리
    (400ms QuadraticEase 애니메이션).
  · SwitchNode/SwitchControl(스위치) — 수동 스위치·리밋 스위치·도어 인터록 등
    디지털 On/Off 신호 표시. 알약형 트랙 + 슬라이딩 원(Thumb)으로 구성,
    EngValue>0 → On(녹색, 원이 우측) / 그 외 Off(회색, 원이 좌측). ValveControl 의
    개폐 판정 관례(EngValue>0)를 그대로 재사용.
  · HeaterNode/HeaterControl(히터) — EngValue(온도로 해석) 절대값에 따라 발열선
    (지그재그 Path) 색상 3단계 전환: <1=회색(꺼짐), 1~60=황색(가열중),
    60 이상=적색+발광 펄스(Opacity 0.5~1.0 AutoReverse 600ms, "고온 강조").

파일 목록:
  · Core/Layout/LayoutNode.cs — PumpNode/SignalTowerNode/GaugeNode/SwitchNode/
    HeaterNode 5개 클래스 추가(기존 5종 클래스는 무수정), LayoutNodeFactory.Create()
    switch 5줄 추가, PaletteItems 5개 항목 추가
  · Views/DeviceControls/PumpControl.cs (신규)
  · Views/DeviceControls/SignalTowerControl.cs (신규)
  · Views/DeviceControls/GaugeControl.cs (신규)
  · Views/DeviceControls/SwitchControl.cs (신규)
  · Views/DeviceControls/HeaterControl.cs (신규)
  · Views/LayoutCanvas/LayoutCanvasView.xaml — DataTemplate 5개 추가(기존 5개는
    무수정)
```

## ✅ 컴파일 확인 체크리스트

```
□ LayoutNode.cs: 기존 GenericIconNode/MotorNode/ConveyorNode/TankNode/ValveNode
  클래스·LayoutNodeLayout·AbstractLayoutNode 는 시그니처 변경 없음(신규 5개
  클래스만 추가)
□ 5개 신규 XxxControl.cs 모두 DeviceControlBase 상속 + OnDeviceControlLoaded()
  재정의 패턴 준수(직접 XAML 없음 — DeviceControlBase 만 x:Class 보유)
□ LayoutCanvasView.xaml: DataTemplate DataType 매핑이 LayoutNode.cs 의 실제
  클래스명(PumpNode 등)과 정확히 일치하는지 확인
□ 팔레트에 5개 신규 버튼("💧 펌프","🚦 신호등","📊 게이지","🔘 스위치","🔥 히터")
  노출 확인(LayoutNodeFactory.PaletteItems)
□ F5 실행 → [레이아웃 편집] 탭 → 팔레트에서 5개 신규 장비 더블클릭 →
  캔버스에 카드 추가 → 속성 패널에서 Tag 바인딩 → 값 변화에 따른 애니메이션/
  상태 전환(회전·점멸·바늘·슬라이드·발광) 육안 확인
□ 미바인딩 상태(IsBound=false)에서 5종 모두 기본(정지/소등/0%/Off/꺼짐) 상태로
  안전하게 렌더링되는지 확인(NullReferenceException 없음)
```

## 📖 사용 설명

```
화면 조작 방법:
  1. HMI 실행 → [레이아웃 편집] 탭 → 좌측 팔레트에서 "💧 펌프"/"🚦 신호등"/
     "📊 게이지"/"🔘 스위치"/"🔥 히터" 중 하나 더블클릭 → 캔버스에 카드 추가
  2. 카드 선택 → 우측 속성 패널에서 Collector→Device→Tag 3단 선택으로 실시간
     Tag 바인딩
  3. Tag 값 변화에 따라:
     - 펌프: 값이 클수록 임펠러가 빠르게 회전
     - 신호등: 0/1/2/3 이상 값에 따라 소등/녹색/황색 점멸/적색 점멸
     - 게이지: 0~100 값에 비례해 바늘이 회전(70%/90% 지점에 위험구간 밴드 표시)
     - 스위치: 0 이하=Off(회색, 좌측) / 0 초과=On(녹색, 우측)
     - 히터: 값이 60 이상이면 발열선이 적색으로 바뀌며 은은하게 발광 펄스

다음 진행: HM-22(Manager 원격 통합 설정관리 화면) 착수.
```

---

## ✅ HM-22: Manager 원격 통합 설정관리 화면 (코드 완료 — 2026-07-20, 빌드 확인 대기)

```
배경: HM-22 는 v11.4(2026-07-16)부터 "아이디어만 기록"된 미착수 신규 기능이었음.
사용자 확인(2026-07-20)으로 방식 확정: "기존 NamedPipe 헬스체크 채널 확장" —
Manager 가 이미 각 프로그램(Studio/Collector/Monitor/HMI)과 열어 둔 헬스체크
파이프(ping/pong, MG-03)를 그대로 재사용해 settings.json 원문을 원격 조회·저장.
신규 파이프를 별도로 열지 않는다.

프로토콜 확장(Contracts/Health/HealthPipeServer.cs, 기존 ping/pong 무수정):
  · get-settings              → "settings|{Base64(UTF8 JSON)}" 또는 "error|{메시지}"
  · save-settings|{Base64 JSON} → "ok" 또는 "error|{메시지}"
  · 페이로드가 개행을 포함할 수 있는 JSON 원문이라 Base64 로 감싸 기존 프로토콜의
    "줄 단위(ReadLine/WriteLine)" 특성을 유지. settingsProvider/settingsSaver 콜백이
    둘 다 null 이면 해당 프로그램은 "not-supported" 로 응답(하위 호환 — 구버전
    빌드와 통신해도 깨지지 않음).
  · 저장 전 서버 측에서 JsonDocument.Parse() 로 최소 문법 검증만 수행(필드별
    유효성 검사는 각 프로그램 로컬 [환경설정] 탭의 책임 그대로 유지).

서버 측 콜백 연결(Studio/Collector/Monitor/HMI 4개 프로그램 App.xaml.cs,
기존 HealthPipeServer 생성 호출에 인자만 추가):
  · Studio  → StudioSettingsLoader.SettingsPath (studio-settings.json)
  · Collector → CollectorSettingsLoader.SettingsPath (settings.json)
  · Monitor → MonitorSettingsLoader.SettingsPath (monitor 설정 파일)
  · HMI     → HmiSettingsLoader.SettingsPath (hmi.json)
  · 구현은 File.ReadAllText/WriteAllText 로 원문을 그대로 읽고 쓰는 것뿐 —
    각 프로그램의 로더 인메모리 Settings 객체는 건드리지 않는다(다른 프로그램의
    로컬 저장과 동일하게 "재시작해야 반영" 원칙 유지, cross-thread 부작용 없음 —
    파이프 백그라운드 스레드에서 안전하게 호출 가능).

클라이언트 측(Manager Core/HealthCheckService.cs, 기존 PingAsync 무수정):
  · GetSettingsAsync(processName) → RemoteSettingsResult(Ok, Json, Error)
  · SaveSettingsAsync(processName, json) → RemoteSettingsResult
  · PingAsync 와 동일한 "연결→요청 1줄→응답 1줄→종료" 패턴, 1초 타임아웃 재사용.

Manager 신규 [🌐 원격 설정] 탭(인덱스 6):
  · ViewModels/RemoteSettingsViewModel.cs(신규) — Targets(Studio/Collector/
    Monitor/HMI 4개 고정 목록, Manager 자신은 로컬 [환경설정] 탭이 있으므로 제외),
    SelectedTarget/JsonText/StatusMessage/HasError/IsBusy, LoadCommand(조회)/
    SaveCommand(저장)는 [RelayCommand(CanExecute=...)] 로 대상 미선택·진행중
    자동 비활성화(S-17A 확립 패턴). 저장 전 클라이언트 측에서도 JSON 문법 1차
    검증(서버 재검증과 이중 방어).
  · Views/RemoteSettings/RemoteSettingsView.xaml(.cs)(신규) — 대상 콤보박스 +
    Consolas 폰트 다중행 JSON 편집기(PropInput+AcceptsReturn 관례) + 조회/저장
    버튼 + 상태 메시지. Manager 기존 SettingsView 와 동일하게 좌측 네비 없음.
  · ManagerMainViewModel.cs — IsRemoteSettingsTab(인덱스 6) 추가(파일 I/O 가
    없어 InitializeAsync() 에 별도 초기화 호출 불필요).
  · MainWindow.xaml/.xaml.cs — "🌐 원격 설정" 탭 버튼(TabBtn6) + RemoteSettingsHost
    ContentControl + 생성자 9번째 인자로 RemoteSettingsView 추가.
  · App.xaml.cs — RemoteSettingsViewModel/View DI 등록(HealthCheckService 는
    이미 MG-03 에서 등록됨), MainWindow 팩토리 인자 추가.
```

## ✅ 컴파일 확인 체크리스트

```
□ HealthPipeServer.cs: 기존 4개 프로그램의 HealthPipeServer 생성 호출이 새
  선택 인자(settingsProvider/settingsSaver)를 추가했을 뿐 기존 statusProvider/
  onLog 인자 순서·값은 무수정인지 확인
□ Studio/Collector/Monitor/HMI App.xaml.cs: using System.IO; 가 이미 있어
  File.Exists/ReadAllText/WriteAllText 사용에 추가 using 불필요한지 확인
  (Encoding 은 System.Text.Encoding.UTF8 로 완전정규화해 별도 using 없음)
□ HealthCheckService.cs: PingAsync 시그니처·동작 무수정(GetSettingsAsync/
  SaveSettingsAsync 만 추가)
□ RemoteSettingsViewModel.cs: [RelayCommand(CanExecute = nameof(_CanLoadOrSave))]
  두 커맨드 모두 SelectedTarget/IsBusy 변경 시 [NotifyCanExecuteChangedFor] 로
  버튼 활성상태 자동 갱신되는지 확인(S-17A 동일 패턴)
□ MainWindow.xaml.cs: 생성자 9번째 매개변수(RemoteSettingsView) ↔ App.xaml.cs
  MainWindow 팩토리 9번째 인자 순서·타입 일치 확인
□ ManagerMainViewModel.cs: ActiveTabIndex 의
  [NotifyPropertyChangedFor(nameof(IsRemoteSettingsTab))] 추가 확인
□ F5 실행(Manager + 대상 프로그램 최소 1개 함께 실행) → [🌐 원격 설정] 탭 →
  대상 선택 → [↻ 조회] → JSON 표시 확인 → 값 수정 → [💾 원격 저장] → "ok"
  응답 확인 → 대상 프로그램 재시작 후 값 반영 확인
□ 대상 프로그램이 실행 중이 아닐 때 [↻ 조회] → "연결 실패" 오류 메시지 확인
  (예외로 앱이 죽지 않아야 함)
□ 저장 시 JSON 문법을 일부러 깨뜨려 봄 → 파이프 전송 전 클라이언트 측에서
  "JSON 문법 오류" 로 즉시 차단되는지 확인(서버까지 가지 않음)
```

## 📖 사용 설명

```
화면 조작 방법:
  1. Manager 와 원격 조회할 프로그램(예: Collector)을 함께 실행
  2. Manager [🌐 원격 설정] 탭 클릭 → 콤보박스에서 "Collector" 선택
  3. [↻ 조회] → Collector 의 settings.json 원문이 편집기에 표시됨
  4. 편집기에서 값을 직접 수정(들여쓰기 형태로 재포맷되어 표시됨)
  5. [💾 원격 저장] → 저장 완료 메시지 확인 — 이 시점에는 파일만 갱신되며,
     Collector 를 재시작해야 실제로 반영됨
  6. 대상 프로그램이 꺼져 있으면 조회/저장 모두 "연결 실패" 메시지로 안내됨

이로써 HM-22 착수 완료 — Manager 에서 Studio·Collector·Monitor·HMI 4개
프로그램의 환경설정을 창을 전환하지 않고 한 화면에서 원격으로 조회·수정 가능.
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

### ⑥ HM-07 (레이아웃 저장·불러오기 + Z-레벨 우선순위) — ✅ 빌드·런타임 확인 완료 (2026-07-19, 사용자 직접 검증)
```
★ 사용자 요청 추가: Z-레벨(카드 겹침 순서) 우선순위 지정 기능을 HM-07과 함께 구현.
Core/Layout/LayoutNode.cs(ZIndex 추가) · Core/Config/HmiLayoutSettings.cs(신규 —
LayoutNodeDto/LayoutPageDto/HmiLayoutFile/HmiLayoutLoader) ·
ViewModels/LayoutCanvasViewModel.cs(Z-레벨 커맨드 4개 + Pages/ActivePage/
InitializeAsync/AddPage/DeletePage/SaveLayout + LayoutPageViewModel) ·
Views/LayoutCanvas/LayoutCanvasView.xaml(Z-레벨 툴바 버튼 4개 + 화면 관리 바 신설) ·
LayoutCanvasView.xaml.cs(InitializeAsync 호출 추가) · App.xaml.cs(HmiLayoutLoader
DI 등록) — 전체 6개 파일 신규/수정 완료, 사용자 빌드·런타임 확인 완료.
```

### ⑦ HM-08 (알람 오버레이) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
Core/Layout/LayoutNode.cs(알람 상태 필드 6개 추가) · Core/Converters/UiConverters.cs
(AlarmLevelColorConverter) · ViewModels/LayoutCanvasViewModel.cs(AlarmChanged 구독
+ _OnAlarmChanged + AcknowledgeAlarmCommand) · Views/DeviceControls/
DeviceControlBase.xaml(알람 배지+상세 팝업) · DeviceControlBase.xaml.cs(배지/ACK
Click 핸들러 + VisualTreeHelper 상위 탐색 헬퍼) — 전체 5개 파일 신규/수정 완료.
Popup의 DataContext/RelativeSource 제약을 ElementName 바인딩 + 코드비하인드
Click 핸들러로 우회하는 설계 결정 포함(위 "HM-08 구현 내역" 절 참조).
사용자 빌드·런타임 확인 필요(위 체크리스트 참조).
```

### ⑧ HM-09 (ForceWrite 제어 다이얼로그) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
Models/ForceWriteResult.cs(신규) · Views/LayoutCanvas/ForceWriteDialog.xaml(.cs)
(신규 — Collector 자체 다이얼로그 이식) · Core/Connection/CollectorConnection.cs
(ForceWriteAsync 추가) · CollectorConnectionManager.cs(ForceWriteAsync 추가) ·
ViewModels/LayoutCanvasViewModel.cs(ForceWriteAsync 공개 메서드 추가) ·
Views/LayoutCanvas/LayoutCanvasView.xaml.cs(더블클릭 분기 + 다이얼로그 호출) —
전체 6개 파일 신규/수정 완료. Collector C-EX-13(ForceWrite Hub 메서드)이
선행 완료되어 있어 검증 로직은 전부 Collector 측에 위임(위 "HM-09 구현 내역"
절 참조). 사용자 빌드·런타임 확인 필요(위 체크리스트 참조).
```

### ⑨ HM-10 (다중 화면 관리 — 탭 바 UI 교체) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
★ 사용자 확인: "HM-07 콤보박스로 충분/탭 바로 교체/트리 패널 신설" 3안 중
"탭 바로 교체(권장)"를 선택.
ViewModels/LayoutCanvasViewModel.cs(LayoutPageViewModel.IsActive/IsEditingName +
SelectPage 공개 메서드 + OnActivePageChanged 의 IsActive 갱신) ·
Views/LayoutCanvas/LayoutCanvasView.xaml(PageTabTemplate 신규 + Row 0 을 탭 바로
교체, ➕/🗑/💾 버튼은 우측 고정) · LayoutCanvasView.xaml.cs(PageTab_
MouseLeftButtonDown/PageTabNameBox_LostFocus/KeyDown) — 전체 3개 파일 수정 완료.
데이터 구조·저장 파일·커맨드는 HM-07과 완전히 동일, UI 표현 방식만 교체(위
"HM-10 구현 내역" 절 참조). 사용자 빌드·런타임 확인 필요(위 체크리스트 참조).
```

### ⑩ HM-11 (웹 브라우저 표시 확장) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
★ 사전 리서치: Collector C-11(SignalRHostService.cs+wwwroot)·Monitor MN-05
(FrameworkReference 재도입 FIX)의 실제 코드를 조사 후 두 검증된 패턴을 조합.
IIoT.HMI.csproj(FrameworkReference Microsoft.AspNetCore.App 추가 + Microsoft.
Extensions.DependencyInjection 명시버전 제거 — Monitor MN-05 FIX 동일 적용 +
wwwroot Content Include) · Core/Config/HmiSettings.cs(Web/WebHostSettings 추가,
기본 포트 7880) · Core/Web/WebNodeDto.cs(신규) · Core/Web/HmiWebHub.cs(신규,
읽기 전용 빈 Hub) · Core/Web/HmiWebHostService.cs(신규 — Kestrel+SignalR+
wwwroot 호스팅, dirty플래그+500ms 코일레싱 브로드캐스트, Dispatcher.InvokeAsync
로 스레드 안전 스냅샷) · wwwroot/index.html(신규, Collector 페이지와 동일
스타일) · App.xaml.cs(DI 등록+win.Loaded 오케스트레이션 최초 도입+OnExit 정리)
— 전체 7개 파일 신규/수정 완료.
1차 범위=읽기 전용 표시(ACK/ForceWrite/애니메이션/팝업 웹 미제공, WPF 활성
화면 1개만 미러링 — 위 "HM-11 구현 내역" 절 "★ 범위 결정" 참조).
★★ 이번 Step은 FrameworkReference 추가로 인한 NU1605 위험이 있으므로 반드시
빌드부터 꼼꼼히 확인할 것(체크리스트 1단계 참조).
```

### ⑪ HM-12 (보안 — 화면 잠금+알람 경고+세션 API Key 캐시) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
★ 사용자 확인: HM-12 범위(핸드오프상 "Security 정책 전반"으로만 열려 있었음)를
3가지 후보로 물었고, 사용자가 전부 선택 — 화면 잠금 모드(권장)/활성 알람 중
강제쓰기 경고/세션 내 API Key 임시 기억.
Core/Config/HmiSettings.cs(ForceWriteSecuritySettings 추가) ·
ViewModels/LayoutCanvasViewModel.cs(IsForceWriteLocked+LockButtonLabel+
ToggleForceWriteLockCommand, _apiKeyCache+GetCachedApiKey, HmiSettingsLoader
주입) · Views/LayoutCanvas/ForceWriteDialog.xaml(.cs)(AlarmWarningPanel+
ChkAlarmAck+OkButton 활성화 제어+PrefillApiKey) · LayoutCanvasView.xaml(잠금
토글 버튼) · LayoutCanvasView.xaml.cs(잠금 체크+다이얼로그 생성 시 알람/API Key
정보 전달) — 전체 5개 파일 신규/수정 완료(위 "HM-12 구현 내역" 절 참조).
사용자 빌드·런타임 확인 필요(위 체크리스트 참조).
완료 확인 후 → HM-Base-0~HM-12 전체 회귀 확인 → HMI 1차 마감 판단 또는
Sequence 착수. ← 다음 시작점
```

### ⑫ HM-13 (정리 — 현황판 탭 제거·통합) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
★ 사용자 질문 계기: "현황판, 알림 탭의 화면 변경은 없는가??" — 확인해보니 HM-03~12
가 전부 [레이아웃 편집] 탭 안에 구현되어(카드 배치·실시간 값·Z순서·다중 화면·
알람 배지·ForceWrite) 이미 사실상 생산현황판 역할을 겸하고 있었고, 원래 Step
맵에서 별도로 남겨뒀던 "현황판" placeholder 탭은 그대로 미구현 상태로 방치되어
있었음(설계 당시 계획 vs 실제 구현이 어긋난 케이스).
★ 사용자 확인: 4가지 해결 옵션(현황판 탭 제거·통합/현황판에 요약 대시보드 구현/
알람 탭에 전체 목록 구현/보류) 중 "현황판 탭 제거(레이아웃편집으로 통합, 권장)"
단독 선택 — 알람 탭 전체 목록 구현은 하지 않음, 보류도 아님(즉시 제거 확정).
HmiMainViewModel.cs(IsDashboardTab 제거, IsLayoutTab/IsCollectorTab/IsAlarmTab/
IsLogTab 인덱스 0~3으로 재정렬) · MainWindow.xaml("🗂 현황판" 버튼 및 TabBtn0
현황판 placeholder Grid 완전 삭제, 나머지 3개 버튼 CommandParameter "1,2,3"→
"0,1,2,3", 스타일 키 TabBtn1~4 → TabBtn0~3 로 재번호, 레이아웃 편집 탭 라벨을
"🗂 현황판(레이아웃 편집)"으로 변경해 통합 사실을 명시) — 전체 2개 파일 수정 완료.
탭 4개로 재정렬: [레이아웃 편집(=현황판)][Collector 관리][알람][로그].
※ 미선택 항목(참고용 기록): "알람 탭에 전체 알람 목록/이력 구현"은 이번엔 하지
않음 — HM-EX 후속 후보로 등록, 사용자가 이후 요청 시 별도 Step으로 진행.
사용자 빌드·런타임 확인 필요(위 체크리스트 참조).
완료 확인 후 → HM-Base-0~HM-13 전체 회귀 확인 → HMI 1차 마감 판단 또는
Sequence 착수. ← 다음 시작점
```

### ⑬ HM-14 (알람 탭 실시간 목록) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
★ 계기: HM-13 정리 시 "알람 탭에 전체 알람 목록/이력 구현" 옵션은 미선택 상태로
남겨뒀었는데, 사용자가 "알림탭 옵션의 화면 업데이트 해줘"로 착수 요청.
★ 사용자 확인: 범위를 "실시간 목록만(권장)" vs "실시간 목록+SQLite 이력 영구
저장" 중에 물었고, "실시간 목록만" 선택 — SQLite 이력 저장은 HM-EX 후보로 보류.
Monitor MN-03(AlarmAggregator/AlarmViewModel/AlarmView 기본 구조)+MN-EX-06
(Collector/레벨/상태 필터 + Tag/메시지 검색 툴바)를 그대로 이식(필드/동작 동일).
Models/AlarmRow.cs(신규) · Core/Aggregation/AlarmAggregator.cs(신규 — ★
CollectorConnectionManager 와의 결합은 HM-01 "MN-01B 패턴 단순화" 결정을 존중해
Monitor 와 반대 방향으로 구독: Aggregator 가 CollectorConnectionManager.AlarmChanged
를 직접 구독. CollectorName 은 RegisterCollectorName() 훅 없이 GetConnectedEndpoints()
1회 조회로 단순화) · Core/Converters/UiConverters.cs(AlarmStatusColorConverter 추가)
· ViewModels/AlarmViewModel.cs(신규) · Views/Alarm/AlarmView.xaml(.cs)(신규) ·
MainWindow.xaml(알람 placeholder Grid → ContentControl AlarmHost) ·
MainWindow.xaml.cs/App.xaml.cs(AlarmView DI 주입) — 전체 8개 파일 신규/수정 완료.
사용자 빌드·런타임 확인 필요(위 체크리스트 참조).
완료 확인 후 → HM-Base-0~HM-14 전체 회귀 확인 → HMI 1차 마감 판단 또는
Sequence 착수. ← 다음 시작점
```

### ⑭ HM-15 (로그 탭) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
★ 계기: 사용자가 HM-14(알람 탭)와 "같이" 로그 탭도 진행해 달라고 요청 — 남은
마지막 placeholder 탭 정리.
★ 조사 결과: MainWindow.xaml 주석은 "lssLib.Log 의 LogViewerControl 연결 예정"
이라 적혀 있었으나, 실제로 lssLib.Log 의 LogViewerControl 은 demo 프로젝트
전용이라 다른 프로젝트에서 직접 참조 불가하다는 사실을 Studio/Collector/Monitor
세 프로그램의 LogPanelView.xaml 주석에서 확인 — 세 프로그램 모두 자체 제작한
"LogPanelView"(LogManager.Instance.LogAdded 구독 + ListView) 패턴을 대신 쓰고
있었으므로, HMI 도 동일 패턴을 그대로 이식(신규 설계 없음).
Views/Log/LogPanelView.xaml(.cs)(신규 — Monitor 버전 그대로 포팅, 타이틀만
"📋 HMI 로그"로 변경) · MainWindow.xaml(로그 placeholder Grid → ContentControl
LogHost) · MainWindow.xaml.cs/App.xaml.cs(LogPanelView DI 등록+주입, ViewModel
없이 자체 완결형 View라 매개변수 없는 AddSingleton) — 전체 4개 파일 신규/수정 완료.
★ 이 Step으로 HMI의 4개 탭(레이아웃 편집·Collector 관리·알람·로그) 전부 실제
화면으로 채워짐 — 남은 placeholder 없음.
사용자 빌드·런타임 확인 필요(위 체크리스트 참조).
완료 확인 후 → HM-Base-0~HM-15 전체 회귀 확인 → HMI 1차 마감 판단 또는
Sequence 착수. ← 다음 시작점
```

### ⑮ HM-16 (알람 이력 SQLite 영구 저장) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
★ 계기: "빌드 확인 다음으로 HMI 1차 마감 여부(HM-EX 검토) 부분 진행해줘" 요청에
사용자에게 HM-EX 후보 7건(히스토리 트렌드/캡처·PDF/다중모니터/알람이력SQLite/
장비아이콘실형상화/웹ACK·ForceWrite/설정UI)을 전부 제시했고, "1차 마감 확정"이
아니라 "후보 중 일부를 지금 착수"를 선택 → 이어서 두 번째 질문에서 7건 전체를
선택(부분 선택 아님) — 위 "확장" 절 Step 순서(HM-16~22)로 등록하고 그 중 가장
작고 독립적인 HM-16부터 착수.
Monitor MN-EX-02(AlarmHistoryService) 를 그대로 이식 — 필드/테이블/보존기간(90일)
동일, DB 파일만 monitor.db → hmi.db 로 변경.
IIoT.HMI.csproj(lssLib.DB/lssLib.DB.Sqlite ProjectReference 추가, Monitor MN-EX-02
와 동일 참조) · Core/Aggregation/AlarmAggregator.cs(AlarmRecorded 이벤트 추가 —
생성+상태전이마다 발행) · Core/Storage/AlarmHistoryService.cs(신규) ·
App.xaml.cs(DI 등록, AlarmRecorded→RecordAsync 구독은 DI 빌드 직후, DB 초기화는
HM-11과 동일한 win.Loaded 오케스트레이션, OnExit 5초 타임아웃 정리) — 전체 4개
파일 신규/수정 완료.
★ Monitor 원본과 동일하게 "저장 전용" 범위 — 이력을 앱 안에서 조회/검색하는
화면은 없음(외부 SQLite 툴로 hmi.db 를 열어 확인). 조회 UI가 필요하면 별도 요청.
사용자 빌드·런타임 확인 필요(★ 신규 ProjectReference 2건 추가로 인한 참조
해석 오류 여부 우선 확인 — HM-11 FrameworkReference 때처럼 첫 빌드가 중요).
완료 확인 후 → HM-17(히스토리 트렌드 오버레이) 착수. ← 다음 시작점
```

### ⑯ HM-17 (실시간 트렌드 창) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
★ 조사: Collector 의 ITimeSeriesStore 인터페이스는 Write*Async 메서드만 있고
조회(읽기) 메서드가 전혀 없음을 확인. Collector 자체 [트렌드] 탭이 사용하는
TrendQueryService 도 Collector 프로세스 내부에서 자신의 SQLite DB 파일을 직접
여는 방식이라(원격 API 아님) HMI/Monitor 같은 별도 프로세스는 접근 불가.
Monitor 의 기존 [차트] 탭(MN-06)도 마찬가지로 실시간값만 그리고 과거 이력은
조회하지 않는다는 사실도 함께 확인.
★ 사용자 확인: "진짜 과거 이력 조회(Collector 신규 API 필요, 범위 큼)" 대신
"실시간 트렌드만(권장, 바로 구현 가능)" 선택 — Monitor MN-06 과 동일 범위.
Monitor 의 필터형 [차트] 탭과 달리, HMI 는 레이아웃 편집 탭에서 이미 특정
Tag 에 바인딩된 카드가 있으므로 별도 Collector/PLC/Tag 선택기 없이 "카드
우클릭 → 그 카드의 트렌드 창"으로 단순화.
IIoT.HMI.csproj(OxyPlot.Wpf 2.2.0 PackageReference 추가, Collector C-13/
Monitor MN-06 과 동일 버전) · Views/LayoutCanvas/TrendWindow.xaml(.cs)(신규 —
Monitor ChartViewModel 의 롤링 윈도우(300포인트)/PropertyChanged 구독 로직만
이식, DI 없이 코드비하인드에서 직접 PlotModel 구성 — ForceWriteDialog 와 동일
패턴) · LayoutCanvasView.xaml.cs(OnCanvasMouseDown 에 우클릭 분기 추가,
Tag 바인딩된 카드만 대상) — 전체 3개 파일 신규/수정 완료.
★ 여러 트렌드 창을 동시에 열 수 있다(비모달, 노드별 독립 창). 과거(창 열기
이전) 값은 표시되지 않는다 — 창을 연 시점부터만 누적.
사용자 빌드·런타임 확인 필요(★ OxyPlot.Wpf 패키지 복원 확인).
완료 확인 후 → HM-18(화면 캡처/PDF 리포트) 착수. ← 다음 시작점
```

### ⑰ HM-18 (화면 캡처 PNG) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
★ 조사: 5개 프로그램 어디에도 PDF 생성 라이브러리(PdfSharp/QuestPDF/iText 등)
참조가 전혀 없음을 확인 — 이식할 선례가 없어 새로 추가해야 하는 상황.
★ 사용자 확인: PNG 캡처만(권장, 새 의존성 없음) / PNG+간단 PDF(PdfSharp) /
정식 리포트 PDF(QuestPDF, 제목·타임스탬프·요약 포함) 3가지 중 "PNG 캡처만"
선택 — PDF 리포트는 전체가 범위 밖으로 확정.
Views/LayoutCanvas/LayoutCanvasView.xaml(툴바에 "📷 캡처" 버튼 추가) ·
LayoutCanvasView.xaml.cs(CaptureButton_Click 추가 — CanvasBorder 를
RenderTargetBitmap 으로 렌더링 후 PngBitmapEncoder 로 저장, SaveFileDialog로
경로 선택, 기본 파일명 "hmi-capture-{화면이름}-{타임스탬프}.png") — 전체 2개
파일 수정 완료. WPF 내장 기능만 사용해 csproj 변경 없음(새 NuGet 의존성 없음).
사용자 빌드·런타임 확인 필요.
완료 확인 후 → HM-19(다중 모니터 지원) 착수. ← 다음 시작점
```

### ⑱ HM-19 (다중 모니터 지원) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
설계: Monitor/Studio/Manager 등 어디에도 다중 모니터 선례가 없어 새로 설계.
"레이아웃 편집 탭을 새 창으로 분리해 다른 모니터에 띄우기" 요구를, 보조 창이
메인 창과 완전히 독립된 편집 상태를 갖게 하는 대신 "같은 LayoutCanvasViewModel
을 공유하는 두 번째 View 인스턴스"로 구현 — 두 창에 동일한 레이아웃(카드 배치·
값·페이지 전환)이 항상 실시간으로 함께 반영된다(둘 중 어느 창에서 편집해도
다른 창에 즉시 보임). 완전히 독립된 편집 상태를 원한다면 별도 확장 필요.
★ 구현 중 심각한 잠재 버그 발견·수정: LayoutCanvasView 생성자의 Loaded
핸들러가 무조건 _vm.InitializeAsync() 를 호출하는데, 그 메서드는 매번
Pages.Clear()+파일 재로드를 하고 있었다. 같은 ViewModel 을 공유하는 두 번째
View(보조 창)가 뜨면 그 Loaded 도 InitializeAsync() 를 또 호출하게 되어,
아직 "💾 레이아웃 저장"을 누르지 않은 편집 내용이 보조 창을 여는 순간
통째로 파일 재로드본으로 덮어써지는 데이터 손실 버그가 될 뻔했음 — 착수
전 조사 단계에서 발견해 InitializeAsync() 에 멱등 가드(Pages.Count>0 이면
즉시 반환)를 추가해 원천 차단.
Views/LayoutCanvas/SecondaryDisplayWindow.xaml(.cs)(신규 — 코드비하인드에서
주입받은 View 를 담기만 하는 빈 창) · LayoutCanvasView.xaml(툴바에 "🖥 보조
화면" 버튼 추가) · LayoutCanvasView.xaml.cs(SecondaryWindowButton_Click —
new LayoutCanvasView(_vm) 로 두 번째 인스턴스 생성 후 SecondaryDisplayWindow
로 표시, Owner 없음 — 다른 모니터로 자유롭게 이동 가능) ·
LayoutCanvasViewModel.cs(InitializeAsync() 멱등 가드 추가) ·
App.xaml.cs(ShutdownMode=OnMainWindowClose 명시 — 메인 창을 닫으면 Owner
없는 보조 창도 함께 정리되도록. WPF 기본값(OnLastWindowClose)이면 보조 창이
열려 있는 한 프로세스가 종료되지 않았을 것) — 전체 5개 파일 신규/수정 완료.
사용자 빌드·런타임 확인 필요(★ 보조 창을 열고 메인 창을 닫았을 때 프로세스가
정상 종료되는지, 두 창의 편집이 실시간 동기화되는지 확인 우선).
완료 확인 후 → HM-20(장비 아이콘 실형상화) 착수. ← 다음 시작점
```

### ⑲ HM-20 (장비 아이콘 실형상 UI 컨트롤화, HM-04-EX) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
착수 전 조사: 4개 장비 컨트롤(Motor/Conveyor/Tank/Valve)이 HM-06 애니메이션을
전부 베이스의 공유 요소 IconText(TextBlock, 이모지 글리프)에 걸어 두고 있음을
확인 — Motor/Conveyor 는 RenderTransform(회전/이동), Valve 는 Foreground(색상
전환)로 사용 중, Tank 는 IconText 를 전혀 쓰지 않고 별도 LevelTrack/LevelFill
게이지만 사용. 이 결합관계에 따라 컨트롤별로 위험도가 달라 접근을 구분했다.
DeviceControlBase.xaml: IconText(TextBlock) 를 IconHost(Grid, 신규 "아이콘
슬롯") + 그 안의 기본 글리프 IconGlyphText(기존과 동일한 TextBlock)로 교체 —
GenericIconControl 등 커스텀 아이콘이 없는 타입은 지금까지처럼 이모지 그대로
표시되어 100% 하위 호환.
MotorControl.cs: 원형 하우징(Ellipse)+3개 회전 날개(Rectangle, 120도 간격)를
직접 그려 넣고, 날개 그룹 전체에 기존 _rotate(RotateTransform)를 적용 — 회전
속도 계산 로직(_ApplyState/_OnNodePropertyChanged)은 완전히 무수정.
ConveyorControl.cs: 좌우 롤러(Ellipse)+벨트 라인(Line)+화물 3개(Rectangle)를
그려 넣고, 화물 그룹에 기존 _shift(TranslateTransform)를 적용 — 흐름 속도
계산 로직은 무수정.
TankControl.cs: 원통 몸통(Rectangle)+타원 뚜껑(Ellipse)을 정적 장식으로만
추가 — IconText 를 원래 쓰지 않았으므로 수위 게이지 로직(_ApplyState/
LevelTrack.SizeChanged 등)은 단 한 줄도 건드리지 않음(최저 위험 변경).
ValveControl.cs: 배관(Line)+밸브 바디(Ellipse)+손잡이(Line)를 그려 넣고,
기존 "색상만 전환" 방식을 "손잡이 색상 전환 + 회전(열림=배관과 나란히/
닫힘=수직, 표준 밸브 심볼 표기)"으로 확장 — EngValue>0→열림 판정 기준선은
HM-06 그대로 유지.
전체 5개 파일(DeviceControlBase.xaml + 4개 컨트롤) 수정 완료.
★ 중요 — 시각 검증 필요: 이 도형들은 좌표 계산으로 코드에서 직접 그린
것이라(WPF 렌더링 미리보기 불가 환경에서 작성) 실제 화면에서 크기 비율·
겹침·색상 대비를 육안으로 확인해야 한다. 어색한 부분이 있으면 구체적으로
알려주시면 좌표/크기만 조정하는 건 빠르게 가능하다.
사용자 빌드·런타임 확인 필요(★ 특히 카드 크기(Width=120/MinHeight=100) 안에
아이콘(56x56)이 잘 들어맞는지, 4개 장비 모두 확인).
완료 확인 후 → HM-21(웹에서 ACK/ForceWrite 지원) 착수. ← 다음 시작점
```

### ⑲-1 HM-20b (탱크 게이지·컨베이어 애니메이션 재작업, 사용자 피드백) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-19)
```
★ 사용자 피드백: "컨트롤러 UI를 사각형이 아니라 게이지의 경우 차량 속도계처럼,
컨베이어 벨트의 경우 실제 컨베이어가 돌아가거나 회전하는 그런 UI"를 요청 —
HM-20 1차 결과물(탱크=단순 막대 게이지, 컨베이어=화물 3개가 좌우로 왕복)이
기대에 못 미쳐 두 컨트롤만 다시 설계(모터/밸브는 대상 아님, 이번 피드백에서
언급되지 않음).
TankControl.cs: 직사각형 LevelTrack/LevelFill 막대 게이지를 완전히 대체 —
PathGeometry+ArcSegment 로 240도 원호 다이얼을 그리고, 0/25/50/75/100% 위치에
눈금(Line)을 배치, 빨간 바늘(Line+RotateTransform)이 수위(%)에 비례한 각도로
회전한다(값이 바뀔 때마다 400ms 이징 애니메이션으로 부드럽게 이동 — 실제
속도계 바늘처럼). 각도 계산용 _PointOnCircle() 헬퍼(위쪽=0도·시계방향 +) 신규
추가. LevelTrack/LevelFill 은 이 컨트롤에서는 더 이상 사용하지 않지만 베이스
요소 자체는 삭제하지 않음(다른 장비가 막대형이 필요하면 재사용 가능하도록).
ConveyorControl.cs: 화물 3개가 왕복(oscillate)하던 방식을 폐기하고 ①좌우
롤러에 스포크(십자선) 2개를 넣고 그 스포크 그룹을 RotateTransform 으로 계속
회전시켜 "롤러가 실제로 도는" 것처럼 보이게 하고, ②벨트 상/하 라인을
점선(StrokeDashArray)으로 그린 뒤 Shape.StrokeDashOffsetProperty 를
AutoReverse 없이 한 방향으로 계속 흘려보내 "벨트가 흐르는"(마칭 앤츠) 효과를
낸다. 속도(seconds)는 기존과 동일하게 EngValue 절대값에 비례.
전체 2개 파일(TankControl.cs, ConveyorControl.cs) 수정 완료.
사용자 빌드·런타임 확인 필요(★ 바늘 회전 중심이 정확히 맞는지, 벨트 스크롤
방향/속도가 자연스러운지 육안 확인 우선 — 마음에 안 드는 부분 있으면 구체적으로
알려주시면 좌표/속도만 조정 가능).
완료 확인 후 → HM-21(웹에서 ACK/ForceWrite 지원) 착수. ← 다음 시작점
```

### ⑲-2 HM-20 관련 컨트롤러 실형상화 현황 점검 (사용자 질의 응답, 2026-07-20)
```
사용자 질의: "탱크와 컨베이어 같이 만들어야 할 컨트롤러 목록을 정리해달라"
→ 점검 결과, 4개 장비 컨트롤 전부 이미 HM-20/HM-20b 로 벡터 도형+애니메이션
처리가 끝나 있으며, 추가 작업이 필요한 컨트롤러는 없음:
  · Tank(탱크)     — HM-20b 로 속도계 다이얼(회전 바늘)까지 완료.
  · Conveyor(컨베이어) — HM-20b 로 롤러 회전+벨트 스크롤까지 완료.
  · Motor(모터)    — HM-20 1차에서 이미 회전 날개(3개 Rectangle, 120도 간격)가
    실제로 회전하는 벡터 아이콘으로 완료. 이번 사용자 피드백(속도계/컨베이어
    회전)에서 별도로 지적되지 않았음 — Tank/Conveyor 와 동일한 수준의 "실제로
    움직이는" 표현이 이미 적용되어 있어 추가 조치 불필요.
  · Valve(밸브)    — HM-20 1차에서 이미 배관+바디+손잡이 벡터 아이콘 + 손잡이
    회전(열림=나란히/닫힘=수직, 표준 밸브 심볼)까지 완료. 마찬가지로 이번
    피드백에서 지적되지 않았고 이미 동등한 수준.
  · GenericIconControl(미지정 장비) — 의도적으로 이모지 글리프 그대로 유지
    (향후 추가될 미지정 장비 타입을 위한 기본 폴백, 커스텀 벡터 대상 아님).
결론: 추가로 "탱크/컨베이어처럼 만들어야 할" 컨트롤러 없음. Motor/Valve 도
구체적으로 어색한 부분이 있다면 언제든 알려주면 좌표/애니메이션만 조정 가능.
```

### ⑳ HM-21: 웹에서 ACK/ForceWrite 지원 (HM-11-EX) — ✅ 코드 완료, 빌드 확인 대기 (2026-07-20)
```
목표: HM-11(웹 표시)이 "읽기 전용"으로 남겨뒀던 ACK(알람 확인)/ForceWrite(강제
쓰기)를 웹 페이지에서도 가능하게 한다.

보안 설계(HM-12 재검토 결과 — 신규 인증체계를 만들지 않고 기존 안전장치를
웹 경로까지 그대로 확장):
  · ForceWrite 는 WPF 쪽 LayoutCanvasViewModel.IsForceWriteLocked(기본값=잠금)
    를 그대로 재사용 — WPF 콘솔에서 🔒 버튼으로 잠금 해제하기 전에는 웹에서도
    ForceWrite 가 거부된다. ACK 는 WPF 알람 팝업과 동일하게 잠금과 무관하게 허용.
  · ForceWrite 는 기존과 동일하게 API Key(Collector Security.ForceWriteApiKey
    검증)를 웹 페이지에서 직접 입력받아 매 요청마다 함께 전송.
  · ⚠ 잔여 리스크: HmiWebHostService 의 CORS 정책은 변경 없이 전체 허용
    (SetIsOriginAllowed(_ => true))이므로, 같은 네트워크에서 웹 포트에 접근
    가능한 누구나 두 메서드 호출을 "시도"할 수 있다 — 실제 통제는 위 잠금+
    API Key 2단계로 이루어진다. 더 강한 네트워크 격리/인증이 필요하면 후속
    Step 에서 별도 검토.

WebNodeDto.cs: AlarmKey/BoundCollectorId/BoundPlcId/BoundTagId/BoundTagName
5개 필드 추가(ACK/ForceWrite 요청을 어느 노드/Tag/Collector 로 라우팅할지
식별하기 위함 — 전부 서버가 nodeId 로 노드를 다시 찾아 검증하므로 클라이언트가
값을 변조해도 실제 라우팅에는 영향 없음, 표시 참고용).

HmiWebHostService.cs: 생성자에 CollectorConnectionManager 의존성 추가 +
StartAsync() 의 ASP.NET Core 자체 DI 컨테이너(builder.Services)에 그 인스턴스와
LayoutCanvasViewModel 인스턴스를 그대로 등록(WPF DI 컨테이너와 별개이므로 Hub
생성자가 참조하려면 공유 등록이 필요) + _BuildSnapshotAsync() 에 신규 DTO
필드 5개 채우기 추가.

HmiWebHub.cs: 빈 Hub → 클라이언트 호출 가능한 2개 메서드 추가.
  · AcknowledgeAsync(nodeId, alarmKey): nodeId 로 노드의 BoundCollectorId 를
    찾아 CollectorConnectionManager.AcknowledgeAlarmAsync 로 위임("발생
    출처로만 전송" 원칙 그대로 유지).
  · ForceWriteAsync(nodeId, value, apiKey): IsForceWriteLocked 체크 →
    IsBound 체크 → CollectorConnectionManager.ForceWriteAsync 위임, 결과
    (ForceWriteResult)를 그대로 반환.
  둘 다 LayoutCanvasViewModel.Nodes(WPF UI 스레드 소유)를 참조하는 부분은
  Application.Current.Dispatcher.InvokeAsync 로 마샬링(프로젝트 UI 마샬링
  규칙 준수).

wwwroot/index.html: 카드 클릭/알람 배지 클릭에 대응하는 모달 UI 신규 추가.
  · 알람 배지(⚠) 클릭 → ACK 확인 모달 → conn.invoke("AcknowledgeAsync", ...).
  · Tag 바인딩된 카드 클릭 → 값 입력+API Key 입력 모달 →
    conn.invoke("ForceWriteAsync", ...) → 성공/실패 메시지 표시(잠금 상태면
    서버가 즉시 실패 메시지 반환).
  · render() 가 매번 카드를 새로 그리는 기존 구조를 유지하되, 클릭 핸들러가
    참조할 최신 노드 데이터를 latestNodes(Map) 로 별도 캐시.
  · 헤더 문구에서 "(읽기 전용)" 문구 제거(더 이상 읽기 전용이 아니므로).

전체 4개 파일 수정: WebNodeDto.cs / HmiWebHostService.cs / HmiWebHub.cs /
wwwroot/index.html.
사용자 빌드·런타임 확인 필요(★ ACK/ForceWrite 는 실제 PLC/Collector 에
영향을 주는 기능이므로, 반드시 테스트 환경에서 먼저 확인 — 특히 (1) WPF
잠금 상태에서 웹 ForceWrite 가 정상 거부되는지, (2) 잠금 해제 후 정상
동작하는지, (3) 여러 브라우저/여러 Collector 동시 접속 시 라우팅이 섞이지
않는지).
완료 확인 후 → HM-22(설정 UI 편집 화면) 착수. ← 다음 시작점
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
| **v11.28** | **C-SET-01 코드 완료 (2026-07-20, 빌드 확인 대기) — Collector 환경설정 탭.** |
| | | **개별 프로그램 로컬 설정탭 착수 순서 확정(Collector→Manager→Monitor→HMI,** |
| | | **사용자 확인) 후 Collector 부터 구현. SettingsViewModel(11섹션+유효성검사+** |
| | | **PasswordBox 5종 마스킹) / SettingsView / MainWindow·MainViewModel 탭 연동 /** |
| | | **App.xaml.cs DI 등록. CollectorSettingsLoader 에 SaveAsync/** |
| | | **GenerateNewCollectorId 추가(기존 메서드 미변경). 다음: Manager 환경설정 탭** |
| **v11.29** | **C-SET-01 후속(Manager) 코드 완료 (2026-07-20, 빌드 확인 대기) — Collector** |
| | | **빌드 확인 완료 보고 받고 이어서 진행. Resource(CpuWarnPercent/MemoryWarnMb)** |
| | | **1개 섹션만 대상이라 좌측 네비게이션 없이 단일 카드로 구성. ManagerSettingsLoader** |
| | | **에 SaveAsync 가 이미 있어 신규 추가 불필요. ResourceSettings 가 실행 중인** |
| | | **ProcessCardViewModel 에 참조로 공유되어 저장 전에도 즉시 반영되는 특성 확인·** |
| | | **문서화. 다음: Monitor 환경설정 탭(Web Enabled/Port 1개 항목)** |
| **v11.30** | **C-SET-01 후속(Studio) 코드 완료 (2026-07-20, 빌드 확인 대기) — 사용자 요청으로** |
| | | **원래 계획(Collector/Manager/Monitor/HMI)에 없던 Studio 도 트랙에 추가. Studio 는** |
| | | **자체 settings.json 이 없어 studio-settings.json(Log/Editor) 신규 도입.** |
| | | **LogManager.Start()/DeviceTreeViewModel(Undo maxSize)/MainViewModel(저장이력** |
| | | **개수) 가 전부 DI 그래프 구성 시점(동기)에 값이 필요해 StudioSettingsLoader.** |
| | | **LoadSync() 동기 로더를 OnStartup 맨 앞에 추가(다른 프로그램과 다른 설계).** |
| | | **DeviceTreeViewModel _history 필드초기화 → 생성자 본문 이동. 다음: Monitor** |
| **v11.31** | **C-SET-01 후속(Monitor) 코드 완료 (2026-07-20, 빌드 확인 대기) — Web(자체** |
| | | **SignalR Hub Enabled/Port) 1개 섹션만 대상, Manager 와 동일하게 단일 카드로** |
| | | **구성. MonitorSettingsLoader 에 SaveAsync 가 이미 있어 신규 추가 불필요.** |
| | | **CollectorManageView.Loaded 와 SettingsView.Loaded 가 동일 로더를 각자** |
| | | **로드하는 구조(실행 순서 비의존) 확인. 이로써 Collector·Manager·Studio·** |
| | | **Monitor 4개 프로그램 로컬 설정탭 전부 코드 완료 — 남은 건 HMI 뿐** |
| **v11.32** | **HM-Base-0~HM-21 정적 사전 점검 완료 (2026-07-20) — 샌드박스에 .NET SDK** |
| | | **없어 실제 빌드 불가 확인(`dotnet` command not found), 대체로 HMI 전체 DI** |
| | | **등록↔생성자 시그니처 1:1 정적 대조 수행. MainWindow/CollectorManageViewModel/** |
| | | **LayoutCanvasViewModel/HmiWebHostService/AlarmAggregator·ViewModel 등 전부** |
| | | **일치, 불일치 0건. ForceWriteDialog/TrendWindow/SecondaryDisplayWindow 는** |
| | | **DI 미등록이 정상(코드 내 `new`로 직접 생성)임을 확인. 개별 Step 15개로** |
| | | **흩어진 체크리스트를 "🔍 HM-Base-0~HM-21 정적 사전 점검" 절 1개로 통합.** |
| | | **실제 빌드·조작 검증은 여전히 사용자 로컬 Windows 머신에서 필요** |
| **v11.33** | **C-SET-01 후속(HMI) 코드 완료 (2026-07-20, 빌드 확인 대기) — 범위 확인** |
| | | **(사용자 확인): Web+ForceWriteSecurity+Log 포함, Collectors 는 [Collector** |
| | | **관리] 탭과 중복이라 제외. HmiSettings.cs 에 LogSettings 추가 +** |
| | | **HmiSettingsLoader.LoadSync() 신규(Studio 이중 로더 패턴 이식) — Log 설정이** |
| | | **LogManager.Instance.Start() 보다 먼저 필요하기 때문. SettingsView 는** |
| | | **HMI 기존 관례를 따라 Studio(서브 VM) 대신 Monitor 패턴(DI 생성자 주입 +** |
| | | **ContentControl 호스트) 재사용. App.xaml.cs: OnStartup 맨 앞에서 LoadSync()** |
| | | **호출 후 LogConfig 하드코딩값 교체, _ConfigureServices(HmiSettingsLoader)** |
| | | **시그니처 변경. MainWindow/HmiMainViewModel 에 환경설정 탭(인덱스 4) 추가.** |
| | | **이로써 Collector·Manager·Studio·Monitor·HMI 5개 프로그램 전부 로컬** |
| | | **환경설정 탭 코드 완료 — 다음: HM-22(Manager 원격 통합 설정관리 화면)** |
| **v11.34** | **HM-23 코드 완료 (2026-07-20, 빌드 확인 대기) — 사용자 요청("실제 HMI** |
| | | **에서 쓰는 컨트롤러 추가")으로 장비 노드 5종 신규: Pump(펌프)/SignalTower** |
| | | **(신호등)/Gauge(게이지)/Switch(스위치)/Heater(히터). HM-04 확립 3단계** |
| | | **(모델 추가 → DeviceControlBase 상속 컨트롤 추가 → DataTemplate 매핑)** |
| | | **무수정 재사용. SignalTower: EngValue 상태코드(0~3+)로 소등/녹색/황색점멸/** |
| | | **적색점멸. Gauge: TankControl 다이얼에 녹/황/적 위험구간 밴드 추가한 범용** |
| | | **계기판. Switch: 알약형 트랙+슬라이딩 원 On/Off. Heater: 온도 3단계** |
| | | **색상(회색/황색/적색+발광펄스). 기존 Motor/Valve/Tank/Conveyor 4종 및** |
| | | **LayoutNode.cs/LayoutCanvasView.xaml 기존 코드는 무수정(추가만)** |
| **v11.35** | **HM-22 코드 완료 (2026-07-20, 빌드 확인 대기) — Manager 원격 통합** |
| | | **설정관리 화면. 사용자 확인: 기존 NamedPipe 헬스체크 채널 확장 방식(신규** |
| | | **파이프 없음). HealthPipeServer(Contracts) 에 get-settings/save-settings** |
| | | **커맨드 추가(Base64 페이로드, 기존 ping/pong 무수정) + Studio/Collector/** |
| | | **Monitor/HMI App.xaml.cs 에 File.ReadAllText/WriteAllText 콜백 연결.** |
| | | **Manager HealthCheckService 에 GetSettingsAsync/SaveSettingsAsync 클라** |
| | | **이언트 메서드 추가. 신규 [🌐 원격 설정] 탭(인덱스 6): RemoteSettingsViewModel** |
| | | **(대상 4개 고정 목록 + CanExecute 자동 비활성화) + RemoteSettingsView(콤보+** |
| | | **Consolas JSON 편집기). 저장은 파일만 갱신 — 대상 재시작 필요 원칙 유지.** |
| | | **이로써 설정(Settings) UI 트랙 전체 완료 — 로컬 5개 프로그램 환경설정 탭 +** |
| | | **Manager 원격 통합 조회·저장까지 코드 완료** |
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
| **v11.13** | **HM-07 빌드·런타임 확인 완료(사용자 직접 검증) + HM-08(알람 오버레이)** |
| | | **코드 완료(빌드 확인 대기)** |
| | | **AbstractLayoutNode: HasActiveAlarm/AlarmKey/AlarmLevel/AlarmStatusText/** |
| | | **AlarmMessage/AlarmTimeText 추가 · AlarmLevelColorConverter 신규** |
| | | **LayoutCanvasViewModel: AlarmChanged 구독 + _OnAlarmChanged(Recovered 시** |
| | | **필드 초기화, 그 외 배지 갱신) + AcknowledgeAlarmCommand(발생 출처로만 전송)** |
| | | **DeviceControlBase: 알람 배지(Button)+상세 팝업(Popup)+ACK 버튼 추가 —** |
| | | **★ WPF Popup 은 별도 시각 트리 루트라 RelativeSource/DataContext 자동** |
| | | **상속이 신뢰 불가 → Popup.DataContext 는 ElementName 바인딩, ACK 버튼은** |
| | | **코드비하인드 Click+VisualTreeHelper 상위 탐색으로 우회(설계 결정 기록)** |
| | | **다음 세션 시작점: HM-08 빌드 확인 → HM-09(ForceWrite 제어 다이얼로그) 착수** |
| **v11.14** | **HM-09(ForceWrite 제어 다이얼로그) 코드 완료(빌드 확인 대기) — 아이콘** |
| | | **더블클릭 → 값 입력 → SignalR Invoke("ForceWrite") 원격 강제쓰기** |
| | | **Models/ForceWriteResult.cs(신규) · Views/LayoutCanvas/ForceWriteDialog.** |
| | | **xaml(.cs)(신규 — Collector 자체 다이얼로그 이식) · CollectorConnection/** |
| | | **ConnectionManager.ForceWriteAsync 추가("발생 출처로만 전송" 원칙) ·** |
| | | **LayoutCanvasViewModel.ForceWriteAsync(공개 메서드, 커맨드 아님) ·** |
| | | **LayoutCanvasView.xaml.cs(더블클릭 분기, 드래그와 분리)** |
| | | **검증(Enabled/ApiKey/Tag존재/활성/형식)은 전부 Collector 측(C-15) 위임 —** |
| | | **HMI 는 얇은 호출 래퍼+다이얼로그만 담당** |
| | | **다음 세션 시작점: HM-08~09 빌드 확인 → HM-10(다중 화면 관리) 착수** |
| **v11.15** | **HM-10(다중 화면 관리) 코드 완료(빌드 확인 대기) — HM-07 콤보박스+** |
| | | **이름편집 UI를 탭 바로 교체(사용자가 3안 중 "탭 바로 교체" 선택)** |
| | | **LayoutPageViewModel.IsActive(탭 강조)/IsEditingName(더블클릭 시 인라인** |
| | | **이름편집) 추가 · LayoutCanvasViewModel.SelectPage(공개 메서드, SelectNode와** |
| | | **동일 패턴) · LayoutCanvasView.xaml PageTabTemplate(신규, MainWindow 5탭과** |
| | | **동일한 AccFaintBrush/AccBrush 강조색 재사용) · LayoutCanvasView.xaml.cs** |
| | | **탭 클릭/더블클릭/이름편집 확정·취소 핸들러 3개 추가**|
| | | **데이터 구조·저장 파일·커맨드는 HM-07과 동일 — UI 표현 방식만 교체** |
| | | **다음 세션 시작점: HM-08~10 빌드 확인 → HM-11(웹 브라우저 표시 확장) 착수** |
| **v11.16** | **HM-11(웹 브라우저 표시 확장) 코드 완료(빌드 확인 대기) — Collector** |
| | | **C-11·Monitor MN-05 실제 코드 조사 후 검증된 패턴 조합** |
| | | **IIoT.HMI.csproj: FrameworkReference Microsoft.AspNetCore.App 재도입 +** |
| | | **Microsoft.Extensions.DependencyInjection 명시버전 제거(Monitor MN-05** |
| | | **FIX 동일 적용 — NU1605 재발 방지) + wwwroot Content Include** |
| | | **HmiSettings.Web(Enabled/Port=7880) · Core/Web/(WebNodeDto·HmiWebHub·** |
| | | **HmiWebHostService 신규) · wwwroot/index.html(신규, Collector 페이지와** |
| | | **동일 다크 테마+SignalR CDN 스타일) · App.xaml.cs(win.Loaded 오케스트레이션** |
| | | **최초 도입+OnExit 정리)** |
| | | **1차 범위=읽기 전용(ACK/ForceWrite/애니메이션 웹 미제공, WPF 활성화면** |
| | | **1개만 미러링) — HM-11-EX 로 후속·보류 항목에 등록** |
| | | **★ FrameworkReference 추가로 NU1605 위험 있음 — 빌드 확인 최우선** |
| | | **다음 세션 시작점: HM-08~11 빌드 확인 → HM-12(보안) 착수** |
| **v11.17** | **HM-12(보안) 코드 완료(빌드 확인 대기) — 사용자가 3가지 항목** |
| | | **모두 선택: 화면 잠금 모드(권장)+활성 알람 중 강제쓰기 경고+세션 내** |
| | | **API Key 임시 기억**|
| | | **HmiSettings.ForceWriteSecurity(DefaultLocked) 추가 ·** |
| | | **LayoutCanvasViewModel: IsForceWriteLocked/LockButtonLabel/** |
| | | **ToggleForceWriteLockCommand + _apiKeyCache(메모리 전용)/GetCachedApiKey** |
| | | **ForceWriteDialog: AlarmWarningPanel(체크박스 확인 전 [쓰기] 비활성화)+** |
| | | **PrefillApiKey · LayoutCanvasView: 잠금 토글 버튼+더블클릭 시 잠금/알람/** |
| | | **API Key 캐시 반영**|
| | | **API Key 는 디스크에 저장되지 않음(세션 중 메모리 캐시만)**|
| | | **다음 세션 시작점: HM-Base-0~HM-12 전체 빌드·런타임 확인 → HMI 1차** |
| | | **마감 판단 또는 Sequence 착수** |
| **v11.18** | **HM-13(정리) 코드 완료(빌드 확인 대기) — 사용자 질문("현황판, 알림** |
| | | **탭의 화면 변경은 없는가??") 계기로 확인한 결과, HM-03~12 가 전부** |
| | | **[레이아웃 편집] 탭에 구현되어 이미 생산현황판 역할을 겸하고 있었음** |
| | | **사용자가 "현황판 탭 제거(레이아웃편집으로 통합, 권장)" 단독 선택** |
| | | **HmiMainViewModel.cs(IsDashboardTab 제거, 나머지 Is*Tab 인덱스 0~3** |
| | | **재정렬) · MainWindow.xaml(현황판 버튼/placeholder Grid 삭제,** |
| | | **CommandParameter·TabBtn 스타일 키 재번호, 탭 4개로 재정렬)**|
| | | **미선택 항목(기록용): "알람 탭 전체 목록/이력 구현" — HM-EX 후속 후보 등록**|
| | | **다음 세션 시작점: HM-Base-0~HM-13 전체 빌드·런타임 확인 → HMI 1차** |
| | | **마감 판단 또는 Sequence 착수** |
| **v11.19** | **HM-14(알람 탭 실시간 목록) 코드 완료(빌드 확인 대기) — 사용자가** |
| | | **"실시간 목록만(권장)" 범위 선택(SQLite 이력 저장은 HM-EX 보류)** |
| | | **Monitor MN-03+MN-EX-06 이식: AlarmRow · AlarmAggregator(Collector** |
| | | **ConnectionManager.AlarmChanged 직접 구독) · AlarmStatusColorConverter** |
| | | **· AlarmViewModel(Collector/레벨/상태 필터+검색+ACK) · AlarmView(그리드,** |
| | | **Collector별 그룹핑·최신순) · MainWindow(알람 placeholder→ContentControl)**|
| | | **다음 세션 시작점: HM-Base-0~HM-14 전체 빌드·런타임 확인 → HMI 1차** |
| | | **마감 판단 또는 Sequence 착수** |
| **v11.20** | **HM-15(로그 탭) 코드 완료(빌드 확인 대기) — 사용자가 HM-14와 "같이"** |
| | | **로그 탭도 요청. lssLib.Log LogViewerControl 은 demo 전용이라 직접** |
| | | **참조 불가함을 재확인 → Studio/Collector/Monitor 공통 LogPanelView 패턴** |
| | | **(LogManager.Instance.LogAdded 구독+레벨/Source 필터+지우기) 그대로 이식**|
| | | **Views/Log/LogPanelView.xaml(.cs)(신규) · MainWindow(로그 placeholder→** |
| | | **ContentControl LogHost) · App.xaml.cs(ViewModel 없는 완결형 View 등록)**|
| | | **★ 이 Step으로 4개 탭 전부 실제 화면으로 채워짐 — 남은 placeholder 없음**|
| | | **다음 세션 시작점: HM-Base-0~HM-15 전체 빌드·런타임 확인 → HMI 1차** |
| | | **마감 판단 또는 Sequence 착수** |
| **v11.21** | **HMI 1차 마감 여부 검토 결과 — 사용자가 "1차 마감 보류, HM-EX 후보** |
| | | **7건 전체 착수"를 선택. HM-16~22 확장 로드맵으로 등록(작은/독립 항목→** |
| | | **큰/의존 항목 순): 알람이력SQLite→히스토리트렌드→캡처·PDF→다중모니터→** |
| | | **장비아이콘실형상화→웹ACK/ForceWrite→설정UI편집화면**|
| | | **HM-16(알람 이력 SQLite) 코드 완료(빌드 확인 대기) — Monitor MN-EX-02** |
| | | **AlarmHistoryService 이식(90일 보존, 저장 전용·조회 UI 없음)**|
| | | **AlarmAggregator.AlarmRecorded 이벤트 추가 · csproj에 lssLib.DB/** |
| | | **lssLib.DB.Sqlite ProjectReference 신규 추가**|
| | | **다음 세션 시작점: HM-Base-0~HM-16 빌드 확인(★신규 참조 2건 우선 확인)** |
| | | **→ HM-17(히스토리 트렌드 오버레이) 착수** |
| **v11.22** | **HM-17(실시간 트렌드 창) 코드 완료(빌드 확인 대기) — 조사 결과** |
| | | **Collector 시계열 저장소에 조회 API가 전혀 없어(TrendQueryService 도** |
| | | **프로세스 내부 전용) "과거 이력 조회"는 불가 확인 → 사용자가 "실시간** |
| | | **트렌드만(권장)" 선택, Monitor MN-06 과 동일 범위**|
| | | **레이아웃 편집 탭 카드 우클릭 → TrendWindow(OxyPlot, 롤링 300포인트)**|
| | | **비모달 다중 오픈 가능. OxyPlot.Wpf 2.2.0 패키지 신규 추가**|
| | | **다음 세션 시작점: HM-Base-0~HM-17 빌드 확인 → HM-18(캡처/PDF) 착수** |
| **v11.23** | **HM-18(화면 캡처 PNG) 코드 완료(빌드 확인 대기) — 조사 결과 5개** |
| | | **프로그램 전부 PDF 라이브러리 선례 없음 확인 → 사용자가 "PNG 캡처만** |
| | | **(권장)" 선택, PDF 리포트는 범위 밖으로 확정**|
| | | **레이아웃 편집 탭 툴바에 "📷 캡처" 버튼 추가 — RenderTargetBitmap+** |
| | | **PngBitmapEncoder(WPF 내장) 로 현재 화면을 PNG 저장. 새 의존성 없음**|
| | | **다음 세션 시작점: HM-Base-0~HM-18 빌드 확인 → HM-19(다중 모니터) 착수** |
| **v11.24** | **HM-19(다중 모니터 지원) 코드 완료(빌드 확인 대기) — 같은** |
| | | **LayoutCanvasViewModel 을 공유하는 두 번째 View 를 독립 창(Owner 없음)** |
| | | **으로 띄우는 방식으로 구현, 두 창이 실시간 동기화됨**|
| | | **★ 착수 전 조사에서 심각한 잠재 버그 발견·차단: InitializeAsync() 가** |
| | | **매번 Pages.Clear()+파일 재로드를 해서, 보조 창을 열 때마다 미저장** |
| | | **편집 내용이 사라질 뻔함 — 멱등 가드(Pages.Count>0 이면 즉시 반환) 추가**|
| | | **App.xaml.cs 에 ShutdownMode=OnMainWindowClose 명시(메인 창 닫으면** |
| | | **보조 창도 함께 종료)**|
| | | **다음 세션 시작점: HM-Base-0~HM-19 빌드 확인(★특히 정상 종료 여부)** |
| | | **→ HM-20(장비 아이콘 실형상화) 착수** |
| **v11.25** | **HM-20(장비 아이콘 실형상화, HM-04-EX) 코드 완료(빌드 확인 대기)** |
| | | **DeviceControlBase: IconText(TextBlock)→IconHost(Grid, 아이콘 슬롯)** |
| | | **+IconGlyphText(기본 글리프, 하위호환) 로 구조 확장**|
| | | **Motor: 원형 하우징+3개 회전 날개 벡터 그림, 기존 회전 애니메이션 로직** |
| | | **무수정(대상만 이동) · Conveyor: 롤러+벨트+화물 3개, 흐름 로직 무수정**|
| | | **Tank: 원통+뚜껑 정적 장식만 추가, 수위 게이지 로직 완전 무수정(최저** |
| | | **위험) · Valve: 배관+바디+손잡이, 색상전환에 회전(열림/닫힘) 추가**|
| | | **★ 코드로 직접 그린 벡터라 실제 화면 육안 확인 필요 — 좌표/비율 조정은** |
| | | **피드백 주시면 빠르게 반영 가능**|
| | | **다음 세션 시작점: HM-Base-0~HM-20 빌드 확인(★비율/색상 육안 확인 우선)** |
| | | **→ HM-21(웹 ACK/ForceWrite) 착수** |
| **v11.26** | **HM-20b(탱크 게이지·컨베이어 애니메이션 재작업) 코드 완료(빌드** |
| | | **확인 대기) — 사용자 피드백: "게이지는 차량 속도계처럼, 컨베이어는** |
| | | **실제로 돌아가는 느낌"**|
| | | **Tank: 막대 게이지 폐기 → PathGeometry+ArcSegment 240도 다이얼+눈금+** |
| | | **회전 바늘(RotateTransform, 400ms 이징 애니메이션)**|
| | | **Conveyor: 화물 왕복 폐기 → 롤러 스포크 회전(RotateTransform 연속) +** |
| | | **벨트 점선 StrokeDashOffset 연속 스크롤(AutoReverse 없음)**|
| | | **다음 세션 시작점: HM-Base-0~HM-20(b 포함) 빌드 확인(★바늘 회전 중심/** |
| | | **벨트 스크롤 육안 확인) → HM-21(웹 ACK/ForceWrite) 착수** |
| **v11.27** | **컨트롤러 실형상화 현황 점검(Motor/Valve 는 이미 HM-20 1차로 완료,** |
| | | **추가 조치 불필요 — Tank/Conveyor 만 HM-20b 대상이었음) +** |
| | | **HM-21(웹 ACK/ForceWrite) 코드 완료(빌드 확인 대기)**|
| | | **WebNodeDto: AlarmKey/BoundCollectorId/BoundPlcId/BoundTagId/** |
| | | **BoundTagName 5개 필드 추가(라우팅 식별용)**|
| | | **HmiWebHostService: CollectorConnectionManager 의존성 추가 + 웹 자체** |
| | | **DI 컨테이너에 공유 인스턴스 등록(WPF DI 와 별개 컨테이너이므로 필요)**|
| | | **HmiWebHub: AcknowledgeAsync/ForceWriteAsync 2개 클라이언트 호출** |
| | | **메서드 추가 — ForceWrite 는 기존 IsForceWriteLocked(HM-12) 그대로** |
| | | **재사용해 잠금 시 거부 + API Key 검증은 Collector 기존 로직 그대로**|
| | | **index.html: 알람 배지 클릭→ACK 모달, 바인딩 카드 클릭→ForceWrite** |
| | | **모달(값+API Key 입력) 추가, "(읽기 전용)" 문구 제거**|
| | | **⚠ CORS 는 기존 그대로 전체 허용 — 실 통제는 잠금+API Key 2단계임을** |
| | | **핸드오프에 명시(잔여 리스크로 기록)**|
| | | **다음 세션 시작점: HM-Base-0~HM-21 빌드·런타임 확인(★PLC 영향 기능이므로** |
| | | **테스트 환경에서 먼저 확인) → HM-22(설정 UI 편집 화면) 착수** |

---

*다음 세션: 이 파일을 먼저 읽고 → HM-Base-0~HM-21 전체 빌드·런타임 확인(★웹 ACK/ForceWrite 는 테스트 환경에서 먼저 확인) → HM-22(설정 UI 편집 화면) 착수*
