using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using Nashi.Core;
namespace Nashi.Windows;
internal static class GlobalGestureChecks
{
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr FindWindow(string? type,string name);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr handle,out NativeRect rect);
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")]private static extern bool PostMessage(IntPtr handle,uint message,IntPtr w,IntPtr l);
    internal static void Run(Window owner,string root,string original,Action<bool,string> check)
    {
        // Explorer is a separate source process. A same-thread WPF drag source
        // blocks the target's dispatcher inside DoDragDrop and is not a valid
        // test of the global Explorer gesture.
        var folder=Directory.CreateDirectory(Path.Combine(root,"Nashi drag fixtures"));var file=Path.Combine(folder.FullName,"Source.png");File.Copy(original,file,true);var launch=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")){UseShellExecute=false};launch.ArgumentList.Add(folder.FullName);Process.Start(launch);
        var phase="discovering Explorer";using var watchdog=new System.Threading.Timer(_=>{File.WriteAllText(Path.Combine(root,"global-gesture-timeout.txt"),phase);DesktopInput.HoldMouse(false);Environment.Exit(1);},null,TimeSpan.FromSeconds(45),System.Threading.Timeout.InfiniteTimeSpan);
        // UI Automation must run away from Nashi's UI thread: enumerating the
        // desktop can query this process's own WPF providers.
        var discovery=Task.Run(()=>
        {
            AutomationElement? explorer=null;for(var n=0;n<50;n++){Thread.Sleep(100);explorer=AutomationElement.RootElement.FindFirst(TreeScope.Children,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Window),new PropertyCondition(AutomationElement.NameProperty,folder.Name)));if(explorer is not null)break;}
            if(explorer is null)throw new Exception("The isolated Explorer fixture window did not open.");var handle=new IntPtr(explorer.Current.NativeWindowHandle);var item=explorer.FindFirst(TreeScope.Descendants,new OrCondition(new PropertyCondition(AutomationElement.NameProperty,"Source.png"),new PropertyCondition(AutomationElement.NameProperty,"Source")))??throw new Exception("Explorer did not expose its synthetic source file.");return(Handle:handle,Bounds:item.Current.BoundingRectangle);
        });
        var discoveryLimit=DateTime.UtcNow.AddSeconds(12);while(!discovery.IsCompleted&&DateTime.UtcNow<discoveryLimit)DesktopInput.Pump(30);if(!discovery.IsCompleted)throw new TimeoutException("Explorer UI Automation did not respond.");var fixture=discovery.GetAwaiter().GetResult();var handle=fixture.Handle;var bounds=fixture.Bounds;if(bounds.IsEmpty)throw new Exception("Explorer's source file has no visible bounds.");
        var settings=new PreferencesStore(Path.Combine(root,"Gesture preferences"));string? selected=null;string[]? received=null;
        using var controller=new GlobalDragController(owner,settings,(id,data,hint)=>{selected=id;received=data.GetData(DataFormats.FileDrop) as string[];},()=>selected="link-shortcut");
        void Drag(Key modifier,string mode,string expected)
        {
            phase="dragging "+mode;selected=null;string? state=null;SetForegroundWindow(handle);DesktopInput.Pump(200);var start=new Point(bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2);
            var driver=Task.Run(()=>
            {
                DesktopInput.MovePointer(start);Thread.Sleep(100);DesktopInput.HoldMouse(true);Thread.Sleep(150);
                try
                {
                    DesktopInput.MovePointer(new(start.X+45,start.Y+20));Thread.Sleep(150);DesktopInput.Hold(modifier,true);var wheel=IntPtr.Zero;for(var n=0;n<30&&wheel==IntPtr.Zero;n++){Thread.Sleep(100);wheel=FindWindow(null,"Nashi · "+mode);}if(wheel==IntPtr.Zero||!GetWindowRect(wheel,out var r))throw new Exception("The global "+mode+" wheel did not open during the Explorer drag.");
                    DesktopInput.MovePointer(new((r.Left+r.Right)/2d,r.Top+(r.Bottom-r.Top)*.18));Thread.Sleep(400);state=controller.ActiveWheel?.State;owner.Dispatcher.BeginInvoke(()=>DesktopCapture.Save(Path.Combine(root,"global-drag-"+mode+".png")));Thread.Sleep(200);
                }
                finally{DesktopInput.HoldMouse(false);DesktopInput.Hold(modifier,false);}
            });
            var until=DateTime.UtcNow.AddSeconds(12);while(!driver.IsCompleted&&DateTime.UtcNow<until)DesktopInput.Pump(30);if(!driver.IsCompleted)throw new TimeoutException("Global Explorer drag timed out.");driver.GetAwaiter().GetResult();DesktopInput.Pump(350);check(selected==expected,"Real Explorer OLE drag: "+expected+"; observed="+selected+"; "+state);check(received?.SingleOrDefault()==file,"Explorer drop preserved the selected file path");
        }
        try
        {
            Drag(Key.LeftCtrl,"tools","compress");Drag(Key.LeftAlt,"actions","share");Drag(Key.LeftShift,"convert","convert:"+FeatureCatalog.Formats([file])[0]);
            if(controller.LinkShortcutRegistered){DesktopInput.Key(owner,Key.D,control:true,shift:true);check(selected=="link-shortcut","Global copied-link shortcut Ctrl+Shift+D");}
            settings.Save(settings.Value with{Enabled=false});selected="disabled";DesktopInput.Key(owner,Key.D,control:true,shift:true);check(selected=="disabled","Disabled global shortcuts ignored copied-link input");
            var data=new DataObject();using var stream=new MemoryStream(System.Text.Encoding.Unicode.GetBytes("https://www.instagram.com/p/FixtureCode/\0"));data.SetData("UniformResourceLocatorW",stream);check(DragPayload.Parse(data) is {Kind:"instagramPhotos"},"Browser URL stream was decoded as a photo post");
            data.SetData(DataFormats.Html,"<a href=\"https://www.instagram.com/reel/FixtureCode/\"><img src=\"https://cdn.example/preview.jpg\"></a>");check(DragPayload.Parse(data) is {Kind:"instagramReel"},"Browser HTML identified a reel without thumbnail actions");
        }
        finally{DesktopInput.HoldMouse(false);DesktopInput.Hold(Key.LeftCtrl,false);DesktopInput.Hold(Key.LeftAlt,false);DesktopInput.Hold(Key.LeftShift,false);PostMessage(handle,0x10,IntPtr.Zero,IntPtr.Zero);}
    }
}
