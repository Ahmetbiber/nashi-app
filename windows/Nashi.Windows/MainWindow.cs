using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Nashi.Core;
namespace Nashi.Windows;

public sealed partial class MainWindow:Window
{
    private readonly ObservableCollection<string> files=[];
    private readonly ListBox list=new(){MinHeight=0,MaxHeight=110};
    private readonly WrapPanel actions=new();
    private readonly TextBlock selection=new(){FontSize=23,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock status=Text("Files stay on your computer.",12,"Muted");
    private readonly TextBox link=new(){ToolTip="Paste a public video, image or audio page link"};
    private readonly ComboBox downloadKind=new(){ItemsSource=new[]{"video","audio","thumbnail"},SelectedIndex=0,MinWidth=110};
    private readonly Toolchain tools=new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nashi","Tools","v1"));
    private readonly LibraryStore library;
    private readonly PreferencesStore preferences;private GlobalDragController? globalDrag;
    private readonly StackPanel home=new(),libraryPage=new(),libraryRows=new(),recentRows=new();
    private readonly TextBox recentSearch=new(){ToolTip="Find a recent file",Width=165};private readonly TextBlock recentCount=Text("0",11,"Muted");private Button? chooseFilesButton,openWheelButton;private readonly TextBlock fileDescription=Text("Images, videos, documents, and all the little things in between.",12,"Muted");
    private readonly TextBox search=new(){ToolTip="Find a file or tool in Library",MinWidth=200};
    private readonly WrapPanel manage=new();private readonly FileGarden garden=new();private readonly StackPanel webContent=new();private Window? webWindow;
    internal Window DownloadWindow=>webWindow??this;
    private readonly Button homeButton,libraryButton,updateButton;private readonly ComboBox appearance;
    private readonly StackPanel workArea=new();
    private bool busy,checking;private WindowsUpdate? update;
    internal IReadOnlyList<string> SelectedFiles=>files;
    internal bool LibraryVisible=>libraryPage.Visibility==Visibility.Visible;
    internal TextBox SearchField=>search;
    internal TextBox LinkField=>link;
    public MainWindow(string[]? initial=null,LibraryStore? library=null,bool checkForUpdates=true)
    {
        this.library=library??new LibraryStore();preferences=new(this.library.DirectoryPath);ApplyPreferences();Theme.Apply(this.library.Appearance);Theme.Attach(this);
        Title="Nashi";Width=820;Height=800;MinWidth=780;MinHeight=620;WindowStartupLocation=WindowStartupLocation.CenterScreen;AllowDrop=true;
        Icon=BitmapFrame.Create(new Uri("pack://application:,,,/Assets/NashiIcon.png"));
        var root=new DockPanel{Margin=new(28,18,28,18)};Content=root;
        var header=new DockPanel{Margin=new(0,0,0,18)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var controls=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(controls,Dock.Right);header.Children.Add(controls);
        homeButton=Button("Home",()=>ShowLibrary(false));libraryButton=Button("Library",()=>ShowLibrary(true));controls.Children.Add(homeButton);controls.Children.Add(libraryButton);
        appearance=new ComboBox{ItemsSource=new[]{"System","Light","Dark"},SelectedItem=char.ToUpper(this.library.Appearance[0])+this.library.Appearance[1..],ToolTip="Appearance",MinWidth=90};
        appearance.SelectionChanged+=(_,_)=>{var choice=((string)appearance.SelectedItem).ToLowerInvariant();Theme.Apply(choice);try{this.library.SetAppearance(choice);}catch(Exception error)when(error is IOException or UnauthorizedAccessException){status.Text="Appearance changed, but it could not be saved.";}};controls.Children.Add(appearance);
        updateButton=Button("Check for updates",async()=>await CheckUpdatesAsync(true));updateButton.ToolTip="Ctrl+U · Check for updates";controls.Children.Add(updateButton);
        controls.Children.Add(Button("Settings",ShowSettings));
        var brand=new StackPanel{Orientation=Orientation.Horizontal};brand.Children.Add(new Image{Source=FileGarden.Artwork("NashiMark"),Width=40,Height=40,Margin=new(0,0,10,0)});brand.Children.Add(Text("Nashi",21));header.Children.Add(brand);
        DockPanel.SetDock(status,Dock.Bottom);status.Margin=new(4,14,4,0);root.Children.Add(status);
        var pages=new Grid();pages.Children.Add(new ScrollViewer{Content=home,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});pages.Children.Add(new ScrollViewer{Content=libraryPage,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});root.Children.Add(pages);
        home.Children.Add(Text("Your files. Their next form.",30));var intro=Text("Convert, fine-tune, and send them on their way.",14,"Muted");intro.Margin=new(0,6,0,22);home.Children.Add(intro);
        home.Children.Add(workArea);
        var drop=new Grid();drop.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});drop.ColumnDefinitions.Add(new(){Width=new(166)});
        var dropText=new StackPanel();drop.Children.Add(dropText);dropText.Children.Add(Text("✦  START HERE",11,"Gold"));selection.Margin=new(0,12,0,8);dropText.Children.Add(selection);dropText.Children.Add(fileDescription);
        var picks=new WrapPanel{Margin=new(0,14,0,8)};chooseFilesButton=Primary("Choose files",Choose);picks.Children.Add(chooseFilesButton);openWheelButton=Primary("Open wheel",ShowWheel);picks.Children.Add(openWheelButton);picks.Children.Add(Button("Paste  ·  Ctrl+V",PasteClipboard));dropText.Children.Add(picks);
        Grid.SetColumn(garden,1);drop.Children.Add(garden);
        var dropStack=new StackPanel();dropStack.Children.Add(drop);list.ItemsSource=files;list.SelectionMode=SelectionMode.Extended;
        list.ItemTemplate=new DataTemplate(typeof(string)){VisualTree=new FrameworkElementFactory(typeof(TextBlock))};list.ItemTemplate.VisualTree.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding{Converter=new FileNameConverter()});list.ItemTemplate.VisualTree.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);
        var chips=new FrameworkElementFactory(typeof(WrapPanel));list.ItemsPanel=new ItemsPanelTemplate(chips);list.ItemContainerStyle=new Style(typeof(ListBoxItem),(Style)Application.Current.FindResource(typeof(ListBoxItem)));list.ItemContainerStyle.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty,190d));list.ItemContainerStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,3,6,0)));dropStack.Children.Add(list);
        manage.Children.Add(Button("Remove selected",()=>{foreach(var file in list.SelectedItems.Cast<string>().ToArray())files.Remove(file);Refresh();}));manage.Children.Add(Button("Clear",()=>{files.Clear();Refresh();}));dropStack.Children.Add(manage);
        var hint=Text("Ctrl+O  Choose files       Ctrl+Space  Open tools       Ctrl+V  Paste",11,"Muted");hint.Margin=new(0,12,0,0);dropStack.Children.Add(hint);workArea.Children.Add(Card(dropStack));
        actions.Margin=new(0,10,0,0);dropStack.Children.Add(actions);
        var web=new StackPanel();web.Children.Add(Text("Download a link",17));web.Children.Add(Text("Video, audio, or an image. Straight into your tools.",12,"Muted"));
        var download=new DockPanel{Margin=new(0,12,0,0)};var save=Primary("Download ↗",async()=>await DownloadAsync());DockPanel.SetDock(save,Dock.Right);download.Children.Add(save);DockPanel.SetDock(downloadKind,Dock.Right);download.Children.Add(downloadKind);download.Children.Add(link);web.Children.Add(download);
        web.Children.Add(Text("Ctrl+Shift+D  Use a copied link  ·  Public, downloadable media",11,"Muted"));webContent.Margin=new(24);webContent.Children.Add(Text("Download from the web",27));webContent.Children.Add(Text("A link. A file. Yours to work with.",14,"Muted"));var webCard=Card(web);webCard.Margin=new(0,20,0,0);webContent.Children.Add(webCard);BuildWebOptions();
        var webHeader=new DockPanel();var shortcut=Text("Ctrl+Shift+D",12,"Muted");DockPanel.SetDock(shortcut,Dock.Right);webHeader.Children.Add(shortcut);var arrow=Text("↓",28,"Gold");arrow.Margin=new(0,0,14,0);DockPanel.SetDock(arrow,Dock.Left);webHeader.Children.Add(arrow);var caption=new StackPanel();caption.SetValue(TextBlock.TextAlignmentProperty,TextAlignment.Left);caption.Children.Add(Text("Download a link",15));caption.Children.Add(Text("Video, audio, or an image. Straight into your tools.",12,"Muted"));webHeader.Children.Add(caption);var webButton=Button("",OpenDownload);webButton.Content=webHeader;webButton.HorizontalContentAlignment=HorizontalAlignment.Stretch;webButton.Margin=new(0);webButton.Padding=new(18);webButton.BorderThickness=new(0);var webLanding=Card(webButton);webLanding.Padding=new(0);workArea.Children.Add(webLanding);
        var recentHeading=new DockPanel{Margin=new(0,24,0,10)};var all=Button("See all  ↗",()=>ShowLibrary(true));DockPanel.SetDock(all,Dock.Right);recentHeading.Children.Add(all);var recentTitle=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};recentTitle.Children.Add(Text("Freshly made",19));recentCount.Margin=new(10,0,0,0);recentTitle.Children.Add(recentCount);recentHeading.Children.Add(recentTitle);home.Children.Add(recentHeading);recentSearch.Margin=new(0,0,0,10);recentSearch.TextChanged+=(_,_)=>RenderLibrary();home.Children.Add(recentSearch);var recentCard=Card(recentRows);recentCard.Padding=new(7);home.Children.Add(recentCard);
        var footer=new WrapPanel{Margin=new(0,14,0,0)};footer.Children.Add(Button("Set up media tools",async()=>await SetupAsync()));footer.Children.Add(Button("Set up office tools",async()=>await SetupOfficeAsync()));footer.Children.Add(Button("Features & shortcuts",ShowShortcuts));home.Children.Add(new Expander{Header="Tools & shortcuts",Content=footer,Margin=new(4,8,4,0)});
        libraryPage.Children.Add(Text("Your Library",30));libraryPage.Children.Add(Text("Freshly made, pasted, and ready for another round.",14,"Muted"));
        var searchLabel=Text("Find a file or tool  ·  Ctrl+F",12,"Muted");searchLabel.Margin=new(4,20,4,0);libraryPage.Children.Add(searchLabel);libraryPage.Children.Add(search);libraryPage.Children.Add(libraryRows);search.TextChanged+=(_,_)=>RenderLibrary();
        AutomationProperties.SetAutomationId(search,"LibrarySearch");AutomationProperties.SetAutomationId(link,"DownloadLink");AutomationProperties.SetAutomationId(list,"SelectedFiles");
        Closing+=(_,e)=>{if(busy){e.Cancel=true;status.Text="Finish the current operation or choose Cancel before closing Nashi.";}};
        DragOver+=(_,e)=>{e.Effects=!busy&&(e.Data.GetDataPresent(DataFormats.FileDrop)||e.Data.GetDataPresent(DataFormats.UnicodeText)||e.Data.GetDataPresent(DataFormats.Html)||e.Data.GetDataPresent("UniformResourceLocatorW"))?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
        Drop+=(_,e)=>{if(!busy){if(!e.Data.GetDataPresent(DataFormats.FileDrop)&&DragPayload.Parse(e.Data) is not null)OnGlobalDrop("download:auto",e.Data,null);else Import(e.Data);}e.Handled=true;};
        PreviewKeyDown+=(_,e)=>{if(HandleShortcut(e.Key,Keyboard.Modifiers,Keyboard.FocusedElement as DependencyObject))e.Handled=true;};
        if(initial is not null)Add(initial);else Refresh();ShowLibrary(false);RenderLibrary();
        if(this.library.RecoveryNotice is {} notice)status.Text=notice;
        else if(preferences.RecoveryNotice is {} settingsNotice)status.Text=settingsNotice;
        if(checkForUpdates)
        {
            Loaded+=(_,_)=>SetupDesktop();Closed+=(_,_)=>globalDrag?.Dispose();
            Loaded+=async(_,_)=>await CheckUpdatesAsync(false);
            var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromHours(1)};timer.Tick+=async(_,_)=>await CheckUpdatesAsync(false);Closed+=(_,_)=>timer.Stop();timer.Start();
        }
    }
    private static TextBlock Text(string value,double size,string? color=null)
    {
        var text=new TextBlock{Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,FontWeight=size>=17?FontWeights.SemiBold:FontWeights.Normal};if(color is not null)text.SetResourceReference(TextBlock.ForegroundProperty,color);return text;
    }
    private static Border Card(UIElement content){var card=new Border{CornerRadius=new(24),Padding=new(22),Margin=new(0,0,0,6),Child=content,BorderThickness=new(1)};card.SetResourceReference(Border.BackgroundProperty,"Surface");card.SetResourceReference(Border.BorderBrushProperty,"Line");return card;}
    public static Button Button(string title,Action action){var button=new Button{Content=title};button.Click+=(_,_)=>action();return button;}
    private static Button Primary(string title,Action action){var button=Button(title,action);button.Background=Theme.Primary;button.Foreground=new SolidColorBrush(Color.FromRgb(43,54,28));return button;}
    public static void OpenUrl(string url)=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
    public static void Reveal(string path){var info=new ProcessStartInfo("explorer.exe"){UseShellExecute=false};info.ArgumentList.Add("/select,"+Path.GetFullPath(path));Process.Start(info);}
    private void Choose(){if(busy)return;var picker=new OpenFileDialog{Multiselect=true,Title="Choose files for Nashi"};if(picker.ShowDialog(this)==true){Add(picker.FileNames);ShowLibrary(false);}}
    private void Add(IEnumerable<string> paths){foreach(var path in paths.Where(p=>File.Exists(p)||Directory.Exists(p))){var full=Path.GetFullPath(path);if(!files.Contains(full))files.Add(full);}Refresh();}
    internal void ShowLibrary(bool visible)
    {
        if(!busy)status.Text="Files stay on your computer.";
        home.Visibility=visible?Visibility.Collapsed:Visibility.Visible;libraryPage.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
        homeButton.SetResourceReference(Control.BackgroundProperty,visible?"Surface":"Sage");libraryButton.SetResourceReference(Control.BackgroundProperty,visible?"Sage":"Surface");if(visible)RenderLibrary();
    }
    private void Record(string label,IReadOnlyList<string> paths)
    {
        try{library.Record(label,paths);RenderLibrary();}catch(Exception error)when(error is IOException or UnauthorizedAccessException){status.Text="Your files are ready, but Library could not be saved.";}
    }
    internal void Import(IDataObject data)
    {
        if(busy)return;
        try
        {
            var result=ClipboardImport.Read(data,library);ShowLibrary(false);
            if(result.Link is {} url){var payload=WebMediaPayload.Parse(url);link.Text=payload?.Url??url;if(payload is not null)SetDownloadKind(payload);OpenDownload();link.Focus();status.Text="Link ready. Choose a format, then Download.";return;}
            files.Clear();Add(result.Paths);Record(result.Label,result.Paths);status.Text=result.Label+". Choose a tool or create a ZIP.";
        }
        catch(Exception error)when(ClipboardImport.IsClipboardError(error)){status.Text="Couldn’t paste: "+error.Message;}
    }
    private void PasteClipboard()
    {
        if(busy)return;try{Import(Clipboard.GetDataObject()??new DataObject());}catch(Exception error)when(ClipboardImport.IsClipboardError(error)){status.Text="Clipboard is busy. Try Paste again.";}
    }
    internal bool HandleShortcut(Key key,ModifierKeys modifiers,DependencyObject? focus)
    {
        if(modifiers==ModifierKeys.Control&&key==Key.OemComma){ShowSettings();return true;}
        if(modifiers==ModifierKeys.Control&&key==Key.D1){ShowLibrary(false);return true;}
        if(modifiers==ModifierKeys.Control&&key==Key.D2){ShowLibrary(true);return true;}
        if(modifiers==ModifierKeys.Control&&key==Key.F){ShowLibrary(true);search.Focus();search.SelectAll();return true;}
        if(busy)return false;
        if(modifiers==ModifierKeys.Control&&key==Key.O){Choose();return true;}
        if(modifiers==ModifierKeys.Control&&key==Key.U){_ = CheckUpdatesAsync(true);return true;}
        if(ClipboardImport.IsEditing(focus))return false;
        if(modifiers==ModifierKeys.Control&&key==Key.V){PasteClipboard();return true;}
        if(modifiers==(ModifierKeys.Control|ModifierKeys.Shift)&&key==Key.D)
        {OpenCopiedLink();return true;}
        if((modifiers==ModifierKeys.Control&&key==Key.Space)||(modifiers==ModifierKeys.None&&key==Key.Enter&&focus is not System.Windows.Controls.Button)){ShowWheel();return true;}
        return false;
    }
    private void RenderLibrary()
    {
        libraryRows.Children.Clear();recentRows.Children.Clear();var filtered=library.Search(search.Text).ToArray();
        if(filtered.Length==0)libraryRows.Children.Add(Card(Text(library.Entries.Count==0?"Nothing here yet. Paste something or make your first fresh copy.":"No matches. Try a file name or tool.",14,"Muted")));
        foreach(var entry in filtered)libraryRows.Children.Add(LibraryRow(entry));
        recentCount.Text=library.Entries.Count.ToString();foreach(var entry in library.Search(recentSearch.Text).Take(3))recentRows.Children.Add(LibraryRow(entry,true));
        if(library.Entries.Count==0)recentRows.Children.Add(Card(Text("Your next fresh copy will land here.",13,"Muted")));
    }
    private Border LibraryRow(LibraryEntry entry,bool compact=false)
    {
        var row=new DockPanel();var available=entry.AvailablePaths;var buttons=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(buttons,Dock.Right);row.Children.Add(buttons);
        var reuse=Button("Use in Nashi",()=>{if(busy)return;var existing=entry.AvailablePaths.ToArray();if(existing.Length==0){status.Text="These files have moved or were deleted.";RenderLibrary();return;}files.Clear();Add(existing);ShowLibrary(false);status.Text=existing.Length==entry.Paths.Length?"Ready for another round.":"Available files selected. Some files have moved or were deleted.";});reuse.IsEnabled=!busy&&available.Length>0;buttons.Children.Add(reuse);
        var reveal=Button("Show in Explorer",()=>{var existing=entry.AvailablePaths;if(existing.Length>0)Reveal(existing[0]);else{status.Text="This file has moved or was deleted.";RenderLibrary();}});reveal.IsEnabled=available.Length>0;buttons.Children.Add(reveal);
        var text=new StackPanel{Margin=new(0,0,12,0),VerticalAlignment=VerticalAlignment.Center};var name=Text(entry.Name,14);name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.ToolTip=string.Join(Environment.NewLine,entry.Paths);text.Children.Add(name);text.Children.Add(Text(entry.Action+"  ·  "+entry.Created.ToLocalTime().ToString("g")+(available.Length<entry.Paths.Length?"  ·  Files missing":""),11,"Muted"));row.Children.Add(text);
        var card=Card(row);card.Padding=new(14,10,14,10);if(compact){card.CornerRadius=new(12);card.BorderThickness=new(0);card.Margin=new(0,0,0,2);}return card;
    }
    private void OpenDownload()
    {
        if(webWindow?.IsVisible==true){webWindow.Activate();link.Focus();return;}
        var dialog=new Window{Owner=this,Title="Download from the web · Nashi",Width=800,Height=650,MinWidth=640,MinHeight=400,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=new ScrollViewer{Content=webContent,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};Theme.Attach(dialog);webWindow=dialog;
        dialog.Closing+=(_,e)=>{if(busy){e.Cancel=true;status.Text="Finish or cancel the current operation first.";}};
        dialog.Closed+=(_,_)=>{inspection?.Cancel();if(dialog.Content is ScrollViewer scroll)scroll.Content=null;dialog.Content=null;webWindow=null;};dialog.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&!busy){dialog.Close();e.Handled=true;}};dialog.Show();link.Focus();
    }
    private void SetBusy(bool value){busy=value;workArea.IsEnabled=!value;webContent.IsEnabled=!value;RenderLibrary();}
    private void ShowShortcuts()=>MessageBox.Show(this,"Ctrl+O  Choose files\nCtrl+V  Paste files, text, an image or link\nCtrl+Space / Enter  Open tools\nCtrl+1 / Ctrl+2  Home / Library\nCtrl+F  Find in Library\nCtrl+Shift+D  Use a copied download link\nCtrl+U  Check for updates\n\nWheel: arrows / Tab to choose, Enter to open, Esc to close\nMedia: Space play/pause, ←/→ 1 second, Shift+←/→ 1/30s, I/O set range\nCrop: drag, Shift locks ratio, Enter saves\nEditing text: normal paste and arrow keys are preserved.","Nashi · Features & shortcuts");
    private sealed class FileNameConverter:System.Windows.Data.IValueConverter
    {
        public object Convert(object value,Type type,object parameter,System.Globalization.CultureInfo culture)=>Path.GetFileName((string)value);
        public object ConvertBack(object value,Type type,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
    }
    private bool All(string[] extensions)=>files.Count>0&&files.All(p=>extensions.Contains(Path.GetExtension(p).TrimStart('.').ToLowerInvariant()));
    private bool Images=>files.Count>0&&files.All(FeatureCatalog.IsImage);
    private bool Video=>All(["mp4","mov","mkv","webm","avi","wmv","m4v","gif"]);
    private bool Audio=>All(["mp3","m4a","wav","flac","ogg","opus","aiff","aif","wma","aac"]);
    private IReadOnlyList<ToolChoice> Choices()
    {
        return FeatureCatalog.Tools(files).Select(id=>new ToolChoice(id switch{"cropImage"=>"crop","annotateImage"=>"annotate","mergePDF"=>"mergePDFs",_=>id},FeatureCatalog.ToolNames[id],ToolSymbol(id))).ToArray();
    }
    private void Refresh()
    {
        selection.Text=files.Count==0?"Drop something good.":files.Count==1?Path.GetFileName(files[0]):$"{files.Count} files, ready to go.";fileDescription.Text=files.Count==0?"Images, videos, documents, and all the little things in between.":files.Count==1?Path.GetDirectoryName(files[0])!:"Keep them together, or give each one a fresh start.";if(chooseFilesButton is not null){chooseFilesButton.Content=files.Count==0?"Choose files":"Add or change files";if(files.Count==0){chooseFilesButton.Background=Theme.Primary;chooseFilesButton.Foreground=new SolidColorBrush(Color.FromRgb(43,54,28));}else{chooseFilesButton.SetResourceReference(Control.BackgroundProperty,"Surface");chooseFilesButton.SetResourceReference(Control.ForegroundProperty,"Ink");}}if(openWheelButton is not null)openWheelButton.Visibility=files.Count>0?Visibility.Visible:Visibility.Collapsed;list.Visibility=manage.Visibility=files.Count==0?Visibility.Collapsed:Visibility.Visible;actions.Children.Clear();garden.SetSelected(files.Count>0);
        if(files.Count>0){if(FeatureCatalog.Formats(files).Length>0)actions.Children.Add(Button("Convert ↗",async()=>await EditAsync(new("convert","Convert","↗"))));actions.Children.Add(Button("File actions",ShowFileActions));actions.Children.Add(Button("Create ZIP",async()=>await RunAsync((destination,p,t)=>Task.Run<IReadOnlyList<string>>(()=>[DocumentEngine.Zip(files.ToArray(),destination,p,t)],t))));}
    }
    private void ShowWheel(){if(!busy&&Choices().Count>0)new WheelWindow(this,Choices(),async choice=>await EditAsync(choice)).Show();}
    private async Task EditAsync(ToolChoice choice)
    {
        if(busy||files.Count==0)return;string? temporary=null;try
        {
            var requestedFormat=choice.Id.StartsWith("convert:",StringComparison.Ordinal)?choice.Id[8..]:null;if(requestedFormat is not null)choice=new("convert","Convert to "+requestedFormat.ToUpperInvariant(),"↗");
            choice=choice with{Id=choice.Id switch{"cropImage"=>"crop","annotateImage"=>"annotate","mergePDF"=>"mergePDFs",_=>choice.Id}};
            SetBusy(true);var imageSelection=Images;var mediaSelection=Video||Audio;var selected=files.ToArray();MediaInfo? info=null;BitmapSource? preview=null;var input=selected[0];
            if(choice.Id=="removeMetadata"){SetBusy(false);await EditMetadataAsync(selected);return;}
            if(choice.Id=="readQRCode"){SetBusy(false);await ReadQrAsync(selected);return;}
            if(!imageSelection&&!mediaSelection&&choice.Id is not ("extractArchive" or "unzip")){SetBusy(false);await EditDocumentAsync(choice,selected,requestedFormat);return;}
            if(imageSelection)preview=ImageEngine.Load(input);
            else if(mediaSelection)
            {
                if(!tools.Ready){MessageBox.Show(this,"Choose Set up media tools once to enable media editing.","Media setup");return;}
                var engine=new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe"));info=await engine.InspectAsync(input);
                if(choice.Id is "cropVideo" or "redactVideo")
                {
                    temporary=Path.Combine(Path.GetTempPath(),"Nashi-preview-"+Guid.NewGuid().ToString("N")+".png");
                    await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-v","error","-i",input,"-frames:v","1",temporary]);preview=ImageEngine.Load(temporary);
                }
            }
            SetBusy(false);var editor=new ToolEditor(this,choice.Id,choice.Title,input,info,preview,selected,choice.Id=="convert"?FeatureCatalog.Formats(selected):null);editor.ConfigureProfiles(preferences,requestedFormat);editor.ConfigureMediaRuntime(tools);var accepted=editor.ShowDialog()==true;if(temporary is not null)File.Delete(temporary);if(!accepted)return;
            var inputs=choice.Id is "cropVideo" or "redactVideo" or "annotate"?new[]{input}:editor.OrderedInputs;
            if(imageSelection)await RunAsync(async(destination,p,t)=>
            {
                using var tx=new ExportTransaction(destination);var exported=await OnStaAsync(()=>ImageEngine.Apply(choice.Id,inputs,tx.StagingDirectory,editor.ImageOptions with{EncoderPath=tools.Ready?tools.Executable("ffmpeg"):null},choice.Id=="convert"?editor.Format:null,t,p));
                if(choice.Id=="convert"&&editor.PreserveImageMetadata&&editor.Format is not ("pdf" or "bmp" or "gif"))
                {
                    await metadataService.InstallAsync(p,t);for(var n=0;n<exported.Count;n++)await metadataService.CopyImageTagsAsync(inputs[n],exported[n],t);
                }
                return tx.Publish(exported,t);
            },choice.Title);
            else if(mediaSelection)await RunAsync((destination,p,t)=>{var engine=new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe"));return choice.Id=="convert"?engine.ConvertAsync(inputs,editor.Format,destination,editor.MediaOptions,p,t):engine.ToolAsync(choice.Id,inputs,destination,editor.MediaOptions,p,t);},choice.Title);
            else if(choice.Id is "unzip" or "extractArchive")await RunAsync((destination,p,t)=>Task.Run<IReadOnlyList<string>>(()=>{using var tx=new ExportTransaction(destination);var outputs=inputs.SelectMany(file=>ArchiveEngine.Extract(file,tx.StagingDirectory,editor.DocumentOptions.Password,p,t)).ToList();return tx.Publish(outputs,t);},t));
            else await RunAsync((destination,p,t)=>Task.Run(()=>DocumentEngine.Pdf(choice.Id,inputs,destination,editor.Pages,t),t),choice.Title);
        }
        catch(Exception error){MessageBox.Show(this,error.Message,"Couldn’t open this tool",MessageBoxButton.OK,MessageBoxImage.Information);}
        finally{if(temporary is not null&&File.Exists(temporary))File.Delete(temporary);SetBusy(false);}
    }
    private async Task RunAsync(Func<string,IProgress<OperationProgress>,CancellationToken,Task<IReadOnlyList<string>>> work,string label="Fresh copy")
    {
        if(busy)return;var owner=webWindow?.IsActive==true?webWindow:this;string destination;
        if(label=="Move"||preferences.Value.OutputMode=="ask"||(files.Count==0&&owner==this)){var folder=new OpenFolderDialog{Title="Save new files to…",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)};if(folder.ShowDialog(owner)!=true)return;destination=folder.FolderName;}
        else destination=owner==webWindow?preferences.Value.DownloadFolder:preferences.Value.OutputMode=="folder"&&!string.IsNullOrWhiteSpace(preferences.Value.OutputFolder)?preferences.Value.OutputFolder:Path.GetDirectoryName(files[0])!;
        SetBusy(true);using var cancellation=new CancellationTokenSource();var toast=new ToastWindow(owner,cancellation.Cancel);toast.Show();
        try{var outputs=await work(destination,new Progress<OperationProgress>(toast.Update),cancellation.Token);toast.Complete(outputs);Record(label,outputs);status.Text=$"Saved {outputs.Count} new {(outputs.Count==1?"file":"files")}. Originals kept.";if(preferences.Value.RevealFinished&&outputs.Count>0)Reveal(outputs[0]);}
        catch(OperationCanceledException){toast.Close();status.Text="Cancelled. Originals kept.";}
        catch(Exception error){toast.Close();MessageBox.Show(this,error.Message,"Couldn’t finish",MessageBoxButton.OK,MessageBoxImage.Information);}
        finally{SetBusy(false);}
    }
    private static Task<IReadOnlyList<string>> OnStaAsync(Func<IReadOnlyList<string>> work)
    {
        var completion=new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>{try{completion.SetResult(work());}catch(Exception error){completion.SetException(error);}}){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
    }
    private async Task DownloadAsync()
    {
        if(busy)return;
        var payload=WebMediaPayload.Parse(link.Text);var kind=downloadKind.SelectedItem as string??"video";
        if(!tools.Ready&&kind!="image"&&payload?.Kind!="image"&&!(kind=="thumbnail"&&payload?.Kind=="youtube")){MessageBox.Show(webWindow??this,"Choose Set up media tools once to enable web downloads.","Media setup");return;}
        if(kind=="photos"&&inspectedLink!=link.Text){await InspectWebLinkAsync();return;}
        try{var url=WebDownload.Validate(payload?.Url??link.Text).AbsoluteUri;await RunAsync((destination,p,t)=>new WebDownload(tools).DownloadAsync(url,kind,destination,p,t,webQuality.SelectedIndex==1?1080:webQuality.SelectedIndex==2?720:null,kind=="photos"?new HashSet<int>(selectedWebPhotos):null),"Download "+kind);}catch(Exception error){MessageBox.Show(webWindow??this,error.Message,"Check your link");}
    }
    private async Task SetupAsync()
    {
        if(busy)return;if(tools.Ready){status.Text="Media tools are already installed.";return;}
        if(MessageBox.Show(this,"Download FFmpeg (~180 MB), yt-dlp and Deno directly from their official GitHub releases? SHA-256 hashes are verified before use. FFmpeg is GPL; the other upstream licenses and source links are listed on our Features page. No administrator password is needed.","Set up media tools",MessageBoxButton.OKCancel,MessageBoxImage.Information)!=MessageBoxResult.OK)return;
        SetBusy(true);using var cancellation=new CancellationTokenSource();var toast=new ToastWindow(this,cancellation.Cancel);toast.Show();
        try{await tools.InstallAsync(new Progress<OperationProgress>(toast.Update),cancellation.Token);toast.Close();status.Text="Media tools ready. You can edit, convert and download media.";}
        catch(OperationCanceledException){toast.Close();status.Text="Setup cancelled.";}catch(Exception error){toast.Close();MessageBox.Show(this,error.Message,"Setup couldn’t finish");}finally{SetBusy(false);}
    }
    private async Task CheckUpdatesAsync(bool user)
    {
        if(busy||checking)return;checking=true;updateButton.IsEnabled=false;if(user)status.Text="Checking for updates…";try
        {
            if(update is null){update=await PublicUpdates.CheckAsync(UpdateInstaller.Current);if(update is null){if(user)status.Text="You have the latest published Windows version.";return;}updateButton.Content="Update to "+update.Version;if(!user)return;}
            if(busy)return;
            if(MessageBox.Show(this,"Download, verify and install Nashi "+update.Version+"? The app will restart. No account or token is needed.","Nashi update",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
            SetBusy(true);using var cancellation=new CancellationTokenSource();var toast=new ToastWindow(this,cancellation.Cancel);toast.Show();
            try{await UpdateInstaller.StageAsync(update,new Progress<OperationProgress>(toast.Update),cancellation.Token);busy=false;Application.Current.Shutdown();}finally{toast.Close();SetBusy(false);}
        }
        catch(OperationCanceledException){status.Text="Update cancelled.";}catch(Exception error){if(user)MessageBox.Show(this,error.Message,"Couldn’t check for updates");}
        finally{checking=false;updateButton.IsEnabled=true;}
    }
}
