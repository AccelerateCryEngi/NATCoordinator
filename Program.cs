using System;
using System.Collections.Generic;
using System.Net;
using LiteNetLib;

class Program
{
    // Хранилище активных комнат: Ключ = Токен комнаты, Значение = Конечная точка Хоста
    private static Dictionary<string, IPEndPoint> _activeLobbies = new Dictionary<string, IPEndPoint>();
    private static NetManager _server;

    static void Main(string[] args)
    {
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
        while (!Console.KeyAvailable)
        {
            _server.PollEvents();
            Thread.Sleep(10); // Защита процессора от 100% нагрузки
        }

        _server.Stop();
    }

    private class NatPunchListener : INatPunchListener
    {
        // Вызывается, когда Хост или Клиент присылают запрос координатору
        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            // Токен имеет формат "HOST:НазваниеКомнаты" или "CLIENT:НазваниеКомнаты"
            string[] parts = token.Split(':');
            if (parts.Length < 2) return;

            string action = parts[0];
            string roomName = parts[1];

            if (action == "HOST")
            {
                // Запоминаем ВНЕШНИЙ IP и ПОРТ роутера Хоста, который определил наш сервер
                _activeLobbies[roomName] = remoteEndPoint;
                Console.WriteLine($"[РЕГИСТРАЦИЯ] Комната '{roomName}' зарегистрирована. Внешний IP Хоста: {remoteEndPoint}");
            }
            else if (action == "CLIENT")
            {
                if (_activeLobbies.TryGetValue(roomName, out IPEndPoint hostEndPoint))
                {
                    Console.WriteLine($"[СВАХА] Клиент {remoteEndPoint} хочет зайти в '{roomName}'. Начинаем пробитие на Хост {hostEndPoint}...");

                    // Самый важный метод: Сервер отправляет ОДНОВРЕМЕННО два пакета.
                    // Хосту он отсылает IP Клиента, а Клиенту — IP Хоста.
                    _server.NatPunchModule.Introduce(hostEndPoint, remoteEndPoint, roomName);
                }
                else
                {
                    Console.WriteLine($"[ПРЕДУПРЕЖДЕНИЕ] Клиент запрашивает несуществующую комнату: '{roomName}'");
                }
            }
        }

        public void OnNatIntroductionResult(IPEndPoint remoteEndPoint, NatResultType natResultType, string token)
        {
            // Этот метод на самом сервере-посреднике не используется, он нужен на клиентах
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            throw new NotImplementedException();
        }
    }
}
