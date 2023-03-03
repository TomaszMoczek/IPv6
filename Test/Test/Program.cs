using System;
using System.IO;
using System.Diagnostics;

namespace Test
{
    class Program
    {
        private readonly Process process;
        private readonly StreamWriter streamWriter;

        public Program(string file, string host, int port)
        {
            process = new Process();

            process.StartInfo.FileName = file;
            process.StartInfo.Arguments = host + " " + port.ToString();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;

            process.OutputDataReceived += Process_OutputDataReceived;

            process.Start();

            streamWriter = process.StandardInput;

            process.BeginOutputReadLine();
        }

        public bool HasExited
        {
            get { return process.HasExited; }
        }

        public void Send(string plaintext)
        {
            streamWriter.WriteLine(plaintext);
        }

        public void WaitForExit()
        {
            process.WaitForExit();
        }

        public void Close()
        {
            try
            {
                streamWriter.Close();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }

            try
            {
                process.Close();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        static void Main(string[] args)
        {
            Program program = null;

            try
            {
                Console.WriteLine("{0} v{1}"
                    , System.Reflection.Assembly.GetEntryAssembly().GetName().Name
                    , System.Reflection.Assembly.GetEntryAssembly().GetName().Version);

                if (args.Length == 3)
                {
                    string file = args[0];
                    string host = args[1];
                    int port = int.Parse(args[2]);

                    program = new Program(file, host, port);

                    while (true)
                    {
                        string plaintext = Console.ReadLine();

                        if (program.HasExited)
                        {
                            break;
                        }

                        program.Send(plaintext);

                        if (String.IsNullOrEmpty(plaintext))
                        {
                            break;
                        }
                    }

                    if (!program.HasExited)
                    {
                        program.WaitForExit();
                    }
                }
                else
                {
                    throw new Exception("Usage: Test file host port");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
            finally
            {
                if (program != null)
                {
                    program.Close();
                }
            }
        }
    }
}

