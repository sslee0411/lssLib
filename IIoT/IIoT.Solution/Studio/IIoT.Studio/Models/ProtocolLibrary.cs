// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/ProtocolLibrary.cs
//  역할: 프로토콜 라이브러리 항목 모델 (S-프로토콜01)
//        PLC/장비가 공유 참조하는 통신 프로토콜 정의 1건.
//        읽기 블록 N개 + 쓰기 블록 N개로 구성되며, 각 블록은
//        주소범위(기존 Modbus/미쓰비시 등 표준 드라이버용) 또는
//        커스텀 바이트 프레임(STX/LEN/CMD/DATA/CRC — UseFraming=true 일 때)
//        둘 다로 쓰일 수 있다.
//
//  ★ 설계 메모(2026-07-20, 사용자 확인): 두 가지 용도를 모두 지원
//    ① 기존 PLC(Modbus/미쓰비시 등)에서 Tag 들을 주소범위 단위로 묶어
//       일괄 읽기/쓰기 블록으로 구성(UseFraming=false, CmdCode 미사용)
//    ② 독자 바이너리 프로토콜을 STX/LEN/CMD/DATA/CRC 프레임으로 시각 정의
//       (UseFraming=true) — 다만 이를 실제로 실행하는 Collector 측
//       Raw/커스텀 드라이버는 아직 없음(후속 작업, 핸드오프 참조)
//
//  S-프로토콜01: 신규
//  S-프로토콜01 Step B 후속(2026-07-20): ProtocolField.ScaleEntryId 추가 —
//    Tag(TagTreeNode.ScaleEntryId)와 동일하게 스케일 라이브러리(ScaleEntry:
//    RawMin/RawMax/EngMin/EngMax) 항목을 참조해 Collector 가 실제 선형/수식
//    변환을 적용할 수 있도록 함. 기존 ScaleMin/ScaleMax(참고용 표시 범위)는
//    그대로 유지 — 둘은 독립적인 값(ScaleEntryId 가 실제 변환, ScaleMin/Max 는
//    여전히 참고용 표시 범위일 뿐 변환식이 아님).
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace IIoT.Studio.Models;

// §1 ─ 블록 내 필드 ───────────────────────────────────────

/// <summary>
/// 블록 안의 개별 값(필드) 정의.
/// TagTemplateItem과 유사하나 프로토콜 블록 전용 — 여기서 정의한 필드는
/// 블록이 적용된 PLC/장비 하위에 Tag 를 자동 생성하는 데 사용될 수 있다.
/// </summary>
public partial class ProtocolField : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty] private string _name = "필드";

    /// <summary>블록 시작 기준 바이트 오프셋</summary>
    [ObservableProperty] private int _byteOffset;

    /// <summary>lssLib BufType 이름 (FloatLE/FloatBE/Int16/UInt16/Int32/UInt32/Bool/Byte/String)</summary>
    [ObservableProperty] private string _bufType = "UInt16";

    [ObservableProperty] private string _unit = string.Empty;

    /// <summary>참고용 표시 범위(스케일 라이브러리 별도 연결도 가능하므로 필수는 아님)</summary>
    [ObservableProperty] private double _scaleMin;
    [ObservableProperty] private double _scaleMax = 100;

    /// <summary>스케일 라이브러리(ScaleEntry) 참조 ID — null 이면 Raw 값 그대로 발행.
    /// TagTreeNode.ScaleEntryId 와 동일한 참조 방식(S-프로토콜01 Step B 후속).</summary>
    [ObservableProperty] private Guid? _scaleEntryId;
}

// §2 ─ 읽기/쓰기 블록 ─────────────────────────────────────

/// <summary>
/// 읽기 또는 쓰기 블록 1건.
/// UseFraming=false(기본) — StartAddress/Length 만으로 표준 드라이버(Modbus 등)의
///   레지스터 범위 일괄 읽기/쓰기 단위를 표현.
/// UseFraming=true — CmdCode 등 프레임 필드까지 사용해 커스텀 바이너리 프로토콜
///   프레임(STX+LEN+CMD+DATA+CRC)의 DATA 구간을 표현.
/// </summary>
public partial class ProtocolBlock : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty] private string _name = "블록";
    [ObservableProperty] private string _description = string.Empty;

    /// <summary>블록 시작 주소(레지스터 주소 문자열, 예: "40001", "D100")</summary>
    [ObservableProperty] private string _startAddress = "40001";

    /// <summary>블록 길이 — 표준 드라이버는 레지스터 개수, 커스텀 프레임은 DATA 바이트 수</summary>
    [ObservableProperty] private int _length = 2;

    /// <summary>커스텀 프레임 명령 코드(16진 문자열, 예: "03"). 표준 드라이버 블록에서는 미사용(빈 값)</summary>
    [ObservableProperty] private string _cmdCode = string.Empty;

    public ObservableCollection<ProtocolField> Fields { get; } = new();

    /// <summary>Fields 증감 시 PreviewSummary 를 자동 갱신 — ViewModel 이
    /// 외부에서 protected OnPropertyChanged() 를 직접 호출할 필요가 없도록 함.</summary>
    public ProtocolBlock() => Fields.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PreviewSummary));

    public string PreviewSummary =>
        string.IsNullOrWhiteSpace(CmdCode)
            ? $"{StartAddress} · {Length}개 · 필드 {Fields.Count}개"
            : $"{StartAddress} · CMD {CmdCode} · 필드 {Fields.Count}개";
}

// §3 ─ 프로토콜 정의 ──────────────────────────────────────

/// <summary>
/// 프로토콜 라이브러리 항목 1건.
/// 읽기 블록 N개 + 쓰기 블록 N개로 구성 — PLC/장비 편집기에서 ProtocolEntryId 로 참조.
/// </summary>
public partial class ProtocolEntry : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty] private string _name = "새 프로토콜";
    [ObservableProperty] private string _description = string.Empty;

    // ── 프레임 설정 (커스텀 바이너리 프로토콜 — 선택 사항) ──

    /// <summary>true 면 STX/LEN/CRC 프레임 설정을 사용(커스텀 프로토콜).
    /// false 면 블록은 순수 주소범위 일괄 읽기/쓰기 단위로만 쓰인다(표준 드라이버).</summary>
    [ObservableProperty] private bool _useFraming;

    [ObservableProperty] private string _stxHex = "AA";
    [ObservableProperty] private bool   _hasLengthField = true;

    /// <summary>CRC 방식: None/Crc16Modbus/Xor/Sum</summary>
    [ObservableProperty] private string _crcType = "None";

    // ── 블록 N개 ──

    public ObservableCollection<ProtocolBlock> ReadBlocks  { get; } = new();
    public ObservableCollection<ProtocolBlock> WriteBlocks { get; } = new();

    /// <summary>ReadBlocks/WriteBlocks 증감 시 PreviewSummary 를 자동 갱신.</summary>
    public ProtocolEntry()
    {
        ReadBlocks.CollectionChanged  += (_, _) => OnPropertyChanged(nameof(PreviewSummary));
        WriteBlocks.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PreviewSummary));
    }

    public string PreviewSummary =>
        $"읽기 {ReadBlocks.Count}개 · 쓰기 {WriteBlocks.Count}개" +
        (UseFraming ? " · 커스텀 프레임" : string.Empty);
}
