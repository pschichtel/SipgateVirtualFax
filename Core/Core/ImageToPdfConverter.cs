using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace SipgateVirtualFax.Core
{
    public static class ImageToPdfConverter
    {
        public static void Convert(string imagePath, string targetPath)
        {
            using var document = new Document();
            document.SetMargins(0, 0, 0, 0);
            PdfWriter.GetInstance(document, new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None));
            document.Open();

            var image = Image.GetInstance(new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            document.Add(image);
        }
    }
}