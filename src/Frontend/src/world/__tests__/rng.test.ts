import { describe, it, expect } from 'vitest';
import { mulberry32, intBetween, pick, shuffle } from '../rng';

describe('mulberry32', () => {
  it('is deterministic for the same seed', () => {
    const a = mulberry32(1234);
    const b = mulberry32(1234);
    for (let i = 0; i < 100; i++) {
      expect(a()).toBe(b());
    }
  });

  it('differs for different seeds', () => {
    const a = mulberry32(1);
    const b = mulberry32(2);
    const seqA = Array.from({ length: 10 }, () => a());
    const seqB = Array.from({ length: 10 }, () => b());
    expect(seqA).not.toEqual(seqB);
  });

  it('produces values in [0, 1)', () => {
    const rnd = mulberry32(42);
    for (let i = 0; i < 1000; i++) {
      const v = rnd();
      expect(v).toBeGreaterThanOrEqual(0);
      expect(v).toBeLessThan(1);
    }
  });
});

describe('intBetween', () => {
  it('is inclusive on both ends', () => {
    const rnd = mulberry32(7);
    for (let i = 0; i < 500; i++) {
      const v = intBetween(rnd, 3, 3);
      expect(v).toBe(3);
    }
  });

  it('stays within bounds', () => {
    const rnd = mulberry32(99);
    for (let i = 0; i < 1000; i++) {
      const v = intBetween(rnd, 5, 12);
      const isInt = Number.isInteger(v);
      expect(v).toBeGreaterThanOrEqual(5);
      expect(v).toBeLessThanOrEqual(12);
      expect(isInt).toBe(true);
    }
  });
});

describe('pick', () => {
  it('returns an element of the array', () => {
    const rnd = mulberry32(99);
    const items = ['a', 'b', 'c'];
    for (let i = 0; i < 50; i++) {
      const chosen = pick(rnd, items);
      expect(items).toContain(chosen);
    }
  });
});

describe('shuffle', () => {
  it('is deterministic for the same seed', () => {
    const items = [1, 2, 3, 4, 5, 6, 7, 8];
    const a = shuffle(mulberry32(5), items);
    const b = shuffle(mulberry32(5), items);
    expect(a).toEqual(b);
  });

  it('is a permutation of the input', () => {
    const items = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
    const result = shuffle(mulberry32(11), items);
    const sorted = [...result].sort((x, y) => x - y);
    expect(sorted).toEqual(items);
  });
});