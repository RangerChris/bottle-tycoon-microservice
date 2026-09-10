// Visitor arrival pacing, in game-seconds. Ticked by the economy's deposit
// loop (depositTick) so arrivals follow the same pause/speed clock as trucks
// and bottle deposits — never a raw setTimeout.

export const DEFAULT_ARRIVAL_MIN_SEC = 2;
export const DEFAULT_ARRIVAL_MAX_SEC = 8;

// A queue deeper than 4 pushes the next arrival back, up to 3× the base delay.
export function arrivalDelaySeconds(
  queueDepth: number,
  minSec: number = DEFAULT_ARRIVAL_MIN_SEC,
  maxSec: number = DEFAULT_ARRIVAL_MAX_SEC,
  rand: () => number = Math.random,
): number {
  const multiplier = queueDepth > 4 ? Math.min(3, 1 + (queueDepth - 4) * 0.5) : 1;
  const clampedMin = Math.max(1, Math.ceil(minSec * multiplier));
  const clampedMax = Math.max(clampedMin, Math.ceil(maxSec * multiplier));
  return Math.floor(rand() * (clampedMax - clampedMin + 1)) + clampedMin;
}