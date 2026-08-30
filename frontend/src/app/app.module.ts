import { NgModule } from '@angular/core';
import { BrowserModule, provideClientHydration } from '@angular/platform-browser';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { CommonModule } from '@angular/common';
import { provideHttpClient, withFetch } from '@angular/common/http';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { WelcomePageComponent } from './welcome-page/welcome-page.component';
import { CamFeedComponent } from './pages/components/cam-feed/cam-feed.component';
import { VideoListComponent } from './pages/components/video-list/video-list.component';
import { MoviePlayerComponent } from './pages/components/movie-player/movie-player.component';
import { MoviesListComponent } from './pages/components/movies-list/movies-list.component';
import { TvListComponent } from './pages/components/tv-list/tv-list.component';
import { SeriesListComponent } from './pages/components/series-list/series-list.component';
import { SeriesDetailComponent } from './pages/components/series-detail/series-detail.component';
import { MoviesTableComponent } from './pages/components/movies-table/movies-table.component';
import { TvTableComponent } from './pages/components/tv-table/tv-table.component';
import { SettingsComponent } from './pages/components/settings/settings.component';
import { AiChatComponent } from './pages/components/ai-chat/ai-chat.component';
import { ExpensesComponent } from './pages/components/expenses/expenses.component';

import { providePrimeNG } from 'primeng/config';
import Aura from '@primeuix/themes/aura';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { ProgressBarModule } from 'primeng/progressbar';
import { TabsModule } from 'primeng/tabs';
import { TableModule } from 'primeng/table';
import { PaginatorModule } from 'primeng/paginator';
import { DatePickerModule } from 'primeng/datepicker';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

@NgModule({
  declarations: [
    AppComponent,
    WelcomePageComponent,
    CamFeedComponent,
    VideoListComponent,
    MoviePlayerComponent,
    MoviesListComponent,
    TvListComponent,
    SeriesListComponent,
    SeriesDetailComponent,
    MoviesTableComponent,
    TvTableComponent,
    SettingsComponent,
    AiChatComponent,
    ExpensesComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    BrowserAnimationsModule,
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    ProgressSpinnerModule,
    ProgressBarModule,
    TabsModule,
    TableModule,
    PaginatorModule,
    DatePickerModule,
    ToastModule
  ],
  providers: [
    MessageService,
    provideHttpClient(withFetch()),
    provideClientHydration(),
    providePrimeNG({
      ripple: true,
      theme: {
        preset: Aura
      }
    })
  ],
  bootstrap: [AppComponent]
})
export class AppModule {}
