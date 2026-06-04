using Karrolinaku.Game;
using Microsoft.AspNetCore.Components;

namespace Karrolinaku.Pages
{
    public partial class Home
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; } = null!;

        private const int ROWS_NUM = 35;
        private const int COLS_NUM = 45;
        private ShikakuBoard ShikakuBoard { get; set; } = new(ROWS_NUM, COLS_NUM);


        protected override Task OnInitializedAsync()
        {
            return base.OnInitializedAsync();
        }

        private bool IsClicked { get; set; }

        private Rect? CurrentRect { get; set; }
        private int CurrentRectStartX { get; set; }
        private int CurrentRectStartY { get; set; }

        private async Task RefreshUI() => await InvokeAsync(StateHasChanged);
        private async Task HandleClick(GridCell gridCell)
        {
            string color = ShikakuBoard.GenerateRandomRectColor();

            gridCell.Color = color;
            CurrentRect = new() { LeftUpCornerX = gridCell.X, LeftUpCornerY = gridCell.Y, Color = color, RowsNumber = 1, ColsNumber = 1 };
            CurrentRectStartX = gridCell.X;
            CurrentRectStartY = gridCell.Y;
            IsClicked = true;

            await PaintGrid();
        }

        private async Task HandleMouseUp(GridCell gridCell)
        {
            if (!IsClicked || CurrentRect is null)
            {
                return;
            }

            var currentRectAreaCells = GetRectCells(CurrentRect).Where(x => !string.IsNullOrWhiteSpace(x.Content));
            if (
                currentRectAreaCells.Count() == 1
                && currentRectAreaCells.First().ContentInt == CurrentRect.Area
                )
            {

                ShikakuBoard.Rects.RemoveAll(x => RectsOverlapping(x,CurrentRect));
                ShikakuBoard.Rects.Add(CurrentRect);
            }

            CurrentRect = null;
            IsClicked = false;
            await PaintGrid();

            if (ShikakuBoard.Grid.SelectMany(x => x).All(x => x.Color != GridCell.DefaultColor))
            {
                NavigationManager.NavigateTo("/win");
            }
        }

        private bool RectsOverlapping(Rect r1, Rect r2)
        {
            return r1.IsOverlappedBy(r2);
        }

        private List<GridCell> GetRectCells(Rect rect)
        {
            if (rect is null)
                return [];
            List<GridCell> result = [];
            var currentX = rect.LeftUpCornerX;
            var currentY = rect.LeftUpCornerY;

            for (int i = 0; i < rect.RowsNumber; i++)
            {
                for (int j = 0; j < rect.ColsNumber; j++)
                {
                    GridCell? cell = ShikakuBoard.Grid.SelectMany(x => x).FirstOrDefault(x => x.X == currentX + j && x.Y == currentY + i);
                    if (cell is not null)
                    {
                        result.Add(cell);
                    }
                }
            }
            return result;
        }

        private async Task PaintGrid()
        {
            var rects = ShikakuBoard.Rects.ToList();

            foreach (var cell in ShikakuBoard.Grid.SelectMany(x => x))
            {
                var rect = ShikakuBoard.Rects
                    .FirstOrDefault(r
                    => cell.X >= r.LeftUpCornerX && cell.X <= r.LeftUpCornerX + r.ColsNumber - 1
                    && cell.Y >= r.LeftUpCornerY && cell.Y <= r.LeftUpCornerY + r.RowsNumber - 1);

                if (rect is not null)
                {
                    cell.Color = rect.Color;
                }
                else
                {
                    cell.Color = GridCell.DefaultColor;
                }
                cell.AdditionalContent = string.Empty;
            }

            if (CurrentRect is not null)
            {
                var currentX = CurrentRect.LeftUpCornerX;
                var currentY = CurrentRect.LeftUpCornerY;

                int middleX = CurrentRect.LeftUpCornerX + (CurrentRect.ColsNumber - 1) / 2;
                int middleY = CurrentRect.LeftUpCornerY + (CurrentRect.RowsNumber - 1) / 2;

                for (int i = 0; i < CurrentRect.RowsNumber; i++)
                {
                    for (int j = 0; j < CurrentRect.ColsNumber; j++)
                    {
                        int cellX = currentX + j;
                        int cellY = currentY + i;
                        GridCell? cell = ShikakuBoard.Grid.SelectMany(x => x).FirstOrDefault(x => x.X == cellX && x.Y == cellY);
                        cell?.Color = CurrentRect.Color;
                        if (cellX == middleX && cellY == middleY)
                        {
                            cell?.AdditionalContent = $"{CurrentRect.RowsNumber}:{CurrentRect.ColsNumber}";
                        }
                    }
                }
            }

            await RefreshUI();
        }


        private async Task HandleHover(GridCell gridCell)
        {
            if (!IsClicked || CurrentRect is null)
                return;

            var minX = Math.Min(CurrentRectStartX, gridCell.X);
            var maxX = Math.Max(CurrentRectStartX, gridCell.X);
            CurrentRect.LeftUpCornerX = minX;
            CurrentRect.ColsNumber = maxX - minX + 1;

            var minY = Math.Min(CurrentRectStartY, gridCell.Y);
            var maxY = Math.Max(CurrentRectStartY, gridCell.Y);
            CurrentRect.LeftUpCornerY = minY;
            CurrentRect.RowsNumber = maxY - minY + 1;

            await PaintGrid();
        }



    }

    public class ShikakuBoard
    {
        public Shikaku Shikaku { get; private set; }
        public List<Rect> Rects { get; set; } = [];

        public List<List<GridCell>> Grid { get; set; } = [];

        public string GenerateRandomRectColor()
        {
            string[] palette = [
    // Greens
    "#1e3d2a", "#2e4a38", "#1a4030", "#3a5228", "#243d20",
    "#2a4a20", "#1e4a3a", "#304828", "#1a3828", "#384a20",
    // Blues
    "#1e2d4a", "#2d3d5e", "#1a2e52", "#243358", "#1e3a5e",
    "#283d60", "#1a2a48", "#203558", "#1e3050", "#2a3a5a",
    // Purples
    "#3d2a4a", "#2e1e4a", "#3a2252", "#321a48", "#402a50",
    "#2a1e42", "#382850", "#301a44", "#3a2248", "#2e2448",
    // Reds / Magentas
    "#4a1e2a", "#521a28", "#4a2232", "#481e30", "#501828",
    "#42202e", "#4a1a24", "#501e30", "#481e28", "#422030",
    // Ambers / Browns
    "#4a3018", "#3d3820", "#4a381a", "#3d3418", "#483a20",
    "#423220", "#4a3422", "#3a3018", "#4a3c1e", "#3e3020",
    // Teals / Cyans
    "#1a3d40", "#1e4044", "#1a3a3e", "#1e3c42", "#163840",
    "#1a3c3e", "#183a40", "#1e3e44", "#1a4040", "#1c3c42",
    // Olive / Khaki
    "#383d18", "#3a3e1e", "#34381a", "#3c401e", "#363a18",
    "#3a3c1c", "#363e1a", "#3c3e20", "#383c1c", "#343618",
    // Deep roses / Wines
    "#4a1e38", "#421a32", "#4a2040", "#401c38", "#482040",
    "#3e1a36", "#44203a", "#3c1c34", "#462238", "#401e36",
];

            var unused = palette.Where(c => !Rects.Any(x => x.Color == c)).ToArray();
            if (unused.Length == 0 && Rects.Count != palette.Count())
                return palette[new Random().Next(palette.Length)];

            return unused[new Random().Next(unused.Length)];
        }


        public ShikakuBoard(int rowsNumber, int colsNumber)
        {
            Shikaku = new(rowsNumber, colsNumber);
            int x = 0;
            int y = 0;
            foreach (var row in Shikaku.GetRectString())
            {
                x = 0;
                List<GridCell> rowGrid = [];
                foreach (var col in row)
                {
                    rowGrid.Add(new() { Content = col, X = x, Y = y });
                    x++;
                }
                Grid.Add(rowGrid);
                y++;
            }
        }
    }


    public class GridCell
    {
        public int X { get; init; }
        public int Y { get; init; }
        public string Content { get; set; } = string.Empty;
        public string Color { get; set; } = DefaultColor;

        public const string DefaultColor = "#141820";

        public string Class => Color == DefaultColor ? string.Empty : "rect";

        public int ContentInt => !string.IsNullOrEmpty(Content) ? Convert.ToInt32(Content) : -1;

        public string AdditionalContent { get; set; } = string.Empty;


        public bool IsSameGrid(GridCell other)
        {
            return X == other.X && Y == other.Y;
        }

    }
}
