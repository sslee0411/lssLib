// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Converters/UiConverters.cs
//  역할: UI 디자인 개선(MN-02B)용 컨버터 모음
//        ① TabActiveBackgroundConverter / TabActiveForegroundConverter
//           — 탭바에서 선택된 탭에 "필(pill)" 배경 + 강조색 텍스트 표시
//        ② ConnectionStatusColorConverter — Collector 연결상태 텍스트 → 색상
//        ③ QualityColorConverter — Tag 품질(Good/Bad 등) → 색상
//        ④ AlarmLevelColorConverter / AlarmStatusColorConverter (MN-03)
//           — 알람 레벨(HH/H/L/LL)·상태(Active/Acked/Recovered) → 색상
//        전부 IIoT.UI.Themes 의 DynamicResource 키(AccBrush/AccFaintBrush/
//        GreenBrush/RedBrush/YellowBrush/OrangeBrush/BlueBrush/PurpleBrush/
//        Text2Brush 등)를 그대로 조회하므로 7개 기본 테마 어디서도 동일하게
//        동작한다(테마별 재정의 불필요).
//  MN-02B: 신규
//  MN-03: AlarmLevelColorConverter / AlarmStatusColorConverter 추가
//  생성: 2026-07-07 / 수정: 2026-07-07 (MN-03)
// ══════════════════════════════════════════════════════════

using System.Globalization;
using System.Windows;
using System.Windows.Data;
// ★ FIX(2026-07-08): UseWindowsForms=true 로 System.Drawing 도 전역 using에
//   걸려 Brush/Brushes 가 System.Drawing.Brush(es) 와 모호해짐 — 별칭으로
//   WPF(System.Windows.Media) 쪽을 명시적으로 고정.
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace IIoT.Monitor.Core.Converters;

/// <summary>공통 헬퍼: 테마 리소스 브러시를 안전하게 조회 (없으면 투명)</summary>
internal static class ThemeResource
{
    public static Brush Find(string key) =>
        Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;
}

/// <summary>
/// 탭 인덱스가 <c>ConverterParameter</c>와 같으면 강조 배경(AccFaintBrush), 아니면 투명.
/// 사용: Background="{Binding ActiveTabIndex, Converter={StaticResource TabBg}, ConverterParameter=0}"
/// </summary>
public sealed class TabActiveBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var active = value is int i && parameter is string s && int.TryParse(s, out var idx) && i == idx;
        return active ? ThemeResource.Find("AccFaintBrush") : Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 탭 인덱스가 <c>ConverterParameter</c>와 같으면 강조색(AccBrush) 텍스트, 아니면 보조색(Text2Brush).
/// </summary>
public sealed class TabActiveForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var active = value is int i && parameter is string s && int.TryParse(s, out var idx) && i == idx;
        return ThemeResource.Find(active ? "AccBrush" : "Text2Brush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
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

/// <summary>Tag 품질(LiveTagRow.Quality) → 색상. Good=Green, 그 외(Bad/Timeout/Disconnected)=Red.</summary>
public sealed class QualityColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value as string) == "Good" ? ThemeResource.Find("GreenBrush") : ThemeResource.Find("RedBrush");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// ★ MN-03: 알람 레벨(AlarmRow.Level) → 색상.
/// HH=Red(위험), H=Orange(경고), L=Blue(주의), LL=Purple(참고). 테마 헤더 주석의
/// "BlueColor - L 알람 전용", "PurpleColor - LL 알람 전용" 관례를 그대로 따른다.
/// </summary>
public sealed class AlarmLevelColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value as string) switch
        {
            "HH" => ThemeResource.Find("RedBrush"),
            "H"  => ThemeResource.Find("OrangeBrush"),
            "L"  => ThemeResource.Find("BlueBrush"),
            "LL" => ThemeResource.Find("PurpleBrush"),
            _    => ThemeResource.Find("Text2Brush")
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// ★ MN-03: 알람 상태(AlarmRow.Status) → 색상.
/// Active=Red(미확인), Acked=Yellow(확인됨·진행중), Recovered=Green(복귀됨).
/// </summary>
public sealed class AlarmStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value as string) switch
        {
            "Active"    => ThemeResource.Find("RedBrush"),
            "Acked"     => ThemeResource.Find("YellowBrush"),
            "Recovered" => ThemeResource.Find("GreenBrush"),
            _           => ThemeResource.Find("Text2Brush")
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>★ MN-EX-05: LiveTagRow.IsFavorite(bool) → 아이콘 문자열. true="⭐", false="☆"</summary>
public sealed class FavoriteIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is true) ? "⭐" : "☆";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
