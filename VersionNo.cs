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

            return version ?? "7.3.8";
        }
    }
}
/*Version history:
 *
 *: v8.4.11: Had to change the print selection for windows for .net 10 as the old way no longer works. mdail 11-28-25
 *: v8.4.10:Upgrade for .Net 10 & MAUI 14.0, also updated the libraries to the latest version that supports .Net 10. mdail 11-18-25
 *: v7.3.9: Edit the white color to make them use Hex numbers instead of named colors to avoid problems on 
 *          some platforms * dark mode  mdail 10-30-25
 *: v7.3.8: Modify the framework to require a pdf file name or path to a pdf file the is either compressed to a gz 
 *          or uncompressed pdf file. It requires the file name only or a full path to the file as long as the file is
 *          located in the app package or local storage of the app or the App Context Base Directory or local directory. 
 *          It also take a Model with data to be used to fill in form data or no model to just open the form. mdail 10-24-25
 *: v6.2.7: Started to add the framework to the Award quick app. mdail 10-23-25
 *: v6.2.6: Fix problems that Mac found that the Windows system did not. mdail 10-23-25
 *: v6.2.5: Added a way to only email form, and figured out to open the pdf without filling it in I only need to set the data model to null.
 *          I need to look at the email only option though as it only askes for a subject right now. mdail 10-22-25
 *: v6.2.4: Got the print function to work properly on all platforms still need to test it on mac,iOS & Android though. mdail 10-21-25
 *: v6.1.3: I finally got the framework to print the pdf form properly on Windows, I still need to test on other platforms mdail 10-17-25
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