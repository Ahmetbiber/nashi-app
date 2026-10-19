using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nashi.Core;
namespace Nashi.Windows;
internal static class SmokeTests
{
    public static void Run()
    {
        var root=Environment.GetEnvironmentVariable("NASHI_SMOKE_DIRECTORY")??Path.Combine(Path.GetTempPath(),"Nashi-Smoke-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var checks=0;
        void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(Brushes.DarkOliveGreen,null,new Rect(0,0,320,180));dc.DrawRectangle(Brushes.Gold,null,new Rect(20,20,120,80));}
        var bitmap=new RenderTargetBitmap(320,180,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var source=Path.Combine(root,"Source.png");ImageEngine.Save(bitmap,source,"png");
        var crop=ImageEngine.Apply("crop",[source],root,new(Crop:new Int32Rect(10,10,80,60)));Check(ImageEngine.Load(crop[0]).PixelWidth==80,"Crop width mismatch");
        var resized=ImageEngine.Apply("resize",[source],root,new(Width:160));Check(ImageEngine.Load(resized[0]).PixelHeight==90,"Resize aspect mismatch");
        var rotated=ImageEngine.Apply("rotate",[source],root,new(Rotation:90));Check(ImageEngine.Load(rotated[0]).PixelWidth==180,"Rotate mismatch");
        foreach(var ext in ImageEngine.Formats){var converted=ImageEngine.Apply("convert",[source],root,new(),ext);Check(new FileInfo(converted[0]).Length>0,"Empty "+ext);}
        var pdfs=Directory.GetFiles(root,"*.pdf");var merged=DocumentEngine.Pdf("mergePDFs",[pdfs[0],pdfs[0]],root);var parts=DocumentEngine.Pdf("splitPDF",merged,root);Check(parts.Count==2,"PDF page count mismatch");
        var zip=DocumentEngine.Zip([source],root);var extracted=DocumentEngine.Unzip(zip,root);Check(File.Exists(Path.Combine(extracted[0],"Source.png")),"ZIP output missing");
        var window=new MainWindow([source]);window.Show();window.UpdateLayout();
        var screenshot=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);screenshot.Render(window);ImageEngine.Save(screenshot,Path.Combine(root,"windows-home.png"),"png");
        var wheel=new WheelWindow(window,[new("crop","Crop","⌗"),new("resize","Resize","⤢"),new("annotate","Annotate","✎")],_=>{});wheel.Show();wheel.UpdateLayout();
        var wheelImage=new RenderTargetBitmap((int)wheel.ActualWidth,(int)wheel.ActualHeight,96,96,PixelFormats.Pbgra32);wheelImage.Render(wheel);ImageEngine.Save(wheelImage,Path.Combine(root,"windows-wheel.png"),"png");wheel.Close();
        var editor=new ToolEditor(window,"crop","Crop",source,preview:bitmap);editor.Show();editor.UpdateLayout();var editorImage=new RenderTargetBitmap((int)editor.ActualWidth,(int)editor.ActualHeight,96,96,PixelFormats.Pbgra32);editorImage.Render(editor);ImageEngine.Save(editorImage,Path.Combine(root,"windows-crop.png"),"png");editor.Close();
        var toast=new ToastWindow(window,null);toast.Show();Check(toast.WindowStyle==WindowStyle.None&&toast.ResizeMode==ResizeMode.NoResize,"Toast has window chrome");toast.Close();window.Close();
        File.WriteAllText(Path.Combine(root,"smoke.json"),JsonSerializer.Serialize(new{passed=checks,scope="Windows WPF render and real image/PDF/ZIP operations. No physical mouse/global Explorer gesture claimed."}));
    }
}
