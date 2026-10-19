param(
    [Parameter(Mandatory=$true)][string]$AppPath,
    [Parameter(Mandatory=$true)][string]$ExpectedVersion,
    [Parameter(Mandatory=$true)][string]$ProofPath
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Run this desktop test only on an isolated GitHub Actions Windows runner.' }
if ([IO.Path]::GetFileName($AppPath) -ne 'Nashi.exe' -or !(Test-Path $AppPath)) { throw 'Choose the extracted Nashi test app.' }
if ($ExpectedVersion -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'Choose a numeric update version.' }
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Find-Element($Root, $Scope, $Conditions) {
    $condition = New-Object System.Windows.Automation.AndCondition -ArgumentList (,$Conditions)
    return $Root.FindFirst($Scope, $condition)
}
function Property-Equals($Property, $Value) {
    return New-Object System.Windows.Automation.PropertyCondition -ArgumentList $Property,$Value
}
try {
    $app = Start-Process -FilePath $AppPath -WorkingDirectory ([IO.Path]::GetDirectoryName($AppPath)) -PassThru
    $processCondition = Property-Equals ([System.Windows.Automation.AutomationElement]::ProcessIdProperty) $app.Id
    $button = $null
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while ([DateTime]::UtcNow -lt $deadline) {
        $app.Refresh()
        if ($app.HasExited) { throw 'The original app exited before showing its update.' }
        if ($app.MainWindowHandle -ne [IntPtr]::Zero) {
            $root = [System.Windows.Automation.AutomationElement]::FromHandle($app.MainWindowHandle)
            $button = Find-Element $root ([System.Windows.Automation.TreeScope]::Descendants) @(
                (Property-Equals ([System.Windows.Automation.AutomationElement]::NameProperty) ('Update to '+$ExpectedVersion)),
                (Property-Equals ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Button))
            )
            if ($null -ne $button -and $button.Current.IsEnabled) { break }
        }
        Start-Sleep -Milliseconds 500
    }
    if ($null -eq $button) { throw 'The real app never displayed its available-update button.' }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $dialog = $null
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while ([DateTime]::UtcNow -lt $deadline) {
        $dialog = Find-Element ([System.Windows.Automation.AutomationElement]::RootElement) ([System.Windows.Automation.TreeScope]::Children) @(
            $processCondition,
            (Property-Equals ([System.Windows.Automation.AutomationElement]::NameProperty) 'Nashi update')
        )
        if ($null -eq $dialog) { $dialog = Find-Element $root ([System.Windows.Automation.TreeScope]::Descendants) @($processCondition,(Property-Equals ([System.Windows.Automation.AutomationElement]::NameProperty) 'Nashi update')) }
        if ($null -ne $dialog) { break }
        Start-Sleep -Milliseconds 200
    }
    if ($null -eq $dialog) { throw 'The update confirmation did not appear.' }
    $confirm = Find-Element $dialog ([System.Windows.Automation.TreeScope]::Descendants) @(
        (Property-Equals ([System.Windows.Automation.AutomationElement]::NameProperty) 'OK'),
        (Property-Equals ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Button))
    )
    if ($null -eq $confirm) { throw 'The update confirmation had no OK action.' }
    $confirm.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    if (!$app.WaitForExit(120000)) { throw 'The real update did not close the original app within two minutes.' }
    if ($app.ExitCode -ne 0) { throw 'The original app exited with an update error.' }
    @{ availableUpdateObserved=$true; updateButtonInvoked=$true; confirmationInvoked=$true; originalProcessId=$app.Id; expectedVersion=$ExpectedVersion } | ConvertTo-Json | Set-Content -Encoding utf8 $ProofPath
} catch {
    @{ error=$_.Exception.Message } | ConvertTo-Json | Set-Content -Encoding utf8 $ProofPath
    Write-Error $_
    exit 1
}
