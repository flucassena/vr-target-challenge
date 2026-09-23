using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceProcess;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var errors = new List<string>();
        // Stop the runtime first so its launcher does not recreate killed processes.
        try
        {
            using (var service = new ServiceController("OVRService"))
            {
                service.Refresh();
                if (service.Status != ServiceControllerStatus.Stopped)
                {
                    if (service.Status != ServiceControllerStatus.StopPending)
                        service.Stop();
                    try { service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5)); }
                    catch (System.ServiceProcess.TimeoutException) { /* Kill the hung runtime below. */ }
                }
            }
        }
        catch (Exception ex) { errors.Add("Serviço da Meta: " + ex.Message); }

        string[] names = {
            "OculusClient", "OculusDash", "OVRServer_x64", "OVRRedir",
            "oculus-platform-runtime", "OVRServiceLauncher"
        };
        foreach (string name in names)
        {
            foreach (Process process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            if (!process.WaitForExit(3000)) errors.Add(name + ": não encerrou no prazo.");
                        }
                    }
                    catch (InvalidOperationException) { /* Already exited. */ }
                    catch (Exception ex) { errors.Add(name + ": " + ex.Message); }
                }
            }
        }
        try
        {
            using (var service = new ServiceController("OVRService"))
            {
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(3));
            }
        }
        catch (Exception ex) { errors.Add("Não foi possível confirmar que o serviço parou: " + ex.Message); }

        string message = errors.Count == 0
            ? "Meta/OVR encerrados.\n\nPara voltar a usar o Link, inicie o Oculus VR Runtime Service em services.msc e abra o aplicativo da Meta."
            : "Alguns componentes não puderam ser encerrados:\n\n" + string.Join("\n", errors.ToArray());
        MessageBox.Show(message, "Fechar Meta VR", MessageBoxButtons.OK,
            errors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }
}
