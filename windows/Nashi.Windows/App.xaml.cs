using System.Windows;
namespace Nashi.Windows;
public partial class App:Application
{
    private SingleInstance? instance;
    internal static bool IsSmokeTest{get;private set;}
    private WindowManagement? windowManagement;
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException+=(_,error)=>{var folder=Environment.GetEnvironmentVariable("NASHI_SMOKE_DIRECTORY");if(folder is not null){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"dispatcher-error.txt"),error.Exception.ToString());error.Handled=true;Shutdown(1);}};
        base.OnStartup(e);
        if(e.Args.Contains("--window-management-fixture"))
        {
            new Window{Title="Nashi Window Layout Fixture",Width=620,Height=420,Left=80,Top=90,WindowStartupLocation=WindowStartupLocation.Manual,
                Content=new System.Windows.Controls.TextBlock{Text="Nashi window arrangement test fixture",Margin=new Thickness(30),FontSize=22}}.Show();return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--apply-update"){try{Shutdown(UpdateInstaller.Apply(e.Args[1]));}catch(Exception error){MessageBox.Show(error.Message,"Nashi update could not finish");Shutdown(1);}return;}
        if(e.Args.Contains("--verify-update-state")){try{ShutdownMode=ShutdownMode.OnExplicitShutdown;SmokeTests.VerifyUpdateState();Shutdown(0);}catch(Exception error){Console.Error.WriteLine(error);Shutdown(1);}return;}
        if(e.Args.Contains("--smoke-test")){IsSmokeTest=true;try{SmokeTests.Run();Shutdown(0);}catch(Exception error){Console.Error.WriteLine(error);var directory=Environment.GetEnvironmentVariable("NASHI_SMOKE_DIRECTORY");if(directory is not null){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"error.txt"),error.ToString());}Shutdown(1);}return;}
        if(e.Args.Length==1&&e.Args[0]=="--install-update"){ShutdownMode=ShutdownMode.OnExplicitShutdown;_ = InstallUpdateAsync();return;}
        instance=new SingleInstance();Exit+=(_,_)=>instance.Dispose();var paths=e.Args.Where(p=>File.Exists(p)||Directory.Exists(p)).ToArray();
        if(!instance.Primary){ShutdownMode=ShutdownMode.OnExplicitShutdown;_ = ForwardAndExit(paths);return;}
        var window=new MainWindow(paths);instance.Start(incoming=>Dispatcher.BeginInvoke(()=>window.AcceptLaunch(incoming)));window.Show();
        windowManagement=new WindowManagement(notice:message=>MessageBox.Show(message,"Window arrangement",MessageBoxButton.OK,MessageBoxImage.Information),isEnabled:()=>window.WindowManagementEnabled);
    }
    private async Task ForwardAndExit(string[] paths)
    {
        try{await instance!.Notify(paths);Shutdown(0);}catch(Exception error)when(error is IOException or OperationCanceledException or UnauthorizedAccessException){MessageBox.Show("Nashi is already running but is still starting. Open it from the system tray or try again in a moment.","Nashi");Shutdown(1);}
    }
    protected override void OnExit(ExitEventArgs e){windowManagement?.Dispose();base.OnExit(e);}
    private async Task InstallUpdateAsync()
    {
        try
        {
            var update=await Nashi.Core.PublicUpdates.CheckAsync(UpdateInstaller.Current);
            if(update is not null)await UpdateInstaller.StageAsync(update,new Progress<Nashi.Core.OperationProgress>(),CancellationToken.None);
            Shutdown(0);
        }
        catch(Exception error)
        {
            var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nashi");Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"update-error.txt"),error.ToString());Shutdown(1);
        }
    }
}
