﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChatExchangeDotNet;
using Hatman.Triggers;
using Hatman.Commands;
using System.Reflection;
using System.Threading;
using System.Text.RegularExpressions;

namespace Hatman
{
    public class ChatEventRouter
    {
        private Room monitoredRoom;

        public ManualResetEvent ShutdownMre = new ManualResetEvent(false);

        public ChatEventRouter(Room chatRoom, string token)
        {
            monitoredRoom = chatRoom;

            PopulateCommands(token);
            PopulateTriggers();
            
            chatRoom.EventManager.ConnectListener(EventType.UserMentioned, new Action<Message>(m =>
            {
                EventCallback(EventType.UserMentioned, m, null, monitoredRoom, null);
            }));
            chatRoom.EventManager.ConnectListener(EventType.MessagePosted, new Action<Message>(m =>
            {
                EventCallback(EventType.MessagePosted, m, null, monitoredRoom, null);
            }));
            chatRoom.EventManager.ConnectListener(EventType.UserEntered, new Action<User>(u =>
            {
                EventCallback(EventType.UserEntered, null, u, monitoredRoom, null);
            }));
            chatRoom.EventManager.ConnectListener(EventType.UserLeft, new Action<User>(u =>
            {
                EventCallback(EventType.UserLeft, null, u, monitoredRoom, null);
            }));
        }


        #region Event Router

        private void EventCallback(EventType evt, Message m, User u, Room r, string raw)
        {
            if (evt == EventType.UserMentioned)
            {
                if (Regex.IsMatch(m.Content, @"(?i)^(die|stop|shutdown)$"))
                {
                    ShutdownMre.Set();
                    return;
                }
            }

            Console.WriteLine("{0} - {1}{2}", evt, m != null ? m.Content : "", u != null ? u.GetChatFriendlyUsername() : "");

            ChatEventArgs args = new ChatEventArgs(evt, m, u, r, raw);
            bool handled = HandleTriggerEvent(args);
            if (!handled && m != null) HandleCommandEvent(args);
        }


        #endregion


        #region Triggers

        private Dictionary<EventType, ITrigger> ActiveTriggers = new Dictionary<EventType, ITrigger>();
        private Dictionary<EventType, List<ITrigger>> Triggers = new Dictionary<EventType, List<ITrigger>>();

        private void PopulateTriggers()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            var trgs = types.Where(t => t.Namespace == "Hatman.Triggers");

            foreach (var type in trgs)
            {
                if (type.IsInterface) { continue; }

                var instance = (ITrigger)Activator.CreateInstance(type);

                instance.AttachEvents(this);
            }
        }

        public bool RegisterTriggerEvent(EventType type, ITrigger trigger)
        {
            if (!Triggers.ContainsKey(type))
            {
                Triggers.Add(type, new List<ITrigger>());
            }

            if (!ActiveTriggers.ContainsKey(type))
            {
                ActiveTriggers.Add(type, null);
            }

            Triggers[type].Add(trigger);
            return true;
        }
                
        private bool HandleTriggerEvent(ChatEventArgs e) 
        {

            bool handled = false;

            if (!Triggers.ContainsKey(e.Type)) return false;
                        
            // Last active gets first crack
            if (ActiveTriggers[e.Type] != null)
            {
                handled = ActiveTriggers[e.Type].HandleEvent(this, e);
                if (!handled)
                {
                    ActiveTriggers[e.Type] = null;
                }
            }
            else
            {
                foreach (ITrigger trigger in Triggers[e.Type])
                {
                    handled = trigger.HandleEvent(this, e);
                    if (handled)
                    {
                        ActiveTriggers[e.Type] = trigger;
                        break;
                    }
                }
            }
            return handled; 
        }
        
        #endregion

        #region Commands

        List<ICommand> commands = new List<ICommand>();

        private void PopulateCommands(string tkn)
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            var cmds = types.Where(t => t.Namespace == "Hatman.Commands");

            foreach (var type in cmds)
            {
                if (type.IsInterface || type.IsSealed) { continue; }

                ICommand instance;

                if (type.Name == "Update")
                {
                    instance = (ICommand)Activator.CreateInstance(type, tkn);
                }
                else
                {
                    instance = (ICommand)Activator.CreateInstance(type);
                }
                commands.Add(instance);
            }
        }

        private void HandleCommandEvent(ChatEventArgs e)
        {
            foreach (ICommand command in commands)
            {
                if (command.CommandPattern.IsMatch(e.Message.Content))
                {
                    Room r = e.Room;
                    command.ProcessMessage(e.Message, ref r);
                }
            }
        }

        #endregion

    }

    public class ChatEventArgs
    {
        public Message Message { get; private set; }
        public User User { get; private set; }
        public Room Room { get; private set; }
        public string RawData { get; private set; }
        public EventType Type { get; private set; }

        public ChatEventArgs(EventType t, Message m, User u, Room r, string rawData)
        {
            this.Type = t;
            this.Message = m;
            this.User = u;
            this.Room = r;
        }
    }
}