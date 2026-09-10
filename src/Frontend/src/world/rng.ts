// Seeded PRNG (mulberry32) + small helpers so world generation is fully deterministic.
export type Rnd = () => number;

export function mulberry32(seed: number): Rnd {
  let a = seed >>> 0;
  return () => {
    a |= 0;
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// Inclusive on both ends.
export function intBetween(rnd: Rnd, min: number, max: number): number {
  return min + Math.floor(rnd() * (max - min + 1));
}

export function pick<T>(rnd: Rnd, items: readonly T[]): T {
  return items[intBetween(rnd, 0, items.length - 1)];
}

// Fisher-Yates; returns a new array, input untouched.
export function shuffle<T>(rnd: Rnd, items: readonly T[]): T[] {
  const out = [...items];
  for (let i = out.length - 1; i > 0; i--) {
    const j = intBetween(rnd, 0, i);
    const tmp = out[i];
    out[i] = out[j];
    out[j] = tmp;
  }
  return out;
}