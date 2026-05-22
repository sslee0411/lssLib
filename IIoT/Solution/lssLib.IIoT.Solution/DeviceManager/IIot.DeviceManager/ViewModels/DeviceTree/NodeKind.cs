// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · NodeKind.cs
//  역할: 장비 트리 노드 종류 열거형
//  생성: 2025-05-22
// ══════════════════════════════════════════════════════════

using System;
namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// 장비 트리 노드 종류
/// </summary>
public enum NodeKind
{
    /// <summary>논리 그룹 (무제한 중첩)</summary>
    Group,

    /// <summary>실제 장비 (CommConfig 연결)</summary>
    Device,

    /// <summary>PLC 슬롯 / 채널</summary>
    Plc,

    /// <summary>수집 태그 (리프 노드)</summary>
    Tag
}