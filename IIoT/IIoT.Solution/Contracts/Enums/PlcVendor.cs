// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Enums/PlcVendor.cs
//  역할: PLC 제조사 열거형 — 드라이버 플러그인 식별에 사용
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

namespace IIoT.Contracts;

/// <summary>
/// PLC 제조사 구분.
/// Studio의 PlcVendor(Models/Enums.cs)와 동일 값을 유지한다.
/// </summary>
public enum PlcVendor
{
    /// <summary>Modbus TCP / RTU (제조사 무관 범용)</summary>
    Modbus      = 0,

    /// <summary>미쓰비시 MELSEC Q/L/FX 시리즈 (MC 프로토콜)</summary>
    Mitsubishi  = 1,

    /// <summary>LS산전 XGT / XGB 시리즈 (XGT 프레임)</summary>
    LsSanXgt    = 2,

    /// <summary>지멘스 S7-300/400/1200/1500 (ISO-on-TCP)</summary>
    Siemens     = 3,

    /// <summary>오므론 CS/CJ/CP/NX 시리즈 (FINS)</summary>
    Omron       = 4,

    /// <summary>사용자 정의 / 기타 제조사</summary>
    Free        = 99
}
