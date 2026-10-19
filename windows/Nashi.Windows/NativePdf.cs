using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using global::Windows.Storage;
using global::Windows.Storage.Streams;
using global::Windows.Data.Pdf;
using global::Windows.Graphics.Imaging;
using global::Windows.Media.Ocr;
using global::Windows.Media.FaceAnalysis;
using Nashi.Core;
namespace Nashi.Windows;
public static class NativePdf
{
    public static async Task<byte[][]> RenderAsync(string path,int dpi=144,CancellationToken token=default,string password="")
    {
        var source=await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));var pdf=await global::Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(source,password);if(pdf.PageCount>1000)throw new InvalidDataException("Choose a PDF with at most 1,000 pages.");var images=new List<byte[]>();
        for(uint i=0;i<pdf.PageCount;i++){token.ThrowIfCancellationRequested();using var page=pdf.GetPage(i);var width=(uint)Math.Max(1,Math.Round(page.Size.Width*dpi/96));var height=(uint)Math.Max(1,Math.Round(page.Size.Height*dpi/96));if((ulong)width*height>100_000_000)throw new InvalidDataException("PDF page exceeds 100 MP.");using var stream=new InMemoryRandomAccessStream();await page.RenderToStreamAsync(stream,new PdfPageRenderOptions{DestinationWidth=width,DestinationHeight=height});using var output=new MemoryStream();await stream.AsStreamForRead().CopyToAsync(output,token);images.Add(output.ToArray());if(images.Sum(p=>(long)p.Length)>512_000_000)throw new InvalidDataException("Rendered pages exceed 512 MB. Choose a lower DPI or split the PDF first.");}return images.ToArray();
    }
    public static async Task<Rect[]> DetectAsync(BitmapSource image,bool text,bool faces)
    {
        using var stream=new InMemoryRandomAccessStream();using(var writer=new DataWriter(stream.GetOutputStreamAt(0))){writer.WriteBytes(ImageEngine.PngBytes(image));await writer.StoreAsync();}stream.Seek(0);var decoder=await global::Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied);var regions=new List<Rect>();
        if(text){var engine=OcrEngine.TryCreateFromUserProfileLanguages()??OcrEngine.TryCreateFromLanguage(new global::Windows.Globalization.Language("en-US"));if(engine is null)throw new InvalidOperationException("Install an OCR language in Windows Language settings to detect text.");var result=await engine.RecognizeAsync(bitmap);regions.AddRange(result.Lines.SelectMany(l=>l.Words).Select(w=>new Rect(w.BoundingRect.X,w.BoundingRect.Y,w.BoundingRect.Width,w.BoundingRect.Height)));}
        if(faces){using var gray=SoftwareBitmap.Convert(bitmap,BitmapPixelFormat.Gray8);var detector=await FaceDetector.CreateAsync();regions.AddRange((await detector.DetectFacesAsync(gray)).Select(f=>new Rect(f.FaceBox.X,f.FaceBox.Y,f.FaceBox.Width,f.FaceBox.Height)));}return regions.ToArray();
    }
    public static string[] DecodeQr(BitmapSource image)
    {
        var converted=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);var pixels=new byte[checked(converted.PixelWidth*converted.PixelHeight*4)];converted.CopyPixels(pixels,converted.PixelWidth*4,0);
        var reader=new ZXing.BarcodeReaderGeneric{AutoRotate=true,Options=new ZXing.Common.DecodingOptions{TryHarder=true,PossibleFormats=[ZXing.BarcodeFormat.QR_CODE]}};return reader.DecodeMultiple(pixels,converted.PixelWidth,converted.PixelHeight,ZXing.RGBLuminanceSource.BitmapFormat.BGRA32)?.Select(r=>r.Text).Distinct().ToArray()??[];
    }
}
