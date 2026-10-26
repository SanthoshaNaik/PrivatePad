import { TestBed } from '@angular/core/testing';
import { EncryptionService } from './encryption.service';
import { EncryptedPayload } from '../models/note.model';

describe('EncryptionService', () => {
  let service: EncryptionService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(EncryptionService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should derive deterministic 64-char hex lookup ID from secret key', async () => {
    const key = 'MyPersonalSecretKey999';
    const id1 = await service.deriveLookupId(key);
    const id2 = await service.deriveLookupId(key);

    expect(id1).toBe(id2);
    expect(id1.length).toBe(64);
    expect(/^[0-9a-f]{64}$/.test(id1)).toBe(true);

    const otherId = await service.deriveLookupId('DifferentSecretKey888');
    expect(otherId).not.toBe(id1);
  });

  it('should reject empty secret key when deriving lookup ID', async () => {
    await expect(service.deriveLookupId('')).rejects.toThrow('Secret key cannot be empty.');
    await expect(service.deriveLookupId('   ')).rejects.toThrow('Secret key cannot be empty.');
  });

  it('should encrypt plaintext into ciphertext and not leak plaintext', async () => {
    const plaintext = 'Top Secret: The eagle flies at midnight!';
    const passphrase = 'SuperSecretPassphrase123!';

    const payload = await service.encrypt(plaintext, passphrase);

    expect(payload).toBeDefined();
    expect(payload.ciphertext).toBeDefined();
    expect(payload.salt).toBeDefined();
    expect(payload.iv).toBeDefined();
    expect(payload.encryptionVersion).toBe(1);
    expect(payload.kdfIterations).toBeGreaterThanOrEqual(100000);

    // Ciphertext must NOT contain plaintext
    expect(payload.ciphertext).not.toContain(plaintext);
    expect(payload.ciphertext).not.toContain('eagle');
  });

  it('should generate fresh salt and IV for independent encryptions of identical plaintext', async () => {
    const plaintext = 'Identical Secret Message';
    const passphrase = 'SharedPassphrase987!';

    const first = await service.encrypt(plaintext, passphrase);
    const second = await service.encrypt(plaintext, passphrase);

    // Cryptographic parameters must differ
    expect(first.salt).not.toBe(second.salt);
    expect(first.iv).not.toBe(second.iv);
    expect(first.ciphertext).not.toBe(second.ciphertext);
  });

  it('should successfully decrypt with the correct passphrase', async () => {
    const originalText = 'Confidential bank notes & sensitive credentials: #1234-ABCD.';
    const passphrase = 'CorrectPassphrase42$';

    const payload = await service.encrypt(originalText, passphrase);
    const decrypted = await service.decrypt(payload, passphrase);

    expect(decrypted).toBe(originalText);
  });

  it('should fail decryption when given an incorrect passphrase', async () => {
    const originalText = 'Private personal reflections...';
    const correctPassphrase = 'RightPassphrase1';
    const wrongPassphrase = 'WrongPassphrase2';

    const payload = await service.encrypt(originalText, correctPassphrase);

    await expect(service.decrypt(payload, wrongPassphrase)).rejects.toThrow(
      'Decryption failed. Please verify your passphrase.'
    );
  });

  it('should reject decryption when ciphertext has been tampered with (GCM authentication check)', async () => {
    const originalText = 'Untampered financial data';
    const passphrase = 'SecureKey#1';

    const payload = await service.encrypt(originalText, passphrase);

    // Tamper with ciphertext by altering a byte in the Base64 string
    const bytes = service.base64ToBytes(payload.ciphertext);
    bytes[0] ^= 0xff; // flip bits
    const tamperedPayload: EncryptedPayload = {
      ...payload,
      ciphertext: service.bytesToBase64(bytes),
    };

    await expect(service.decrypt(tamperedPayload, passphrase)).rejects.toThrow(
      'Decryption failed. Please verify your passphrase.'
    );
  });

  it('should reject decryption when IV has been altered', async () => {
    const originalText = 'Integrity protected message';
    const passphrase = 'SecureKey#2';

    const payload = await service.encrypt(originalText, passphrase);

    const ivBytes = service.base64ToBytes(payload.iv);
    ivBytes[0] ^= 0x01;
    const tamperedPayload: EncryptedPayload = {
      ...payload,
      iv: service.bytesToBase64(ivBytes),
    };

    await expect(service.decrypt(tamperedPayload, passphrase)).rejects.toThrow(
      'Decryption failed. Please verify your passphrase.'
    );
  });

  it('should reject empty note content when encrypting', async () => {
    await expect(service.encrypt('', 'passphrase')).rejects.toThrow('Cannot encrypt an empty note.');
    await expect(service.encrypt('   ', 'passphrase')).rejects.toThrow('Cannot encrypt an empty note.');
  });

  it('should reject empty passphrase when encrypting or decrypting', async () => {
    await expect(service.encrypt('some note', '')).rejects.toThrow('Passphrase cannot be empty.');

    const dummyPayload: EncryptedPayload = {
      ciphertext: 'AAAA',
      salt: 'AAAA',
      iv: 'AAAA',
      encryptionVersion: 1,
      kdfIterations: 600000,
    };
    await expect(service.decrypt(dummyPayload, '')).rejects.toThrow('Passphrase cannot be empty.');
  });

  it('should reject malformed payload safely', async () => {
    const invalidPayload = {
      ciphertext: '',
      salt: '',
      iv: '',
      encryptionVersion: 1,
      kdfIterations: 600000,
    } as EncryptedPayload;

    await expect(service.decrypt(invalidPayload, 'passphrase')).rejects.toThrow(
      'Malformed encrypted payload'
    );
  });
});

