import React, { memo, useCallback, useMemo, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { CheckCircle2, Eraser, Lightbulb, RotateCcw, Sparkles } from 'lucide-react';
import './styles.css';

const SIZES = [
  { label: 'Mała 6 × 6', rows: 6, cols: 6 },
  { label: 'Średnia 8 × 8', rows: 8, cols: 8 },
  { label: 'Duża 10 × 10', rows: 10, cols: 10 },
  { label: 'Ekspercka 12 × 12', rows: 12, cols: 12 },
];

const DIFFICULTIES = {
  calm: { label: 'Spokojny', targetArea: 4, splitBias: 0.52, clueDensity: 1, hint: 'mniej, większych prostokątów' },
  focus: { label: 'Skupienie', targetArea: 6, splitBias: 0.62, clueDensity: 1, hint: 'zrównoważone dzielenie planszy' },
  deep: { label: 'Głębokie myślenie', targetArea: 8, splitBias: 0.72, clueDensity: 1, hint: 'więcej małych i podchwytliwych pól' },
};

const makeId = () => Math.random().toString(36).slice(2, 9);
const rand = (min, max) => Math.floor(Math.random() * (max - min + 1)) + min;
const areaOf = (r) => r.w * r.h;
const contains = (r, row, col) => row >= r.y && row < r.y + r.h && col >= r.x && col < r.x + r.w;
const normalizeSelection = (a, b) => ({
  x: Math.min(a.col, b.col),
  y: Math.min(a.row, b.row),
  w: Math.abs(a.col - b.col) + 1,
  h: Math.abs(a.row - b.row) + 1,
});

function splitRect(rect, difficulty) {
  const canVertical = rect.w >= 2;
  const canHorizontal = rect.h >= 2;
  if (!canVertical && !canHorizontal) return [rect];

  const preferVertical = rect.w > rect.h ? true : rect.h > rect.w ? false : Math.random() > 0.5;
  const vertical = canVertical && (!canHorizontal || preferVertical);

  if (vertical) {
    const cut = rand(1, rect.w - 1);
    return [
      { ...rect, w: cut },
      { x: rect.x + cut, y: rect.y, w: rect.w - cut, h: rect.h },
    ];
  }

  const cut = rand(1, rect.h - 1);
  return [
    { ...rect, h: cut },
    { x: rect.x, y: rect.y + cut, w: rect.w, h: rect.h - cut },
  ];
}

function generatePuzzle(rows, cols, difficultyKey) {
  const difficulty = DIFFICULTIES[difficultyKey];
  let regions = [{ x: 0, y: 0, w: cols, h: rows }];
  let safety = 0;

  while (safety++ < 500) {
    const candidates = regions
      .map((region, index) => ({ region, index, score: areaOf(region) + Math.random() * difficulty.targetArea }))
      .filter(({ region }) => areaOf(region) > difficulty.targetArea || Math.random() < difficulty.splitBias)
      .sort((a, b) => b.score - a.score);

    if (!candidates.length || regions.length >= Math.ceil((rows * cols) / Math.max(3, difficulty.targetArea - 1))) break;

    const { region, index } = candidates[0];
    const next = splitRect(region, difficulty);
    if (next.length === 1) break;
    regions = [...regions.slice(0, index), ...next, ...regions.slice(index + 1)];
  }

  const solution = regions.map((region, index) => {
    const clueRow = rand(region.y, region.y + region.h - 1);
    const clueCol = rand(region.x, region.x + region.w - 1);
    return { ...region, id: `r${index}`, area: areaOf(region), clueRow, clueCol };
  });

  return {
    id: makeId(),
    rows,
    cols,
    difficultyKey,
    solution,
    clues: solution.map(({ id, area, clueRow, clueCol }) => ({ id, area, row: clueRow, col: clueCol })),
  };
}

function regionProblems(region, puzzle) {
  const clues = puzzle.clues.filter((clue) => contains(region, clue.row, clue.col));
  const area = areaOf(region);

  if (clues.length !== 1) return 'Każdy prostokąt musi zawierać dokładnie jedną liczbę.';
  if (clues[0].area !== area) return `Ten prostokąt ma pole ${area}, a liczba wymaga pola ${clues[0].area}.`;
  return null;
}

function validateRegions(regions, puzzle) {
  const occupied = new Map();
  const issues = [];

  for (const region of regions) {
    const problem = regionProblems(region, puzzle);
    if (problem) issues.push(problem);

    for (let y = region.y; y < region.y + region.h; y += 1) {
      for (let x = region.x; x < region.x + region.w; x += 1) {
        const key = `${y}:${x}`;
        if (occupied.has(key)) issues.push('Prostokąty nie mogą na siebie nachodzić.');
        occupied.set(key, region.id);
      }
    }
  }

  if (occupied.size !== puzzle.rows * puzzle.cols) issues.push('Cała plansza musi być przykryta prostokątami.');
  return [...new Set(issues)];
}

const Cell = memo(function Cell({ row, col, clue, state, onPointerDown, onPointerEnter, onPointerUp }) {
  return (
    <button
      className={`cell ${state}`}
      onPointerDown={() => onPointerDown(row, col)}
      onPointerEnter={(event) => event.buttons === 1 && onPointerEnter(row, col)}
      onPointerUp={() => onPointerUp(row, col)}
      aria-label={clue ? `Pole z liczbą ${clue.area}` : 'Puste pole'}
    >
      {clue && <span className="clue">{clue.area}</span>}
    </button>
  );
});

function App() {
  const [sizeIndex, setSizeIndex] = useState(1);
  const [difficultyKey, setDifficultyKey] = useState('focus');
  const [puzzle, setPuzzle] = useState(() => generatePuzzle(8, 8, 'focus'));
  const [regions, setRegions] = useState([]);
  const [dragStart, setDragStart] = useState(null);
  const [dragEnd, setDragEnd] = useState(null);
  const [message, setMessage] = useState('Zaznacz prostokąt tak, aby zawierał jedną liczbę i miał dokładnie takie pole.');
  const [showHint, setShowHint] = useState(false);

  const clueMap = useMemo(() => new Map(puzzle.clues.map((clue) => [`${clue.row}:${clue.col}`, clue])), [puzzle]);
  const preview = dragStart && dragEnd ? normalizeSelection(dragStart, dragEnd) : null;
  const issues = useMemo(() => validateRegions(regions, puzzle), [regions, puzzle]);
  const solved = issues.length === 0 && regions.length > 0;

  const selectedByCell = useMemo(() => {
    const map = new Map();
    regions.forEach((region) => {
      const problem = regionProblems(region, puzzle);
      for (let y = region.y; y < region.y + region.h; y += 1) {
        for (let x = region.x; x < region.x + region.w; x += 1) {
          map.set(`${y}:${x}`, problem ? 'marked invalid' : 'marked valid');
        }
      }
    });
    if (preview) {
      for (let y = preview.y; y < preview.y + preview.h; y += 1) {
        for (let x = preview.x; x < preview.x + preview.w; x += 1) map.set(`${y}:${x}`, 'preview');
      }
    }
    return map;
  }, [regions, preview, puzzle]);

  const newGame = useCallback(() => {
    const size = SIZES[sizeIndex];
    setPuzzle(generatePuzzle(size.rows, size.cols, difficultyKey));
    setRegions([]);
    setDragStart(null);
    setDragEnd(null);
    setShowHint(false);
    setMessage('Nowa plansza gotowa. Oddychaj spokojnie i szukaj prostokątów po największych liczbach.');
  }, [difficultyKey, sizeIndex]);

  const clearBoard = useCallback(() => {
    setRegions([]);
    setMessage('Plansza wyczyszczona. Możesz zacząć układanie od nowa.');
  }, []);

  const removeRegionAt = useCallback((row, col) => {
    setRegions((current) => current.filter((region) => !contains(region, row, col)));
  }, []);

  const onPointerDown = useCallback((row, col) => {
    removeRegionAt(row, col);
    const point = { row, col };
    setDragStart(point);
    setDragEnd(point);
  }, [removeRegionAt]);

  const onPointerEnter = useCallback((row, col) => setDragEnd({ row, col }), []);

  const onPointerUp = useCallback((row, col) => {
    if (!dragStart) return;
    const rect = normalizeSelection(dragStart, { row, col });
    const id = makeId();
    setRegions((current) => [...current, { ...rect, id }]);
    setDragStart(null);
    setDragEnd(null);
  }, [dragStart]);

  const check = useCallback(() => {
    const nextIssues = validateRegions(regions, puzzle);
    if (!nextIssues.length) {
      setMessage('Świetnie — plansza rozwiązana poprawnie. To było czyste Shikaku.');
      return;
    }
    setMessage(nextIssues[0]);
  }, [regions, puzzle]);

  const revealOne = useCallback(() => {
    const missing = puzzle.solution.find((solutionRegion) => !regions.some((region) => {
      return region.x === solutionRegion.x && region.y === solutionRegion.y && region.w === solutionRegion.w && region.h === solutionRegion.h;
    }));
    if (!missing) return;
    setRegions((current) => [...current.filter((region) => !puzzle.solution.some((s) => s.id === missing.id && contains(region, s.clueRow, s.clueCol))), { ...missing, id: makeId() }]);
    setMessage('Dodałem jeden poprawny prostokąt. Potraktuj to jak delikatną podpowiedź, nie spoiler całej planszy.');
  }, [puzzle, regions]);

  const cells = useMemo(() => {
    const items = [];
    for (let row = 0; row < puzzle.rows; row += 1) {
      for (let col = 0; col < puzzle.cols; col += 1) {
        const clue = clueMap.get(`${row}:${col}`);
        const state = selectedByCell.get(`${row}:${col}`) ?? '';
        items.push(
          <Cell
            key={`${row}:${col}`}
            row={row}
            col={col}
            clue={clue}
            state={state}
            onPointerDown={onPointerDown}
            onPointerEnter={onPointerEnter}
            onPointerUp={onPointerUp}
          />
        );
      }
    }
    return items;
  }, [puzzle, clueMap, selectedByCell, onPointerDown, onPointerEnter, onPointerUp]);

  return (
    <main className="app-shell">
      <section className="hero">
        <div>
          <p className="eyebrow"><Sparkles size={16} /> Karrolinaku</p>
          <h1>Relaksujące Shikaku, które nadal zmusza mózg do pracy.</h1>
          <p className="lead">Dziel planszę na prostokąty. Każdy prostokąt musi zawierać dokładnie jedną liczbę, a jego pole musi być równe tej liczbie.</p>
        </div>
        <div className="panel settings-panel">
          <label>
            Rozmiar planszy
            <select value={sizeIndex} onChange={(event) => setSizeIndex(Number(event.target.value))}>
              {SIZES.map((size, index) => <option key={size.label} value={index}>{size.label}</option>)}
            </select>
          </label>
          <label>
            Poziom trudności
            <select value={difficultyKey} onChange={(event) => setDifficultyKey(event.target.value)}>
              {Object.entries(DIFFICULTIES).map(([key, difficulty]) => <option key={key} value={key}>{difficulty.label}</option>)}
            </select>
          </label>
          <button className="primary" onClick={newGame}><RotateCcw size={18} /> Nowa plansza</button>
        </div>
      </section>

      <section className="game-layout">
        <aside className="panel side-panel">
          <h2>Tryb: {DIFFICULTIES[puzzle.difficultyKey].label}</h2>
          <p>{DIFFICULTIES[puzzle.difficultyKey].hint}. Plansza: {puzzle.rows} × {puzzle.cols}.</p>
          <div className="stats">
            <span><strong>{puzzle.clues.length}</strong> liczb</span>
            <span><strong>{regions.length}</strong> zaznaczeń</span>
            <span><strong>{issues.length}</strong> problemów</span>
          </div>
          <div className="actions">
            <button onClick={check}><CheckCircle2 size={18} /> Sprawdź</button>
            <button onClick={revealOne}><Lightbulb size={18} /> Podpowiedź</button>
            <button onClick={clearBoard}><Eraser size={18} /> Wyczyść</button>
          </div>
          <button className="text-button" onClick={() => setShowHint((value) => !value)}>{showHint ? 'Ukryj zasady' : 'Pokaż spokojną strategię'}</button>
          {showHint && <p className="hint">Zacznij od dużych liczb przy krawędziach. Potem szukaj pól, które mogą mieć tylko jeden możliwy kształt, np. 1, liczby pierwsze albo liczby przy narożnikach.</p>}
          <p className={`message ${solved ? 'success' : ''}`}>{message}</p>
        </aside>

        <section className="board-wrap" aria-label="Plansza gry Shikaku">
          <div className="board" style={{ '--rows': puzzle.rows, '--cols': puzzle.cols }}>
            {cells}
          </div>
        </section>
      </section>
    </main>
  );
}

createRoot(document.getElementById('root')).render(<App />);
