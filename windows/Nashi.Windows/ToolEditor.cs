using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Nashi.Core;
namespace Nashi.Windows;

public sealed class ToolEditor:Window
{
    public MediaOptions MediaOptions{get;private set;}=new();public ImageOptions ImageOptions{get;private set;}=new();public string Format{get;private set;}="png";public int[] Pages{get;private set;}=[];
    private readonly string tool;private readonly BitmapSource? bitmap;private readonly Dictionary<string,TextBox> fields=[];private readonly Dictionary<string,string> defaults=[];
    private readonly ComboBox format=new();private readonly ComboBox drawingTool=new(){ItemsSource=new[]{"Marker","Rectangle"},SelectedIndex=0};
    private readonly Canvas drawing=new(){Background=Brushes.Transparent,Focusable=true};private Rectangle? selection,activeRectangle;private Polyline? activeStroke;private Point origin;private bool dragging;
    private readonly MediaElement player=new(){LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Close,Stretch=Stretch.Uniform,Height=260};private bool playing;
    private readonly RangeTimeline timeline=new();private readonly TextBlock time=new(){Margin=new(4,6,4,10)};private double duration=1;
    internal double PreviewSeconds=>player.Position.TotalSeconds;
    internal bool Playing=>playing;
    internal RangeTimeline Timeline=>timeline;
    internal TextBox FieldForTest(string name)=>fields[name];
    private readonly string input;private readonly MediaInfo? media;private readonly StackPanel options=new(){Margin=new(18,0,0,0)};
    public ToolEditor(Window owner,string tool,string title,string input,MediaInfo? media=null,BitmapSource? preview=null)
    {
        Theme.Attach(this);
        Owner=owner;this.tool=tool;this.input=input;this.media=media;bitmap=preview;Title=title+" · Nashi";Width=1060;Height=780;MinWidth=800;MinHeight=560;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var grid=new Grid{Margin=new(28)};grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});Content=grid;
        var heading=new StackPanel();heading.Children.Add(new TextBlock{Text=title,FontSize=30,FontWeight=FontWeights.Bold});heading.Children.Add(new TextBlock{Text=Path.GetFileName(input)+"  ·  Original stays safe",Foreground=Theme.Brush("Muted"),Margin=new(0,5,0,22)});grid.Children.Add(heading);
        var body=new Grid();body.ColumnDefinitions.Add(new(){Width=new(1.6,GridUnitType.Star)});body.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});Grid.SetRow(body,1);grid.Children.Add(body);
        var previewStack=new StackPanel();previewStack.Children.Add(new TextBlock{Text="PREVIEW",FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=Theme.Brush("Muted"),Margin=new(4,0,0,16)});options.Children.Add(new TextBlock{Text="MAKE IT YOURS",FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=Theme.Brush("Muted"),Margin=new(4,0,0,16)});body.Children.Add(new ScrollViewer{Content=previewStack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Grid.SetColumn(options,1);body.Children.Add(new ScrollViewer{Content=options,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Grid.SetColumn(body.Children[^1],1);
        if(bitmap is not null)
        {
            drawing.Width=bitmap.PixelWidth;drawing.Height=bitmap.PixelHeight;
            drawing.Children.Add(new Image{Source=bitmap,Width=bitmap.PixelWidth,Height=bitmap.PixelHeight});
            var viewbox=new Viewbox{Child=drawing,Stretch=Stretch.Uniform,MaxHeight=420};viewbox.SizeChanged+=(_,_)=>viewbox.Clip=new RectangleGeometry(new Rect(0,0,viewbox.ActualWidth,viewbox.ActualHeight),14,14);previewStack.Children.Add(viewbox);
            drawing.MouseLeftButtonDown+=BeginDraw;drawing.MouseMove+=MoveDraw;drawing.MouseLeftButtonUp+=EndDraw;
        }
        else if(media is not null)
        {
            player.Source=new Uri(input);previewStack.Children.Add(player);player.MediaOpened+=(_,_)=>{if(player.NaturalDuration.HasTimeSpan)duration=player.NaturalDuration.TimeSpan.TotalSeconds;};
            player.MediaFailed+=(_,_)=>time.Text="Windows cannot preview this codec. Export still uses FFmpeg.";
        }
        else previewStack.Children.Add(new TextBlock{Text="Your files will be saved as new copies.\nChoose options, then save.",FontSize=21,TextWrapping=TextWrapping.Wrap,Margin=new(0,50,20,0)});
        duration=media?.Duration??1;
        if(media is not null)
        {
            if(tool is "trimVideo" or "trimAudio" or "redactAudio")
            {
                timeline.Set(duration,0,duration);previewStack.Children.Add(timeline);timeline.Changed+=(a,b)=>{fields["Start"].Text=Number(a);fields["End"].Text=Number(b);Seek(a);};
            }
            var controls=new StackPanel{Orientation=Orientation.Horizontal};var play=MainWindow.Button("▶ / Ⅱ",()=>{if(playing){player.Pause();playing=false;}else{player.Play();playing=true;}});controls.Children.Add(MainWindow.Button("−1s",()=>Seek(player.Position.TotalSeconds-1)));controls.Children.Add(play);controls.Children.Add(MainWindow.Button("+1s",()=>Seek(player.Position.TotalSeconds+1)));previewStack.Children.Add(controls);previewStack.Children.Add(time);
            previewStack.Children.Add(new TextBlock{Text="Space play/pause · ←/→ 1s · Shift+←/→ 1/30s · I/O set range · Enter save",TextWrapping=TextWrapping.Wrap,Foreground=Theme.Brush("Muted")});
        }
        if(tool=="convert")
        {
            var formats=media is not null?(media.Video is null?MediaEngine.AudioFormats:MediaEngine.VideoFormats.Concat(MediaEngine.AudioFormats).ToHashSet()):ImageEngine.Formats.ToHashSet();
            options.Children.Add(new TextBlock{Text="Output format",FontWeight=FontWeights.SemiBold});format.ItemsSource=formats.Order().ToArray();format.SelectedIndex=0;options.Children.Add(format);
        }
        if(tool is "trimVideo" or "trimAudio" or "redactAudio"){Field("Start",0);Field("End",duration);}
        if(tool=="resize"){Field("Width",bitmap?.PixelWidth??1280);Field("Height",bitmap?.PixelHeight??720);}
        if(tool=="compress")Field("Quality (%)",80);
        if(tool=="changeVideoSpeed")Field("Speed",1);
        if(tool=="splitVideo")Field("Number of clips",2);
        if(tool=="videoSnapshots")TextField("Times (seconds)","0");
        if(tool=="normalizeAudio"){Field("Target LUFS",-16);Field("True peak dB",-1.5);}
        if(tool=="audioChannels"){Field("Channels",2);Field("Left volume",1);Field("Right volume",1);}
        if(tool=="audioToVideo"){Field("Width",1280);Field("Height",720);}
        if(tool=="organizePDF")TextField("Page numbers","1");
        if(tool=="rotate")Field("Degrees",90);
        if(tool is "crop" or "cropVideo" or "redactVideo")
        {
            options.Children.Add(new TextBlock{Text=tool=="redactVideo"?"Drag a region to cover with a solid block.":"Drag to select a crop. Hold Shift to lock the original aspect ratio. Press Enter to save.",TextWrapping=TextWrapping.Wrap,Margin=new(4,4,4,16)});
            var w=bitmap?.PixelWidth??media?.Video?.Width??1280;var h=bitmap?.PixelHeight??media?.Video?.Height??720;
            Field("X",0);Field("Y",0);Field("Width",w);Field("Height",h);
            if(tool=="redactVideo"){Field("Start",0);Field("End",duration);}
        }
        if(tool=="annotate")
        {
            options.Children.Add(new TextBlock{Text="Drawing tool"});options.Children.Add(drawingTool);options.Children.Add(new TextBlock{Text="Drag to draw. Marker and rectangle are mutually exclusive.",TextWrapping=TextWrapping.Wrap,Margin=new(4,12,4,12)});
            options.Children.Add(MainWindow.Button("Undo last mark",()=>{if(drawing.Children.Count>1)drawing.Children.RemoveAt(drawing.Children.Count-1);}));
        }
        if(options.Children.Count==1)options.Children.Add(new TextBlock{Text="Ready to make a fresh copy. No extra settings needed.",TextWrapping=TextWrapping.Wrap,Margin=new(4,20,4,0)});
        var footer=new DockPanel{Margin=new(0,20,0,0),LastChildFill=false};Grid.SetRow(footer,2);grid.Children.Add(footer);var save=MainWindow.Button("Save new copy  ↗",Save);save.Background=Theme.Brush("Gold");save.Foreground=new SolidColorBrush(Color.FromRgb(43,54,28));DockPanel.SetDock(save,Dock.Right);footer.Children.Add(save);var cancel=MainWindow.Button("Cancel",()=>DialogResult=false);DockPanel.SetDock(cancel,Dock.Left);footer.Children.Add(cancel);var reset=MainWindow.Button("↶ Reset",Reset);DockPanel.SetDock(reset,Dock.Left);footer.Children.Add(reset);var safe=new TextBlock{Text="Original stays safe",Foreground=Theme.Brush("Muted"),FontSize=12,VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,16,0)};DockPanel.SetDock(safe,Dock.Right);footer.Children.Add(safe);
        var clock=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        clock.Tick+=(_,_)=>{if(playing)time.Text=$"{player.Position:mm\\:ss\\.fff} / {TimeSpan.FromSeconds(duration):mm\\:ss\\.fff}";};
        player.MediaEnded+=(_,_)=>{playing=false;player.Pause();};clock.Start();
        PreviewKeyDown+=OnKey;Closed+=(_,_)=>{clock.Stop();player.Close();};
    }
    private static string Number(double n)=>n.ToString("0.###",CultureInfo.CurrentCulture);
    private void Field(string name,double value)=>TextField(name,Number(value));
    private void TextField(string name,string value){options.Children.Add(new TextBlock{Text=name,Margin=new(4,12,4,0)});var box=new TextBox{Text=value};System.Windows.Automation.AutomationProperties.SetAutomationId(box,name);if(name is "Start" or "End")box.LostKeyboardFocus+=(_,_)=>{try{SyncTimeline();}catch(ArgumentException){}};fields.Add(name,box);defaults.Add(name,value);options.Children.Add(box);}
    private void Reset(){foreach(var pair in defaults)fields[pair.Key].Text=pair.Value;while(drawing.Children.Count>1)drawing.Children.RemoveAt(drawing.Children.Count-1);selection=null;activeRectangle=null;activeStroke=null;timeline.Set(duration,0,duration);if(media is not null)Seek(0);}
    private double Value(string key,double fallback=0)
    {
        if(!fields.TryGetValue(key,out var box))return fallback;
        if(!double.TryParse(box.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out var value)&&!double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||!double.IsFinite(value))throw new ArgumentException("Enter a valid number for "+key+".");return value;
    }
    private void BeginDraw(object sender,MouseButtonEventArgs e)
    {
        if(tool is not ("crop" or "cropVideo" or "redactVideo" or "annotate"))return;
        drawing.Focus();origin=e.GetPosition(drawing);dragging=drawing.CaptureMouse();
        if(tool=="annotate"&&drawingTool.SelectedIndex==0){activeStroke=new Polyline{Stroke=new SolidColorBrush(Color.FromArgb(160,234,181,40)),StrokeThickness=Math.Max(3,drawing.Width/100),StrokeLineJoin=PenLineJoin.Round};activeStroke.Points.Add(origin);drawing.Children.Add(activeStroke);}
        else
        {
            if(tool!="annotate"&&selection is not null)drawing.Children.Remove(selection);
            activeRectangle=new Rectangle{Stroke=tool=="annotate"?Brushes.OrangeRed:Brushes.Gold,StrokeThickness=Math.Max(2,drawing.Width/350),Fill=tool=="redactVideo"?Brushes.Black:new SolidColorBrush(Color.FromArgb(35,224,189,82)),IsHitTestVisible=false};drawing.Children.Add(activeRectangle);Canvas.SetLeft(activeRectangle,origin.X);Canvas.SetTop(activeRectangle,origin.Y);if(tool!="annotate")selection=activeRectangle;
        }
        e.Handled=true;
    }
    private void MoveDraw(object sender,MouseEventArgs e)
    {
        if(!dragging)return;var p=e.GetPosition(drawing);p.X=Math.Clamp(p.X,0,drawing.Width);p.Y=Math.Clamp(p.Y,0,drawing.Height);
        if(activeStroke is not null){activeStroke.Points.Add(p);return;}
        if(activeRectangle is null)return;var dx=p.X-origin.X;var dy=p.Y-origin.Y;
        if(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            var ratio=drawing.Width/drawing.Height;var width=Math.Min(Math.Abs(dx),Math.Abs(dy)*ratio);var height=width/ratio;p=new(origin.X+Math.CopySign(width,dx),origin.Y+Math.CopySign(height,dy));
        }
        var x=Math.Min(origin.X,p.X);var y=Math.Min(origin.Y,p.Y);var w=Math.Abs(p.X-origin.X);var h=Math.Abs(p.Y-origin.Y);
        Canvas.SetLeft(activeRectangle,x);Canvas.SetTop(activeRectangle,y);activeRectangle.Width=w;activeRectangle.Height=h;
        if(tool!="annotate"){fields["X"].Text=Number(Math.Floor(x));fields["Y"].Text=Number(Math.Floor(y));fields["Width"].Text=Number(Math.Max(1,Math.Floor(w)));fields["Height"].Text=Number(Math.Max(1,Math.Floor(h)));}
    }
    private void EndDraw(object sender,MouseButtonEventArgs e){dragging=false;activeStroke=null;activeRectangle=null;drawing.ReleaseMouseCapture();e.Handled=true;}
    private void Seek(double seconds){player.Position=TimeSpan.FromSeconds(Math.Clamp(seconds,0,duration));time.Text=$"{player.Position:mm\\:ss\\.fff} / {TimeSpan.FromSeconds(duration):mm\\:ss\\.fff}";}
    private void OnKey(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Escape){Close();e.Handled=true;return;}
        if(ClipboardImport.IsEditing(Keyboard.FocusedElement as DependencyObject))return;if(e.Key==Key.Enter){Save();e.Handled=true;return;}
        if(media is null||Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift))return;
        switch(e.Key){case Key.Space:if(playing)player.Pause();else player.Play();playing=!playing;break;case Key.Left:Seek(player.Position.TotalSeconds-(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?1d/30:1));break;case Key.Right:Seek(player.Position.TotalSeconds+(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?1d/30:1));break;case Key.I:if(fields.TryGetValue("Start",out var start)&&player.Position.TotalSeconds<Value("End",duration))start.Text=Number(player.Position.TotalSeconds);SyncTimeline();break;case Key.O:if(fields.TryGetValue("End",out var end)&&player.Position.TotalSeconds>Value("Start"))end.Text=Number(player.Position.TotalSeconds);SyncTimeline();break;default:return;}e.Handled=true;
    }
    private void SyncTimeline(){if(fields.ContainsKey("Start")&&fields.ContainsKey("End"))timeline.Set(duration,Value("Start"),Value("End",duration));}
    private void Save()
    {
        try
        {
            if(tool is "trimVideo" or "trimAudio" or "redactAudio" && (Value("Start")<0||Value("End")>duration||Value("Start")>=Value("End")))throw new ArgumentException("Start must be before End, within the media duration.");
            var x=Value("X");var y=Value("Y");var w=Value("Width");var h=Value("Height");
            if(tool is "crop" or "cropVideo" or "redactVideo"&& (w<1||h<1))throw new ArgumentException("Drag an area first.");
            MediaOptions=new(){Start=Value("Start"),End=fields.ContainsKey("End")?Value("End"):null,Quality=Value("Quality (%)",80)/100,Speed=Value("Speed",1),Width=fields.ContainsKey("Width")?(int)w:null,Height=fields.ContainsKey("Height")?(int)h:null,Channels=(int)Value("Channels",2),LeftVolume=Value("Left volume",1),RightVolume=Value("Right volume",1),EqualClips=(int)Value("Number of clips",2),TargetLoudness=Value("Target LUFS",-16),TruePeak=Value("True peak dB",-1.5),Regions=tool is "cropVideo" or "redactVideo"?[new(x,y,w,h,Value("Start"),fields.ContainsKey("End")?Value("End"):null)]:[],AudioRedactions=tool=="redactAudio"?[new(Value("Start"),Value("End"))]:[],SnapshotTimes=tool=="videoSnapshots"?fields["Times (seconds)"].Text.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(s=>double.Parse(s,CultureInfo.CurrentCulture)).ToArray():[0]};
            BitmapSource? annotated=null;if(tool=="annotate"){var target=new RenderTargetBitmap(bitmap!.PixelWidth,bitmap.PixelHeight,96,96,PixelFormats.Pbgra32);target.Render(drawing);target.Freeze();annotated=target;}
            ImageOptions=new(fields.ContainsKey("Width")?(int)w:null,fields.ContainsKey("Height")?(int)h:null,(int)Value("Degrees",90),tool=="crop"?new Int32Rect((int)x,(int)y,(int)w,(int)h):null,(int)Value("Quality (%)",85),true,annotated);
            Format=format.SelectedItem as string??"png";
            if(tool=="organizePDF")Pages=fields["Page numbers"].Text.Split(',',StringSplitOptions.RemoveEmptyEntries).Select(s=>int.Parse(s.Trim(),CultureInfo.InvariantCulture)).ToArray();
            DialogResult=true;
        }
        catch(Exception error)when(error is ArgumentException or FormatException or OverflowException){MessageBox.Show(this,error.Message,"Check your settings",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
}
