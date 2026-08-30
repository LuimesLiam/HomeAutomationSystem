import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CamFeedComponent } from './pages/components/cam-feed/cam-feed.component';
import { VideoListComponent } from './pages/components/video-list/video-list.component';
import { MoviePlayerComponent } from './pages/components/movie-player/movie-player.component';
import { MoviesTableComponent } from './pages/components/movies-table/movies-table.component';
import { TvTableComponent } from './pages/components/tv-table/tv-table.component';
import { SettingsComponent } from './pages/components/settings/settings.component';
import { AiChatComponent } from './pages/components/ai-chat/ai-chat.component';
import { ExpensesComponent } from './pages/components/expenses/expenses.component';

const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'movie-player' },
  { path: 'video-view', component: VideoListComponent },
  { path: 'web-cam-feed', component: CamFeedComponent },
  { path: 'movie-player', component: MoviePlayerComponent },
  { path: 'movies-table', component: MoviesTableComponent },
  { path: 'tv-table', component: TvTableComponent },
  { path: 'ai-chat', component: AiChatComponent },
  { path: 'expenses', component: ExpensesComponent },
  { path: 'settings', component: SettingsComponent },
  { path: '**', redirectTo: 'movie-player' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
