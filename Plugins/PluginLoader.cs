using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Adamantite.VFS;

namespace StarChart.Plugins
{
    /// <summary>
    /// Result of plugin discovery.
    /// </summary>
    public class PluginDiscoveryResult
    {
        public Type? PluginType { get; set; }
        public PluginKind Kind { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public Attribute? Attribute { get; set; }
    }

    public enum PluginKind
    {
        Unknown,
        App,
        WindowManager,
        DesktopEnvironment,
        Game,
        Service
    }

    /// <summary>
    /// Loads and manages StarChart plugins via reflection.
    /// </summary>
    public class PluginLoader
    {
        readonly VfsManager? _vfs;
        readonly List<IStarChartPlugin> _loadedPlugins = new();

        public IReadOnlyList<IStarChartPlugin> LoadedPlugins => _loadedPlugins.AsReadOnly();

        public PluginLoader(VfsManager? vfs = null)
        {
            _vfs = vfs;
        }

        /// <summary>
        /// Discover plugin information from a DLL without loading it.
        /// </summary>
        public PluginDiscoveryResult? DiscoverPlugin(string dllPath)
        {
            try
            {
                byte[] dllBytes;
                if (_vfs != null && _vfs.Exists(dllPath))
                {
                    dllBytes = _vfs.ReadAllBytes(dllPath);
                }
                else if (File.Exists(dllPath))
                {
                    dllBytes = File.ReadAllBytes(dllPath);
                }
                else
                {
                    return null;
                }

                var assembly = System.Reflection.Assembly.Load(dllBytes);
                return DiscoverPluginFromAssembly(assembly);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PluginLoader] Error discovering plugin: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Discover plugin from an already loaded assembly.
        /// </summary>
        public PluginDiscoveryResult? DiscoverPluginFromAssembly(System.Reflection.Assembly assembly)
        {
            try
            {
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface) continue;

                    // Check for WindowManager attribute
                    var wmAttr = type.GetCustomAttribute<StarChartWindowManagerAttribute>();
                    if (wmAttr != null)
                    {
                        return new PluginDiscoveryResult
                        {
                            PluginType = type,
                            Kind = PluginKind.WindowManager,
                            Name = wmAttr.Name,
                            Description = wmAttr.Description,
                            Version = wmAttr.Version,
                            Attribute = wmAttr
                        };
                    }

                    // Check for DesktopEnvironment attribute
                    var deAttr = type.GetCustomAttribute<StarChartDesktopEnvironmentAttribute>();
                    if (deAttr != null)
                    {
                        return new PluginDiscoveryResult
                        {
                            PluginType = type,
                            Kind = PluginKind.DesktopEnvironment,
                            Name = deAttr.Name,
                            Description = deAttr.Description,
                            Version = deAttr.Version,
                            Attribute = deAttr
                        };
                    }

                    // Check for App attribute
                    var appAttr = type.GetCustomAttribute<StarChartAppAttribute>();
                    if (appAttr != null)
                    {
                        return new PluginDiscoveryResult
                        {
                            PluginType = type,
                            Kind = PluginKind.App,
                            Name = appAttr.Name,
                            Description = appAttr.Description,
                            Version = appAttr.Version,
                            Attribute = appAttr
                        };
                    }

                    // Check for Game attribute
                    var gameAttr = type.GetCustomAttribute<StarChartGameAttribute>();
                    if (gameAttr != null)
                    {
                        return new PluginDiscoveryResult
                        {
                            PluginType = type,
                            Kind = PluginKind.Game,
                            Name = gameAttr.Name,
                            Description = gameAttr.Description,
                            Version = gameAttr.Version,
                            Attribute = gameAttr
                        };
                    }

                    // Check for Service attribute
                    var serviceAttr = type.GetCustomAttribute<StarChartServiceAttribute>();
                    if (serviceAttr != null)
                    {
                        return new PluginDiscoveryResult
                        {
                            PluginType = type,
                            Kind = PluginKind.Service,
                            Name = serviceAttr.Name,
                            Description = serviceAttr.Description,
                            Attribute = serviceAttr
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PluginLoader] Error discovering from assembly: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load and initialize a plugin from a DLL file.
        /// </summary>
        public IStarChartPlugin? LoadPlugin(string dllPath, PluginContext context)
        {
            try
            {
                var discovery = DiscoverPlugin(dllPath);
                if (discovery == null || discovery.PluginType == null)
                {
                    Console.WriteLine($"[PluginLoader] No valid plugin found in {dllPath}");
                    return null;
                }

                Console.WriteLine($"[PluginLoader] Loading {discovery.Kind}: {discovery.Name} v{discovery.Version}");

                // Create instance
                var instance = Activator.CreateInstance(discovery.PluginType);
                if (instance is not IStarChartPlugin plugin)
                {
                    Console.WriteLine($"[PluginLoader] Plugin type does not implement IStarChartPlugin");
                    return null;
                }

                // Populate PATH entries for the plugin context based on VFS
                try
                {
                    var vfs = context.VFS;
                    var paths = new List<string>();
                    // root is always useful
                    paths.Add("/");
                    if (vfs != null)
                    {
                        foreach (var candidate in new[] { "/bin", "/apps", "/usr/bin", "/home/bin" })
                        {
                            try { if (vfs.Exists(candidate)) paths.Add(candidate); } catch { }
                        }
                    }
                    context.Path = paths.ToArray();
                }
                catch { }

                // Initialize
                plugin.Initialize(context);
                _loadedPlugins.Add(plugin);

                Console.WriteLine($"[PluginLoader] Successfully loaded: {discovery.Name}");
                return plugin;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PluginLoader] Error loading plugin: {ex.Message}");
                Console.WriteLine($"[PluginLoader] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Unload a plugin.
        /// </summary>
        public void UnloadPlugin(IStarChartPlugin plugin)
        {
            try
            {
                plugin.Stop();
                _loadedPlugins.Remove(plugin);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PluginLoader] Error unloading plugin: {ex.Message}");
            }
        }

        /// <summary>
        /// Unload all plugins.
        /// </summary>
        public void UnloadAll()
        {
            var plugins = _loadedPlugins.ToList();
            foreach (var plugin in plugins)
            {
                try
                {
                    plugin.Stop();
                }
                catch { }
            }
            _loadedPlugins.Clear();
        }
    }
}
