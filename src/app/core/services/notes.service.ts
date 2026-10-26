import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateNoteRequest, NoteResponse, UpdateNoteRequest } from '../../features/notes/models/note.model';

@Injectable({
  providedIn: 'root',
})
export class NotesService {
  private readonly http = inject(HttpClient);

  // Connects to Render backend in production (via environment.apiUrl), localhost:5000 in local dev, or relative proxy
  private readonly baseUrl = this.resolveBaseUrl();

  private resolveBaseUrl(): string {
    if (environment.apiUrl && environment.apiUrl.trim().length > 0) {
      return `${environment.apiUrl.replace(/\/$/, '')}/api/notes`;
    }
    if (typeof window !== 'undefined' && window.location.port === '4200') {
      return 'http://localhost:5000/api/notes';
    }
    return '/api/notes';
  }

  /**
   * Persists an encrypted note to the backend database.
   * Plaintext content and user passphrase are NEVER sent.
   */
  async createNote(request: CreateNoteRequest): Promise<NoteResponse> {
    return await firstValueFrom(
      this.http.post<NoteResponse>(this.baseUrl, request)
    );
  }

  /**
   * Retrieves an encrypted note and its cryptographic metadata by note ID.
   */
  async getNote(id: string): Promise<NoteResponse> {
    const encodedId = encodeURIComponent(id.trim());
    return await firstValueFrom(
      this.http.get<NoteResponse>(`${this.baseUrl}/${encodedId}`)
    );
  }

  /**
   * Updates an existing encrypted note with a fresh salt and IV.
   */
  async updateNote(id: string, request: UpdateNoteRequest): Promise<NoteResponse> {
    const encodedId = encodeURIComponent(id.trim());
    return await firstValueFrom(
      this.http.put<NoteResponse>(`${this.baseUrl}/${encodedId}`, request)
    );
  }

  /**
   * Permanently deletes a note from the database.
   */
  async deleteNote(id: string): Promise<void> {
    const encodedId = encodeURIComponent(id.trim());
    await firstValueFrom(
      this.http.delete<void>(`${this.baseUrl}/${encodedId}`)
    );
  }

  /**
   * Checks the health of the backend API.
   */
  async checkHealth(): Promise<{ status: string }> {
    const healthUrl = this.baseUrl.replace(/\/notes$/, '/health');
    return await firstValueFrom(
      this.http.get<{ status: string }>(healthUrl)
    );
  }
}
