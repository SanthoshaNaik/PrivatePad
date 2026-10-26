import { TestBed, ComponentFixture } from '@angular/core/testing';
import { NoteEditorComponent } from './note-editor';
import { EncryptionService } from '../../services/encryption.service';
import { NotesService } from '../../../../core/services/notes.service';
import { EncryptedPayload, NoteResponse } from '../../models/note.model';

describe('NoteEditorComponent', () => {
  let component: NoteEditorComponent;
  let fixture: ComponentFixture<NoteEditorComponent>;
  let mockEncryptionService: {
    deriveLookupId: (key: string) => Promise<string>;
    encrypt: (text: string, pass: string) => Promise<EncryptedPayload>;
    decrypt: (payload: EncryptedPayload, pass: string) => Promise<string>;
  };
  let mockNotesService: {
    createNote: (req: any) => Promise<NoteResponse>;
    getNote: (id: string) => Promise<NoteResponse>;
    updateNote: (id: string, req: any) => Promise<NoteResponse>;
    deleteNote: (id: string) => Promise<void>;
  };

  const samplePayload: EncryptedPayload = {
    ciphertext: 'U2FtcGxlQ2lwaGVydGV4dA==',
    salt: 'U2FtcGxlU2FsdA==',
    iv: 'U2FtcGxlSVY=',
    encryptionVersion: 1,
    kdfIterations: 600000,
  };

  beforeEach(async () => {
    mockEncryptionService = {
      deriveLookupId: async (key: string) => `hash_of_${key}`,
      encrypt: async (text: string, pass: string) => ({
        ...samplePayload,
      }),
      decrypt: async (payload: EncryptedPayload, pass: string) => {
        if (pass === 'wrong_key') {
          throw new Error('Decryption failed. Please verify your passphrase.');
        }
        return 'Decrypted private note content from database';
      },
    };

    mockNotesService = {
      createNote: async (req: any) => ({
        ...req,
        id: req.id || 'note-xyz-123',
        createdAt: new Date().toISOString(),
      }),
      getNote: async (id: string) => {
        if (id.includes('existing_key')) {
          return {
            id: id,
            ...samplePayload,
            createdAt: new Date().toISOString(),
          };
        }
        throw new Error('Note not found.');
      },
      updateNote: async (id: string, req: any) => ({
        id,
        ...req,
        createdAt: new Date().toISOString(),
      }),
      deleteNote: async (id: string) => {
        if (id === 'fail_delete') {
          throw new Error('Server error deleting note.');
        }
      },
    };

    await TestBed.configureTestingModule({
      imports: [NoteEditorComponent],
      providers: [
        { provide: EncryptionService, useValue: mockEncryptionService },
        { provide: NotesService, useValue: mockNotesService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(NoteEditorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize on the secret key login screen', () => {
    expect(component.activeScreen()).toBe('login');
    expect(component.secretKey()).toBe('');
    expect(component.noteText()).toBe('');
    expect(component.lookupId()).toBeNull();
  });

  it('should reject opening pad with empty secret key', async () => {
    component.secretKey.set('');
    await component.openPad();

    expect(component.activeScreen()).toBe('login');
    expect(component.status()?.type).toBe('error');
    expect(component.status()?.text).toContain('Please enter your secret key');
  });

  it('should toggle secret key visibility', () => {
    expect(component.showSecretKey()).toBe(false);
    component.toggleSecretKeyVisibility();
    expect(component.showSecretKey()).toBe(true);
    component.toggleSecretKeyVisibility();
    expect(component.showSecretKey()).toBe(false);
  });

  it('should open existing pad and decrypt content if secret key exists on DB', async () => {
    component.secretKey.set('my_existing_key');
    await component.openPad();

    expect(component.activeScreen()).toBe('editor');
    expect(component.isNewPad()).toBe(false);
    expect(component.noteText()).toBe('Decrypted private note content from database');
    expect(component.status()?.type).toBe('success');
  });

  it('should open new empty pad if secret key does not exist on DB', async () => {
    component.secretKey.set('brand_new_secret_key');
    await component.openPad();

    expect(component.activeScreen()).toBe('editor');
    expect(component.isNewPad()).toBe(true);
    expect(component.noteText()).toBe('');
    expect(component.status()?.type).toBe('info');
    expect(component.status()?.text).toContain('New pad ready');
  });

  it('should reject saving an empty note', async () => {
    component.secretKey.set('some_key');
    await component.openPad();

    component.noteText.set('');
    await component.saveNote();

    expect(component.status()?.type).toBe('error');
    expect(component.status()?.text).toContain('empty note');
  });

  it('should encrypt locally and save note to database', async () => {
    component.secretKey.set('brand_new_secret_key');
    await component.openPad();

    component.noteText.set('My confidential private thoughts');
    await component.saveNote();

    expect(component.status()?.type).toBe('success');
    expect(component.status()?.text).toContain('saved securely');
    expect(component.isNewPad()).toBe(false);
  });

  it('should lock pad and clear secret key and content from memory', async () => {
    component.secretKey.set('my_existing_key');
    await component.openPad();

    expect(component.activeScreen()).toBe('editor');
    expect(component.noteText().length).toBeGreaterThan(0);

    component.lockPad();

    expect(component.activeScreen()).toBe('login');
    expect(component.secretKey()).toBe('');
    expect(component.noteText()).toBe('');
    expect(component.lookupId()).toBeNull();
  });

  it('should prompt confirmation and permanently delete pad from database', async () => {
    component.secretKey.set('my_existing_key');
    await component.openPad();

    component.requestDelete();
    expect(component.showDeleteConfirm()).toBe(true);

    await component.confirmDelete();

    expect(component.activeScreen()).toBe('login');
    expect(component.secretKey()).toBe('');
    expect(component.noteText()).toBe('');
    expect(component.status()?.type).toBe('success');
    expect(component.status()?.text).toContain('permanently deleted');
  });
});
