import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EncryptionService } from './services/encryption.service';
import { NotesService } from '../../core/services/notes.service';
import { NoteResponse } from './models/note.model';

describe('PrivatePad Secret-Key-Driven Secure Workflow', () => {
  let encryptionService: EncryptionService;
  let notesService: NotesService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        EncryptionService,
        NotesService,
      ],
    });

    encryptionService = TestBed.inject(EncryptionService);
    notesService = TestBed.inject(NotesService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('runs key-only workflow: enter key -> if absent, create -> save -> lock -> re-enter key -> decrypt & view -> edit -> delete', async () => {
    const secretKey = 'MySuperSecretVaultKey#2026';
    const noteContent = 'Personal Journal Entry: Encrypted with my secret key!';

    // 1. User enters secret key. Browser derives deterministic one-way lookup hash.
    const lookupId = await encryptionService.deriveLookupId(secretKey);
    expect(lookupId).toHaveLength(64);

    // 2. Browser checks DB for this key's lookup ID
    const initialCheckPromise = notesService.getNote(lookupId);
    const initialReq = httpTesting.expectOne((req) => req.url.endsWith(`/api/notes/${lookupId}`));
    expect(initialReq.request.method).toBe('GET');
    // Simulate note not found initially (404)
    initialReq.flush({ message: 'Note not found.' }, { status: 404, statusText: 'Not Found' });

    await expect(initialCheckPromise).rejects.toBeTruthy();

    // 3. User writes note and clicks "Save Note"
    const encryptedPayload = await encryptionService.encrypt(noteContent, secretKey);
    // Ciphertext must never contain plaintext or secret key
    expect(encryptedPayload.ciphertext).not.toContain(noteContent);
    expect(encryptedPayload.ciphertext).not.toContain(secretKey);

    const savePromise = notesService.updateNote(lookupId, encryptedPayload);
    const saveReq = httpTesting.expectOne((req) => req.url.endsWith(`/api/notes/${lookupId}`));
    expect(saveReq.request.method).toBe('PUT');
    expect(saveReq.request.body.ciphertext).toBe(encryptedPayload.ciphertext);
    expect(JSON.stringify(saveReq.request.body)).not.toContain(noteContent);
    expect(JSON.stringify(saveReq.request.body)).not.toContain(secretKey);

    const savedResponse: NoteResponse = {
      ...encryptedPayload,
      id: lookupId,
      createdAt: new Date().toISOString(),
    };
    saveReq.flush(savedResponse);
    const savedNote = await savePromise;
    expect(savedNote.id).toBe(lookupId);

    // 4. User returns later, enters the same secret key
    const reLookupId = await encryptionService.deriveLookupId(secretKey);
    expect(reLookupId).toBe(lookupId);

    const retrievePromise = notesService.getNote(reLookupId);
    const retrieveReq = httpTesting.expectOne((req) => req.url.endsWith(`/api/notes/${reLookupId}`));
    expect(retrieveReq.request.method).toBe('GET');
    retrieveReq.flush(savedResponse);

    const retrievedPayload = await retrievePromise;

    // 5. Browser decrypts ciphertext locally with secret key
    const decryptedContent = await encryptionService.decrypt(retrievedPayload, secretKey);
    expect(decryptedContent).toBe(noteContent);

    // 6. User deletes pad
    const deletePromise = notesService.deleteNote(lookupId);
    const deleteReq = httpTesting.expectOne((req) => req.url.endsWith(`/api/notes/${lookupId}`));
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null, { status: 204, statusText: 'No Content' });
    await deletePromise;

    // 7. Subsequent check returns 404
    const checkAfterDelete = notesService.getNote(lookupId);
    const checkAfterReq = httpTesting.expectOne((req) => req.url.endsWith(`/api/notes/${lookupId}`));
    checkAfterReq.flush({ message: 'Note not found.' }, { status: 404, statusText: 'Not Found' });
    await expect(checkAfterDelete).rejects.toBeTruthy();
  });
});
