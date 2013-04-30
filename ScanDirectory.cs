using System;
using System.IO;

namespace gutenberg.collect
{
    public class ScanDirectory
    {    
        public const string INI_FILE = @"desktop.ini";
        public const string FLDR_ICON_FILE = @"scanbot-folder-icon.ico";
        public const string SCAN_DIR = @"Scantray";
        
        public ScanDirectory()
        {
            scanDirPath = Create();
            SetIcon(scanDirPath);
        }
        
        public string[] GetFiles(string pattern)
        {
            return Directory.GetFiles(scanDirPath, "*" + pattern);
        }
        
        public override string ToString()
        {
            return scanDirPath;
        }
        
        private string Create()
        {
            scanDirPath = Path.Combine(System.Environment.GetFolderPath
                (Environment.SpecialFolder.Desktop), SCAN_DIR);
                
            if (Directory.Exists(scanDirPath)) return scanDirPath;
            
            DirectoryInfo dinfo = Directory.CreateDirectory(scanDirPath);
            dinfo.Attributes &= ~FileAttributes.ReadOnly;
            
            return scanDirPath;
        }
        
        private void SetIcon(string scanDirPath)
        {
            string iconFilePath = Path.Combine(scanDirPath, 
                FLDR_ICON_FILE);
            if (!File.Exists(iconFilePath))
            {
                File.Copy(FLDR_ICON_FILE, iconFilePath);
                File.SetAttributes(iconFilePath, 
                    File.GetAttributes(iconFilePath)|FileAttributes.Hidden);
            }
            string iniPath = Path.Combine(scanDirPath, INI_FILE);
            if (!File.Exists(iniPath))
            {
                string[] iniText = {
                    "[.ShellClassInfo]",
                    "InfoTip=Scanbot Tray",
                    "IconFile=" + iconFilePath,
                    "IconIndex=0"
                };                     
                File.WriteAllLines(iniPath, iniText);         
                File.SetAttributes(iniPath, 
                    File.GetAttributes(iniPath)|FileAttributes.Hidden);
                File.SetAttributes(scanDirPath, 
                    File.GetAttributes(scanDirPath)|FileAttributes.System);
            }
        }
        
        private string scanDirPath;
                
    }
}

