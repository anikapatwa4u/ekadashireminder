# .NET 10 Upgrade Report

## Project target framework modifications

| Project name          | Old Target Frameworks                                                        | New Target Frameworks                                                        |
|:----------------------|:----------------------------------------------------------------------------|:----------------------------------------------------------------------------|
| EkadashiReminder.csproj | net9.0-android;net9.0-ios;net9.0-maccatalyst;net9.0-windows10.0.19041.0     | net10.0-android;net10.0-ios;net10.0-maccatalyst;net10.0-windows10.0.19041.0  |

## NuGet Packages

| Package Name                        | Old Version | New Version |
|:------------------------------------|:-----------:|:-----------:|
| Microsoft.Extensions.Logging.Debug  |   9.0.9     |  10.0.9     |

Note: `Microsoft.Maui.Controls` uses the `$(MauiVersion)` property supplied by the installed .NET MAUI workload. After retargeting to net10.0 it now resolves to the latest MAUI 10.x automatically (no explicit version pin needed).

## Project feature upgrades

### EkadashiReminder

Here is what changed for the project during upgrade:

- Retargeted all platforms (Android, iOS, MacCatalyst, Windows) from net9.0 to net10.0 across the conditional `TargetFrameworks` definitions, including the Tizen comment block.
- Updated `Microsoft.Extensions.Logging.Debug` from `9.0.9` to `10.0.9`.
- Removed a duplicate `using EkadashiReminder.Models;` directive in `NotificationService.cs` (resolved warning CS0105).
- Added explicit `using` directives (System, System.Collections.Generic, System.IO, System.Linq, System.Threading.Tasks) in `EkadashiDataService.cs`.
- Verified the upgrade with a real `dotnet build` for `net10.0-windows10.0.19041.0`: **build succeeded with 0 errors**. A clean `dotnet restore` resolves all four net10.0 targets (android, ios, maccatalyst, windows) successfully.

## Next steps

- Consider addressing the pre-existing security advisory NU1904 for the transitive `System.Drawing.Common` 4.7.0 dependency (known critical severity vulnerability) if it is reachable in your app.
- Optionally update the remaining plugins to their latest versions: `Plugin.LocalNotification` (11.1.3 -> 13.0.0) and `Plugin.Maui.CalendarStore` (1.0.0 -> 5.0.0). These were not required by the framework upgrade but bring newer features/fixes.
- Optionally replace the obsolete `Page.DisplayAlert(...)` call in `RemindersPage.xaml.cs` with `DisplayAlertAsync(...)` to clear warning CS0618.
- Build and smoke-test the mobile heads (Android/iOS/MacCatalyst) on a device or emulator to validate runtime behavior on .NET 10.