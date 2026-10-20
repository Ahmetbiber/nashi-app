using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nashi.Core;
namespace Nashi.Windows;
internal static class DesktopParityChecks
{
    private static IEnumerable<DependencyObject> Children(DependencyObject root){for(var n=0;n<VisualTreeHelper.GetChildrenCount(root);n++){var child=VisualTreeHelper.GetChild(root,n);yield return child;foreach(var inner in Children(child))yield return inner;}}
    internal static void Run(string root,MainWindow owner,string image,string text,string pastedImage,Action<bool,string> check)
    {
        var isolated=new LibraryStore(Path.Combine(root,"Desktop parity library"));var folder=Directory.CreateDirectory(Path.Combine(root,"Folder clipboard fixture")).FullName;
        var result=ClipboardImport.Read(new DataObject(DataFormats.FileDrop,new[]{folder,image,folder}),isolated);check(result.Paths.Length==2&&result.Paths.Contains(folder),"Folder paste rejected a directory or duplicated it");
        var window=new MainWindow(library:isolated,checkForUpdates:false);window.Show();DesktopInput.Pump();
        // Actual OS clipboard import, routed through native Ctrl+V, into the separate fixture Library.
        var drops=new System.Collections.Specialized.StringCollection{folder,image};Clipboard.SetFileDropList(drops);Children(window).OfType<Button>().First(b=>b.Content as string=="Home").Focus();DesktopInput.Key(window,Key.V,control:true);
        check(window.SelectedFiles.Contains(folder)&&window.SelectedFiles.Contains(image),"Native folder clipboard gesture lost selected items");
        isolated.Record("Preview batch",[text,pastedImage]);DesktopInput.Key(window,Key.F,control:true);window.SearchField.Text="Preview batch";DesktopInput.Key(window,Key.Down);
        check(!window.SearchField.IsKeyboardFocused&&window.LibraryVisible,"Down from Library search did not focus a result");
        DesktopInput.Key(window,Key.Space);var preview=Application.Current.Windows.OfType<FilePreviewWindow>().Single();DesktopInput.Pump();
        check(preview.CurrentPath==text&&Children(preview).OfType<TextBox>().Any(t=>t.Text.Contains("Türkçe")),"Library Space preview lost Unicode text");
        // The preview text is selectable; navigate from its header rather than hijacking text arrows.
        Children(preview).OfType<Button>().First().Focus();DesktopInput.Key(preview,Key.Right);DesktopInput.Pump();check(preview.CurrentPath==pastedImage&&Children(preview).OfType<Image>().Any(i=>i.Source is BitmapSource{PixelWidth:320,PixelHeight:180}),"Preview Right did not navigate to pasted image");
        var capture=new RenderTargetBitmap((int)preview.ActualWidth,(int)preview.ActualHeight,96,96,PixelFormats.Pbgra32);capture.Render(preview);ImageEngine.Save(capture,Path.Combine(root,"windows-library-preview.png"),"png");DesktopInput.Key(preview,Key.Escape);check(!preview.IsVisible,"Preview Escape did not close");
        window.Activate();DesktopInput.Pump();DesktopInput.Key(window,Key.Enter);check(window.SelectedFiles.SequenceEqual(new[]{text,pastedImage})&&!window.LibraryVisible,"Library Enter did not reuse the selected batch");window.Close();
        var shelf=new FloatShelf([image,text],(_,_)=>Task.CompletedTask,_=>{});shelf.Show();DesktopInput.Pump();Children(shelf).OfType<ListBox>().Single().Focus();DesktopInput.Key(shelf,Key.Space);preview=Application.Current.Windows.OfType<FilePreviewWindow>().Single();DesktopInput.Pump();check(preview.CurrentPath==image,"Float Space did not use the common preview");DesktopInput.Key(preview,Key.Right);DesktopInput.Pump();check(preview.CurrentPath==text,"Float preview did not navigate its file set");DesktopInput.Key(preview,Key.Escape);shelf.Close();
        var missing=new FilePreviewWindow(owner,[Path.Combine(root,"Missing preview.txt")]);missing.Show();DesktopInput.Pump();check(Children(missing).OfType<TextBlock>().Any(t=>t.Text.Contains("moved or was removed")),"Missing preview did not recover safely");missing.Close();
        check(File.Exists(text)&&File.Exists(pastedImage),"Preview changed imported source files");
    }
}
