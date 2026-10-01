import { Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pagination',
  standalone: true,
  template: `
    @if (totalPages() > 1) {
      <div class="pagination">
        <button class="btn ghost small" type="button" [disabled]="page() <= 1" (click)="pageChange.emit(page() - 1)">
          ← Poprzednia
        </button>
        <span class="small-muted">Strona {{ page() }} z {{ totalPages() }} ({{ totalCount() }} wyników)</span>
        <button class="btn ghost small" type="button" [disabled]="page() >= totalPages()" (click)="pageChange.emit(page() + 1)">
          Następna →
        </button>
      </div>
    }
  `,
  styles: [
    `.pagination{display:flex;align-items:center;gap:12px;justify-content:center;padding:14px 0}`
  ]
})
export class PaginationComponent {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalCount = input.required<number>();
  readonly pageChange = output<number>();

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));
}
