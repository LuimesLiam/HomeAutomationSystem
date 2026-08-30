import { Component, signal } from '@angular/core';

@Component({
  selector: 'app-welcome-page',
  templateUrl: './welcome-page.component.html',
  styleUrl: './welcome-page.component.scss',
  standalone: false
})
export class WelcomePageComponent {
  readonly isCollapsed = signal(true);

  toggleSidebar(): void {
    this.isCollapsed.update((collapsed) => !collapsed);
  }
}
