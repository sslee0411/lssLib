// ══════════════════════════════════════════════════════════
//  IIoT.Shared · Config/ConfigBundle.cs
//  역할: Studio ViewModel DI 번들 — 파라미터 8개 → 2개로 축소
//  Phase C: 완성본 (object → Generic 타입 안전 접근)
// ══════════════════════════════════════════════════════════

namespace IIoT.Shared.Config;

/// <summary>
/// IIoT.Studio ViewModel 생성자 번들.
/// IIoT.Shared 가 Studio 특정 타입에 의존하지 않도록
/// object 타입으로 보관하고, Get&lt;T&gt;()로 타입 안전 접근.
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
    /// <summary>보관된 서비스를 T 타입으로 꺼냅니다.</summary>
    public T Get<T>(object service) where T : class =>
        service as T ?? throw new InvalidCastException(
            $"ConfigBundle: {service.GetType().Name} → {typeof(T).Name} 변환 실패");
}