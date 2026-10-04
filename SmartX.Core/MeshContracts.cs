// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 3 (PoE): Smart-X Network Topology & Mesh Routing
// File:    SmartX.Core/MeshContracts.cs
// Purpose: Shared DTO contracts for the mesh topology feature - graph nodes/edges, traversal
//          results, MST routing paths, and historical range-query requests/results.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Records (C# reference). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
namespace SmartX.Core;

/// <summary>A vertex in the mesh graph. X/Y are normalised layout coordinates in [0,1].</summary>
public record MeshNodeDto(
    string Id,
    string Label,
    SensorCategory Category,
    bool IsGateway,
    bool IsDown,
    double X,
    double Y);

/// <summary>A weighted, undirected wireless link between two nodes. Weight = routing cost.</summary>
public record MeshEdgeDto(string FromId, string ToId, double Weight, bool InMst);

/// <summary>The whole mesh graph plus the total weight of its minimum spanning tree.</summary>
public record MeshGraphDto(
    IReadOnlyList<MeshNodeDto> Nodes,
    IReadOnlyList<MeshEdgeDto> Edges,
    double MstTotalWeight);

/// <summary>Result of a BFS/DFS traversal from a start node.</summary>
public record TraversalDto(
    string Kind,
    string StartId,
    IReadOnlyList<string> Order,
    IReadOnlyList<string> Reachable,
    IReadOnlyList<string> Unreachable);

/// <summary>The power-efficient routing path (through the MST) from a device to the gateway.</summary>
public record RouteDto(
    string DeviceId,
    IReadOnlyList<string> Hops,
    double TotalCost,
    bool Reachable);

/// <summary>The minimum spanning tree edges and their total routing cost.</summary>
public record MstResult(string Algorithm, double TotalWeight, IReadOnlyList<MeshEdgeDto> Edges);

/// <summary>Request for a historical range query against a device's AVL tree.</summary>
public record HistoryQuery(string DeviceId, DateTimeOffset FromUtc, DateTimeOffset ToUtc);
