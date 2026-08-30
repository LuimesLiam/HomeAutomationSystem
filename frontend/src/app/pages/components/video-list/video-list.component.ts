import { Component, Inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { VideoService } from '../../../service/video.service';

@Component({
    selector: 'app-video-list',
    templateUrl: './video-list.component.html',
    styleUrls: ['./video-list.component.scss'],
    standalone: false
})
export class VideoListComponent implements OnInit {
  readonly videos = signal<any[]>([]);
  readonly selectedVideo = signal<string | null>(null);

  constructor(
    private videoService: VideoService,
    @Inject(PLATFORM_ID) private platformId: Object) {}

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.loadVideos();
  }

  loadVideos(): void {
    this.videoService.getMovies().subscribe((data) => {
      this.videos.set(Object.values(data).flat());
    });
  }

  syncMovies(): void {
    this.videoService.syncMovies().subscribe(() => {
      this.loadVideos();
    });
  }

  playVideo(videoUrl: string): void {
    this.selectedVideo.set(videoUrl);
    console.log(videoUrl);
  }

  closeVideo(): void {
    this.selectedVideo.set(null);
  }

  playVideoOnServer(videoName: string): void {
    this.videoService.playServerVideo(`${videoName}.mp4`);
  }
}
