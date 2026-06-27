// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Core/Plugin/PluginLoadContext.cs
//  역할: 드라이버 플러그인 격리 로드 컨텍스트
//        isCollectible:true → 언로드(핫리로드) 가능
//  CS0103 수정: using System.IO / System.Reflection 명시 추가
//  생성: 2026-06-27 / 수정: 2026-06-27
// ══════════════════════════════════════════════════════════

using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace IIoT.Contracts.Core.Plugin;

/// <summary>
/// 드라이버 플러그인 격리 로드 컨텍스트.
/// <para>
/// 각 드라이버 dll을 독립된 컨텍스트에서 로드하여
/// 버전 충돌 방지 + 런타임 언로드(핫리로드)를 가능하게 한다.
/// </para>
/// <para>
/// isCollectible: true — GC가 수집 가능 → PluginLoader.Unload() 호출 시 메모리 해제.
/// </para>
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly AssemblyDependencyResolver _resolver;

    // §2 ─ 생성자 ──────────────────────────────────────────

    /// <param name="pluginPath">드라이버 dll 절대 경로</param>
    public PluginLoadContext(string pluginPath)
        : base(name: Path.GetFileNameWithoutExtension(pluginPath),
               isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    // §3 ─ 로드 오버라이드 ─────────────────────────────────

    /// <summary>
    /// 어셈블리 로드 시 플러그인 디렉터리 우선, 없으면 기본 컨텍스트로 폴백.
    /// IIoT.Contracts 는 기본 컨텍스트에서 공유 (버전 통일).
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // ★ IIoT.Contracts 는 항상 기본 컨텍스트에서 로드
        //   → 인터페이스 타입 동일성 보장 (is IProtocolPlugin 비교 가능)
        if (assemblyName.Name == "IIoT.Contracts")
            return null;  // null → Default 컨텍스트로 폴백

        // 플러그인 디렉터리의 의존성 dll 탐색
        var resolved = _resolver.ResolveAssemblyToPath(assemblyName);
        return resolved is not null
            ? LoadFromAssemblyPath(resolved)
            : null;  // null → Default 컨텍스트로 폴백
    }

    /// <summary>비관리 라이브러리 경로 해석.</summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var resolved = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return resolved is not null
            ? LoadUnmanagedDllFromPath(resolved)
            : IntPtr.Zero;
    }
}
