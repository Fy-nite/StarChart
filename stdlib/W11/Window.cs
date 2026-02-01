using System;
using System.Collections.Generic;

namespace StarChart.stdlib.W11
{
    // The minimal W11 windowing API used by the runtime and TWM.
    public class WindowGeometry
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public WindowGeometry(int x, int y, int width, int height)
        {
            X = x; Y = y; Width = width; Height = height;
        }
    }

    public enum WindowStyle
    {
        Titled,
        Borderless,
        Popup
    }

    public class Canvas
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public byte[] PixelBuffer { get; private set; }

        int _dirtyX;
        int _dirtyY;
        int _dirtyW;
        int _dirtyH;
        bool _dirty;

        public bool IsDirty => _dirty;
        public int DirtyX => _dirtyX;
        public int DirtyY => _dirtyY;
        public int DirtyWidth => _dirtyW;
        public int DirtyHeight => _dirtyH;

        public Canvas(int width, int height)
        {
            Width = Math.Max(0, width);
            Height = Math.Max(0, height);
            PixelBuffer = new byte[Width * Height * 4];
            MarkDirtyRect(0, 0, Width, Height);
        }

        public void Resize(int width, int height)
        {
            Width = Math.Max(0, width);
            Height = Math.Max(0, height);
            PixelBuffer = new byte[Width * Height * 4];
            MarkDirtyRect(0, 0, Width, Height);
        }

        public void SetPixel(int x, int y, uint argb)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            var idx = (y * Width + x) * 4;
            PixelBuffer[idx + 0] = (byte)((argb >> 24) & 0xFF);
            PixelBuffer[idx + 1] = (byte)((argb >> 16) & 0xFF);
            PixelBuffer[idx + 2] = (byte)((argb >> 8) & 0xFF);
            PixelBuffer[idx + 3] = (byte)(argb & 0xFF);
            MarkDirtyRect(x, y, 1, 1);
        }

        public uint GetPixel(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
            var idx = (y * Width + x) * 4;
            uint a = PixelBuffer[idx + 0];
            uint r = PixelBuffer[idx + 1];
            uint g = PixelBuffer[idx + 2];
            uint b = PixelBuffer[idx + 3];
            return (a << 24) | (r << 16) | (g << 8) | b;
        }

        public void Clear(uint argb)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    SetPixel(x, y, argb);
        }

        public void ClearDirty()
        {
            _dirty = false;
            _dirtyX = 0;
            _dirtyY = 0;
            _dirtyW = 0;
            _dirtyH = 0;
        }

        public void MarkDirtyRect(int x, int y, int w, int h)
        {
            if (w <= 0 || h <= 0) return;
            if (!_dirty)
            {
                _dirty = true;
                _dirtyX = x;
                _dirtyY = y;
                _dirtyW = w;
                _dirtyH = h;
                return;
            }

            int x0 = Math.Min(_dirtyX, x);
            int y0 = Math.Min(_dirtyY, y);
            int x1 = Math.Max(_dirtyX + _dirtyW, x + w);
            int y1 = Math.Max(_dirtyY + _dirtyH, y + h);
            _dirtyX = x0;
            _dirtyY = y0;
            _dirtyW = x1 - x0;
            _dirtyH = y1 - y0;
        }
    }

    public class Window
    {
        public uint XID { get; }
        // RenderId used by compositors/caches to identify window surfaces.
        public uint RenderId => XID;
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; set; }
        public string Title { get; set; }
        public WindowGeometry Geometry { get; set; }
        public WindowStyle Style { get; set; }
        public bool IsMapped { get; private set; }
        public bool IsDestroyed { get; private set; }
        public Canvas Canvas { get; private set; }
        public int Scale { get; private set; } = 1;
        internal DisplayServer? Owner { get; set; }

        internal Window(uint xid, string name, string title, WindowGeometry geometry, WindowStyle style, int scale = 1)
        {
            XID = xid;
            Name = name;
            Title = title;
            Geometry = geometry;
            Style = style;
            IsMapped = false;
            IsDestroyed = false;
            Canvas = new Canvas(geometry.Width, geometry.Height);
            SetScale(scale, preserveOnScreenSize: false);
        }

        public void Map()
        {
            IsMapped = true;
            Owner?.NotifyWindowMapped(this);
            Owner?.MarkFullRedraw();
        }

        public void Unmap()
        {
            IsMapped = false;
            Owner?.NotifyWindowUnmapped(this);
            Owner?.MarkFullRedraw();
        }

        internal void MarkDestroyed()
        {
            IsDestroyed = true;
            Owner?.NotifyWindowDestroyed(this);
        }

        public void ResizeCanvas(int width, int height)
        {
            if (width == Canvas.Width && height == Canvas.Height) return;
            Canvas.Resize(width, height);
            Geometry.Width = width;
            Geometry.Height = height;
        }

        public void SetScale(int scale, bool preserveOnScreenSize = true)
        {
            if (scale < 1) scale = 1;
            if (!preserveOnScreenSize)
            {
                Scale = scale;
                return;
            }

            var oldScale = Scale <= 0 ? 1 : Scale;
            long onscreenW = (long)Geometry.Width * oldScale;
            long onscreenH = (long)Geometry.Height * oldScale;

            Scale = scale;

            int newW = Math.Max(1, (int)Math.Round((double)onscreenW / Scale));
            int newH = Math.Max(1, (int)Math.Round((double)onscreenH / Scale));
            ResizeCanvas(newW, newH);
        }

        public void SetOnScreenSize(int onscreenWidth, int onscreenHeight)
        {
            int scale = Math.Max(1, Scale);
            int newW = Math.Max(1, (int)Math.Round((double)onscreenWidth / scale));
            int newH = Math.Max(1, (int)Math.Round((double)onscreenHeight / scale));
            ResizeCanvas(newW, newH);
        }

        public void SetContentSize(int contentWidth, int contentHeight)
        {
            ResizeCanvas(contentWidth, contentHeight);
        }
    }

    public class DisplayServer
    {
        readonly List<Window> _windows = new();
        uint _nextXid = 1;
        public IReadOnlyList<Window> Windows => _windows.AsReadOnly();
        // Optional window manager (TWM or plugin) registered on the server.
        public StarChart.Plugins.IStarChartWindowManager? WindowManager { get; internal set; }
        public Window? FocusedWindow { get; private set; }
        bool _fullRedrawNeeded = true;

        public event Action<Window>? WindowCreated;
        public event Action<Window>? WindowMapped;
        public event Action<Window>? WindowUnmapped;
        public event Action<Window>? WindowDestroyed;
        public event Action<Window>? WindowFocused;

        public Window CreateWindow(string title, WindowGeometry geometry, WindowStyle style = WindowStyle.Titled, int scale = 1)
        {
            var xid = _nextXid++;
            var w = new Window(xid, title, title, geometry, style, scale);
            w.Owner = this;
            _windows.Add(w);
            WindowCreated?.Invoke(w);
            MarkFullRedraw();
            return w;
        }

        public Window CreateWindow(string name, string title, WindowGeometry geometry, WindowStyle style = WindowStyle.Titled, int scale = 1)
        {
            var xid = _nextXid++;
            var w = new Window(xid, name, title, geometry, style, scale);
            w.Owner = this;
            _windows.Add(w);
            WindowCreated?.Invoke(w);
            MarkFullRedraw();
            return w;
        }

        public Window? GetWindowById(uint xid)
        {
            for (int i = 0; i < _windows.Count; i++)
            {
                if (_windows[i].XID == xid) return _windows[i];
            }
            return null;
        }

        public bool SetWindowScale(uint xid, int scale, bool preserveOnScreenSize = true)
        {
            var w = GetWindowById(xid);
            if (w == null) return false;
            w.SetScale(scale, preserveOnScreenSize);
            MarkFullRedraw();
            return true;
        }

        public bool SetWindowOnScreenSize(uint xid, int onscreenWidth, int onscreenHeight)
        {
            var w = GetWindowById(xid);
            if (w == null) return false;
            w.SetOnScreenSize(onscreenWidth, onscreenHeight);
            MarkFullRedraw();
            return true;
        }

        public bool SetWindowContentSize(uint xid, int contentWidth, int contentHeight)
        {
            var w = GetWindowById(xid);
            if (w == null) return false;
            w.SetContentSize(contentWidth, contentHeight);
            MarkFullRedraw();
            return true;
        }

        public bool DestroyWindow(Window w)
        {
            if (w == null) return false;
            var removed = _windows.Remove(w);
            if (removed)
            {
                w.MarkDestroyed();
                if (FocusedWindow == w) FocusedWindow = null;
                MarkFullRedraw();
            }
            return removed;
        }

        public bool DestroyWindowById(uint xid)
        {
            var w = GetWindowById(xid);
            if (w == null) return false;
            return DestroyWindow(w);
        }

        public bool BringToFront(Window w)
        {
            if (w == null) return false;
            var idx = _windows.IndexOf(w);
            if (idx < 0) return false;
            _windows.RemoveAt(idx);
            _windows.Add(w);
            MarkFullRedraw();
            return true;
        }

        public bool MoveWindow(Window w, int x, int y)
        {
            if (w == null) return false;
            w.Geometry.X = x;
            w.Geometry.Y = y;
            MarkFullRedraw();
            return true;
        }

        public bool FocusWindow(Window w)
        {
            if (w == null) return false;
            FocusedWindow = w;
            WindowFocused?.Invoke(w);
            return true;
        }

        // Internal notification helpers used by Window to inform the server of state changes.
        internal void NotifyWindowMapped(Window w) => WindowMapped?.Invoke(w);
        internal void NotifyWindowUnmapped(Window w) => WindowUnmapped?.Invoke(w);
        internal void NotifyWindowDestroyed(Window w) => WindowDestroyed?.Invoke(w);

        internal void MarkFullRedraw() => _fullRedrawNeeded = true;

        public bool ConsumeFullRedraw()
        {
            var value = _fullRedrawNeeded;
            _fullRedrawNeeded = false;
            return value;
        }
    }
}
