# StarChart Plugin System - Complete Implementation

## 🎉 What We Built

A powerful, attribute-based plugin system for StarChart that enables loading external DLLs as apps, window managers, desktop environments, games, and services.

## ✨ Key Features

### 1. Attribute-Based Discovery
```csharp
[StarChartWindowManager("Lantanite", "OpenBox")]
public class LantaniteWindowManager : IStarChartWindowManager
{
    // Implementation
}
```

Just add an attribute, and StarChart automatically recognizes and loads your plugin!

### 2. Type-Safe Interfaces
- `IStarChartApp` - Applications
- `IStarChartWindowManager` - Window managers
- `IStarChartDesktopEnvironment` - Desktop environments  
- `IStarChartGame` - Games
- `IStarChartService` - Background services

### 3. Reflection-Based Loader
The `PluginLoader` class:
- Discovers plugins without loading them (for menus/lists)
- Loads and initializes plugins with proper context
- Manages plugin lifecycle
- Handles errors gracefully

### 4. XTerm Integration
```bash
# In XTerm:
./lantanite.dll

# Output:
# Discovered: Lantanite (WindowManager) v1.0.0
#   OpenBox-style window manager
# Started: Lantanite
```

The Shell automatically detects `.dll` files and uses the plugin loader!

### 5. Rich Plugin Context
```csharp
public class PluginContext
{
    public DisplayServer? DisplayServer { get; set; }
    public VfsManager? VFS { get; set; }
    public string[] Arguments { get; set; }
    public string WorkingDirectory { get; set; }
}
```

Plugins get everything they need to integrate with StarChart.

## 📁 Files Created

### Core Plugin System
- **StarChart/Plugins/PluginAttributes.cs** - Attribute definitions
- **StarChart/Plugins/IStarChartPlugin.cs** - Interface definitions
- **StarChart/Plugins/PluginLoader.cs** - Reflection-based loader
- **StarChart/Plugins/README.md** - Complete documentation

### Updated Files
- **StarChart/stdlib/W11/Shell.cs** - Added plugin loading for `.dll` files
- **lantanite/LantaniteWindowManager.cs** - Added plugin attributes and interfaces

### Examples
- **ExamplePlugins/HelloWorldApp/** - Complete example app
  - HelloWorldApp.csproj
  - HelloWorldApplication.cs

## 🔄 How It Works

```
User types: ./myapp.dll
        │
        ▼
┌─────────────────────────┐
│ Shell.OnEnter()         │
│ - Detects .dll          │
│ - Calls LoadPluginFrom  │
└───────┬─────────────────┘
        │
        ▼
┌─────────────────────────┐
│ PluginLoader.Discover() │
│ - Load assembly         │
│ - Reflect for attrs     │
│ - Return info           │
└───────┬─────────────────┘
        │
        ▼
┌─────────────────────────┐
│ Shell displays info:    │
│ "Discovered: MyApp      │
│  (App) v1.0.0"          │
└───────┬─────────────────┘
        │
        ▼
┌─────────────────────────┐
│ PluginLoader.Load()     │
│ - Instantiate class     │
│ - Create context        │
│ - Call Initialize()     │
└───────┬─────────────────┘
        │
        ▼
┌─────────────────────────┐
│ plugin.Start()          │
│ - Plugin begins running │
└─────────────────────────┘
```

## 📝 Usage Examples

### Create an App

```csharp
[StarChartApp("Calculator", "Simple calculator")]
public class CalculatorApp : IStarChartApp
{
    public Window? MainWindow { get; private set; }
    
    public void Initialize(PluginContext context) { }
    public void Start() { /* Create window */ }
    public void Stop() { /* Cleanup */ }
}
```

### Create a Window Manager

```csharp
[StarChartWindowManager("MyWM", "Custom")]
public class MyWindowManager : IStarChartWindowManager
{
    public void Initialize(PluginContext context) { }
    public void Start() { }
    public void Stop() { }
    public void Update() { }
    public void HandleMouse(int x, int y, bool down, bool pressed, bool released) { }
}
```

### Create a Game

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

## 🚀 Deployment

### Build a Plugin
```bash
dotnet build -c Release
```

### Deploy
```bash
cp bin/Release/net10.0/myplugin.dll /path/to/vfs/apps/
```

### Run
```bash
# In XTerm:
./apps/myplugin.dll
```

## 🔍 Discovery API

### Get Plugin Info Without Loading
```csharp
var loader = new PluginLoader(vfs);
var info = loader.DiscoverPlugin("/apps/myapp.dll");

Console.WriteLine($"{info.Name} - {info.Kind}");
Console.WriteLine($"Version: {info.Version}");
Console.WriteLine($"Description: {info.Description}");
```

Perfect for building app menus, launchers, etc.

## 💡 Benefits

✅ **Zero Boilerplate** - Just add an attribute  
✅ **Type Safety** - Compile-time checking via interfaces  
✅ **Auto-Discovery** - No manual registration  
✅ **Flexible Context** - Provide exactly what plugins need  
✅ **Clean Separation** - Plugins are independent DLLs  
✅ **User-Friendly** - Simple `./app.dll` loading  
✅ **Extensible** - Easy to add new plugin types  

## 🎯 Example: Lantanite as Plugin

**Before:**
```csharp
// Program.cs - complex setup
var server = GetDisplayServer();
var wm = new LantaniteWindowManager(server);
// ... manual initialization ...
```

**After:**
```csharp
// LantaniteWindowManager.cs
[StarChartWindowManager("Lantanite", "OpenBox")]
public class LantaniteWindowManager : IStarChartWindowManager
{
    // Automatic initialization from plugin system!
}
```

**Usage:**
```bash
./lantanite.dll
```

That's it! No Program.cs needed anymore!

## 📊 Architecture

```
┌─────────────────────────────────────────┐
│         Plugin Ecosystem                │
│                                         │
│  ┌──────────┐  ┌──────────┐           │
│  │   Apps   │  │  Games   │           │
│  └──────────┘  └──────────┘           │
│  ┌──────────┐  ┌──────────┐           │
│  │   WMs    │  │ Services │           │
│  └──────────┘  └──────────┘           │
└───────────┬─────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────┐
│      Plugin System                      │
│                                         │
│  ┌────────────────┐  ┌───────────────┐ │
│  │  Attributes    │  │  Interfaces   │ │
│  └────────────────┘  └───────────────┘ │
│  ┌────────────────┐  ┌───────────────┐ │
│  │  Loader        │  │  Context      │ │
│  └────────────────┘  └───────────────┘ │
└───────────┬─────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────┐
│        StarChart Core                   │
│  - DisplayServer                        │
│  - VFS                                  │
│  - Shell                                │
└─────────────────────────────────────────┘
```

## 🛠️ Future Enhancements

- [ ] Plugin dependencies/requirements
- [ ] Version compatibility checking
- [ ] Hot reload/unload
- [ ] Plugin marketplace
- [ ] Sandboxing/security
- [ ] Config file support
- [ ] Inter-plugin communication
- [ ] Plugin update system

## 📚 Documentation

See **StarChart/Plugins/README.md** for complete API documentation and examples.

## 🎓 Learning Path

1. **Read** `StarChart/Plugins/README.md`
2. **Study** `ExamplePlugins/HelloWorldApp/`
3. **Try** `./helloworldapp.dll` in XTerm
4. **Build** your own plugin
5. **Share** with the community!

---

**The StarChart plugin system makes extending the desktop environment as easy as adding an attribute! 🚀**
