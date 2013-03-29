using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.FtpClient;

namespace gutenberg.collect
{
	public class Scan
	{
		
		public static string AUTO_PROCESS = ".ap.grdns";
		public static string MANUAL_PROCESS = ".mp.grdns";
		public static string DONT_PROCESS = ".dp.grdns";
		public static string GRDNS_EXTNSN = ".grdns";
		
		public Scan(string base36ScanId, string scanContext, int scanRotation,
            string scanFile)
		{
			// scanContext=[studentId]-[first name]-[last name]-[quiz_id]
			// [testpaper_id]-[question_no]-[page_no]-[totalPages]
			char[] sep = {'-'};
			string[] tokens = scanContext.Split(sep);
			string quizId = tokens[3], testpaperId = tokens[4], id = tokens[0],
				page = tokens[6];
			string scanId = string.Format("{0}-{1}-{2}-{3}", quizId, testpaperId, id, page);
			nameInStaging = string.Format("staging/{0}_{1}_{2}",
				base36ScanId, scanId, scanRotation);
			this.scanFile = scanFile;
		}
		
		public void Process(FaTaPhat fataphat)
		{
			Shrink(scanFile);
			if (fataphat.Put(scanFile, nameInStaging, FtpDataType.Binary))
			{
				File.Delete(scanFile);
			}
		}
		
		private void Shrink(string file)
		{
			Bitmap image= new Bitmap(Image.FromStream(
                            new MemoryStream(File.ReadAllBytes(file))));
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
		
		public string nameInStaging;
		private string scanFile;
		
	}
}

