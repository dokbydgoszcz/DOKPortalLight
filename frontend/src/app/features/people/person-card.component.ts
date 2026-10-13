import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { PeopleService } from './people.service';
import { PersonOverview } from './person-overview.model';
import { functionText } from './person.model';
import { roleLabel } from '../../shared/role-labels';
import { serverMessage } from '../../shared/http-error';

/** Karta osoby: dane, funkcje i wszystkie powiązania (sprawy, misje, kandydatura, pisma, superwizje, konto). */
@Component({
  selector: 'app-person-card',
  standalone: true,
  imports: [RouterLink, DatePipe],
  templateUrl: './person-card.component.html',
  styleUrl: './person-card.component.scss'
})
export class PersonCardComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly peopleService = inject(PeopleService);
  private readonly title = inject(Title);

  readonly overview = signal<PersonOverview | null>(null);
  readonly error = signal<string | null>(null);
  readonly functionText = functionText;
  readonly roleLabel = roleLabel;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.peopleService.overview(id).subscribe({
      next: overview => {
        this.overview.set(overview);
        this.title.setTitle(`${overview.person.fullName} | DOK Portal`);
      },
      error: err => this.error.set(err?.status === 404 ? 'Nie znaleziono osoby.' : serverMessage(err, 'Nie udało się wczytać karty osoby.'))
    });
  }

  /** Czego brakuje do usunięcia osoby – te same powiązania, które blokują „Usuń”. */
  links(o: PersonOverview): number {
    return (o.dokCases?.length ?? 0) + (o.casesLed?.length ?? 0) + (o.candidate ? 1 : 0) + (o.missions?.length ?? 0)
      + (o.parishAssignments?.length ?? 0) + (o.formatorFunctions?.length ?? 0) + (o.documents?.length ?? 0) + (o.account ? 1 : 0);
  }
}
