using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using lssLib.Net.Demo;

// LogManager.Instance.LogLevel = LogLevel.Debug;
// 예시 1: Serial Modbus RTU
//async Task<await> Example1_SerialModbus.RunAsync();

// 예시 2: TCP Passive
// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
// await Example2_TcpPassive.RunAsync(cts.Token);

// 예시 3: NetDeviceRegistry 다중 장비 관리
// await Example3_MultiDevice.RunAsync();

await Example4_TcpPassiveClient.RunAsync();

//await Example5A_TcpRequestResponse_Periodic.RunAsync();