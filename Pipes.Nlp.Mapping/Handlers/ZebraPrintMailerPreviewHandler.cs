using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Dansby.Shared;
using Microsoft.Extensions.Logging;

namespace Pipes.Devices.ZebraPrinter;

public sealed class ZebraPrintMailerPreviewHandler : IIntentHandler
{
    public string Name => "zebra.print.mailer.preview";

    private readonly ILogger<ZebraPrintMailerPreviewHandler> _log;

    public ZebraPrintMailerPreviewHandler(ILogger<ZebraPrintMailerPreviewHandler> log)
    {
        _log = log;
    }

    private static readonly string[] ReturnAddressLines =
    {
        "Bankers Life",
        "6 Century Drive, Suite 190",
        "Parsippany, New Jersey 07054"
    };

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
        // Inputs
        var csvText = payload.TryGetProperty("csvText", out var csvEl) && csvEl.ValueKind == JsonValueKind.String
            ? csvEl.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(csvText))
            return HandlerResult.Fail("BAD_INPUT", "payload.csvText (string) required for preview.");

        bool doPrint = GetBool(payload, "doPrint", defaultValue: false);
        bool printReturnLabel = GetBool(payload, "printReturnLabel", defaultValue: true);
        bool splitReturnToSeparateLabel = GetBool(payload, "splitReturnToSeparateLabel", defaultValue: false);
        bool hasHeader = GetBool(payload, "hasHeader", defaultValue: true);
        int maxLabels = GetInt(payload, "maxLabels", defaultValue: 1, min: 1, max: 10);

        // Parse CSV
        var rows = CsvParser.Parse(csvText);
        if (rows.Count == 0)
            return HandlerResult.Fail("NO_ROWS", "CSV contained no rows.");

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
            idxFirst = 0; idxLast = 1; idxAddr = 2; idxTown = 3;
        }

        if (idxFirst < 0 || idxLast < 0 || idxAddr < 0 || idxTown < 0)
            return HandlerResult.Fail("BAD_INPUT", "Missing required columns for preview.");

        // Build up to maxLabels labels ZPL
        var zplBuilder = new StringBuilder();
        int built = 0;
        var skipped = new List<object>();

        for (int r = startRow; r < rows.Count && built < maxLabels; r++)
        {
            var row = rows[r];

            string first = GetCell(row, idxFirst);
            string last  = GetCell(row, idxLast);
            string addr  = GetCell(row, idxAddr);
            string town  = GetCell(row, idxTown);

            if (string.IsNullOrWhiteSpace(addr) || string.IsNullOrWhiteSpace(town))
            {
                skipped.Add(new { row = r + 1, reason = "Missing address or town." });
                continue;
            }

            string name = BuildNameLine(first, last);
            string normTown = NormalizeTown(town);

            if (!ZipByTown.TryGetValue(normTown, out var zip))
            {
                skipped.Add(new { row = r + 1, reason = $"Unknown town '{town}'", town });
                continue;
            }

            var recipientLines = new[]
            {
                name,
                addr,
                $"{normTown}, NJ {zip}"
            };

            if (printReturnLabel && splitReturnToSeparateLabel)
            {
                zplBuilder.Append(BuildLabelZpl(ReturnAddressLines, isReturn: true));
                zplBuilder.Append(BuildLabelZpl(recipientLines, isReturn: false));
            }
            else
            {
                zplBuilder.Append(BuildMailerLabelZpl(
                    returnLines: printReturnLabel ? ReturnAddressLines : null,
                    recipientLines: recipientLines
                ));
            }

            built++;
        }

        var zpl = zplBuilder.ToString();

        // Optionally print
        if (doPrint)
        {
            var print = await PrintViaLpAsync(zpl, corr, ct);
            if (!print.Success)
                return HandlerResult.Fail("PRINT_FAILED", print.Stderr ?? "Unknown printing error.");
        }

        // Return ZPL so your web console can display it
        return HandlerResult.Success(new
        {
            corr,
            builtLabels = built,
            skipped,
            doPrint,
            zpl
        });
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

    private static int GetInt(JsonElement payload, string propName, int defaultValue, int min, int max)
    {
        if (payload.TryGetProperty(propName, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
        return defaultValue;
    }

    private static (int idxFirst, int idxLast, int idxAddr, int idxTown) ResolveHeaderIndexes(string[] header)
    {
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
                if (map.TryGetValue(n, out var idx)) return idx;
            return -1;
        }

        return (
            Find("first_name", "fname", "first", "firstname"),
            Find("last_name", "lname", "last", "lastname"),
            Find("street_address", "address", "address1", "street"),
            Find("town", "city")
        );
    }

    private static string GetCell(string[] row, int idx)
    {
        if (idx < 0 || idx >= row.Length) return string.Empty;
        return (row[idx] ?? string.Empty).Trim();
    }

    private static string BuildNameLine(string first, string last)
    {
        var full = $"{(first ?? "").Trim()} {(last ?? "").Trim()}".Trim();
        return string.IsNullOrWhiteSpace(full) ? "Current Resident" : full;
    }

    private static string NormalizeTown(string town)
    {
        var t = (town ?? "").Trim();
        t = string.Join(" ", t.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return ToTitleCaseInvariant(t);
    }

    private static string ToTitleCaseInvariant(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            parts[i] = p.Length switch
            {
                0 => p,
                1 => p.ToUpperInvariant(),
                _ => char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant()
            };
        }
        return string.Join(" ", parts);
    }

    private static string EscapeZpl(string s)
        => string.IsNullOrEmpty(s) ? "" : s.Replace("^", " ").Replace("~", " ");

    private static string BuildLabelZpl(string[] lines, bool isReturn)
    {
        var sb = new StringBuilder();
        sb.Append("^XA\n^CI28\n");

        int x = 30, y = 25;
        int fontH = isReturn ? 24 : 30;
        int fontW = isReturn ? 24 : 30;
        int gap   = isReturn ? 28 : 34;

        for (int i = 0; i < lines.Length; i++)
        {
            var text = EscapeZpl(lines[i]);
            sb.Append($"^FO{x},{y + (i * gap)}\n");
            sb.Append($"^A0N,{fontH},{fontW}\n");
            sb.Append($"^FD{text}^FS\n");
        }

        sb.Append("^XZ\n");
        return sb.ToString();
    }

    private static string BuildMailerLabelZpl(string[]? returnLines, string[] recipientLines)
    {
        var sb = new StringBuilder();
        sb.Append("^XA\n^CI28\n");

        int x = 30;
        int y = 20;

        if (returnLines is not null && returnLines.Length > 0)
        {
            int retH = 22, retW = 22, retGap = 26;

            for (int i = 0; i < returnLines.Length; i++)
            {
                sb.Append($"^FO{x},{y + (i * retGap)}\n");
                sb.Append($"^A0N,{retH},{retW}\n");
                sb.Append($"^FD{EscapeZpl(returnLines[i])}^FS\n");
            }

            y += (returnLines.Length * retGap) + 18;
        }

        int recH = 30, recW = 30, recGap = 34;
        for (int i = 0; i < recipientLines.Length; i++)
        {
            sb.Append($"^FO{x},{y + (i * recGap)}\n");
            sb.Append($"^A0N,{recH},{recW}\n");
            sb.Append($"^FD{EscapeZpl(recipientLines[i])}^FS\n");
        }

        sb.Append("^XZ\n");
        return sb.ToString();
    }

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

            var exit = proc.ExitCode;
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);

            return exit == 0
                ? new PrintResult(true, exit, stdout, null)
                : new PrintResult(false, exit, stdout, stderr);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Print exception corr={Corr}", corr);
            return new PrintResult(false, -1, null, ex.Message);
        }
    }

    private static class CsvParser
    {
        public static List<string[]> Parse(string csv)
        {
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
                        inQuotes = true;
                    else if (c == ',')
                    {
                        currentRow.Add(field.ToString());
                        field.Clear();
                    }
                    else if (c == '\r') { }
                    else if (c == '\n')
                    {
                        currentRow.Add(field.ToString());
                        field.Clear();

                        if (currentRow.Count > 1 || (currentRow.Count == 1 && !string.IsNullOrWhiteSpace(currentRow[0])))
                            rows.Add(currentRow.ToArray());

                        currentRow.Clear();
                    }
                    else
                        field.Append(c);
                }
            }

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
