using System.Resources;
using System.Reflection;
using System.Globalization;
using System.Threading;

namespace EasySave.Console.Resources
{
    /// <summary>
    /// This class handles language management.
    /// </summary>
    public class LangHelper
    {
        private static ResourceManager _rm;

        static LangHelper()
        {
            _rm = new ResourceManager("ConsoleApp1.Resources.Strings", Assembly.GetExecutingAssembly());
        }

        public static string? GetString(string name)
        {
            return _rm.GetString(name);
        }

        public static void ChangeLanguage(string lang)
        {
            var cultureInfo = new CultureInfo(lang);
        
            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
    
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
        }
    }   
}