using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Nashi.Windows;
public sealed class RangeTimeline:FrameworkElement
{
    public double Duration{get;set;}=1;public double Start{get;private set;}public double End{get;private set;}=1;
    public event Action<double,double>? Changed;
    private bool dragging,startHandle;private double originalStart,originalEnd,originX,laneWidth;
    private BitmapSource[] artwork=[];
    public void SetArtwork(BitmapSource[] images){artwork=images;InvalidateVisual();}
    public RangeTimeline(){Height=54;MinWidth=160;Cursor=Cursors.SizeWE;Focusable=true;}
    public void Set(double duration,double start,double end){Duration=Math.Max(.05,duration);Start=Math.Clamp(start,0,Duration-.05);End=Math.Clamp(end,Start+.05,Duration);InvalidateVisual();}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);var w=Math.Max(1,ActualWidth-24);dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(215,226,204)),null,new Rect(12,8,w,38),8,8);
        if(artwork.Length>0){dc.PushClip(new RectangleGeometry(new Rect(12,8,w,38),8,8));for(var n=0;n<artwork.Length;n++)dc.DrawImage(artwork[n],new Rect(12+w*n/artwork.Length,8,w/artwork.Length,38));dc.Pop();}
        var left=12+w*Start/Duration;var right=12+w*End/Duration;
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(artwork.Length>0?(byte)70:(byte)255,229,189,80)),new Pen(Brushes.Gold,1),new Rect(left,8,Math.Max(4,right-left),38),6,6);
        foreach(var x in new[]{left,right}){dc.DrawRoundedRectangle(Brushes.DarkOliveGreen,null,new Rect(x-7,3,14,48),5,5);dc.DrawLine(new Pen(Brushes.White,2),new Point(x,19),new Point(x,35));}
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();var x=e.GetPosition(this).X;laneWidth=Math.Max(1,ActualWidth-24);var a=12+laneWidth*Start/Duration;var b=12+laneWidth*End/Duration;
        startHandle=Math.Abs(x-a)<=Math.Abs(x-b);originalStart=Start;originalEnd=End;originX=x;dragging=CaptureMouse();e.Handled=true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if(!dragging)return;var delta=(e.GetPosition(this).X-originX)/laneWidth*Duration;
        if(startHandle)Start=Math.Clamp(originalStart+delta,0,originalEnd-.05);else End=Math.Clamp(originalEnd+delta,originalStart+.05,Duration);
        InvalidateVisual();Changed?.Invoke(Start,End);e.Handled=true;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){dragging=false;ReleaseMouseCapture();e.Handled=true;}
    protected override void OnLostMouseCapture(MouseEventArgs e){dragging=false;base.OnLostMouseCapture(e);}
}
