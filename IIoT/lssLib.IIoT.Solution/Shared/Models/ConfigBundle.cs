// ══════════════════════════════════════════════════════════
//  IIoT.Shared · Config/ConfigBundle.cs
//  역할: Studio(ConfigApp) ViewModel DI 번들
//        파라미터 8개 → ConfigBundle 1개로 단순화
//  V3 Step2: 신규
//
//  사용 예 (App.xaml.cs):
//    services.AddSingleton<ConfigBundle>(sp => new ConfigBundle {
//        Loader  = sp.GetRequiredService<JsonConfigLoader>(),
//        Writer  = sp.GetRequiredService<JsonWriteService>(),
//        Collect = sp.GetRequiredService<CollectConfigService>(),
//        Scale   = sp.GetRequiredService<ScaleLibraryViewModel>(),
//        Alarm   = sp.GetRequiredService<AlarmLibraryViewModel>(),
//        Comm    = sp.GetRequiredService<CommLibraryViewModel>(),
//        Canvas  = sp.GetRequiredService<CanvasViewModel>(),
//    });
//    services.AddSingleton<StudioMainViewModel>(sp => new StudioMainViewModel(
//        sp.GetRequiredService<DeviceTreeViewModel>(),
//        sp.GetRequiredService<ConfigBundle>()         // ← 2개 파라미터
//    ));
// ══════════════════════════════════════════════════════════

namespace IIoT.Shared.Config;

/// <summary>
/// Studio(ConfigApp) ViewModel 생성자 파라미터 번들.
/// 8개 서비스를 1개 번들 객체로 묶어 DI 복잡도를 낮춥니다.
///
/// ※ 각 프로퍼티 타입은 object로 선언하여 IIoT.Shared가
///   ConfigApp 특정 타입에 의존하지 않도록 합니다.
///   실제 사용 시 각 프로그램에서 구체 타입으로 캐스팅합니다.
/// </summary>
public sealed class ConfigBundle
{
    // §1 ─ 설정 서비스 ────────────────────────────────────────

    /// <summary>JSON 설정 로더 (JsonConfigLoader)</summary>
    public required object Loader { get; init; }

    /// <summary>JSON 설정 저장 서비스 (JsonWriteService)</summary>
    public required object Writer { get; init; }

    /// <summary>수집 흐름 설정 서비스 (CollectConfigService)</summary>
    public required object Collect { get; init; }

    // §2 ─ 라이브러리 ViewModel ───────────────────────────────

    /// <summary>스케일 라이브러리 ViewModel (ScaleLibraryViewModel)</summary>
    public required object Scale { get; init; }

    /// <summary>알람 라이브러리 ViewModel (AlarmLibraryViewModel)</summary>
    public required object Alarm { get; init; }

    /// <summary>통신 라이브러리 ViewModel (CommLibraryViewModel)</summary>
    public required object Comm { get; init; }

    /// <summary>캔버스 ViewModel (CanvasViewModel)</summary>
    public required object Canvas { get; init; }

    // §3 ─ 타입 안전 접근 헬퍼 ───────────────────────────────

    /// <summary>타입 안전 서비스 꺼내기</summary>
    public T Get<T>(object service) where T : class =>
        service as T ?? throw new InvalidCastException(
            $"ConfigBundle: {service.GetType().Name}을 {typeof(T).Name}으로 변환 실패");
}
