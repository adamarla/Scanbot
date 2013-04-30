using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Net.FtpClient;

namespace gutenberg.collect
{
	public class Scan
	{
		public static string TO_DETECT = ".ap.grdns";
		public static string TO_SEND = ".dp.grdns";
		public static string GRDNS_EXTNSN = ".grdns";
		
		public Scan(string scanFile)
		{
			this.scanFile = scanFile;
		}
        
        public void Process(DetectionResult result)
        {
			string filename = Path.GetFileName(scanFile);			
            if (result.Text == null)
            {
                string signature = SHA1(scanFile);
                string undetectable = scanFile.Replace(filename,
                    string.Format(NAME_FORMAT, signature, UNDETECTED, 0, 
					Scan.TO_SEND));
                if (!File.Exists(undetectable))
                    File.Move(scanFile, undetectable);
            }
            else if (!result.Text.Equals(BLANK_PAGE_CODE))
            {
                string dontProcess = scanFile.Replace(filename,
                    string.Format(NAME_FORMAT, result.Text.Replace(" ", String.Empty), 
					DETECTED, result.Rotation, Scan.TO_SEND));
                if (!File.Exists(dontProcess))
                    File.Move(scanFile, dontProcess);
            }            
            if (File.Exists(scanFile)) File.Delete(scanFile);
        }
		
		public void Process(FaTaPhat fataphat)
		{
		    string filename = Path.GetFileName(scanFile);			
			string nameInStaging = string.Format("staging/{0}", 
                filename.Split(new char[] {'.'})[0]);
			if (filename.Contains("_1_"))
			    Shrink(scanFile);				
			if (fataphat.Put(scanFile, nameInStaging, FtpDataType.Binary))
			{
				File.Delete(scanFile);
			}
		}
        
        public bool IsLocked()
        {
            FileInfo fileInfo = new FileInfo(scanFile);
            FileStream stream = null;
            try
            {
                stream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }
            //file is not locked
            return false;        
        }        
		
		private void Shrink(string file)
		{
            MemoryStream mstream = new MemoryStream(File.ReadAllBytes(file));
			Bitmap image= new Bitmap(Image.FromStream(mstream));
			image = Crop(image);
			int WIDTH = 1275;//width in pixels @150dpi
			if (image.Width > WIDTH)
			{
				int w = WIDTH, h = (int)(((double)image.Height)/image.Width*WIDTH);
				Bitmap newImage = new Bitmap(w, h);
				using (Graphics gr = Graphics.FromImage(newImage))
				{
					gr.CompositingQuality = CompositingQuality.HighQuality;
					gr.SmoothingMode = SmoothingMode.AntiAlias;
					gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
					gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
					gr.DrawImage(image, new Rectangle(0, 0, w, h));
				}
				newImage.Save(file, ImageFormat.Jpeg);
			}				
		}
		
		private Bitmap Crop(Bitmap image)
		{
			double aspectRatio;
			int offset = System.TimeZone.CurrentTimeZone.
				GetUtcOffset(DateTime.Now).Hours;
			if (offset > 3 && offset < 10)
				aspectRatio = 8.5/11;			//US LETTER
			else
				aspectRatio = 1/Math.Sqrt(2);	//A4
			
			int w = image.Width, h = image.Height;
		        if ((aspectRatio - ((double) w / h)) > 0.1) {
				Size size = new Size(image.Width, 
					(int)(image.Width*aspectRatio));
				Rectangle r = new Rectangle(new Point(0, 0), size);				
				image = image.Clone(r, image.PixelFormat);
			}
			return image;
		}
        
        private string SHA1(string file)
        {
            byte[] fileData = File.ReadAllBytes(file);
            byte[] hashData = System.Security.Cryptography.
                SHA1.Create().ComputeHash(fileData); // SHA1 or MD5
            string hashString = BitConverter.ToString(fileData);
            return hashString.Substring((int)hashString.Length/2, 60).
                Replace("-", "");
        }
        
		private string scanFile;
		private const int DETECTED = 1, UNDETECTED = 0;
		private const string NAME_FORMAT = "{0}_{1}_{2}{3}";
		private const string BLANK_PAGE_CODE = "0";		
	}
}

