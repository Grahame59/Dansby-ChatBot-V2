// ZebraPrintSimpleHandler.cs

using System.Text.Json;
using System.Diagnostics;
using Dansby.Shared;
using Microsoft.Extensions.Logging;

namespace Pipes.Devices.ZebraPrinter;

/// <summary>
/// Version 0.1 handler that takes a simple label text payload
/// and sends it to the Zebra ZD410 as ZPL.
/// </summary>
public sealed class ZebraPrintSimpleHandler : IIntentHandler
{
    public string Name => "zebra.print.simple";

    // Dependency Injection
    private readonly ILogger<ZebraPrintSimpleHandler> _log;

    public ZebraPrintSimpleHandler(ILogger<ZebraPrintSimpleHandler> log)
    {
        _log = log;
    }

    public async Task<HandlerResult> HandleAsync(JsonElement payload, string corr, CancellationToken ct)
    {
        // 1) Validate payload.labelText
        if (!payload.TryGetProperty("labelText", out var lt) || lt.ValueKind != JsonValueKind.String)
        {
            return HandlerResult.Fail("BAD_INPUT", "payload.labeltext (string) required.");
        }

        // Extract String When Successfully Validated
        var labelText = lt.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(labelText))
        {
            return HandlerResult.Fail("BAD_INPUT", "payload.labelText cannot be empty");
        }

        _log.LogInformation("Zebra handler received label text: {Text} (corr={Corr})", labelText, corr);

        // 2) Building the ZPL Format (0.1 formatting as its very simple.)
        var zpl = "^XA\n" +                 // ^XA = Start Format
                  "^FO40,40\n" +            // ^FO = Positioning the element at x,y
                  "^A0N, 40, 40\n" +        // ^A0N = Font 0, normal orientation, height,width
                  $"^FD{labelText}^FS\n" +  // ^FD is starts field data, what is being printed & ^FS ends the field
                  "^XZ\n";                  // ^XZ = end label format

        _log.LogDebug("Zebra ZPL generated corr={Corr}: {Zpl}", corr, zpl);

        // 3) Call `lp -d zebra1` and send ZPL on stdin
        var psi = new ProcessStartInfo
        {
            FileName = "lp",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        // Printer queue name configured on the server ("zebra1" from earlier setup)
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add("zebra1");

        using var proc = new Process { StartInfo = psi };

        try
        {
            proc.Start();

            // Write ZPL to stdin
            await proc.StandardInput.WriteAsync(zpl.AsMemory(), ct);
            proc.StandardInput.Close();

            // Wait for process to exit
            await proc.WaitForExitAsync(ct);

            var exitCode = proc.ExitCode;
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);

            if (exitCode == 0)
            {
                _log.LogInformation(
                    "Zebra print succeeded corr={Corr} exitCode={Code} stdout={Out}",
                    corr, exitCode, stdout
                );

                return HandlerResult.Success(new
                {
                    printed = true,
                    labelText,
                    exitCode,
                    stdout,
                    stderr
                });
            }
            else
            {
                _log.LogWarning(
                    "Zebra print failed corr={Corr} exitCode={Code} stderr={Err}",
                    corr, exitCode, stderr
                );

                return HandlerResult.Fail(
                    "PRINT_FAILED",
                    $"lp exited with code {exitCode}: {stderr}"
                );
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception while printing label corr={Corr}", corr);
            return HandlerResult.Fail("PRINT_EXCEPTION", ex.Message);
        }
    }
}