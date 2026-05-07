
namespace lssLib.Net;


/// <summary>전송 계층 종류 열거형.</summary>
public enum NetTransportType
{
    // --- 기본 네트워크 ---
    /// <summary>TCP 클라이언트.</summary>
    Tcp = 0,
    /// <summary>UDP 송수신.</summary>
    Udp,
    /// <summary>COM 포트 직렬 통신(RS-232/485).</summary>
    Serial,

    // --- 프로세스 간 통신 (IPC) ---
    /// <summary>프로세스 간 공유 메모리 IPC.</summary>
    SharedMemory,
    /// <summary>로컬 파이프 통신 (Windows/Linux IPC).</summary>
    NamedPipe,

    // --- 웹 및 메시징 ---
    /// <summary>HTTP/REST API 호출.</summary>
    Http,
    /// <summary>WebSocket (실시간 양방향 웹 통신).</summary>
    WebSocket,
    /// <summary>MQTT 브로커 (IoT 표준 메시징).</summary>
    Mqtt,
    /// <summary>gRPC (고성능 RPC 프레임워크).</summary>
    Grpc,

    // --- 산업용 표준 (OT/FA) ---
    /// <summary>Modbus TCP/RTU (산업용 기기 표준).</summary>
    Modbus,
    /// <summary>OPC UA (스마트 팩토리 통합 표준).</summary>
    OpcUa,
    /// <summary>EtherCAT (초고속 실시간 제어).</summary>
    EtherCat,

    // --- 특수 목적 ---
    /// <summary>Bluetooth LE (근거리 저전력 무선).</summary>
    Bluetooth,
    /// <summary>테스트용 가상 드라이버 (Loopback).</summary>
    Virtual,

    //--- 확장 가능성 ---
    ETC = 9999
}