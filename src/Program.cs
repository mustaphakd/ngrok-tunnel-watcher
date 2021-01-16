using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Dynamic;
using System.Net.Http;
using System.Net.Http.Headers;

namespace runner
{
    //./runner.exe "http://worosoft.space/citizen/endpoint/"   "C:\tools\ngrok\ngrok.exe"
    // load from Env variables
    class Program
    {
        private static readonly HttpClient client = new HttpClient();

        static void Main(string[] args)
        {
            var argsLength = args.Length;
            string[] commands;

            if (argsLength < 2)
            {
                Console.WriteLine("runner {serverUrl} {ngrokFullPath} [port]");
                return;
            }

            var port = argsLength == 3 ? args[2] : "5000";
            var serverUrl = args[0];
            var ngrokCommand = args[1];

            Console.WriteLine($"You provided serverulr: {serverUrl} \n\tngrok: {ngrokCommand}");



            //intercept kill signal and cleanup
            System.Threading.Thread thread = new System.Threading.Thread((dynamic arg) =>
            {
                var param = arg.command;
                var cmd = arg.arguments;

                RunCommand(param, cmd);
            });

            while(true)
            {
               // thread.Start(new { command = $"cd {ngrokCommand}", arguments = $" && ngrok  http {port}" });
                thread.Start(new { command = $"cd {ngrokCommand}", arguments = $" && ngrok  start --all" });
                //thread.Start(new { command = $"cd {ngrokCommand}", arguments = $" && ngrok  start nginxhttp" });
                System.Threading.Thread.Sleep(16000);

                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                var stringTask = client.GetStringAsync("http://127.0.0.1:4040/api/tunnels");

                var json = stringTask.GetAwaiter().GetResult();
                dynamic jsonResult = JsonConvert.DeserializeObject<ExpandoObject>(json, new ExpandoObjectConverter());

                var tunnels = (IEnumerable<dynamic>)jsonResult.tunnels;


                var publicUrl = ""; // (string)tunnel.public_url;

                foreach (var tunnel in tunnels)
                {
                    Console.WriteLine(tunnel);
                    var url = tunnel.public_url as string;

                    if(url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        publicUrl = url;
                        break;
                    }
                }
                //dynamic tunnel = ().First();


                client.DefaultRequestHeaders.Add("User-Agent", "PostmanRuntime/8.0.0");
                json = client.GetStringAsync($"{serverUrl}?link={publicUrl}").GetAwaiter().GetResult();

                var full_time = 1000 * 3600 * 7;
                System.Threading.Thread.Sleep(full_time);
                RunCommand("taskkill", "/f /im ngrok.exe");
                System.Threading.Thread.Sleep(10_000);
            }
        }

        static void RunCommand(string path, string command, bool wait = false)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            //startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.RedirectStandardInput = true;
            //startInfo.RedirectStandardOutput = true;
            //startInfo.Arguments = $"/C {path} {command}";

            //startInfo.CreateNoWindow = true;
            //startInfo.UseShellExecute = false;

            process.StartInfo = startInfo;
            process.Start();

            process.StandardInput.WriteLine($" {path} {command}");

            if (wait)
            {
                process.WaitForExit();
            }
        }
    }
}
