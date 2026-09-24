// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Core/MacAddress.cs
// Purpose: MAC-address validation and normalisation via a source-generated regex.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. .NET regular expressions. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions [Accessed 10 September 2026].
//   - Microsoft, 2024. GeneratedRegexAttribute Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.generatedregexattribute [Accessed 10 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using System.Text.RegularExpressions;

namespace SmartX.Core;

/// <summary>Validation + normalisation helpers for device MAC addresses / unique identifiers.</summary>
public static partial class MacAddress
{
    // Accepts 6 hex octets separated by ':' or '-', e.g. 3C:71:BF:0A:2D:9E
    [GeneratedRegex(@"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$")]
    private static partial Regex MacRegex();

    public static bool IsValid(string? mac)
        => !string.IsNullOrWhiteSpace(mac) && MacRegex().IsMatch(mac.Trim());

    /// <summary>Uppercases and standardises separators to ':' for consistent storage/keys.</summary>
    public static string Normalise(string mac)
        => mac.Trim().ToUpperInvariant().Replace('-', ':');
}
