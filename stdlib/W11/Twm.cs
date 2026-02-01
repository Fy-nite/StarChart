using System;
using System.Collections.Generic;

namespace StarChart.stdlib.W11
{
    // A tiny, example "TWM"-like window manager.
    // It "frames" existing client windows by creating a frame window and
    // copying the client's canvas into the frame's canvas while drawing a
    // simple titlebar. This is only an example to show how a WM could
    // manage windows with the minimal DisplayServer API.
    public class TwmManager : StarChart.Plugins.IStarChartWindowManager
    {
        readonly DisplayServer _server;
        readonly int _titlebarHeight;
        readonly bool _autoWrapMapped;

        // maps client XID -> frame window
        readonly Dictionary<uint, Window> _frames = new();
        readonly Dictionary<uint, Window> _clients = new();
        readonly Dictionary<uint, Window> _frameToClient = new();

        Window? _dragClient;
        Window? _dragFrame;
        int _dragOffsetX;
        int _dragOffsetY;
        bool _dragging;

        Window? _resizeClient;
        Window? _resizeFrame;
        bool _resizing;
        ResizeHandle _resizeHandle;
        int _resizeStartW;
        int _resizeStartH;
        int _resizeStartMouseX;
        int _resizeStartMouseY;
        int _resizeStartFrameX;
        int _resizeStartFrameY;
        const int ResizeGripSize = 8;

        enum ResizeHandle
        {
            None,
            BottomRight,
            BottomLeft,
            TopRight,
            TopLeft
        }

        public TwmManager(DisplayServer server, int titlebarHeight = 16, bool autoWrapMapped = true)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _titlebarHeight = Math.Max(8, titlebarHeight);
            _autoWrapMapped = autoWrapMapped;

            // Register this manager on the server so external code can detect it.
            _server.WindowManager = this;

            if (_autoWrapMapped)
            {
                _server.WindowMapped += OnWindowMapped;
            }
        }

        public int TitlebarHeight => _titlebarHeight;

        // Stop the window manager: unsubscribe and unwrap/cleanup managed windows.
        public void Stop()
        {
            if (_autoWrapMapped)
            {
                _server.WindowMapped -= OnWindowMapped;
            }

            // Unwrap all clients we currently manage
            var clients = new List<Window>(_clients.Values);
            foreach (var client in clients)
            {
                try { UnwrapWindow(client); } catch { }
            }

            // Clear registration
            if (_server.WindowManager == this) _server.WindowManager = null;
        }

        // IStarChartPlugin lifecycle (noop for this simple manager)
        public void Initialize(StarChart.Plugins.PluginContext context)
        {
            // No initialization required for this example manager
        }

        public void Start()
        {
            // No-op
        }

        public bool TryGetFrame(Window client, out Window frame)
        {
            frame = null!;
            if (client == null) return false;
            if (_frames.TryGetValue(client.XID, out var f))
            {
                frame = f;
                return true;
            }
            return false;
        }

        // Given a frame window, return the corresponding client window if any.
        public bool TryGetClientFromFrame(Window frame, out Window client)
        {
            client = null!;
            if (frame == null) return false;
            if (_frameToClient.TryGetValue(frame.XID, out var c))
            {
                client = c;
                return true;
            }
            return false;
        }

        // Wrap an existing client window in a frame window. The client will be
        // unmapped and its pixels will be composited into the frame by the manager.
        public bool WrapWindow(Window client)
        {
            if (client == null) return false;
            if (_clients.ContainsKey(client.XID)) return false; // already wrapped

            // Create frame geometry: same position, add titlebar to height
            var frameGeom = new WindowGeometry(client.Geometry.X, client.Geometry.Y, client.Geometry.Width, client.Geometry.Height + _titlebarHeight);
            var frameName = $"twm_frame_{client.XID}";
            var frameTitle = $"TWM: {client.Title}";
            var frame = _server.CreateWindow(frameName, frameTitle, frameGeom, WindowStyle.Titled, client.Scale);
            frame.Map();

            // hide the real client; manager will copy pixels into the frame
            client.Unmap();

            _frames[client.XID] = frame;
            _clients[client.XID] = client;
            _frameToClient[frame.XID] = client;

            return true;
        }

        // Unwrap a previously wrapped client: destroy the frame and remap the client.
        public bool UnwrapWindow(Window client)
        {
            if (client == null) return false;
            if (!_clients.ContainsKey(client.XID)) return false;

            var frame = _frames[client.XID];
            _server.DestroyWindow(frame);
            client.Map();

            _frames.Remove(client.XID);
            _clients.Remove(client.XID);
            _frameToClient.Remove(frame.XID);
            return true;
        }

        // Must be called once per frame to update frames' canvases by copying client pixels.
        public void Update()
        {
            foreach (var kv in _clients)
            {
                var client = kv.Value;
                if (client.IsDestroyed) continue;
                if (!_frames.TryGetValue(client.XID, out var frame)) continue;

                if ((!_dragging || _dragFrame != frame) && (!_resizing || _resizeFrame != frame))
                {
                    int desiredX = client.Geometry.X;
                    int desiredY = client.Geometry.Y - _titlebarHeight * frame.Scale;
                    if (frame.Geometry.X != desiredX || frame.Geometry.Y != desiredY)
                    {
                        _server.MoveWindow(frame, desiredX, desiredY);
                    }
                }

                // ensure frame canvas matches client size + titlebar
                int contentW = client.Canvas.Width;
                int contentH = client.Canvas.Height;
                int desiredFrameH = contentH + _titlebarHeight;
                if (frame.Canvas.Width != contentW || frame.Canvas.Height != desiredFrameH)
                {
                    frame.SetContentSize(contentW, desiredFrameH);
                }

                if (frame.Scale != client.Scale)
                {
                    frame.SetScale(client.Scale, preserveOnScreenSize: false);
                }

                // Draw titlebar: fill with dark gray (fast block write into byte buffer)
                uint titleColor = 0xFF444444u;
                var fBuf = frame.Canvas.PixelBuffer;
                int fW = frame.Canvas.Width;
                byte ta = (byte)((titleColor >> 24) & 0xFF);
                byte tr = (byte)((titleColor >> 16) & 0xFF);
                byte tg = (byte)((titleColor >> 8) & 0xFF);
                byte tb = (byte)(titleColor & 0xFF);
                for (int y = 0; y < _titlebarHeight; y++)
                {
                    int row = (y * fW) * 4;
                    for (int x = 0; x < fW; x++)
                    {
                        int idx = row + x * 4;
                        fBuf[idx + 0] = ta;
                        fBuf[idx + 1] = tr;
                        fBuf[idx + 2] = tg;
                        fBuf[idx + 3] = tb;
                    }
                }
                frame.Canvas.MarkDirtyRect(0, 0, fW, _titlebarHeight);

                // Draw a simple close box at the right side of the titlebar
                int boxSize = Math.Min(12, _titlebarHeight - 4);
                int boxX = frame.Canvas.Width - boxSize - 4;
                int boxY = (_titlebarHeight - boxSize) / 2;
                uint boxColor = 0xFFAA3333u;
                // Draw close box (fast write)
                byte ba = (byte)((boxColor >> 24) & 0xFF);
                byte br = (byte)((boxColor >> 16) & 0xFF);
                byte bg = (byte)((boxColor >> 8) & 0xFF);
                byte bb = (byte)(boxColor & 0xFF);
                for (int y = 0; y < boxSize; y++)
                {
                    int row = ((boxY + y) * fW) * 4;
                    for (int x = 0; x < boxSize; x++)
                    {
                        int idx = row + (boxX + x) * 4;
                        fBuf[idx + 0] = ba;
                        fBuf[idx + 1] = br;
                        fBuf[idx + 2] = bg;
                        fBuf[idx + 3] = bb;
                    }
                }
                frame.Canvas.MarkDirtyRect(boxX, boxY, boxSize, boxSize);

                // Draw a resize grip in the bottom-right corner
                int gripX = frame.Canvas.Width - ResizeGripSize;
                int gripY = frame.Canvas.Height - ResizeGripSize;
                uint gripColor = 0xFF777777u;
                // Draw resize grip (fast write)
                byte ga = (byte)((gripColor >> 24) & 0xFF);
                byte gr = (byte)((gripColor >> 16) & 0xFF);
                byte gg = (byte)((gripColor >> 8) & 0xFF);
                byte gb = (byte)(gripColor & 0xFF);
                for (int y = 0; y < ResizeGripSize; y++)
                {
                    int row = ((gripY + y) * fW) * 4;
                    for (int x = 0; x < ResizeGripSize; x++)
                    {
                        if (x >= y)
                        {
                            int idx = row + (gripX + x) * 4;
                            fBuf[idx + 0] = ga;
                            fBuf[idx + 1] = gr;
                            fBuf[idx + 2] = gg;
                            fBuf[idx + 3] = gb;
                        }
                    }
                }
                frame.Canvas.MarkDirtyRect(gripX, gripY, ResizeGripSize, ResizeGripSize);

                // Copy client pixels into frame canvas below the titlebar (fast block copy per-row)
                var cBuf = client.Canvas.PixelBuffer;
                int cW = client.Canvas.Width;
                var dstBuf = frame.Canvas.PixelBuffer;
                int dstRowStart = _titlebarHeight * frame.Canvas.Width * 4;
                int copyBytesPerRow = contentW * 4;
                for (int y = 0; y < contentH; y++)
                {
                    int srcOffset = y * cW * 4;
                    int dstOffset = dstRowStart + y * frame.Canvas.Width * 4;
                    // copy contentW pixels from srcOffset to dstOffset
                    Buffer.BlockCopy(cBuf, srcOffset, dstBuf, dstOffset, copyBytesPerRow);
                }
                frame.Canvas.MarkDirtyRect(0, _titlebarHeight, contentW, contentH);
            }
        }

        public void HandleMouse(int x, int y, bool leftDown, bool leftPressed, bool leftReleased)
        {
            if (leftPressed)
            {
                var hit = HitTestFrame(x, y);
                if (hit.frame != null && hit.client != null)
                {
                    _server.BringToFront(hit.frame);
                    _server.FocusWindow(hit.client);

                    int localX = hit.localX;
                    int localY = hit.localY;

                    var handle = GetResizeHandle(hit.frame, localX, localY);
                    if (handle != ResizeHandle.None)
                    {
                        _resizing = true;
                        _resizeClient = hit.client;
                        _resizeFrame = hit.frame;
                        _resizeHandle = handle;
                        _resizeStartW = hit.client.Canvas.Width;
                        _resizeStartH = hit.client.Canvas.Height;
                        _resizeStartMouseX = x;
                        _resizeStartMouseY = y;
                        _resizeStartFrameX = hit.frame.Geometry.X;
                        _resizeStartFrameY = hit.frame.Geometry.Y;
                        return;
                    }

                    if (IsInCloseBox(hit.frame, localX, localY))
                    {
                        _server.DestroyWindow(hit.client);
                        _server.DestroyWindow(hit.frame);
                        _frameToClient.Remove(hit.frame.XID);
                        _clients.Remove(hit.client.XID);
                        _frames.Remove(hit.client.XID);
                        return;
                    }

                    if (localY < _titlebarHeight)
                    {
                        _dragging = true;
                        _dragClient = hit.client;
                        _dragFrame = hit.frame;
                        _dragOffsetX = x - hit.frame.Geometry.X;
                        _dragOffsetY = y - hit.frame.Geometry.Y;
                    }
                }
            }

            if (leftDown && _dragging && _dragFrame != null && _dragClient != null)
            {
                int newX = x - _dragOffsetX;
                int newY = y - _dragOffsetY;
                _server.MoveWindow(_dragFrame, newX, newY);

                int clientY = newY + _titlebarHeight * _dragFrame.Scale;
                _server.MoveWindow(_dragClient, newX, clientY);
            }

            if (leftDown && _resizing && _resizeClient != null && _resizeFrame != null)
            {
                int scale = Math.Max(1, _resizeFrame.Scale);
                int dx = (x - _resizeStartMouseX) / scale;
                int dy = (y - _resizeStartMouseY) / scale;
                int newW = _resizeStartW;
                int newH = _resizeStartH;
                int newFrameX = _resizeStartFrameX;
                int newFrameY = _resizeStartFrameY;

                switch (_resizeHandle)
                {
                    case ResizeHandle.BottomRight:
                        newW = _resizeStartW + dx;
                        newH = _resizeStartH + dy;
                        break;
                    case ResizeHandle.BottomLeft:
                        newW = _resizeStartW - dx;
                        newH = _resizeStartH + dy;
                        newFrameX = _resizeStartFrameX + dx * scale;
                        break;
                    case ResizeHandle.TopRight:
                        newW = _resizeStartW + dx;
                        newH = _resizeStartH - dy;
                        newFrameY = _resizeStartFrameY + dy * scale;
                        break;
                    case ResizeHandle.TopLeft:
                        newW = _resizeStartW - dx;
                        newH = _resizeStartH - dy;
                        newFrameX = _resizeStartFrameX + dx * scale;
                        newFrameY = _resizeStartFrameY + dy * scale;
                        break;
                }

                newW = Math.Max(10, newW);
                newH = Math.Max(6, newH);
                _resizeClient.SetContentSize(newW, newH);
                _resizeFrame.SetContentSize(newW, newH + _titlebarHeight);

                _server.MoveWindow(_resizeFrame, newFrameX, newFrameY);
                int clientX = newFrameX;
                int clientY = newFrameY + _titlebarHeight * scale;
                _server.MoveWindow(_resizeClient, clientX, clientY);
            }

            if (leftReleased)
            {
                _dragging = false;
                _dragClient = null;
                _dragFrame = null;

                _resizing = false;
                _resizeHandle = ResizeHandle.None;
                if (_resizeClient != null && _resizeFrame != null)
                {
                    int scale = Math.Max(1, _resizeFrame.Scale);
                    int clientX = _resizeFrame.Geometry.X;
                    int clientY = _resizeFrame.Geometry.Y + _titlebarHeight * scale;
                    _server.MoveWindow(_resizeClient, clientX, clientY);
                }
                _resizeClient = null;
                _resizeFrame = null;
            }
        }

        void OnWindowMapped(Window w)
        {
            // If a window is mapped by a client, auto-wrap it unless it's already wrapped
            if (w == null) return;
            if (_clients.ContainsKey(w.XID)) return;
            // Don't wrap TWM-created frames (they have titles starting with "TWM:")
            if (w.Name != null && w.Name.StartsWith("twm_frame_")) return;
            WrapWindow(w);
        }

        (Window? frame, Window? client, int localX, int localY) HitTestFrame(int x, int y)
        {
            for (int i = _server.Windows.Count - 1; i >= 0; i--)
            {
                var w = _server.Windows[i];
                if (!w.IsMapped || w.IsDestroyed) continue;
                if (!_frameToClient.TryGetValue(w.XID, out var client)) continue;

                int scale = Math.Max(1, w.Scale);
                int onscreenW = w.Geometry.Width * scale;
                int onscreenH = w.Geometry.Height * scale;
                if (x < w.Geometry.X || y < w.Geometry.Y) continue;
                if (x >= w.Geometry.X + onscreenW || y >= w.Geometry.Y + onscreenH) continue;

                int localX = (x - w.Geometry.X) / scale;
                int localY = (y - w.Geometry.Y) / scale;
                return (w, client, localX, localY);
            }
            return (null, null, 0, 0);
        }

        bool IsInCloseBox(Window frame, int localX, int localY)
        {
            int boxSize = Math.Min(12, _titlebarHeight - 4);
            int boxX = frame.Canvas.Width - boxSize - 4;
            int boxY = (_titlebarHeight - boxSize) / 2;
            return localX >= boxX && localX < boxX + boxSize && localY >= boxY && localY < boxY + boxSize;
        }

        ResizeHandle GetResizeHandle(Window frame, int localX, int localY)
        {
            int w = frame.Canvas.Width;
            int h = frame.Canvas.Height;
            bool left = localX <= ResizeGripSize;
            bool right = localX >= w - ResizeGripSize;
            bool top = localY <= ResizeGripSize;
            bool bottom = localY >= h - ResizeGripSize;

            if (right && bottom) return ResizeHandle.BottomRight;
            if (left && bottom) return ResizeHandle.BottomLeft;
            if (right && top) return ResizeHandle.TopRight;
            if (left && top) return ResizeHandle.TopLeft;
            return ResizeHandle.None;
        }
    }
}
