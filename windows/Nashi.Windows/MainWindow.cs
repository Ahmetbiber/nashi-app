using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Nashi.Core;
namespace Nashi.Windows;

public sealed class MainWindow:Window
{
    private readonly ObservableCollection<string> files=[];private readonly ListBox list=new(){MinHeight=50,MaxHeight=180,BorderThickness=new(0),Background=Brushes.Transparent};
    private readonly StackPanel actions=new(){Orientation=Orientation.Horizontal};private readonly TextBlock selection=new(){FontSize=17,TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock status=new(){Text="Files stay on your computer.",TextWrapping=TextWrapping.Wrap};
    private readonly TextBox link=new(){MinWidth=260,ToolTip="Paste a public video, image or audio page link"};
    private readonly ComboBox downloadKind=new(){ItemsSource=new[]{"video","audio","thumbnail"},SelectedIndex=0,MinWidth=110};
    private readonly Toolchain tools=new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nashi","Tools","v1"));
    private bool busy;private WindowsUpdate? update;private readonly Button updateButton;
    public MainWindow(string[]? initial=null)
    {
        Background=(Brush)Application.Current.FindResource("Paper");Foreground=(Brush)Application.Current.FindResource("Ink");FontSize=14;FontFamily=new FontFamily("Segoe UI");Title="Nashi";Width=940;Height=840;MinWidth=800;MinHeight=620;WindowStartupLocation=WindowStartupLocation.CenterScreen;AllowDrop=true;Icon=BitmapFrame.Create(new Uri("pack://application:,,,/Assets/NashiIcon.png"));
        var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Content=scroll;var root=new StackPanel{Margin=new(34)};scroll.Content=root;
        var header=new DockPanel();var brand=new StackPanel{Orientation=Orientation.Horizontal};brand.Children.Add(new Image{Source=new BitmapImage(new Uri("pack://application:,,,/Assets/NashiIcon.png")),Width=64,Height=64,Margin=new(0,0,15,0)});brand.Children.Add(new TextBlock{Text="Nashi",FontSize=34,FontWeight=FontWeights.Bold,VerticalAlignment=VerticalAlignment.Center});header.Children.Add(brand);
        updateButton=Button("Check for updates",async()=>await CheckUpdatesAsync(true));DockPanel.SetDock(updateButton,Dock.Right);header.Children.Insert(0,updateButton);root.Children.Add(header);
        root.Children.Add(new TextBlock{Text="A little less busywork.",FontSize=31,FontWeight=FontWeights.SemiBold,Margin=new(0,28,0,6)});
        root.Children.Add(new TextBlock{Text="Convert, trim and tidy your files. Fresh copies, every time.",FontSize=16,Foreground=Brushes.DarkOliveGreen,Margin=new(0,0,0,24)});
        var dropContent=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};dropContent.Children.Add(new TextBlock{Text="↓",FontSize=32,HorizontalAlignment=HorizontalAlignment.Center});dropContent.Children.Add(new TextBlock{Text="Drop files here",FontSize=21,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center});dropContent.Children.Add(Button("Choose files…",Choose));
        root.Children.Add(new Border{CornerRadius=new(24),Background=new SolidColorBrush(Color.FromRgb(232,237,219)),Padding=new(24),Child=dropContent});
        root.Children.Add(selection);list.ItemsSource=files;list.SelectionMode=SelectionMode.Extended;root.Children.Add(list);
        var manage=new StackPanel{Orientation=Orientation.Horizontal};manage.Children.Add(Button("Add files",Choose));manage.Children.Add(Button("Remove selected",()=>{foreach(var file in list.SelectedItems.Cast<string>().ToArray())files.Remove(file);Refresh();}));manage.Children.Add(Button("Clear",()=>{files.Clear();Refresh();}));root.Children.Add(manage);
        actions.Margin=new(0,12,0,22);root.Children.Add(actions);
        root.Children.Add(new TextBlock{Text="Save something from the web",FontSize=21,FontWeight=FontWeights.SemiBold,Margin=new(0,8,0,10)});
        var download=new DockPanel();var save=Button("Download ↗",async()=>await DownloadAsync());DockPanel.SetDock(save,Dock.Right);download.Children.Add(save);DockPanel.SetDock(downloadKind,Dock.Right);download.Children.Add(downloadKind);download.Children.Add(link);root.Children.Add(download);
        root.Children.Add(new TextBlock{Text="Video, audio or thumbnail · Public, downloadable media · No browser login or cookies imported",FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DarkOliveGreen,Margin=new(4,5,0,14)});
        var footer=new WrapPanel();footer.Children.Add(Button("Set up media tools",async()=>await SetupAsync()));footer.Children.Add(Button("Features & shortcuts",()=>OpenUrl("https://orkunisitmak.github.io/nashi-app/")));root.Children.Add(footer);status.Margin=new(4,12,4,20);root.Children.Add(status);
        Closing+=(_,e)=>{if(busy){e.Cancel=true;status.Text="Finish the current operation or choose Cancel before closing Nashi.";}};
        DragOver+=(_,e)=>{e.Effects=!busy&&e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};Drop+=(_,e)=>{if(!busy&&e.Data.GetData(DataFormats.FileDrop) is string[] paths)Add(paths);};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.O&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){Choose();e.Handled=true;}else if(e.Key==Key.Space&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){ShowWheel();e.Handled=true;}};
        if(initial is not null)Add(initial);else Refresh();Loaded+=async(_,_)=>await CheckUpdatesAsync(false);
        var updateTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromHours(1)};updateTimer.Tick+=async(_,_)=>await CheckUpdatesAsync(false);Closed+=(_,_)=>updateTimer.Stop();updateTimer.Start();
    }
    public static Button Button(string title,Action action){var button=new Button{Content=title};button.Click+=(_,_)=>action();return button;}
    public static void OpenUrl(string url)=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
    public static void Reveal(string path){var info=new ProcessStartInfo("explorer.exe"){UseShellExecute=false};info.ArgumentList.Add("/select,"+Path.GetFullPath(path));Process.Start(info);}
    private void Choose(){if(busy)return;var picker=new OpenFileDialog{Multiselect=true,Title="Choose files for Nashi"};if(picker.ShowDialog(this)==true)Add(picker.FileNames);}
    private void Add(IEnumerable<string> paths){foreach(var path in paths.Where(File.Exists)){var full=Path.GetFullPath(path);if(!files.Contains(full))files.Add(full);}Refresh();}
    private bool All(string[] extensions)=>files.Count>0&&files.All(p=>extensions.Contains(Path.GetExtension(p).TrimStart('.').ToLowerInvariant()));
    private bool Images=>All(["png","jpg","jpeg","bmp","gif","tiff","tif"]);
    private bool Video=>All(["mp4","mov","mkv","webm","avi","wmv","m4v"]);
    private bool Audio=>All(["mp3","m4a","wav","flac","ogg","opus","aiff","aif","wma","aac"]);
    private IReadOnlyList<ToolChoice> Choices()
    {
        if(Images)return [new("resize","Resize","⤢"),new("crop","Crop","⌗"),new("rotate","Rotate","↻"),new("flip","Flip","⇆"),new("annotate","Annotate","✎"),new("compress","Compress","↘"),new("removeMetadata","Remove metadata","◇")];
        if(Video)return [new("trimVideo","Trim video","✂"),new("cropVideo","Crop video","⌗"),new("compress","Compress","↘"),new("muteVideo","Remove audio","♫"),new("changeVideoSpeed","Speed","◴"),new("videoSnapshots","Snapshots","▣"),new("splitVideo","Split video","◫"),new("redactVideo","Redact video","▨"),new("removeMetadata","Metadata","◇"),..(files.Count>1?new[]{new ToolChoice("joinVideos","Join videos","⇥")}:[])];
        if(Audio)return [new("trimAudio","Trim audio","✂"),new("normalizeAudio","Normalize","≋"),new("audioChannels","Channels","⇆"),new("redactAudio","Bleep audio","▨"),new("audioToVideo","Audio to video","▷"),new("compress","Compress","↘"),new("removeMetadata","Metadata","◇")];
        if(All(["pdf"]))return [new("splitPDF","Split PDF","◫"),new("organizePDF","Organize PDF","▤"),..(files.Count>1?new[]{new ToolChoice("mergePDFs","Merge PDFs","▣")}:[])];
        if(All(["zip"]))return [new("unzip","Extract ZIP","↗")];return [];
    }
    private void Refresh()
    {
        selection.Text=files.Count==0?"":$"{files.Count} {(files.Count==1?"file":"files")} selected";selection.Margin=new(4,18,4,4);actions.Children.Clear();
        if(files.Count>0){if(Images||Video||Audio)actions.Children.Add(Button("Convert ↗",async()=>await EditAsync(new("convert","Convert","↗"))));if(Choices().Count>0)actions.Children.Add(Button("Open tools wheel  ·  Ctrl+Space",ShowWheel));actions.Children.Add(Button("Create ZIP",async()=>await RunAsync((destination,p,t)=>Task.Run<IReadOnlyList<string>>(()=>[DocumentEngine.Zip(files.ToArray(),destination,p,t)],t))));}
    }
    private void ShowWheel(){if(!busy&&Choices().Count>0)new WheelWindow(this,Choices(),async choice=>await EditAsync(choice)).Show();}
    private async Task EditAsync(ToolChoice choice)
    {
        if(busy||files.Count==0)return;try
        {
            MediaInfo? info=null;BitmapSource? preview=null;var input=files[0];string? temporary=null;
            if(Images)preview=ImageEngine.Load(input);
            else if(Video||Audio)
            {
                if(!tools.Ready){MessageBox.Show(this,"Choose Set up media tools once to enable media editing.","Media setup");return;}
                var engine=new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe"));info=await engine.InspectAsync(input);
                if(choice.Id is "cropVideo" or "redactVideo")
                {
                    temporary=Path.Combine(Path.GetTempPath(),"Nashi-preview-"+Guid.NewGuid().ToString("N")+".png");
                    await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-v","error","-i",input,"-frames:v","1",temporary]);preview=ImageEngine.Load(temporary);
                }
            }
            var editor=new ToolEditor(this,choice.Id,choice.Title,input,info,preview);var accepted=editor.ShowDialog()==true;if(temporary is not null)File.Delete(temporary);if(!accepted)return;
            var inputs=choice.Id is "crop" or "cropVideo" or "redactVideo" or "annotate"?new[]{files[0]}:files.ToArray();
            if(Images)await RunAsync((destination,p,t)=>OnStaAsync(()=>ImageEngine.Apply(choice.Id,inputs,destination,editor.ImageOptions,choice.Id=="convert"?editor.Format:null,t,p)));
            else if(Video||Audio)await RunAsync((destination,p,t)=>{var engine=new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe"));return choice.Id=="convert"?engine.ConvertAsync(inputs,editor.Format,destination,editor.MediaOptions,p,t):engine.ToolAsync(choice.Id,inputs,destination,editor.MediaOptions,p,t);});
            else if(choice.Id=="unzip")await RunAsync((destination,p,t)=>Task.Run<IReadOnlyList<string>>(()=>{using var tx=new ExportTransaction(destination);var outputs=inputs.SelectMany(file=>DocumentEngine.Unzip(file,tx.StagingDirectory,p,t)).ToList();return tx.Publish(outputs,t);},t));
            else await RunAsync((destination,p,t)=>Task.Run(()=>DocumentEngine.Pdf(choice.Id,inputs,destination,editor.Pages,t),t));
        }
        catch(Exception error){MessageBox.Show(this,error.Message,"Couldn’t open this tool",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    private async Task RunAsync(Func<string,IProgress<OperationProgress>,CancellationToken,Task<IReadOnlyList<string>>> work)
    {
        if(busy)return;var folder=new OpenFolderDialog{Title="Save new files to…",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)};if(folder.ShowDialog(this)!=true)return;
        busy=true;using var cancellation=new CancellationTokenSource();var toast=new ToastWindow(this,cancellation.Cancel);toast.Show();
        try{var outputs=await work(folder.FolderName,new Progress<OperationProgress>(toast.Update),cancellation.Token);toast.Complete(outputs);status.Text=$"Saved {outputs.Count} new {(outputs.Count==1?"file":"files")}. Originals kept.";}
        catch(OperationCanceledException){toast.Close();status.Text="Cancelled. Originals kept.";}
        catch(Exception error){toast.Close();MessageBox.Show(this,error.Message,"Couldn’t finish",MessageBoxButton.OK,MessageBoxImage.Information);}
        finally{busy=false;}
    }
    private static Task<IReadOnlyList<string>> OnStaAsync(Func<IReadOnlyList<string>> work)
    {
        var completion=new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>{try{completion.SetResult(work());}catch(Exception error){completion.SetException(error);}}){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
    }
    private async Task DownloadAsync()
    {
        if(!tools.Ready){MessageBox.Show(this,"Choose Set up media tools once to enable web downloads.","Media setup");return;}
        try{var url=WebDownload.Validate(link.Text).AbsoluteUri;var kind=(string)downloadKind.SelectedItem;await RunAsync((destination,p,t)=>new WebDownload(tools).DownloadAsync(url,kind,destination,p,t));}catch(Exception error){MessageBox.Show(this,error.Message,"Check your link");}
    }
    private async Task SetupAsync()
    {
        if(busy)return;if(tools.Ready){status.Text="Media tools are already installed.";return;}
        if(MessageBox.Show(this,"Download FFmpeg (~180 MB), yt-dlp and Deno directly from their official GitHub releases? SHA-256 hashes are verified before use. FFmpeg is GPL; the other upstream licenses and source links are listed on our Features page. No administrator password is needed.","Set up media tools",MessageBoxButton.OKCancel,MessageBoxImage.Information)!=MessageBoxResult.OK)return;
        busy=true;using var cancellation=new CancellationTokenSource();var toast=new ToastWindow(this,cancellation.Cancel);toast.Show();
        try{await tools.InstallAsync(new Progress<OperationProgress>(toast.Update),cancellation.Token);toast.Close();status.Text="Media tools ready. You can edit, convert and download media.";}
        catch(OperationCanceledException){toast.Close();status.Text="Setup cancelled.";}catch(Exception error){toast.Close();MessageBox.Show(this,error.Message,"Setup couldn’t finish");}finally{busy=false;}
    }
    private async Task CheckUpdatesAsync(bool user)
    {
        if(busy)return;try
        {
            if(update is null){update=await PublicUpdates.CheckAsync(UpdateInstaller.Current);if(update is null){if(user)status.Text="You have the latest published Windows version.";return;}updateButton.Content="Update to "+update.Version;if(!user)return;}
            if(busy)return;
            if(MessageBox.Show(this,"Download, verify and install Nashi "+update.Version+"? The app will restart. No account or token is needed.","Nashi update",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
            busy=true;using var cancellation=new CancellationTokenSource();var toast=new ToastWindow(this,cancellation.Cancel);toast.Show();
            try{await UpdateInstaller.StageAsync(update,new Progress<OperationProgress>(toast.Update),cancellation.Token);busy=false;Application.Current.Shutdown();}finally{toast.Close();busy=false;}
        }
        catch(OperationCanceledException){status.Text="Update cancelled.";}catch(Exception error){if(user)MessageBox.Show(this,error.Message,"Couldn’t check for updates");}
    }
}
