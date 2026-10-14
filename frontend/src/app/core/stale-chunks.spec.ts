import { NavigationError } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { isStaleChunkError, reloadOnStaleChunk } from './stale-chunks';

describe('stale chunks after a deployment', () => {
  it('recognises the browsers’ messages for a missing screen file', () => {
    expect(isStaleChunkError(new TypeError('Failed to fetch dynamically imported module: https://x/chunk-AB.js'))).toBe(true);
    expect(isStaleChunkError(new TypeError('Importing a module script failed.'))).toBe(true);
    expect(isStaleChunkError(new Error('error loading dynamically imported module'))).toBe(true);
    expect(isStaleChunkError(new Error('Cannot read properties of undefined'))).toBe(false);
    expect(isStaleChunkError(undefined)).toBe(false);
  });

  it('reloads the page at the target address only for such errors', () => {
    const assign = vi.fn();
    reloadOnStaleChunk(new NavigationError(1, '/login', new TypeError('Failed to fetch dynamically imported module')), assign);
    reloadOnStaleChunk(new NavigationError(2, '/people', new Error('inny błąd')), assign);

    expect(assign).toHaveBeenCalledTimes(1);
    expect(assign).toHaveBeenCalledWith('/login');
  });
});
