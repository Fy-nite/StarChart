using System.Collections.Generic;

namespace StarChart.Systemd
{
    public enum ServiceState
    {
        Inactive,
        Activating,
        Active,
        Failed,
        Stopping
    }

    public class ServiceUnit
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ExecStart { get; set; } = string.Empty;
        public List<string> WantedBy { get; set; } = new();
        public List<string> After { get; set; } = new();
        public bool Enabled { get; set; } = true;

        // runtime
        public ServiceState State { get; set; } = ServiceState.Inactive;
    }
}
