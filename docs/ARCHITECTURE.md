# Architecture — Smart-X Data Ingestion & Validation Gateway

## Layers

| Layer | Project | Responsibility |
|-------|---------|----------------|
| Domain | `SmartX.Core` | Models, DTOs and the advanced OOP building blocks. Referenced by **both** other projects, so there is one source of truth for the wire contract. |
| Backend | `SmartX.Api` | ASP.NET Core Minimal API. Receives telemetry, validates it, detects anomalies, seeds mock data, serves the dashboard snapshot. |
| Frontend | `SmartX.Client` | WPF desktop client. Startup pillars, device registration, file upload, and the live engagement dashboard. Talks to the API over async HTTP. |

```
ESP32 mesh (simulated / seeded)
        │  float · int · bool
        ▼
SmartX.Api  ──►  TelemetryStore  ──►  anomaly detection (spike + disconnect)
        ▲                                   │
        │ HTTP/JSON (async HttpClient)       ▼
SmartX.Client  ◄────────────────────  IngestionSnapshot (KPIs, heat-strip, alerts)
        └──────────► SmartX.Core ◄──────────┘  (shared DTOs + OOP concepts)
```

## Data flow (ingestion)

1. The client POSTs an `IngestRequest` (device, metric, kind, value) to `/api/telemetry`.
2. `TelemetryStore.Ingest` builds a strongly-typed `TelemetryPacket<float|int|bool>` — the
   payload is stored as `T`, never boxed.
3. A trailing-window mean ± 3σ test flags spikes; alerts are appended.
4. `/api/snapshot` rolls the store up into KPIs, per-device health (including silent/
   disconnected nodes) and recent alerts.
5. The client polls the snapshot every 3 s and repaints the heat-strip and alert wall.

## Advanced OOP concepts and where they live

| Concept | Location |
|---------|----------|
| Generics (no boxing) | `SmartX.Core/TelemetryPacket.cs` |
| Operator overloading | `SmartX.Core/SensorReading.cs` |
| Recursion | `SmartX.Core/DeploymentTopology.cs` |
| Multi-dim / jagged arrays → `List<T>` | `SmartX.Core/HistoricalBatch.cs` |
| Collections optimisation | `SmartX.Api/Services/TelemetryStore.cs` |

## Key design decisions

- **Shared Core library** keeps DTOs identical on both sides of the wire; enums are
  serialised as strings via `JsonStringEnumConverter`.
- **In-memory store** (no database) per the brief; heavily seeded so the structures are
  exercised under load, with two injected spikes and one injected disconnect.
- **Async-only client I/O** so the WPF UI thread never blocks on the network.
- **Global exception logging** in `App.xaml.cs` keeps the client alive and writes a crash
  log instead of closing silently.

## Part 2 — Real-Time Command Stream

Part 2 adds `CommandStreamEngine` (a second in-memory singleton) and the `CommandStreamView`,
wired into the same shell. Advanced collections carry the volatile real-time state:

| Structure | Role |
|-----------|------|
| `Queue<T>` | FIFO telemetry ingestion stream. |
| `PriorityQueue<T,int>` | Critical alerts bypass the FIFO stream (processed first). |
| `Stack<T>` | Manual-command history for Undo. |
| `Dictionary<string,…>` | O(1) live device lookup by MAC. |
| `SortedDictionary<long,double>` | Per-device timeline in timestamp order. |
| `HashSet<string>` | Unique active errors / disconnected nodes. |

A predictive engine records *condition → command* associations (e.g. low soil moisture →
Trigger Pump) and surfaces them as "Suggested Actions". Navigation reuses each view instance,
so switching between the Part 1 and Part 2 modules preserves state without re-hitting the API.

## Part 3 — Network Topology & Mesh Routing

Part 3 adds `TopologyEngine` (a third in-memory singleton) and the `TopologyView`, completing
the three-pillar suite. Tree, heap and graph structures power the mesh feature:

| Structure | Role |
|-----------|------|
| `TelemetryAvlTree` (AVL) | Self-balancing BST of timestamped readings; O(log n + k) range queries. |
| `MinHeap<T>` | Binary heap driving Prim's MST (cheapest frontier edge). |
| `MeshGraph` (adjacency list) | Vertices = sensors/gateway, edges = weighted links. |
| BFS / DFS | Reachability from the gateway; detects down/isolated nodes. |
| MST (Prim's + Kruskal's) | Most power-efficient routing tree; both agree on total weight. |

The graph is computed server-side and returned with normalised (0-1) layout coordinates, so the
WPF client only scales them to the canvas - keeping all algorithms in one place and the UI thin.
