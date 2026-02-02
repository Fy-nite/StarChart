# ⭐ StarChart

A **powerful OS framework** that brings retro computing dreams to life! StarChart is a custom runtime environment built on MonoGame and the AsmoV2 engine, featuring a complete virtual file system, gorgeous X11 style GUI, and a full-featured console shell. Run applications in a fully simulated operating system environment—all within your game!

## ✨ Features

- **🗂️ Virtual File System (VFS)**: Mount physical and in-memory file systems with symbolic links and advanced directory operations.
- **🖼️ Graphical Runtime**: X11 style display server with stunning window compositing, resizable windows, and fullscreen capabilities.
- **💻 Console Shell**: Powerful headless shell for interaction, featuring commands like `startw` to launch the GUI.
- **⚡ Scheduler**: Smart round-robin task scheduler for managing background jobs and scheduled operations.
- **🏗️ Modular Architecture**: Clean, extensible design with dedicated subsystems (AppFramework, GPU, PTY, stdlib, Systemd).
- **🔌 Plugin System**: Easily extend functionality with custom plugins.
- **📦 Default Applications**: Pre-loaded with essential apps (see StarChart-Software repo).

## 🏗️ Project Structure

| Directory | Purpose |
|-----------|---------|
| `Program.cs` | Entry point—boots VFS, console shell, and MonoGame runtime |
| `Runtime.cs` | Core engine managing display server, scheduler & compositing |
| `AppFramework/` | Build powerful applications for StarChart |
| `Assembly/` | Assembly utilities and tools |
| `DefaultApps/` | Essential built-in applications |
| `GPU/` | GPU abstractions and rendering backend |
| `Plugins/` | Plugin system for custom extensions |
| `PTY/` | Pseudo-terminal (pty) support |
| `stdlib/` | Standard library with W11-specific modules |
| `Systemd/` | Service management (systemd-like) |

## 🚀 Getting Started

### Prerequisites

- **.NET 10.0 SDK** or higher
- **Dependencies**: AsmoV2, Adamantite libraries

### Building

```bash
# Clone and navigate
cd StarChart

# Build the project
dotnet build
```

### Running

```bash
# Make sure root directory exists
# (e.g., bin/Debug/net10.0/root)

# Start StarChart
dotnet run

# In the console shell, launch the GUI
startx
```

### Development

- Use **Visual Studio** or **VS Code** with C# extensions
- Built on **MonoGame** for graphics and input
- Extend by adding subsystems or creating plugins
- Check out the examples folder for inspiration!

## 📚 Dependencies

| Project | Role |
|---------|------|
| **AsmoV2** | Underlying game engine core |
| **Adamantite** | VFS, graphics, GPU, and system libraries |
| **MonoGame** | Cross-platform graphics and windowing |

## 🤝 Contributing

We'd love your contributions! Help bring the OS framework to the next level:

1. **Fork** the repository
2. **Create** a feature branch
3. **Commit** your changes with clear messages
4. **Push** to your fork
5. **Submit** a pull request

Please ensure your code follows our style guidelines and includes appropriate tests. Check out our [ARCHITECTURE.md](ARCHITECTURE.md) for design patterns!

## 📖 Related Projects

- **[StarChart-Software](https://github.com/fy-nite/StarChart-Software)** — Default programs & applications ecosystem
- **[AsmoV2](https://github.com/fy-nite/AsmoV2)** — The core game engine
- **[Adamantite](https://github.com/fy-nite/Adamantite)** — System libraries & abstractions

## 📄 License

See [LICENSE](LICENSE) file in the repository root.

