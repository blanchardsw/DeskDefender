using System.Threading.Tasks;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for encryption and decryption operations
    /// </summary>
    public interface IEncryptionService
    {
        /// <summary>
        /// Encrypts a plain text string
        /// </summary>
        /// <param name="plainText">The text to encrypt</param>
        /// <returns>Encrypted text</returns>
        string Encrypt(string plainText);

        /// <summary>
        /// Decrypts an encrypted string
        /// </summary>
        /// <param name="cipherText">The encrypted text to decrypt</param>
        /// <returns>Decrypted plain text</returns>
        string Decrypt(string cipherText);

        /// <summary>
        /// Encrypts a file
        /// </summary>
        /// <param name="filePath">Path to the file to encrypt</param>
        /// <param name="outputPath">Path where the encrypted file will be saved</param>
        Task EncryptFileAsync(string filePath, string outputPath);

        /// <summary>
        /// Decrypts a file
        /// </summary>
        /// <param name="encryptedFilePath">Path to the encrypted file</param>
        /// <param name="outputPath">Path where the decrypted file will be saved</param>
        Task DecryptFileAsync(string encryptedFilePath, string outputPath);
    }
}
