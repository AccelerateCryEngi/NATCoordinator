using System;
using System.Collections.Generic;
using System.Net;
using LiteNetLib;

class Program
{
    // Хранилище активных комнат: Ключ = Токен комнаты, Значение = Конечная точка Хоста
    // Изменяем тип хранилища, чтобы сохранять И внутренний, И внешний IP хоста
    private static Dictionary<string, (IPEndPoint Internal, IPEndPoint External)> _activeLobbies = new Dictionary<string, (IPEndPoint, IPEndPoint)>();
    private static NetManager _server;


    static void Main(string[] args)
    {
        // Специальный микро-сервер для прохождения проверки (Health Check) Render
        Task.Run(() =>
        {
            try
            {
                string portEnv = Environment.GetEnvironmentVariable("PORT") ?? "10000";
                using HttpListener httpListener = new HttpListener();
                httpListener.Prefixes.Add($"http://*:{portEnv}/");
                httpListener.Start();
                Console.WriteLine($"[HEALTH CHECK] HTTP-заглушка запущена на порту {portEnv}");

                while (true)
                {
                    HttpListenerContext context = httpListener.GetContext();
                    HttpListenerResponse response = context.Response;
                    string responseString = "OK";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HTTP ОШИБКА] {ex.Message}");
            }
        });

        EventBasedNetListener listener = new EventBasedNetListener();
        _server = new NetManager(listener);

        // Инициализируем модуль пробития NAT внутри LiteNetLib
        _server.NatPunchModule.Init(new NatPunchListener());

        // Запускаем сервер на порту 5005 для всего мира
        if (_server.Start(5005))
        {
            Console.WriteLine("[КООРДИНАТОР] Запущен успешно на UDP порту 5005!");
            Console.WriteLine("[КООРДИНАТОР] Ожидание запросов на пробитие NAT...");
        }
        else
        {
            Console.WriteLine("[ОШИБКА] Не удалось занять порт 5005.");
            return;
        }

        // Высокопроизводительный игровой цикл опроса сетевой карты
        // Высокопроизводительный игровой цикл опроса сетевой карты
        // Бесконечный цикл опроса сети, безопасный для облачных контейнеров Linux
        while (true)
        {
            _server.PollEvents();
            Thread.Sleep(15); // Защита процессора от перегрева
        }

        _server.Stop();
    }

    private class NatPunchListener : INatPunchListener
    {
        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            // 1. Разбиваем токен "HOST:Имя" или "CLIENT:Имя" на части
            string[] parts = token.Split(':');
            if (parts.Length < 2) return;

            string action = parts[0];   // HOST или CLIENT
            string roomName = parts[1]; // Имя лобби

            // 2. Логика для Хоста
            if (action == "HOST")
            {
                // Сохраняем КОРТЕЖ из локального и внешнего адресов Хоста
                _activeLobbies[roomName] = (localEndPoint, remoteEndPoint);
                Console.WriteLine($"[РЕГИСТРАЦИЯ] Комната '{roomName}' добавлена. Локальный: {localEndPoint}, Внешний: {remoteEndPoint}");
            }
            // 3. Логика для Клиента
            else if (action == "CLIENT")
            {
                // Извлекаем сохраненную пару адресов Хоста
                if (_activeLobbies.TryGetValue(roomName, out var hostAddresses))
                {
                    Console.WriteLine($"[СВАХА] Клиент (Локальный: {localEndPoint}, Внешний: {remoteEndPoint}) запрашивает '{roomName}'. Знакомим игроков...");

                    // Передаем ВСЕ 5 обязательных параметров:
                    // 1. Внутренний Хоста, 2. Внешний Хоста, 3. Внутренний Клиента, 4. Внешний Клиента, 5. Имя комнаты
                    _server.NatPunchModule.NatIntroduce(
                        hostAddresses.Internal,
                        hostAddresses.External,
                        localEndPoint,
                        remoteEndPoint,
                        roomName
                    );
                }
                else
                {
                    Console.WriteLine($"[ПРЕДУПРЕЖДЕНИЕ] Клиент запрашивает несуществующую комнату: '{roomName}'");
                }
            }
        }

        public void OnNatIntroductionResult(IPEndPoint remoteEndPoint, NatAddressType natResultType, string token)
        {
            // На самом сервере-координаторе этот метод не используется (он нужен только игрокам)
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            throw new NotImplementedException();
        }
    }

}
