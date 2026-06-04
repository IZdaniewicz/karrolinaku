namespace Karrolinaku.Game
{
    public class Shikaku
    {
        private readonly int _gridRowsNum;
        private readonly int _gridColsNum;

        private int _area => _gridColsNum * _gridRowsNum;
        private readonly List<Rect> _rects = [];

        private readonly Random _random;

        // Parametry pod "master"
        private readonly int _softMinFinalArea;
        private readonly int _softMaxFinalArea;
        private readonly int _hardMaxFinalArea;

        private const int MaxAspectRatio = 8;

        public Shikaku(int gridRowsNum, int gridColsNum, Random? random = null)
        {
            if (gridRowsNum <= 0)
                throw new ArgumentOutOfRangeException(nameof(gridRowsNum));

            if (gridColsNum <= 0)
                throw new ArgumentOutOfRangeException(nameof(gridColsNum));

            _gridColsNum = gridColsNum;
            _gridRowsNum = gridRowsNum;
            _random = random ?? Random.Shared;

            // Dla 35x45:
            // _softMinFinalArea ~= 7
            // _softMaxFinalArea ~= 28
            // _hardMaxFinalArea ~= 52
            //
            // Czyli generator najchętniej kończy prostokąty w zakresie 7-28,
            // czasem pozwala na większe, ale nie chce robić ogromnych pól.
            _softMinFinalArea = Math.Clamp(_area / 220, 4, 9);
            _softMaxFinalArea = Math.Clamp(_area / 55, 16, 32);
            _hardMaxFinalArea = Math.Clamp(_area / 30, _softMaxFinalArea + 4, 60);

            Rect mainRect = new()
            {
                ColsNumber = gridColsNum,
                RowsNumber = gridRowsNum,
                LeftUpCornerX = 0,
                LeftUpCornerY = 0
            };

            _rects.AddRange(SplitRectMaster(mainRect));
        }

        private List<Rect> SplitRectMaster(Rect rect)
        {
            if (ShouldStop(rect))
            {
                return [rect];
            }

            var candidates = GetSplitCandidates(rect);

            if (candidates.Count == 0)
            {
                return [rect];
            }

            var chosen = ChooseWeighted(candidates);

            Rect newRect;

            if (chosen.SplitUpDown)
            {
                newRect = rect.SplitUpDown(chosen.Axis);
            }
            else
            {
                newRect = rect.SplitLeftRight(chosen.Axis);
            }

            return [
                .. SplitRectMaster(rect),
                .. SplitRectMaster(newRect)
            ];
        }

        private bool ShouldStop(Rect rect)
        {
            if (rect.RowsNumber == 1 && rect.ColsNumber == 1)
                return true;

            if (rect.Area < 2 * _softMinFinalArea)
                return true;

            // Za duże pole trzeba dalej dzielić.
            if (rect.Area > _hardMaxFinalArea)
                return false;

            double quality = FinalRectQuality(rect.RowsNumber, rect.ColsNumber);

            double probability = 0.05 + 0.78 * quality;

            // Zniechęcamy do kończenia na dużych polach.
            if (rect.Area > _softMaxFinalArea)
                probability *= 0.60;

            // Liczby pierwsze są zwykle łatwiejsze w Shikaku,
            // bo mają mniej możliwych kształtów.
            if (CountDivisors(rect.Area) <= 2)
                probability *= 0.20;

            // Liczby z wieloma dzielnikami są ciekawsze.
            if (FactorScore(rect.Area) >= 0.5)
                probability += 0.10;

            probability = Math.Clamp(probability, 0.05, 0.90);

            return RandomBool(probability);
        }

        private List<SplitCandidate> GetSplitCandidates(Rect rect)
        {
            List<SplitCandidate> candidates = [];

            for (int axis = 1; axis < rect.RowsNumber; axis++)
            {
                candidates.Add(CreateSplitCandidate(rect, splitUpDown: true, axis));
            }

            for (int axis = 1; axis < rect.ColsNumber; axis++)
            {
                candidates.Add(CreateSplitCandidate(rect, splitUpDown: false, axis));
            }

            if (candidates.Count == 0)
                return candidates;

            // Jeżeli da się uniknąć pól 1 i 2, to unikamy.
            var withoutTiny = candidates
                .Where(c => c.AreaA >= 3 && c.AreaB >= 3)
                .ToList();

            if (withoutTiny.Count > 0)
                candidates = withoutTiny;

            // Jeżeli da się zrobić oba kawałki sensownie duże, to preferujemy to.
            var withoutTooSmall = candidates
                .Where(c => c.AreaA >= _softMinFinalArea && c.AreaB >= _softMinFinalArea)
                .ToList();

            if (withoutTooSmall.Count > 0)
                candidates = withoutTooSmall;

            // Jeżeli da się uniknąć bardzo cienkich prostokątów, to unikamy.
            var withoutSkinny = candidates
                .Where(c =>
                    !IsTooThin(c.RowsA, c.ColsA) &&
                    !IsTooThin(c.RowsB, c.ColsB))
                .ToList();

            if (withoutSkinny.Count > 0)
                candidates = withoutSkinny;

            // Nie wybieramy zupełnie losowego cięcia.
            // Bierzemy górną część najlepszych kandydatów,
            // a potem losujemy ważeniem.
            candidates = candidates
                .OrderByDescending(c => c.Weight)
                .Take(Math.Max(1, candidates.Count / 3))
                .ToList();

            return candidates;
        }

        private SplitCandidate CreateSplitCandidate(Rect rect, bool splitUpDown, int axis)
        {
            int rowsA;
            int colsA;
            int rowsB;
            int colsB;

            if (splitUpDown)
            {
                rowsA = axis;
                colsA = rect.ColsNumber;

                rowsB = rect.RowsNumber - axis;
                colsB = rect.ColsNumber;
            }
            else
            {
                rowsA = rect.RowsNumber;
                colsA = axis;

                rowsB = rect.RowsNumber;
                colsB = rect.ColsNumber - axis;
            }

            int areaA = rowsA * colsA;
            int areaB = rowsB * colsB;

            double weight = ScoreSplit(rowsA, colsA, rowsB, colsB);

            return new SplitCandidate(
                splitUpDown,
                axis,
                weight,
                areaA,
                areaB,
                rowsA,
                colsA,
                rowsB,
                colsB
            );
        }

        private double ScoreSplit(int rowsA, int colsA, int rowsB, int colsB)
        {
            int areaA = rowsA * colsA;
            int areaB = rowsB * colsB;

            double potentialA = PotentialScore(rowsA, colsA);
            double potentialB = PotentialScore(rowsB, colsB);

            double potential = (potentialA + potentialB) / 2.0;

            double balance = Math.Min(areaA, areaB) / (double)Math.Max(areaA, areaB);

            double score = 0.75 * potential + 0.25 * Math.Sqrt(balance);

            if (areaA <= 2 || areaB <= 2)
                score *= 0.15;

            if (IsTooThin(rowsA, colsA) || IsTooThin(rowsB, colsB))
                score *= 0.25;

            return Math.Max(0.0001, score);
        }

        private double PotentialScore(int rows, int cols)
        {
            int area = rows * cols;

            if (area <= 2)
                return 0.02;

            // Jeżeli kawałek nadal jest duży, to jest okej,
            // bo później można go dalej podzielić.
            if (area > _hardMaxFinalArea)
            {
                return 0.55 + 0.35 * ShapeScore(rows, cols);
            }

            return FinalRectQuality(rows, cols);
        }

        private double FinalRectQuality(int rows, int cols)
        {
            int area = rows * cols;

            double areaScore = AreaScore(area);
            double factorScore = FactorScore(area);
            double shapeScore = ShapeScore(rows, cols);

            double nonPrimeBonus = CountDivisors(area) > 2 ? 1.0 : 0.15;

            return
                0.45 * areaScore +
                0.25 * factorScore +
                0.20 * shapeScore +
                0.10 * nonPrimeBonus;
        }

        private double AreaScore(int area)
        {
            if (area < 3)
                return 0.0;

            if (area < _softMinFinalArea)
            {
                return 0.35 * area / _softMinFinalArea;
            }

            if (area <= _softMaxFinalArea)
            {
                return 1.0;
            }

            if (area <= _hardMaxFinalArea)
            {
                double t = (area - _softMaxFinalArea) /
                           (double)(_hardMaxFinalArea - _softMaxFinalArea);

                return 1.0 - 0.5 * t;
            }

            return 0.0;
        }

        private double FactorScore(int area)
        {
            int divisors = CountDivisors(area);

            // Liczby pierwsze mają 2 dzielniki.
            // Im więcej dzielników, tym ciekawszy clue.
            return Math.Clamp((divisors - 2) / 8.0, 0.0, 1.0);
        }

        private int CountDivisors(int number)
        {
            int count = 0;

            for (int i = 1; i * i <= number; i++)
            {
                if (number % i != 0)
                    continue;

                count++;

                if (i * i != number)
                    count++;
            }

            return count;
        }

        private double ShapeScore(int rows, int cols)
        {
            int longer = Math.Max(rows, cols);
            int shorter = Math.Min(rows, cols);

            double ratio = longer / (double)shorter;

            if (ratio >= MaxAspectRatio)
                return 0.0;

            return 1.0 - (ratio - 1.0) / (MaxAspectRatio - 1.0);
        }

        private bool IsTooThin(int rows, int cols)
        {
            int longer = Math.Max(rows, cols);
            int shorter = Math.Min(rows, cols);

            return longer > shorter * MaxAspectRatio;
        }

        private SplitCandidate ChooseWeighted(List<SplitCandidate> candidates)
        {
            double totalWeight = 0.0;

            foreach (var candidate in candidates)
            {
                totalWeight += candidate.Weight * candidate.Weight;
            }

            if (totalWeight <= 0.0)
            {
                return candidates[_random.Next(candidates.Count)];
            }

            double roll = _random.NextDouble() * totalWeight;
            double current = 0.0;

            foreach (var candidate in candidates)
            {
                current += candidate.Weight * candidate.Weight;

                if (current >= roll)
                    return candidate;
            }

            return candidates[^1];
        }

        private bool RandomBool(double probability)
        {
            probability = Math.Clamp(probability, 0.0, 1.0);
            return _random.NextDouble() < probability;
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
                int x = _random.Next(rect.LeftUpCornerX, rect.LeftUpCornerX + rect.RowsNumber);
                int y = _random.Next(rect.LeftUpCornerY, rect.LeftUpCornerY + rect.ColsNumber);

                for (int i = rect.LeftUpCornerX; i < rect.LeftUpCornerX + rect.RowsNumber; i++)
                {
                    for (int j = rect.LeftUpCornerY; j < rect.LeftUpCornerY + rect.ColsNumber; j++)
                    {
                        result[i][j] = (i == x && j == y)
                            ? rect.Area.ToString()
                            : string.Empty;
                    }
                }
            }

            return result;
        }

        private readonly record struct SplitCandidate(
            bool SplitUpDown,
            int Axis,
            double Weight,
            int AreaA,
            int AreaB,
            int RowsA,
            int ColsA,
            int RowsB,
            int ColsB
        );
    }
    namespace Karolinaku.Game
    {
        public class Shikaku
        {
            private readonly int _gridRowsNum;
            private readonly int _gridColsNum;

            private int _area => _gridColsNum * _gridRowsNum;
            private readonly List<Rect> _rects = [];

            private readonly Random _random;

            // Parametry pod "master"
            private readonly int _softMinFinalArea;
            private readonly int _softMaxFinalArea;
            private readonly int _hardMaxFinalArea;

            private const int MaxAspectRatio = 8;

            public Shikaku(int gridRowsNum, int gridColsNum, Random? random = null)
            {
                if (gridRowsNum <= 0)
                    throw new ArgumentOutOfRangeException(nameof(gridRowsNum));

                if (gridColsNum <= 0)
                    throw new ArgumentOutOfRangeException(nameof(gridColsNum));

                _gridColsNum = gridColsNum;
                _gridRowsNum = gridRowsNum;
                _random = random ?? Random.Shared;

                // Dla 35x45:
                // _softMinFinalArea ~= 7
                // _softMaxFinalArea ~= 28
                // _hardMaxFinalArea ~= 52
                //
                // Czyli generator najchętniej kończy prostokąty w zakresie 7-28,
                // czasem pozwala na większe, ale nie chce robić ogromnych pól.
                _softMinFinalArea = Math.Clamp(_area / 220, 4, 9);
                _softMaxFinalArea = Math.Clamp(_area / 55, 16, 32);
                _hardMaxFinalArea = Math.Clamp(_area / 30, _softMaxFinalArea + 4, 60);

                Rect mainRect = new()
                {
                    ColsNumber = gridColsNum,
                    RowsNumber = gridRowsNum,
                    LeftUpCornerX = 0,
                    LeftUpCornerY = 0
                };

                _rects.AddRange(SplitRectMaster(mainRect));
            }

            private List<Rect> SplitRectMaster(Rect rect)
            {
                if (ShouldStop(rect))
                {
                    return [rect];
                }

                var candidates = GetSplitCandidates(rect);

                if (candidates.Count == 0)
                {
                    return [rect];
                }

                var chosen = ChooseWeighted(candidates);

                Rect newRect;

                if (chosen.SplitUpDown)
                {
                    newRect = rect.SplitUpDown(chosen.Axis);
                }
                else
                {
                    newRect = rect.SplitLeftRight(chosen.Axis);
                }

                return [
                    .. SplitRectMaster(rect),
                .. SplitRectMaster(newRect)
                ];
            }

            private bool ShouldStop(Rect rect)
            {
                if (rect.RowsNumber == 1 && rect.ColsNumber == 1)
                    return true;

                // Nie rozbijaj małych pól na 1, 2, 3 itd.
                // Dzięki temu generator nie produkuje masy banalnych prostokątów.
                if (rect.Area < 2 * _softMinFinalArea)
                    return true;

                // Za duże pole trzeba dalej dzielić.
                if (rect.Area > _hardMaxFinalArea)
                    return false;

                double quality = FinalRectQuality(rect.RowsNumber, rect.ColsNumber);

                double probability = 0.05 + 0.78 * quality;

                // Trochę zniechęcamy do kończenia na dużych polach.
                if (rect.Area > _softMaxFinalArea)
                    probability *= 0.60;

                // Liczby pierwsze są zwykle łatwiejsze w Shikaku,
                // bo mają mniej możliwych kształtów.
                if (CountDivisors(rect.Area) <= 2)
                    probability *= 0.20;

                // Liczby z wieloma dzielnikami są ciekawsze.
                if (FactorScore(rect.Area) >= 0.5)
                    probability += 0.10;

                probability = Math.Clamp(probability, 0.05, 0.90);

                return RandomBool(probability);
            }

            private List<SplitCandidate> GetSplitCandidates(Rect rect)
            {
                List<SplitCandidate> candidates = [];

                for (int axis = 1; axis < rect.RowsNumber; axis++)
                {
                    candidates.Add(CreateSplitCandidate(rect, splitUpDown: true, axis));
                }

                for (int axis = 1; axis < rect.ColsNumber; axis++)
                {
                    candidates.Add(CreateSplitCandidate(rect, splitUpDown: false, axis));
                }

                if (candidates.Count == 0)
                    return candidates;

                // Jeżeli da się uniknąć pól 1 i 2, to unikamy.
                var withoutTiny = candidates
                    .Where(c => c.AreaA >= 3 && c.AreaB >= 3)
                    .ToList();

                if (withoutTiny.Count > 0)
                    candidates = withoutTiny;

                // Jeżeli da się zrobić oba kawałki sensownie duże, to preferujemy to.
                var withoutTooSmall = candidates
                    .Where(c => c.AreaA >= _softMinFinalArea && c.AreaB >= _softMinFinalArea)
                    .ToList();

                if (withoutTooSmall.Count > 0)
                    candidates = withoutTooSmall;

                // Jeżeli da się uniknąć bardzo cienkich prostokątów, to unikamy.
                var withoutSkinny = candidates
                    .Where(c =>
                        !IsTooThin(c.RowsA, c.ColsA) &&
                        !IsTooThin(c.RowsB, c.ColsB))
                    .ToList();

                if (withoutSkinny.Count > 0)
                    candidates = withoutSkinny;

                // Nie wybieramy zupełnie losowego cięcia.
                // Bierzemy górną część najlepszych kandydatów,
                // a potem losujemy ważeniem.
                candidates = candidates
                    .OrderByDescending(c => c.Weight)
                    .Take(Math.Max(1, candidates.Count / 3))
                    .ToList();

                return candidates;
            }

            private SplitCandidate CreateSplitCandidate(Rect rect, bool splitUpDown, int axis)
            {
                int rowsA;
                int colsA;
                int rowsB;
                int colsB;

                if (splitUpDown)
                {
                    rowsA = axis;
                    colsA = rect.ColsNumber;

                    rowsB = rect.RowsNumber - axis;
                    colsB = rect.ColsNumber;
                }
                else
                {
                    rowsA = rect.RowsNumber;
                    colsA = axis;

                    rowsB = rect.RowsNumber;
                    colsB = rect.ColsNumber - axis;
                }

                int areaA = rowsA * colsA;
                int areaB = rowsB * colsB;

                double weight = ScoreSplit(rowsA, colsA, rowsB, colsB);

                return new SplitCandidate(
                    splitUpDown,
                    axis,
                    weight,
                    areaA,
                    areaB,
                    rowsA,
                    colsA,
                    rowsB,
                    colsB
                );
            }

            private double ScoreSplit(int rowsA, int colsA, int rowsB, int colsB)
            {
                int areaA = rowsA * colsA;
                int areaB = rowsB * colsB;

                double potentialA = PotentialScore(rowsA, colsA);
                double potentialB = PotentialScore(rowsB, colsB);

                double potential = (potentialA + potentialB) / 2.0;

                double balance = Math.Min(areaA, areaB) / (double)Math.Max(areaA, areaB);

                double score = 0.75 * potential + 0.25 * Math.Sqrt(balance);

                if (areaA <= 2 || areaB <= 2)
                    score *= 0.15;

                if (IsTooThin(rowsA, colsA) || IsTooThin(rowsB, colsB))
                    score *= 0.25;

                return Math.Max(0.0001, score);
            }

            private double PotentialScore(int rows, int cols)
            {
                int area = rows * cols;

                if (area <= 2)
                    return 0.02;

                // Jeżeli kawałek nadal jest duży, to jest okej,
                // bo później można go dalej podzielić.
                if (area > _hardMaxFinalArea)
                {
                    return 0.55 + 0.35 * ShapeScore(rows, cols);
                }

                return FinalRectQuality(rows, cols);
            }

            private double FinalRectQuality(int rows, int cols)
            {
                int area = rows * cols;

                double areaScore = AreaScore(area);
                double factorScore = FactorScore(area);
                double shapeScore = ShapeScore(rows, cols);

                double nonPrimeBonus = CountDivisors(area) > 2 ? 1.0 : 0.15;

                return
                    0.45 * areaScore +
                    0.25 * factorScore +
                    0.20 * shapeScore +
                    0.10 * nonPrimeBonus;
            }

            private double AreaScore(int area)
            {
                if (area < 3)
                    return 0.0;

                if (area < _softMinFinalArea)
                {
                    return 0.35 * area / _softMinFinalArea;
                }

                if (area <= _softMaxFinalArea)
                {
                    return 1.0;
                }

                if (area <= _hardMaxFinalArea)
                {
                    double t = (area - _softMaxFinalArea) /
                               (double)(_hardMaxFinalArea - _softMaxFinalArea);

                    return 1.0 - 0.5 * t;
                }

                return 0.0;
            }

            private double FactorScore(int area)
            {
                int divisors = CountDivisors(area);

                // Liczby pierwsze mają 2 dzielniki.
                // Im więcej dzielników, tym ciekawszy clue.
                return Math.Clamp((divisors - 2) / 8.0, 0.0, 1.0);
            }

            private int CountDivisors(int number)
            {
                int count = 0;

                for (int i = 1; i * i <= number; i++)
                {
                    if (number % i != 0)
                        continue;

                    count++;

                    if (i * i != number)
                        count++;
                }

                return count;
            }

            private double ShapeScore(int rows, int cols)
            {
                int longer = Math.Max(rows, cols);
                int shorter = Math.Min(rows, cols);

                double ratio = longer / (double)shorter;

                if (ratio >= MaxAspectRatio)
                    return 0.0;

                return 1.0 - (ratio - 1.0) / (MaxAspectRatio - 1.0);
            }

            private bool IsTooThin(int rows, int cols)
            {
                int longer = Math.Max(rows, cols);
                int shorter = Math.Min(rows, cols);

                return longer > shorter * MaxAspectRatio;
            }

            private SplitCandidate ChooseWeighted(List<SplitCandidate> candidates)
            {
                double totalWeight = 0.0;

                foreach (var candidate in candidates)
                {
                    totalWeight += candidate.Weight * candidate.Weight;
                }

                if (totalWeight <= 0.0)
                {
                    return candidates[_random.Next(candidates.Count)];
                }

                double roll = _random.NextDouble() * totalWeight;
                double current = 0.0;

                foreach (var candidate in candidates)
                {
                    current += candidate.Weight * candidate.Weight;

                    if (current >= roll)
                        return candidate;
                }

                return candidates[^1];
            }

            private bool RandomBool(double probability)
            {
                probability = Math.Clamp(probability, 0.0, 1.0);
                return _random.NextDouble() < probability;
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
                    int x = _random.Next(rect.LeftUpCornerX, rect.LeftUpCornerX + rect.RowsNumber);
                    int y = _random.Next(rect.LeftUpCornerY, rect.LeftUpCornerY + rect.ColsNumber);

                    for (int i = rect.LeftUpCornerX; i < rect.LeftUpCornerX + rect.RowsNumber; i++)
                    {
                        for (int j = rect.LeftUpCornerY; j < rect.LeftUpCornerY + rect.ColsNumber; j++)
                        {
                            result[i][j] = (i == x && j == y)
                                ? rect.Area.ToString()
                                : string.Empty;
                        }
                    }
                }

                return result;
            }

            private readonly record struct SplitCandidate(
                bool SplitUpDown,
                int Axis,
                double Weight,
                int AreaA,
                int AreaB,
                int RowsA,
                int ColsA,
                int RowsB,
                int ColsB
            );
        }
    }
    public class Rect
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
                LeftUpCornerX = LeftUpCornerX,
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