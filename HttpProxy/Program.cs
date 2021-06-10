using System.Threading;
using System.Net.Http;
using System;
using System.Net;
using System.Threading.Tasks;

namespace HttpProxy
{
    class Program
    {
        static async Task Main(string[] args)
        {
            CancellationTokenSource cancellationTokenSource = new();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            Console.CancelKeyPress += (sender, e) =>
            {
                cancellationTokenSource.CancelAfter(300);
            };
            Console.WriteLine("Ctrl + c exit");

            //await HttpListenerAsync(cancellationToken, "http://localhost:5000/");
            //await HttpListenerAsync(cancellationToken, "http://127.0.0.1:5000/");
            //await HttpListenerAsync(cancellationToken, "http://192.168.199.101:5000/");
            await HttpListenerAsync(cancellationToken, "http://*:5000/");
            // netsh http add urlacl url=http://*:5123/  user=Everyone

            #region
            // HttpListener Start 拒绝访问 https://www.cnblogs.com/qinghub/p/5603659.html
            // C#Http 服务器报 HttpListener 拒绝访问异常解决方法 https://blog.csdn.net/swordsf/article/details/77413738
            // HttpListener,调用Start()方法会出现“拒绝访问”异常，如何解决？https://social.msdn.microsoft.com/Forums/ie/zh-CN/752ec611-40cc-4fe4-882e-8adc5a4bd3ac/httplistener3584329992start260412786120250209862961682202529832477
            #endregion
        }

        static async Task HttpListenerAsync(CancellationToken cancellationToken, params string[] prefixes)
        {
            if (prefixes == null || prefixes.Length == 0)
                throw new ArgumentException("Prefixes needed");

            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls13 | System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11;
            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            using HttpMessageHandler httpMessageHandler = new HttpClientHandler
            {
                // ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                AutomaticDecompression = DecompressionMethods.All,
                ServerCertificateCustomValidationCallback = (message, cert, chain, error) => true,
            };
            using HttpClient httpProxy = new(httpMessageHandler);
            httpProxy.BaseAddress = new Uri("https://www.baidu.com");
            // httpProxy.BaseAddress = new Uri("http://isoredirect.centos.org");
            // httpProxy.BaseAddress = new Uri("https://www.funbikes.co.uk");
            // httpProxy.BaseAddress = new Uri("https://cn.bing.com");

            using HttpListener httpListener = new();
            httpListener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            foreach (string s in prefixes)
            {
                httpListener.Prefixes.Add(s);
            }
            httpListener.Start();
            Console.WriteLine("Listening..");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    HttpListenerContext context = await httpListener.GetContextAsync();
                    await ProcessAsync(context, httpProxy);
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            httpListener.Stop();
        }

        static async Task ProcessAsync(HttpListenerContext context, HttpClient httpProxy)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            Console.WriteLine(request.RawUrl);

            // using HttpRequestMessage requestMessage = new(new HttpMethod(request.HttpMethod), request.RawUrl);
            using HttpRequestMessage requestMessage = new(new HttpMethod(request.HttpMethod), new Uri(httpProxy.BaseAddress, request.RawUrl));
            foreach (string header in request.Headers)
            {
                requestMessage.Headers.TryAddWithoutValidation(header, request.Headers[header]);
            }
            requestMessage.Headers.Remove("Host");
            requestMessage.Headers.TryAddWithoutValidation("Host", httpProxy.BaseAddress.Authority);
            requestMessage.Content = new StreamContent(request.InputStream);

            using HttpResponseMessage responseMessage = await httpProxy.SendAsync(requestMessage);
            foreach (var header in responseMessage.Headers)
            {
                response.Headers.Add(header.Key, string.Join(";", header.Value));
            }
            await responseMessage.Content.CopyToAsync(response.OutputStream);
            response.OutputStream.Flush();
            response.Close();
        }
    }
}
