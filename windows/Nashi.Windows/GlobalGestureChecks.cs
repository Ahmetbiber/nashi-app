using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nashi.Core;
namespace Nashi.Windows;
internal static class GlobalGestureChecks
{
    internal static void Run(Window owner,string root,string file,Action<bool,string> check)
    {
        var settings=new PreferencesStore(Path.Combine(root,"Gesture preferences"));string? selected=null;string[]? received=null;WebMediaPayload? web=null;
        using var controller=new GlobalDragController(owner,settings,(id,data,hint)=>{selected=id;received=data.GetData(DataFormats.FileDrop) as string[];web=DragPayload.Parse(data,hint);},()=>selected="link-shortcut");
        var source=new Window{Owner=owner,Title="Nashi drag test source",Width=340,Height=160,Left=30,Top=30};var target=new Border{Child=new TextBlock{Text="Drag test fixture",Margin=new(20)},Margin=new(20),Background=System.Windows.Media.Brushes.OliveDrab};source.Content=target;IDataObject payload=new DataObject(DataFormats.FileDrop,new[]{file});var started=false;
        target.PreviewMouseLeftButtonDown+=(_,e)=>{if(!started){started=true;DragDrop.DoDragDrop(target,payload,DragDropEffects.Copy);}};source.Show();DesktopInput.Pump();
        void Drag(Key modifier,string expected)
        {
            selected=null;started=false;string? state=null;GlobalDropWheel? shownWheel=null;source.Activate();DesktopInput.Pump();var start=target.PointToScreen(new Point(50,30));
            var driver=Task.Run(()=>
            {
                DesktopInput.MovePointer(start);Thread.Sleep(100);DesktopInput.Hold(modifier,true);DesktopInput.HoldMouse(true);Thread.Sleep(100);
                try{DesktopInput.MovePointer(new(start.X+35,start.Y+15));Thread.Sleep(700);Point drop=default;owner.Dispatcher.Invoke(()=>{var wheel=Application.Current.Windows.OfType<GlobalDropWheel>().SingleOrDefault();if(wheel is null)throw new Exception("Drag shortcut did not open its global wheel.");shownWheel=wheel;drop=wheel.PointToScreen(new(wheel.ActualWidth/2,wheel.ActualHeight*.18));});DesktopInput.MovePointer(drop);Thread.Sleep(300);owner.Dispatcher.Invoke(()=>{state=shownWheel?.State;DesktopCapture.Save(Path.Combine(root,"global-drag-desktop.png"));});}
                finally{DesktopInput.HoldMouse(false);DesktopInput.Hold(modifier,false);}
            });
            var until=DateTime.UtcNow.AddSeconds(12);while(!driver.IsCompleted&&DateTime.UtcNow<until)DesktopInput.Pump(30);if(!driver.IsCompleted)throw new TimeoutException("Global drag test timed out.");driver.GetAwaiter().GetResult();DesktopInput.Pump(300);check(selected==expected,"Real OLE drag shortcut: "+expected+"; started="+started+"; observed="+selected+"; "+state);check(received?.SingleOrDefault()==file,"Global file drop preserved the selected path");
        }
        try
        {
            Drag(Key.LeftCtrl,"compress");Drag(Key.LeftAlt,"share");Drag(Key.LeftShift,"convert:"+FeatureCatalog.Formats([file])[0]);
            if(controller.LinkShortcutRegistered){DesktopInput.Key(source,Key.D,control:true,shift:true);check(selected=="link-shortcut","Global copied-link shortcut Ctrl+Shift+D");}
            settings.Save(settings.Value with{Enabled=false});selected="disabled";DesktopInput.Key(source,Key.D,control:true,shift:true);check(selected=="disabled","Disabled shortcuts preserved current state");
            var data=new DataObject();using var stream=new MemoryStream(System.Text.Encoding.Unicode.GetBytes("https://www.instagram.com/p/FixtureCode/\0"));data.SetData("UniformResourceLocatorW",stream);check(DragPayload.Parse(data) is {Kind:"instagramPhotos"},"Browser URL stream was decoded as an Instagram photo post");
            data.SetData(DataFormats.Html,"<a href=\"https://www.instagram.com/reel/FixtureCode/\"><img src=\"https://cdn.example/preview.jpg\"></a>");check(DragPayload.Parse(data) is {Kind:"instagramReel"},"Browser HTML context identified a reel without thumbnail actions");
        }
        finally{DesktopInput.HoldMouse(false);DesktopInput.Hold(Key.LeftCtrl,false);DesktopInput.Hold(Key.LeftAlt,false);DesktopInput.Hold(Key.LeftShift,false);source.Close();}
    }
}
