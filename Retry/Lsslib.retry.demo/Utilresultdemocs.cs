using lssLib.Retry;

namespace lssLib.Retry.Demo;

// ═══════════════════════════════════════════════════════════════════
//  UtilResultDemo — UtilResult / UtilResult<T> / UtilResults 예제
//  §1 팩토리·기본  §2 Unwrap  §3 Map  §4 TryExecuteAsync  §5 운용패턴
// ═══════════════════════════════════════════════════════════════════

internal static class UtilResultDemo
{
    public static void Run()
    {
        DemoHelper.Header("UtilResult — 안전 실행 반환 타입");
        BasicDemo();
        UnwrapDemo();
        MapDemo();
        TryExecuteDemo();
        PatternDemo();
    }

    // ─────────────────────────────────────────────
    // §1  팩토리 · 기본 프로퍼티
    // ─────────────────────────────────────────────
    static void BasicDemo()
    {
        DemoHelper.Section("§1  팩토리 · 기본 프로퍼티");

        var ok = UtilResults.Ok();
        DemoHelper.Show("Ok().IsOk", ok.IsOk);
        DemoHelper.Show("Ok().IsError", ok.IsError);
        DemoHelper.Show("Ok().Error", ok.Error?.Message ?? "<null>");

        var okVal = UtilResults.Ok(42);
        DemoHelper.Show("Ok(42).Value", okVal.Value);
        DemoHelper.Show("Ok(42).IsOk", okVal.IsOk);

        var ex = new InvalidOperationException("센서 응답 없음");
        var fail = UtilResults.Fail(ex);
        DemoHelper.Show("Fail().IsError", fail.IsError);
        DemoHelper.Show("Fail().Error.Message", fail.Error!.Message);

        var failT = UtilResults.Fail<byte[]>(ex);
        DemoHelper.Show("Fail<byte[]>.Value", failT.Value?.Length.ToString() ?? "<null>");
        DemoHelper.Show("Fail<byte[]>.IsError", failT.IsError);
    }

    // ─────────────────────────────────────────────
    // §2  Unwrap · UnwrapOr · UnwrapOrElse
    // ─────────────────────────────────────────────
    static void UnwrapDemo()
    {
        DemoHelper.Section("§2  Unwrap · UnwrapOr · UnwrapOrElse · ThrowIfError");

        var ok = UtilResults.Ok(new byte[] { 0x02, 0xAA, 0x03 });
        var fail = UtilResults.Fail<byte[]>(new Exception("읽기 오류"));

        DemoHelper.Ok($"ok.Unwrap() 길이 = {ok.Unwrap().Length}");
        DemoHelper.TryCatch("fail.Unwrap()", () => fail.Unwrap());

        byte[] fallback = fail.UnwrapOr(Array.Empty<byte>());
        DemoHelper.Ok($"fail.UnwrapOr([]) 길이 = {fallback.Length}  (예외 없음)");

        byte[] dynamic = fail.UnwrapOrElse(e =>
        {
            DemoHelper.Warn($"  폴백 람다: {e!.Message}");
            return new byte[] { 0x00 };
        });
        DemoHelper.Ok($"fail.UnwrapOrElse() 길이 = {dynamic.Length}");

        ok.ThrowIfError();
        DemoHelper.Ok("ok.ThrowIfError() → 무동작 (성공)");
        DemoHelper.TryCatch("fail.ThrowIfError()", () => fail.ThrowIfError());
    }

    // ─────────────────────────────────────────────
    // §3  Map — 타입 변환 · 실패 전파
    // ─────────────────────────────────────────────
    static void MapDemo()
    {
        DemoHelper.Section("§3  Map — 타입 변환 · 실패 전파");

        var rawOk = UtilResults.Ok(new byte[] { 0x01, 0x00, 0x00, 0x00 });
        UtilResult<uint> parsed = rawOk.Map(b => BitConverter.ToUInt32(b, 0));
        DemoHelper.Ok($"Ok(byte[]).Map(ToUInt32) → {parsed.Value}");

        var rawFail = UtilResults.Fail<byte[]>(new Exception("수신 실패"));
        UtilResult<uint> parsedFail = rawFail.Map(b => BitConverter.ToUInt32(b, 0));
        DemoHelper.Show("Fail.Map() IsError", parsedFail.IsError);
        DemoHelper.Show("Fail.Map() Error", parsedFail.Error!.Message);

        DemoHelper.Info("── Map 체이닝 ──");
        var r1 = UtilResults.Ok("42.5");
        var r2 = r1.Map(s => double.Parse(s));
        var r3 = r2.Map(d => (float)d);
        var r4 = r3.Map(f => $"Temp={f:F1}°C");
        DemoHelper.Show("string→double→float→string", r4.UnwrapOr("오류"));
    }

    // ─────────────────────────────────────────────
    // §4  TryExecuteAsync
    // ─────────────────────────────────────────────
    static void TryExecuteDemo()
    {
        DemoHelper.Section("§4  TryExecuteAsync — 예외 없는 안전 실행");

        // Action 성공
        DemoHelper.RunAsync(async () =>
        {
            Func<Task> action = async () => await Task.Delay(10);
            UtilResult r = await action.TryExecuteAsync();
            DemoHelper.Show("Action 성공 IsOk", r.IsOk);
        });

        // Func<T> 성공
        DemoHelper.RunAsync(async () =>
        {
            Func<Task<int>> func = () => Task.FromResult(100);
            UtilResult<int> r = await func.TryExecuteAsync();
            DemoHelper.Show("Func<int> 성공 Value", r.Value);
        });

        // 실패 케이스
        DemoHelper.RunAsync(async () =>
        {
            Func<Task<byte[]>> func = async () =>
            {
                await Task.Delay(5);
                throw new IOException("파일을 찾을 수 없습니다.");
            };
            UtilResult<byte[]> r = await func.TryExecuteAsync();
            DemoHelper.Show("실패 IsError", r.IsError);
            DemoHelper.Show("실패 Error.Type", r.Error!.GetType().Name);
            DemoHelper.Show("실패 Error.Message", r.Error.Message);
        });

        // switch 패턴
        DemoHelper.Info("── switch 패턴 분기 ──");
        DemoHelper.RunAsync(async () =>
        {
            Func<Task<byte[]>> func = () =>
                Task.FromException<byte[]>(new TimeoutException("응답 없음"));
            var r = await func.TryExecuteAsync();
            switch (r)
            {
                case { IsOk: true }: DemoHelper.Ok($"성공: {r.Unwrap().Length}B"); break;
                case { Error: TimeoutException tex }: DemoHelper.Warn($"타임아웃: {tex.Message}"); break;
                default: DemoHelper.Err($"오류: {r.Error!.Message}"); break;
            }
        });
    }

    // ─────────────────────────────────────────────
    // §5  실전 운용 패턴
    // ─────────────────────────────────────────────
    static void PatternDemo()
    {
        DemoHelper.Section("§5  실전 운용 패턴");

        DemoHelper.Info("── 앱 종료 정리 (실패 무시) ──");
        DemoHelper.RunAsync(async () =>
        {
            var tasks = new (string name, Func<Task> action)[]
            {
                ("MQ 연결 해제",  async () => await Task.Delay(5)),
                ("DB 연결 해제",  async () => { await Task.Delay(5); throw new Exception("already closed"); }),
                ("캐시 플러시",   async () => await Task.Delay(5)),
                ("텔레메트리",    async () => { await Task.Delay(5); throw new IOException("server gone"); }),
            };
            foreach (var (name, action) in tasks)
            {
                var r = await action.TryExecuteAsync();
                if (r.IsOk) DemoHelper.Ok($"  {name}");
                else DemoHelper.Warn($"  {name} → 무시: {r.Error!.Message}");
            }
        });

        DemoHelper.Info("── Map 체이닝 파이프라인 ──");
        DemoHelper.RunAsync(async () =>
        {
            Func<Task<byte[]>> recv = async () =>
            {
                await Task.Delay(5);
                return new byte[] { 0x01, 0x00, 0x00, 0x00, 0x9A, 0x99, 0x29, 0x42 };
            };
            var raw = await recv.TryExecuteAsync();
            var result = raw
                .Map(b => (id: BitConverter.ToUInt32(b, 0), temp: BitConverter.ToSingle(b, 4)))
                .Map(d => $"ID={d.id:D4}  Temp={d.temp:F2}°C");
            DemoHelper.Show("파이프라인 결과", result.UnwrapOr("파싱 실패"));
        });
    }
}