using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Collections.Concurrent;

// Класс сервера для обработки подключений и передачи файлов
public class FileServer
{
	private const int Port = 5000; // Порт для прослушивания соединений
	private TcpListener _listener; // Серверный слушатель TCP-соединений
	private ConcurrentDictionary<string, TcpClient> _clients = new(); // Список подключенных клиентов

	public FileServer()
	{
		_listener = new TcpListener(IPAddress.Any, Port); // Инициализация слушателя
	}

	// Запуск сервера и ожидание подключений
	public async Task StartAsync()
	{
		_listener.Start(); // Запуск прослушивания порта
		Console.WriteLine($"Server listening on port {Port}...");

		while (true)
		{
			var client = await _listener.AcceptTcpClientAsync(); // Ожидание подключения клиента
			string clientEndPoint = client.Client.RemoteEndPoint.ToString(); // Получение IP клиента
			_clients[clientEndPoint] = client; // Добавление клиента в список
			Console.WriteLine($"Client connected: {clientEndPoint}");
			_ = HandleClientAsync(client, clientEndPoint); // Запуск обработки клиента
		}
	}

	// Обработка запросов клиента
	public async Task HandleClientAsync(TcpClient client, string clientEndPoint)
	{
		using var stream = client.GetStream(); // Получение потока данных
		using var reader = new StreamReader(stream, Encoding.UTF8);
		using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

		while (true)
		{
			string request = await reader.ReadLineAsync(); // Читаем запрос от клиента
			if (request == "GET_CLIENTS") // Если клиент запрашивает список подключенных пользователей
			{
				string clientsList = GetConnectedClients(); // Получаем список клиентов
				await writer.WriteLineAsync(clientsList); // Отправляем список клиенту
			}
		}
	}

	// Возвращает список подключенных клиентов
	public string GetConnectedClients()
	{
		return string.Join(", ", _clients.Keys); // Объединяем IP-адреса в строку
	}
}

// Класс клиента, который взаимодействует с сервером
public class FileClient
{
	private const int Port = 5000; // Порт сервера

	// Запрос списка клиентов у сервера
	public async Task<string> RequestClientListAsync(string serverIp)
	{
		using var client = new TcpClient(); // Создаем TCP-клиент
		await client.ConnectAsync(IPAddress.Parse(serverIp), Port); // Подключаемся к серверу
		using var stream = client.GetStream(); // Получаем поток
		using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
		using var reader = new StreamReader(stream, Encoding.UTF8);

		await writer.WriteLineAsync("GET_CLIENTS"); // Отправляем запрос на получение списка клиентов
		return await reader.ReadLineAsync(); // Получаем ответ со списком клиентов
	}

	// Отправка файла через сервер другому клиенту
	public async Task SendFileAsync(string serverIp, string targetClient, string filePath)
	{
		using var client = new TcpClient(); // Создаем TCP-клиент
		await client.ConnectAsync(IPAddress.Parse(serverIp), Port); // Подключаемся к серверу
		using var stream = client.GetStream(); // Получаем поток
		using var writer = new BinaryWriter(stream, Encoding.UTF8);

		FileInfo fileInfo = new FileInfo(filePath); // Получаем информацию о файле
		string fileHash = ComputeFileHash(filePath); // Вычисляем хеш файла для проверки целостности

		writer.Write(targetClient); // Указываем, какому клиенту предназначен файл
		writer.Write(fileInfo.Name); // Отправляем имя файла
		writer.Write(fileInfo.Length); // Отправляем размер файла
		writer.Write(fileHash); // Отправляем хеш файла

		using var fileStream = File.OpenRead(filePath); // Открываем файл для чтения
		await fileStream.CopyToAsync(stream); // Передаем файл через поток

		Console.WriteLine($"Sent file: {filePath} ({fileInfo.Length} bytes) to {targetClient} via server {serverIp}");
	}

	// Вычисление SHA-256 хеша файла
	private string ComputeFileHash(string filePath)
	{
		using var sha256 = SHA256.Create(); // Создаем объект SHA-256
		using var stream = File.OpenRead(filePath); // Открываем файл для чтения
		byte[] hashBytes = sha256.ComputeHash(stream); // Вычисляем хеш
		return BitConverter.ToString(hashBytes).Replace("-", "").ToLower(); // Преобразуем в строку
	}
}

// Главный класс программы
class Program
{
	static async Task Main(string[] args)
	{
		Console.WriteLine("Start as (server/client): ");
		string mode = Console.ReadLine().ToLower(); // Определяем режим работы

		if (mode == "server")
		{
			var server = new FileServer(); // Создаем сервер
			_ = Task.Run(async () => await server.StartAsync()); // Запускаем сервер асинхронно

			while (true)
			{
				Console.WriteLine("Enter 'list' to view connected clients:");
				string command = Console.ReadLine().ToLower(); // Читаем команду
				if (command == "list") // Если введена команда "list"
				{
					Console.WriteLine("Connected clients: " + server.GetConnectedClients()); // Выводим список клиентов
				}
			}
		}
		else if (mode == "client")
		{
			var client = new FileClient(); // Создаем клиента
			Console.WriteLine("Enter server IP:");
			string serverIp = Console.ReadLine(); // Ввод IP сервера

			Console.WriteLine("Fetching connected clients...");
			string clients = await client.RequestClientListAsync(serverIp); // Запрашиваем список клиентов
			Console.WriteLine("Available clients: " + clients); // Выводим список клиентов

			Console.WriteLine("Enter target client IP:");
			string targetClient = Console.ReadLine(); // Ввод IP получателя

			Console.WriteLine("Enter file path:");
			string filePath = Console.ReadLine(); // Ввод пути к файлу

			await client.SendFileAsync(serverIp, targetClient, filePath); // Отправляем файл
		}
		else
		{
			Console.WriteLine("Invalid mode. Please restart and enter 'server' or 'client'."); // Ошибка выбора режима
		}
	}
}