import { Injectable } from '@angular/core';
import { EncryptedPayload } from '../models/note.model';

@Injectable({
  providedIn: 'root',
})
export class EncryptionService {
  /**
   * Format version for encrypted payloads.
   * Incremented if encryption scheme or parameters change in the future.
   */
  public static readonly CURRENT_VERSION = 1;

  /**
   * OWASP recommended iteration count for PBKDF2 with SHA-256 (600,000).
   * Provides strong resistance against offline brute-force attacks while remaining
   * responsive in modern browser Web Crypto implementations.
   */
  public static readonly DEFAULT_KDF_ITERATIONS = 600000;

  private static readonly SALT_BYTE_LENGTH = 16; // 128-bit salt
  private static readonly IV_BYTE_LENGTH = 12;   // 96-bit IV recommended for AES-GCM
  private static readonly KEY_BIT_LENGTH = 256;  // AES-256

  private readonly textEncoder = new TextEncoder();
  private readonly textDecoder = new TextDecoder('utf-8', { fatal: true });

  /**
   * Derives a deterministic, one-way 64-character hex lookup identifier from the secret key.
   * Uses domain-separated SHA-256 hash so the secret key is never sent over the wire,
   * and the lookup hash cannot be inverted to recover the key or decrypt the ciphertext.
   */
  async deriveLookupId(secretKey: string): Promise<string> {
    if (!secretKey || secretKey.trim().length === 0) {
      throw new Error('Secret key cannot be empty.');
    }
    const subtle = this.getSubtleCrypto();
    const data = this.textEncoder.encode('privatepad:v1:lookup-tag:' + secretKey);
    const hashBuffer = await subtle.digest('SHA-256', data as unknown as BufferSource);
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    return hashArray.map((b) => b.toString(16).padStart(2, '0')).join('');
  }

  /**
   * Encrypts plaintext using AES-256-GCM with a key derived from the passphrase via PBKDF2-SHA256.
   * Every call generates a fresh, cryptographically secure random salt and IV.
   *
   * @param plaintext The secret note text to encrypt.
   * @param passphrase The user-supplied secret passphrase.
   * @returns An EncryptedPayload containing Base64-encoded ciphertext, salt, IV, and metadata.
   */
  async encrypt(plaintext: string, passphrase: string): Promise<EncryptedPayload> {
    if (!plaintext || plaintext.trim().length === 0) {
      throw new Error('Cannot encrypt an empty note.');
    }
    if (!passphrase || passphrase.length === 0) {
      throw new Error('Passphrase cannot be empty.');
    }

    const subtle = this.getSubtleCrypto();

    // 1. Generate fresh cryptographically random salt and IV
    const salt = new Uint8Array(EncryptionService.SALT_BYTE_LENGTH);
    crypto.getRandomValues(salt);

    const iv = new Uint8Array(EncryptionService.IV_BYTE_LENGTH);
    crypto.getRandomValues(iv);

    const iterations = EncryptionService.DEFAULT_KDF_ITERATIONS;

    // 2. Derive non-extractable AES-256-GCM key from passphrase
    const aesKey = await this.deriveKey(passphrase, salt, iterations, subtle);

    // 3. Encrypt and authenticate the plaintext
    const plaintextBytes = this.textEncoder.encode(plaintext);
    const ciphertextBuffer = await subtle.encrypt(
      {
        name: 'AES-GCM',
        iv: iv as unknown as BufferSource,
      },
      aesKey,
      plaintextBytes as unknown as BufferSource
    );

    const ciphertext = new Uint8Array(ciphertextBuffer);

    return {
      ciphertext: this.bytesToBase64(ciphertext),
      salt: this.bytesToBase64(salt),
      iv: this.bytesToBase64(iv),
      encryptionVersion: EncryptionService.CURRENT_VERSION,
      kdfIterations: iterations,
    };
  }

  /**
   * Decrypts and authenticates ciphertext using AES-256-GCM and a PBKDF2-SHA256 derived key.
   *
   * @param payload The encrypted payload received from the server.
   * @param passphrase The secret passphrase entered by the user.
   * @returns The decrypted plaintext note string.
   * @throws Generic error on wrong passphrase, corrupted ciphertext, or tampered payload.
   */
  async decrypt(payload: EncryptedPayload, passphrase: string): Promise<string> {
    if (!passphrase || passphrase.length === 0) {
      throw new Error('Passphrase cannot be empty.');
    }

    this.validatePayload(payload);

    const subtle = this.getSubtleCrypto();

    let salt: Uint8Array;
    let iv: Uint8Array;
    let ciphertext: Uint8Array;

    try {
      salt = this.base64ToBytes(payload.salt);
      iv = this.base64ToBytes(payload.iv);
      ciphertext = this.base64ToBytes(payload.ciphertext);
    } catch {
      throw new Error('Invalid or corrupted encrypted data format.');
    }

    if (salt.length !== EncryptionService.SALT_BYTE_LENGTH) {
      throw new Error('Invalid cryptographic salt length.');
    }
    if (iv.length !== EncryptionService.IV_BYTE_LENGTH) {
      throw new Error('Invalid initialization vector length.');
    }

    try {
      // Derive key using the stored iteration count from payload
      const aesKey = await this.deriveKey(passphrase, salt, payload.kdfIterations, subtle);

      // Decrypt and authenticate
      const decryptedBuffer = await subtle.decrypt(
        {
          name: 'AES-GCM',
          iv: iv as unknown as BufferSource,
        },
        aesKey,
        ciphertext as unknown as BufferSource
      );

      return this.textDecoder.decode(decryptedBuffer);
    } catch {
      // Never expose internal Web Crypto error messages or stack traces
      throw new Error('Decryption failed. Please verify your passphrase.');
    }
  }

  /**
   * Derives a non-extractable AES-GCM CryptoKey using PBKDF2 with SHA-256.
   */
  private async deriveKey(
    passphrase: string,
    salt: Uint8Array,
    iterations: number,
    subtle: SubtleCrypto
  ): Promise<CryptoKey> {
    const passphraseBytes = this.textEncoder.encode(passphrase);

    // Import the passphrase as raw key material for derivation
    const keyMaterial = await subtle.importKey(
      'raw',
      passphraseBytes as unknown as BufferSource,
      'PBKDF2',
      false, // non-extractable
      ['deriveKey']
    );

    // Derive the AES-GCM 256-bit key
    return await subtle.deriveKey(
      {
        name: 'PBKDF2',
        salt: salt as unknown as BufferSource,
        iterations: iterations,
        hash: 'SHA-256',
      },
      keyMaterial,
      {
        name: 'AES-GCM',
        length: EncryptionService.KEY_BIT_LENGTH,
      },
      false, // non-extractable: derived key cannot be extracted or exported
      ['encrypt', 'decrypt']
    );
  }

  /**
   * Validates the structure and constraints of an EncryptedPayload.
   */
  private validatePayload(payload: EncryptedPayload): void {
    if (!payload) {
      throw new Error('Encrypted payload is missing.');
    }
    if (!payload.ciphertext || typeof payload.ciphertext !== 'string') {
      throw new Error('Malformed encrypted payload: missing ciphertext.');
    }
    if (!payload.salt || typeof payload.salt !== 'string') {
      throw new Error('Malformed encrypted payload: missing salt.');
    }
    if (!payload.iv || typeof payload.iv !== 'string') {
      throw new Error('Malformed encrypted payload: missing IV.');
    }
    if (typeof payload.kdfIterations !== 'number' || payload.kdfIterations < 1000) {
      throw new Error('Malformed encrypted payload: invalid KDF iterations.');
    }
    if (typeof payload.encryptionVersion !== 'number' || payload.encryptionVersion < 1) {
      throw new Error('Malformed encrypted payload: unsupported encryption version.');
    }
  }

  /**
   * Helper to retrieve subtle crypto API with error handling if unsupported.
   */
  private getSubtleCrypto(): SubtleCrypto {
    if (typeof crypto === 'undefined' || !crypto.subtle) {
      throw new Error('Web Crypto API is not supported in this environment.');
    }
    return crypto.subtle;
  }

  /**
   * Converts a Uint8Array into a standard Base64 string.
   */
  public bytesToBase64(bytes: Uint8Array): string {
    let binary = '';
    const len = bytes.byteLength;
    for (let i = 0; i < len; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
  }

  /**
   * Converts a Base64 string into a Uint8Array.
   */
  public base64ToBytes(base64: string): Uint8Array {
    const binary = atob(base64);
    const len = binary.length;
    const bytes = new Uint8Array(len);
    for (let i = 0; i < len; i++) {
      bytes[i] = binary.charCodeAt(i);
    }
    return bytes;
  }
}
