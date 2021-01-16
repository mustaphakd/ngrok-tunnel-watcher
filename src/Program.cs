using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Dynamic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace runner
{
    //./runner.exe "http://worosoft.space/citizen/endpoint/"   "C:\tools\ngrok\ngrok.exe"
    class Program
    {
        private static readonly HttpClient client = new HttpClient();

        const string ENVNGROK = "NGROK_HOME";
        const string requiredNgrokFullpath = "ngrok tunel watcher expects to find full installation of ngrok in a user defined environment variable named: " + Program.ENVNGROK;

        private static bool sigIntRaised = false;

        static void Main(string[] args)
        {

            var rootCommand = new RootCommand
            {
                new Option<string>(
                    "--remote-url",
                    description: "Remote endpoint to send updated ngrok address"),
                new Option<FileInfo>(
                    "--path",
                    description: "Full path to ngrok executable",
                    getDefaultValue: () => new FileInfo(Environment.GetEnvironmentVariable(Program.ENVNGROK))),
                new Option<string>(
                    "--action",
                    description: "Parameters to pass on to ngrok e.g: http 8080 or start someConfigSectionFromNgrok.yaml ",
                    getDefaultValue: () => " start --all")
            };

            rootCommand.Description = $"ngrok tunnel watcher launches and periodically restarts ngrok executable.  It will update remote enpoint with your local ngrok address on every start.";




            // Note that the parameters of the handler method are matched according to the names of the options
            rootCommand.Handler = CommandHandler.Create<string, FileInfo, string>((remoteUrl, executable, executableArguments) =>
            {
                Console.WriteLine($"The value for --remote-url is: {remoteUrl}");
                Console.WriteLine($"The value for --path is: {executable?.FullName ?? "null"}");
                Console.WriteLine($"The value for --action is: {executableArguments}");

                var processRunner = Program.GetProcessRunner();
                WathcTunnel(processRunner, remoteUrl, executable.FullName, executableArguments);
            });


            //intercept kill signal and cleanup
            Console.CancelKeyPress += (_, ea) =>
            {
                // do not kill process
                ea.Cancel = true;

                Console.WriteLine(" SIGINT .  Did you press Ctrl+C ?? " + ea.SpecialKey);
                Program.sigIntRaised = true;
            };

            AppDomain.CurrentDomain.ProcessExit += (source, e) =>
            {
                Console.WriteLine(" SIGTERM .  Did you use TaskManager ??");
                Program.sigIntRaised = true;
            };

            // Parse the incoming args and invoke the handler
            rootCommand.InvokeAsync(args).Wait();
        }


        static void WathcTunnel(System.Threading.Thread thread, string remoteEndpoint, string ngrokFullPath, string ngrokCommand)
        {
            Console.WriteLine($"You provided serverulr: {remoteEndpoint} \n\tngrok: {ngrokCommand}");

            while (Program.sigIntRaised == false)
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

                    if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        publicUrl = url;
                        break;
                    }
                }
                //dynamic tunnel = ().First();


                client.DefaultRequestHeaders.Add("User-Agent", "PostmanRuntime/8.0.0");
                json = client.GetStringAsync($"{remoteEndpoint}?link={publicUrl}").GetAwaiter().GetResult();

                var full_time = 1000 * 3600 * 7;
                System.Threading.Thread.Sleep(full_time);
                RunCommand("taskkill", "/f /im ngrok.exe");
                System.Threading.Thread.Sleep(10_000);
            }

        }

        static System.Threading.Thread GetProcessRunner()
        {
            System.Threading.Thread thread = new System.Threading.Thread((dynamic arg) =>
            {
                var param = arg.command;
                var cmd = arg.arguments;

                RunCommand(param, cmd);
            });

            return thread;
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
