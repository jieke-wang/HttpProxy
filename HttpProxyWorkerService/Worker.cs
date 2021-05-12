using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using RestSharp;

namespace HttpProxyWorkerService
{
    public class Worker : BackgroundService
    {
        private CookieContainer _cookieContainer;
        private HttpListener _listener;
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            _cookieContainer = new CookieContainer();
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:8888/");
            _listener.Start();

            Console.WriteLine($"开始监听: {string.Join("; ", _listener.Prefixes)}");

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                HttpListenerContext ctx = await _listener.GetContextAsync();
                //await ProcessAsync(ctx);
                //if(ctx.Request.IsWebSocketRequest)
                //{
                //    await ProcessWebSocketAsync(ctx, stoppingToken);
                //}
                //else
                {
                    await Process2Async(ctx, stoppingToken);
                }
            }
        }

        private async Task ProcessAsync(HttpListenerContext ctx)
        {
            await Task.Factory.StartNew(() =>
            {
                HttpListenerRequest request = ctx.Request;
                HttpListenerResponse response = ctx.Response;
                string responseString = "<HTML><BODY> Hello world!</BODY></HTML>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);

                response.ContentLength64 = buffer.Length;
                Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);

                output.Close();
            });
        }

        private async Task Process2Async(HttpListenerContext ctx, CancellationToken stoppingToken)
        {
            HttpListenerRequest request = ctx.Request;
            HttpListenerResponse response = ctx.Response;

            Console.WriteLine(request.RawUrl);

            #region 代理设置
            //const string protocol = "https";
            //const string host = "www.runoob.com";

            //const string protocol = "https";
            //const string host = "www.zhihu.com";

            //const string protocol = "https";
            //const string host = "www.cnblogs.com";

            const string protocol = "http";
            const string host = "www.baidu.com";

            //const string protocol = "http";
            //const string host = "www.kaifenginternet.com";

            //const string protocol = "https";
            //const string host = "www.bing.com";
            #endregion

            string proxyBaseUrl = $"{protocol}{Uri.SchemeDelimiter}{host}";
            RestClient proxyClient = new RestClient(proxyBaseUrl)
            {
                RemoteCertificateValidationCallback = delegate { return true; },
                CookieContainer = _cookieContainer,
                FollowRedirects = true,
            };
            RestRequest proxyRequest = new RestRequest(request.RawUrl.TrimStart('/'), Enum.Parse<Method>(request.HttpMethod, true));

            string originalBaseUrl = $"{request.Url.Scheme}{Uri.SchemeDelimiter}{request.Url.Authority}";
            foreach (var header in request.Headers.AllKeys)
            {
                if (string.Equals(header, "Host", StringComparison.OrdinalIgnoreCase))
                {
                    proxyRequest.AddHeader("Host", host);
                    continue;
                }
                else if(string.Equals(header, "Origin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(header, "Referer", StringComparison.OrdinalIgnoreCase))
                {
                    proxyRequest.AddHeader(header, request.Headers.Get(header).Replace(originalBaseUrl, proxyBaseUrl, StringComparison.OrdinalIgnoreCase));
                    continue;
                }

                proxyRequest.AddHeader(header, request.Headers.Get(header));
            }

            //foreach (Cookie cookie in request.Cookies)
            //{
            //    proxyRequest.AddCookie(cookie.Name, cookie.Value);
            //}

            if (string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) == false && request.ContentLength64 > 0)
            {
                using MemoryStream ms = new MemoryStream();
                await request.InputStream.CopyToAsync(ms);
                proxyRequest.AddParameter(string.Empty, ms.ToArray(), request.ContentType, ParameterType.RequestBody);
            }

            IRestResponse proxyResponse = await proxyClient.ExecuteAsync(proxyRequest, stoppingToken);
            foreach (var header in proxyResponse.Headers)
            {
                if (string.Equals(header.Name, "Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                response.AddHeader(header.Name, Convert.ToString(header.Value));
            }

            //foreach (var cookie in proxyResponse.Cookies)
            //{
            //    response.Cookies.Add(new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain) 
            //    {
            //        Secure = cookie.Secure,
            //        Port = cookie.Port,
            //        HttpOnly = cookie.HttpOnly,
            //        Expires = cookie.Expires,
            //        Discard = cookie.Discard,
            //        CommentUri = cookie.CommentUri,
            //        Comment = cookie.Comment,
            //        Expired = cookie.Expired,
            //        Version = cookie.Version
            //    });
            //}

            response.StatusCode = (int)proxyResponse.StatusCode;
            response.ProtocolVersion = proxyResponse.ProtocolVersion;
            response.ContentType = proxyResponse.ContentType;
            //response.ContentLength64 = proxyResponse.ContentLength;
            if (proxyResponse.RawBytes?.Length > 0)
            {
                //using MemoryStream ms = new MemoryStream(proxyResponse.RawBytes);
                //ms.Seek(0, SeekOrigin.Begin);
                //await ms.CopyToAsync(response.OutputStream);
                ReadOnlyMemory<byte> rawBytes = new ReadOnlyMemory<byte>(proxyResponse.RawBytes);
                await response.OutputStream.WriteAsync(rawBytes, stoppingToken);
            }

            response.Close();
        }

        private async Task ProcessWebSocketAsync(HttpListenerContext ctx, CancellationToken stoppingToken)
        {
            HttpListenerWebSocketContext webSocketContext;
            try
            {
                webSocketContext = await ctx.AcceptWebSocketAsync(default);
                //Console.WriteLine($"连接地址: {webSocketContext.WebSocket}");
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
                _logger.LogError(ex, ex.Message);
                return;
            }

            using WebSocket webSocket = webSocketContext.WebSocket;
            try
            {
                byte[] reciveBuffer = new byte[1024];
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult webSocketReceiveResult = await webSocket.ReceiveAsync(reciveBuffer, stoppingToken);

                    if(webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, stoppingToken);
                    }
                    else
                    {
                        await webSocket.SendAsync(new ArraySegment<byte>(reciveBuffer, 0, webSocketReceiveResult.Count), WebSocketMessageType.Binary, webSocketReceiveResult.EndOfMessage, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return;
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _listener.Stop();
            _listener.Close();

            return base.StopAsync(cancellationToken);
        }
    }
}

// https://stackoverflow.com/questions/13709946/simple-task-returning-asynchronous-htpplistener-with-async-await-and-handling-hi
// https://blog.csdn.net/winy_lm/article/details/84881038

// upstream sent invalid chunked response while reading upstream解决
// https://blog.csdn.net/sc9018181134/article/details/82055225

// https://blog.csdn.net/starfd/article/details/86508581
// https://www.c-sharpcorner.com/UploadFile/bhushanbhure/websocket-server-using-httplistener-and-client-with-client/
