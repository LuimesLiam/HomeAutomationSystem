import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CamFeedComponent } from './pages/components/cam-feed/cam-feed.component';
import { VideoListComponent } from './pages/components/video-list/video-list.component';
import { MoviePlayerComponent } from './pages/components/movie-player/movie-player.component';
import { MoviesTableComponent } from './pages/components/movies-table/movies-table.component';
import { TvTableComponent } from './pages/components/tv-table/tv-table.component';


const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'movie-player' },
  { path: 'video-view', component: VideoListComponent },
  { path: 'web-cam-feed', component: CamFeedComponent },
  { path: 'movie-player', component: MoviePlayerComponent },
  { path: 'movies-table', component: MoviesTableComponent },
  { path: 'tv-table', component: TvTableComponent },
  { path: 'ai-chat', loadComponent: () => import('./pages/components/ai-chat/ai-chat.component').then(module => module.AiChatComponent) },
  { path: 'commute', loadComponent: () => import('./pages/components/commute/commute.component').then(module => module.CommuteComponent) },
  { path: 'expenses', loadComponent: () => import('./pages/components/expenses/expenses.component').then(module => module.ExpensesComponent) },
  { path: 'settings', loadComponent: () => import('./pages/components/settings/settings.component').then(module => module.SettingsComponent) },
  { path: '**', redirectTo: 'movie-player' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
