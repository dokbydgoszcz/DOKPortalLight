import { Injectable } from '@angular/core';

/** Zdjęcia osób mogą mieć najwyżej 5 MB (limit serwera) – większe zmniejszamy w przeglądarce. */
export const PHOTO_MAX_BYTES = 4 * 1024 * 1024;
export const PHOTO_MAX_SIDE = 1600;

/**
 * Zmniejsza zdjęcie (np. 8 MB z telefonu) do rozsądnego rozmiaru: dłuższy bok najwyżej 1600 px, JPG.
 * Małe JPG/PNG/WEBP zostawia bez zmian.
 */
@Injectable({ providedIn: 'root' })
export class ImageShrinker {
  async shrink(blob: Blob, maxSide = PHOTO_MAX_SIDE, maxBytes = PHOTO_MAX_BYTES): Promise<Blob> {
    const supported = ['image/jpeg', 'image/png', 'image/webp'].includes(blob.type);
    if (supported && blob.size <= maxBytes) return blob;
    const bitmap = await createImageBitmap(blob);
    const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(bitmap.width * scale);
    canvas.height = Math.round(bitmap.height * scale);
    const context = canvas.getContext('2d');
    if (!context) throw new Error('Przeglądarka nie potrafi zmniejszyć zdjęcia.');
    context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    bitmap.close?.();
    return new Promise<Blob>((resolve, reject) =>
      canvas.toBlob(result => (result ? resolve(result) : reject(new Error('Nie udało się zmniejszyć zdjęcia.'))), 'image/jpeg', 0.85)
    );
  }
}
