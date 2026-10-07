# Iterative Updates & Feedback Ledger — Smart-X (PROG7312)

**Author:** Lesego Masike (ST10058999)

This ledger tracks the changes and refinements made to the application across the three
submissions, in line with the "iterative feedback incorporation" requirement. Each entry lists
the trigger (self-review/testing, or lecturer feedback), the change, and the file(s) affected.

> Where lecturer feedback for Parts 1 and 2 is received, add it under **Lecturer feedback** below
> and record the resulting change in the table.

## Part 1 → Part 2 refinements

| # | Trigger | Change made | Affected files |
|---|---------|-------------|----------------|
| 1 | Self-review (dark-theme testing) | The default WPF `ComboBox` template ignored the dark foreground, so selected text was invisible. Retemplated the `ComboBox`/`ComboBoxItem` for the dark theme. | `SmartX.Client/Theme.xaml` |
| 2 | Runtime testing | The client could close silently on an unhandled exception. Added global dispatcher/AppDomain exception logging so errors surface and write a crash log. | `SmartX.Client/App.xaml.cs` |
| 3 | UX testing | On smaller windows the command-stream columns collapsed and text wrapped one character per line. Set the window to open **maximized** and added minimum column widths. | `SmartX.Client/MainWindow.xaml`, `Views/CommandStreamView.xaml` |
| 4 | Data-quality review | The seeded history filled the FIFO queue with 400 items, making the "backlog" misleading. The seeder now **drains** processed history so the queue shows only the live backlog. | `SmartX.Api/Services/CommandStreamSeeder.cs` |
| 5 | Consistency | Standardised on a shared `SmartXApiClient` (partial class) so Part 2's calls reuse Part 1's JSON options and error handling rather than duplicating them. | `SmartX.Client/Services/SmartXApiClient*.cs` |

## Part 2 → Part 3 refinements

| # | Trigger | Change made | Affected files |
|---|---------|-------------|----------------|
| 6 | Requirement review | Verified navigation preserves state by **reusing one instance** of each view; extended the same pattern to the new Topology view so all three modules keep their state. | `SmartX.Client/MainWindow.xaml.cs` |
| 7 | Correctness testing | Cross-checked the MST by implementing **both** Prim's and Kruskal's and asserting they return the same total weight (237.6) on the seeded mesh. | `SmartX.Core/MeshGraph.cs` |
| 8 | Efficiency review | The historical query originally implied a linear scan. Replaced it with a self-balancing **AVL tree** whose range query prunes whole subtrees (O(log n + k)); confirmed the tree stays balanced (60 items → height 6). | `SmartX.Core/TelemetryAvlTree.cs`, `SmartX.Api/Services/TopologyEngine.cs` |
| 9 | Robustness | Down/disconnected nodes must not be routed through. BFS/DFS and MST now skip down vertices, and routing reports a node as unreachable instead of returning a broken path. | `SmartX.Core/MeshGraph.cs` |
| 10 | Documentation | Added Harvard reference headers to **every** source file and a consolidated `docs/CODE_REFERENCES.md`; expanded the README with per-part "how to test" sections. | all `*.cs`/`*.xaml`, `docs/CODE_REFERENCES.md`, `README.md` |

## Lecturer feedback

_Record any feedback received on Parts 1 and 2 here, and link it to the change made:_

- Part 1 feedback: _…_
- Part 2 feedback: _…_
