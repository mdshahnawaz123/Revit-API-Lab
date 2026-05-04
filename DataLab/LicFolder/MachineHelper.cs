using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLab.LicFolder
{
    public static class MachineHelper
    {
        public static string GetMachineId()
        {
            string machineGuid = "";
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    machineGuid = key?.GetValue("MachineGuid") as string ?? "";
                }
            }
            catch { }

            string macAddress = "";
            try
            {
                var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    // Find the first active, non-loopback network interface
                    if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && 
                        nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    {
                        macAddress = nic.GetPhysicalAddress().ToString();
                        break;
                    }
                }
            }
            catch { }

            string combined = machineGuid + "-" + macAddress;
            
            // If both fail, fallback to machine name
            if (combined == "-")
                return Environment.MachineName;

            // Hash the combined string to make a clean, consistent ID
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
