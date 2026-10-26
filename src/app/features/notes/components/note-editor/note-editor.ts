import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EncryptionService } from '../../services/encryption.service';
import { NotesService } from '../../../../core/services/notes.service';
import { StatusNotification } from '../../models/note.model';

@Component({
  selector: 'app-note-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './note-editor.html',
  styleUrl: './note-editor.css',
})
export class NoteEditorComponent {
  private readonly encryptionService = inject(EncryptionService);
  private readonly notesService = inject(NotesService);

  public static readonly MAX_NOTE_LENGTH = 100000; // 100k characters max

  // Flow State: 'login' (secret key entrance) vs 'editor' (active writing pad)
  readonly activeScreen = signal<'login' | 'editor'>('login');

  // Secret Key State
  readonly secretKey = signal('');
  readonly showSecretKey = signal(false);

  // Active Pad State
  readonly noteText = signal('');
  readonly lookupId = signal<string | null>(null);
  readonly isNewPad = signal(false);

  // Processing & Feedback State
  readonly isProcessing = signal(false);
  readonly status = signal<StatusNotification | null>(null);
  readonly showDeleteConfirm = signal(false);

  // Character limit calculation
  readonly charCount = computed(() => this.noteText().length);
  readonly isOverLimit = computed(() => this.charCount() > NoteEditorComponent.MAX_NOTE_LENGTH);

  /**
   * Toggles visibility of the secret key input field.
   */
  toggleSecretKeyVisibility(): void {
    this.showSecretKey.update((v) => !v);
  }

  /**
   * Opens or creates a pad based on the user's secret key.
   * If a note exists for this key in the database, decrypts and displays it.
   * If no note exists, opens a fresh editor ready to write.
   */
  async openPad(): Promise<void> {
    const key = this.secretKey().trim();
    if (!key) {
      this.setStatus('error', 'Please enter your secret key.');
      return;
    }

    this.isProcessing.set(true);
    this.setStatus('info', 'Checking for existing pad...');

    try {
      // 1. Derive the deterministic one-way lookup hash from secret key
      const id = await this.encryptionService.deriveLookupId(key);
      this.lookupId.set(id);

      // 2. Query the server for this lookup ID
      try {
        const encryptedNote = await this.notesService.getNote(id);

        // Note exists: decrypt with the secret key
        this.setStatus('info', 'Decrypting your private note...');
        const decryptedText = await this.encryptionService.decrypt(encryptedNote, key);

        this.noteText.set(decryptedText);
        this.isNewPad.set(false);
        this.activeScreen.set('editor');
        this.setStatus('success', 'Existing pad loaded and decrypted successfully.');
      } catch (getErr: any) {
        // If 404 or not found, open a new empty pad
        this.noteText.set('');
        this.isNewPad.set(true);
        this.activeScreen.set('editor');
        this.setStatus('info', 'No existing pad found for this secret key. New pad ready to write.');
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error accessing pad.';
      this.setStatus('error', msg);
    } finally {
      this.isProcessing.set(false);
    }
  }

  /**
   * Encrypts the note locally using the secret key and saves to the server.
   */
  async saveNote(): Promise<void> {
    const text = this.noteText();
    const key = this.secretKey().trim();
    const id = this.lookupId();

    if (!key || !id) {
      this.setStatus('error', 'Session expired. Please re-enter your secret key.');
      this.activeScreen.set('login');
      return;
    }

    if (!text || text.trim().length === 0) {
      this.setStatus('error', 'Cannot save an empty note.');
      return;
    }

    if (text.length > NoteEditorComponent.MAX_NOTE_LENGTH) {
      this.setStatus('error', `Note exceeds limit of ${NoteEditorComponent.MAX_NOTE_LENGTH.toLocaleString()} characters.`);
      return;
    }

    this.isProcessing.set(true);
    this.setStatus('info', 'Encrypting note locally...');

    try {
      // Authenticated client-side encryption (AES-256-GCM + fresh salt & IV)
      const encryptedPayload = await this.encryptionService.encrypt(text, key);

      this.setStatus('info', 'Saving encrypted note to database...');
      await this.notesService.updateNote(id, encryptedPayload);

      this.isNewPad.set(false);
      this.setStatus('success', 'Note saved securely to database.');
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to save note.';
      this.setStatus('error', msg);
    } finally {
      this.isProcessing.set(false);
    }
  }

  /**
   * Prompts confirmation to delete the active pad.
   */
  requestDelete(): void {
    this.showDeleteConfirm.set(true);
  }

  cancelDelete(): void {
    this.showDeleteConfirm.set(false);
  }

  /**
   * Permanently deletes the note from the database and returns to the login screen.
   */
  async confirmDelete(): Promise<void> {
    const id = this.lookupId();
    if (!id) {
      this.lockPad();
      return;
    }

    this.isProcessing.set(true);
    this.setStatus('info', 'Deleting note from database...');

    try {
      await this.notesService.deleteNote(id);
      this.showDeleteConfirm.set(false);
      this.noteText.set('');
      this.lookupId.set(null);
      this.secretKey.set('');
      this.activeScreen.set('login');
      this.setStatus('success', 'Pad has been permanently deleted from the database.');
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to delete note.';
      this.setStatus('error', msg);
    } finally {
      this.isProcessing.set(false);
    }
  }

  /**
   * Locks the pad, wipes the note text and secret key from browser memory,
   * and returns to the initial entrance screen.
   */
  lockPad(): void {
    this.noteText.set('');
    this.secretKey.set('');
    this.lookupId.set(null);
    this.showDeleteConfirm.set(false);
    this.activeScreen.set('login');
    this.setStatus('info', 'Pad locked. Secret key and decrypted text cleared from memory.');
  }

  private statusTimeoutId: any = null;

  setStatus(type: 'success' | 'error' | 'info', text: string): void {
    if (this.statusTimeoutId) {
      clearTimeout(this.statusTimeoutId);
      this.statusTimeoutId = null;
    }
    this.status.set({ type, text });

    // Auto-dismiss notification after 4.5 seconds for non-error messages
    if (type !== 'error') {
      this.statusTimeoutId = setTimeout(() => {
        this.clearStatus();
      }, 4500);
    }
  }

  clearStatus(): void {
    if (this.statusTimeoutId) {
      clearTimeout(this.statusTimeoutId);
      this.statusTimeoutId = null;
    }
    this.status.set(null);
  }
}
