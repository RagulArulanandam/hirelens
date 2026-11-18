import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { ToastService } from './core/http';
import { ThemeService } from './core/theme.service';
import { LogoComponent } from './shared/ui';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LogoComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  auth = inject(AuthService);
  theme = inject(ThemeService);
  toast = inject(ToastService);
}
