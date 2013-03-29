using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Collections;
using System.ComponentModel;
using System.Globalization;

using com.google.zxing;
using com.google.zxing.common;
using com.google.zxing.qrcode;

namespace gutenberg.collect
{
	public class Manual
	{
	    public Manual (ScanDirectory scanDir, Manifest manifest)
		{
			this.scanDir = scanDir;
			this.manifest = manifest;
		}

		public void Execute(int index, bool flipped)
		{
			int scanRotation = flipped ? 1 : 0;
			string scanKey = ((DisplayItem)displayItems[index]).Key;
			string scanValue = manifest.Get(scanKey);
			string dontProcess = string.Format("{0}/{1}_{2}_{3}{4}", scanDir, 
				scanKey, scanValue, scanRotation, Scan.DONT_PROCESS);
			File.Move(selectedFile, dontProcess);
			manifest.Remove(scanKey, true);
		}

		public void Delete()
		{
			File.Delete(selectedFile);
		}

		public Image GetNextImage()
		{
			Image image = null;
			selectedFile = null;
			string[] scanFiles = scanDir.GetFiles(Scan.MANUAL_PROCESS);
			foreach (string file in scanFiles) {

				byte[] buffer = File.ReadAllBytes(file);
				using (MemoryStream ms = new MemoryStream(buffer))
				{
					try
					{
						image = Image.FromStream(ms);
					}
					catch (Exception) { }
				}

				if (image == null)
					continue;

				selectedFile = file;
				break;
			}
			return image;
		}
				
		public object[] Initialize()
		{
			displayItems = new ArrayList();
			string[] keys = manifest.GetKeys();
			foreach (string key in keys) {

				string val = manifest.Get(key);
				char[] sep = {'-'};
				string[] tokens = val.Split(sep);
				DisplayItem displayItem = new DisplayItem();
				displayItem.Key = key;
				displayItem.Page = tokens[6];
				displayItem.FName = tokens[1];
				displayItem.LName = tokens[2];
				displayItem.CompleteSet = manifest.IsComplete(key);
				displayItems.Add(displayItem);
			}
			displayItems.Sort(new DisplayItem());
			return displayItems.ToArray();
		}
		
		private ArrayList displayItems;
		private string selectedFile;
		private ScanDirectory scanDir;
		private Manifest manifest;
	}

	public struct DisplayItem: IComparer
	{
		public string Key, FName, LName,
			Page;
		public bool CompleteSet;
		
		public string CompareValue {
			get 
			{
				return string.Format("{0}{1}{2}", FName, 
					LName, Page);
			}
		}

        public string DisplayName {
            get 
            {
                TextInfo tinfo = CultureInfo.CurrentCulture.TextInfo;
                return tinfo.ToTitleCase
                    (string.Format("{0} {1}", FName, LName));
            }
        }

        public string DisplayPageNumber {
            get 
            {
                return string.Format("#{0}", Page);
            }

        }
		
		public override string ToString()
		{
			TextInfo tinfo = CultureInfo.CurrentCulture.TextInfo;
			return tinfo.ToTitleCase
				(string.Format(" {0}/ {1} {2}", Page, FName, LName));
		}

		public int Compare(object x, object y)
		{
			DisplayItem xDispItem = (DisplayItem)x;			
			DisplayItem yDispItem = (DisplayItem)y;
			
			if ((xDispItem.CompleteSet && yDispItem.CompleteSet) ||
				(!xDispItem.CompleteSet && !yDispItem.CompleteSet))
			{
				return string.Compare(xDispItem.CompareValue,
					yDispItem.CompareValue);
			}				
			else if (xDispItem.CompleteSet && !yDispItem.CompleteSet)
			{
				return 1;
			}
			else
			{
				return -1;	
			}
				
		}
	}
}

