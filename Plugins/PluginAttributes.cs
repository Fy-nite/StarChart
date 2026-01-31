using System;

namespace StarChart.Plugins
{
    /// <summary>
    /// Marks a class as a StarChart application.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StarChartAppAttribute : Attribute
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string Author { get; set; } = string.Empty;

        public StarChartAppAttribute() { }

        public StarChartAppAttribute(string name, string description = "")
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// Marks a class as a StarChart window manager.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StarChartWindowManagerAttribute : Attribute
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string Style { get; set; } = "Default";

        public StarChartWindowManagerAttribute() { }

        public StarChartWindowManagerAttribute(string name, string style = "Default")
        {
            Name = name;
            Style = style;
        }
    }

    /// <summary>
    /// Marks a class as a StarChart desktop environment.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StarChartDesktopEnvironmentAttribute : Attribute
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public bool IncludesWindowManager { get; set; } = true;
        public bool IncludesPanel { get; set; } = true;

        public StarChartDesktopEnvironmentAttribute() { }

        public StarChartDesktopEnvironmentAttribute(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Marks a class as a StarChart game.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StarChartGameAttribute : Attribute
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string Author { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;

        public StarChartGameAttribute() { }

        public StarChartGameAttribute(string name, string genre = "")
        {
            Name = name;
            Genre = genre;
        }
    }

    /// <summary>
    /// Marks a class as a StarChart service/daemon.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StarChartServiceAttribute : Attribute
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool AutoStart { get; set; } = false;

        public StarChartServiceAttribute() { }

        public StarChartServiceAttribute(string name, bool autoStart = false)
        {
            Name = name;
            AutoStart = autoStart;
        }
    }
}
