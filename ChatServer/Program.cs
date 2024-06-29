// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public class ChatServer
{
    private const int Port = 8888; // Zmiana portu na 8889

    private static readonly ConcurrentDictionary<string, TcpClient> clients = new ConcurrentDictionary<string, TcpClient>();

    public static async Task StartServerAsync()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        Console.WriteLine($"Server started. Listening on port {Port}...");

        try
        {
            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client); // Fire-and-forget to handle the client
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in server: {ex.Message}");
        }
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            NetworkStream stream = client.GetStream();
            using (Aes aes = Aes.Create())
            {
                byte[] key = aes.Key;
                byte[] iv = aes.IV;
                await stream.WriteAsync(key, 0, key.Length);
                await stream.WriteAsync(iv, 0, iv.Length);

                byte[] usernameBytes = new byte[1024];
                int bytesRead = await stream.ReadAsync(usernameBytes, 0, usernameBytes.Length);

                // Check if usernameBytes contains valid data
                if (bytesRead > 0)
                {
                    string username = Encoding.UTF8.GetString(usernameBytes, 0, bytesRead).TrimEnd('\0');

                    // Ensure username is not null or empty
                    if (!string.IsNullOrEmpty(username))
                    {
                        clients[username] = client;
                        Console.WriteLine($"Client connected: {username}");

                        NetworkStream clientStream = client.GetStream();

                        while (client.Connected)
                        {
                            byte[] messageLengthBytes = new byte[4];
                            bytesRead = await clientStream.ReadAsync(messageLengthBytes, 0, 4);
                            if (bytesRead == 0)
                                break;

                            int messageLength = BitConverter.ToInt32(messageLengthBytes, 0);
                            byte[] messageBytes = new byte[messageLength];
                            bytesRead = await clientStream.ReadAsync(messageBytes, 0, messageLength);
                            if (bytesRead == 0)
                                break;

                            string encryptedMessage = Encoding.UTF8.GetString(messageBytes, 0, bytesRead);

                            // Ensure encryptedMessage is not null or empty
                            if (!string.IsNullOrEmpty(encryptedMessage))
                            {
                                string decryptedMessage = DecryptMessage(encryptedMessage, aes);
                                BroadcastMessage($"{username}: {decryptedMessage}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Received empty username.");
                    }
                }
                else
                {
                    Console.WriteLine("No username received.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            clients.TryRemove(GetUsername(client), out _); // Remove client from dictionary upon disconnect
            client.Close();
        }
    }

    private static string DecryptMessage(string encryptedMessage, Aes aes)
    {
        byte[] encryptedBytes = Convert.FromBase64String(encryptedMessage);
        using (MemoryStream ms = new MemoryStream(encryptedBytes))
        using (CryptoStream cryptoStream = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
        using (StreamReader reader = new StreamReader(cryptoStream))
        {
            return reader.ReadToEnd();
        }
    }

    private static void BroadcastMessage(string message)
    {
        foreach (var kvp in clients)
        {
            TcpClient client = kvp.Value;
            NetworkStream stream = client.GetStream();

            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            byte[] lengthBytes = BitConverter.GetBytes(messageBytes.Length);

            stream.Write(lengthBytes, 0, lengthBytes.Length);
            stream.Write(messageBytes, 0, messageBytes.Length);
        }
    }

    public static async Task Main()
    {
        await StartServerAsync();
    }

    private static string GetUsername(TcpClient client)
    {
        foreach (var kvp in clients)
        {
            if (kvp.Value == client)
                return kvp.Key;
        }
        return null;
    }
}
