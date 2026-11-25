$exe = Join-Path $PSScriptRoot "publish\IPTray.exe"

Register-ScheduledTask -TaskName "IPTray" -Force `
  -Action    (New-ScheduledTaskAction -Execute $exe) `
  -Trigger   (New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME) `
  -Principal (New-ScheduledTaskPrincipal -UserId $env:USERNAME -RunLevel Highest -LogonType Interactive) `
  -Settings  (New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0)