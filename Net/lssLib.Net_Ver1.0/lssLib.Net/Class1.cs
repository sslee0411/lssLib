
using System.Data;

namespace lssLib.Net
{
    /*
    public class Body
    {
        public IConfig; // 장비 설정 정보 (IP, Port, Baudrate, 등등) , 채널사용
        public ConnectionState; // 접속 여부

        public INetType; //Serial, TCP, UDP, SharedMemory, 등
        public ireTray // Retray 패펀(접속, 읽기, 쓰기) 사용할지 여부, 및 기타 접속 패턴
        public iSequence;  // 접속, 읽기, 쓰기 시 순차적으로 할지 여부
        public List<Byte[]> ReadCommand; // 주기적으로 보낼 읽기 요청 프레임 정의 ( 우선순위 사용 일반 ),채널사용
        public List<Byte[]> WriteCommand; // 쓰기 요청 프레임 정의 ( 우선순위사용  놑음 ) , 채널사용

        public Body()
        {
            ReadCommand = new List<byte[]>();
            WriteCommand = new List<byte[]>();
        }
        public Body(DeviceID, DeviceName, ConnectionState, INetType, ireTray, iSequence, IConfig)
        {
            this.DeviceID = DeviceID;
            this.DeviceName = DeviceName;
            this.ConnectionState = ConnectionState;
            this.INetType = INetType;
            this.ireTray = ireTray;
            this.iSequence = iSequence;
            ReadCommand = new List<byte[]>();
            WriteCommand = new List<byte[]>();
        }
        public void AddReadCommand(byte[] command)
        {
            ReadCommand.Add(command);
        }
        public void AddWriteCommand(byte[] command)
        {
            WriteCommand.Add(command);
        }
        private void RemoveReadCommand(byte[] command)
        {
            ReadCommand.Remove(command);
        }

        private void RemoveWriteCommand(byte[] command)
        {
            WriteCommand.Remove(command);
        }

        private void ClearReadCommands()
        {
            ReadCommand.Clear();
        }

        private void ClearWriteCommands()
        {
            WriteCommand.Clear();
        }


    }

    */
}
