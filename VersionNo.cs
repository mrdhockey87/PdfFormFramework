using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PdfFormViewerFramework
{
    public class VersionNo
    {
        public VersionNo()
        {

        }
        public static string GetFrameworkVersion()
        {
            // This will get the assembly containing this class
            var assembly = Assembly.GetExecutingAssembly();

            // Try to get the informational version first (most detailed)
            var infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (infoVersionAttr != null)
                return infoVersionAttr.InformationalVersion;

            // Fallback to AssemblyVersion
            var version = assembly.GetName().Version?.ToString();

            return version ?? "4.3.5";
        }
    }
}
/*Version history:
 *
 *: v5.0.2: I got the form to display properly so the basic framework is working. The save function is working however I need to give it a way to change the 
 *                   file name. The print as if I want to share the document, I need to fix that so it opens the print function of the system it is on. mdail 10-16-25
 *  10-15-25:v5.0.1: Continued to work on getting the form to display properly. And the Model to be correct, Added a VIew Model to the MainPage 
 *                   to help get it all to work mdail 10-15-25
 *  10-9-25: v5.0.0: Started to convert to displaying a form to collect data to fill the form. Then sending the data to the framework
 *                   so the framework can fill in the data and display a filled in form  mdail 10-14-25
 *  10-9-25: v4.3.6: It actually showed the form & fields however the field were below the form and it took far too long to load. mdail 10-9-25
 *  10-9-25: v4.3.5: Finally got it to work and the fields are being set and gotten properly. mdail 10-9-25
 *  10-9-25: v3.2.3: Had the Agent fix the PdfFieldService to handle all of the fields get them and set them properly.mdail 10-8-25
 *  10-8-25: v2.0.2.2: Changed to using PdfSharp instead of PdfSharpCore, also changed the PdfInteractiveFormView class a lot
 *                     to get the fields from the form. mdail 10-8-25
 *  9-30-25: v1.0.1.0: Set the starting version to 1.0.1.0 mdail
 *  9-30-25: v0.0.1: Initial version. framework to add hybrid MAUI/Blazor pdf form viewer to MAUI apps.
 */