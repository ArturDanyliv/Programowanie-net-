// See https://aka.ms/new-console-template for more information
using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public class ChatClient
{
    private const string ServerIp = "127.0.0.1";
    private const int Port = 8888;

    private TcpClient client;
    private NetworkStream stream;
    private byte[] key;
    private byte[] iv;

    public ChatClient()
    {
        client = new TcpClient();
        key = new byte[32];
        iv = new byte[16];
        stream = null; // Inicjalizacja jako null
    }

    public async Task StartClientAsync()
    {
        try
        {
            await client.ConnectAsync(ServerIp, Port);
            stream = client.GetStream();

            // Read AES key and initialization vector (IV)
            await ReadKeyAndIVAsync();

            string username = ReadUsernameFromConsole();
            if (string.IsNullOrEmpty(username))
            {
                Console.WriteLine("Username cannot be empty.");
                return;
            }

            // Send username to the server
            await SendUsernameAsync(username);

            // Start receiving and printing messages from the server
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    string encryptedMessage = await ReceiveMessageAsync();
                    if (string.IsNullOrEmpty(encryptedMessage))
                        break;

                    string decryptedMessage = DecryptMessage(encryptedMessage);
                    Console.WriteLine(decryptedMessage);
                }
            });

            // Start sending messages to the server
            while (true)
            {
                string message = ReadMessageFromConsole();
                if (string.IsNullOrEmpty(message))
                    continue; // Skip empty messages

                string encryptedMessage = EncryptMessage($"{username}: {message}");
                await SendMessageAsync(encryptedMessage);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }

    private async Task ReadKeyAndIVAsync()
    {
        await stream.ReadAsync(key, 0, key.Length);
        await stream.ReadAsync(iv, 0, iv.Length);
    }

    private async Task SendUsernameAsync(string username)
    {
        byte[] usernameBytes = Encoding.UTF8.GetBytes(username);
        await stream.WriteAsync(usernameBytes, 0, usernameBytes.Length);
    }

    private async Task<string> ReceiveMessageAsync()
    {
        byte[] buffer = new byte[1024];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
    }

    private async Task SendMessageAsync(string message)
    {
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
    }

    private string ReadUsernameFromConsole()
    {
        try
        {
            Console.Write("Enter your username: ");
            return Console.ReadLine();
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error reading username: {ex.Message}");
            return string.Empty; // lub inny sposób obsługi błędu
        }
    }

    private string ReadMessageFromConsole()
    {
        try
        {
            Console.Write("Enter your message: ");
            ClearConsoleInputBuffer();
            return Console.ReadLine();
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error reading message: {ex.Message}");
            return string.Empty; // lub inny sposób obsługi błędu
        }
    }

    private void ClearConsoleInputBuffer()
    {
        while (Console.KeyAvailable)
        {
            Console.ReadKey(intercept: true);
        }
    }

    private string EncryptMessage(string message)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cryptoStream = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                cryptoStream.Write(messageBytes, 0, messageBytes.Length);
                cryptoStream.FlushFinalBlock();
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    private string DecryptMessage(string encryptedMessage)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;

            byte[] encryptedBytes = Convert.FromBase64String(encryptedMessage);
            using (MemoryStream ms = new MemoryStream(encryptedBytes))
            using (CryptoStream cryptoStream = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
            using (StreamReader reader = new StreamReader(cryptoStream))
            {
                return reader.ReadToEnd();
            }
        }
    }

    public static async Task Main()
    {
        await new ChatClient().StartClientAsync();
    }
}
