import { HttpClient } from '@angular/common/http';
import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { buildApiUrl } from '../../../shared/api.constants';

interface CameraTile {
  id: string;
  title: string;
  location: string;
  streamPath: string;
  enabled: boolean;
  loading: boolean;
}

@Component({
    selector: 'app-cam-feed',
    templateUrl: './cam-feed.component.html',
    styleUrl: './cam-feed.component.scss',
    standalone: false
})
export class CamFeedComponent implements OnInit, OnDestroy {
  readonly isFullscreen = signal(false);
  readonly fullscreenCameraId = signal<string | null>(null);
  readonly cameras = signal<CameraTile[]>([
    {
      id: 'cam-1',
      title: 'Front Door',
      location: 'Entryway',
      streamPath: '/camera/stream',
      enabled: true,
      loading: false
    }
  ]);

  constructor(private http: HttpClient) {}

  ngOnInit() {
    if (typeof document !== 'undefined') {
      document.addEventListener('fullscreenchange', this.onFullscreenChange);
    }

    this.http.get<{ feedEnabled: boolean }>(buildApiUrl('/camera/status')).subscribe({
      next: (status) => this.patchCamera('cam-1', { enabled: status.feedEnabled, loading: false }),
      error: () => {
        // Keep the configured initial state when the camera service is unavailable.
      }
    });
  }

  ngOnDestroy() {
    if (typeof document !== 'undefined') {
      document.removeEventListener('fullscreenchange', this.onFullscreenChange);
    }
  }

  onFullscreenChange = () => {
    this.isFullscreen.set(!!document.fullscreenElement);
    const fullscreenElement = document.fullscreenElement as HTMLElement | null;
    this.fullscreenCameraId.set(fullscreenElement?.dataset['cameraId'] ?? null);
  };

  toggleFullScreen(event: Event) {
    const frame = (event.currentTarget as HTMLElement).closest('.camera-frame') as HTMLElement | null;

    if (!document.fullscreenElement) {
      frame?.requestFullscreen();
    } else {
      document.exitFullscreen();
    }
  }

  toggleCamera(cameraId: string) {
    const camera = this.cameras().find((item) => item.id === cameraId);
    if (!camera || camera.loading) {
      return;
    }

    const nextEnabledState = !camera.enabled;
    // Remove the MJPEG image immediately when disabling. Browsers otherwise keep
    // showing the last decoded frame while the API waits for the camera process.
    this.patchCamera(cameraId, {
      enabled: nextEnabledState ? camera.enabled : false,
      loading: true
    });

    this.http.post(buildApiUrl(`/camera/${nextEnabledState ? 'enable' : 'disable'}`), {}).subscribe({
      next: () => {
        this.patchCamera(cameraId, { enabled: nextEnabledState, loading: false });
      },
      error: () => {
        this.patchCamera(cameraId, {
          // A failed disable still means the .NET stream gate is closed.
          enabled: false,
          loading: false
        });
      }
    });
  }

  enableAllFeeds() {
    this.cameras().forEach((camera) => {
      if (!camera.enabled && !camera.loading) {
        this.toggleCamera(camera.id);
      }
    });
  }

  disableAllFeeds() {
    this.cameras().forEach((camera) => {
      if (camera.enabled && !camera.loading) {
        this.toggleCamera(camera.id);
      }
    });
  }

  isAnyCameraBusy() {
    return this.cameras().some((camera) => camera.loading);
  }

  hasEnabledFeed() {
    return this.cameras().some((camera) => camera.enabled);
  }

  getActiveCameraCount() {
    return this.cameras().filter((camera) => camera.enabled).length;
  }

  getStreamUrl(camera: CameraTile) {
    return camera.enabled ? buildApiUrl(camera.streamPath) : null;
  }

  private patchCamera(cameraId: string, patch: Partial<CameraTile>) {
    this.cameras.update((cameraList) =>
      cameraList.map((camera) => (camera.id === cameraId ? { ...camera, ...patch } : camera))
    );
  }
}
