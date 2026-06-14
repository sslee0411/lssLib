// ══════════════════════════════════════════════════════════
//  IIoT.Shared · Config/ConfigBundle.cs
//  역할: Studio ViewModel DI 번들 — 8개 파라미터 → 1개
//  V3: 신규
// ══════════════════════════════════════════════════════════

namespace IIoT.Shared.Config;

/// <summary>
/// Studio ViewModel 생성자 번들.
/// object 타입으로 선언하여 IIoT.Shared 가
/// Studio 특정 타입에 의존하지 않도록 합니다.
/// </summary>
public sealed class ConfigBundle
{
    // §1 ─ 설정 서비스 ────────────────────────────────────────
    public required object Loader { get; init; }   // JsonConfigLoader
    public required object Writer { get; init; }   // JsonWriteService
    public required object Collect { get; init; }   // CollectConfigService

    // §2 ─ 라이브러리 ViewModel ───────────────────────────────
    public required object Scale { get; init; }    // ScaleLibraryViewModel
    public required object Alarm { get; init; }    // AlarmLibraryViewModel
    public required object Comm { get; init; }    // CommLibraryViewModel
    public required object Canvas { get; init; }    // CanvasViewModel

    // §3 ─ 타입 안전 접근 ─────────────────────────────────────
    public T Get<T>(object service) where T : class =>
        service as T ?? throw new InvalidCastException(
            $"ConfigBundle: {service.GetType().Name} → {typeof(T).Name} 변환 실패");
}