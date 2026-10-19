using System.Windows;
namespace Nashi.Windows;
public partial class App:Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException+=(_,error)=>{var folder=Environment.GetEnvironmentVariable("NASHI_SMOKE_DIRECTORY");if(folder is not null){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"dispatcher-error.txt"),error.Exception.ToString());error.Handled=true;Shutdown(1);}};
        base.OnStartup(e);
        if(e.Args.Length==2&&e.Args[0]=="--apply-update"){try{Shutdown(UpdateInstaller.Apply(e.Args[1]));}catch(Exception error){MessageBox.Show(error.Message,"Nashi update could not finish");Shutdown(1);}return;}
        if(e.Args.Contains("--verify-update-state")){try{ShutdownMode=ShutdownMode.OnExplicitShutdown;SmokeTests.VerifyUpdateState();Shutdown(0);}catch(Exception error){Console.Error.WriteLine(error);Shutdown(1);}return;}
        if(e.Args.Contains("--smoke-test")){try{SmokeTests.Run();Shutdown(0);}catch(Exception error){Console.Error.WriteLine(error);var directory=Environment.GetEnvironmentVariable("NASHI_SMOKE_DIRECTORY");if(directory is not null){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"error.txt"),error.ToString());}Shutdown(1);}return;}
        if(e.Args.Length==1&&e.Args[0]=="--install-update"){ShutdownMode=ShutdownMode.OnExplicitShutdown;_ = InstallUpdateAsync();return;}
        new MainWindow(e.Args.Where(File.Exists).ToArray()).Show();
    }
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
