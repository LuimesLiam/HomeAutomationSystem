import { TestBed } from '@angular/core/testing';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AppModule } from '../../../app.module';
import { CamFeedComponent } from './cam-feed.component';

describe('Camera controls', () => {
  it('keeps the stream disabled when the provider fails to stop', async () => {
    await TestBed.configureTestingModule({ imports: [AppModule], providers: [provideHttpClientTesting()] }).compileComponents();
    const fixture = TestBed.createComponent(CamFeedComponent);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne(request => request.url.endsWith('/camera/status')).flush({ feedEnabled: true });
    fixture.componentInstance.toggleCamera('cam-1');
    expect(fixture.componentInstance.cameras()[0].enabled).toBe(false);
    http.expectOne(request => request.url.endsWith('/camera/disable')).flush({}, { status: 503, statusText: 'Unavailable' });
    expect(fixture.componentInstance.cameras()[0].enabled).toBe(false);
    expect(fixture.componentInstance.cameras()[0].loading).toBe(false);
    http.verify();
  });
});
