using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Security.Cryptography;

namespace FileClient
{
	public class FileClient
	{
		private const int Port = 5000; // Порт сервера

		static async Task Main(string[] args)
		{
			var client = new FileClient(); // Создаем клиента

			string server_IP = "62.113.44.183";


			string peers = await RequestClientListAsync(server_IP);

			string filePath = "C:\\Users\\Студент\\Desktop\\Работа для мокапа.png";
			
			string[] peer_list = peers.Split(',');

			foreach (string peer in peer_list)
			{
				await SendFileAsync(server_IP, peer, filePath);
			}
		}


		public static async Task<string> RequestClientListAsync(string serverIp)
		{
			using var client = new TcpClient(); // Создаем TCP-клиент
			await client.ConnectAsync(IPAddress.Parse(serverIp), Port); // Подключаемся к серверу
			using var stream = client.GetStream(); // Получаем поток
			using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
			using var reader = new StreamReader(stream, Encoding.UTF8);

			await writer.WriteLineAsync("GET_CLIENTS"); // Отправляем запрос на получение списка клиентов

			return await reader.ReadLineAsync(); // Получаем ответ со списком клиентов
		}


		public static async Task SendFileAsync( string serverIp, string targetClient, string filePath)
		{
			using var client = new TcpClient(); // Создаем TCP-клиент
			await client.ConnectAsync(IPAddress.Parse(serverIp), Port); // Подключаемся к серверу
			using var stream = client.GetStream(); // Получаем поток

			using var writer = new BinaryWriter(stream, Encoding.UTF8);

			FileInfo fileInfo = new FileInfo(filePath);
			string fileHash = await ComputeFileHash(filePath); // Вычисляем хеш файла для проверки целостности
			
			writer.Write(targetClient); // Указываем, какому клиенту предназначен файл
			writer.Write(fileInfo.Name); // Отправляем имя файла
			writer.Write(fileInfo.Length); // Отправляем размер файла
			writer.Write(fileHash); // Отправляем хеш файла
		}

		// Вычисление SHA-256 хеша файла
		private static async Task<string> ComputeFileHash(string filePath)
		{
			using var sha256 = SHA256.Create(); // Создаем объект SHA-256
			using var stream = File.OpenRead(filePath); // Открываем файл для чтения
			byte[] hashBytes =  await sha256.ComputeHashAsync(stream);
			return BitConverter.ToString(hashBytes).Replace("-", "").ToLower(); // Преобразуем в строку
		}


	}
}
