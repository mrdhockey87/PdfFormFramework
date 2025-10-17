using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfFormFramework.Printing
{
    public partial class PdfPrinterHelper
    {
        public static Task PrintOrEmailAsync(string filePath) =>
            PlatformPrintOrEmailAsync(filePath);

        static public partial Task PlatformPrintOrEmailAsync(string filePath);
    }
}
