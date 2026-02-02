# StarChart

StarChart is a custom runtime environment and operating system framework built on top of MonoGame and the AsmoV2 engine. It provides a virtual file system (VFS), a graphical user interface (GUI) with window compositing, a console shell, and various subsystems for building and running applications in a simulated OS environment.

## Features

- **Virtual File System (VFS)**: Supports mounting physical and in-memory file systems, with symbolic links and directory operations.
- **Graphical Runtime**: Hosts a W11-style display server that composites windows onto a canvas, allowing for resizable, fullscreen-capable applications.
- **Console Shell**: Includes a headless console shell for initial interaction, with commands like `startx` to launch the GUI.
- **Scheduler**: A round-robin scheduler for managing scheduled tasks and jobs.
- **Modular Architecture**: Organized into subsystems like AppFramework, GPU, PTY, stdlib, and Systemd for extensibility.
- **Plugin Support**: Supports plugins for extending functionality.
- **Default Applications**: Comes with default apps provided in the StarChart-Software repository.

## Project Structure

- `Program.cs`: Entry point that sets up VFS, runs the console shell, and launches the MonoGame runtime.
- `Runtime.cs`: Core runtime class that manages the display server, scheduler, and window compositing.
- `AppFramework/`: Framework for building applications.
- `Assembly/`: Assembly-related utilities.
- `DefaultApps/`: Default applications included with StarChart.
- `GPU/`: GPU-related abstractions and implementations.
- `Plugins/`: Plugin system for extensions.
- `PTY/`: Pseudo-terminal support.
- `stdlib/`: Standard library components, including W11-specific modules.
- `Systemd/`: Systemd-like service management.

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- Dependencies: AsmoV2 (VBlank.csproj), Adamantite libraries

### Building

1. Clone the repository.
2. Navigate to the StarChart directory.
3. Run `dotnet build` to build the project.

### Running

1. Ensure the `root` directory exists in the output folder (e.g., `bin/Debug/net10.0/root`).
2. Run `dotnet run` to start StarChart.
3. The console shell will start first. Type `startx` to launch the GUI runtime.

### Development

- Use Visual Studio or VS Code with C# extensions.
- The project uses MonoGame for graphics and input handling.
- Extend functionality by adding to the subsystems or creating plugins.

## Dependencies

- **AsmoV2**: The underlying game engine.
- **Adamantite**: Provides VFS, GFX, GPU, and other core components.
- **MonoGame**: For cross-platform game development and windowing.

## Contributing

Contributions are welcome! Please fork the repository and submit pull requests. Ensure code follows the project's style and includes appropriate tests.

## License

See LICENSE file in the repository root.

## Related Projects

- [StarChart-Software](https://github.com/your-repo/StarChart-Software): Default programs and applications for StarChart.
- [AsmoV2](https://github.com/your-repo/AsmoV2): The core engine.
- [Adamantite](https://github.com/your-repo/Adamantite): Core libraries.