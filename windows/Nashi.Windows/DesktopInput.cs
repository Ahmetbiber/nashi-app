using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
namespace Nashi.Windows;

// Used only by the explicit --smoke-test path on an isolated Windows desktop.
// SendInput exercises WPF's real keyboard routing, including focused text boxes.
internal static class DesktopInput
{
    [StructLayout(LayoutKind.Sequential)]private struct Input{public uint Type;public Union Data;}
    [StructLayout(LayoutKind.Explicit)]private struct Union
    {
        [FieldOffset(0)]public KeyboardInput Keyboard;
        [FieldOffset(0)]public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)]private struct KeyboardInput{public ushort Key,Scan;public uint Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseInput{public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,Input[] inputs,int size);
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    internal static void Pump(int milliseconds=140)
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(milliseconds)};
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
    internal static void Key(Window window,Key key,bool control=false,bool shift=false)
    {
        window.Activate();Pump();var inputs=new List<Input>();
        void Add(int vk,bool up=false)=>inputs.Add(new(){Type=1,Data=new(){Keyboard=new(){Key=(ushort)vk,Flags=up?2u:0u}}});
        if(control)Add(0x11);if(shift)Add(0x10);Add(KeyInterop.VirtualKeyFromKey(key));Add(KeyInterop.VirtualKeyFromKey(key),true);if(shift)Add(0x10,true);if(control)Add(0x11,true);
        if(SendInput((uint)inputs.Count,inputs.ToArray(),Marshal.SizeOf<Input>())!=inputs.Count)throw new InvalidOperationException("Native keyboard input was unavailable on this Windows desktop.");Pump();
    }
    internal static void Drag(FrameworkElement view,Point from,Point to,bool shift=false)
    {
        void Send(Input input){if(SendInput(1,[input],Marshal.SizeOf<Input>())!=1)throw new InvalidOperationException("Native drag input unavailable.");}
        if(shift)Send(new(){Type=1,Data=new(){Keyboard=new(){Key=0x10}}});
        var a=view.PointToScreen(from);var b=view.PointToScreen(to);
        try
        {
            SetCursorPos((int)a.X,(int)a.Y);Pump(50);Send(new(){Data=new(){Mouse=new(){Flags=2}}});Pump(50);
            for(var i=1;i<=8;i++){SetCursorPos((int)(a.X+(b.X-a.X)*i/8),(int)(a.Y+(b.Y-a.Y)*i/8));Pump(30);}
        }
        finally{Send(new(){Data=new(){Mouse=new(){Flags=4}}});if(shift)Send(new(){Type=1,Data=new(){Keyboard=new(){Key=0x10,Flags=2}}});Pump();}
    }
}
