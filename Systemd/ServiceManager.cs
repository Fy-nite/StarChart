using System;
using System.Collections.Generic;
using System.Linq;
using StarChart.Assembly;

namespace StarChart.Systemd
{
    /// <summary>
    /// Small service manager inspired by systemd. It can register ServiceUnit definitions and start/stop them.
    /// ExecStart may be one of:
    /// - "asm:/path" to run assembly via VFS host
    /// - arbitrary host call strings through the Assembly host (HOST <name>)
    /// - an action name that the manager understands
    /// </summary>
    public class ServiceManager
    {
        readonly Dictionary<string, ServiceUnit> _units = new(StringComparer.OrdinalIgnoreCase);

        public void Register(ServiceUnit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            _units[unit.Name] = unit;
        }

        public bool TryGet(string name, out ServiceUnit? unit) => _units.TryGetValue(name, out unit);

        public IEnumerable<ServiceUnit> List() => _units.Values.ToList();

        public void Start(string name)
        {
            if (!_units.TryGetValue(name, out var unit)) throw new InvalidOperationException("Unit not found");
            if (unit.State == ServiceState.Active || unit.State == ServiceState.Activating) return;

            unit.State = ServiceState.Activating;

            // start dependencies
            foreach (var dep in unit.After)
            {
                if (_units.TryGetValue(dep, out var dunit))
                {
                    if (dunit.State != ServiceState.Active)
                        Start(dunit.Name);
                }
            }

            try
            {
                // support execStart formats
                if (unit.ExecStart.StartsWith("asm:/", StringComparison.OrdinalIgnoreCase))
                {
                    var path = unit.ExecStart.Substring(5);
                    var host = new StarChartAssemblyHost();
                    var runtime = new AssemblyRuntime(host);

                    // read from VFS and execute
                    var vfs = Adamantite.VFS.VFSGlobal.Manager;
                    if (vfs == null) throw new InvalidOperationException("VFS not initialized");
                    if (!vfs.Exists(path)) throw new InvalidOperationException("Service asm file not found: " + path);
                    var src = System.Text.Encoding.UTF8.GetString(vfs.ReadAllBytes(path));
                    unit.State = ServiceState.Active;
                    runtime.RunSource(src);
                }
                else if (unit.ExecStart.StartsWith("host:", StringComparison.OrdinalIgnoreCase))
                {
                    var call = unit.ExecStart.Substring(5);
                    var host = new StarChartAssemblyHost();
                    host.Call(call, new AssemblyRuntimeContext());
                    unit.State = ServiceState.Active;
                }
                else
                {
                    // unknown exec type: mark failed
                    unit.State = ServiceState.Failed;
                }
            }
            catch
            {
                unit.State = ServiceState.Failed;
                throw;
            }
        }

        public void Stop(string name)
        {
            if (!_units.TryGetValue(name, out var unit)) return;
            if (unit.State != ServiceState.Active) return;

            unit.State = ServiceState.Stopping;
            // no lifecycle for now - simply mark inactive
            unit.State = ServiceState.Inactive;
        }
    }
}
