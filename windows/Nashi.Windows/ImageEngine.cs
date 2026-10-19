using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nashi.Core;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
namespace Nashi.Windows;

public sealed record ImageOptions(int? Width=null,int? Height=null,int Rotation=90,Int32Rect? Crop=null,int Quality=85,bool Horizontal=true,BitmapSource? Annotated=null);
public static class ImageEngine
{
    public static readonly string[] Formats=["png","jpg","bmp","gif","tiff","pdf"];
    public static BitmapSource Load(string file)
    {
        using var stream=File.OpenRead(file);var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var image=decoder.Frames[0];
        if((long)image.PixelWidth*image.PixelHeight>100_000_000)throw new InvalidDataException("This image is too large (100 MP limit).");image.Freeze();return image;
    }
    public static IReadOnlyList<string> Apply(string tool,IReadOnlyList<string> inputs,string destination,ImageOptions options,string? format=null,CancellationToken token=default,IProgress<OperationProgress>? progress=null)
    {
        using var tx=new ExportTransaction(destination);var outputs=new List<string>();var completed=0;
        foreach(var input in inputs)
        {
            token.ThrowIfCancellationRequested();BitmapSource image=options.Annotated??Load(input);var ext=(format??Path.GetExtension(input).TrimStart('.')).ToLowerInvariant();if(ext=="jpeg")ext="jpg";if(ext=="tif")ext="tiff";
            if(!Formats.Contains(ext))ext="png";
            switch(tool)
            {
                case "resize":
                    var width=options.Width??(int)Math.Round(image.PixelWidth*(double)(options.Height??image.PixelHeight)/image.PixelHeight);
                    var height=options.Height??(int)Math.Round(image.PixelHeight*(double)width/image.PixelWidth);
                    if(width<1||height<1||width>30000||height>30000||(long)width*height>100_000_000)throw new ArgumentException("Choose dimensions between 1 and 30,000 pixels (100 MP limit).");
                    image=new TransformedBitmap(image,new ScaleTransform((double)width/image.PixelWidth,(double)height/image.PixelHeight));break;
                case "crop":if(options.Crop is not {} crop||crop.Width<1||crop.Height<1||crop.X<0||crop.Y<0||crop.X+crop.Width>image.PixelWidth||crop.Y+crop.Height>image.PixelHeight)throw new ArgumentException("Drag a crop inside the image.");image=new CroppedBitmap(image,crop);break;
                case "rotate":image=new TransformedBitmap(image,new RotateTransform(options.Rotation));break;
                case "flip":image=new TransformedBitmap(image,new ScaleTransform(options.Horizontal?-1:1,options.Horizontal?1:-1));break;
                case "convert":case "compress":case "removeMetadata":case "annotate":break;
                default:throw new ArgumentException("Unsupported image tool.");
            }
            if(tool=="compress")ext="jpg";
            var output=SafePaths.Unique(tx.StagingDirectory,Path.GetFileNameWithoutExtension(input)+(tool=="convert"?"":" "+tool)+"."+ext);
            Save(image,output,ext,options.Quality);outputs.Add(output);progress?.Report(new(++completed/(double)inputs.Count*.98,"Saving images…"));
        }
        return tx.Publish(outputs,token);
    }
    public static void Save(BitmapSource image,string output,string format,int quality=85)
    {
        if(format=="pdf")
        {
            using var png=new MemoryStream();var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(png);png.Position=0;
            using var pdf=new PdfDocument();var page=pdf.AddPage();page.Width=XUnit.FromPoint(image.PixelWidth*72/96d);page.Height=XUnit.FromPoint(image.PixelHeight*72/96d);
            using var graphic=XGraphics.FromPdfPage(page);using var resource=XImage.FromStream(png);graphic.DrawImage(resource,0,0,page.Width.Point,page.Height.Point);pdf.Save(output);return;
        }
        BitmapEncoder target=format switch{"jpg"=>new JpegBitmapEncoder{QualityLevel=Math.Clamp(quality,1,100)},"bmp"=>new BmpBitmapEncoder(),"gif"=>new GifBitmapEncoder(),"tiff"=>new TiffBitmapEncoder(),_=>new PngBitmapEncoder()};
        // New frames omit source metadata deliberately; originals stay unchanged.
        target.Frames.Add(BitmapFrame.Create(image));using var file=new FileStream(output,FileMode.CreateNew);target.Save(file);
    }
}
