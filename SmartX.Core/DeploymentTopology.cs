// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Core/DeploymentTopology.cs
// Purpose: Deployment-tree model and recursive topology validator.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Sedgewick, R. and Wayne, K., 2011. Algorithms. 4th ed. Boston: Addison-Wesley.
//   - Microsoft, 2024. List<T> Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1 [Accessed 10 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
namespace SmartX.Core;

/// <summary>
/// A node in a multi-tier deployment tree, e.g.
/// <c>Facility A -&gt; Zone 1 -&gt; Sub-Zone B -&gt; [esp32 device]</c>.
/// Branch nodes group the topology; leaf nodes carry a device MAC address.
/// </summary>
public sealed class DeploymentNode
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Populated only on leaf nodes (actual devices). Null for grouping tiers.</summary>
    public string? DeviceMac { get; set; }

    public List<DeploymentNode> Children { get; set; } = new();

    public bool IsLeaf => Children.Count == 0;

    public DeploymentNode() { }
    public DeploymentNode(string name, string? deviceMac = null)
    {
        Name = name;
        DeviceMac = deviceMac;
    }

    public DeploymentNode Add(DeploymentNode child)
    {
        Children.Add(child);
        return this;
    }
}

/// <summary>Outcome of a recursive validation pass over a deployment tree.</summary>
public sealed class TopologyValidationResult
{
    public bool IsValid { get; set; }
    public int DeviceCount { get; set; }
    public int MaxDepth { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> ValidatedPaths { get; set; } = new();
}

/// <summary>
/// Recursive validator for nested device deployment trees. It parses arbitrarily deep
/// multi-tier configuration profiles (Sub-Zone B -&gt; Zone 1 -&gt; Facility A) and confirms
/// each node is safely configured. All traversal is done with recursion.
/// </summary>
public static class TopologyValidator
{
    private const int MaxAllowedDepth = 8; // guards against runaway / cyclic configs

    public static TopologyValidationResult Validate(DeploymentNode root)
    {
        var result = new TopologyValidationResult { IsValid = true };
        var seenMacs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Walk(root, depth: 1, pathPrefix: "", result, seenMacs);
        result.IsValid = result.Issues.Count == 0;
        return result;
    }

    // The recursive step: validate this node, then recurse into every child.
    private static void Walk(DeploymentNode node, int depth, string pathPrefix,
        TopologyValidationResult result, HashSet<string> seenMacs)
    {
        string path = string.IsNullOrEmpty(pathPrefix) ? node.Name : $"{pathPrefix} -> {node.Name}";

        if (depth > result.MaxDepth) result.MaxDepth = depth;

        if (string.IsNullOrWhiteSpace(node.Name))
            result.Issues.Add($"[depth {depth}] A node has an empty name (path: {path}).");

        if (depth > MaxAllowedDepth)
            result.Issues.Add($"Configuration nested too deep (> {MaxAllowedDepth} tiers) at '{path}'.");

        if (node.IsLeaf)
        {
            // A leaf must be a real device with a unique, well-formed MAC.
            if (string.IsNullOrWhiteSpace(node.DeviceMac))
            {
                result.Issues.Add($"Leaf '{path}' has no device MAC address.");
            }
            else if (!MacAddress.IsValid(node.DeviceMac))
            {
                result.Issues.Add($"Leaf '{path}' has an invalid MAC '{node.DeviceMac}'.");
            }
            else if (!seenMacs.Add(node.DeviceMac))
            {
                result.Issues.Add($"Duplicate device MAC '{node.DeviceMac}' detected at '{path}'.");
            }
            else
            {
                result.DeviceCount++;
                result.ValidatedPaths.Add($"{path} [{node.DeviceMac}]");
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(node.DeviceMac))
                result.Issues.Add($"Branch '{path}' should not carry a device MAC (only leaves do).");

            // Recurse into every child tier.
            foreach (var child in node.Children)
                Walk(child, depth + 1, path, result, seenMacs);
        }
    }

    /// <summary>Recursively counts the physical devices (leaves) under a node.</summary>
    public static int CountDevices(DeploymentNode node)
    {
        if (node.IsLeaf)
            return string.IsNullOrWhiteSpace(node.DeviceMac) ? 0 : 1;

        int total = 0;
        foreach (var child in node.Children)
            total += CountDevices(child); // recursive descent
        return total;
    }
}
