using System;
using System.Linq;
using SipgateVirtualFax.Core;

namespace SipgateVirtualFax.CLI
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var pages = Scanner.Scan(false);
            var firstPage = pages.First();
            var targetPath = $"{firstPage}.pdf";
            ImageToPdfConverter.Convert(firstPage, targetPath);
            Console.WriteLine(firstPage);
            Console.WriteLine(targetPath);
        }
    }
}