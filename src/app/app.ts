import { Component } from '@angular/core';
import { NoteEditorComponent } from './features/notes/components/note-editor/note-editor';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [NoteEditorComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {}
