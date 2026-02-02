// ZebraPrintMailerFromCsvHandler.cs

using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Dansby.Shared;
using Microsoft.Extensions.Logging;

namespace Pipes.Devices.ZebraPrinter;

/// <summary>
/// Intent handler for <c>zebra.print.mailer.from_csv</c>.
///
/// Reads a Google Sheets-exported CSV (either passed inline as csvText or via csvPath),
/// parses recipient data, derives NJ ZIP code from a town map, and prints mailing labels
/// to Zebra ZD410 via <c>lp -d zebra1</c>.
///
/// Payload:
/// - csvText (string) OR csvPath (string)
/// - printReturnLabel (bool, default true)
/// - splitReturnToSeparateLabel (bool, default false)
/// - hasHeader (bool, default true)
///
/// Columns (case-insensitive, flexible names):
/// - first_name|fname|first
/// - last_name|lname|last
/// - street_address|address|address1
/// - town|city
///
/// Emits:
/// - zebra.print.success on full success
/// - zebra.print.failed if printing fails (or no valid rows)
/// </summary>
public sealed class ZebraPrintMailerFromCsvHandler : IIntentHandler
{
    public string Name => "zebra.print.mailer.from_csv";

    private readonly ILogger<ZebraPrintMailerFromCsvHandler> _log;
    private readonly IIntentQueue _queue;

    public ZebraPrintMailerFromCsvHandler(
        ILogger<ZebraPrintMailerFromCsvHandler> log,
        IIntentQueue queue)
    {
        _log = log;
        _queue = queue;
    }

    // Return address (constant for now)
    private static readonly string[] ReturnAddressLines =
    {
        "Bankers Life",
        "6 Century Drive, Suite 190",
        "Parsippany, New Jersey 07054"
    };

    // Town -> Primary ZIP mapping (Passaic County campaign)
    // Add/adjust as you expand towns.
    private static readonly Dictionary<string, string> ZipByTown =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["West Milford"] = "07480",
            ["Hewitt"]       = "07421",
            ["Oak Ridge"]    = "07438",
            ["Wanaque"]      = "07465",
            ["Wayne"]        = "07470",
            ["Butler"]       = "07405",
        };

    public async Task<HandlerResult> HandleAsync(JsonElement payload, string corr, CancellationToken ct)
    {
        // 1) Read CSV content (csvText or csvPath)
        var csv = ReadCsvFromPayload(payload, out var csvSourceError);
        if (csv is null)
            return HandlerResult.Fail("BAD_INPUT", csvSourceError ?? "CSV input required.");

        bool printReturnLabel = GetBool(payload, "printReturnLabel", defaultValue: true);
        bool splitReturnToSeparateLabel = GetBool(payload, "splitReturnToSeparateLabel", defaultValue: false);
        bool hasHeader = GetBool(payload, "hasHeader", defaultValue: true);

        // 2) Parse CSV rows
        List<string[]> rows;
        try
        {
            rows = CsvParser.Parse(csv);
        }
        catch (Exception ex)
        {
            return HandlerResult.Fail("CSV_PARSE_FAILED", ex.Message);
        }

        if (rows.Count == 0)
            return HandlerResult.Fail("NO_ROWS", "CSV contained no rows.");

        // 3) Resolve column indexes (header-based if present, else default positions)
        int idxFirst, idxLast, idxAddr, idxTown;
        int startRow = 0;

        if (hasHeader)
        {
            var header = rows[0].Select(h => (h ?? string.Empty).Trim()).ToArray();
            (idxFirst, idxLast, idxAddr, idxTown) = ResolveHeaderIndexes(header);

            startRow = 1;
        }
        else
        {
            // If no header, assume fixed positions:
            // 0=first, 1=last, 2=address, 3=town
            idxFirst = 0;
            idxLast  = 1;
            idxAddr  = 2;
            idxTown  = 3;
        }

        // Validate indexes
        if (idxFirst < 0 || idxLast < 0 || idxAddr < 0 || idxTown < 0)
        {
            return HandlerResult.Fail(
                "BAD_INPUT",
                "Could not find required columns. Expected header columns like: first_name,last_name,street_address,town."
            );
        }

        // 4) Build ZPL for all labels in one print job
        var zplBuilder = new StringBuilder();
        int printedCount = 0;
        var skipped = new List<object>();

        for (int r = startRow; r < rows.Count; r++)
        {
            var row = rows[r];
            // Safely fetch columns even if row is short
            string first = GetCell(row, idxFirst);
            string last  = GetCell(row, idxLast);
            string addr  = GetCell(row, idxAddr);
            string town  = GetCell(row, idxTown);

            // Minimal validation
            if (string.IsNullOrWhiteSpace(addr) || string.IsNullOrWhiteSpace(town))
            {
                skipped.Add(new { row = r + 1, reason = "Missing address or town." });
                continue;
            }

            string fullName = BuildNameLine(first, last); // "Current Resident" if empty
            string normTown = NormalizeTown(town);

            if (!ZipByTown.TryGetValue(normTown, out var zip))
            {
                skipped.Add(new { row = r + 1, reason = $"Unknown town '{town}' (normalized '{normTown}'). Add mapping.", town = town });
                continue;
            }

            // Recipient lines
            // Street address already may include commas + apt; print as-is.
            var recipientLines = new List<string>
            {
                fullName,
                addr,
                $"{normTown}, NJ {zip}"
            };

            // Decide printing mode
            if (printReturnLabel && splitReturnToSeparateLabel)
            {
                zplBuilder.Append(BuildLabelZpl(ReturnAddressLines, isReturn: true));
                zplBuilder.Append(BuildLabelZpl(recipientLines.ToArray(), isReturn: false));
                printedCount += 2;
            }
            else
            {
                // Combined label (return + recipient) if enabled
                zplBuilder.Append(BuildMailerLabelZpl(
                    returnLines: printReturnLabel ? ReturnAddressLines : null,
                    recipientLines: recipientLines.ToArray()
                ));
                printedCount += 1;
            }
        }

        if (printedCount == 0)
        {
            var msg = "No valid rows to print. All rows were skipped.";
            _log.LogWarning("{Msg} corr={Corr} skippedCount={SkipCount}", msg, corr, skipped.Count);

            var failEnv = EnvelopeFactory.ForIntent(
                intent: "zebra.print.failed",
                payloadObj: new { message = msg, skipped },
                corr: corr
            );

            _queue.Enqueue(failEnv);
            return HandlerResult.Fail("NO_VALID_ROWS", msg);
        }

        var zpl = zplBuilder.ToString();
        _log.LogInformation(
            "Prepared ZPL for mailer labels corr={Corr}: printedCount={Printed} skipped={Skipped}",
            corr, printedCount, skipped.Count
        );

        // 5) Print via lp -d zebra1
        var printResult = await PrintViaLpAsync(zpl, corr, ct);

        if (printResult.Success)
        {
            var successEnv = EnvelopeFactory.ForIntent(
                intent: "zebra.print.success",
                payloadObj: new
                {
                    printedCount,
                    skippedCount = skipped.Count,
                    skipped,
                    exitCode = printResult.ExitCode,
                    stdout = printResult.Stdout
                },
                corr: corr
            );

            _queue.Enqueue(successEnv);
            return HandlerResult.Success(new { printed = true, printedCount, skippedCount = skipped.Count });
        }
        else
        {
            var failEnv = EnvelopeFactory.ForIntent(
                intent: "zebra.print.failed",
                payloadObj: new
                {
                    printedCount,
                    skippedCount = skipped.Count,
                    skipped,
                    exitCode = printResult.ExitCode,
                    stderr = printResult.Stderr
                },
                corr: corr
            );

            _queue.Enqueue(failEnv);
            return HandlerResult.Fail("PRINT_FAILED", printResult.Stderr ?? "Unknown printing error.");
        }
    }

    private static string? ReadCsvFromPayload(JsonElement payload, out string? error)
    {
        error = null;

        if (payload.TryGetProperty("csvText", out var csvTextEl) && csvTextEl.ValueKind == JsonValueKind.String)
        {
            var s = csvTextEl.GetString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
            error = "payload.csvText was provided but empty.";
            return null;
        }

        if (payload.TryGetProperty("csvPath", out var csvPathEl) && csvPathEl.ValueKind == JsonValueKind.String)
        {
            var path = csvPathEl.GetString();
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "payload.csvPath was provided but empty.";
                return null;
            }

            if (!File.Exists(path))
            {
                error = $"CSV file not found at path: {path}";
                return null;
            }

            return File.ReadAllText(path);
        }

        error = "Provide payload.csvText (string) or payload.csvPath (string).";
        return null;
    }

    private static bool GetBool(JsonElement payload, string propName, bool defaultValue)
    {
        if (payload.TryGetProperty(propName, out var el))
        {
            if (el.ValueKind == JsonValueKind.True) return true;
            if (el.ValueKind == JsonValueKind.False) return false;
        }
        return defaultValue;
    }

    private static (int idxFirst, int idxLast, int idxAddr, int idxTown) ResolveHeaderIndexes(string[] header)
    {
        // Normalize header names
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Length; i++)
        {
            var name = (header[i] ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(name) && !map.ContainsKey(name))
                map[name] = i;
        }

        int Find(params string[] names)
        {
            foreach (var n in names)
            {
                if (map.TryGetValue(n, out var idx))
                    return idx;
            }
            return -1;
        }

        int idxFirst = Find("first_name", "fname", "first", "firstname", "First Name");
        int idxLast  = Find("last_name", "lname", "last", "lastname", "Last Name");
        int idxAddr  = Find("street_address", "address", "address1", "street", "Street Address");
        int idxTown  = Find("town", "city", "Town");

        return (idxFirst, idxLast, idxAddr, idxTown);
    }

    private static string GetCell(string[] row, int idx)
    {
        if (idx < 0 || idx >= row.Length) return string.Empty;
        return (row[idx] ?? string.Empty).Trim();
    }

    private static string BuildNameLine(string first, string last)
    {
        var f = (first ?? "").Trim();
        var l = (last ?? "").Trim();

        var full = $"{f} {l}".Trim();
        return string.IsNullOrWhiteSpace(full) ? "Current Resident" : full;
    }

    private static string NormalizeTown(string town)
    {
        // light normalization: trim + collapse spaces
        var t = (town ?? "").Trim();
        t = string.Join(" ", t.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        // Keep user casing? For matching, casing doesn't matter in dictionary.
        // But we want consistent printing:
        // Title-case-ish without changing acronyms.
        // Simple approach:
        return ToTitleCaseInvariant(t);
    }

    private static string ToTitleCaseInvariant(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p.Length == 1) parts[i] = p.ToUpperInvariant();
            else parts[i] = char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant();
        }
        return string.Join(" ", parts);
    }

    // --- ZPL builders -------------------------------------------------------

    /// <summary>
    /// Builds a simple label with up to ~3-4 lines, used for return label mode.
    /// </summary>
    private static string BuildLabelZpl(string[] lines, bool isReturn)
    {
        // NOTE: ZD410 label sizes vary. These coordinates are a sane starter.
        // You will likely tune FO and font sizes after a test print.
        var sb = new StringBuilder();

        sb.Append("^XA\n");
        sb.Append("^CI28\n"); // UTF-8 (generally helpful; depends on printer config)

        int x = 30;
        int y = 25;

        // Smaller font for return address
        int fontH = isReturn ? 24 : 30;
        int fontW = isReturn ? 24 : 30;
        int lineGap = isReturn ? 28 : 34;

        for (int i = 0; i < lines.Length; i++)
        {
            var text = EscapeZpl(lines[i]);
            sb.Append($"^FO{x},{y + (i * lineGap)}\n");
            sb.Append($"^A0N,{fontH},{fontW}\n");
            sb.Append($"^FD{text}^FS\n");
        }

        sb.Append("^XZ\n");
        return sb.ToString();
    }

    /// <summary>
    /// Builds a combined mailer label:
    /// - Return address (small) top-left (optional)
    /// - Recipient block (larger) below
    /// </summary>
    private static string BuildMailerLabelZpl(string[]? returnLines, string[] recipientLines)
    {
        var sb = new StringBuilder();

        sb.Append("^XA\n");
        sb.Append("^CI28\n");

        int x = 30;
        int y = 20;

        // Return block (small)
        if (returnLines is not null && returnLines.Length > 0)
        {
            int retFontH = 22;
            int retFontW = 22;
            int retGap = 26;

            for (int i = 0; i < returnLines.Length; i++)
            {
                var text = EscapeZpl(returnLines[i]);
                sb.Append($"^FO{x},{y + (i * retGap)}\n");
                sb.Append($"^A0N,{retFontH},{retFontW}\n");
                sb.Append($"^FD{text}^FS\n");
            }

            // Leave space before recipient block
            y += (returnLines.Length * retGap) + 18;
        }

        // Recipient block (bigger)
        int recFontH = 30;
        int recFontW = 30;
        int recGap = 34;

        for (int i = 0; i < recipientLines.Length; i++)
        {
            var text = EscapeZpl(recipientLines[i]);
            sb.Append($"^FO{x},{y + (i * recGap)}\n");
            sb.Append($"^A0N,{recFontH},{recFontW}\n");
            sb.Append($"^FD{text}^FS\n");
        }

        sb.Append("^XZ\n");
        return sb.ToString();
    }

    /// <summary>
    /// Basic ZPL escaping to avoid command injection / broken labels.
    /// For production, you may want a stricter whitelist.
    /// </summary>
    private static string EscapeZpl(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        // Minimal safety: remove characters that can break ZPL commands.
        // - '^' starts commands
        // - '~' can introduce control sequences
        // If you *need* these characters literally printed, we can switch to hex encoding (^FH).
        return s.Replace("^", " ").Replace("~", " ");
    }

    // --- Printing -----------------------------------------------------------

    private sealed record PrintResult(bool Success, int ExitCode, string? Stdout, string? Stderr);

    private async Task<PrintResult> PrintViaLpAsync(string zpl, string corr, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "lp",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add("zebra1");

        using var proc = new Process { StartInfo = psi };

        try
        {
            proc.Start();

            await proc.StandardInput.WriteAsync(zpl.AsMemory(), ct);
            proc.StandardInput.Close();

            await proc.WaitForExitAsync(ct);

            var exitCode = proc.ExitCode;
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);

            if (exitCode == 0)
            {
                _log.LogInformation("Zebra bulk print succeeded corr={Corr} stdout={Out}", corr, stdout);
                return new PrintResult(true, exitCode, stdout, null);
            }

            _log.LogWarning("Zebra bulk print failed corr={Corr} exitCode={Code} stderr={Err}", corr, exitCode, stderr);
            return new PrintResult(false, exitCode, stdout, stderr);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception while printing bulk labels corr={Corr}", corr);
            return new PrintResult(false, -1, null, ex.Message);
        }
    }

    // --- CSV Parser (handles quoted commas) --------------------------------
    private static class CsvParser
    {
        public static List<string[]> Parse(string csv)
        {
            // Robust-enough CSV parsing for:
            // - quoted fields
            // - commas inside quotes
            // - CRLF/LF newlines
            //
            // Not intended for every edge case (like embedded newlines inside quotes),
            // but Google Sheets exports for addresses usually behave well.
            var rows = new List<string[]>();
            var currentRow = new List<string>();
            var field = new StringBuilder();

            bool inQuotes = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Double quote inside quoted field -> literal quote
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        currentRow.Add(field.ToString());
                        field.Clear();
                    }
                    else if (c == '\r')
                    {
                        // ignore, handle on \n
                    }
                    else if (c == '\n')
                    {
                        currentRow.Add(field.ToString());
                        field.Clear();

                        // Skip completely empty trailing rows
                        if (currentRow.Count > 1 || (currentRow.Count == 1 && !string.IsNullOrWhiteSpace(currentRow[0])))
                            rows.Add(currentRow.ToArray());

                        currentRow.Clear();
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
            }

            // last line
            if (field.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(field.ToString());
                if (currentRow.Count > 1 || (currentRow.Count == 1 && !string.IsNullOrWhiteSpace(currentRow[0])))
                    rows.Add(currentRow.ToArray());
            }

            return rows;
        }
    }
}
