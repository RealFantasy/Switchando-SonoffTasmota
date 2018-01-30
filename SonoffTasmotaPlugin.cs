using HomeAutomation.Application.ConfigRetriver;
using HomeAutomation.Network;
using HomeAutomation.ObjectInterfaces;
using HomeAutomation.Objects.External.Plugins;
using Switchando.Events;
using System;
using System.IO;
using System.Reflection;

namespace Switchando.Plugin.Sonoff
{
    public class SonoffTasmotaPlugin : IPlugin
    {
        public string OnEnable()
        {
            Console.WriteLine("[SonoffTasmota] Initializing SonoffTasmota plugin...");

            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/plugins/SonoffTasmota/");
            File.WriteAllText(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/plugins/SonoffTasmota/config.html", SonoffTasmota.Properties.Resources.config);

            new SetupTool("SONOFF_TASMOTA_SWITCH", Switchando.Plugin.Sonoff.Objects.SonoffTasmota.Setup);
            NetworkInterface sonoff = new NetworkInterface("SONOFF_TASMOTA_SWITCH", Switchando.Plugin.Sonoff.Objects.SonoffTasmota.SendParameters);
            new ObjectInterface(sonoff, "Switch", typeof(uint), "ON / OFF state");
            var mi = new MethodInterface(sonoff, "switch", "Switch (on / off)");
            mi.AddParameter(new MethodParameter("objname", typeof(string), "Device name"));
            mi.AddParameter(new MethodParameter("switch", typeof(string), "Switch on (true / false)"));
            new Event("switchon", "Switch ON", sonoff);
            new Event("switchoff", "Switch OFF", sonoff);

            new SetupTool("SONOFF_TASMOTA_BUTTON", Switchando.Plugin.Sonoff.Objects.TasmotaSwitch.Setup);
            NetworkInterface tasmotaSwitch = new NetworkInterface("SONOFF_TASMOTA_BUTTON", Switchando.Plugin.Sonoff.Objects.TasmotaSwitch.SendParameters);

            return "";
        }
        public string GetName()
        {
            return "SonoffTasmota";
        }
        public string GetDeveloper()
        {
            return "Marco Realacci";
        }
        public string GetDescription()
        {
            return "iTead Sonoff with Tasmota firmware support for Switchando Automation";
        }
    }
}