// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · NodeKind.cs
//  역할: 장비 트리 노드 종류 열거형
//  생성: 2025-05-22
//  수정: 2025-05-23 v2 — Sensor 추가 (물리 레이어)
//        Tag/Sensor 이중 레이어 구조 반영
// ══════════════════════════════════════════════════════════

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// 장비 트리 노드 종류.
///
/// ── 수집 레이어 (PLC 통신 구조) ─────────────────────────
///   Plc  : PLC 슬롯/채널 (통신 설정, IP/포트/프로토콜)
///   Tag  : PLC 레지스터 주소 (Plc 하위)
///          Address, DataType, PollRateMs
///
/// ── 물리 레이어 (실 장비 관점) ──────────────────────────
///   Sensor : 실 물리 센서 표현 (Device 하위 직접)
///            Name, Unit, ScaleConfig, AlarmConfig, TagRefs
///
/// 핵심: Tag는 "어디서 읽느냐", Sensor는 "무엇을 보여주느냐"
/// </summary>
public enum NodeKind
{
    /// <summary>논리 그룹 (무제한 중첩)</summary>
    Group,

    /// <summary>실 장비 — 수집 레이어(PLC/Tag)와 물리 레이어(Sensor) 모두 포함</summary>
    Device,

    /// <summary>PLC 슬롯/채널 — 수집 레이어 (Device 또는 Plc 하위)</summary>
    Plc,

    /// <summary>
    /// PLC 레지스터 주소 — 수집 레이어 (Plc 하위)
    /// RawValue 보유, ScaleConfig/AlarmConfig 없음
    /// </summary>
    Tag,

    /// <summary>
    /// 물리 센서 표현 — 물리 레이어 (Device 하위 직접)
    /// TagRef로 Tag를 참조, ScaleConfig/AlarmConfig/Formula 보유
    /// Sensor = "베어링온도1 (°C)" ← Tag = "MW100 = 2847"
    /// </summary>
    Sensor,
}