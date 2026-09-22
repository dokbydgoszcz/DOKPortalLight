import { Component, OnInit, computed, signal } from '@angular/core';
import { DokCasesService } from '../dok-cases/dok-cases.service';
import { DokCase } from '../dok-cases/dok-case.model';

@Component({
  selector: 'app-graduates-list',
  standalone: true,
  templateUrl: './graduates-list.component.html',
  styleUrl: './graduates-list.component.scss'
})
export class GraduatesListComponent implements OnInit {
  private readonly allCases = signal<DokCase[]>([]);
  readonly graduates = computed(() => this.allCases().filter(c => c.stage === 'Graduate'));

  constructor(private readonly dokCasesService: DokCasesService) {}

  ngOnInit(): void {
    this.dokCasesService.search().subscribe(result => this.allCases.set(result.items));
  }
}
