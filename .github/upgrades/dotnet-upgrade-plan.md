c# .NET 10.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that a .NET 10.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10.0 upgrade.
3. Upgrade EkadashiReminder.csproj

## Settings

This section contains settings and data used by execution steps.

### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                        | Current Version | New Version | Description                     |
|:------------------------------------|:---------------:|:-----------:|:--------------------------------|
| Microsoft.Extensions.Logging.Debug  |   9.0.9         |  10.0.9     | Recommended for .NET 10.0       |

### Project upgrade details

This section contains details about each project upgrade and modifications that need to be done in the project.

#### EkadashiReminder.csproj modifications

Project properties changes:
  - Target frameworks should be changed from `net9.0-android;net9.0-ios;net9.0-maccatalyst;net9.0-windows10.0.19041.0` to `net10.0-android;net10.0-ios;net10.0-maccatalyst;net10.0-windows10.0.19041.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Debug should be updated from `9.0.9` to `10.0.9` (*recommended for .NET 10.0*)
