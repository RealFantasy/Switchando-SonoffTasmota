using HomeAutomation.Network;
using HomeAutomation.Network.APIStatus;
using HomeAutomation.Objects;
using HomeAutomation.Objects.Switches;
using HomeAutomation.Rooms;
using HomeAutomation.Users;
using HomeAutomationCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace Switchando.Plugin.Sonoff.Objects
{
    public class TasmotaSwitch : IObject
    {
        public string TasmotaTopic { get; set; }
        public string Name { get; set; }
        public List<string> ActionsOn;
        public List<string> ActionsOff;
        public bool Switch { get; set; }
        public byte Channel = 1;
        public string ObjectType = "SONOFF_TASMOTA_BUTTON";
        public string ClientName = "local";

        public TasmotaSwitch(string name, string topic)
        {
            this.TasmotaTopic = topic;
            this.Name = name;
            this.ActionsOn = new List<string>();
            this.ActionsOff = new List<string>();
            HomeAutomationServer.server.Objects.Add(this);

            HomeAutomationServer.server.MQTTClient.Subscribe("cmnd/" + topic + "/POWER" + Channel, OnMQTTMessage);
        }
        public void Toggle(bool status)
        {
            Switch = status;
            if (status)
            {
                foreach(string action in ActionsOn)
                {
                    HomeAutomation.ObjectInterfaces.Action.FromName(action).Run(Identity.GetAdminUser());
                }
            }
            else
            {
                foreach (string action in ActionsOff)
                {
                    HomeAutomation.ObjectInterfaces.Action.FromName(action).Run(Identity.GetAdminUser());
                }
            }
        }
        public void Toggle()
        {
            if (Switch) Switch = false; else Switch = true;
            if (Switch)
            {
                foreach (string action in ActionsOn)
                {
                    HomeAutomation.ObjectInterfaces.Action.FromName(action).Run(Identity.GetAdminUser());
                }
            }
            else
            {
                foreach (string action in ActionsOff)
                {
                    HomeAutomation.ObjectInterfaces.Action.FromName(action).Run(Identity.GetAdminUser());
                }
            }
        }
        public void AddActionOn(HomeAutomation.ObjectInterfaces.Action action)
        {
            ActionsOn.Add(action.Name);
        }
        public void RemoveActionOn(HomeAutomation.ObjectInterfaces.Action action)
        {
            ActionsOn.Remove(action.Name);
        }
        public void AddActionOff(HomeAutomation.ObjectInterfaces.Action action)
        {
            ActionsOff.Add(action.Name);
        }
        public void RemoveActionOff(HomeAutomation.ObjectInterfaces.Action action)
        {
            ActionsOff.Remove(action.Name);
        }
        public static void OnMQTTMessage(MqttClient sender, MqttMsgPublishEventArgs e)
        {
            //if (e.Topic.StartsWith("cmnd/") && e.Topic.EndsWith("/POWER"))
            //{
            string id = e.Topic.Substring("cmnd/".Length);
            id = id.Substring(0, id.Length - "/POWERx".Length);
            TasmotaSwitch sonoff = FindSwitchFromTopic(id);
            if (sonoff == null) return;
            string message = Encoding.UTF8.GetString(e.Message);

            if (message.Equals("ON")) sonoff.Toggle(true);
            if (message.Equals("OFF")) sonoff.Toggle(false);
            if (message.Equals("TOGGLE")) sonoff.Toggle();
            //}
        }
        public bool IsOn()
        {
            return Switch;
        }
        public string GetName()
        {
            return Name;
        }
        public string[] GetFriendlyNames()
        {
            return new string[0];
        }
        public string GetId()
        {
            return Name;
        }
        public string GetObjectType()
        {
            return "SONOFF_TASMOTA_BUTTON";
        }
        public NetworkInterface GetInterface()
        {
            return NetworkInterface.FromId(ObjectType);
        }
        private static TasmotaSwitch FindSwitchFromTopic(string topic)
        {
            foreach (IObject obj in HomeAutomationServer.server.Objects)
            {
                if (obj is TasmotaSwitch)
                {
                    TasmotaSwitch sonoff = (TasmotaSwitch)obj;
                    if (sonoff.TasmotaTopic.Equals(topic)) return sonoff;
                }
            }
            return null;
        }
        private static TasmotaSwitch FindSonoffFromTopic(string topic)
        {
            foreach (IObject obj in HomeAutomationServer.server.Objects)
            {
                if (obj is TasmotaSwitch)
                {
                    TasmotaSwitch sonoff = (TasmotaSwitch)obj;
                    if (sonoff.TasmotaTopic.Equals(topic)) return sonoff;
                }
            }
            return null;
        }
        public static string SendParameters(string method, string[] request, Identity login)
        {
            if (method.Equals("createSonoff"))
            {
                if (!login.IsAdmin()) return new ReturnStatus(CommonStatus.ERROR_FORBIDDEN_REQUEST, "Insufficient permissions").Json();
                string name = null;
                string topic = null;
                byte channel = 1;
                Room room = null;

                foreach (string cmd in request)
                {
                    string[] command = cmd.Split('=');
                    switch (command[0])
                    {
                        case "objname":
                            name = command[1];
                            break;
                        case "topic":
                            topic = command[1];
                            break;
                        case "channel":
                            channel = byte.Parse(command[1]);
                            break;
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
                if (ObjectFactory.FromString(name) != null) return new ReturnStatus(CommonStatus.ERROR_BAD_REQUEST, name + " already exists").Json();
                TasmotaSwitch relay = new TasmotaSwitch(name, topic);
                relay.Channel = channel;
                room.AddItem(relay);
                ReturnStatus data = new ReturnStatus(CommonStatus.SUCCESS);
                data.Object.device = relay;
                return data.Json();
            }
            if (method.Equals("addAction/on"))
            {
                if (!login.IsAdmin()) return new ReturnStatus(CommonStatus.ERROR_FORBIDDEN_REQUEST, "Insufficient permissions").Json();
                string name = null;
                string obj = null;

                foreach (string cmd in request)
                {
                    string[] command = cmd.Split('=');
                    if (command[0].Equals("interface")) continue;
                    switch (command[0])
                    {
                        case "objname":
                            name = command[1];
                            break;

                        case "action":
                            obj = command[1];
                            break;
                    }
                }
                if (name == null || obj == null) return new ReturnStatus(CommonStatus.ERROR_BAD_REQUEST).Json();

                TasmotaSwitch button = null;
                HomeAutomation.ObjectInterfaces.Action action = null;

                foreach (HomeAutomation.ObjectInterfaces.Action iobj in HomeAutomationServer.server.Actions)
                {
                    if (iobj.Name.Equals(obj))
                    {
                        action = iobj;
                    }
                }
                foreach (IObject iobj in HomeAutomationServer.server.Objects)
                {
                    if (iobj.GetName().Equals(name))
                    {
                        button = (TasmotaSwitch)iobj;
                    }
                }
                if (button == null) return new ReturnStatus(CommonStatus.ERROR_NOT_FOUND, name + " not found").Json();
                if (button == null) return new ReturnStatus(CommonStatus.ERROR_NOT_FOUND, obj + " not found").Json();

                button.AddActionOn(action);
                ReturnStatus data = new ReturnStatus(CommonStatus.SUCCESS);
                data.Object.button = button;
                return data.Json();
            }
            if (method.Equals("addAction/off"))
            {
                if (!login.IsAdmin()) return new ReturnStatus(CommonStatus.ERROR_FORBIDDEN_REQUEST, "Insufficient permissions").Json();
                string name = null;
                string obj = null;

                foreach (string cmd in request)
                {
                    string[] command = cmd.Split('=');
                    if (command[0].Equals("interface")) continue;
                    switch (command[0])
                    {
                        case "objname":
                            name = command[1];
                            break;

                        case "action":
                            obj = command[1];
                            break;
                    }
                }
                if (name == null || obj == null) return new ReturnStatus(CommonStatus.ERROR_BAD_REQUEST).Json();

                TasmotaSwitch button = null;
                HomeAutomation.ObjectInterfaces.Action action = null;

                foreach (HomeAutomation.ObjectInterfaces.Action iobj in HomeAutomationServer.server.Actions)
                {
                    if (iobj.Name.Equals(obj))
                    {
                        action = iobj;
                    }
                }
                foreach (IObject iobj in HomeAutomationServer.server.Objects)
                {
                    if (iobj.GetObjectType() == "SONOFF_TASMOTA_BUTTON")
                    {
                        if (iobj.GetName().Equals(name))
                        {
                            button = (TasmotaSwitch)iobj;
                        }
                    }
                }
                if (button == null) return new ReturnStatus(CommonStatus.ERROR_NOT_FOUND, name + " not found").Json();
                if (button == null) return new ReturnStatus(CommonStatus.ERROR_NOT_FOUND, obj + " not found").Json();

                button.AddActionOff(action);
                ReturnStatus data = new ReturnStatus(CommonStatus.SUCCESS);
                data.Object.button = button;
                return data.Json();
            }
            if (method.Equals("removeAction/on"))
            {
                if (!login.IsAdmin()) return new ReturnStatus(CommonStatus.ERROR_FORBIDDEN_REQUEST, "Insufficient permissions").Json();
                string name = null;
                string obj = null;

                foreach (string cmd in request)
                {
                    string[] command = cmd.Split('=');
                    if (command[0].Equals("interface")) continue;
                    switch (command[0])
                    {
                        case "objname":
                            name = command[1];
                            break;

                        case "action":
                            obj = command[1];
                            break;
                    }
                }
                if (name == null || obj == null) return new ReturnStatus(CommonStatus.ERROR_BAD_REQUEST).Json();

                TasmotaSwitch button = null;
                HomeAutomation.ObjectInterfaces.Action action = null;

                foreach (HomeAutomation.ObjectInterfaces.Action iobj in HomeAutomationServer.server.Actions)
                {
                    if (iobj.Name.Equals(obj))
                    {
                        action = iobj;
                    }
                }
                foreach (IObject iobj in HomeAutomationServer.server.Objects)
                {
                    if (iobj.GetObjectType() == "SONOFF_TASMOTA_BUTTON")
                    {
                        if (iobj.GetName().Equals(name))
                        {
                            button = (TasmotaSwitch)iobj;
                        }
                    }
                }
                if (button == null) return new ReturnStatus(CommonStatus.ERROR_NOT_FOUND, name + " not found").Json();
                if (button == null) return new ReturnStatus(CommonStatus.ERROR_NOT_FOUND, obj + " not found").Json();

                button.RemoveActionOn(action);
                ReturnStatus data = new ReturnStatus(CommonStatus.SUCCESS);
                data.Object.button = button;
                return data.Json();
            }
            if (method.Equals("removeAction/off"))
            {
                if (!login.IsAdmin()) return new ReturnStatus(CommonStatus.ERROR_FORBIDDEN_REQUEST, "Insufficient permissions").Json();
                string name = null;
                string obj = null;

                foreach (string cmd in request)
                {
                    string[] command = cmd.Split('=');
                    if (command[0].Equals("interface")) continue;
                    switch (command[0])
                    {
                        case "objname":
                            name = command[1];
                            break;

                        case "action":
                            obj = command[1];
                            break;
                    }
                }
                if (name == null || obj == null) return new ReturnStatus(CommonStatus.ERROR_BAD_REQUEST).Json();

                TasmotaSwitch button = null;
                HomeAutomation.ObjectInterfaces.Action action = null;

                foreach (HomeAutomation.ObjectInterfaces.Action iobj in HomeAutomationServer.server.Actions)
                {
                    if (iobj.Name.Equals(obj))
                    {
                        action = iobj;
                    }
                }
                foreach (IObject iobj in HomeAutomationServer.server.Objects)
                {
                    if (iobj.GetObjectType() == "SONOFF_TASMOTA_BUTTON")
                    {
                        if (iobj.GetName().Equals(name))
                        {
                            button = (TasmotaSwitch)iobj;
                        }
                    }
                }
                if (button == null) return new ReturnStatus(CommonStatus.ERROR_NOT_FOUND, name + " not found").Json();
                if (button == null) return new ReturnStatus(CommonStatus.ERROR_NOT_FOUND, obj + " not found").Json();

                button.RemoveActionOff(action);
                ReturnStatus data = new ReturnStatus(CommonStatus.SUCCESS);
                data.Object.button = button;
                return data.Json();
            }
            return new ReturnStatus(CommonStatus.ERROR_NOT_IMPLEMENTED).Json();
        }
        public static void Setup(Room room, dynamic device)
        {
            TasmotaSwitch relay = new TasmotaSwitch(device.Name, device.TasmotaTopic);
            relay.Channel = (byte)device.Channel;
            foreach (string action in device.ActionsOn)
            {
                relay.ActionsOn.Add(action);
            }
            foreach (string action in device.ActionsOff)
            {
                relay.ActionsOff.Add(action);
            }
            room.AddItem(relay);
        }

    }
}
