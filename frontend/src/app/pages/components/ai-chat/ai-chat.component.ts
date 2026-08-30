import { Component, OnInit, SecurityContext, signal } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { marked } from 'marked';
import {
  AiChatMessage,
  AiChatSessionDetail,
  AiChatSessionSummary,
  AiModelSettingsItem,
  VideoService
} from '../../../service/video.service';

type RenderedChatMessage = AiChatMessage & {
  html: SafeHtml;
  timestamp: string;
  modelLabel?: string;
};

@Component({
  selector: 'app-ai-chat',
  templateUrl: './ai-chat.component.html',
  styleUrls: ['./ai-chat.component.scss'],
  standalone: false
})
export class AiChatComponent implements OnInit {
  readonly loadingModels = signal(false);
  readonly loadingSessions = signal(false);
  readonly loadingSessionDetail = signal(false);
  readonly sending = signal(false);
  readonly errorMessage = signal('');
  readonly selectedModelKey = signal('');
  readonly selectedSessionId = signal<number | null>(null);
  readonly models = signal<AiModelSettingsItem[]>([]);
  readonly sessions = signal<AiChatSessionSummary[]>([]);
  readonly messages = signal<RenderedChatMessage[]>([]);

  draftMessage = '';

  constructor(
    private videoService: VideoService,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void {
    marked.setOptions({
      breaks: true,
      gfm: true
    });

    this.loadModels();
    this.loadSessions();
  }

  loadModels(): void {
    this.loadingModels.set(true);
    this.errorMessage.set('');

    this.videoService.getAiSettings().subscribe({
      next: (settings) => {
        const enabledModels = (settings.models ?? []).filter((model) => model.isEnabled);
        this.models.set(enabledModels);

        const defaultModel = enabledModels.find((model) => model.isDefault) ?? enabledModels[0];
        if (!this.selectedModelKey()) {
          this.selectedModelKey.set(defaultModel?.key ?? '');
        }

        this.loadingModels.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load configured models.');
        this.loadingModels.set(false);
      }
    });
  }

  loadSessions(): void {
    this.loadingSessions.set(true);

    this.videoService.getAiChatSessions().subscribe({
      next: (sessions) => {
        this.sessions.set(sessions);
        this.loadingSessions.set(false);

        if (!this.selectedSessionId() && sessions.length > 0) {
          this.openSession(sessions[0].id);
        } else if (sessions.length === 0) {
          this.startFreshChat();
        }
      },
      error: () => {
        this.errorMessage.set('Failed to load chat sessions.');
        this.loadingSessions.set(false);
      }
    });
  }

  openSession(sessionId: number): void {
    this.loadingSessionDetail.set(true);
    this.errorMessage.set('');

    this.videoService.getAiChatSession(sessionId).subscribe({
      next: (session) => {
        this.applySessionDetail(session);
        this.loadingSessionDetail.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load the selected chat session.');
        this.loadingSessionDetail.set(false);
      }
    });
  }

  startFreshChat(): void {
    this.selectedSessionId.set(null);
    this.messages.set([
      this.createRenderedMessage({
        role: 'assistant',
        content: 'Start a new conversation. The first message will create and persist a session automatically.'
      })
    ]);
  }

  async sendMessage(): Promise<void> {
    const content = this.draftMessage.trim();
    if (!content || this.sending()) {
      return;
    }

    const selectedModelKey = this.selectedModelKey();
    if (!selectedModelKey) {
      this.errorMessage.set('Select a model before sending a message.');
      return;
    }

    const selectedModel = this.models().find((model) => model.key === selectedModelKey);
    const userMessage = this.createRenderedMessage({
      role: 'user',
      content,
      modelKey: selectedModelKey,
      modelName: selectedModel?.name,
      providerName: selectedModel?.providerKey
    });

    const pendingAssistant = this.createRenderedMessage({
      role: 'assistant',
      content: '',
      modelKey: selectedModelKey,
      modelName: selectedModel?.name,
      providerName: selectedModel?.providerKey
    });

    this.messages.set([...this.messages(), userMessage, pendingAssistant]);
    this.draftMessage = '';
    this.sending.set(true);
    this.errorMessage.set('');

    try {
      let accumulated = '';

      await this.videoService.streamAiChatMessage(
        this.selectedSessionId(),
        {
          modelKey: selectedModelKey,
          content
        },
        (event) => {
          if (event.type === 'session' && event.session) {
            this.upsertSession(event.session);
            this.selectedSessionId.set(event.session.id);
          }

          if (event.type === 'chunk') {
            accumulated += event.content ?? '';
            pendingAssistant.content = accumulated;
            pendingAssistant.html = this.renderMarkdown(accumulated);
            this.messages.set([...this.messages()]);
          }

          if (event.type === 'completed' && event.message) {
            this.upsertSession(event.session);
            const completed = this.createRenderedMessage(event.message);
            this.messages.set(this.messages().map((message) => message === pendingAssistant ? completed : message));
          }
        });
    } catch {
      this.errorMessage.set('Chat request failed.');
      this.messages.set(this.messages().filter((message) => message !== pendingAssistant));
    } finally {
      this.sending.set(false);
    }
  }

  handleComposerKeydown(event: Event): void {
    const keyboardEvent = event as KeyboardEvent;
    if (keyboardEvent.shiftKey) {
      return;
    }

    keyboardEvent.preventDefault();
    void this.sendMessage();
  }

  trackBySession(_: number, session: AiChatSessionSummary): number {
    return session.id;
  }

  trackByModel(_: number, model: AiModelSettingsItem): number {
    return model.id;
  }

  trackByMessage(index: number, message: RenderedChatMessage): number {
    return message.id ?? index;
  }

  private applySessionDetail(session: AiChatSessionDetail): void {
    this.selectedSessionId.set(session.id);
    this.selectedModelKey.set(session.modelKey);
    this.upsertSession(session);
    this.messages.set(session.messages.map((message) => this.createRenderedMessage(message)));
  }

  private upsertSession(session?: AiChatSessionSummary): void {
    if (!session) {
      return;
    }

    const next = [...this.sessions().filter((item) => item.id !== session.id), session]
      .sort((left, right) => new Date(right.updatedAtUtc).getTime() - new Date(left.updatedAtUtc).getTime());

    this.sessions.set(next);
  }

  private createRenderedMessage(message: AiChatMessage): RenderedChatMessage {
    return {
      ...message,
      html: this.renderMarkdown(message.content ?? ''),
      timestamp: this.formatTimestamp(message.createdAtUtc),
      modelLabel: message.modelName
        ? `${message.modelName}${message.providerName ? ` via ${message.providerName}` : ''}`
        : undefined
    };
  }

  private renderMarkdown(content: string): SafeHtml {
    const rawHtml = marked.parse(content) as string;
    const sanitized = this.sanitizer.sanitize(SecurityContext.HTML, rawHtml) ?? '';
    return this.sanitizer.bypassSecurityTrustHtml(sanitized);
  }

  private formatTimestamp(value?: string): string {
    return value
      ? new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      : new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
}
