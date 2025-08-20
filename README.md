# VeeamTestTask-FolderSync


## Overview
This is a simple folder synchronization console app.
It keeps a replica folder in sync with a source folder at a specified interval, logging all operations.

## How to Run
1. Build the solution using Visual Studio or `dotnet build`.
2. Run the application from the command line:

```
dotnet run --project FolderSync.App -- -s "<SourceFolder>" -r "<ReplicaFolder>" -i <IntervalMs> -l "<LogFilePath>"
```

Or, if you have a compiled executable (e.g., `FolderSync.exe`):

```
FolderSync.exe -s "C:\Source" -r "C:\Replica" -i 2000 -l "C:\Logs\sync.log"
```

### Arguments
- `-s`, `--source`   : Path to the source folder (required)
- `-r`, `--replica`  : Path to the replica folder (required)
- `-i`, `--interval` : Synchronization interval in milliseconds (required)
- `-l`, `--log`      : Path to the log file (required)

## Where It Can Be Used
- Keeping a backup folder in sync with a main folder (even remote, virtual machine, server)

## Example

```
dotnet run --project FolderSync.App -- -s "C:\Data\Source" -r "C:\Data\Replica" -i 5000 -l "C:\Logs\sync.log"
```

This will synchronize `C:\Data\Source` to `C:\Data\Replica` every 5 seconds, logging to `C:\Logs\sync.log`.