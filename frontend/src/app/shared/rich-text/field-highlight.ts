import { Extension } from '@tiptap/core';
import { Node as ProseMirrorNode } from '@tiptap/pm/model';
import { Plugin, PluginKey } from '@tiptap/pm/state';
import { Decoration, DecorationSet } from '@tiptap/pm/view';

/** Rodzaj pola {{…}} w treści: znane, do wpisania przy generowaniu, forma zależna od płci albo nieznane (literówka). */
export type FieldKind = 'known' | 'input' | 'gender' | 'unknown';
export type FieldClassifier = (token: string) => FieldKind;

export const fieldHighlightKey = new PluginKey<DecorationSet>('fieldHighlight');

const TITLES: Record<FieldKind, string> = {
  known: 'Pole – portal wstawi tu dane',
  input: 'Pole do wpisania – portal zapyta o nie przy generowaniu pisma',
  gender: 'Forma zależna od płci osoby',
  unknown: 'Nieznane pole – sprawdź pisownię albo wstaw je z listy'
};

/** Pełne pole {{…}} albo otwarte „{{” bez zamknięcia do końca akapitu. */
const FIELD = /\{\{\s*([^{}]*?)\s*\}\}|\{\{[^{}]*$/g;

/**
 * Podświetla pola w akapicie. Pole może być pocięte formatowaniem (pół pogrubione), więc szukamy w tekście
 * całego bloku, a nie pojedynczych fragmentów.
 */
export function fieldDecorations(doc: ProseMirrorNode, classify: FieldClassifier): DecorationSet {
  const decorations: Decoration[] = [];
  doc.descendants((node, pos) => {
    if (!node.isTextblock) return true;
    // Znak zastępczy dla węzłów bez tekstu (np. twardy enter), żeby pozycje w tekście zgadzały się z dokumentem.
    const text = node.textBetween(0, node.content.size, undefined, '￼');
    for (const match of text.matchAll(FIELD)) {
      const closed = match[0].endsWith('}}');
      const kind: FieldKind = closed && match[1] ? classify(match[1]) : 'unknown';
      const from = pos + 1 + match.index;
      decorations.push(Decoration.inline(from, from + match[0].length, {
        class: `letter-field letter-field-${kind}`,
        title: closed ? TITLES[kind] : 'Pole nie jest zamknięte – brakuje „}}”'
      }));
    }
    return false;
  });
  return DecorationSet.create(doc, decorations);
}

/** Rozszerzenie TipTap: kolorowe tło pól {{…}}; nieznane (literówki) na czerwono. Nie zmienia treści dokumentu. */
export const FieldHighlight = Extension.create<{ classify: () => FieldClassifier }>({
  name: 'fieldHighlight',

  addOptions() {
    return { classify: () => () => 'known' };
  },

  addProseMirrorPlugins() {
    const classify = this.options.classify;
    return [
      new Plugin<DecorationSet>({
        key: fieldHighlightKey,
        state: {
          init: (_config, state) => fieldDecorations(state.doc, classify()),
          // Przeliczamy po zmianie treści albo na żądanie (zmieniła się lista znanych pól).
          apply: (tr, old) => (tr.docChanged || tr.getMeta(fieldHighlightKey) ? fieldDecorations(tr.doc, classify()) : old)
        },
        props: {
          decorations(state) {
            return fieldHighlightKey.getState(state);
          }
        }
      })
    ];
  }
});
