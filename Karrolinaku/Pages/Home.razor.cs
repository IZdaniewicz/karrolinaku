namespace Karrolinaku.Pages;

public partial class Home
{
    private static readonly BoardSizeOption[] BoardSizes =
    [
        new("small", "Mała 6 × 6", 6, 6),
        new("medium", "Średnia 8 × 8", 8, 8),
        new("large", "Duża 10 × 10", 10, 10),
        new("wide", "Szeroka 10 × 14", 10, 14)
    ];

    private static readonly DifficultyOption[] DifficultyLevels =
    [
        new("calm", "Spokojny", "Duże, czytelne prostokąty. Dobre na pierwszą, relaksującą partię.", 7, 0.42, 4),
        new("focus", "Skupienie", "Zrównoważona plansza: nadal przyjemna, ale wymaga już planowania.", 5, 0.62, 3),
        new("deep", "Głębokie myślenie", "Więcej mniejszych obszarów i mniej oczywistych cięć.", 4, 0.78, 2)
    ];

    private string SelectedBoardSizeKey { get; set; } = "medium";
    private string SelectedDifficultyKey { get; set; } = "focus";
    private PuzzleBoard Board { get; set; } = PuzzleBoard.Generate(8, 8, DifficultyLevels[1]);
    private BoardRect? CurrentRect { get; set; }
    private GridCell? DragStartCell { get; set; }
    private bool IsPointerDown { get; set; }
    private bool IsSolved { get; set; }
    private string StatusMessage { get; set; } = "Zaznacz prostokąt: dokładnie jedna liczba w środku, a pole prostokąta równe tej liczbie.";
    private string StatusMessageClass => IsSolved ? "status-message success" : "status-message";

    private DifficultyOption CurrentDifficulty => DifficultyLevels.First(x => x.Key == SelectedDifficultyKey);

    private int CompletionPercent
    {
        get
        {
            int covered = Board.AcceptedRects.Sum(x => x.Area);
            return (int)Math.Round(100.0 * covered / Board.Area);
        }
    }

    private void StartNewGame()
    {
        var size = BoardSizes.First(x => x.Key == SelectedBoardSizeKey);
        Board = PuzzleBoard.Generate(size.Rows, size.Cols, CurrentDifficulty);
        CurrentRect = null;
        DragStartCell = null;
        IsPointerDown = false;
        IsSolved = false;
        StatusMessage = "Nowa plansza gotowa. Zacznij od liczb przy krawędziach albo od największych wartości.";
        PaintGrid();
    }

    private void ClearBoard()
    {
        Board.AcceptedRects.Clear();
        CurrentRect = null;
        IsSolved = false;
        StatusMessage = "Plansza wyczyszczona. Możesz spokojnie zacząć jeszcze raz.";
        PaintGrid();
    }

    private void CheckBoard()
    {
        var issues = ValidateBoard();
        if (issues.Count == 0)
        {
            IsSolved = true;
            StatusMessage = "Pięknie. Cała plansza jest poprawnie podzielona.";
            return;
        }

        IsSolved = false;
        StatusMessage = issues[0];
    }

    private void RevealOneRect()
    {
        var missing = Board.Solution.FirstOrDefault(solution =>
            !Board.AcceptedRects.Any(accepted => accepted.HasSameGeometry(solution)));

        if (missing is null)
        {
            CheckBoard();
            return;
        }

        Board.AcceptedRects.RemoveAll(rect => rect.Overlaps(missing));
        Board.AcceptedRects.Add(missing.Copy());
        CurrentRect = null;
        StatusMessage = "Dodałem jeden poprawny prostokąt jako delikatną podpowiedź.";
        PaintGrid();
    }

    private void HandlePointerDown(GridCell cell)
    {
        IsPointerDown = true;
        DragStartCell = cell;
        CurrentRect = BoardRect.FromCells(cell, cell);
        Board.AcceptedRects.RemoveAll(rect => rect.Contains(cell.Row, cell.Col));
        PaintGrid();
    }

    private void HandlePointerEnter(GridCell cell)
    {
        if (!IsPointerDown || DragStartCell is null)
            return;

        CurrentRect = BoardRect.FromCells(DragStartCell, cell);
        PaintGrid();
    }

    private void HandlePointerUp(GridCell cell)
    {
        if (!IsPointerDown || DragStartCell is null)
            return;

        CurrentRect = BoardRect.FromCells(DragStartCell, cell);
        TryAcceptCurrentRect();
        CurrentRect = null;
        DragStartCell = null;
        IsPointerDown = false;
        PaintGrid();
    }

    private void TryAcceptCurrentRect()
    {
        if (CurrentRect is null)
            return;

        var clues = Board.Clues.Where(CurrentRect.Contains).ToList();

        if (clues.Count != 1)
        {
            StatusMessage = "Prostokąt musi zawierać dokładnie jedną liczbę.";
            return;
        }

        if (clues[0].Area != CurrentRect.Area)
        {
            StatusMessage = $"Ten prostokąt ma pole {CurrentRect.Area}, a liczba wymaga pola {clues[0].Area}.";
            return;
        }

        Board.AcceptedRects.RemoveAll(rect => rect.Overlaps(CurrentRect));
        Board.AcceptedRects.Add(CurrentRect.Copy());
        StatusMessage = "Dobry prostokąt. Kontynuuj w tym tempie.";

        if (ValidateBoard().Count == 0)
        {
            IsSolved = true;
            StatusMessage = "Wygrana. Plansza jest domknięta bez konfliktów.";
        }
    }

    private List<string> ValidateBoard()
    {
        bool[,] covered = new bool[Board.RowsNumber, Board.ColsNumber];
        List<string> issues = [];

        foreach (var rect in Board.AcceptedRects)
        {
            var clues = Board.Clues.Where(rect.Contains).ToList();
            if (clues.Count != 1)
                issues.Add("Każdy prostokąt musi zawierać dokładnie jedną liczbę.");
            else if (clues[0].Area != rect.Area)
                issues.Add($"Jeden z prostokątów ma pole {rect.Area}, a powinien mieć {clues[0].Area}.");

            for (int row = rect.LeftUpCornerX; row <= rect.RightDownCornerX; row++)
            {
                for (int col = rect.LeftUpCornerY; col <= rect.RightDownCornerY; col++)
                {
                    if (row < 0 || row >= Board.RowsNumber || col < 0 || col >= Board.ColsNumber)
                    {
                        issues.Add("Prostokąt wychodzi poza planszę.");
                        continue;
                    }

                    if (covered[row, col])
                        issues.Add("Prostokąty nie mogą na siebie nachodzić.");

                    covered[row, col] = true;
                }
            }
        }

        int coveredCount = 0;
        foreach (bool value in covered)
        {
            if (value)
                coveredCount++;
        }

        if (coveredCount != Board.Area)
            issues.Add("Cała plansza musi być przykryta prostokątami.");

        return issues.Distinct().ToList();
    }

    private void PaintGrid()
    {
        foreach (var cell in Board.Grid.SelectMany(x => x))
        {
            cell.CssClass = string.Empty;
            cell.Title = cell.Clue is null ? "Puste pole" : $"Liczba {cell.Clue.Area}";
        }

        foreach (var rect in Board.AcceptedRects)
        {
            var isValid = RectIsValid(rect);
            foreach (var cell in Board.CellsInside(rect))
            {
                cell.CssClass = isValid ? "accepted valid" : "accepted invalid";
            }
        }

        if (CurrentRect is not null)
        {
            foreach (var cell in Board.CellsInside(CurrentRect))
            {
                cell.CssClass = "preview";
            }
        }
    }

    private bool RectIsValid(BoardRect rect)
    {
        var clues = Board.Clues.Where(rect.Contains).ToList();
        return clues.Count == 1 && clues[0].Area == rect.Area;
    }

    public sealed record BoardSizeOption(string Key, string Label, int Rows, int Cols);

    public sealed record DifficultyOption(string Key, string Label, string Description, int TargetArea, double SplitChance, int MinArea);

    public sealed class PuzzleBoard
    {
        public int RowsNumber { get; }
        public int ColsNumber { get; }
        public int Area => RowsNumber * ColsNumber;
        public List<List<GridCell>> Grid { get; } = [];
        public List<Clue> Clues { get; } = [];
        public List<BoardRect> Solution { get; } = [];
        public List<BoardRect> AcceptedRects { get; } = [];

        private PuzzleBoard(int rowsNumber, int colsNumber)
        {
            RowsNumber = rowsNumber;
            ColsNumber = colsNumber;

            for (int row = 0; row < rowsNumber; row++)
            {
                List<GridCell> cells = [];
                for (int col = 0; col < colsNumber; col++)
                {
                    cells.Add(new GridCell(row, col));
                }
                Grid.Add(cells);
            }
        }

        public static PuzzleBoard Generate(int rowsNumber, int colsNumber, DifficultyOption difficulty)
        {
            PuzzleBoard board = new(rowsNumber, colsNumber);
            Random random = Random.Shared;
            List<BoardRect> rects = [new BoardRect(0, 0, rowsNumber, colsNumber)];

            for (int guard = 0; guard < 600; guard++)
            {
                var candidate = rects
                    .Where(rect => rect.Area > difficulty.TargetArea || random.NextDouble() < difficulty.SplitChance)
                    .OrderByDescending(rect => rect.Area + random.NextDouble() * difficulty.TargetArea)
                    .FirstOrDefault();

                if (candidate is null || rects.Count >= Math.Ceiling(board.Area / (double)difficulty.MinArea))
                    break;

                var split = Split(candidate, random, difficulty);
                if (split is null)
                    break;

                rects.Remove(candidate);
                rects.Add(split.Value.First);
                rects.Add(split.Value.Second);
            }

            foreach (var rect in rects.OrderBy(x => x.LeftUpCornerX).ThenBy(x => x.LeftUpCornerY))
            {
                var clue = new Clue(
                    random.Next(rect.LeftUpCornerX, rect.RightDownCornerX + 1),
                    random.Next(rect.LeftUpCornerY, rect.RightDownCornerY + 1),
                    rect.Area);

                board.Solution.Add(rect.Copy());
                board.Clues.Add(clue);
                board.Grid[clue.Row][clue.Col].Clue = clue;
            }

            return board;
        }

        private static (BoardRect First, BoardRect Second)? Split(BoardRect rect, Random random, DifficultyOption difficulty)
        {
            bool canHorizontal = rect.RowsNumber >= 2;
            bool canVertical = rect.ColsNumber >= 2;

            if (!canHorizontal && !canVertical)
                return null;

            bool horizontal = rect.RowsNumber > rect.ColsNumber || (rect.RowsNumber == rect.ColsNumber && random.NextDouble() > 0.5);
            if (horizontal && !canHorizontal)
                horizontal = false;
            if (!horizontal && !canVertical)
                horizontal = true;

            if (horizontal)
            {
                int cut = random.Next(1, rect.RowsNumber);
                var first = new BoardRect(rect.LeftUpCornerX, rect.LeftUpCornerY, cut, rect.ColsNumber);
                var second = new BoardRect(rect.LeftUpCornerX + cut, rect.LeftUpCornerY, rect.RowsNumber - cut, rect.ColsNumber);
                return first.Area >= difficulty.MinArea && second.Area >= difficulty.MinArea ? (first, second) : null;
            }

            int columnCut = random.Next(1, rect.ColsNumber);
            var left = new BoardRect(rect.LeftUpCornerX, rect.LeftUpCornerY, rect.RowsNumber, columnCut);
            var right = new BoardRect(rect.LeftUpCornerX, rect.LeftUpCornerY + columnCut, rect.RowsNumber, rect.ColsNumber - columnCut);
            return left.Area >= difficulty.MinArea && right.Area >= difficulty.MinArea ? (left, right) : null;
        }

        public IEnumerable<GridCell> CellsInside(BoardRect rect)
        {
            for (int row = rect.LeftUpCornerX; row <= rect.RightDownCornerX; row++)
            {
                for (int col = rect.LeftUpCornerY; col <= rect.RightDownCornerY; col++)
                {
                    if (row >= 0 && row < RowsNumber && col >= 0 && col < ColsNumber)
                        yield return Grid[row][col];
                }
            }
        }
    }

    public sealed class GridCell(int row, int col)
    {
        public int Row { get; } = row;
        public int Col { get; } = col;
        public Clue? Clue { get; set; }
        public string CssClass { get; set; } = string.Empty;
        public string Title { get; set; } = "Puste pole";
    }

    public sealed record Clue(int Row, int Col, int Area);

    public sealed record BoardRect(int LeftUpCornerX, int LeftUpCornerY, int RowsNumber, int ColsNumber)
    {
        public int RightDownCornerX => LeftUpCornerX + RowsNumber - 1;
        public int RightDownCornerY => LeftUpCornerY + ColsNumber - 1;
        public int Area => RowsNumber * ColsNumber;

        public static BoardRect FromCells(GridCell first, GridCell second)
        {
            int top = Math.Min(first.Row, second.Row);
            int left = Math.Min(first.Col, second.Col);
            int bottom = Math.Max(first.Row, second.Row);
            int right = Math.Max(first.Col, second.Col);
            return new BoardRect(top, left, bottom - top + 1, right - left + 1);
        }

        public bool Contains(Clue clue) => Contains(clue.Row, clue.Col);

        public bool Contains(int row, int col) =>
            row >= LeftUpCornerX && row <= RightDownCornerX &&
            col >= LeftUpCornerY && col <= RightDownCornerY;

        public bool Overlaps(BoardRect other) =>
            LeftUpCornerX <= other.RightDownCornerX && RightDownCornerX >= other.LeftUpCornerX &&
            LeftUpCornerY <= other.RightDownCornerY && RightDownCornerY >= other.LeftUpCornerY;

        public bool HasSameGeometry(BoardRect other) =>
            LeftUpCornerX == other.LeftUpCornerX &&
            LeftUpCornerY == other.LeftUpCornerY &&
            RowsNumber == other.RowsNumber &&
            ColsNumber == other.ColsNumber;

        public BoardRect Copy() => this with { };
    }
}
