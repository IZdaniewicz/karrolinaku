using System.Diagnostics;

namespace Karrolinaku.Game
{
    public class ShikakuOld
    {

        private int _gridRowsNum;
        private int _gridColsNum;

        private int _area => _gridColsNum * _gridRowsNum;
        private List<Rect> _rects = [];

        public ShikakuOld(int gridRowsNum, int gridColsNum)
        {
            _gridColsNum = gridColsNum;
            _gridRowsNum = gridRowsNum;

            Rect mainRect = new() { ColsNumber = gridColsNum, RowsNumber = gridRowsNum, LeftUpCornerX = 0, LeftUpCornerY = 0 };

            _rects.AddRange(_splitRect(mainRect));
        }

        private List<Rect> _splitRect(Rect rect)
        {
            if (rect.Area <= _area/4 && (rect.Area == 1 || rect.Area == 2 || _randomBool(0.38 + 1.0/(rect.Area)))) 
            {
                return [rect];
            }

            bool splitUpDown = true;
            if (rect.RowsNumber == 1)
            {
                splitUpDown = false;
            }
            else if (rect.ColsNumber == 1)
            {
                splitUpDown = true;
            }
            else
            {
                splitUpDown = _randomBool(0.5);
            }



            if (splitUpDown)
            {
                int rowSplitAxis = Random.Shared.Next(1, rect.RowsNumber);

                var newRect = rect.SplitUpDown(rowSplitAxis);

                return [.. _splitRect(rect), .. _splitRect(newRect)];

            }
            else
            {
                int colSplitAxis = Random.Shared.Next(1, rect.ColsNumber);

                var newRect = rect.SplitLeftRight(colSplitAxis);
                return [.. _splitRect(rect), .. _splitRect(newRect)];
            }

        }



        private bool _randomBool(double probability)
        {
            probability = Math.Clamp(probability, 0.0, 1.0);
            return Random.Shared.NextDouble() < probability;
        }

        public string[][] GetRectString()
        {
            string[][] result = new string[_gridRowsNum][];

            for (int i = 0; i < _gridRowsNum; i++)
            {
                result[i] = new string[_gridColsNum];

                for (int j = 0; j < _gridColsNum; j++)
                {
                    result[i][j] = string.Empty;
                }
            }


            foreach (var rect in _rects)
            {
                int x = Random.Shared.Next(rect.LeftUpCornerX, rect.LeftUpCornerX + rect.RowsNumber);
                int y = Random.Shared.Next(rect.LeftUpCornerY, rect.LeftUpCornerY + rect.ColsNumber);
                for (int i = rect.LeftUpCornerX; i < rect.LeftUpCornerX + rect.RowsNumber; i++)
                {
                    for (int j = rect.LeftUpCornerY; j < rect.LeftUpCornerY + rect.ColsNumber; j++)
                    {
                        result[i][j] = (i == x && j == y) ? rect.Area.ToString() : string.Empty;
                    }
                }
            }

            return result;
        }
    }

    public class RectOLD
    {
        public int LeftUpCornerX { get; set; }
        public int LeftUpCornerY { get; set; }

        public int RightUpCornerX => LeftUpCornerX + ColsNumber - 1;
        public int RightUpCornerY => LeftUpCornerY;

        public int LeftDownCornerX => LeftUpCornerX;
        public int LeftDownCornerY => LeftUpCornerY + RowsNumber - 1;

        public int RightDownCornerX => LeftDownCornerX + ColsNumber - 1;
        public int RightDownCornerY => LeftDownCornerY;


        public int RowsNumber { get; set; }
        public int ColsNumber { get; set; }

        public string Color { get; set; } = "slategrey";

        public int Area => RowsNumber * ColsNumber;

        /// <summary>
        /// Rozdziela prostokąt na dwa według podanej osi
        /// </summary>
        /// <param name="rowSplitAxis">Oś podziału np. 1 jeżeli chcemy podzielic prostokąt na dwa gdzie pierwszy ma jeden wiersz drugi pozostałe</param>
        /// <returns>Drugi prostokąt po podziale</returns>
        public Rect SplitUpDown(int rowSplitAxis)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(rowSplitAxis, 0);
            if (rowSplitAxis >= RowsNumber)
            {
                throw new ArgumentOutOfRangeException($"{nameof(rowSplitAxis)} musi być mniejsze od {RowsNumber}");
            }

            var newRect = new Rect()
            {
                RowsNumber = RowsNumber - rowSplitAxis,
                ColsNumber = ColsNumber,
                LeftUpCornerX = LeftUpCornerX + rowSplitAxis,
                LeftUpCornerY = LeftUpCornerY 
            };
            RowsNumber = rowSplitAxis;

            return newRect;
        }

        /// <summary>
        /// Rozdziela prostokąt na dwa według podanej osi
        /// </summary>
        /// <param name="colSplitAxis">Oś podziału np. 1 jeżeli chcemy podzielic prostokąt na dwa gdzie pierwszy ma jedną kolumnę drugi pozostałe</param>
        /// <returns>Drugi prostokąt po podziale</returns>
        public Rect SplitLeftRight(int colSplitAxis)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(colSplitAxis, 0);
            if (colSplitAxis >= ColsNumber)
            {
                throw new ArgumentOutOfRangeException($"{nameof(colSplitAxis)} musi być mniejsze od {ColsNumber}");
            }


            var newRect = new Rect()
            {
                RowsNumber = RowsNumber,
                ColsNumber = ColsNumber - colSplitAxis,
                LeftUpCornerX = LeftUpCornerX ,
                LeftUpCornerY = LeftUpCornerY + colSplitAxis
            };
            ColsNumber = colSplitAxis;
            return newRect;
        }

        public bool IsOverlappedBy(Rect other)
        {
            var l1x = LeftUpCornerX;
            var l2x = other.LeftUpCornerX;
            var l1y = LeftUpCornerY;
            var l2y = other.LeftUpCornerY;

            var r1x = RightDownCornerX;
            var r2x = other.RightDownCornerX;
            var r1y = RightDownCornerY;
            var r2y = other.RightDownCornerY;

            if (l1x > r2x || l2x > r1x)
            {
                return false;
            }
            if (l1y > r2y || l2y > r1y)
                return false;

            return true;
        }
    }
}
