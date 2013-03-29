using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Collections;
using System.ComponentModel;
using System.Globalization;

using com.google.zxing;
using com.google.zxing.common;
using com.google.zxing.qrcode;

using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

namespace gutenberg.collect
{
	public class Automatic
	{
		public Automatic(ScanDirectory scanDir, Manifest manifest)
		{
			this.scanDir = scanDir;
			this.manifest = manifest;
		}

		public void Execute(Object o, DoWorkEventArgs args)
		{
			string scanKey = null, scanValue = null;
			int scanRotation = 0;
			Result QRCodeResult = null;

			Hashtable hints = new Hashtable();
			hints.Add(DecodeHintType.TRY_HARDER, Boolean.TrueString);

			while (GotScan())
			{
				int fileCount = 0;
				string[] scanFiles = scanDir.GetFiles(Scan.AUTO_PROCESS);
  			    progress = (int)(fileCount * 100.00 / scanFiles.Length);
				((BackgroundWorker)o).ReportProgress(progress, "TASK_START");
				foreach (string scanFile in scanFiles)
				{
                    if (((BackgroundWorker)o).CancellationPending)
                    {
                        args.Cancel = true;
                        break;
                    }            
            
					fileCount++;
					progress = (int)(fileCount * 100.00 / scanFiles.Length);
					((BackgroundWorker)o).ReportProgress(progress);
                    
					try {
						QRCodeResult = DetectQRC(scanFile, hints);
					}
					catch (Exception)
					{
						File.Delete(scanFile);
						continue;
					}
					
					if (QRCodeResult == null && File.Exists(scanFile))
					{
						string undetectable = scanFile.Replace(
							Scan.AUTO_PROCESS,
							Scan.MANUAL_PROCESS);
						File.Move(scanFile, undetectable);
						continue;
					}
					
					if (QRCodeResult.Text.Equals(BLANK_PAGE_CODE))
					{
						File.Delete(scanFile);
						continue;
					}

					scanRotation = (int)QRCodeResult.ResultMetadata[FLIPPED_KEY];
					scanKey = QRCodeResult.Text;
					scanValue = manifest.Get(scanKey);
					if (scanValue == null)
					{
						File.Delete(scanFile);
						continue;
					}
					else if (scanValue.Contains(SUGGESTION_CODE))
					{
						string signature = string.Format("0-{0}-0",
							new FileInfo(scanFile).Length);
						scanValue = scanValue.Replace(SUGGESTION_CODE, 
								signature);
					}						
						
					string dontProcess = string.Format("{0}/{1}_{2}_{3}{4}", 
                        scanDir, scanKey, scanValue, scanRotation, 
                        Scan.DONT_PROCESS);
					if (File.Exists(dontProcess))
						File.Delete(scanFile);
					else
						File.Move(scanFile, dontProcess);
					manifest.Remove(scanKey, false);
				}
                
                if (((BackgroundWorker)o).CancellationPending)
                {
                    break;
                }
                
			}
			((BackgroundWorker)o).ReportProgress(100, "TASK_END");
			manifest.Checkpoint();
		}

		private Result DetectQRC(string file, Hashtable hints)
		{
			Image img = null;
			LuminanceSource source = null;
			BinaryBitmap bitmap = null;
			Binarizer binarizer = null;
			Reader reader = null;
			Result QRCodeResult = null;

			byte[] buffer = File.ReadAllBytes(file);
			using (MemoryStream ms = new MemoryStream(buffer))
			{
				img = Image.FromStream(ms);
			}
				
			bool flipped = false;
			while (true)
			{
				Bitmap bmp = new Bitmap(img);
				int w = bmp.Width;
				int third = (int)(w / 3.0);
				Rectangle r = new Rectangle(new Point(2 * third, 0), 
					new Size(third, bmp.Height));
				bmp = bmp.Clone(r, bmp.PixelFormat);
				
				source = new RGBLuminanceSource(bmp, bmp.Width, bmp.Height);
				ZXingConfig[] configs = ZXingConfig.getInstances(source);
				foreach (ZXingConfig config in configs)
				{					
					reader = config.reader;
					binarizer = config.binarizer;
					bitmap = new BinaryBitmap(binarizer);
					try {
						QRCodeResult = reader.decode(bitmap, hints);
                        QRCodeResult.ResultMetadata[FLIPPED_KEY] = flipped? 1: 0;                        
						break;
					} catch (Exception) { }
				}				
				if (QRCodeResult != null)
					break;
				
				if (!flipped)
				{
					img.RotateFlip(RotateFlipType.Rotate180FlipNone);	
					flipped = true;
				} else
					break;
			}
			return QRCodeResult;
		}
		
		private bool GotScan()
		{
			progress = 0;
            
            string[] pdfs = scanDir.GetFiles(".pdf");
            foreach (string pdf in pdfs)
            {
                    ExplodePDF(pdf);
            }
            
			string[] scanFiles = Directory.GetFiles(scanDir.ToString());
			string pickedUp = string.Empty, moved = string.Empty;
			foreach (string file in scanFiles)
			{
				if (Directory.Exists(file)) continue;

				if (file.EndsWith(Scan.GRDNS_EXTNSN)) continue;

                if (file.EndsWith(ScanDirectory.INI_FILE)) continue;
                
                if (file.EndsWith(ScanDirectory.FLDR_ICON_FILE)) continue;

				int i = 0;
				pickedUp = string.Format("{0}-{1}{2}",
					Path.ChangeExtension(file, null), i, Scan.AUTO_PROCESS);
				moved = string.Format("{0}-{1}{2}",
					Path.ChangeExtension(file, null), i, Scan.MANUAL_PROCESS);
				while (File.Exists(pickedUp) || File.Exists(moved))
				{
					i++;
					pickedUp = string.Format("{0}-{1}{2}",
						Path.ChangeExtension(file, null), i, Scan.AUTO_PROCESS);
					moved = string.Format("{0}-{1}{2}",
						Path.ChangeExtension(file, null), i, Scan.MANUAL_PROCESS);
				}
				File.Move(file, pickedUp);
			}
            
			return scanDir.GetFiles(Scan.AUTO_PROCESS).Length != 0;
		}
        
        public void ExplodePDF(string PDF)
        {
            try {
                PdfReader pdfReader = new PdfReader (PDF);
                PdfReaderContentParser pdfParser = 
                    new PdfReaderContentParser(pdfReader);
                IRenderListener listener = 
                    new PDFImageRenderListener(Path.GetDirectoryName(PDF));
                
                for (int pg = 1; pg <= pdfReader.NumberOfPages; pg++)
                {
                    pdfParser.ProcessContent(pg, listener);
                }            
                pdfReader.Close();            
            } 
            catch (Exception) { }
            File.Delete(PDF);
        }

		private int progress;
		private ScanDirectory scanDir;
		private Manifest manifest;
		private const string BLANK_PAGE_CODE = "0";
		private const string SUGGESTION_CODE = "0-0-0";
        private const string FLIPPED_KEY = "flipped";
	}

    class PDFImageRenderListener : IRenderListener
    {
        protected string path;

        public PDFImageRenderListener(string path)
        {
            this.path = path;
        }

        public void BeginTextBlock()
        {
        }

        public void EndTextBlock()
        {
        }
 
        public void RenderImage(ImageRenderInfo renderInfo)
        {
            PdfImageObject image = renderInfo.GetImage();
            if (image == null)
                return;                
            Image drawingImage = image.GetDrawingImage();
            string file = Path.Combine(path, Path.GetRandomFileName());
            while (File.Exists(file))
            {
                file = Path.Combine(path, Path.GetRandomFileName());
            }
            drawingImage.Save(file, ImageFormat.Jpeg);
        }

        public void RenderText(TextRenderInfo renderInfo)
        {
        }

    }
    
    
	class ZXingConfig
	{

		public static ZXingConfig[] getInstances(LuminanceSource source)
		{
			ZXingConfig[] configs = new ZXingConfig[2];

			configs[0] = new ZXingConfig();
			configs[0].binarizer = new HybridBinarizer(source);
			configs[0].reader = new QRCodeReader();

			configs[1] = new ZXingConfig();
			configs[1].binarizer = new GlobalHistogramBinarizer(source);
			configs[1].reader = new QRCodeReader();

			return configs;
		}

		public Binarizer binarizer;
		public Reader    reader;

	}

}

