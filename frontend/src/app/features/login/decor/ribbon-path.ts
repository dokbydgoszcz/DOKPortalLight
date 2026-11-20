/** Parametry jednej wstążki: fala środka i zmienna grubość (efekt skręconego jedwabiu). */
export interface RibbonShape {
  /** Wysokość środka wstążki w układzie 0–600. */
  center: number;
  /** Wychylenie fali. */
  amplitude: number;
  /** Średnia grubość. */
  thickness: number;
  /** O ile grubość „oddycha” (skręt wstążki); mniejsza niż thickness. */
  twist: number;
  /** Przesunięcie fazy (rad), żeby wstążki się nie pokrywały. */
  phase: number;
}

/** Okres fali w jednostkach viewBox – ścieżka ma dwa okresy, więc przesunięcie o jeden daje płynną pętlę. */
export const RIBBON_PERIOD = 1200;

/**
 * Obrys wstążki jako ścieżka SVG o szerokości dwóch okresów. Górna krawędź to fala środka minus połowa grubości,
 * dolna – plus; grubość zmienia się z inną częstotliwością, przez co wstążka wygląda, jakby się skręcała.
 */
export function ribbonPath(shape: RibbonShape, steps = 96): string {
  const width = RIBBON_PERIOD * 2;
  const point = (x: number, side: 1 | -1) => {
    const t = (2 * Math.PI * x) / RIBBON_PERIOD;
    const middle = shape.center + shape.amplitude * Math.sin(t + shape.phase);
    const half = (shape.thickness + shape.twist * Math.sin(2 * t + shape.phase * 1.7)) / 2;
    return `${x.toFixed(1)},${(middle + side * half).toFixed(1)}`;
  };
  const top: string[] = [];
  const bottom: string[] = [];
  for (let i = 0; i <= steps; i++) {
    const x = (width * i) / steps;
    top.push(point(x, -1));
    bottom.unshift(point(x, 1));
  }
  return `M${top.join('L')}L${bottom.join('L')}Z`;
}
