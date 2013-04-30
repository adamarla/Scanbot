using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
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
	public class Detector
	{
		public Detector(ScanDirectory scanDir)
		{
			this.scanDir = scanDir;
		}

		public void Execute(Object o, DoWorkEventArgs args)
		{
			Result QRCodeResult = null;
			Hashtable hints = new Hashtable();
			hints.Add(DecodeHintType.TRY_HARDER, Boolean.TrueString);

			while (GotScan())
			{			
				int fileCount = 0;
				string[] scanFiles = scanDir.GetFiles(Scan.TO_DETECT);
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
                    
					try 
					{
						QRCodeResult = DetectQRC(scanFile, hints);
					}
					catch (Exception)
					{
						File.Delete(scanFile);
						continue;
					}
                                        
                    DetectionResult result = new DetectionResult();
                    if (QRCodeResult != null)
                    {
                        result.Text = QRCodeResult.Text;
                        result.Rotation = (int)QRCodeResult.
                            ResultMetadata[FLIPPED_KEY];
                    }
					
                    Scan scan = new Scan(scanFile);
                    scan.Process(result);
				}
                
                if (((BackgroundWorker)o).CancellationPending)
                {
                    break;
                }
                
			}
			((BackgroundWorker)o).ReportProgress(100, "TASK_END");
		}

		private Result DetectQRC(string file, Hashtable hints)
		{
			Image img = null;
			LuminanceSource source = null;
			BinaryBitmap bitmap = null;
			Binarizer binarizer = null;
			Result QRCodeResult = null;
			Reader reader = new QRCodeReader();

			byte[] buffer = File.ReadAllBytes(file);
			using (MemoryStream ms = new MemoryStream(buffer))
			{
				img = Image.FromStream(ms);
			}
			
			bool flipped = false;
			while (true)
			{
				Bitmap[] snippets = GetSnippets(img);			
				foreach (Bitmap bmp in snippets)
				{
					source = new RGBLuminanceSource(bmp, bmp.Width, bmp.Height);
					ZXingConfig[] configs = ZXingConfig.getInstances(source);
					foreach (ZXingConfig config in configs)
					{
						binarizer = config.binarizer;
						bitmap = new BinaryBitmap(binarizer);
						try {
							QRCodeResult = reader.decode(bitmap, hints);
							QRCodeResult.ResultMetadata[FLIPPED_KEY] = flipped? 1: 0;                        
							return QRCodeResult;//found it, get outta Dodge!
						} catch (Exception) { }                    
					}
				}
				
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
			string[] scanFiles = null;
			string pickedUp = string.Empty, moved = string.Empty;
            bool keepLooping = true;
            while(keepLooping)
            {
                keepLooping = false;
                scanFiles = Directory.GetFiles(scanDir.ToString());
                foreach (string file in scanFiles)
                {
                    if (Directory.Exists(file)) continue;

                    if (file.EndsWith(Scan.GRDNS_EXTNSN)) continue;

                    if (file.EndsWith(ScanDirectory.INI_FILE)) continue;
                    
                    if (file.EndsWith(ScanDirectory.FLDR_ICON_FILE)) continue;
                    
                    if (IsTIFF(file)) 
                        {ExplodeTIFF(file); keepLooping = true; continue;}
                    
                    if (IsPDF(file)) 
                        {ExplodePDF(file); keepLooping = true; continue;}

                    pickedUp = Path.ChangeExtension(file, Scan.TO_DETECT);
                    File.Move(file, pickedUp);
                }
            }            
			return scanDir.GetFiles(Scan.TO_DETECT).Length != 0;
		}
        
        private void ExplodePDF(string PDF)
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
        
        private void ExplodeTIFF(string TIFF)
        {
            using (Image imageFile = Image.FromFile(TIFF)) 
            { 
                FrameDimension frameDimensions = new FrameDimension( 
                    imageFile.FrameDimensionsList[0]); 

                string path = Path.GetDirectoryName(TIFF);
                // Gets the number of pages from the tiff image (if multipage) 
                int frameNum = imageFile.GetFrameCount(frameDimensions);
                for (int frame = 0; frame < frameNum; frame++) 
                { 
                    // Selects one frame at a time and save as jpeg. 
                    imageFile.SelectActiveFrame(frameDimensions, frame); 
                    using (Bitmap bmp = new Bitmap(imageFile)) 
                    { 
                        string file = Path.Combine(path, Path.GetRandomFileName());
                        while (File.Exists(file))
                        {
                            file = Path.Combine(path, Path.GetRandomFileName());
                        }
                        bmp.Save(file, ImageFormat.Jpeg);
                    } 
                }
            }
            File.Delete(TIFF);
        }
        
        private bool IsPDF(string file)
        {
            FileStream stream = new FileStream(file, FileMode.Open);
            byte[] buffer = new byte[PDF_HDR_SIZE];
            bool isPDF = false;
            if (stream.Read(buffer, 0, buffer.Length) == buffer.Length)
            {
                string s = System.Text.Encoding.Default.GetString(buffer);
                isPDF = s.Contains("%PDF-1.");
            }
            stream.Close();
            return isPDF;
        }
        
        private bool IsTIFF(string file)
        {
            FileStream stream = new FileStream(file, FileMode.Open);
            byte[] buffer = new byte[TIF_HDR_SIZE];
            bool isTIF = false;
            if (stream.Read(buffer, 0, buffer.Length) == buffer.Length)
            {
                string s = BitConverter.ToString(buffer).
                    Replace("-", string.Empty).ToUpper();
                isTIF = s.Equals("49492A00") || s.Equals("4D4D002A");
            }
            stream.Close();
            return isTIF;
        }
	
	    private Bitmap[] GetSnippets(Image image)
		{
		    ArrayList snippets = new ArrayList();
			
			Bitmap bmp = new Bitmap(image);
			int third = (int)(bmp.Width / 3.0);
			Rectangle r = new Rectangle(new Point(2 * third, 0), 
				new Size(third, bmp.Height));
			bmp = bmp.Clone(r, bmp.PixelFormat);
			snippets.Add(bmp);			
			
			int[] widths = {425, 566, 354, 283};
			foreach (int width in widths) 
			{
                if (third < width) continue;
				int height = (int)(((double)bmp.Height)/bmp.Width*width);
				Bitmap newImage = new Bitmap(width, height);
				using (Graphics gr = Graphics.FromImage(newImage))
				{
					gr.CompositingQuality = CompositingQuality.HighQuality;
					gr.SmoothingMode = SmoothingMode.AntiAlias;
					gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
					gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
					gr.DrawImage(bmp, new Rectangle(0, 0, width, height));
				}
				snippets.Add(newImage);
		    }
            return (Bitmap[])snippets.ToArray(bmp.GetType());	
		}
	
		private int progress;
		private ScanDirectory scanDir;
        private const string FLIPPED_KEY = "flipped";
        
        private const int TIF_HDR_SIZE = 4, PDF_HDR_SIZE = 1024;        
	}
    
    public struct DetectionResult 
    {    
        public string Text;
        public int Rotation;    
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

			configs[1] = new ZXingConfig();
			configs[1].binarizer = new GlobalHistogramBinarizer(source);

			return configs;
		}

		public Binarizer binarizer;
	}

}

