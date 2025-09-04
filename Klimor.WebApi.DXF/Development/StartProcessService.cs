using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klimor.WebApi.DXF.Development
{
    public class StartProcessService
    {
        public void TerminateExistingPreviousProcess(string processName)
        {
            try
            {
                var current = Process.GetCurrentProcess();

                foreach (var process in Process.GetProcessesByName(processName))
                {
                    if (process.Id != current.Id)
                    {
                        try
                        {
                            process.Kill();
                            Console.WriteLine("Zamknięto poprzednią instancję.");
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error terminating process {processName}: {ex.Message}");
            }
        }
    }
}
