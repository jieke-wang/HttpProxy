using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebSocketClientWorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ConnectAsync("ws://127.0.0.1:8888/socketdemo", stoppingToken);
            }
        }

        private async Task ConnectAsync(string url, CancellationToken stoppingToken)
        {
            await Task.Delay(1000);
            ClientWebSocket webSocket = null;
            try
            {
                webSocket = new ClientWebSocket();
                await webSocket.ConnectAsync(new Uri(url), stoppingToken);
                await Task.WhenAll(SendAsync(webSocket, stoppingToken), ReciveAsync(webSocket, stoppingToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return;
            }
            finally
            {
                webSocket?.Dispose();
            }
        }

        private async Task SendAsync(ClientWebSocket webSocket, CancellationToken stoppingToken)
        {
            while (webSocket.State == WebSocketState.Open)
            {
                Console.WriteLine("填写发往服务端的内容");
                string stringToSend = Console.ReadLine();
                byte[] buffer = Encoding.UTF8.GetBytes(stringToSend);
                await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, false, stoppingToken);
                Console.WriteLine($"发送: {stringToSend}");
                await Task.Delay(1000);
            }
        }

        private async Task ReciveAsync(ClientWebSocket webSocket, CancellationToken stoppingToken)
        {
            byte[] buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult webSocketReceiveResult = await webSocket.ReceiveAsync(buffer, stoppingToken);

                if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, stoppingToken);
                }
                else
                {
                    Console.WriteLine($"接收: {Encoding.UTF8.GetString(buffer).TrimEnd('\0')}");
                }
            }
        }
    }
}
