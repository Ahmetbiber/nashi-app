using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
namespace Nashi.Windows;
public sealed record ToolChoice(string Id,string Title,string Symbol);
public sealed class WheelWindow:Window
{
    public WheelWindow(Window owner,IReadOnlyList<ToolChoice> tools,Action<ToolChoice> select)
    {
        Owner=owner;Title="Nashi tools";Width=540;Height=540;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var canvas=new Canvas{Width=540,Height=540};Content=canvas;
        var disc=new Ellipse{Width=530,Height=530,Fill=new SolidColorBrush(Color.FromRgb(30,45,33))};Canvas.SetLeft(disc,5);Canvas.SetTop(disc,5);canvas.Children.Add(disc);
        var count=Math.Min(12,tools.Count);
        for(var i=0;i<count;i++)
        {
            var choice=tools[i];var angle=(-90d+360d*i/count)*Math.PI/180;var half=(180d/count-1)*Math.PI/180;var a=angle-half;var b=angle+half;
            Point P(double r,double v)=>new(270+r*Math.Cos(v),270+r*Math.Sin(v));
            var geometry=new StreamGeometry();using(var g=geometry.Open()){g.BeginFigure(P(108,a),true,true);g.LineTo(P(250,a),true,false);g.ArcTo(P(250,b),new Size(250,250),0,b-a>Math.PI,SweepDirection.Clockwise,true,false);g.LineTo(P(108,b),true,false);g.ArcTo(P(108,a),new Size(108,108),0,b-a>Math.PI,SweepDirection.Counterclockwise,true,false);}geometry.Freeze();
            var path=new System.Windows.Shapes.Path{Data=geometry,Fill=new SolidColorBrush(Color.FromRgb(43,62,45)),Stroke=new SolidColorBrush(Color.FromRgb(72,86,63)),StrokeThickness=1};canvas.Children.Add(path);
            var radial=4*Math.Sin(half)*(Math.Pow(250,3)-Math.Pow(108,3))/(6*half*(250*250-108*108));
            var label=new StackPanel{Width=112};label.Children.Add(new TextBlock{Text=choice.Symbol,FontSize=25,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center});label.Children.Add(new TextBlock{Text=choice.Title,FontSize=13,FontWeight=FontWeights.SemiBold,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.White});
            var button=new Button{Content=label,Width=126,Height=86,Background=Brushes.Transparent,Foreground=Brushes.White,Padding=new(0)};Canvas.SetLeft(button,270+radial*Math.Cos(angle)-63);Canvas.SetTop(button,270+radial*Math.Sin(angle)-43);canvas.Children.Add(button);
            button.Click+=(_,_)=>{Close();select(choice);};button.MouseEnter+=(_,_)=>Feedback.Play();
            path.MouseLeftButtonUp+=(_,_)=>{Close();select(choice);};path.MouseEnter+=(_,_)=>Feedback.Play();
        }
        var center=new Button{Content="Tools\nEsc to close",Width=180,Height=180,Background=Brushes.Transparent,Foreground=Brushes.White};Canvas.SetLeft(center,180);Canvas.SetTop(center,180);canvas.Children.Add(center);center.Click+=(_,_)=>Close();KeyDown+=(_,e)=>{if(e.Key==Key.Escape)Close();};bool closing=false;Closing+=(_,_)=>closing=true;Deactivated+=(_,_)=>{if(!closing&&IsVisible)Close();};
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
