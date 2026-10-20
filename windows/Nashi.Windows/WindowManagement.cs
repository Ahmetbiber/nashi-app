using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Nashi.Core;

namespace Nashi.Windows;

// WinEvent reports native window moves/resizes; file, text and browser-tab drags do not trigger it.
internal sealed class WindowManagement:IDisposable
{
    private readonly Native.EventCallback callback;
    private readonly nint hook;
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(25)};
    private nint target;
    private Native.Point origin;
    private WheelWindow? wheel;
    private Window? preview;
    private bool cancelled;
    private bool disposed;private volatile bool wheelReady;
    internal bool WheelReady=>wheelReady;
    private readonly Action<string> notice;
    private readonly Func<bool> enabled;
    internal Action<string>? Trace { get; set; }
    private static readonly WindowPlacement[] placements=Enum.GetValues<WindowPlacement>();
    internal WheelWindow? ActiveWheel=>wheel;
    internal bool PlacementPending { get; private set; }

    internal WindowManagement(Action<string>? notice=null,Func<bool>? isEnabled=null)
    {
        this.notice=notice??(_=>{});enabled=isEnabled??(()=>true);
        var dispatcher=Dispatcher.CurrentDispatcher;
        callback=(handle,type,window,objectId,childId,thread,time)=>
        {
            if(disposed||dispatcher.HasShutdownStarted)return;Native.GetCursorPos(out var point);var mouseDown=(Native.GetAsyncKeyState(1)&0x8000)!=0;var escapeDown=(Native.GetAsyncKeyState(0x1B)&0x8000)!=0;
            // Native WinEvent callbacks can reenter window teardown. Return to
            // Windows before creating/closing WPF HWNDs; snapshot Escape now.
            dispatcher.BeginInvoke(DispatcherPriority.Input,()=>OnWindowEvent(handle,type,window,objectId,childId,thread,time,point,mouseDown,escapeDown));
        };
        hook=Native.SetWinEventHook(0x000A,0x000B,0,callback,0,0,0); // MOVESIZESTART / MOVESIZEEND
        timer.Tick+=(_,_)=>Tick();
    }
    private void OnWindowEvent(nint handle,uint type,nint window,int objectId,int childId,uint thread,uint time,Native.Point capturedPoint,bool mouseDown,bool escapeDown)
    {
        if(disposed||objectId!=0||childId!=0||window==0)return;
        if(type==0x000A)
        {
            Clear();if(!enabled()||!Native.IsWindowVisible(window)||Native.IsIconic(window))return;
            if(!mouseDown)return; // Never intercept keyboard-initiated system moves.
            target=window;origin=capturedPoint;cancelled=false;timer.Start();Trace?.Invoke($"start pointer={origin.X},{origin.Y}");
        }
        else if(type==0x000B&&window==target)
        {
            // Escape can end the native move loop before the next timer tick sees it.
            if(escapeDown)cancelled=true;
            var point=capturedPoint;wheel?.HoverScreen(new(point.X,point.Y));
            var selected=wheel?.SelectedIndex??-1;var captured=target;
            var choice=selected>=0&&selected<placements.Length&&!cancelled?placements[selected]:(WindowPlacement?)null;
            var work=WorkArea(point);Trace?.Invoke($"end pointer={point.X},{point.Y} selected={selected} cancelled={cancelled}");Clear();
            if(choice is {} placement)
            {
                PlacementPending=true;
                var apply=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};
                apply.Tick+=(_,_)=>{apply.Stop();PlacementPending=false;if(disposed||!Native.IsWindow(captured))return;var result=Apply(captured,placement.Frame(work),placement);Trace?.Invoke($"apply {placement} success={result} actual={Bounds(captured)}");if(!result)notice("This window could not be arranged. The app may restrict resizing.");};apply.Start();
            }
        }
    }
    private void Tick()
    {
        if(!enabled()||target==0||!Native.IsWindow(target)||Native.IsIconic(target)){Clear();return;}
        if((Native.GetAsyncKeyState(0x1B)&0x8000)!=0){cancelled=true;HideOverlays();return;}
        if(cancelled)return;
        Native.GetCursorPos(out var point);
        var moved=Math.Abs(point.X-origin.X)+Math.Abs(point.Y-origin.Y)>4;
        var requested=(Native.GetAsyncKeyState(0x10)&0x8000)!=0||(Native.GetAsyncKeyState(0x12)&0x8000)!=0;
        if(wheel is null&&moved&&requested)ShowWheel(point);
        if(wheel is null)return;
        wheel.HoverScreen(new(point.X,point.Y));
        var index=wheel.SelectedIndex;
        if(index<0){preview?.Hide();return;}
        var frame=placements[index].Frame(WorkArea(point));
        if(preview is null)
        {
            preview=new Window{Title="Nashi Placement Preview",WindowStyle=WindowStyle.None,AllowsTransparency=true,
                Background=Brushes.Transparent,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Focusable=false};
            preview.Content=new Border{Background=new SolidColorBrush(Color.FromArgb(35,225,189,99)),BorderBrush=Theme.Brush("Gold"),BorderThickness=new(2),CornerRadius=new(14),Margin=new(4)};
            PassThrough(preview);
        }
        if(!preview.IsVisible)preview.Show();Position(preview,frame);
        _=Native.SetWindowPos(new WindowInteropHelper(wheel).Handle,new nint(-1),0,0,0,0,0x10|0x1|0x2);
    }
    private void ShowWheel(Native.Point point)
    {
        wheel=new WheelWindow(null,placements.Select(p=>new ToolChoice(p.ToString(),p.Title(),"",p)).ToArray(),_=>{},externalDrag:true);
        PassThrough(wheel);wheel.Show();
        var dpi=Native.GetDpiForWindow(target);var size=(int)Math.Round(WheelPresentation.Diameter*(dpi>0?dpi:96)/96d);
        var work=WorkArea(point);
        var x=Math.Clamp(point.X-size/2,work.X+4,Math.Max(work.X+4,work.Right-size-4));
        var y=Math.Clamp(point.Y-size/2,work.Y+4,Math.Max(work.Y+4,work.Bottom-size-4));
        Position(wheel,new(x,y,size,size));wheelReady=true;Trace?.Invoke($"wheel desired={x},{y},{size} actual={Bounds(new WindowInteropHelper(wheel).Handle)}");
    }
    private static void PassThrough(Window window)
    {
        window.SourceInitialized+=(_,_)=>
        {
            var handle=new WindowInteropHelper(window).Handle;
            var style=Native.GetWindowLongPtr(handle,-20).ToInt64();
            _=Native.SetWindowLongPtr(handle,-20,new nint(style|0x08000000|0x20|0x80));
        };
    }
    private static void Position(Window window,WindowFrame frame)
        =>Native.SetWindowPos(new WindowInteropHelper(window).Handle,new nint(-1),frame.X,frame.Y,frame.Width,frame.Height,0x10|0x40);
    private void HideOverlays(){wheelReady=false;wheel?.Close();wheel=null;preview?.Close();preview=null;}
    private void Clear(){timer.Stop();HideOverlays();target=0;cancelled=false;}
    public void Dispose(){if(disposed)return;disposed=true;Clear();if(hook!=0)Native.UnhookWinEvent(hook);}

    internal static WindowFrame WorkArea(Native.Point point)
    {
        var monitor=Native.MonitorFromPoint(point,2);var info=new Native.MonitorInfo{Size=Marshal.SizeOf<Native.MonitorInfo>()};
        if(!Native.GetMonitorInfo(monitor,ref info))throw new InvalidOperationException("Display work area could not be read.");
        var r=info.Work;return new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);
    }
    internal static WindowFrame Bounds(nint handle)
    {
        if(!Native.GetWindowRect(handle,out var r))return default;
        if(Native.DwmGetWindowAttribute(handle,9,out var visible,Marshal.SizeOf<Native.Rect>())==0)r=visible;
        return new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);
    }
    internal static bool Apply(nint handle,WindowFrame frame,WindowPlacement? placement=null)
    {
        if(frame.Width<=0||frame.Height<=0||!Native.IsWindow(handle)||!Native.GetWindowRect(handle,out var raw))return false;
        var visual=Bounds(handle);var left=visual.X-raw.Left;var top=visual.Y-raw.Top;
        var right=raw.Right-visual.Right;var bottom=raw.Bottom-visual.Bottom;
        if(!Native.SetWindowPos(handle,0,frame.X-left,frame.Y-top,frame.Width+left+right,frame.Height+top+bottom,0x10|0x4))return false;
        var observed=Bounds(handle);
        if(placement is {} choice&&observed.Width>frame.Width+3)
        {
            var work=WorkArea(new Native.Point{X=frame.X+frame.Width/2,Y=frame.Y+frame.Height/2});
            var fitted=choice.Frame(work,observed.Width);
            return Apply(handle,fitted); // One bounded retry with the app's real minimum width.
        }
        return Native.GetWindowRect(handle,out var actual)&&Math.Abs(actual.Left-(frame.X-left))<3&&Math.Abs(actual.Top-(frame.Y-top))<3&&Math.Abs(actual.Right-actual.Left-(frame.Width+left+right))<3&&Math.Abs(actual.Bottom-actual.Top-(frame.Height+top+bottom))<3;
    }

    internal static class Native
    {
        internal delegate void EventCallback(nint hook,uint type,nint window,int objectId,int childId,uint thread,uint time);
        [StructLayout(LayoutKind.Sequential)]internal struct Point{internal int X,Y;}
        [StructLayout(LayoutKind.Sequential)]internal struct Rect{internal int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)]internal struct MonitorInfo{internal int Size;internal Rect Monitor,Work;internal uint Flags;}
        [DllImport("user32.dll")]internal static extern nint SetWinEventHook(uint min,uint max,nint module,EventCallback callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")]internal static extern bool UnhookWinEvent(nint hook);
        [DllImport("user32.dll")]internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")]internal static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")]internal static extern bool IsWindow(nint handle);
        [DllImport("user32.dll")]internal static extern bool IsWindowVisible(nint handle);
        [DllImport("user32.dll")]internal static extern bool IsIconic(nint handle);
        [DllImport("user32.dll")]internal static extern bool GetWindowRect(nint handle,out Rect rect);
        [DllImport("user32.dll")]internal static extern bool SetWindowPos(nint handle,nint after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]internal static extern nint GetWindowLongPtr(nint handle,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]internal static extern nint SetWindowLongPtr(nint handle,int index,nint value);
        [DllImport("user32.dll")]internal static extern uint GetDpiForWindow(nint handle);
        [DllImport("user32.dll")]internal static extern nint MonitorFromPoint(Point point,uint flags);
        [DllImport("user32.dll",EntryPoint="GetMonitorInfoW")]internal static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
        [DllImport("dwmapi.dll")]internal static extern int DwmGetWindowAttribute(nint handle,uint attribute,out Rect value,int size);
    }
}
