# IIoT.Solution 개발 핸드오프 파일
**작성일: 2026-07-16 | 버전: v11.1 | 다음 세션 시작점: ② IIoT.HMI HM-Base-0 착수 (세부 Step 맵 설계 먼저)**

> 새 세션 시작 시 이 파일을 가장 먼저 읽을 것.
> SKILL.md 는 함께 참조하되, **진행 상태·착수 순서는 이 핸드오프가 최우선**
> (스킬 캐시본/솔루션 루트 SKILL.md 는 구버전 — v6.3/v4.x).

---

## 📌 프로젝트 개요

C# .NET 8 / WPF / lssLib v5 기반 산업용 IIoT/SCADA 플랫폼.
위치: `D:\lssLib\IIoT\IIoT.Solution\` (프로그램별 개별 .sln 구조)

```
프로그램            상태
IIoT.Studio         ✅ 100% (설정 편집기 — 보류 4건은 Sequence 이후)
IIoT.Collector      ✅ 100% (수집+감지+저장, SignalR Hub 7878 — C-EX-11만 후속 보류)
IIoT.Monitor        ✅ 100% (실시간 모니터링, 자체 Hub 7879, MN-EX 8건 전부)
IIoT.Manager        ✅ 100% (코드+통합 빌드+런타임 확인 완료 — 2026-07-16)
IIoT.HMI            ⭕ 다음 착수 (생산현황판 — 별도 프로그램 확정)
IIoT.Sequence       ⭕ HMI 이후
공통: Contracts(플러그인 계약+Health) · Plugins(ModbusTcp/Mitsubishi/Virtual)
     · UI.Themes(7테마) · UI.Controls
★ 전체 착수 순서: Manager 마감(완료) → HMI → Sequence → Studio 보류 4건 → 전체 정리
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
전체 오류 참조표: SKILL.md "오류 빠른 참조" 절 참조
```

---

## 🔧 후속·보류 항목

```
[2차 정리 — Manager~Sequence 완료 후 일괄]
 ① Monitor MonitorMainViewModel.cs → 루트 이동 + namespace 정렬 (규칙 예외 해소)
 ② Collector/Monitor SignalR 코드의 lssLib.SignalR 공통화 검토
 ③ 이벤트 이력 DB(manager.db) 조회 UI (MG-EX-07 통합 검토)
[보류]
 MG-EX-11 웹 상태 페이지 / MG-EX-12 원격 관리 (HMI/Sequence 이후)
 C-EX-11 (Collector 후속) / Studio 보류 4건 (가상Tag·N포트·Function·프로토콜편집)
```

---

## 🔜 다음 세션 진행 순서

### ① Manager + lssLib.SignalR 통합 빌드 확인 — ✅ 완료 (2026-07-16, 사용자 직접 빌드·런타임 검토)
```
Manager 5탭 표시·카드·제어 / 트레이 상주·경고 알림 / manager.json AutoStart·스케줄·배포 /
로그탭 / SignalR Demo 셀프테스트 — 전체 정상 동작 확인됨.
```

### ② IIoT.HMI 착수 (생산현황판 — 신규 프로그램) ← 다음 시작점
```
요구사항: 모터·컨베이어 등 장비가 Tag/SignalR/DB 연동으로 애니메이션 표시,
         화면에서 직접 제어(ForceWrite — Collector C-15 연동)
착수 방식: Manager 때와 동일 — HMI\IIoT.HMI.sln 신규 생성(개별 sln 패턴),
         HM-Base-0(빈 WPF+테마)부터 증분 개발, 착수 시 세부 Step 맵 설계 먼저
기반 재사용: Tag 구독 = lssLib.SignalR 클라이언트 (Monitor MN-01B 패턴 단순화)
남은 작업: HM 세부 Step 맵(HM-Base-0 ~ HM-N) 설계 → 사용자 확인 → HM-Base-0 착수
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
| **v11.1** | **Manager + lssLib.SignalR 통합 빌드·런타임 확인 완료 (사용자 직접 검증, 2026-07-16)** |
| | | **IIoT.Manager 상태 🔄 → ✅ 100% / MG-04~07·MG-EX 12건 전부 ✅ 전환** |
| | | **다음 세션 시작점: ② IIoT.HMI HM-Base-0 (세부 Step 맵 설계 먼저)** |

---

*다음 세션: 이 파일을 먼저 읽고 → IIoT.HMI 세부 Step 맵 설계 → HM-Base-0 착수*
