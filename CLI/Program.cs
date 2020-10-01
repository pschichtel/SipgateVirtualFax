using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SipgateVirtualFax.Core;

namespace SipgateVirtualFax.CLI
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            IEnumerable<string> paths;
            if (args.Length > 0)
            {
                paths = args;
            }
            else
            {
                paths = Scanner.Scan(false);
            }
            var targetPath = Path.ChangeExtension(paths.First(), "pdf");
            ImageToPdfConverter.Convert(paths, targetPath);
            Console.WriteLine(targetPath);
        }
    }
}