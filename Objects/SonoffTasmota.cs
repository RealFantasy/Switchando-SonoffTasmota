using HomeAutomation.Network;
using HomeAutomation.Network.APIStatus;
using HomeAutomation.Objects;
using HomeAutomation.Objects.Switches;
using HomeAutomation.Rooms;
using HomeAutomation.Users;
using HomeAutomationCore;
using Newtonsoft.Json;
using Switchando.Events;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace Switchando.Plugin.Sonoff.Objects
{
    public class SonoffTasmota : ISwitch
    {
        public string TasmotaTopic { get; set; }
        public string Name { get; set; }
        public string[] FriendlyNames { get; set; }
        public bool Switch { get; set; }
        public string Description { get; set; }
        public string RealDescription { get; set; }
        public bool Connected = true;
        private bool TaskRunning = false;
        public byte Channel = 1;
        public string ObjectType = "SONOFF_TASMOTA_SWITCH";
        public string ObjectModel = "SWITCH";
        public string ClientName = "local";

        private Event OnSwitchOn;
        private Event OnSwitchOff;

        public SonoffTasmota(string name, string topic, string description, string[] friendlyNames)
        {
            this.FriendlyNames = friendlyNames;
            this.Description = description;
            this.TasmotaTopic = topic;
            this.Name = name;
            HomeAutomationServer.server.Objects.Add(this);
            this.OnSwitchOn = HomeAutomationServer.server.Events.GetEvent(this, "switchon");
            this.OnSwitchOff = HomeAutomationServer.server.Events.GetEvent(this, "switchoff");
            HomeAutomationServer.server.MQTTClient.Subscribe("tele/" + topic + "/STATE", OnMQTTMessage);
            HomeAutomationServer.server.MQTTClient.Subscribe("stat/" + topic + "/POWER", OnMQTTMessage);
        }
        public void Start()
        {
            Console.WriteLine("Switch `" + this.Name + "` has been turned on.");
            Switch = true;
            Connected = false;
            HomeAutomationServer.server.MQTTClient.Publish("cmnd/" + TasmotaTopic + "/power" + Channel, "on");
            OnSwitchOn.Throw(this);
            TestConnection();
        }
        public void Stop()
        {
            Console.WriteLine("Switch `" + this.Name + "` has been turned off.");
            Switch = false;
            Connected = false;
            HomeAutomationServer.server.MQTTClient.Publish("cmnd/" + TasmotaTopic + "/power" + Channel, "off");
            OnSwitchOff.Throw(this);
            TestConnection();

        }
        private async void TestConnection()
        {
            if (TaskRunning) return;
            TaskRunning = true;
            await Task.Delay(5000);
            while (Connected == false)
            {
                Description = "Offline";
                await Task.Delay(5000);
            }
            Description = RealDescription;
        }
        public static void OnMQTTMessage(MqttClient sender, MqttMsgPublishEventArgs e)
        {
            if (e.Topic.StartsWith("tele/") && e.Topic.EndsWith("/STATE"))
            {
                string id = e.Topic.Substring("tele/".Length);
                id = id.Substring(0, id.Length - "/STATE".Length);
                SonoffTasmota sonoff = FindSonoffFromTopic(id);
                if (sonoff == null) return;
                string message = Encoding.UTF8.GetString(e.Message);
                dynamic update = JsonConvert.DeserializeObject<ExpandoObject>(message);
                string state = update.POWER;
                if (sonoff.Connected == true)
                {
                    if (state.Equals("ON")) sonoff.Switch = true; else sonoff.Switch = false;
                }
                else
                {
                    if (sonoff.Switch)
                    {
                        HomeAutomationServer.server.MQTTClient.Publish("cmnd/" + sonoff.TasmotaTopic + "/power" + sonoff.Channel, "on");
                    }
                    else
                    {
                        HomeAutomationServer.server.MQTTClient.Publish("cmnd/" + sonoff.TasmotaTopic + "/power" + sonoff.Channel, "off");
                    }
                    sonoff.Connected = true;
                }
            }
            if (e.Topic.StartsWith("stat/") && e.Topic.EndsWith("/POWER"))
            {
                string id = e.Topic.Substring("stat/".Length);
                id = id.Substring(0, id.Length - "/POWER".Length);
                SonoffTasmota sonoff = FindSonoffFromTopic(id);
                if (sonoff == null) return;
                string message = Encoding.UTF8.GetString(e.Message);
                if (sonoff.Connected == true)
                {
                    if (message.Equals("ON")) sonoff.Switch = true; else sonoff.Switch = false;
                }
                else
                {
                    if (sonoff.Switch)
                    {
                        HomeAutomationServer.server.MQTTClient.Publish("cmnd/" + sonoff.TasmotaTopic + "/power" + sonoff.Channel, "on");
                    }
                    else
                    {
                        HomeAutomationServer.server.MQTTClient.Publish("cmnd/" + sonoff.TasmotaTopic + "/power" + sonoff.Channel, "off");
                    }
                    sonoff.Connected = true;
                }
            }
        }
        public bool IsOn()
        {
            return Switch;
        }
        public string GetName()
        {
            return Name;
        }
        public string GetId()
        {
            return Name;
        }
        public string GetObjectType()
        {
            return "GENERIC_SWITCH";
        }
        public string[] GetFriendlyNames()
        {
            return FriendlyNames;
        }
        public NetworkInterface GetInterface()
        {
            return NetworkInterface.FromId(ObjectType);
        }
        private static SonoffTasmota FindSonoffFromName(string name)
        {
            SonoffTasmota relay = null;
            foreach (IObject obj in HomeAutomationServer.server.Objects)
            {
                if (obj.GetName().ToLower().Equals(name.ToLower()))
                {
                    relay = (SonoffTasmota)obj;
                    break;
                }
                if (obj.GetFriendlyNames() == null) continue;
                if (Array.IndexOf(obj.GetFriendlyNames(), name.ToLower()) > -1)
                {
                    relay = (SonoffTasmota)obj;
                    break;
                }
            }
            return relay;
        }
        private static SonoffTasmota FindSonoffFromTopic(string topic)
        {
            foreach (IObject obj in HomeAutomationServer.server.Objects)
            {
                if (obj is SonoffTasmota)
                {
                    SonoffTasmota sonoff = (SonoffTasmota)obj;
                    if (sonoff.TasmotaTopic.Equals(topic)) return sonoff;
                }
            }
            return null;
        }
        public static string SendParameters(string method, string[] request, Identity login)
        {
            if (method.Equals("switch"))
            {
                SonoffTasmota relay = null;
                bool status = false;

                foreach (string cmd in request)
                {
                    string[] command = cmd.Split('=');
                    switch (command[0])
                    {
                        case "objname":
                            relay = FindSonoffFromName(command[1]);
                            break;
                        case "switch":
                            status = bool.Parse(command[1]);
                            break;
                    }
                }
                if (relay == null) return new ReturnStatus(CommonStatus.ERROR_NOT_FOUND).Json();
                if (!login.HasAccess(relay)) return new ReturnStatus(CommonStatus.ERROR_FORBIDDEN_REQUEST, "Insufficient permissions").Json();
                if (status) relay.Start(); else relay.Stop();
                return new ReturnStatus(CommonStatus.SUCCESS).Json();
            }
            if (method.Equals("createSonoff"))
            {
                if (!login.IsAdmin()) return new ReturnStatus(CommonStatus.ERROR_FORBIDDEN_REQUEST, "Insufficient permissions").Json();
                string name = null;
                string[] friendlyNames = null;
                string description = null;
                string topic = null;
                byte channel = 1;
                Room room = null;

                foreach (string cmd in request)
                {
                    string[] command = cmd.Split('=');
                    if (command[0].Equals("interface")) continue;
                    switch (command[0])
                    {
                        case "objname":
                            name = command[1];
                            break;
                        case "description":
                            description = command[1];
                            break;
                        case "setfriendlynames":
                            string names = command[1];
                            friendlyNames = names.Split(',');
                            break;
                        case "topic":
                            topic = command[1];
                            break;
                        case "channel":
                            channel = byte.Parse(command[1]);
                            break;
                        /*case "client":
                            string clientName = command[1];
                            foreach (Client clnt in HomeAutomationServer.server.Clients)
                            {
                                if (clnt.Name.Equals(clientName))
                                {
                                    client = clnt;
                                }
                            }
                            if (client == null) return new ReturnStatus(CommonStatus.ERROR_NOT_FOUND, "Raspi-Client not found").Json();
                            break;*/
                        case "room":
                            foreach (Room stanza in HomeAutomationServer.server.Rooms)
                            {
                                if (stanza.Name.ToLower().Equals(command[1].ToLower()))
                                {
                                    room = stanza;
                                }
                            }
                            break;
                    }
                }
                if (room == null) return new ReturnStatus(CommonStatus.ERROR_NOT_FOUND, "Room not found").Json();
                SonoffTasmota relay = new SonoffTasmota(name, topic, description, friendlyNames);
                relay.RealDescription = description;
                relay.Channel = channel;
                room.AddItem(relay);
                ReturnStatus data = new ReturnStatus(CommonStatus.SUCCESS);
                data.Object.device = relay;
                return data.Json();
            }
            return new ReturnStatus(CommonStatus.ERROR_NOT_IMPLEMENTED).Json();
        }
        public static void Setup(Room room, dynamic device)
        {
            string[] friendlyNames = Array.ConvertAll(((List<object>)device.FriendlyNames).ToArray(), x => x.ToString());
            SonoffTasmota relay = new SonoffTasmota(device.Name, device.TasmotaTopic, device.Description, friendlyNames);
            relay.RealDescription = device.RealDescription;
            relay.Channel = (byte)device.Channel;
            if (device.Switch) relay.Start(); else relay.Stop();
            room.AddItem(relay);
        }
    }
}