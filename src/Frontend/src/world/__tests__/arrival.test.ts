import { describe, it, expect } from 'vitest';
import { arrivalDelaySeconds } from '../arrival';

describe('arrivalDelaySeconds', () => {
  it('returns a whole second inside the min..max window', () => {
    expect(arrivalDelaySeconds(0, 2, 8, () => 0)).toBe(2);
    expect(arrivalDelaySeconds(0, 2, 8, () => 0.999999)).toBe(8);
    for (let i = 0; i < 50; i++) {
      const d = arrivalDelaySeconds(0, 2, 8);
      expect(d).toBeGreaterThanOrEqual(2);
      expect(d).toBeLessThanOrEqual(8);
      expect(Number.isInteger(d)).toBe(true);
    }
  });

  it('deep queues push arrivals back by up to 3×', () => {
    // depth 6 → ×2 → 4..16
    expect(arrivalDelaySeconds(6, 2, 8, () => 0)).toBe(4);
    expect(arrivalDelaySeconds(6, 2, 8, () => 0.999999)).toBe(16);
    // depth 20 → capped at ×3 → 6..24
    expect(arrivalDelaySeconds(20, 2, 8, () => 0)).toBe(6);
    expect(arrivalDelaySeconds(20, 2, 8, () => 0.999999)).toBe(24);
  });

  it('never returns less than 1 second', () => {
    expect(arrivalDelaySeconds(20, 1, 3, () => 0)).toBe(3); // 1×3 = 3
    expect(arrivalDelaySeconds(0, 1, 1, () => 0.5)).toBe(1);
  });
});