using System;
using System.Collections.Generic;
using StarChart.Plugins;

namespace StarChart.stdlib.W11
{
    public class XTerm : IStarChartApp, IScheduledTask
    {
        // IStarChartApp implementation
        public Window? MainWindow => Window;
        public bool IsComplete => Window.IsDestroyed;

        public void Initialize(PluginContext context) { }
        public void Start() { }
        public void Stop() { _server.DestroyWindow(Window); }

        public void Execute(double deltaTime) => Update(deltaTime);

        public enum FontKind
        {
            Small4x6,
            Classic5x7,
            Clean8x8
        }

        const int SmallGlyphWidth = 4;
        const int SmallGlyphHeight = 6;
        const int ClassicGlyphWidth = 5;
        const int ClassicGlyphHeight = 7;

        readonly DisplayServer _server;
        readonly int _cols;
        readonly int _rows;
        readonly char[,] _buffer;
        readonly int _glyphWidth;
        readonly int _glyphHeight;
        readonly int _cellWidth;
        readonly int _cellHeight;
        readonly Dictionary<char, byte[]> _font;

        int _cursorX;
        int _cursorY;
        bool _dirty = true;
        bool _cursorVisible = true;
        double _blinkTimer;
        bool _selecting;
        bool _hasSelection;
        int _selStartX;
        int _selStartY;
        int _selEndX;
        int _selEndY;

        public Window Window { get; }
        public uint Foreground { get; set; } = 0xFF000000u;
        public uint Background { get; set; } = 0xFFFFFFFFu;
        public uint SelectionBackground { get; set; } = 0xFFBBD7FFu;
        public uint CursorBackground { get; set; } = 0xFF000000u;
        public event Action<string>? OnEnter;

        public XTerm(DisplayServer server, string name, string title, int cols, int rows, int x, int y, int scale = 1, FontKind fontKind = FontKind.Small4x6)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _cols = Math.Max(1, cols);
            _rows = Math.Max(1, rows);
            _buffer = new char[_rows, _cols];

            if (fontKind == FontKind.Small4x6)
            {
                _glyphWidth = SmallGlyphWidth;
                _glyphHeight = SmallGlyphHeight;
                _cellWidth = SmallGlyphWidth;
                _cellHeight = SmallGlyphHeight;
                _font = SmallFont;
            }
            else if (fontKind == FontKind.Classic5x7)
            {
                _glyphWidth = ClassicGlyphWidth;
                _glyphHeight = ClassicGlyphHeight;
                _cellWidth = ClassicGlyphWidth;
                _cellHeight = ClassicGlyphHeight;
                _font = ClassicFont;
            }
            else
            {
                // new clean 8x8 bitmap font
                _glyphWidth = 8;
                _glyphHeight = 8;
                _cellWidth = 8;
                _cellHeight = 8;
                _font = Clean8x8;
            }

            var width = _cols * _cellWidth;
            var height = _rows * _cellHeight;
            Window = _server.CreateWindow(name, title, new WindowGeometry(x, y, width, height), WindowStyle.Titled, scale);
            Window.Map();

            Clear();
        }

        public void Clear()
        {
            for (int y = 0; y < _rows; y++)
                for (int x = 0; x < _cols; x++)
                    _buffer[y, x] = ' ';
            _cursorX = 0;
            _cursorY = 0;
            _dirty = true;
        }

        public void WriteLine(string text)
        {
            Write(text);
            NewLine();
        }

        public void SetLines(IEnumerable<string> lines)
        {
            Clear();
            if (lines == null) return;
            foreach (var line in lines)
            {
                WriteLine(line ?? string.Empty);
            }
            Render();
        }

        public void Write(string text)
        {
            if (text == null) return;
            foreach (var ch in text)
            {
                if (ch == '\n')
                {
                    NewLine();
                    continue;
                }
                if (ch == '\r')
                {
                    _cursorX = 0;
                    continue;
                }

                PutChar(ch);
            }
            _dirty = true;
        }

        public void Update(double deltaTime)
        {
            _blinkTimer += deltaTime;
            if (_blinkTimer >= 0.5)
            {
                _blinkTimer = 0;
                _cursorVisible = !_cursorVisible;
                _dirty = true;
            }

            if (_dirty) Render();
        }

        void PutChar(char ch)
        {
            if (_cursorX >= _cols) NewLine();
            if (_cursorY >= _rows) Scroll();

            _buffer[_cursorY, _cursorX] = ch;
            _cursorX++;
            if (_cursorX >= _cols) NewLine();
            _dirty = true;
        }

        void NewLine()
        {
            _cursorX = 0;
            _cursorY++;
            if (_cursorY >= _rows) Scroll();
        }

        void Scroll()
        {
            for (int y = 1; y < _rows; y++)
                for (int x = 0; x < _cols; x++)
                    _buffer[y - 1, x] = _buffer[y, x];

            for (int x = 0; x < _cols; x++)
                _buffer[_rows - 1, x] = ' ';

            _cursorY = _rows - 1;
        }

        public void Render()
        {
            var canvas = Window.Canvas;
            canvas.Clear(Background);

            for (int row = 0; row < _rows; row++)
            {
                for (int col = 0; col < _cols; col++)
                {
                    var ch = _buffer[row, col];
                    if (IsSelected(col, row))
                    {
                        FillRect(canvas, col * _cellWidth, row * _cellHeight, _cellWidth, _cellHeight, SelectionBackground);
                    }
                    if (_cursorVisible && col == _cursorX && row == _cursorY)
                    {
                        FillRect(canvas, col * _cellWidth, row * _cellHeight, _cellWidth, _cellHeight, CursorBackground);
                    }
                    DrawChar(canvas, col * _cellWidth, row * _cellHeight, ch, Foreground);
                }
            }

            _dirty = false;
        }

        public void HandleKey(Microsoft.Xna.Framework.Input.Keys key, bool shift)
        {
            if (key == Microsoft.Xna.Framework.Input.Keys.Back)
            {
                Backspace();
                _dirty = true;
                return;
            }
            if (key == Microsoft.Xna.Framework.Input.Keys.Left)
            {
                MoveCursor(-1, 0);
                _dirty = true;
                return;
            }
            if (key == Microsoft.Xna.Framework.Input.Keys.Right)
            {
                MoveCursor(1, 0);
                _dirty = true;
                return;
            }
            if (key == Microsoft.Xna.Framework.Input.Keys.Up)
            {
                MoveCursor(0, -1);
                _dirty = true;
                return;
            }
            if (key == Microsoft.Xna.Framework.Input.Keys.Down)
            {
                MoveCursor(0, 1);
                _dirty = true;
                return;
            }
            if (key == Microsoft.Xna.Framework.Input.Keys.Enter)
            {
                // Notify shell or listeners with the current line, then advance to a new line
                OnEnter?.Invoke(GetCurrentLine());
                NewLine();
                _dirty = true;
                return;
            }
            if (key == Microsoft.Xna.Framework.Input.Keys.Tab)
            {
                Write("    ");
                return;
            }
            if (TryMapKey(key, shift, out var ch))
            {
                PutChar(ch);
                _dirty = true;
            }
        }

        // Return the current line under the cursor (used by shell)
        private string GetCurrentLine()
        {
            var sb = new System.Text.StringBuilder();
            if (_cursorY < 0 || _cursorY >= _rows) return string.Empty;
            for (int x = 0; x < _cols; x++)
            {
                var ch = _buffer[_cursorY, x];
                if (ch == '\0') break;
                sb.Append(ch);
            }
            return sb.ToString().TrimEnd();
        }

        void MoveCursor(int dx, int dy)
        {
            int nx = _cursorX + dx;
            int ny = _cursorY + dy;

            if (nx < 0)
            {
                if (ny > 0)
                {
                    ny--;
                    nx = _cols - 1;
                }
                else
                {
                    nx = 0;
                }
            }
            if (nx >= _cols)
            {
                nx = 0;
                ny++;
            }

            if (ny < 0) ny = 0;
            if (ny >= _rows) ny = _rows - 1;

            _cursorX = nx;
            _cursorY = ny;
        }

        public void HandleMousePixel(int px, int py, bool leftDown, bool leftPressed, bool leftReleased)
        {
            if (px < 0 || py < 0) return;
            int col = px / _cellWidth;
            int row = py / _cellHeight;
            if (col < 0 || row < 0 || col >= _cols || row >= _rows) return;

            if (leftPressed)
            {
                _selecting = true;
                _hasSelection = true;
                _selStartX = col;
                _selStartY = row;
                _selEndX = col;
                _selEndY = row;
                _dirty = true;
            }

            if (leftDown && _selecting)
            {
                _selEndX = col;
                _selEndY = row;
                _dirty = true;
            }

            if (leftReleased)
            {
                _selecting = false;
            }
        }

        void Backspace()
        {
            if (_cursorX > 0)
            {
                _cursorX--;
                _buffer[_cursorY, _cursorX] = ' ';
                return;
            }

            if (_cursorY > 0)
            {
                _cursorY--;
                _cursorX = _cols - 1;
                _buffer[_cursorY, _cursorX] = ' ';
            }
        }

        bool IsSelected(int col, int row)
        {
            if (!_hasSelection) return false;
            int start = _selStartY * _cols + _selStartX;
            int end = _selEndY * _cols + _selEndX;
            if (start > end) (start, end) = (end, start);
            int idx = row * _cols + col;
            return idx >= start && idx <= end;
        }

        static void FillRect(Canvas canvas, int x, int y, int w, int h, uint color)
        {
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++)
                    canvas.SetPixel(x + xx, y + yy, color);
        }

        static bool TryMapKey(Microsoft.Xna.Framework.Input.Keys key, bool shift, out char ch)
        {
            ch = '\0';

            if (key >= Microsoft.Xna.Framework.Input.Keys.A && key <= Microsoft.Xna.Framework.Input.Keys.Z)
            {
                ch = (char)('A' + (key - Microsoft.Xna.Framework.Input.Keys.A));
                if (!shift) ch = char.ToLowerInvariant(ch);
                return true;
            }

            if (key >= Microsoft.Xna.Framework.Input.Keys.D0 && key <= Microsoft.Xna.Framework.Input.Keys.D9)
            {
                int d = key - Microsoft.Xna.Framework.Input.Keys.D0;
                string normal = "0123456789";
                string shifted = ")!@#$%^&*(";
                ch = shift ? shifted[d] : normal[d];
                return true;
            }

            switch (key)
            {
                case Microsoft.Xna.Framework.Input.Keys.Space: ch = ' '; return true;
                case Microsoft.Xna.Framework.Input.Keys.OemPeriod: ch = shift ? '>' : '.'; return true;
                case Microsoft.Xna.Framework.Input.Keys.OemComma: ch = shift ? '<' : ','; return true;
                case Microsoft.Xna.Framework.Input.Keys.OemMinus: ch = shift ? '_' : '-'; return true;
                case Microsoft.Xna.Framework.Input.Keys.OemPlus: ch = shift ? '+' : '='; return true;
                case Microsoft.Xna.Framework.Input.Keys.OemQuestion: ch = shift ? '?' : '/'; return true;
                case Microsoft.Xna.Framework.Input.Keys.OemSemicolon: ch = shift ? ':' : ';'; return true;
                case Microsoft.Xna.Framework.Input.Keys.OemQuotes: ch = shift ? '"' : '\''; return true;
                case Microsoft.Xna.Framework.Input.Keys.OemOpenBrackets: ch = shift ? '{' : '['; return true;
                case Microsoft.Xna.Framework.Input.Keys.OemCloseBrackets: ch = shift ? '}' : ']'; return true;
                case Microsoft.Xna.Framework.Input.Keys.OemPipe: ch = shift ? '|' : '\\'; return true;
                case Microsoft.Xna.Framework.Input.Keys.OemTilde: ch = shift ? '~' : '`'; return true;
            }

            return false;
        }

        void DrawChar(Canvas canvas, int x, int y, char ch, uint fg)
        {
            var glyph = GetGlyph(ch);
            for (int row = 0; row < _glyphHeight; row++)
            {
                byte bits = glyph[row];
                for (int col = 0; col < _glyphWidth; col++)
                {
                    int mask = 1 << (_glyphWidth - 1 - col);
                    if ((bits & mask) != 0)
                    {
                        canvas.SetPixel(x + col, y + row, fg);
                    }
                }
            }
        }

        byte[] GetGlyph(char c)
        {
            char up = char.ToUpperInvariant(c);
            if (_font.TryGetValue(up, out var glyph)) return glyph;
            return _font['?'];
        }

        // Minimal clean 8x8 font: only ASCII letters, digits and a few symbols for now.
        static readonly Dictionary<char, byte[]> Clean8x8 = new()
        {
            [' '] = new byte[] {0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00},
            ['?'] = new byte[] {0x3C,0x42,0x02,0x04,0x08,0x00,0x08,0x00},
            ['.'] = new byte[] {0x00,0x00,0x00,0x00,0x00,0x18,0x18,0x00},
            [','] = new byte[] {0x00,0x00,0x00,0x00,0x00,0x18,0x10,0x00},
            ['0'] = new byte[] {0x3C,0x42,0x46,0x4A,0x52,0x62,0x42,0x3C},
            ['1'] = new byte[] {0x08,0x18,0x28,0x08,0x08,0x08,0x08,0x3E},
            ['2'] = new byte[] {0x3C,0x42,0x02,0x1C,0x20,0x40,0x42,0x7E},
            ['3'] = new byte[] {0x3C,0x42,0x02,0x1C,0x02,0x02,0x42,0x3C},
            ['4'] = new byte[] {0x04,0x0C,0x14,0x24,0x44,0x7E,0x04,0x04},
            ['5'] = new byte[] {0x7E,0x40,0x40,0x7C,0x02,0x02,0x42,0x3C},
            ['6'] = new byte[] {0x1C,0x20,0x40,0x7C,0x42,0x42,0x42,0x3C},
            ['7'] = new byte[] {0x7E,0x42,0x04,0x08,0x10,0x10,0x10,0x10},
            ['8'] = new byte[] {0x3C,0x42,0x42,0x3C,0x42,0x42,0x42,0x3C},
            ['9'] = new byte[] {0x3C,0x42,0x42,0x42,0x3E,0x02,0x04,0x38},
            ['A'] = new byte[] {0x18,0x24,0x24,0x24,0x3C,0x24,0x24,0x24},
            ['B'] = new byte[] {0x78,0x24,0x24,0x38,0x24,0x24,0x24,0x78},
            ['C'] = new byte[] {0x3C,0x42,0x40,0x40,0x40,0x40,0x42,0x3C},
            ['D'] = new byte[] {0x78,0x24,0x22,0x22,0x22,0x22,0x24,0x78},
            ['E'] = new byte[] {0x7E,0x40,0x40,0x7C,0x40,0x40,0x40,0x7E},
            ['F'] = new byte[] {0x7E,0x40,0x40,0x7C,0x40,0x40,0x40,0x40},
            ['G'] = new byte[] {0x3C,0x42,0x40,0x4E,0x42,0x42,0x42,0x3C},
            ['H'] = new byte[] {0x42,0x42,0x42,0x7E,0x42,0x42,0x42,0x42},
            ['I'] = new byte[] {0x3C,0x10,0x10,0x10,0x10,0x10,0x10,0x3C},
            ['J'] = new byte[] {0x1E,0x08,0x08,0x08,0x08,0x08,0x48,0x30},
            ['K'] = new byte[] {0x42,0x44,0x48,0x70,0x48,0x44,0x42,0x42},
            ['L'] = new byte[] {0x40,0x40,0x40,0x40,0x40,0x40,0x40,0x7E},
            ['M'] = new byte[] {0x42,0x66,0x5A,0x5A,0x42,0x42,0x42,0x42},
            ['N'] = new byte[] {0x42,0x62,0x52,0x4A,0x46,0x42,0x42,0x42},
            ['O'] = new byte[] {0x3C,0x42,0x42,0x42,0x42,0x42,0x42,0x3C},
            ['P'] = new byte[] {0x3C,0x42,0x42,0x3C,0x40,0x40,0x40,0x40},
            ['Q'] = new byte[] {0x3C,0x42,0x42,0x42,0x4A,0x44,0x3A,0x00},
            ['R'] = new byte[] {0x3C,0x42,0x42,0x3C,0x48,0x44,0x42,0x42},
            ['S'] = new byte[] {0x3C,0x42,0x40,0x3C,0x02,0x02,0x42,0x3C},
            ['T'] = new byte[] {0x7E,0x10,0x10,0x10,0x10,0x10,0x10,0x10},
            ['U'] = new byte[] {0x42,0x42,0x42,0x42,0x42,0x42,0x42,0x3C},
            ['V'] = new byte[] {0x42,0x42,0x42,0x42,0x42,0x42,0x24,0x18},
            ['W'] = new byte[] {0x42,0x42,0x42,0x5A,0x5A,0x5A,0x66,0x42},
            ['X'] = new byte[] {0x42,0x24,0x18,0x18,0x18,0x24,0x42,0x42},
            ['Y'] = new byte[] {0x42,0x42,0x24,0x18,0x18,0x10,0x10,0x10},
            ['Z'] = new byte[] {0x7E,0x02,0x04,0x08,0x10,0x20,0x40,0x7E}
        };

        static readonly Dictionary<char, byte[]> ClassicFont = new()
        {
            [' '] = new byte[] { 0x00,0x00,0x00,0x00,0x00,0x00,0x00 },
            ['?'] = new byte[] { 0x0E,0x11,0x01,0x02,0x04,0x00,0x04 },
            ['.'] = new byte[] { 0x00,0x00,0x00,0x00,0x00,0x0C,0x0C },
            [','] = new byte[] { 0x00,0x00,0x00,0x00,0x00,0x0C,0x08 },
            [':'] = new byte[] { 0x00,0x0C,0x0C,0x00,0x0C,0x0C,0x00 },
            [';'] = new byte[] { 0x00,0x0C,0x0C,0x00,0x0C,0x08,0x00 },
            ['-'] = new byte[] { 0x00,0x00,0x00,0x1F,0x00,0x00,0x00 },
            ['_'] = new byte[] { 0x00,0x00,0x00,0x00,0x00,0x00,0x1F },
            ['+'] = new byte[] { 0x00,0x04,0x04,0x1F,0x04,0x04,0x00 },
            ['='] = new byte[] { 0x00,0x00,0x1F,0x00,0x1F,0x00,0x00 },
            ['!'] = new byte[] { 0x04,0x04,0x04,0x04,0x04,0x00,0x04 },
            ['('] = new byte[] { 0x02,0x04,0x08,0x08,0x08,0x04,0x02 },
            [')'] = new byte[] { 0x08,0x04,0x02,0x02,0x02,0x04,0x08 },
            ['['] = new byte[] { 0x0E,0x08,0x08,0x08,0x08,0x08,0x0E },
            [']'] = new byte[] { 0x0E,0x02,0x02,0x02,0x02,0x02,0x0E },
            ['/'] = new byte[] { 0x01,0x02,0x04,0x08,0x10,0x00,0x00 },
            ['\\'] = new byte[] { 0x10,0x08,0x04,0x02,0x01,0x00,0x00 },
            ['*'] = new byte[] { 0x00,0x0A,0x04,0x1F,0x04,0x0A,0x00 },
            ['#'] = new byte[] { 0x0A,0x0A,0x1F,0x0A,0x1F,0x0A,0x0A },
            ['$'] = new byte[] { 0x04,0x0F,0x14,0x0E,0x05,0x1E,0x04 },
            ['%'] = new byte[] { 0x19,0x1A,0x04,0x08,0x16,0x06,0x00 },
            ['&'] = new byte[] { 0x06,0x09,0x0A,0x04,0x0A,0x11,0x0E },
            ['@'] = new byte[] { 0x0E,0x11,0x1D,0x15,0x1D,0x10,0x0E },
            ['<'] = new byte[] { 0x02,0x04,0x08,0x10,0x08,0x04,0x02 },
            ['>'] = new byte[] { 0x08,0x04,0x02,0x01,0x02,0x04,0x08 },
            ['0'] = new byte[] { 0x0E,0x11,0x13,0x15,0x19,0x11,0x0E },
            ['1'] = new byte[] { 0x04,0x0C,0x04,0x04,0x04,0x04,0x0E },
            ['2'] = new byte[] { 0x0E,0x11,0x01,0x02,0x04,0x08,0x1F },
            ['3'] = new byte[] { 0x1F,0x02,0x04,0x02,0x01,0x11,0x0E },
            ['4'] = new byte[] { 0x02,0x06,0x0A,0x12,0x1F,0x02,0x02 },
            ['5'] = new byte[] { 0x1F,0x10,0x1E,0x01,0x01,0x11,0x0E },
            ['6'] = new byte[] { 0x06,0x08,0x10,0x1E,0x11,0x11,0x0E },
            ['7'] = new byte[] { 0x1F,0x01,0x02,0x04,0x08,0x08,0x08 },
            ['8'] = new byte[] { 0x0E,0x11,0x11,0x0E,0x11,0x11,0x0E },
            ['9'] = new byte[] { 0x0E,0x11,0x11,0x0F,0x01,0x02,0x0C },
            ['A'] = new byte[] { 0x0E,0x11,0x11,0x1F,0x11,0x11,0x11 },
            ['B'] = new byte[] { 0x1E,0x11,0x11,0x1E,0x11,0x11,0x1E },
            ['C'] = new byte[] { 0x0E,0x11,0x10,0x10,0x10,0x11,0x0E },
            ['D'] = new byte[] { 0x1C,0x12,0x11,0x11,0x11,0x12,0x1C },
            ['E'] = new byte[] { 0x1F,0x10,0x10,0x1E,0x10,0x10,0x1F },
            ['F'] = new byte[] { 0x1F,0x10,0x10,0x1E,0x10,0x10,0x10 },
            ['G'] = new byte[] { 0x0E,0x11,0x10,0x17,0x11,0x11,0x0E },
            ['H'] = new byte[] { 0x11,0x11,0x11,0x1F,0x11,0x11,0x11 },
            ['I'] = new byte[] { 0x0E,0x04,0x04,0x04,0x04,0x04,0x0E },
            ['J'] = new byte[] { 0x01,0x01,0x01,0x01,0x11,0x11,0x0E },
            ['K'] = new byte[] { 0x11,0x12,0x14,0x18,0x14,0x12,0x11 },
            ['L'] = new byte[] { 0x10,0x10,0x10,0x10,0x10,0x10,0x1F },
            ['M'] = new byte[] { 0x11,0x1B,0x15,0x15,0x11,0x11,0x11 },
            ['N'] = new byte[] { 0x11,0x19,0x15,0x13,0x11,0x11,0x11 },
            ['O'] = new byte[] { 0x0E,0x11,0x11,0x11,0x11,0x11,0x0E },
            ['P'] = new byte[] { 0x1E,0x11,0x11,0x1E,0x10,0x10,0x10 },
            ['Q'] = new byte[] { 0x0E,0x11,0x11,0x11,0x15,0x12,0x0D },
            ['R'] = new byte[] { 0x1E,0x11,0x11,0x1E,0x14,0x12,0x11 },
            ['S'] = new byte[] { 0x0F,0x10,0x10,0x0E,0x01,0x01,0x1E },
            ['T'] = new byte[] { 0x1F,0x04,0x04,0x04,0x04,0x04,0x04 },
            ['U'] = new byte[] { 0x11,0x11,0x11,0x11,0x11,0x11,0x0E },
            ['V'] = new byte[] { 0x11,0x11,0x11,0x11,0x11,0x0A,0x04 },
            ['W'] = new byte[] { 0x11,0x11,0x11,0x15,0x15,0x15,0x0A },
            ['X'] = new byte[] { 0x11,0x11,0x0A,0x04,0x0A,0x11,0x11 },
            ['Y'] = new byte[] { 0x11,0x11,0x0A,0x04,0x04,0x04,0x04 },
            ['Z'] = new byte[] { 0x1F,0x01,0x02,0x04,0x08,0x10,0x1F }
        };

        static readonly Dictionary<char, byte[]> SmallFont = new()
        {
            [' '] = new byte[] { 0x00,0x00,0x00,0x00,0x00,0x00 },
            ['?'] = new byte[] { 0x0E,0x01,0x02,0x04,0x00,0x04 },
            ['.'] = new byte[] { 0x00,0x00,0x00,0x00,0x0C,0x0C },
            [','] = new byte[] { 0x00,0x00,0x00,0x00,0x0C,0x08 },
            ['-'] = new byte[] { 0x00,0x00,0x0F,0x00,0x00,0x00 },
            ['_'] = new byte[] { 0x00,0x00,0x00,0x00,0x00,0x0F },
            ['+'] = new byte[] { 0x00,0x04,0x0E,0x04,0x00,0x00 },
            ['='] = new byte[] { 0x00,0x0F,0x00,0x0F,0x00,0x00 },
            ['!'] = new byte[] { 0x04,0x04,0x04,0x04,0x00,0x04 },
            ['('] = new byte[] { 0x02,0x04,0x04,0x04,0x04,0x02 },
            [')'] = new byte[] { 0x04,0x02,0x02,0x02,0x02,0x04 },
            ['['] = new byte[] { 0x06,0x04,0x04,0x04,0x04,0x06 },
            [']'] = new byte[] { 0x06,0x02,0x02,0x02,0x02,0x06 },
            ['/'] = new byte[] { 0x01,0x02,0x04,0x08,0x00,0x00 },
            ['\\'] = new byte[] { 0x08,0x04,0x02,0x01,0x00,0x00 },
            ['0'] = new byte[] { 0x0E,0x09,0x09,0x09,0x09,0x0E },
            ['1'] = new byte[] { 0x04,0x0C,0x04,0x04,0x04,0x0E },
            ['2'] = new byte[] { 0x0E,0x01,0x06,0x08,0x08,0x0F },
            ['3'] = new byte[] { 0x0E,0x01,0x06,0x01,0x01,0x0E },
            ['4'] = new byte[] { 0x02,0x06,0x0A,0x0F,0x02,0x02 },
            ['5'] = new byte[] { 0x0F,0x08,0x0E,0x01,0x01,0x0E },
            ['6'] = new byte[] { 0x06,0x08,0x0E,0x09,0x09,0x0E },
            ['7'] = new byte[] { 0x0F,0x01,0x02,0x04,0x04,0x04 },
            ['8'] = new byte[] { 0x0E,0x09,0x0E,0x09,0x09,0x0E },
            ['9'] = new byte[] { 0x0E,0x09,0x09,0x0F,0x01,0x06 },
            ['A'] = new byte[] { 0x06,0x09,0x09,0x0F,0x09,0x09 },
            ['B'] = new byte[] { 0x0E,0x09,0x0E,0x09,0x09,0x0E },
            ['C'] = new byte[] { 0x06,0x09,0x08,0x08,0x09,0x06 },
            ['D'] = new byte[] { 0x0E,0x09,0x09,0x09,0x09,0x0E },
            ['E'] = new byte[] { 0x0F,0x08,0x0E,0x08,0x08,0x0F },
            ['F'] = new byte[] { 0x0F,0x08,0x0E,0x08,0x08,0x08 },
            ['G'] = new byte[] { 0x06,0x08,0x0B,0x09,0x09,0x06 },
            ['H'] = new byte[] { 0x09,0x09,0x0F,0x09,0x09,0x09 },
            ['I'] = new byte[] { 0x0E,0x04,0x04,0x04,0x04,0x0E },
            ['J'] = new byte[] { 0x01,0x01,0x01,0x01,0x09,0x06 },
            ['K'] = new byte[] { 0x09,0x0A,0x0C,0x0A,0x09,0x09 },
            ['L'] = new byte[] { 0x08,0x08,0x08,0x08,0x08,0x0F },
            ['M'] = new byte[] { 0x09,0x0F,0x0F,0x09,0x09,0x09 },
            ['N'] = new byte[] { 0x09,0x0D,0x0B,0x09,0x09,0x09 },
            ['O'] = new byte[] { 0x06,0x09,0x09,0x09,0x09,0x06 },
            ['P'] = new byte[] { 0x0E,0x09,0x0E,0x08,0x08,0x08 },
            ['Q'] = new byte[] { 0x06,0x09,0x09,0x09,0x0A,0x05 },
            ['R'] = new byte[] { 0x0E,0x09,0x0E,0x0A,0x09,0x09 },
            ['S'] = new byte[] { 0x07,0x08,0x06,0x01,0x01,0x0E },
            ['T'] = new byte[] { 0x0F,0x04,0x04,0x04,0x04,0x04 },
            ['U'] = new byte[] { 0x09,0x09,0x09,0x09,0x09,0x06 },
            ['V'] = new byte[] { 0x09,0x09,0x09,0x09,0x06,0x06 },
            ['W'] = new byte[] { 0x09,0x09,0x0F,0x0F,0x0F,0x06 },
            ['X'] = new byte[] { 0x09,0x06,0x06,0x06,0x06,0x09 },
            ['Y'] = new byte[] { 0x09,0x09,0x06,0x04,0x04,0x04 },
            ['Z'] = new byte[] { 0x0F,0x01,0x02,0x04,0x08,0x0F }
        };
    }
}
