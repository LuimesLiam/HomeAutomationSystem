import { TestBed } from '@angular/core/testing';
import { AppModule } from '../app.module';
import { WelcomePageComponent } from './welcome-page.component';

describe('Navigation', () => {
  it('expands accessibly and includes the commute page', async () => {
    await TestBed.configureTestingModule({ imports: [AppModule] }).compileComponents();
    const fixture = TestBed.createComponent(WelcomePageComponent);
    fixture.detectChanges();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(button.getAttribute('aria-expanded')).toBe('false');
    button.click();
    fixture.detectChanges();
    expect(button.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelector('a[href="/commute"]')).toBeTruthy();
  });
});
