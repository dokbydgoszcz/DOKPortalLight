import { afterEach, describe, expect, it, vi } from 'vitest';
import { playSound } from './sound';

describe('playSound', () => {
  afterEach(() => vi.restoreAllMocks());

  it('plays the file and ignores a refused playback', async () => {
    const play = vi.spyOn(HTMLMediaElement.prototype, 'play').mockRejectedValue(new Error('NotAllowedError'));
    playSound('/sounds/x.mp3');
    expect(play).toHaveBeenCalled();
    await Promise.resolve();
  });

  it('stays quiet when audio is not available at all', () => {
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockImplementation(() => {
      throw new Error('brak');
    });
    expect(() => playSound('/sounds/x.mp3')).not.toThrow();
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockReturnValue(undefined as unknown as Promise<void>);
    expect(() => playSound('/sounds/x.mp3')).not.toThrow();
  });
});
