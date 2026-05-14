// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/NetTransportType.cs
//  역할: 전송 계층 종류 열거형 — 전 계층 포함
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>전송 계층 종류.</summary>
public enum NetTransportType
{
    // ── 기본 네트워크 ───────────────────────────────────────────
    /// <summary>TCP 클라이언트 통신. 가장 범용적인 산업용 통신.</summary>
    Tcp,
    /// <summary>COM 포트 직렬 통신 (RS-232 / RS-485 / Modbus RTU).</summary>
    Serial,
    /// <summary>UDP 송수신 (비연결, 브로드캐스트 포함).</summary>
    Udp,
    // ── 프로세스 간 통신 (IPC) ──────────────────────────────────
    /// <summary>프로세스 간 공유 메모리 IPC (동일 호스트, 초저지연).</summary>
    SharedMemory,
    /// <summary>Named Pipe IPC (System.IO.Pipes, 동일 호스트 또는 네트워크).</summary>
    NamedPipe,
    // ── 웹 및 메시징 ─────────────────────────────────────────────
    /// <summary>HTTP REST API (HttpClient POST/GET 기반 요청-응답).</summary>
    Http,
    /// <summary>WebSocket 실시간 양방향 통신 (System.Net.WebSockets).</summary>
    WebSocket,
    /// <summary>MQTT 경량 메시징 프로토콜 (BCL TCP 기반 MQTT 3.1.1 직접 구현). 브로커: Mosquitto / EMQX.</summary>
    Mqtt,
    // ── 특수 목적 ────────────────────────────────────────────────
    /// <summary>가상 Transport (인메모리 Channel기반). 단위 테스트·시뮬레이터용.</summary>
    Virtual
}