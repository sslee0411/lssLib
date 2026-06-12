// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/DataModel/Location.cs
//  역할: 위치 정보 라이브러리 레코드
//        Group / Device 의 LocationId 로 참조
//        location-library.json 에 저장
//  Phase 1 Update: 신규 추가
// ══════════════════════════════════════════════════════════

namespace IIoT.DeviceManager.Core.DataModel;

/// <summary>
/// 위치 정보 레코드.
/// Group(소분류/대분류) 또는 Device 가 LocationId 로 참조합니다.
/// 도면 좌표(CoordX/Y) 와 GPS(Latitude/Longitude) 를 모두 지원합니다.
/// </summary>
/// <example><code>
/// var loc = new Location
/// {
///     Id = "loc-001", Name = "1동 1층 전기패널",
///     Building = "1동", Floor = "1층", Room = "전기실",
///     CoordX = 12.5, CoordY = 34.0,
///     Latitude = 35.1796, Longitude = 129.0756,
///     Description = "메인 분전반 옆"
/// };
/// </code></example>
public record Location
{
    // §1 ─ 식별 ───────────────────────────────────────────────
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    // §2 ─ 건물/층/구역 (텍스트) ─────────────────────────────
    /// <summary>건물명 (예: "1동", "A동", "본관")</summary>
    public string Building { get; init; } = string.Empty;

    /// <summary>층 (예: "1층", "B1", "옥상")</summary>
    public string Floor { get; init; } = string.Empty;

    /// <summary>실/구역명 (예: "전기실", "중앙제어실", "컴프레서룸")</summary>
    public string Room { get; init; } = string.Empty;

    // §3 ─ 도면 좌표 (2D 평면도용) ────────────────────────────
    /// <summary>도면 X 좌표 (미터 또는 픽셀 단위, 미설정 시 null)</summary>
    public double? CoordX { get; init; }

    /// <summary>도면 Y 좌표 (미터 또는 픽셀 단위, 미설정 시 null)</summary>
    public double? CoordY { get; init; }

    // §4 ─ GPS 좌표 ───────────────────────────────────────────
    /// <summary>위도 (WGS84, 예: 35.1796)</summary>
    public double? Latitude { get; init; }

    /// <summary>경도 (WGS84, 예: 129.0756)</summary>
    public double? Longitude { get; init; }

    /// <summary>고도 (미터, 선택)</summary>
    public double? Altitude { get; init; }

    // §5 ─ 헬퍼 ──────────────────────────────────────────────
    /// <summary>도면 좌표가 설정되어 있는지 확인합니다.</summary>
    public bool HasCoord => CoordX.HasValue && CoordY.HasValue;

    /// <summary>GPS 좌표가 설정되어 있는지 확인합니다.</summary>
    public bool HasGps => Latitude.HasValue && Longitude.HasValue;

    /// <summary>사람이 읽기 쉬운 전체 위치 문자열을 반환합니다.</summary>
    public string FullAddress
    {
        get
        {
            var parts = new[] { Building, Floor, Room }
                        .Where(s => !string.IsNullOrWhiteSpace(s));
            return string.Join(" / ", parts);
        }
    }

    // §6 ─ ConfigManager 직렬화 헬퍼 ──────────────────────────
    public string SectionKey => $"LocationLibrary:{Id}";

    public Dictionary<string, string> ToConfigEntries() => new()
    {
        ["name"] = Name,
        ["description"] = Description,
        ["building"] = Building,
        ["floor"] = Floor,
        ["room"] = Room,
        ["coordX"] = CoordX?.ToString("G") ?? string.Empty,
        ["coordY"] = CoordY?.ToString("G") ?? string.Empty,
        ["latitude"] = Latitude?.ToString("G") ?? string.Empty,
        ["longitude"] = Longitude?.ToString("G") ?? string.Empty,
        ["altitude"] = Altitude?.ToString("G") ?? string.Empty,
    };

    public static Location FromConfigEntries(string id,
                                             IReadOnlyDictionary<string, string> e)
    {
        static double? ParseOpt(string? s)
            => !string.IsNullOrEmpty(s) && double.TryParse(s, out var v) ? v : null;

        return new Location
        {
            Id = id,
            Name = e.GetValueOrDefault("name", string.Empty),
            Description = e.GetValueOrDefault("description", string.Empty),
            Building = e.GetValueOrDefault("building", string.Empty),
            Floor = e.GetValueOrDefault("floor", string.Empty),
            Room = e.GetValueOrDefault("room", string.Empty),
            CoordX = ParseOpt(e.GetValueOrDefault("coordX")),
            CoordY = ParseOpt(e.GetValueOrDefault("coordY")),
            Latitude = ParseOpt(e.GetValueOrDefault("latitude")),
            Longitude = ParseOpt(e.GetValueOrDefault("longitude")),
            Altitude = ParseOpt(e.GetValueOrDefault("altitude")),
        };
    }
}