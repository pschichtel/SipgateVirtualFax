using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxCli;

public class Options
{
    [Option('r', "recipient", Required = false,
        HelpText = "The recipient number, set if you want to send the document")]
    public string? Recipient { get; set; }

    [Option('i', "image", Required = false, Separator = ';',
        HelpText = "A path to an image. Will be ignored if a document is provided!")]
    public IList<string> Images { get; set; }

    [Option('g', "use-scanner-gui", Required = false, HelpText = "Whether to use the scanner UI.")]
    public bool UseScannerUI { get; set; }

    [Option('d', "document", Required = false, HelpText = "The path to a PDF document to be faxed.")]
    public string? DocumentPath { get; set; }

    [Option('u', "username", Required = false, HelpText = "The sipgate username to authenticate for fax.")]
    public string? Username { get; set; }

    [Option('p', "password", Required = false, HelpText = "The sipgate password to authenticate for fax.")]
    public string? Password { get; set; }

    [Option('f', "faxline", Required = false, HelpText = "The sipgate faxline to use.")]
    public string? Faxline { get; set; }

    [Option('s', "scanner", Required = false, HelpText = "The name of the scanner to be used.")]
    public string? Scanner { get; set; }

    public Options()
    {
        Images = new List<string>();
        UseScannerUI = false;
    }
}

public class CliOAuthImplicitFlowHandler : IOAuthImplicitFlowHandler
{
    public Task<string?> GetAccessTokenFromStorage() => Task.FromResult<string?>(null);

    public Task<Uri> Authorize(Uri authorizationUri)
    {
        while (true)
        {
            Console.WriteLine($"Open in Browser: {authorizationUri}");
            var redirectionTarget = ReadLine.Read("Paste redirection target: ")?.Trim();
            if (string.IsNullOrEmpty(redirectionTarget))
            {
                continue;
            }

            return Task.FromResult(new Uri(redirectionTarget));
        }
    }

    public Task StoreAccessToken(string accessToken) => Task.CompletedTask;
}

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine(CultureInfo.InstalledUICulture);
        Console.WriteLine(CultureInfo.CurrentCulture);
        Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(Run);
    }

    private static async Task Run(Options options)
    {
        var logger = Logging.GetLogger("cli-main");
        string documentPath;
        if (options.DocumentPath != null)
        {
            documentPath = options.DocumentPath;
        }
        else
        {
            var paths = options.Images;
            if (paths.Count == 0)
            {
                logger.Info("Scanning a document...");
                var scanner = new Scanner()
                {
                    ShowUi = options.UseScannerUI,
                    ScanBasePath = "."
                };
                if (options.Scanner != null)
                {
                    paths = await scanner.Scan(session => session.First(source =>
                        source.Name.Equals(options.Scanner, StringComparison.InvariantCultureIgnoreCase)));
                }
                else
                {
                    paths = await scanner.ScanWithDefault();
                }
            }

            documentPath = Path.ChangeExtension(paths.First(), "pdf");
            logger.Info("Converting the scans into a PDF...");
            foreach (var path in paths)
            {
                logger.Info($"Scan: {path}");
            }

            ImageToPdfConverter.Convert(paths, documentPath);
        }

        if (options.Recipient != null)
        {
            var basicAuth = new OAuth2ImplicitFlowHeaderProvider(new CliOAuthImplicitFlowHandler());
            var faxClient = new SipgateFaxClient(basicAuth);

            var faxlines = await faxClient.GetAllUsableFaxlines();
            if (faxlines.Length == 0)
            {
                logger.Info("The user has no faxlines available!");
                return;
            }

            var validFaxlines = faxlines.Where(f => f.CanSend);
            Faxline faxline;
            if (options.Faxline != null)
            {
                var selectedFaxline = validFaxlines.FirstOrDefault(f =>
                    f.Id == options.Faxline ||
                    f.Alias.Equals(options.Faxline, StringComparison.InvariantCultureIgnoreCase));
                if (selectedFaxline == null)
                {
                    logger.Info(
                        "The requested faxline does not exist or the given user has no access to it!");
                    return;
                }

                faxline = selectedFaxline;
            }
            else
            {
                faxline = validFaxlines.First();
            }

            logger.Info($"Faxline: {faxline}");

            try
            {
                using var scheduler = new FaxScheduler(faxClient);
                var fax = scheduler.ScheduleFax(faxline, options.Recipient, documentPath);
                var completionSource = new TaskCompletionSource<object?>();
                fax.StatusChanged += (_, status) =>
                {
                    logger.Info($"Status changed: {status}");
                    if (status.IsComplete())
                    {
                        completionSource.SetResult(null);
                    }
                };

                await completionSource.Task;

                if (fax.FailureCause != null)
                {
                    logger.Error(fax.FailureCause, $"Fax to {fax.Recipient} failed to send");
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to send the fax!");
            }
        }
        else
        {
            logger.Info($"Nothing to be done with this document: {documentPath}");
        }
    }
}