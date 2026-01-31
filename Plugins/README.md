# StarChart Plugin System

## Overview

StarChart now supports a powerful reflection-based plugin system that allows you to load:
- **Applications** - GUI or console apps
- **Window Managers** - Like Lantanite or TWM
- **Desktop Environments** - Complete DE with WM + panels + etc.
- **Games** - Interactive games
- **Services** - Background daemons

## Quick Start

### 1. Create a Plugin

```csharp
using StarChart.Plugins;
using StarChart.stdlib.W11;

[StarChartApp("MyApp", "My awesome application")]
public class MyApplication : IStarChartApp
{
    public Window? MainWindow { get; private set; }

    public void Initialize(PluginContext context)
    {
        // Setup your app with the provided context
        // context.DisplayServer, context.VFS, context.Arguments
    }

    public void Start()
    {
        // Start your app
    }

    public void Stop()
    {
        // Clean up
    }
}
```

### 2. Build as DLL

```bash
dotnet build -c Release
```

### 3. Load from XTerm

```bash
./myapp.dll
```

That's it! StarChart automatically:
1. Reflects into your DLL
2. Finds the attributed class
3. Determines the plugin type
4. Initializes it with proper context
5. Starts it

## Plugin Types

### Application

```csharp
[StarChartApp("Calculator", "Simple calculator app")]
public class Calculator : IStarChartApp
{
    public Window? MainWindow { get; private set; }
    
    public void Initialize(PluginContext context) { }
    public void Start() { /* Create window */ }
    public void Stop() { /* Cleanup */ }
}
```

**Properties:**
- `Name` - App name
- `Description` - Short description
- `Version` - Version string (default: "1.0.0")
- `Author` - Author name

**Interface Methods:**
- `Initialize(PluginContext)` - Setup
- `Start()` - Begin execution
- `Stop()` - Cleanup
- `MainWindow` - Primary window (if any)

### Window Manager

```csharp
[StarChartWindowManager("Lantanite", "OpenBox")]
public class LantaniteWM : IStarChartWindowManager
{
    public void Initialize(PluginContext context) { }
    public void Start() { }
    public void Stop() { }
    public void Update() { /* Per-frame updates */ }
    public void HandleMouse(int x, int y, bool down, bool pressed, bool released) { }
}
```

**Properties:**
- `Name` - WM name
- `Description` - Short description
- `Version` - Version string
- `Style` - Style name (e.g., "OpenBox", "TWM", "i3")

**Interface Methods:**
- `Initialize(PluginContext)` - Setup
- `Start()` - Begin managing windows
- `Stop()` - Cleanup
- `Update()` - Called each frame
- `HandleMouse(...)` - Handle mouse input

### Desktop Environment

```csharp
[StarChartDesktopEnvironment("MyDE")]
public class MyDesktopEnv : IStarChartDesktopEnvironment
{
    public IStarChartWindowManager? WindowManager { get; private set; }
    
    public void Initialize(PluginContext context) { }
    public void Start() { }
    public void Stop() { }
    public void Update() { }
}
```

**Properties:**
- `Name` - DE name
- `Description` - Short description
- `Version` - Version string
- `IncludesWindowManager` - Whether it has a WM (default: true)
- `IncludesPanel` - Whether it has a panel/taskbar (default: true)

### Game

```csharp
[StarChartGame("Pong", "Arcade")]
public class PongGame : IStarChartGame
{
    public void Initialize(PluginContext context) { }
    public void Start() { }
    public void Stop() { }
    public void Update(double deltaTime) { }
    public void HandleInput(int mouseX, int mouseY, bool[] mouseButtons, bool[] keys) { }
}
```

**Properties:**
- `Name` - Game name
- `Description` - Short description  
- `Version` - Version string
- `Author` - Author name
- `Genre` - Game genre (e.g., "Arcade", "Puzzle", "RPG")

### Service

```csharp
[StarChartService("FileWatcher", AutoStart = true)]
public class FileWatcherService : IStarChartService
{
    public void Initialize(PluginContext context) { }
    public void Start() { }
    public void Stop() { }
    public void Update() { /* Periodic updates */ }
}
```

**Properties:**
- `Name` - Service name
- `Description` - Short description
- `AutoStart` - Whether to start automatically (default: false)

## Plugin Context

All plugins receive a `PluginContext` during initialization:

```csharp
public class PluginContext
{
    public DisplayServer? DisplayServer { get; set; }
    public VfsManager? VFS { get; set; }
    public string[] Arguments { get; set; }
    public string WorkingDirectory { get; set; }
}
```

**Usage:**

```csharp
public void Initialize(PluginContext context)
{
    // Access display server
    var server = context.DisplayServer;
    
    // Access VFS
    var bytes = context.VFS?.ReadAllBytes("/config.txt");
    
    // Get command-line args
    foreach (var arg in context.Arguments)
    {
        Console.WriteLine(arg);
    }
    
    // Get working directory
    Console.WriteLine($"Working in: {context.WorkingDirectory}");
}
```

## Discovery vs Loading

### Discovery (Info Only)

```csharp
var loader = new PluginLoader(vfs);
var info = loader.DiscoverPlugin("/apps/myapp.dll");

if (info != null)
{
    Console.WriteLine($"Found: {info.Name} ({info.Kind})");
    Console.WriteLine($"Version: {info.Version}");
    Console.WriteLine($"Description: {info.Description}");
}
```

**Discovers without loading** - useful for app lists, menus, etc.

### Loading (Full Initialization)

```csharp
var context = new PluginContext
{
    DisplayServer = myServer,
    VFS = myVfs,
    Arguments = new[] { "arg1", "arg2" }
};

var plugin = loader.LoadPlugin("/apps/myapp.dll", context);
if (plugin != null)
{
    plugin.Start();
}
```

**Fully loads and initializes** the plugin.

## XTerm Integration

The Shell automatically uses the plugin loader for `.dll` files:

```bash
# In XTerm:
./myapp.dll

# Output:
# Discovered: MyApp (App) v1.0.0
#   My awesome application
# Started: MyApp
# Press Ctrl+C or type 'stop' to stop the plugin
```

The shell:
1. Detects `.dll` extension
2. Uses `PluginLoader.DiscoverPlugin()`
3. Shows info to user
4. Creates `PluginContext` with DisplayServer and VFS
5. Calls `LoadPlugin()` and `Start()`

## Updating Lantanite

Lantanite now uses the plugin system:

```csharp
[StarChartWindowManager("Lantanite", "OpenBox")]
public class LantaniteWindowManager : IStarChartWindowManager
{
    // ... implementation ...
}
```

**Before:**
```bash
# Had to manually instantiate
var wm = new LantaniteWindowManager(server);
```

**After:**
```bash
# Just run it!
./lantanite.dll

# Output:
# Discovered: Lantanite (WindowManager) v1.0.0
#   OpenBox-style window manager
# Started: Lantanite
```

## Example: Complete App

```csharp
using System;
using StarChart.Plugins;
using StarChart.stdlib.W11;

namespace MyCalculator
{
    [StarChartApp("Calculator", "Simple RPN calculator")]
    public class CalculatorApp : IStarChartApp
    {
        Window? _window;
        DisplayServer? _server;
        
        public Window? MainWindow => _window;

        public void Initialize(PluginContext context)
        {
            _server = context.DisplayServer;
        }

        public void Start()
        {
            if (_server == null)
            {
                Console.WriteLine("No display server - console mode");
                RunConsole();
                return;
            }

            // Create window
            var geom = new WindowGeometry(100, 100, 320, 240);
            _window = _server.CreateWindow(
                "calculator",
                "Calculator",
                geom,
                WindowStyle.Titled
            );

            // Draw UI
            DrawUI();
            _window.Map();
        }

        public void Stop()
        {
            if (_window != null && _server != null)
            {
                _server.DestroyWindow(_window);
            }
        }

        void DrawUI()
        {
            // Draw calculator UI on canvas
            var canvas = _window!.Canvas;
            // ... drawing code ...
        }

        void RunConsole()
        {
            // Console-only version
            Console.WriteLine("Calculator ready!");
            // ... REPL loop ...
        }
    }
}
```

## Benefits

✅ **No Manual Registration** - Attributes handle it  
✅ **Auto-Discovery** - Reflect and find plugins  
✅ **Type Safety** - Interfaces enforce contracts  
✅ **Flexible Context** - Provide what each plugin needs  
✅ **Clean Separation** - Plugins are independent DLLs  
✅ **Easy Distribution** - Just copy DLL files  
✅ **User-Friendly** - Simple `./app.dll` loading  

## Building a Plugin Project

### 1. Create Project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <ProjectReference Include="..\StarChart\StarChart.csproj" />
  </ItemGroup>
</Project>
```

### 2. Implement Plugin

```csharp
using StarChart.Plugins;

[StarChartApp("MyApp")]
public class MyApp : IStarChartApp
{
    // Implementation
}
```

### 3. Build

```bash
dotnet build -c Release
```

### 4. Deploy

```bash
cp bin/Release/net10.0/myapp.dll /path/to/starchart/vfs/apps/
```

### 5. Run

```bash
# In XTerm:
./apps/myapp.dll
```

## Future Enhancements

- [ ] Plugin dependencies
- [ ] Plugin versioning/compatibility checks
- [ ] Hot-reload support
- [ ] Plugin marketplace/repository
- [ ] Sandboxing/security
- [ ] Plugin configuration files
- [ ] Inter-plugin communication

---

**The StarChart plugin system makes it incredibly easy to extend the system with new apps, games, window managers, and more!** 🚀
