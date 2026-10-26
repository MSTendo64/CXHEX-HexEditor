using cxhex;

class Program
{
    static void Main(string[] args)
    {
        string filePath = args.Length > 0 ? args[0] : null;
        HexEditor editor = new HexEditor(filePath);
        editor.Run();
    }
}