using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
using Nashi.Core;
namespace Nashi.Windows;
public sealed record ToolChoice(string Id,string Title,string Symbol,WindowPlacement? Placement=null);
public sealed class WheelWindow:Window
{
    private readonly List<Button> buttons=[];
    private readonly List<System.Windows.Shapes.Path> sectors=[];
    private readonly IReadOnlyList<ToolChoice> choices;
    private readonly Action<ToolChoice> select;
    private readonly Canvas surface;
    private int selected=-1;
    internal int SelectedIndex=>selected;
    internal void HoverScreen(Point point)
    {
        var local=surface.PointFromScreen(point);var delta=local-new Point(270,270);var distance=delta.Length;
        if(distance<108||distance>250){Highlight(-1);return;}
        var angle=(Math.Atan2(delta.Y,delta.X)*180/Math.PI+450)%360;
        var index=(int)Math.Floor((angle+180d/choices.Count)%360/(360d/choices.Count));
        Highlight(index);
    }
    internal bool Navigate(Key key)
    {
        if(key==Key.Escape){Close();return true;}
        if(buttons.Count==0)return false;
        if(key is Key.Right or Key.Down){Highlight((selected+1)%buttons.Count);buttons[selected].Focus();return true;}
        if(key is Key.Left or Key.Up){Highlight((selected-1+buttons.Count)%buttons.Count);buttons[selected].Focus();return true;}
        if(key is Key.Enter or Key.Space){var choice=choices[Math.Max(0,selected)];Close();select(choice);return true;}
        return false;
    }
    private void Highlight(int index)
    {
        if(selected==index)return;selected=index;
        for(var i=0;i<sectors.Count;i++){sectors[i].Fill=i==index?Theme.Brush("Sage"):Theme.Brush("Inset");sectors[i].Stroke=i==index?Theme.Brush("Gold"):Theme.Brush("Line");sectors[i].StrokeThickness=i==index?2:1;}
        if(index>=0)Feedback.Play();
    }
    public WheelWindow(Window? owner,IReadOnlyList<ToolChoice> tools,Action<ToolChoice> select,bool externalDrag=false)
    {
        this.choices=tools;this.select=select;if(owner is not null)Owner=owner;Title=externalDrag?"Nashi Window Management":"Nashi tools";Width=540;Height=540;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;WindowStartupLocation=externalDrag?WindowStartupLocation.Manual:WindowStartupLocation.CenterOwner;
        if(externalDrag){ShowActivated=false;Topmost=true;Focusable=false;}
        var canvas=new Canvas{Width=540,Height=540};surface=canvas;KeyboardNavigation.SetTabNavigation(canvas,KeyboardNavigationMode.Cycle);Content=externalDrag?new Viewbox{Child=canvas,Stretch=Stretch.Uniform}:canvas;
        var disc=new Ellipse{Width=530,Height=530,Fill=Theme.Brush("Surface")};Canvas.SetLeft(disc,5);Canvas.SetTop(disc,5);canvas.Children.Add(disc);
        var count=Math.Min(12,tools.Count);
        for(var i=0;i<count;i++)
        {
            var choice=tools[i];var angle=(-90d+360d*i/count)*Math.PI/180;var half=(180d/count-1)*Math.PI/180;var a=angle-half;var b=angle+half;
            Point P(double r,double v)=>new(270+r*Math.Cos(v),270+r*Math.Sin(v));
            var geometry=new StreamGeometry();using(var g=geometry.Open()){g.BeginFigure(P(108,a),true,true);g.LineTo(P(250,a),true,false);g.ArcTo(P(250,b),new Size(250,250),0,b-a>Math.PI,SweepDirection.Clockwise,true,false);g.LineTo(P(108,b),true,false);g.ArcTo(P(108,a),new Size(108,108),0,b-a>Math.PI,SweepDirection.Counterclockwise,true,false);}geometry.Freeze();
            var path=new System.Windows.Shapes.Path{Data=geometry,Fill=Theme.Brush("Inset"),Stroke=Theme.Brush("Line"),StrokeThickness=1};canvas.Children.Add(path);sectors.Add(path);
            var radial=4*Math.Sin(half)*(Math.Pow(250,3)-Math.Pow(108,3))/(6*half*(250*250-108*108));
            var label=new StackPanel{Width=112};
            if(choice.Placement is {} placement)
            {
                var glyph=new Canvas{Width=36,Height=25,HorizontalAlignment=HorizontalAlignment.Center,Margin=new(0,0,0,5)};
                glyph.Children.Add(new Rectangle{Width=35,Height=24,RadiusX=3,RadiusY=3,Stroke=Theme.Brush("Ink"),StrokeThickness=1.7});
                var (start,end)=placement.Range();var fill=new Rectangle{Width=29*(end-start),Height=18,Fill=Theme.Brush("Ink"),Opacity=.65};Canvas.SetLeft(fill,3+29*start);Canvas.SetTop(fill,3);glyph.Children.Add(fill);label.Children.Add(glyph);
            }
            else label.Children.Add(new TextBlock{Text=choice.Symbol,FontSize=25,Foreground=Theme.Brush("Ink"),HorizontalAlignment=HorizontalAlignment.Center});
            label.Children.Add(new TextBlock{Text=choice.Title,FontSize=13,FontWeight=FontWeights.SemiBold,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,Foreground=Theme.Brush("Ink")});
            var button=new Button{Style=(Style)Application.Current.FindResource("WheelLabelButton"),Content=label,Width=126,Height=86,Background=Brushes.Transparent,Foreground=Theme.Brush("Ink"),Padding=new(0),BorderThickness=new(0)};Canvas.SetLeft(button,270+radial*Math.Cos(angle)-63);Canvas.SetTop(button,270+radial*Math.Sin(angle)-43);canvas.Children.Add(button);buttons.Add(button);var index=i;
            button.Click+=(_,_)=>{Close();select(choice);};button.MouseEnter+=(_,_)=>Highlight(index);button.GotKeyboardFocus+=(_,_)=>Highlight(index);
            path.MouseLeftButtonUp+=(_,_)=>{Close();select(choice);};path.MouseEnter+=(_,_)=>Highlight(index);
        }
        var centerLabel=new StackPanel();centerLabel.Children.Add(new TextBlock{Text=externalDrag?"Arrange window":"Tools",FontSize=externalDrag?17:20,FontWeight=FontWeights.SemiBold,TextAlignment=TextAlignment.Center});centerLabel.Children.Add(new TextBlock{Text=externalDrag?"Release to place · Esc cancels":"Esc to close",FontSize=externalDrag?9:11,TextAlignment=TextAlignment.Center,Foreground=Theme.Brush("Muted"),Margin=new(0,6,0,0)});
        var center=new Button{Style=(Style)Application.Current.FindResource("WheelLabelButton"),Focusable=false,Content=centerLabel,Width=180,Height=180,Background=Brushes.Transparent,Foreground=Theme.Brush("Ink")};Canvas.SetLeft(center,180);Canvas.SetTop(center,180);canvas.Children.Add(center);center.Click+=(_,_)=>Close();PreviewKeyDown+=(_,e)=>{e.Handled=Navigate(e.Key);};Loaded+=(_,_)=>{if(!externalDrag&&buttons.Count>0)buttons[0].Focus();};bool closing=false;Closing+=(_,_)=>closing=true;Deactivated+=(_,_)=>{if(!externalDrag&&!closing&&IsVisible)Close();};
    }
}
internal static class Feedback
{
    private static MediaPlayer? player;private static DateTime last;
    public static void Play()
    {
        if((DateTime.UtcNow-last).TotalMilliseconds<70)return;last=DateTime.UtcNow;
        try
        {
            var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nashi");Directory.CreateDirectory(directory);var file=Path.Combine(directory,"WheelSelection.wav");
            if(!File.Exists(file)){using var source=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/WheelSelection.wav")).Stream;using var target=File.Create(file);source.CopyTo(target);}
            player??=new MediaPlayer();player.Open(new Uri(file));player.Volume=.35;player.Play();
        }catch(IOException){}catch(InvalidOperationException){}
    }
}
