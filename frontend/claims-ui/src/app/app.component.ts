import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { MatButton } from '@angular/material/button';
import { MatToolbar } from '@angular/material/toolbar';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatSelect, MatSelectChange } from '@angular/material/select';
import { MatOption } from '@angular/material/core';
import { AuthService, DEV_USERS } from './core/services/auth.service';
import { LoadingService } from './core/services/loading.service';
import { UserRole } from './core/models/api.models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, MatButton, MatToolbar, MatProgressBar, MatFormField, MatLabel, MatSelect, MatOption],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent implements OnInit {
  readonly auth = inject(AuthService);
  readonly loading = inject(LoadingService);
  readonly users = DEV_USERS;
  readonly connecting = signal(true);
  private readonly router = inject(Router);
  ngOnInit(): void { this.auth.initialize().subscribe(() => this.connecting.set(false)); }
  retryLogin(): void {
    this.connecting.set(true);
    this.auth.initialize().subscribe(() => {
      this.connecting.set(false);
      if (this.auth.isAuthenticated()) void this.router.navigateByUrl(this.router.url === '/' ? '/claims' : this.router.url);
    });
  }
  switchRole(event: MatSelectChange): void { this.auth.switchRole(event.value as UserRole).subscribe(); }
}
