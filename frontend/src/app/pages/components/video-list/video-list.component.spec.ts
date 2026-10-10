import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AppModule } from '../../../app.module';
import { VideoService } from '../../../service/video.service';
import { VideoListComponent } from './video-list.component';

describe('Video library', () => {
  it('loads genre groups and opens and closes browser playback', async () => {
    await TestBed.configureTestingModule({ imports: [AppModule], providers: [
      { provide: VideoService, useValue: { getMovies: () => of({ Drama: [{ name: 'Synthetic movie', videoUrl: '/test.mp4' }] }) } }
    ] }).compileComponents();
    const fixture = TestBed.createComponent(VideoListComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Synthetic movie');
    fixture.componentInstance.playVideo('/test.mp4');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('video').getAttribute('src')).toBe('/test.mp4');
    fixture.componentInstance.closeVideo();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('video')).toBeNull();
  });
});
