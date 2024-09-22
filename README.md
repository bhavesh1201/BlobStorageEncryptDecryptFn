# BlobStorageEncryptDecryptFn
This Azure Function uploads and downloads files from Azure Blob Storage. Files are encrypted with AES before upload, and the AES key is encrypted using RSA with a key from Azure Key Vault. During download, the process is reversed: the AES key is decrypted with RSA, and then the file is decrypted.
