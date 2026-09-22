import { Component, OnInit, signal } from '@angular/core';
import { NameDaysService } from './name-days.service';
import { UpcomingNameDay } from './name-day.model';

@Component({
  selector: 'app-name-days-list',
  standalone: true,
  templateUrl: './name-days-list.component.html',
  styleUrl: './name-days-list.component.scss'
})
export class NameDaysListComponent implements OnInit {
  readonly nameDays = signal<UpcomingNameDay[]>([]);

  constructor(private readonly nameDaysService: NameDaysService) {}

  ngOnInit(): void {
    this.nameDaysService.upcoming(30).subscribe(items => this.nameDays.set(items));
  }
}
