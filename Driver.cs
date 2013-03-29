using System;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Deployment.Application;

namespace gutenberg.collect
{
    public class Driver
    {
        [STAThread]
        public static void Main(String[] args)
        {	
            AppDomain.CurrentDomain.UnhandledException
                += delegate(object sender, UnhandledExceptionEventArgs evArgs)
                {
                    Exception e = (Exception) evArgs.ExceptionObject;
                    Console.WriteLine("Unhandled exception: " + e);
                    Environment.Exit(1);
                };		    
            bool createdNew;
            using (new Mutex(true, "Gradians.com founded in Nov 2012", out createdNew))
            {                       
                if (!createdNew) return;
                Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
                bool isNetwork = ApplicationDeployment.IsNetworkDeployed;
				ApplicationContext scanbotContext = 
                    new ScanbotContext(GetVersion(isNetwork));
                if (isNetwork) ConfigureAutoStart();                
                Application.Run(scanbotContext);
                
            }
        }
        
        public static string GetVersion(bool network)
        {
            return (network)? ApplicationDeployment.CurrentDeployment.
                    CurrentVersion.ToString(): Application.ProductVersion;
        }
        
        public static void ConfigureAutoStart()
        {
            string shortCutPath = Path.Combine(Path.Combine(Environment.GetFolderPath(
                System.Environment.SpecialFolder.Programs), 
                PUBLISHER), PRODUCT) + ".appref-ms";
            string startUpPath = Path.Combine(Environment.GetFolderPath(
                System.Environment.SpecialFolder.Startup),
                PRODUCT) + ".appref-ms";
            File.Copy(shortCutPath, startUpPath, true);                        
        }
        
        private const string PUBLISHER = "Gradians Educational Services";
        private const string PRODUCT = "Scanbot";
    }	
}

