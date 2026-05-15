// ====================================================================
//  lssLib.Binary — BufferDiff
//  버퍼 비교 / 차이 분석 / 마스킹 비교 / 스키마 기반 필드 비교
//
//  [전체 비교]
//  var diff = BufferDiff.Compare(oldBuf, newBuf);
//  diff.HasChanges          → 변경 여부
//  diff.Similarity          → 유사도 0.0~1.0
//  diff.Patches             → (offset, oldVal, newVal) 목록
//  diff.ToPatchString()     → 변경 내용 텍스트 요약
//
//  [마스킹 비교 — CRC / 타임스탬프 무시]
//  bool eq = old.MaskedEquals(new_, ignoreOffsets:new[]{14,15});
//  bool eq = old.MaskedEquals(new_, schema, ignoreFields:new[]{"CRC","Timestamp"});
//
//  [스키마 기반 필드 단위 비교]
//  var fdiff = BufferDiff.CompareFields(old, new_, schema);
//  fdiff.HasFieldChanged("Config")   → true/false
//  Console.WriteLine(fdiff.Summary);
//
//  [패치 적용]
//  byte[] restored = BufferDiff.ApplyPatches(oldBuf, diff);
//  bool ok = BufferDiff.IsEqual(restored, newBuf);
// ====================================================================

namespace lssLib.Binary
{
    /// <summary>
    /// 두 버퍼 비교 결과 레코드.
    /// <see cref="BufferDiff.Compare"/> 로 생성됩니다.
    /// <example><code>
    /// var diff = BufferDiff.Compare(oldBuf, newBuf);
    ///
    /// Console.WriteLine(diff.ToPatchString());
    /// // [Diff]  A=18B  B=18B  변경=2바이트  유사도=88.9%
    /// //   offset=0x0004  0x41 → 0x42  (65 → 66)
    /// //   offset=0x0008  0x00 → 0x0A  ( 0 → 10)
    ///
    /// bool changed = diff.HasChanges;
    /// double sim   = diff.Similarity;
    /// var patches  = diff.Patches;  // (offset, oldVal, newVal)
    /// </code></example>
    /// </summary>
    public record DiffResult
    {
        /// <summary>비교 버퍼 A 의 길이.</summary>
        public int LengthA { get; init; }

        /// <summary>비교 버퍼 B 의 길이.</summary>
        public int LengthB { get; init; }

        /// <summary>변경된 바이트 수.</summary>
        public int ChangedCount { get; init; }

        /// <summary>변경이 하나라도 있으면 true.</summary>
        public bool HasChanges => ChangedCount > 0;

        /// <summary>두 버퍼가 완전히 동일하면 true.</summary>
        public bool IsIdentical => !HasChanges && LengthA == LengthB;

        /// <summary>유사도 (0.0 = 완전 다름, 1.0 = 완전 동일).</summary>
        public double Similarity { get; init; }

        /// <summary>변경된 offset 목록.</summary>
        public IReadOnlyList<int> ChangedOffsets { get; init; } = [];

        /// <summary>패치 목록 — (offset, oldValue, newValue).</summary>
        public IReadOnlyList<(int Offset, byte OldVal, byte NewVal)> Patches { get; init; } = [];

        /// <summary>
        /// 변경 내용을 텍스트로 반환합니다.
        /// <example><code>
        /// Console.WriteLine(diff.ToPatchString());
        /// // [Diff]  A=18B  B=18B  변경=2바이트  유사도=88.9%
        /// //   offset=0x0004  0x41 → 0x42  (65 → 66)
        /// </code></example>
        /// </summary>
        public string ToPatchString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Diff]  A={LengthA}B  B={LengthB}B  변경={ChangedCount}바이트  유사도={Similarity:P1}");
            if (LengthA != LengthB)
                sb.AppendLine($"  ⚠ 길이 다름: A={LengthA}B vs B={LengthB}B");
            foreach (var (off, oldV, newV) in Patches)
                sb.AppendLine($"  offset=0x{off:X4}  0x{oldV:X2} → 0x{newV:X2}  ({oldV,3} → {newV,3})");
            if (!HasChanges) sb.AppendLine("  ✅ 완전 동일");
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// 버퍼 비교 / 차이 분석 정적 클래스.
    /// <example><code>
    /// // 전체 비교
    /// var diff = BufferDiff.Compare(fw1, fw2);
    /// Console.WriteLine(diff.ToPatchString());
    ///
    /// // 마스킹 비교 (CRC 2바이트 제외)
    /// bool eq = old.MaskedEquals(new_,
    ///     ignoreOffsets: new[]{ frame.Length-2, frame.Length-1 });
    ///
    /// // 스키마 필드 단위 비교
    /// var fdiff = BufferDiff.CompareFields(old, new_, schema,
    ///     ignoreFields: new[]{"Timestamp","CRC"});
    /// bool configChanged = fdiff.HasFieldChanged("Config");
    /// </code></example>
    /// </summary>
    public static class BufferDiff
    {
        // ── 전체 비교 ─────────────────────────────────────────────────

        /// <summary>
        /// 두 버퍼를 바이트 단위로 비교합니다.
        /// <example><code>
        /// var diff = BufferDiff.Compare(oldBuf, newBuf);
        ///
        /// if (diff.HasChanges)
        /// {
        ///     Console.WriteLine(diff.ToPatchString());
        ///     // 펌웨어 업데이트 — 변경 페이지만 전송
        ///     SendPatch(diff.Patches);
        /// }
        ///
        /// // 유사도 확인
        /// Console.WriteLine($"유사도: {diff.Similarity:P1}");  // "유사도: 87.5%"
        /// </code></example>
        /// </summary>
        public static DiffResult Compare(byte[] a, byte[] b)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);

            int minLen = Math.Min(a.Length, b.Length);
            int maxLen = Math.Max(a.Length, b.Length);
            var patches = new List<(int, byte, byte)>();

            for (int i = 0; i < minLen; i++)
                if (a[i] != b[i])
                    patches.Add((i, a[i], b[i]));

            if (a.Length > b.Length)
                for (int i = minLen; i < a.Length; i++)
                    patches.Add((i, a[i], 0x00));
            else if (b.Length > a.Length)
                for (int i = minLen; i < b.Length; i++)
                    patches.Add((i, 0x00, b[i]));

            double sim = maxLen == 0 ? 1.0 : 1.0 - (double)patches.Count / maxLen;

            return new DiffResult
            {
                LengthA = a.Length,
                LengthB = b.Length,
                ChangedCount = patches.Count,
                Similarity = sim,
                ChangedOffsets = patches.Select(p => p.Item1).ToList(),
                Patches = patches,
            };
        }

        /// <summary>
        /// 두 버퍼가 완전히 동일한지 확인합니다.
        /// <example><code>
        /// // 중복 프레임 필터링
        /// bool isDuplicate = BufferDiff.IsEqual(lastFrame, currentFrame);
        /// if (!isDuplicate) ProcessFrame(currentFrame);
        /// </code></example>
        /// </summary>
        public static bool IsEqual(byte[] a, byte[] b)
            => a.Length == b.Length && a.AsSpan().SequenceEqual(b.AsSpan());

        /// <summary>
        /// 두 버퍼의 유사도를 0.0~1.0 으로 반환합니다.
        /// <example><code>
        /// double sim = BufferDiff.Similarity(frameA, frameB);
        /// Console.WriteLine($"유사도: {sim:P1}");  // "유사도: 87.5%"
        /// </code></example>
        /// </summary>
        public static double Similarity(byte[] a, byte[] b)
            => Compare(a, b).Similarity;

        // ── 마스킹 비교 ───────────────────────────────────────────────

        /// <summary>
        /// 특정 offset 들을 무시하고 나머지만 비교합니다.
        /// 타임스탬프·시퀀스·CRC 등 매번 바뀌는 필드를 제외할 때 사용합니다.
        /// <example><code>
        /// // 마지막 2바이트 (CRC) 제외
        /// bool eq = old.MaskedEquals(new_,
        ///     ignoreOffsets: new[]{ frame.Length-2, frame.Length-1 });
        ///
        /// // 타임스탬프 offset 8~11 제외
        /// bool eq = a.MaskedEquals(b, ignoreOffsets: new[]{ 8, 9, 10, 11 });
        /// </code></example>
        /// </summary>
        public static bool MaskedEquals(this byte[] a, byte[] b, int[] ignoreOffsets)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            if (a.Length != b.Length) return false;
            var ignoreSet = new HashSet<int>(ignoreOffsets);
            for (int i = 0; i < a.Length; i++)
                if (!ignoreSet.Contains(i) && a[i] != b[i])
                    return false;
            return true;
        }

        /// <summary>
        /// 스키마 필드 이름 기반으로 특정 필드를 무시하고 비교합니다.
        /// <example><code>
        /// var schema = new BufSchema()
        ///     .Then("STX",       BufType.UInt8)
        ///     .Then("Config",    BufType.UInt16BE)
        ///     .Then("Timestamp", BufType.UInt32LE)   // 무시
        ///     .Then("CRC",       BufType.UInt16BE);  // 무시
        ///
        /// bool eq = a.MaskedEquals(b, schema,
        ///     ignoreFields: new[]{"Timestamp", "CRC"});
        /// // Config 값만 비교
        /// </code></example>
        /// </summary>
        public static bool MaskedEquals(this byte[] a, byte[] b,
            BufSchema schema, string[] ignoreFields)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            var ignoreSet = new HashSet<int>();
            foreach (var name in ignoreFields)
            {
                var field = schema.GetField(name);
                if (field is null) continue;
                int size = BufSchema.FieldBytes(field);
                for (int j = 0; j < size; j++)
                    ignoreSet.Add(field.Offset + j);
            }
            return a.MaskedEquals(b, ignoreSet.ToArray());
        }

        // ── 스키마 기반 필드 비교 ─────────────────────────────────────

        /// <summary>
        /// 스키마 기준으로 두 버퍼를 필드 단위로 비교합니다.
        /// 어떤 필드가 변경됐는지 이름으로 확인할 수 있습니다.
        /// <example><code>
        /// var schema = new BufSchema()
        ///     .Then("STX",       BufType.UInt8)
        ///     .Then("Config",    BufType.UInt16BE)
        ///     .Then("Value",     BufType.FloatBE)
        ///     .Then("Timestamp", BufType.UInt32LE)
        ///     .Then("CRC",       BufType.UInt16BE);
        ///
        /// var fdiff = BufferDiff.CompareFields(oldBuf, newBuf, schema,
        ///     ignoreFields: new[]{"Timestamp","CRC"});
        ///
        /// Console.WriteLine(fdiff.Summary);
        /// // [FieldDiff]  변경=1  동일=2
        /// //   [변경] Config  offset=0x0001  A=[01 00]  B=[02 00]
        ///
        /// bool configChanged = fdiff.HasFieldChanged("Config");
        /// bool valueChanged  = fdiff.HasFieldChanged("Value");
        ///
        /// foreach (var name in fdiff.ChangedFieldNames)
        ///     ApplyChange(name);
        /// </code></example>
        /// </summary>
        public static FieldDiffResult CompareFields(byte[] a, byte[] b,
            BufSchema schema, string[]? ignoreFields = null)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);

            var ignoreSet = new HashSet<string>(
                ignoreFields ?? [], StringComparer.OrdinalIgnoreCase);
            var changed = new List<FieldDiffEntry>();
            var same = new List<FieldDiffEntry>();

            foreach (var field in schema.Fields)
            {
                if (ignoreSet.Contains(field.Name)) continue;

                int size = BufSchema.FieldBytes(field);
                int off = field.Offset;

                if (off + size > a.Length || off + size > b.Length)
                {
                    changed.Add(new FieldDiffEntry(field.Name, off, size,
                        [], [], "[범위 초과]"));
                    continue;
                }

                byte[] va = a.AsSpan(off, size).ToArray();
                byte[] vb = b.AsSpan(off, size).ToArray();
                bool eq = va.AsSpan().SequenceEqual(vb.AsSpan());

                var entry = new FieldDiffEntry(field.Name, off, size, va, vb,
                    eq ? "동일" : "변경");
                if (eq) same.Add(entry);
                else changed.Add(entry);
            }

            return new FieldDiffResult(changed, same);
        }

        // ── 패치 적용 ─────────────────────────────────────────────────

        /// <summary>
        /// DiffResult 의 패치를 원본 버퍼에 적용하여 B 버퍼를 재현합니다.
        /// <example><code>
        /// var diff    = BufferDiff.Compare(fw1, fw2);
        /// byte[] restored = BufferDiff.ApplyPatches(fw1, diff);
        /// bool ok = BufferDiff.IsEqual(restored, fw2);  // true
        /// </code></example>
        /// </summary>
        public static byte[] ApplyPatches(byte[] source, DiffResult diff)
        {
            var result = new byte[diff.LengthB];
            int copyLen = Math.Min(source.Length, diff.LengthB);
            Array.Copy(source, result, copyLen);
            foreach (var (off, _, newV) in diff.Patches)
                if (off < diff.LengthB) result[off] = newV;
            return result;
        }

        /// <summary>
        /// 두 버퍼의 공통 바이트 (동일한 위치에 동일한 값) 를 반환합니다.
        /// <example><code>
        /// byte[] common = BufferDiff.CommonBytes(fw1, fw2);
        /// </code></example>
        /// </summary>
        public static byte[] CommonBytes(byte[] a, byte[] b)
        {
            int len = Math.Min(a.Length, b.Length);
            return Enumerable.Range(0, len)
                .Where(i => a[i] == b[i])
                .Select(i => a[i]).ToArray();
        }
    }

    // ── 스키마 기반 비교 결과 ─────────────────────────────────────────

    /// <summary>필드 단위 비교 항목.</summary>
    public record FieldDiffEntry(
        string Name,
        int Offset,
        int Size,
        byte[] ValA,
        byte[] ValB,
        string Status)
    {
        /// <summary>값이 변경됐으면 true.</summary>
        public bool IsChanged => Status == "변경";

        /// <summary>변경 내용 요약 문자열.</summary>
        public override string ToString()
            => $"  [{Status}] {Name,-18} offset=0x{Offset:X4}" +
               $"  A=[{string.Join(" ", ValA.Select(b => $"{b:X2}"))}]" +
               $"  B=[{string.Join(" ", ValB.Select(b => $"{b:X2}"))}]";
    }

    /// <summary>
    /// 스키마 기반 필드 비교 결과.
    /// <see cref="BufferDiff.CompareFields"/> 로 생성됩니다.
    /// <example><code>
    /// var fdiff = BufferDiff.CompareFields(old, new_, schema,
    ///     ignoreFields: new[]{"Timestamp","CRC"});
    ///
    /// // 요약 출력
    /// Console.WriteLine(fdiff.Summary);
    ///
    /// // 특정 필드 확인
    /// bool changed = fdiff.HasFieldChanged("Config");
    ///
    /// // 변경 필드 목록
    /// foreach (var name in fdiff.ChangedFieldNames)
    ///     Console.WriteLine($"변경: {name}");
    /// </code></example>
    /// </summary>
    public sealed class FieldDiffResult
    {
        /// <summary>변경된 필드 목록.</summary>
        public IReadOnlyList<FieldDiffEntry> ChangedFields { get; }

        /// <summary>동일한 필드 목록.</summary>
        public IReadOnlyList<FieldDiffEntry> SameFields { get; }

        /// <summary>변경된 필드가 있으면 true.</summary>
        public bool HasChanges => ChangedFields.Count > 0;

        internal FieldDiffResult(
            IReadOnlyList<FieldDiffEntry> changed,
            IReadOnlyList<FieldDiffEntry> same)
        {
            ChangedFields = changed;
            SameFields = same;
        }

        /// <summary>특정 필드가 변경됐는지 확인합니다.</summary>
        public bool HasFieldChanged(string fieldName)
            => ChangedFields.Any(f =>
                string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        /// <summary>변경된 필드 이름 목록.</summary>
        public IEnumerable<string> ChangedFieldNames
            => ChangedFields.Select(f => f.Name);

        /// <summary>
        /// 요약 문자열.
        /// <example><code>
        /// Console.WriteLine(fdiff.Summary);
        /// // [FieldDiff]  변경=1  동일=2
        /// //   [변경] Config  offset=0x0001  A=[01 00]  B=[02 00]
        /// </code></example>
        /// </summary>
        public string Summary
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[FieldDiff]  변경={ChangedFields.Count}  동일={SameFields.Count}");
                foreach (var f in ChangedFields) sb.AppendLine(f.ToString());
                return sb.ToString().TrimEnd();
            }
        }
    }
}