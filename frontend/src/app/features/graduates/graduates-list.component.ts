import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { GraduatesService } from './graduates.service';
import { DokCase } from '../dok-cases/dok-case.model';
import { ToastService } from '../../core/notifications/toast.service';
import { PaginationComponent } from '../../shared/pagination.component';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-graduates-list',
  standalone: true,
  imports: [DatePipe, PaginationComponent],
  templateUrl: './graduates-list.component.html',
  styleUrl: './graduates-list.component.scss'
})
export class GraduatesListComponent implements OnInit {
  readonly graduates = signal<DokCase[]>([]);
  readonly query = signal('');
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = PAGE_SIZE;

  constructor(
    private readonly graduatesService: GraduatesService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.graduatesService.search(this.query(), this.page(), this.pageSize).subscribe({
      next: result => {
        this.graduates.set(result.items);
        this.totalCount.set(result.totalCount);
      },
      error: () => this.toast.error('Nie udało się wczytać listy absolwentów.')
    });
  }

  onSearch(value: string): void {
    this.query.set(value);
    this.page.set(1);
    this.load();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }
}
