// ====================================================================
//  lssLib.Serialization.WpfDemo — ViewHelper
//  모든 View 에서 공유하는 공통 유틸리티
// ====================================================================

using System.Text;
using System.Windows.Controls;
using lssLib.Binary;

namespace lssLib.Serialization.WpfDemo.Views
{
    internal static class ViewHelper
    {
        private const string DIVIDER = "──────────────────────────────────────────────────────";

        /// <summary>결과를 TextBox 상단에 prepend 합니다.</summary>
        public static void Output(TextBox box, string text)
        {
            box.Text = text.TrimEnd() + "\n\n" + DIVIDER + "\n" + box.Text;
            box.ScrollToHome();
        }

        /// <summary>예외를 포함한 안전 실행 후 Output 호출.</summary>
        public static void Run(TextBox box, Func<string> fn)
        {
            try   { Output(box, fn()); }
            catch (Exception ex)
            { Output(box, $"[{ex.GetType().Name}]\n  {ex.Message}"); }
        }

        /// <summary>HEX 문자열 → byte[]</summary>
        public static byte[] FromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return [];
            var bp = BufferParser.FromHex(hex);
            return bp.ReadRaw(0, bp.Length);
        }

        /// <summary>BufSchema 텍스트 파싱. 형식: "offset,BufType,size,name"</summary>
        public static BufSchema ParseSchemaText(string text)
        {
            var schema = new BufSchema();
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Trim().Split(',');
                if (p.Length < 4) continue;
                if (!int.TryParse(p[0].Trim(), out int off)) continue;
                if (!Enum.TryParse<BufType>(p[1].Trim(), out var bt)) continue;
                if (!int.TryParse(p[2].Trim(), out int sz)) sz = 1;
                schema.Add(p[3].Trim(), bt, off, sz);
            }
            return schema;
        }

        /// <summary>결과창 초기화.</summary>
        public static void Clear(TextBox box) => box.Text = "";

        /// <summary>구분자 라인 생성.</summary>
        public static string Line(string title = "")
            => string.IsNullOrEmpty(title)
               ? DIVIDER
               : $"── {title} " + new string('─', Math.Max(0, 50 - title.Length));
    }
}
