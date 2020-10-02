using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;

namespace SipgateVirtualFax.CLI
{
    public class Options
    {
        [Option('r', "recipient", Required = false, HelpText = "The recipient number, set if you want to send the document")]
        public string? Recipient { get; set; }
        
        [Option('i', "image", Required = false, HelpText = "A path to an image. Will be ignored if a document is provided!")]
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

        public Options()
        {
            Images = new List<string>();
            UseScannerUI = false;
        }
    }
    
    public static class Program
    {
        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>( args)
                .WithParsedAsync(Run);
        }

        private static async Task Run(Options options)
        {
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
                    Console.WriteLine("Scanning a document...");
                    var scanner = new Scanner();
                    paths = scanner.Scan(options.UseScannerUI);
                }
                documentPath = Path.ChangeExtension(paths.First(), "pdf");
                Console.WriteLine("Converting the document into a PDF...");
                ImageToPdfConverter.Convert(paths, documentPath);
            }
            if (options.Recipient != null)
            {
                var faxClient = new SipgateFaxClient(options.Username ?? "", options.Password ?? "");

                var faxlines = faxClient.GetFaxLines().Result;
                if (faxlines.Length == 0)
                {
                    Console.WriteLine("The user has no faxlines available!");
                    return;
                }

                var validFaxlines = faxlines.Where(f => f.CanSend);
                Faxline faxline;
                if (options.Faxline != null)
                {
                    var selectedFaxline = validFaxlines.First(f =>
                        f.Id == options.Faxline ||
                        f.Alias.Equals(options.Faxline, StringComparison.InvariantCultureIgnoreCase));
                    if (selectedFaxline == null)
                    {
                        Console.WriteLine("The requested faxline does not exist or the given user has no access to it!");
                        return;
                    }

                    faxline = selectedFaxline;
                }
                else
                {
                    faxline = validFaxlines.First();
                }

                Console.WriteLine($"Faxline: {faxline}");
                
                using var scheduler = new FaxScheduler(faxClient);
                var fax = scheduler.ScheduleFax(faxline, options.Recipient, documentPath);
                fax.StatusChanged += (sender, status) =>
                {
                    Console.WriteLine($"Status changed: {status}");
                };

                await fax.AwaitCompletion();

                if (fax.FailureCause != null)
                {
                    Console.WriteLine(fax.FailureCause);
                }
            }
            else
            {
                Console.WriteLine($"Nothing to be done with this document: {documentPath}");
            }
        }
    }
}