using System;
using System.Text;

namespace cxhex
{
    public class HexEditor
    {
        private const int BytesPerRow = 16;
        private const int OffsetWidth = 10;                         
        private const int HexWidth = BytesPerRow * 3;               
        private const int AsciiColumn = OffsetWidth + HexWidth + 1;
        private const int RowWidth = AsciiColumn + BytesPerRow;      

        private static readonly string[] HexTable = BuildHexTable();

        private List<byte> data = new List<byte>();
        private string filePath;
        private string originalFilePath;
        private bool hasBeenSaved = false;
        private bool modified = false;
        private bool isRunning = true;

        private int cursor = 0;       
        private int nibble = 0;        
        private int topRow = 0;      
        private string statusMessage = "";

        private bool fullRedraw = true;        
        private bool needClear = true;        
        private int dirtyMin = int.MaxValue;   
        private int dirtyMax = -1;
        private int prevCursor = -1;     

        public HexEditor(string path)
        {
            filePath = path;
            originalFilePath = path;
            LoadFile();
        }

        private static string[] BuildHexTable()
        {
            const string digits = "0123456789ABCDEF";
            string[] table = new string[256];
            for (int i = 0; i < 256; i++)
            {
                table[i] = new string(new char[] { digits[i >> 4], digits[i & 0x0F], ' ' });
            }
            return table;
        }

        private byte[] ReadAllBytes(string path)
        {
            List<byte> result = new List<byte>();
            try
            {
                using FileStream stream = File.OpenRead(path);
                byte[] buffer = new byte[4096];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                        result.Add(buffer[i]);
                }
                stream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return result.ToArray();
        }

        private void LoadFile()
        {
            if (filePath == null) return;

            try
            {
                if (File.Exists(filePath))
                {
                    data = new List<byte>(ReadAllBytes(filePath));
                    hasBeenSaved = true;
                }
                else
                {
                    string directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    hasBeenSaved = false;
                }
            }
            catch
            {
                Console.WriteLine("Error when opening the file");
            }
        }


        public void Run()
        {
            while (isRunning)
            {
                Render();
                try
                {
                    var key = Console.ReadKey(true);
                    HandleKey(key);
                }
                catch
                {
                    Console.WriteLine();
                }
            }
        }


        private int VisibleRows()
        {
            int rows = Console.WindowHeight - 4;
            return rows < 1 ? 1 : rows;
        }

        private void EnsureCursorVisible()
        {
            int row = cursor / BytesPerRow;
            int visible = VisibleRows();

            if (row < topRow) topRow = row;
            if (row >= topRow + visible) topRow = row - visible + 1;
            if (topRow < 0) topRow = 0;
        }

        private void MarkDirty(int row)
        {
            if (row < dirtyMin) dirtyMin = row;
            if (row > dirtyMax) dirtyMax = row;
        }

        private void MarkDirtyFrom(int row)
        {
            MarkDirty(row);
            dirtyMax = int.MaxValue;
        }

        private bool IsRowDirty(int row)
        {
            return row >= dirtyMin && row <= dirtyMax;
        }

        private void Render()
        {
            int oldTop = topRow;
            EnsureCursorVisible();
            if (topRow != oldTop) fullRedraw = true;

            if (needClear)
            {
                Console.Clear();
                needClear = false;
                fullRedraw = true;
            }

            int visible = VisibleRows();

            if (fullRedraw)
            {
                DrawHeader();
                for (int r = 0; r < visible; r++)
                    DrawRow(topRow + r);
                DrawHelp();

                fullRedraw = false;
            }
            else if (dirtyMax >= 0)
            {
                int from = Math.Max(dirtyMin, topRow);
                int to = (int)Math.Min((long)dirtyMax, (long)topRow + visible - 1);
                for (int row = from; row <= to; row++)
                    DrawRow(row);
            }

            if (prevCursor != cursor && prevCursor >= 0 && !IsRowDirty(prevCursor / BytesPerRow))
                DrawCell(prevCursor);
            DrawCell(cursor);

            dirtyMin = int.MaxValue;
            dirtyMax = -1;
            prevCursor = cursor;

            DrawStatus();
            PlaceCursor();
        }

        private void DrawHeader()
        {
            Console.SetCursorPosition(0, 0);
            Console.ForegroundColor = ConsoleColor.DarkYellow;

            StringBuilder sb = new StringBuilder();
            sb.Append("Offset    ");
            for (int i = 0; i < BytesPerRow; i++)
                sb.Append(HexTable[i]);
            sb.Append(" ASCII");
            Console.Write(Pad(sb.ToString()));

            Console.ResetColor();
        }

        private void DrawRow(int row)
        {
            int screenRow = row - topRow + 1;
            if (screenRow < 1 || screenRow > VisibleRows()) return;

            int rowStart = row * BytesPerRow;

            Console.SetCursorPosition(0, screenRow);

            if (rowStart >= data.Count && rowStart != 0)
            {
                Console.Write(new string(' ', Console.WindowWidth - 1));
                return;
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(rowStart.ToString("X8") + "  ");
            Console.ResetColor();

            StringBuilder sb = new StringBuilder(RowWidth);
            for (int i = 0; i < BytesPerRow; i++)
            {
                int idx = rowStart + i;
                sb.Append(idx < data.Count ? HexTable[data[idx]] : "   ");
            }
            sb.Append(' ');
            for (int i = 0; i < BytesPerRow; i++)
            {
                int idx = rowStart + i;
                if (idx >= data.Count) break;
                sb.Append(ToAscii(data[idx]));
            }

            Console.Write(PadFrom(sb.ToString(), OffsetWidth));
        }

        private void DrawCell(int idx)
        {
            if (idx < 0 || idx >= data.Count) return;

            int row = idx / BytesPerRow - topRow;
            if (row < 0 || row >= VisibleRows()) return;

            int col = idx % BytesPerRow;
            byte b = data[idx];
            bool selected = idx == cursor;

            if (selected)
            {
                Console.BackgroundColor = ConsoleColor.DarkCyan;
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.SetCursorPosition(OffsetWidth + col * 3, row + 1);
            Console.Write(HexTable[b].Substring(0, 2));

            Console.SetCursorPosition(AsciiColumn + col, row + 1);
            Console.Write(ToAscii(b));

            if (selected) Console.ResetColor();
        }

        private void DrawHelp()
        {
            Console.SetCursorPosition(0, Console.WindowHeight - 3);
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write(Pad("[0-9A-F] Edit | [Ins] Insert | [Del] Delete | [Ctrl+D] Save | [Ctrl+X] Exit"));
            Console.ResetColor();
        }

        private void DrawStatus()
        {
            string info = data.Count == 0
                ? "Empty file (press Insert to add a byte)"
                : $"Offset: {cursor:X8}  Value: {data[cursor]:X2} ({data[cursor]})  Size: {data.Count} bytes";
            if (modified) info += "  [modified]";

            Console.SetCursorPosition(0, Console.WindowHeight - 2);
            Console.Write(Pad(info));

            Console.SetCursorPosition(0, Console.WindowHeight - 1);
            Console.Write(Pad(statusMessage));
            statusMessage = "";
        }

        private void PlaceCursor()
        {
            if (data.Count == 0)
            {
                Console.SetCursorPosition(OffsetWidth, 1);
                return;
            }

            int row = cursor / BytesPerRow - topRow;
            int col = cursor % BytesPerRow;
            Console.SetCursorPosition(OffsetWidth + col * 3 + nibble, row + 1);
        }

        private static char ToAscii(byte b)
        {
            return (b >= 32 && b < 127) ? (char)b : '.';
        }

        private static string Pad(string s)
        {
            int width = Console.WindowWidth - 1;
            if (s.Length >= width) return s.Substring(0, width);
            return s.PadRight(width);
        }

        private static string PadFrom(string s, int alreadyWritten)
        {
            int width = Console.WindowWidth - 1 - alreadyWritten;
            if (width < 0) width = 0;
            if (s.Length >= width) return s.Substring(0, width);
            return s.PadRight(width);
        }

        // ---------- Обработка клавиш ----------

        private void HandleKey(ConsoleKeyInfo key)
        {
            if (key.Modifiers == ConsoleModifiers.Control)
            {
                if (key.Key == ConsoleKey.D)
                {
                    SaveFile();
                    return;
                }
                if (key.Key == ConsoleKey.X)
                {
                    if (CheckExit())
                    {
                        Console.Clear();
                        isRunning = false;
                    }
                    else
                    {
                        needClear = true;
                    }
                    return;
                }
            }

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    MoveCursor(cursor - BytesPerRow);
                    break;

                case ConsoleKey.DownArrow:
                    MoveCursor(cursor + BytesPerRow);
                    break;

                case ConsoleKey.LeftArrow:
                    MoveCursor(cursor - 1);
                    break;

                case ConsoleKey.RightArrow:
                    MoveCursor(cursor + 1);
                    break;

                case ConsoleKey.PageUp:
                    MoveCursor(cursor - BytesPerRow * VisibleRows());
                    break;

                case ConsoleKey.PageDown:
                    MoveCursor(cursor + BytesPerRow * VisibleRows());
                    break;

                case ConsoleKey.Home:
                    MoveCursor(cursor - cursor % BytesPerRow);
                    break;

                case ConsoleKey.End:
                    MoveCursor(cursor - cursor % BytesPerRow + BytesPerRow - 1);
                    break;

                case ConsoleKey.Insert:
                    InsertByte();
                    break;

                case ConsoleKey.Delete:
                    DeleteByte();
                    break;

                case ConsoleKey.Backspace:
                    if (cursor > 0)
                    {
                        MoveCursor(cursor - 1);
                        DeleteByte();
                    }
                    break;

                default:
                    char c = key.KeyChar;
                    if (Uri.IsHexDigit(c))
                    {
                        EditNibble(Convert.ToInt32(c.ToString(), 16));
                    }
                    break;
            }
        }

        private void MoveCursor(int newIndex)
        {
            if (data.Count == 0)
            {
                cursor = 0;
                nibble = 0;
                return;
            }

            if (newIndex < 0) newIndex = 0;
            if (newIndex > data.Count - 1) newIndex = data.Count - 1;

            cursor = newIndex;
            nibble = 0;
        }

        private void EditNibble(int value)
        {
            if (data.Count == 0) return;

            int oldRow = cursor / BytesPerRow;
            byte current = data[cursor];

            if (nibble == 0)
            {
                data[cursor] = (byte)((value << 4) | (current & 0x0F));
                nibble = 1;
            }
            else
            {
                data[cursor] = (byte)((current & 0xF0) | value);
                nibble = 0;
                if (cursor < data.Count - 1) cursor++;
            }

            modified = true;

            MarkDirty(oldRow);
            MarkDirty(cursor / BytesPerRow);
        }

        private void InsertByte()
        {
            int oldRow = cursor / BytesPerRow;

            if (data.Count == 0)
            {
                data.Add(0);
                cursor = 0;
            }
            else
            {
                data.Insert(cursor + 1, 0);
                cursor++;
            }

            nibble = 0;
            modified = true;
            MarkDirtyFrom(oldRow);
        }

        private void DeleteByte()
        {
            if (data.Count == 0) return;

            int oldRow = cursor / BytesPerRow;

            data.RemoveAt(cursor);
            if (cursor >= data.Count) cursor = data.Count - 1;
            if (cursor < 0) cursor = 0;

            nibble = 0;
            modified = true;

            MarkDirtyFrom(Math.Min(oldRow, cursor / BytesPerRow));
        }

        private void SaveFile(bool force = false)
        {
            if (filePath == null || force)
            {
                if (!RequestFileName())
                {
                    needClear = true;
                    return;
                }
                hasBeenSaved = false;
            }

            bool needConfirm = File.Exists(filePath) &&
                               !hasBeenSaved &&
                               filePath != originalFilePath;

            if (needConfirm && !ConfirmOverwrite())
            {
                statusMessage = "Save canceled";
                needClear = true;
                return;
            }

            try
            {
                WriteAllBytes(filePath, data.ToArray());
                modified = false;
                hasBeenSaved = true;
                statusMessage = "The file has been saved successfully!";
            }
            catch
            {
                statusMessage = "File saving error!";
            }

            needClear = true;
        }

        public static void WriteAllBytes(string path, byte[] bytes)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (path.Length == 0)
                throw new ArgumentException("Path cant be empty.", nameof(path));
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                throw new ArgumentException("Path is incorrect.", nameof(path));

            if (File.Exists(path))
                File.Delete(path);

            using FileStream stream = File.Create(path);
            stream.Write(bytes, 0, bytes.Length);
            stream.Close();
        }

        private bool RequestFileName()
        {
            Console.ResetColor();
            Console.CursorVisible = true;

            try
            {
                Console.SetCursorPosition(0, Console.WindowHeight - 1);
                Console.Write("Enter file name: ");
                string name = ReadLineWithCancel();

                if (string.IsNullOrWhiteSpace(name))
                {
                    statusMessage = "Save canceled";
                    return false;
                }

                filePath = Path.GetFullPath(name);
                return true;
            }
            finally
            {
                Console.CursorVisible = false;
            }
        }

        private bool ConfirmOverwrite()
        {
            Console.ResetColor();
            Console.CursorVisible = true;

            try
            {
                Console.SetCursorPosition(0, Console.WindowHeight - 1);
                Console.Write($"File {Path.GetFileName(filePath)} exists. Overwrite? (Y/N) ");

                ConsoleKeyInfo key;
                do
                {
                    key = Console.ReadKey(true);
                } while (key.Key != ConsoleKey.Y && key.Key != ConsoleKey.N && key.Key != ConsoleKey.Escape);

                return key.Key == ConsoleKey.Y;
            }
            finally
            {
                Console.CursorVisible = false;
            }
        }

        private string ReadLineWithCancel()
        {
            string result = "";
            int pos = 0;

            while (true)
            {
                var key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        return result;

                    case ConsoleKey.Escape:
                        return null;

                    case ConsoleKey.Backspace when pos > 0:
                        result = result.Remove(pos - 1, 1);
                        pos--;
                        break;

                    case ConsoleKey.LeftArrow when pos > 0:
                        pos--;
                        break;

                    case ConsoleKey.RightArrow when pos < result.Length:
                        pos++;
                        break;

                    default:
                        if (!char.IsControl(key.KeyChar))
                        {
                            result = result.Insert(pos, key.KeyChar.ToString());
                            pos++;
                        }
                        break;
                }

                Console.SetCursorPosition(17, Console.WindowHeight - 1);
                Console.Write(new string(' ', Console.WindowWidth - 18));
                Console.SetCursorPosition(17, Console.WindowHeight - 1);
                Console.Write(result);
                Console.SetCursorPosition(17 + pos, Console.WindowHeight - 1);
            }
        }

        private bool CheckExit()
        {
            if (!modified) return true;

            statusMessage = "There are unsaved changes! Get out? (Y/N)";
            DrawStatus();

            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);
            } while (key.Key != ConsoleKey.Y && key.Key != ConsoleKey.N);

            return key.Key == ConsoleKey.Y;
        }
    }
}
