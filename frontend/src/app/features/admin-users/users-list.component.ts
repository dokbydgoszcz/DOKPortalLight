import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UsersService } from './users.service';
import { ALL_ROLES, AppUserAccount, CreateUserValue } from './user.model';

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './users-list.component.html',
  styleUrl: './users-list.component.scss'
})
export class UsersListComponent implements OnInit {
  readonly users = signal<AppUserAccount[]>([]);
  readonly allRoles = ALL_ROLES;
  newUser: CreateUserValue = { email: '', password: '', roles: [] };

  constructor(private readonly usersService: UsersService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.usersService.list().subscribe(users => this.users.set(users));
  }

  createUser(): void {
    this.usersService.create(this.newUser).subscribe(() => {
      this.newUser = { email: '', password: '', roles: [] };
      this.load();
    });
  }

  toggleRole(user: AppUserAccount, role: string, checked: boolean): void {
    const roles = checked ? [...user.roles, role] : user.roles.filter(r => r !== role);
    this.usersService.assignRoles(user.id, roles).subscribe(() => this.load());
  }
}
