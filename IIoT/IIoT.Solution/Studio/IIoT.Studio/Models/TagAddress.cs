// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/TagAddress.cs
//  역할: PLC 주소 구조 모델 (B-1: 2단계 분리)
//        PlcVendor × RegisterType → 주소 형식 자동 결정
//  S-21A B-1: 신규
//  생성: 2026-06-20
// ══════════════════════════════════════════════════════════

using System.Text.RegularExpressions;

namespace IIoT.Studio.Models;

/// <summary>
/// PLC 주소 구조체.
/// PlcVendor + RegisterType 조합에 따라 주소 형식이 결정된다.
///
/// 조합 예시:
///   Modbus    + HoldingReg → "40001"
///   Mitsubishi+ Word       → "D100"
///   Mitsubishi+ BitInput   → "X0.1"
///   Mitsubishi+ BitOutput  → "Y0.0"
///   Mitsubishi+ BitMemory  → "M100"
///   LsSanXgt  + Word       → "%MW100"
///   LsSanXgt  + BitInput   → "%IX0.0"
///   LsSanXgt  + BitOutput  → "%QX0.0"
///   Siemens   + Word       → "DB1.DBW0"
///   Siemens   + DWord      → "DB1.DBD0"
///   Siemens   + BitMemory  → "DB1.DBX0.0"
///   Omron     + Word       → "D00100"
///   Free      + Free       → 직접 입력
/// </summary>
public readonly struct TagAddress
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    public PlcVendor   Vendor   { get; init; }
    public RegisterType Register { get; init; }

    public int Number   { get; init; }  // 주 번지
    public int Bit      { get; init; }  // 비트 오프셋 (0~7), 없으면 -1
    public int DbNumber { get; init; }  // 지멘스 DB 번호

    // §2 ─ 기본값 생성 ────────────────────────────────────────

    public static TagAddress Default(PlcVendor vendor, RegisterType register) =>
        (vendor, register) switch
        {
            (PlcVendor.Modbus, RegisterType.HoldingReg)    => new() { Vendor=vendor, Register=register, Number=40001, Bit=-1 },
            (PlcVendor.Modbus, RegisterType.InputReg)      => new() { Vendor=vendor, Register=register, Number=30001, Bit=-1 },
            (PlcVendor.Modbus, RegisterType.Coil)          => new() { Vendor=vendor, Register=register, Number=1,     Bit=-1 },
            (PlcVendor.Modbus, RegisterType.DiscreteInput) => new() { Vendor=vendor, Register=register, Number=10001, Bit=-1 },
            (PlcVendor.Mitsubishi, RegisterType.Word)      => new() { Vendor=vendor, Register=register, Number=100,   Bit=-1 },
            (PlcVendor.Mitsubishi, RegisterType.DWord)     => new() { Vendor=vendor, Register=register, Number=100,   Bit=-1 },
            (PlcVendor.Mitsubishi, RegisterType.BitInput)  => new() { Vendor=vendor, Register=register, Number=0,     Bit=0  },
            (PlcVendor.Mitsubishi, RegisterType.BitOutput) => new() { Vendor=vendor, Register=register, Number=0,     Bit=0  },
            (PlcVendor.Mitsubishi, RegisterType.BitMemory) => new() { Vendor=vendor, Register=register, Number=100,   Bit=0  },
            (PlcVendor.Mitsubishi, RegisterType.LinkWord)  => new() { Vendor=vendor, Register=register, Number=100,   Bit=-1 },
            (PlcVendor.Mitsubishi, RegisterType.Timer)     => new() { Vendor=vendor, Register=register, Number=0,     Bit=-1 },
            (PlcVendor.Mitsubishi, RegisterType.Counter)   => new() { Vendor=vendor, Register=register, Number=0,     Bit=-1 },
            (PlcVendor.LsSanXgt, RegisterType.Word)        => new() { Vendor=vendor, Register=register, Number=100,   Bit=-1 },
            (PlcVendor.LsSanXgt, RegisterType.DWord)       => new() { Vendor=vendor, Register=register, Number=100,   Bit=-1 },
            (PlcVendor.LsSanXgt, RegisterType.BitInput)    => new() { Vendor=vendor, Register=register, Number=0,     Bit=0  },
            (PlcVendor.LsSanXgt, RegisterType.BitOutput)   => new() { Vendor=vendor, Register=register, Number=0,     Bit=0  },
            (PlcVendor.LsSanXgt, RegisterType.BitMemory)   => new() { Vendor=vendor, Register=register, Number=100,   Bit=0  },
            (PlcVendor.Siemens, _)                         => new() { Vendor=vendor, Register=register, DbNumber=1, Number=0, Bit=Bit_(register) },
            (PlcVendor.Omron, _)                           => new() { Vendor=vendor, Register=register, Number=100,   Bit=-1 },
            _                                              => new() { Vendor=vendor, Register=register, Number=0,     Bit=-1 }
        };

    private static int Bit_(RegisterType r) => r.IsBit() ? 0 : -1;

    // §3 ─ 주소 문자열 생성 ───────────────────────────────────

    public override string ToString() => (Vendor, Register) switch
    {
        // ── Modbus ───────────────────────────────────────────
        (PlcVendor.Modbus, _) =>
            Number.ToString(),

        // ── 미쓰비시 ──────────────────────────────────────────
        (PlcVendor.Mitsubishi, RegisterType.Word)      => $"D{Number}",
        (PlcVendor.Mitsubishi, RegisterType.DWord)     => $"D{Number}",
        (PlcVendor.Mitsubishi, RegisterType.BitInput)  => Bit >= 0 ? $"X{Number}.{Bit}" : $"X{Number}",
        (PlcVendor.Mitsubishi, RegisterType.BitOutput) => Bit >= 0 ? $"Y{Number}.{Bit}" : $"Y{Number}",
        (PlcVendor.Mitsubishi, RegisterType.BitMemory) => $"M{Number}",
        (PlcVendor.Mitsubishi, RegisterType.LinkWord)  => $"W{Number}",
        (PlcVendor.Mitsubishi, RegisterType.Timer)     => $"TN{Number}",
        (PlcVendor.Mitsubishi, RegisterType.Counter)   => $"CN{Number}",

        // ── LS산전 ────────────────────────────────────────────
        (PlcVendor.LsSanXgt, RegisterType.Word)        => $"%MW{Number}",
        (PlcVendor.LsSanXgt, RegisterType.DWord)       => $"%MD{Number}",
        (PlcVendor.LsSanXgt, RegisterType.BitInput)    => Bit >= 0 ? $"%IX{Number}.{Bit}" : $"%IX{Number}",
        (PlcVendor.LsSanXgt, RegisterType.BitOutput)   => Bit >= 0 ? $"%QX{Number}.{Bit}" : $"%QX{Number}",
        (PlcVendor.LsSanXgt, RegisterType.BitMemory)   => Bit >= 0 ? $"%MX{Number}.{Bit}" : $"%MX{Number}",
        (PlcVendor.LsSanXgt, RegisterType.LinkWord)    => $"%LW{Number}",

        // ── 지멘스 ────────────────────────────────────────────
        (PlcVendor.Siemens, RegisterType.Word)         => $"DB{DbNumber}.DBW{Number}",
        (PlcVendor.Siemens, RegisterType.DWord)        => $"DB{DbNumber}.DBD{Number}",
        (PlcVendor.Siemens, RegisterType.BitMemory)    => Bit >= 0 ? $"DB{DbNumber}.DBX{Number}.{Bit}" : $"DB{DbNumber}.DBX{Number}",

        // ── 오므론 ────────────────────────────────────────────
        (PlcVendor.Omron, RegisterType.Word)           => $"D{Number:D5}",
        (PlcVendor.Omron, RegisterType.LinkWord)       => $"W{Number:D5}",
        (PlcVendor.Omron, RegisterType.Timer)          => $"TIM{Number:D4}",
        (PlcVendor.Omron, RegisterType.Counter)        => $"CNT{Number:D4}",

        // ── 자유 입력 ─────────────────────────────────────────
        _ => Number.ToString()
    };

    // §4 ─ 주소 힌트 (편집기 표시용) ─────────────────────────

    public static string GetHint(PlcVendor vendor, RegisterType register) =>
        (vendor, register) switch
        {
            (PlcVendor.Modbus, RegisterType.HoldingReg)    => "예) 40001 ~ 49999",
            (PlcVendor.Modbus, RegisterType.InputReg)      => "예) 30001 ~ 39999",
            (PlcVendor.Modbus, RegisterType.Coil)          => "예) 1 ~ 9999",
            (PlcVendor.Modbus, RegisterType.DiscreteInput) => "예) 10001 ~ 19999",
            (PlcVendor.Mitsubishi, RegisterType.Word)      => "예) D100, D200  (데이터 레지스터)",
            (PlcVendor.Mitsubishi, RegisterType.DWord)     => "예) D100, D200  (32bit 더블워드)",
            (PlcVendor.Mitsubishi, RegisterType.BitInput)  => "예) X0.0, X1.7  (번지.비트 0~7)",
            (PlcVendor.Mitsubishi, RegisterType.BitOutput) => "예) Y0.0, Y100  (번지.비트 0~7)",
            (PlcVendor.Mitsubishi, RegisterType.BitMemory) => "예) M100, M200  (보조 릴레이)",
            (PlcVendor.Mitsubishi, RegisterType.LinkWord)  => "예) W100, W200  (링크 레지스터)",
            (PlcVendor.Mitsubishi, RegisterType.Timer)     => "예) TN0, TN10",
            (PlcVendor.Mitsubishi, RegisterType.Counter)   => "예) CN0, CN10",
            (PlcVendor.LsSanXgt, RegisterType.Word)        => "예) %MW100, %MW200",
            (PlcVendor.LsSanXgt, RegisterType.DWord)       => "예) %MD100, %MD200",
            (PlcVendor.LsSanXgt, RegisterType.BitInput)    => "예) %IX0.0, %IX0.7",
            (PlcVendor.LsSanXgt, RegisterType.BitOutput)   => "예) %QX0.0, %QX0.7",
            (PlcVendor.LsSanXgt, RegisterType.BitMemory)   => "예) %MX0.0, %MX100.3",
            (PlcVendor.Siemens, RegisterType.Word)         => "예) DB1.DBW0, DB2.DBW4",
            (PlcVendor.Siemens, RegisterType.DWord)        => "예) DB1.DBD0, DB1.DBD4",
            (PlcVendor.Siemens, RegisterType.BitMemory)    => "예) DB1.DBX0.0, DB1.DBX0.7",
            (PlcVendor.Omron, RegisterType.Word)           => "예) D00100, D00200",
            (PlcVendor.Omron, RegisterType.LinkWord)       => "예) W00100, W00200",
            (PlcVendor.Omron, RegisterType.Timer)          => "예) TIM0000, TIM0010",
            (PlcVendor.Omron, RegisterType.Counter)        => "예) CNT0000, CNT0010",
            _                                              => "주소를 직접 입력하세요"
        };

    // §5 ─ 다음 주소 계산 ─────────────────────────────────────

    /// <summary>
    /// step 만큼 이동한 다음 주소 반환.
    /// 비트 주소는 0~7 순환 후 번지 자동 증가.
    /// </summary>
    public TagAddress Next(int step = 1)
    {
        if (Register.IsBit() && Bit >= 0)
        {
            int total = Number * 8 + Bit + step;
            return this with { Number = total / 8, Bit = total % 8 };
        }
        return this with { Number = Number + step };
    }

    // §6 ─ 문자열 파싱 ────────────────────────────────────────

    /// <summary>주소 문자열 → TagAddress 파싱</summary>
    public static TagAddress Parse(
        string       address,
        PlcVendor    vendor,
        RegisterType register)
    {
        address = address.Trim();

        return (vendor, register) switch
        {
            (PlcVendor.Modbus, _) =>
                int.TryParse(address, out var n)
                    ? new() { Vendor=vendor, Register=register, Number=n, Bit=-1 }
                    : Default(vendor, register),

            (PlcVendor.Mitsubishi, RegisterType.Word or RegisterType.DWord) =>
                _ParsePrefixNumber(address, "D", vendor, register),

            (PlcVendor.Mitsubishi, RegisterType.BitInput) =>
                _ParseBit(address, "X", vendor, register),

            (PlcVendor.Mitsubishi, RegisterType.BitOutput) =>
                _ParseBit(address, "Y", vendor, register),

            (PlcVendor.Mitsubishi, RegisterType.BitMemory) =>
                _ParsePrefixNumber(address, "M", vendor, register),

            (PlcVendor.Mitsubishi, RegisterType.LinkWord) =>
                _ParsePrefixNumber(address, "W", vendor, register),

            (PlcVendor.LsSanXgt, _) =>
                _ParseLs(address, vendor, register),

            (PlcVendor.Siemens, _) =>
                _ParseSiemens(address, vendor, register),

            (PlcVendor.Omron, _) =>
                _ParseOmron(address, vendor, register),

            _ => // Free
                int.TryParse(address, out var fn)
                    ? new() { Vendor=vendor, Register=register, Number=fn, Bit=-1 }
                    : new() { Vendor=vendor, Register=register, Number=0, Bit=-1 }
        };
    }

    // ── 파싱 헬퍼 ─────────────────────────────────────────────

    private static TagAddress _ParsePrefixNumber(
        string s, string prefix, PlcVendor v, RegisterType r)
    {
        var upper = s.ToUpperInvariant().TrimStart(prefix[0]);
        return int.TryParse(upper, out var n)
            ? new() { Vendor=v, Register=r, Number=n, Bit=-1 }
            : Default(v, r);
    }

    private static TagAddress _ParseBit(
        string s, string prefix, PlcVendor v, RegisterType r)
    {
        var upper = s.ToUpperInvariant();
        if (!upper.StartsWith(prefix)) return Default(v, r);
        var rest = upper[prefix.Length..];
        var dot  = rest.IndexOf('.');
        if (dot >= 0
            && int.TryParse(rest[..dot], out var num)
            && int.TryParse(rest[(dot+1)..], out var bit))
            return new() { Vendor=v, Register=r, Number=num, Bit=bit };
        if (int.TryParse(rest, out var n))
            return new() { Vendor=v, Register=r, Number=n, Bit=0 };
        return Default(v, r);
    }

    private static TagAddress _ParseLs(string s, PlcVendor v, RegisterType r)
    {
        var m = Regex.Match(s, @"^(%[A-Za-z]+)(\d+)(?:\.(\d+))?$");
        if (!m.Success) return Default(v, r);
        int num = int.Parse(m.Groups[2].Value);
        int bit = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : -1;
        return new() { Vendor=v, Register=r, Number=num, Bit=bit };
    }

    private static TagAddress _ParseSiemens(string s, PlcVendor v, RegisterType r)
    {
        var m = Regex.Match(s, @"^DB(\d+)\.DB[A-Za-z]+(\d+)(?:\.(\d+))?$", RegexOptions.IgnoreCase);
        if (!m.Success) return Default(v, r);
        int db  = int.Parse(m.Groups[1].Value);
        int num = int.Parse(m.Groups[2].Value);
        int bit = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : -1;
        return new() { Vendor=v, Register=r, DbNumber=db, Number=num, Bit=bit };
    }

    private static TagAddress _ParseOmron(string s, PlcVendor v, RegisterType r)
    {
        var m = Regex.Match(s, @"^[A-Za-z]+(\d+)$");
        if (!m.Success) return Default(v, r);
        int num = int.Parse(m.Groups[1].Value);
        return new() { Vendor=v, Register=r, Number=num, Bit=-1 };
    }
}
