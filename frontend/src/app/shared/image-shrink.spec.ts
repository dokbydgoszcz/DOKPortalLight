import { afterEach, describe, expect, it, vi } from 'vitest';
import { ImageShrinker } from './image-shrink';

describe('ImageShrinker', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('leaves small photos as they are', async () => {
    const blob = new Blob(['x'], { type: 'image/png' });
    expect(await new ImageShrinker().shrink(blob)).toBe(blob);
  });

  function stubCanvas(result: Blob | null, context: object | null = { drawImage: vi.fn() }) {
    const close = vi.fn();
    vi.stubGlobal('createImageBitmap', vi.fn(async () => ({ width: 4000, height: 2000, close })));
    const canvas = { width: 0, height: 0, getContext: vi.fn(() => context), toBlob: vi.fn((cb: (b: Blob | null) => void) => cb(result)) };
    const create = vi.spyOn(document, 'createElement').mockReturnValue(canvas as unknown as HTMLElement);
    return { canvas, close, create };
  }

  it('scales big or unusual photos down to a JPG', async () => {
    const out = new Blob(['small'], { type: 'image/jpeg' });
    const { canvas, close, create } = stubCanvas(out);
    expect(await new ImageShrinker().shrink(new Blob(['x'], { type: 'image/heic' }))).toBe(out);
    expect([canvas.width, canvas.height]).toEqual([1600, 800]);
    expect(canvas.toBlob).toHaveBeenCalledWith(expect.any(Function), 'image/jpeg', 0.85);
    expect(close).toHaveBeenCalled();
    create.mockRestore();
  });

  it('reports when the browser cannot do it', async () => {
    let stub = stubCanvas(null);
    await expect(new ImageShrinker().shrink(new Blob(['xx'], { type: 'image/jpeg' }), 100, 1)).rejects.toThrow('Nie udało się zmniejszyć zdjęcia.');
    stub.create.mockRestore();
    stub = stubCanvas(null, null);
    await expect(new ImageShrinker().shrink(new Blob(['xx'], { type: 'image/jpeg' }), 100, 1)).rejects.toThrow('Przeglądarka nie potrafi zmniejszyć zdjęcia.');
    stub.create.mockRestore();
  });
});
