export interface EncryptedPayload {
  ciphertext: string;
  salt: string;
  iv: string;
  encryptionVersion: number;
  kdfIterations: number;
}

export interface CreateNoteRequest extends EncryptedPayload {
  id?: string;
  expiresInHours?: number | null;
}

export interface UpdateNoteRequest extends EncryptedPayload {}

export interface NoteResponse extends EncryptedPayload {
  id: string;
  createdAt: string;
  expiresAt?: string | null;
}

export interface StatusNotification {
  type: 'success' | 'error' | 'info';
  text: string;
}

