using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace lssLib.Serialization.WpfDemo.Views
{
    public class SensorRecord
    {
        public SensorRecord() { }
        public SensorRecord(int id, string name, float temp, float humi, decimal price)
        { Id = id; Name = name; Temp = temp; Humi = humi; Price = price; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public float Temp { get; set; }
        public float Humi { get; set; }
        public decimal Price { get; set; }
    }

    public class SchemaFieldDef
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public int Offset { get; set; }
        public int Size { get; set; } = 1;
    }

    public record ProcessedSensor(byte Id, double Temp, double Humi, double Volt);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SensorPacket
    {
        public byte Header;
        public byte DeviceId;
        public uint Sequence;
        public ushort TempAdc;
        public ushort HumiAdc;
        public byte Flags;
        public byte Checksum;
    }
}
