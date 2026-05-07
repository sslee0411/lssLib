using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using lssLib.Binary;
using lssLib.Extensions;
// ════════════════════════════════════════════════════════════════
//  CrcView — CRC/Checksum
// ════════════════════════════════════════════════════════════════
namespace lssLib.Serialization.WpfDemo.Views
{
    /// <summary>
    /// CrcView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class CrcView : UserControl
    {
        public CrcView() => InitializeComponent();

        private void OnAll(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] d = ViewHelper.FromHex(DataIn.Text);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("전체 알고리즘 비교"));
            sb.AppendLine($"  데이터  : {d.ToHexString()}  ({d.Length}B)");
            sb.AppendLine();
            sb.AppendLine($"  d.Crc8()              = 0x{d.Crc8():X2}         (CRC-8/IBM   0x07)");
            sb.AppendLine($"  d.Crc16()             = 0x{d.Crc16():X4}       (CRC-16/IBM  0x8005)");
            sb.AppendLine($"  d.Crc16Ccitt(0xFFFF)  = 0x{d.Crc16Ccitt(0xFFFF):X4}       (CCITT, BLE)");
            sb.AppendLine($"  d.Crc16Ccitt(0x0000)  = 0x{d.Crc16Ccitt(0x0000):X4}       (XMODEM)");
            sb.AppendLine($"  d.Crc16Modbus()       = 0x{d.Crc16Modbus():X4}       (Modbus RTU)");
            sb.AppendLine($"  d.Crc32()             = 0x{d.Crc32():X8}   (CRC-32)");
            sb.AppendLine($"  d.Sum8()              = 0x{d.Sum8():X2}");
            sb.AppendLine($"  d.Sum8Twos()          = 0x{d.Sum8Twos():X2}");
            sb.AppendLine($"  d.Sum16()             = 0x{d.Sum16():X4}");
            sb.AppendLine($"  d.Xor()               = 0x{d.Xor():X2}");
            sb.AppendLine($"  d.Fletcher16()        = 0x{d.Fletcher16():X4}");
            return sb.ToString();
        });

        private void OnAppend(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] d = ViewHelper.FromHex(DataIn.Text);
            byte[] w = d.AppendCrc32();
            bool ok = w.VerifyCrc32();
            byte[] t = (byte[])w.Clone(); t[0] ^= 0xFF;
            bool fail = t.VerifyCrc32();
            return $"{ViewHelper.Line("AppendCrc32 / VerifyCrc32")}\n  원본 ({d.Length}B): {d.ToHexString()}\n  Append({w.Length}B): {w.ToHexString()}\n  검증    : {(ok ? "✅ PASS" : "❌ FAIL")}\n  훼손 후 : {(fail ? "✅" : "❌ FAIL")}  (byte[0] XOR 0xFF)";
        });

        private void OnCrc8Sht(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] b = { 0x65, 0x66 };
            byte cs = b.Crc8Sht();
            return $"{ViewHelper.Line("Crc8Sht — Sensirion SHT3x (poly=0x31, init=0xFF)")}\n  데이터  : {b.ToHexString()}\n  계산값  : 0x{cs:X2}\n\n  [SHT3x 6바이트 응답 구조]\n  [TempH][TempL][TempCRC][HumiH][HumiL][HumiCRC]\n  각 측정값 CRC = 앞 2바이트에 Crc8Sht() 적용";
        });

        private void OnCustom(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] d = ViewHelper.FromHex(DataIn.Text);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("Crc8Custom — 커스텀 다항식"));
            foreach (var (poly, init, label) in new[]{
                (0x07,(byte)0x00,"CRC-8/IBM"), (0x31,(byte)0xFF,"CRC-8/Sensirion"),
                (0x9B,(byte)0xFF,"CRC-8/CDMA2000"), (0x1D,(byte)0xFF,"CRC-8/WCDMA"),
            })
                sb.AppendLine($"  poly=0x{poly:X2}  init=0x{init:X2}  [{label}] = 0x{d.Crc8Custom((byte)poly, init):X2}");
            return sb.ToString();
        });

        private void OnModbus(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] req = { 0x01, 0x03, 0x00, 0x00, 0x00, 0x04 };
            ushort crc = req.Crc16Modbus();
            byte[] frame = [.. req, (byte)(crc & 0xFF), (byte)(crc >> 8)];
            ushort stored = (ushort)(frame[^2] | (frame[^1] << 8));
            bool ok = stored == frame.Crc16Modbus(0, frame.Length - 2);
            return $"{ViewHelper.Line("Crc16Modbus — Modbus RTU")}\n  데이터 : {req.ToHexString()}\n  CRC-16 : 0x{crc:X4}  → [{crc & 0xFF:X2} {crc >> 8:X2}] (LE)\n  프레임 : {frame.ToHexString()}\n  검증   : {(ok ? "✅ PASS" : "❌ FAIL")}";
        });

        private void OnOffset(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] d = ViewHelper.FromHex(DataIn.Text);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("offset / length 지정 계산"));
            sb.AppendLine($"  전체   : {d.ToHexString()}  Crc32=0x{d.Crc32():X8}");
            if (d.Length >= 4)
            {
                sb.AppendLine($"  [0..2] : Crc32(offset=0,length=3) = 0x{d.Crc32(0, 3):X8}");
                sb.AppendLine($"  [1..3] : Crc32(offset=1,length=3) = 0x{d.Crc32(1, 3):X8}");
                sb.AppendLine($"  [2..]  : Crc32(offset=2)          = 0x{d.Crc32(2):X8}");
            }
            return sb.ToString();
        });

        private void OnXorNmea(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            string[] nmea = {
                "$GPRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*6A",
                "$GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,*47",
            };
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("XOR — NMEA 0183 체크섬"));
            foreach (var sentence in nmea)
            {
                int star = sentence.IndexOf('*');
                byte cs = System.Text.Encoding.ASCII.GetBytes(sentence[1..star]).Xor();
                byte exp = byte.Parse(sentence[(star + 1)..(star + 3)], System.Globalization.NumberStyles.HexNumber);
                sb.AppendLine($"  {sentence}");
                sb.AppendLine($"  XOR=0x{cs:X2}  기대=0x{exp:X2}  {(cs == exp ? "✅ PASS" : "❌ FAIL")}");
                sb.AppendLine();
            }
            return sb.ToString();
        });

        private void OnSum8(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] d = ViewHelper.FromHex(DataIn.Text);
            byte cs = d.Sum8Twos();
            byte[] f = [.. d, cs];
            bool ok = f.Sum8() == 0x00;
            return $"{ViewHelper.Line("Sum8 / Sum8Twos — 프레임 검증")}\n  데이터 : {d.ToHexString()}\n  Sum8   : 0x{d.Sum8():X2}\n  Twos   : 0x{cs:X2}  (체크섬 바이트)\n  프레임 : {f.ToHexString()}\n  Sum8==0: {(ok ? "✅ PASS" : "❌ FAIL")}";
        });

        private void OnClear(object s, RoutedEventArgs e) => ViewHelper.Clear(Out);
    }
}
