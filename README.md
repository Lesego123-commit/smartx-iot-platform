# Smart-X — IoT Operational Intelligence Platform

![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Minimal%20API-512BD4)
![WPF](https://img.shields.io/badge/WPF-Desktop%20Client-0078D6)
![Docker](https://img.shields.io/badge/Docker-ready-2496ED?logo=docker&logoColor=white)

> A full-stack **.NET 9** platform that ingests real-time IoT telemetry, drives a live operations console, and computes power-efficient mesh routing — built on an **ASP.NET Core Minimal API** backend with a **WPF** desktop client.

### Highlights
- **Real-time ingestion pipeline** — multi-typed telemetry processed through FIFO + priority queues, with validation and anomaly detection.
- **Live command stream** — device control with an undo **stack**, O(1) device **dictionary** lookup, and a predictive action engine.
- **Network topology & routing** — a **graph** of the mesh with **BFS/DFS**, a **minimum spanning tree** (Prim + Kruskal), and an **AVL tree** for time-range history queries.
- **Hand-implemented data structures** — generics with no boxing, a binary heap, an AVL tree and graph algorithms, not just library calls.
- Clean three-project solution (shared Core · API · desktop client), Dockerised API, fully documented.

*Built by Lesego Masike · originally developed as a university software-engineering portfolio project (PROG7312).*

A .NET 9 operational-intelligence gateway for the **Smart-X** IoT mesh ecosystem. Thousands
of ESP32-class microcontrollers publish multi-typed telemetry — `float` for soil moisture,
`int` for power wattage, `bool` for valve states — to a central .NET backend that validates,
stores, processes and surfaces the data in real time.

This is the **final Portfolio of Evidence**: the complete, fully-functioning application with
**all three pillars active** — Part 1 (Sensor Data Ingestion), Part 2 (Real-Time Command
Stream) and Part 3 (Network Topology & Mesh Routing). Part 3 adds Trees, Heaps and Graphs:
a self-balancing **AVL tree** for historical range queries, a **graph** of the mesh with
**BFS/DFS** traversal, and a **minimum spanning tree** (Prim's via a custom min-heap, and
Kruskal's via union-find) that computes the most power-efficient routing to the gateway.

> **Jump to:** [Part 3 — Network Topology & Mesh Routing](#part-3--network-topology--mesh-routing) ·
> [Part 2 — Real-Time Command Stream](#part-2--real-time-command-stream)

<!-- Part 2 summary retained below for continuity. -->
It **builds on Part 1 & 2** (both still fully working) with a Real-Time Command Stream pillar
driven by advanced data structures: FIFO and priority queues, a command-undo stack,
dictionaries and sorted dictionaries, hash sets, and a predictive action engine. The third
pillar (Network Topology & Mesh Routing) remains locked for the final PoE.

> **Jump to:** [Part 2 — Real-Time Command Stream](#part-2--real-time-command-stream) ·
> [How to test the Part 2 data structures](#how-to-test-the-part-2-data-structures)

---

## Architecture

The solution is split into three projects so the domain logic has a single source of truth
that **both** the API and the desktop client compile against.

```
SmartX.sln
├── SmartX.Core      → class library: domain models + the advanced OOP concepts
├── SmartX.Api        → ASP.NET Core Minimal API: the ingestion/validation engine (port 5101)
└── SmartX.Client     → WPF desktop client: startup pillars, registration, live dashboard
```

```
        ┌────────────────────┐         HTTP / JSON          ┌────────────────────┐
        │  SmartX.Client     │  ─────────────────────────▶  │   SmartX.Api        │
        │  (WPF desktop)     │  ◀─────────────────────────  │  (Minimal API)      │
        └────────────────────┘      async HttpClient        └─────────┬──────────┘
                    │                                                   │
                    └───────────────► SmartX.Core ◄────────────────────┘
                         (shared DTOs + generics/overloading/recursion)
```

**Stack:** .NET 9 · ASP.NET Core Minimal APIs · WPF · System.Text.Json · xUnit-free (manual
API smoke tests documented below). No database — the store is in-memory and heavily seeded
with mock telemetry, per the brief.

---

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| .NET SDK    | **9.0** | `dotnet --version` should print `9.x`. |
| OS          | Windows 10/11 | The WPF client targets `net9.0-windows`. |
| IDE         | Visual Studio 2022 (17.12+) with the *.NET desktop development* workload | Optional — the CLI works too. |

> **Note on .NET 10:** the brief references .NET 10 as the backbone. This build targets the
> installed **.NET 9** LTS-track SDK so it compiles and runs out of the box. To move to .NET 10,
> change the three `<TargetFramework>` values and re-restore — no code changes required.

---

## How to run

### Option A — Visual Studio 2022
1. Open **`SmartX.sln`**.
2. Right-click the solution → **Configure Startup Projects…** → **Multiple startup projects**.
3. Set **SmartX.Api** = *Start* and **SmartX.Client** = *Start* (Api first).
4. Press **F5**. The API boots on `http://localhost:5101`; the WPF window opens and shows a
   green **Connected** pill.

### Option B — command line (two terminals)

Restore + build once:
```bash
dotnet restore
dotnet build
```

Terminal 1 — boot the backend API:
```bash
dotnet run --project SmartX.Api --urls http://localhost:5101
```

Terminal 2 — launch the desktop client:
```bash
dotnet run --project SmartX.Client
```

The client connects to `http://localhost:5101` (configured in
`SmartX.Client/MainWindow.xaml.cs → ApiBaseUrl` and the API's
`Properties/launchSettings.json`). Start the API **before** the client.

---

## Using the app

1. **Landing page** — three architectural pillars. Only *Sensor Data Ingestion & Telemetry*
   is active; open it.
2. **Register a device** — enter a MAC address, deployment location and category, then
   *Register device*. Invalid MACs are rejected with an inline message.
3. **Attach a config / log / photo** — pick a target device, click *Choose file…*
   (`OpenFileDialog`) and *Upload to gateway* (multipart upload to the API).
4. **Push telemetry** — send a `Float`/`Int`/`Bool` sample; anomalous spikes are flagged.
5. **Live dashboard** — the **Anomaly Heat-Strip** and **Live Alert Wall** refresh every 3s,
   surfacing spikes and silent/disconnected sensors instantly.

---

## Where each required concept lives

| Requirement | File | What it does |
|-------------|------|--------------|
| **Generics** (`TelemetryPacket<T>`, no boxing) | `SmartX.Core/TelemetryPacket.cs` | Value-type-constrained wrapper; payload stored as `T`, never `object`. |
| **Operator overloading** | `SmartX.Core/SensorReading.cs` | `+ - *` and comparison operators — `Meter3 = Meter1 + Meter2`. |
| **Recursion** | `SmartX.Core/DeploymentTopology.cs` | Recursively validates nested deployment trees (Sub-Zone → Zone → Facility). |
| **Arrays → Collections** | `SmartX.Core/HistoricalBatch.cs` | Jagged & multi-dimensional raw batches flattened into `List<SensorReading>`. |
| **File / log upload** | `SmartX.Client/Views/IngestionView.xaml.cs` + `SmartX.Api/Program.cs` | `OpenFileDialog` → multipart POST → saved under `SmartX.Api/uploads/`. |
| **Dynamic engagement feature** | `SmartX.Client/Views/IngestionView.xaml` (heat-strip + alert wall) | Anomaly Heat-Strip + Live Alert Wall (the researched strategy — not a progress bar). |
| **API integration** | `SmartX.Client/Services/SmartXApiClient.cs` | Async `HttpClient` wrapper over every endpoint. |
| **In-memory engine + seeding** | `SmartX.Api/Services/TelemetryStore.cs`, `MockSeeder.cs` | Ingestion, spike/disconnect detection, snapshot; 10 seeded devices + injected anomalies. |

---

## API reference

| Method & path | Purpose |
|---------------|---------|
| `GET  /api/health` | Liveness probe. |
| `GET  /api/sensors` | List registered devices. |
| `POST /api/sensors` | Register a device (`RegisterSensorRequest`). |
| `POST /api/sensors/{id}/attachment` | Multipart upload of a config/log/photo. |
| `POST /api/telemetry` | Ingest one typed sample (`IngestRequest`). |
| `GET  /api/telemetry` | Query samples (`?device=&metric=&limit=`). |
| `GET  /api/telemetry/anomalies` | Recent flagged anomalies. |
| `GET  /api/snapshot` | Rolled-up health for the dashboard. |
| `GET  /api/alerts` | Recent proactive alerts. |
| `POST /api/topology/validate` | Recursive validation of a deployment tree. |
| `GET  /api/topology/sample` | A sample multi-tier tree to validate. |
| `GET  /api/aggregate` | Aggregate load via operator overloading (`?device=&metric=&unit=`). |

### Quick API smoke test (with the API running)
```bash
curl http://localhost:5101/api/snapshot
curl -X POST http://localhost:5101/api/sensors -H "Content-Type: application/json" \
  -d '{"mac":"AA:BB:CC:DD:EE:01","location":"Zone 1","category":"Environmental","description":"probe"}'
curl "http://localhost:5101/api/aggregate?device=3C:71:BF:0A:2D:06&metric=power_wattage&unit=W"
```

---

## Project layout
```
SmartX/
├── SmartX.sln
├── README.md
├── .gitignore
├── SmartX.Core/
│   ├── TelemetryPacket.cs      # generics (no boxing)
│   ├── SensorReading.cs        # operator overloading
│   ├── DeploymentTopology.cs   # recursion
│   ├── HistoricalBatch.cs      # arrays → List<T>
│   ├── MacAddress.cs           # MAC validation
│   ├── Dtos.cs                 # shared contracts
│   └── Enums.cs
├── SmartX.Api/
│   ├── Program.cs              # Minimal API endpoints
│   └── Services/
│       ├── TelemetryStore.cs   # in-memory engine + anomaly detection
│       └── MockSeeder.cs       # seeded mock telemetry
└── SmartX.Client/
    ├── App.xaml(.cs)           # global exception logging
    ├── MainWindow.xaml(.cs)    # shell + 3 pillars
    ├── Theme.xaml              # dark-theme resources
    ├── Services/SmartXApiClient.cs
    ├── Models/DashboardViewModels.cs
    └── Views/
        ├── StartupView.xaml(.cs)   # landing / pillar menu
        └── IngestionView.xaml(.cs) # registration + upload + live dashboard
```

---

## Part 2 — Real-Time Command Stream

Part 2 unlocks the **Real-Time Command Stream** pillar. Open it from the landing page or the
left nav; navigation between the Part 1 and Part 2 modules is asynchronous and preserves each
view's state (views are created once and reused). The page provides:

- a **queue status bar** (FIFO depth, priority depth, processed count, active-error count, command-stack depth) with *Process next* / *Process all* controls;
- a **device list** with **search / filter** by category and alert state, and per-device manual override commands (**Pump**, **Relay**);
- a **timeline** sparkline rendered from the timestamp-ordered history;
- an **Automated Insights & Suggested Actions** panel (the predictive engine), each with an *Apply* button;
- a **Command History** panel with **Undo**; and an **Active Errors (HashSet)** panel.

### Advanced data structures (Part 2 rubric)

| Structure | File | Role |
|-----------|------|------|
| `Queue<T>` (FIFO) | `SmartX.Api/Services/CommandStreamEngine.cs` | Standard telemetry ingestion stream. |
| `PriorityQueue<T,int>` | same | Critical alerts bypass the FIFO stream and are processed first. |
| `Stack<T>` | same | Manual-command history powering the **Undo** feature. |
| `Dictionary<string,…>` | same | O(1) live device lookup by MAC / node id. |
| `SortedDictionary<long,double>` | same | Per-device timeline kept in timestamp order for the chart. |
| `HashSet<string>` | same | Unique active errors / disconnected nodes (no duplicate alerts). |
| Predictive engine | same (`Insights`, `LearnFromCommand`) | Learns *condition → command* patterns and suggests actions. |

### Part 2 API endpoints

| Method & path | Purpose |
|---------------|---------|
| `GET  /api/stream/status` | Queue depths + processed/error/stack counters. |
| `GET  /api/stream/devices` | Live device states (from the dictionary). |
| `GET  /api/stream/search?category=&alertState=` | Filtered device search (logged for the predictive engine). |
| `POST /api/stream/process?all=true|false` | Dequeue one packet (priority first) or drain the queues. |
| `POST /api/commands` | Issue a manual override command (pushes the undo stack). |
| `POST /api/commands/undo` | Undo the most recent command (pops + reverts). |
| `GET  /api/commands/history?limit=` | Command history (stack, newest first). |
| `GET  /api/stream/timeline?device=&limit=` | Timestamp-ordered timeline (sorted dictionary). |
| `GET  /api/stream/errors` | Unique active errors (hash set). |
| `GET  /api/stream/insights` | Predictive "Suggested Actions" / "Automated Insights". |

### How to test the Part 2 data structures

With the API running (`dotnet run --project SmartX.Api --urls http://localhost:5101`):

```bash
# Queue depths on boot: a FIFO backlog and 2 critical packets already on the PRIORITY queue.
curl http://localhost:5101/api/stream/status

# Simulate a HIGH-PRIORITY alert that bypasses the FIFO stream, then process one packet:
#   the priority queue is drained before the standard queue.
curl -X POST http://localhost:5101/api/stream/process        # processes the urgent packet first

# Dictionary O(1) lookup + search/filter by alert state:
curl "http://localhost:5101/api/stream/search?alertState=Critical"

# Sorted-dictionary timeline (returned in ascending timestamp order):
curl "http://localhost:5101/api/stream/timeline?device=3C:71:BF:0A:2D:05&limit=10"

# Command stack + undo:
curl -X POST http://localhost:5101/api/commands -H "Content-Type: application/json" \
  -d '{"deviceId":"3C:71:BF:0A:2D:01","action":"TriggerPump"}'
curl -X POST http://localhost:5101/api/commands/undo         # pops the stack + reverts

# HashSet of unique active errors, and the predictive suggestions:
curl http://localhost:5101/api/stream/errors
curl http://localhost:5101/api/stream/insights
```

In the desktop client the same flows are on the **Real-Time Command Stream** page: click a
device to load its timeline, use **Pump/Relay** to issue commands, **Undo last command** to pop
the stack, **Process next/all** to drain the queues, and **Apply** on a suggested action.

### Docker (optional)

The backend API can be containerised (the WPF client is a Windows desktop app and runs on the
host). From the solution root:

```bash
docker build -t smartx-api -f SmartX.Api/Dockerfile .
docker run -p 5101:8080 smartx-api      # API on http://localhost:5101
```

---

## Part 3 — Network Topology & Mesh Routing

Part 3 unlocks the **Network Topology & Mesh Routing** pillar, completing the suite. Open it
from the landing page or the left nav. The page visualises the mesh as a graph and lets you
trace routes, run traversals, and query historical telemetry.

### Advanced data structures (Part 3 rubric)

| Structure | File | Role |
|-----------|------|------|
| **AVL tree** (self-balancing BST) | `SmartX.Core/TelemetryAvlTree.cs` | Stores timestamped telemetry; answers time-range queries in O(log n + k) instead of scanning a list. |
| **Binary min-heap** | `SmartX.Core/MinHeap.cs` | Priority structure that drives Prim's MST (cheapest frontier edge first). |
| **Graph** (adjacency list) | `SmartX.Core/MeshGraph.cs` | Models the mesh: vertices = sensors/gateway, edges = weighted wireless links. |
| **BFS / DFS** | same | Finds which nodes are reachable from the gateway and which are down. |
| **MST — Prim's & Kruskal's** | same | Computes the most power-efficient routing tree (Prim via the min-heap, Kruskal via union-find). |
| **MST routing** | same (`RouteToGateway`) | Traces a device's hop path to the gateway through the MST. |

### Network Topology page (what it does)

- **Mesh graph** drawn on a canvas — gateway at the centre, sensors around it, weighted links.
- **Trace route**: pick a node (or click it) to highlight its power-efficient path to the gateway.
- **BFS / DFS**: colours reachable nodes and flags down/unreachable ones.
- **Show MST**: highlights the minimum spanning tree and its total routing cost.
- **Historical log**: an AVL **range query** returns a device's readings for the last 10–60 min, in time order.

### Part 3 API endpoints

| Method & path | Purpose |
|---------------|---------|
| `GET /api/mesh/graph` | Full graph: nodes (with layout), weighted edges, MST flags + total weight. |
| `GET /api/mesh/traverse?kind=bfs\|dfs&start=` | BFS/DFS from a node (default gateway); returns order + unreachable. |
| `GET /api/mesh/mst?algorithm=prim\|kruskal` | MST edges + total weight (both algorithms agree). |
| `GET /api/mesh/route?device=` | Power-efficient hop path from a device to the gateway. |
| `GET /api/mesh/history?device=&from=&to=` | AVL range query of a device's history (ISO-8601 times). |
| `GET /api/mesh/history/stats?device=` | Node count + tree height (shows the AVL stays balanced). |

### How to test the Part 3 data structures

With the API running (`dotnet run --project SmartX.Api --urls http://localhost:5101`):

```bash
# Graph + MST: note the down node and that Prim and Kruskal give the SAME total weight.
curl http://localhost:5101/api/mesh/graph
curl "http://localhost:5101/api/mesh/mst?algorithm=prim"
curl "http://localhost:5101/api/mesh/mst?algorithm=kruskal"

# BFS from the gateway: the down node (…2D:03) is reported unreachable.
curl "http://localhost:5101/api/mesh/traverse?kind=bfs"

# Power-efficient route from a node to the gateway (through the MST):
curl "http://localhost:5101/api/mesh/route?device=3C:71:BF:0A:2D:07"

# AVL range query + balance proof (height stays ~log2(count)):
curl "http://localhost:5101/api/mesh/history/stats?device=3C:71:BF:0A:2D:05"
curl "http://localhost:5101/api/mesh/history?device=3C:71:BF:0A:2D:05&from=$(date -u -d '-20 min' +%Y-%m-%dT%H:%M:%SZ)"
```

In the desktop client the same flows are on the **Network Topology & Mesh** page: click a node
to trace its route, use **Show MST** / **BFS** / **DFS**, and run a **historical range query**.

A full written **Implementation & Project Completion report** (with data-structure explanations,
Task 3 challenges and technology recommendations) is in
[`docs/PROG7312_PoE_Report.pdf`](docs/PROG7312_PoE_Report.pdf), and the iterative-improvement
ledger is in [`docs/UPDATES_LEDGER.md`](docs/UPDATES_LEDGER.md).

---

## Referencing

The code is the author's own work. Each source file carries a header block listing the
documentation and texts consulted for the techniques it uses (Harvard style). A
consolidated list of all sources is in [`docs/CODE_REFERENCES.md`](docs/CODE_REFERENCES.md).

---

*PROG7312 Portfolio of Evidence · Final PoE · Smart-X Operational Intelligence Suite (all three pillars).*
