import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CamFeedComponent } from './cam-feed.component';

describe('CamFeedComponent', () => {
  let component: CamFeedComponent;
  let fixture: ComponentFixture<CamFeedComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [CamFeedComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(CamFeedComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
