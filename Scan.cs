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
            if (result.Text == null)
            {
                string signature = SHA1(scanFile);
                string undetectable = scanFile.Replace(Path.GetFileName(scanFile),
                    string.Format("{0}_{1}_{2}{3}", 
                        signature, 0, 0, Scan.TO_SEND));
                if (!File.Exists(undetectable))
                    File.Move(scanFile, undetectable);
            }
            else if (!result.Text.Equals(BLANK_PAGE_CODE))
            {
                string dontProcess = scanFile.Replace(Path.GetFileName(scanFile),
                    string.Format("{0}_{1}_{2}{3}", 
                        result.Text, 1, result.Rotation, Scan.TO_SEND));
                if (!File.Exists(dontProcess))
                    File.Move(scanFile, dontProcess);
            }            
            if (File.Exists(scanFile)) File.Delete(scanFile);
        }
		
		public void Process(FaTaPhat fataphat)
		{
			string nameInStaging = string.Format("staging/{0}", 
                Path.GetFileNameWithoutExtension(scanFile));
			Shrink(scanFile);
			if (fataphat.Put(scanFile, nameInStaging, FtpDataType.Binary))
			{
				File.Delete(scanFile);
			}
		}
		
		private void Shrink(string file)
		{
            MemoryStream mstream = new MemoryStream(File.ReadAllBytes(file));
			Bitmap image= new Bitmap(Image.FromStream(mstream));
			image = Crop(image);
			int WIDTH = 900;
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
		private const string BLANK_PAGE_CODE = "0";		
	}
}

