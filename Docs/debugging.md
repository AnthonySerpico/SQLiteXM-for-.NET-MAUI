# Debugging SQLiteXM

SQLiteXM publishes a symbol package (`.snupkg`) to nuget.org for every release since 2.0.0, so you can
set breakpoints inside SQLiteXM and step through its real source code from your own project.

The source code is embedded directly in the symbols (`EmbedAllSources`), so once the symbol
file is loaded there is nothing else to download — no GitHub round-trip, no local clone of
this repository required.

Visual Studio does **not** do this by default. The setup below is a one-time configuration.

---

## Quick setup (Visual Studio)

All five steps are required. Missing any one of them results in symbols silently failing to
load, usually with no error message.

### 1. Turn off Just My Code

**Tools → Options → Debugging → General**

- Uncheck **'Enable Just My Code'**
- Check **'Enable Source Link support'**

SQLiteXM ships as an optimised Release build, so with Just My Code enabled the debugger
classifies it as "external code" and steps *over* it — even when symbols are available.

### 2. Tell the debugger to search for SQLiteXM.dll

**Tools → Options → Debugging → Symbols**

Under **'Included modules'**, click **'Add'** and enter:

```
SQLiteXM.dll
```

This step is the one most people miss. 

### 3. Enable the NuGet.org symbol server

- On the same page, under **'Search Locations'**, check **'Download symbols from the NuGet.org Symbol Server'**

### 4. Set a symbol cache directory

On the same page, make sure **'Cache symbols in this directory'** is not blank. It needs 
to be set to the location where you want to store the downloaded symbols, for example:

```
C:\SymbolCache
```

The symbol server needs a writable local folder to download PDBs into.


### 5. Restart the debug session

Symbols are resolved when a module is first loaded. Stop debugging completely (**Shift+F5**)
and start again — resuming a paused session will not pick up the new settings.

---

## Verifying it worked

While debugging, open **Debug → Windows → Modules**, find `SQLiteXM.dll`, then right-click it
and choose **'Symbol Load Information...'**.

You are looking for a line referencing `SQLiteXM.pdb` with **'Symbols loaded'**. You can
also confirm on disk — a successful download leaves a file laid out like this:

```
C:\SymbolCache\SQLiteXM.pdb\<GUID><age>\SQLiteXM.pdb
```

Then set a breakpoint in your own code, call into SQLiteXM, and press **F11**. You should land
in SQLiteXM source rather than stepping over the call.

---

## Troubleshooting

**Nothing referencing `SQLiteXM.pdb` appears in Symbol Load Information**

The module filter is almost certainly the cause. Re-check step 4. Alternatively, change
**Automatic symbol searching** from "Automatically choose what module symbols to search for"
to the option that searches **all** modules.

**It worked once and then stopped**

Visual Studio caches failed lookups as well as successful ones. Use **Empty Symbol Cache** on
the Symbols page, then start a fresh debug session.

**Stepping jumps around, or locals show as "optimized away"**

Expected. The published assembly is an optimised Release build. The source shown is correct,
but execution will not always be strictly line-by-line.

**Visual Studio 2026 note**

In VS 2026 these options live under **All Settings → Debugging → Symbols**, split across the
**Search and Load** and **Search Locations** sub-pages. "Included modules" is on *Search and
Load*; the symbol server checkbox and cache directory are on *Search Locations*.

---

## Fallback: load the symbols manually

If the symbol server will not cooperate, you can bypass it entirely by manually 
downloading the PDB and placing it where the debugger will find it.


### 1. Turn off Just My Code

**Tools → Options → Debugging → General**

- Uncheck **'Enable Just My Code'**
- Check **'Enable Source Link support'**

### 2. Manually download the symbol package

In the powershell script below:

- Replace `$version` `2.0.0` with the version of SQLiteXM you are referencing, and `$tfm` `net9.0` with your target framework.
- Replace `$dest` with your application's output folder, for example, `C:\Users\Me\source\repos\App1\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\`

Run the PowerShell snippet below in a terminal.

```powershell
$version = '2.0.0'
$tfm     = 'net9.0'
$dest    = 'C:\Users\ajser\source\repos\MauiApp1\MauiApp1\bin\Debug\net9.0-windows10.0.19041.0\win10-x64'

New-Item -ItemType Directory -Force -Path $dest | Out-Null
$zip = "$env:TEMP\sqlitexm.$version.zip"

Invoke-WebRequest "https://globalcdn.nuget.org/symbol-packages/sqlitexm.$version.snupkg" `
	-OutFile $zip -TimeoutSec 60
Expand-Archive $zip "$env:TEMP\sqlitexm-symbols" -Force
Copy-Item "$env:TEMP\sqlitexm-symbols\lib\$tfm\SQLiteXM.pdb" $dest -Force

Get-ChildItem $dest
```

This will download the symbol package, and copy it to your application's output folder. The debugger will find it automatically.

> ✏️ **Note:**The powershell script will need to be run each time after doing a clean build, since the output folder is wiped out. 
> As an alternative, you can copy the PDB to a permanent location and manually copy it to your application's output folder.

---

## Platform support

Symbol server downloads are a Visual Studio debugger feature and are most reliable when
debugging the **Windows** target of a .NET MAUI app.

When debugging on **Android** or **iOS**, the PDB generally needs to be deployed alongside the
assembly, and on-demand symbol server retrieval may not apply. If you need to step into
SQLiteXM on a mobile target, prefer the manual fallback above, or reference the SQLiteXM
project directly while investigating.

---

## Alternative: reference the project directly

If you are actively modifying SQLiteXM rather than just reading through it, the simplest
option is to skip symbols altogether. Clone this repository and swap the package reference in
your app:

```xml
<!-- <PackageReference Include="SQLiteXM" Version="2.0.0" /> -->
<ProjectReference Include="..\..\SQLiteXM\SQLiteXM\SQLiteXM.csproj" />
```

This gives full source debugging with no configuration at all, and your changes to SQLiteXM
are picked up on every build. Remember to switch back to the `PackageReference` before you
ship.
