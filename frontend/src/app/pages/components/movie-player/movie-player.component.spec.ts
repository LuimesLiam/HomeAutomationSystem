import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AppModule } from '../../../app.module';
import { VideoService } from '../../../service/video.service';
import { MoviePlayerComponent } from './movie-player.component';

describe('Media browser', () => {
  it('filters loaded movies by title without making another request', async () => {
    vi.useFakeTimers();
    vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
    try {
      const getMoviesSample = vi.fn(() => of({ Drama: [{ title: 'Synthetic title' }, { title: 'Other movie' }] }));
      await TestBed.configureTestingModule({ imports: [AppModule], providers: [
        { provide: VideoService, useValue: { getMoviesSample, getSeriesSample: () => of({}) } }
      ] }).compileComponents();
      const fixture = TestBed.createComponent(MoviePlayerComponent);
      fixture.detectChanges();
      expect(fixture.componentInstance.loaded).toBe(true);
      fixture.componentInstance.searchQuery = 'synthetic';
      expect(fixture.componentInstance.filteredMovies['Drama']).toEqual([{ title: 'Synthetic title' }]);
      expect(getMoviesSample).toHaveBeenCalledTimes(1);
      fixture.destroy();
    } finally {
      vi.clearAllTimers();
      vi.useRealTimers();
      vi.unstubAllGlobals();
    }
  });
});
