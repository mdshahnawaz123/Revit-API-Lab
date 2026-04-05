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
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    return key?.GetValue("MachineGuid") as string;
                }
            }
            catch { }

            return Environment.MachineName;
        }
    }
}
