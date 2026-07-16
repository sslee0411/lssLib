// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Converters/UiConverters.cs
//  역할: UI 컨버터 모음
//        ① ConnectionStatusColorConverter — Collector 연결상태 텍스트 → 색상
//        ② TagQualityColorConverter — Tag Quality 문자열 → 색상 (HM-06)
//        (IIoT.Monitor Core/Converters/UiConverters.cs 중 ConnectionStatusColorConverter
//         부분만 이식 — 탭 강조는 Manager MG-04 DataTrigger 스타일 패턴을 사용하므로
//         TabActiveBackground/Foreground 컨버터는 불필요)
//        IIoT.UI.Themes 의 DynamicResource 키(GreenBrush/YellowBrush/RedBrush/
//        Text2Brush)를 그대로 조회하므로 7개 기본 테마 어디서도 동일하게 동작한다.
//  HM-02: 신규
//  HM-06: TagQualityColorConverter 추가 — 카드 우상단 상태 점(StatusDot)의 색상을
//         Tag Quality("Good"/"Bad"/"Timeout"/"Disconnected")에 따라 결정한다.
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace IIoT.HMI.Core.Converters;

/// <summary>공통 헬퍼: 테마 리소스 브러시를 안전하게 조회 (없으면 투명)</summary>
internal static class ThemeResource
{
    public static Brush Find(string key) =>
        Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;
}

/// <summary>
/// Collector 연결상태 텍스트(CollectorEndpoint.StatusText) → 색상.
/// "연결됨" 포함=Green, "재연결"/"중"=Yellow, "오류"/"실패"=Red, 그 외(미연결 등)=Text2(회색).
/// </summary>
public sealed class ConnectionStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? "";

        if (text.Contains("연결됨"))
            return ThemeResource.Find("GreenBrush");
        if (text.Contains("재연결") || text.Contains("중..."))
            return ThemeResource.Find("YellowBrush");
        if (text.Contains("오류") || text.Contains("실패"))
            return ThemeResource.Find("RedBrush");

        return ThemeResource.Find("Text2Brush"); // 미연결/연결 종료 등
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// ★ HM-06: Tag Quality 문자열(AbstractLayoutNode.ValueQuality) → 카드 상태 점 색상.
/// "Good"=Green, "Bad"/"Timeout"=Yellow, "Disconnected"=Red, 그 외(미바인딩 등 빈 문자열)=Text2.
/// </summary>
public sealed class TagQualityColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var quality = value as string ?? "";

        return quality switch
        {
            "Good"                  => ThemeResource.Find("GreenBrush"),
            "Bad" or "Timeout"      => ThemeResource.Find("YellowBrush"),
            "Disconnected"          => ThemeResource.Find("RedBrush"),
            _                       => ThemeResource.Find("Text2Brush") // 미바인딩·값 없음 등
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
