using System.Windows;
namespace Nashi.Windows;
public partial class App:Application
{
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
        if(e.Args.Contains("--smoke-test")){try{SmokeTests.Run();Shutdown(0);}catch(Exception error){Console.Error.WriteLine(error);var directory=Environment.GetEnvironmentVariable("NASHI_SMOKE_DIRECTORY");if(directory is not null){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"error.txt"),error.ToString());}Shutdown(1);}return;}
        if(e.Args.Length==1&&e.Args[0]=="--install-update"){ShutdownMode=ShutdownMode.OnExplicitShutdown;_ = InstallUpdateAsync();return;}
        var main=new MainWindow(e.Args.Where(File.Exists).ToArray());main.Show();
        windowManagement=new WindowManagement(
            notice:message=>{MessageBox.Show(message,"Window arrangement",MessageBoxButton.OK,MessageBoxImage.Information);},
            isEnabled:()=>main.WindowManagementEnabled);
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
