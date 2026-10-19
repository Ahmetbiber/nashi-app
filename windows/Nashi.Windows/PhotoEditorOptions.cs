using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Nashi.Core;
using Microsoft.Win32;
namespace Nashi.Windows;
public sealed partial class ToolEditor
{
    private readonly List<Rect> photoRegions=[];
    private readonly ComboBox backdrop=new(){ItemsSource=new[]{"solid","gradient","image"},SelectedIndex=0},collageLayout=new(){ItemsSource=new[]{"grid","row","column","featured"},SelectedIndex=0},redactionStyle=new(){ItemsSource=new[]{"solid","blur","pixelate"},SelectedIndex=0};
    private string? backgroundImage,annotationImage;private string[] collageInputs=[];private readonly ListBox imageOrder=new(){MaxHeight=140};
    private UIElement? annotationShape;private int strokeKind;private Point endPoint;private int markNumber=1;
    private System.Windows.Threading.DispatcherTimer? photoPreview;
    private void BuildPhotoOptions(IReadOnlyList<string> inputs)
    {
        collageInputs=inputs.ToArray();
        if(tool=="editImage")
        {
            Field("Width",bitmap?.PixelWidth??1280);Field("Height",bitmap?.PixelHeight??720);Field("Degrees",0);
            foreach(var property in typeof(PhotoAdjustments).GetProperties())Field(property.Name,(double)property.GetValue(new PhotoAdjustments())!);
            options.Children.Insert(1,new TextBlock{Text="Light, color, detail and effects. Zero leaves an effect unchanged; Contrast, Gamma and Saturation start at 1.",TextWrapping=TextWrapping.Wrap,Margin=new(4,4,4,16)});
        }
        if(tool=="frameImage")
        {
            options.Children.Add(new TextBlock{Text="Background"});options.Children.Add(backdrop);TextField("Background color","#FFFFFF");TextField("Gradient end","#FFD85C");
            options.Children.Add(MainWindow.Button("Choose background image…",()=>{var picker=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.webp;*.heic;*.avif;*.tiff;*.bmp"};if(picker.ShowDialog(this)==true){backgroundImage=picker.FileName;backdrop.SelectedItem="image";QueuePhotoPreview();}}));
            Field("Padding",80);Field("Corner radius",24);Field("Shadow radius",24);Field("Shadow opacity",.25);Field("Background blur",0);Field("Image scale",1);Field("Position X",.5);Field("Position Y",.5);Field("Canvas width",(bitmap?.PixelWidth??1000)+160);Field("Canvas height",(bitmap?.PixelHeight??1000)+160);
        }
        if(tool=="createCollage")
        {
            options.Children.Add(new TextBlock{Text="Layout"});options.Children.Add(collageLayout);Field("Columns",2);Field("Canvas width",2048);Field("Canvas height",2048);Field("Spacing",12);Field("Padding",16);Field("Corner radius",0);TextField("Background color","#FFFFFF");
            foreach(var path in inputs)imageOrder.Items.Add(new ListBoxItem{Content=System.IO.Path.GetFileName(path),Tag=path});options.Children.Add(imageOrder);
            var buttons=new WrapPanel();buttons.Children.Add(MainWindow.Button("Move up",()=>MoveImage(-1)));buttons.Children.Add(MainWindow.Button("Move down",()=>MoveImage(1)));options.Children.Add(buttons);
        }
        if(tool=="redactImage")
        {
            options.Children.Add(new TextBlock{Text="Cover style"});options.Children.Add(redactionStyle);
            options.Children.Add(MainWindow.Button("Detect text",async()=>await DetectPhoto(true,false)));options.Children.Add(MainWindow.Button("Detect faces",async()=>await DetectPhoto(false,true)));
            options.Children.Add(MainWindow.Button("Undo area",()=>{if(photoRegions.Count==0)return;photoRegions.RemoveAt(photoRegions.Count-1);if(drawing.Children.Count>1)drawing.Children.RemoveAt(drawing.Children.Count-1);}));
        }
        if(tool is "editImage" or "frameImage" or "createCollage")
        {
            photoPreview=new(){Interval=TimeSpan.FromMilliseconds(300)};photoPreview.Tick+=async(_,_)=>{photoPreview.Stop();await PreviewPhoto();};
            foreach(var box in fields.Values)box.TextChanged+=(_,_)=>QueuePhotoPreview();backdrop.SelectionChanged+=(_,_)=>QueuePhotoPreview();collageLayout.SelectionChanged+=(_,_)=>QueuePhotoPreview();Closed+=(_,_)=>photoPreview.Stop();
            var original=new CheckBox{Content="Show original",Margin=new(4,14,4,4)};original.Checked+=(_,_)=>{photoPreview.Stop();if(drawing.Children[0] is Image image)image.Source=bitmap;};original.Unchecked+=(_,_)=>QueuePhotoPreview();options.Children.Add(original);Loaded+=(_,_)=>QueuePhotoPreview();
        }
        if(tool=="annotate")options.Children.Add(MainWindow.Button("Choose annotation image…",()=>{var picker=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.webp;*.heic;*.avif;*.bmp"};if(picker.ShowDialog(this)==true){annotationImage=picker.FileName;drawingTool.SelectedIndex=9;}}));
    }
    private void MoveImage(int delta){var index=imageOrder.SelectedIndex;var next=index+delta;if(index<0||next<0||next>=imageOrder.Items.Count)return;var item=imageOrder.Items[index];imageOrder.Items.RemoveAt(index);imageOrder.Items.Insert(next,item);imageOrder.SelectedIndex=next;QueuePhotoPreview();}
    private void QueuePhotoPreview(){if(photoPreview is null)return;photoPreview.Stop();photoPreview.Start();}
    private ImageOptions PhotoSettings(ImageOptions original)
    {
        var changes=new Dictionary<string,double>();foreach(var property in typeof(PhotoAdjustments).GetProperties())changes[property.Name]=Value(property.Name,(double)property.GetValue(new PhotoAdjustments())!);
        var adjustments=System.Text.Json.JsonSerializer.Deserialize<PhotoAdjustments>(System.Text.Json.JsonSerializer.Serialize(changes))!;
        return original with{Adjustments=adjustments,Frame=new((int)Value("Padding",80),Value("Corner radius",24),fields.GetValueOrDefault("Background color")?.Text??"#FFFFFF",backdrop.SelectedItem as string??"solid",fields.GetValueOrDefault("Gradient end")?.Text??"#FFD85C",backgroundImage,Value("Background blur"),Value("Shadow radius",24),Value("Shadow opacity",.25),fields.ContainsKey("Canvas width")?(int)Value("Canvas width"):null,fields.ContainsKey("Canvas height")?(int)Value("Canvas height"):null,Value("Image scale",1),Value("Position X",.5),Value("Position Y",.5)),Collage=new(collageLayout.SelectedItem as string??"grid",(int)Value("Columns",2),(int)Value("Canvas width",2048),(int)Value("Canvas height",2048),(int)Value("Spacing",12),(int)Value("Padding",16),Value("Corner radius"),fields.GetValueOrDefault("Background color")?.Text??"#FFFFFF",true,imageOrder.Items.Count>0?imageOrder.Items.Cast<ListBoxItem>().Select(i=>Array.IndexOf(collageInputs,(string)i.Tag)).ToArray():null),Redactions=photoRegions.ToArray(),RedactionStyle=redactionStyle.SelectedItem as string??"solid"};
    }
    private async Task PreviewPhoto()
    {
        if(bitmap is null||!IsVisible)return;
        try
        {
            var settings=PhotoSettings(new());BitmapSource result;
            if(tool=="editImage"){var png=ImageEngine.PngBytes(bitmap);result=ImageEngine.FromPng(await Task.Run(()=>PhotoCodec.Adjust(png,settings.Adjustments)));}
            else if(tool=="frameImage")result=ImageEngine.Frame(bitmap,settings.Frame);
            else result=ImageEngine.Collage(collageInputs,settings.Collage,CancellationToken.None);
            if(!IsVisible)return;drawing.Width=result.PixelWidth;drawing.Height=result.PixelHeight;if(drawing.Children[0] is Image image){image.Source=result;image.Width=result.PixelWidth;image.Height=result.PixelHeight;}
        }
        catch(Exception error)when(error is ArgumentException or FormatException or InvalidDataException){ToolTip=error.Message;}
    }
    private async Task DetectPhoto(bool text,bool faces)
    {
        if(bitmap is null)return;try{var regions=await NativePdf.DetectAsync(bitmap,text,faces);foreach(var r in regions){photoRegions.Add(r);var shape=new Rectangle{Width=r.Width,Height=r.Height,Fill=Brushes.Black,Stroke=Brushes.Gold,StrokeThickness=2,IsHitTestVisible=false};Canvas.SetLeft(shape,r.X);Canvas.SetTop(shape,r.Y);drawing.Children.Add(shape);}if(regions.Length==0)MessageBox.Show(this,"No matching regions were detected. You can still draw areas by hand.","Redact Photo");}catch(Exception error){MessageBox.Show(this,error.Message,"Detection unavailable");}
    }
    private void AnnotationStart()
    {
        strokeKind=drawingTool.SelectedIndex;var ink=ImageEngine.ColorBrush(fields["Ink color"].Text);var thickness=Math.Clamp(Value("Stroke width",3),1,100);
        if(strokeKind is 0 or 10){activeStroke=new Polyline{Stroke=ink,Opacity=strokeKind==0?.55:1,StrokeThickness=strokeKind==0?thickness*5:thickness,StrokeLineJoin=PenLineJoin.Round,IsHitTestVisible=false};activeStroke.Points.Add(origin);annotationShape=activeStroke;}
        else if(strokeKind is 2 or 3)annotationShape=new Polyline{Stroke=ink,StrokeThickness=thickness,StrokeLineJoin=PenLineJoin.Round,IsHitTestVisible=false};
        else if(strokeKind==4)annotationShape=new Ellipse{Stroke=ink,StrokeThickness=thickness,IsHitTestVisible=false};
        else if(strokeKind is 5 or 6 or 7)
        {
            var text=strokeKind==6?(markNumber++).ToString():fields["Annotation text"].Text;annotationShape=new Border{Background=strokeKind is 6 or 7?ink:Brushes.Transparent,CornerRadius=new(strokeKind==6?100:8),Padding=new(strokeKind==6?10:4),Child=new TextBlock{Text=text,Foreground=strokeKind is 6 or 7?Brushes.White:ink,FontSize=Math.Clamp(Value("Font size",24),6,500),TextWrapping=TextWrapping.Wrap}};
        }
        else if(strokeKind is 8 or 9)
        {
            if(strokeKind==9&&annotationImage is null){dragging=false;drawing.ReleaseMouseCapture();return;}annotationShape=new Image{Source=strokeKind==9?ImageEngine.Load(annotationImage!):bitmap,Stretch=Stretch.Fill,IsHitTestVisible=false};
        }
        else annotationShape=new Rectangle{Stroke=ink,StrokeThickness=thickness,IsHitTestVisible=false};
        drawing.Children.Add(annotationShape);Canvas.SetLeft(annotationShape,origin.X);Canvas.SetTop(annotationShape,origin.Y);
    }
    private void AnnotationMove(Point point)
    {
        if(annotationShape is null)return;endPoint=point;if(activeStroke is not null){activeStroke.Points.Add(point);return;}
        if(annotationShape is Polyline line)
        {
            Canvas.SetLeft(line,0);Canvas.SetTop(line,0);line.Points=new(){origin,point};if(strokeKind==2){var a=Math.Atan2(point.Y-origin.Y,point.X-origin.X);var size=Math.Max(12,line.StrokeThickness*4);line.Points.Add(new(point.X-size*Math.Cos(a-.5),point.Y-size*Math.Sin(a-.5)));line.Points.Add(point);line.Points.Add(new(point.X-size*Math.Cos(a+.5),point.Y-size*Math.Sin(a+.5)));}return;
        }
        var bounds=new Rect(origin,point);if(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)){var side=Math.Min(bounds.Width,bounds.Height);bounds.Width=side;bounds.Height=side;}
        if(annotationShape is FrameworkElement element){Canvas.SetLeft(element,bounds.X);Canvas.SetTop(element,bounds.Y);element.Width=Math.Max(1,bounds.Width);element.Height=Math.Max(1,bounds.Height);if(strokeKind==8&&element is Image magnifier&&bitmap is not null){var width=Math.Max(1,(int)bounds.Width/2);var height=Math.Max(1,(int)bounds.Height/2);var crop=new Int32Rect(Math.Clamp((int)bounds.X,0,bitmap.PixelWidth-width),Math.Clamp((int)bounds.Y,0,bitmap.PixelHeight-height),width,height);magnifier.Source=new CroppedBitmap(bitmap,crop);}}
    }
}
