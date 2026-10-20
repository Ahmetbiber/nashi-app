using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nashi.Core;
namespace Nashi.Windows;

internal static class WindowManagementSmoke
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint FindWindow(string? type,string title);
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern void mouse_event(uint flags,uint x,uint y,uint data,nuint extra);
    [DllImport("user32.dll")]private static extern void keybd_event(byte key,byte scan,uint flags,nuint extra);

    internal static void Run(string output,Action<bool,string> check)
    {
        using var manager=new WindowManagement();
        var trace=Path.Combine(output,"window-management-trace.txt");
        manager.Trace=message=>File.AppendAllText(trace,DateTime.UtcNow.ToString("O")+" "+message+Environment.NewLine);
        using var fixture=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,ArgumentList={"--window-management-fixture"}})??throw new Exception("Window fixture could not start.");
        nint handle=0;var deadline=DateTime.UtcNow.AddSeconds(10);
        while(handle==0&&DateTime.UtcNow<deadline){DesktopInput.Pump(100);handle=FindWindow(null,"Nashi Window Layout Fixture");}
        try
        {
            check(handle!=0,"Window fixture did not appear.");
            var area=WindowManagement.WorkArea(new WindowManagement.Native.Point{X=100,Y=100});
            foreach(var placement in Enum.GetValues<WindowPlacement>())
            {
                var expected=placement.Frame(area);
                check(WindowManagement.Apply(handle,expected),"Native layout failed: "+placement);
                DesktopInput.Pump(100);var actual=WindowManagement.Bounds(handle);
                check(Math.Abs(actual.X-expected.X)<3&&Math.Abs(actual.Y-expected.Y)<3&&Math.Abs(actual.Width-expected.Width)<3&&Math.Abs(actual.Height-expected.Height)<3,"Native window bounds mismatch: "+placement);
            }
            Gesture(handle,manager,output,WindowPlacement.LeftHalf,shift:true,cancel:false);
            var left=WindowPlacement.LeftHalf.Frame(area);var result=WindowManagement.Bounds(handle);
            check(Math.Abs(result.X-left.X)<3&&Math.Abs(result.Width-left.Width)<3,"Shift native title-bar drag did not arrange the window. Expected "+left+"; actual "+result);
            Gesture(handle,manager,output,WindowPlacement.RightThird,shift:false,cancel:false);
            var right=WindowPlacement.RightThird.Frame(area);result=WindowManagement.Bounds(handle);
            check(Math.Abs(result.X-right.X)<3&&Math.Abs(result.Width-right.Width)<3,"Alt native title-bar drag did not arrange the window. Expected "+right+"; actual "+result);
            Gesture(handle,manager,output,WindowPlacement.LeftTwoThirds,shift:true,cancel:true);
            check(manager.ActiveWheel is null,"Escape left the window wheel open.");

            var wheel=new WheelWindow(null,Enum.GetValues<WindowPlacement>().Select(p=>new ToolChoice(p.ToString(),p.Title(),"",p)).ToArray(),_=>{},externalDrag:true);
            wheel.Show();wheel.UpdateLayout();DesktopInput.Pump();
            var capture=new RenderTargetBitmap((int)wheel.ActualWidth,(int)wheel.ActualHeight,96,96,PixelFormats.Pbgra32);capture.Render(wheel);ImageEngine.Save(capture,Path.Combine(output,"windows-window-management.png"),"png");wheel.Close();
        }
        finally{fixture.CloseMainWindow();if(!fixture.WaitForExit(2000))fixture.Kill();}
    }
    private static void Gesture(nint target,WindowManagement manager,string output,WindowPlacement choice,bool shift,bool cancel)
    {
        var reset=new WindowFrame(80,90,620,420);_ =WindowManagement.Apply(target,reset);DesktopInput.Pump(150);
        var task=Task.Run(()=>
        {
            var key=(byte)(shift?0x10:0x12);var down=false;
            try
            {
                SetForegroundWindow(target);Thread.Sleep(100);SetCursorPos(320,104);Thread.Sleep(100);
                mouse_event(2,0,0,0,0);down=true;Thread.Sleep(70);SetCursorPos(350,130);Thread.Sleep(80);
                keybd_event(key,0,0,0);SetCursorPos(375,145);
                nint wheel=0;var deadline=DateTime.UtcNow.AddSeconds(3);
                while(wheel==0&&DateTime.UtcNow<deadline){Thread.Sleep(50);wheel=FindWindow(null,"Nashi Window Management");}
                if(wheel==0)throw new Exception("Window wheel did not open during a native move.");
                var frame=WindowManagement.Bounds(wheel);File.AppendAllText(Path.Combine(output,"window-management-trace.txt"),$"test wheel={frame} choice={choice}\n");var index=Array.IndexOf(Enum.GetValues<WindowPlacement>(),choice);
                var angle=(-90+index*360d/7)*Math.PI/180;
                SetCursorPos((int)(frame.X+frame.Width*(.5+.33*Math.Cos(angle))),(int)(frame.Y+frame.Height*(.5+.33*Math.Sin(angle))));Thread.Sleep(180);
                if(cancel){keybd_event(0x1B,0,0,0);Thread.Sleep(100);keybd_event(0x1B,0,2,0);Thread.Sleep(100);}
            }
            finally{if(down)mouse_event(4,0,0,0,0);keybd_event(key,0,2,0);}
        });
        var timeout=DateTime.UtcNow.AddSeconds(8);while(!task.IsCompleted&&DateTime.UtcNow<timeout)DesktopInput.Pump(50);
        if(!task.IsCompleted)throw new Exception("Native window drag timed out.");task.GetAwaiter().GetResult();DesktopInput.Pump(350);
    }
}
