// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.WpfDemo · Models/SensorRow.cs
//  역할: 전체 Provider 데모 공통 엔티티
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.DB.WpfDemo.Models;

/// <summary>센서 데이터 데모 엔티티.</summary>
public sealed class SensorRow
{
    public int     SensorId   { get; set; }
    public string  PlantCd    { get; set; } = string.Empty;
    public string  SensorName { get; set; } = string.Empty;
    public double  Value      { get; set; }
    public string  UseYn      { get; set; } = "Y";
    public string  RegDt      { get; set; } = string.Empty;

    public override string ToString() =>
        $"[{SensorId}] {SensorName} ({PlantCd}) = {Value:F2}  [{RegDt}]";
}
