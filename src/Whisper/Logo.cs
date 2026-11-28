using Spectre.Console;

// Pixel-font banner: solid blue-to-green letters with an outlined drop shadow for depth.
static class Logo
{
    const string Word = "WHISPER";
    const int Cell = 6;

    static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['W'] = ["#...#", "#...#", "#.#.#", "##.##", "#...#"],
        ['H'] = ["#...#", "#...#", "#####", "#...#", "#...#"],
        ['I'] = ["#####", "..#..", "..#..", "..#..", "#####"],
        ['S'] = [".####", "#....", ".###.", "....#", "####."],
        ['P'] = ["####.", "#...#", "####.", "#....", "#...."],
        ['E'] = ["#####", "#....", "####.", "#....", "#####"],
        ['R'] = ["####.", "#...#", "####.", "#..#.", "#...#"]
    };

    public static int Width => Word.Length * Cell * 2;

    public static Align Render()
    {
        var w = Word.Length * Cell - 1;
        bool F(int x, int y) => y is >= 0 and < 5 && x >= 0 && x < w && x % Cell < 5 && Glyphs[Word[x / Cell]][y][x % Cell] == '#';
        bool S(int x, int y) => !F(x, y) && F(x - 1, y - 1);
        bool O(int x, int y) => F(x, y) || S(x, y);

        var rows = new List<Markup>();
        for (var y = 0; y < 6; y++)
        {
            var line = new StringBuilder();
            for (var x = 0; x <= w; x++)
            {
                var t = (double)x / w;
                if (F(x, y)) line.Append($"[{Hex(t, 1)}]██[/]");
                else if (S(x, y))
                {
                    bool l = !O(x - 1, y), r = !O(x + 1, y), top = !O(x, y - 1), bottom = !O(x, y + 1);
                    var left = l && top ? '┌' : l && bottom ? '└' : l ? '│' : top || bottom ? '─' : ' ';
                    var right = r && top ? '┐' : r && bottom ? '┘' : r ? '│' : top || bottom ? '─' : ' ';
                    line.Append($"[{Hex(t, 0.85)}]{left}{right}[/]");
                }
                else line.Append("  ");
            }

            rows.Add(new Markup(line.ToString()));
        }

        return Align.Center(new Rows(rows));
    }

    static string Hex(double t, double k)
    {
        int L(int a, int b) => (int)((a + (b - a) * t) * k);
        return $"#{L(0x3B, 0x10):x2}{L(0x82, 0xB9):x2}{L(0xF6, 0x81):x2}";
    }
}
