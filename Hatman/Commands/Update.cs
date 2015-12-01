﻿using System.Diagnostics;
using System.Text.RegularExpressions;
using ChatExchangeDotNet;

namespace Hatman.Commands
{
    class Update : ICommand
    {
        private readonly Regex ptn = new Regex(@"(?i)^(update|pull|fetch)", Extensions.RegOpts);
        private readonly AutoUpdater au;

        public Regex CommandPattern => ptn;

        public string Description => "Updates the bot with steaming fresh code.";

        public string Usage => "Update|pull|fetch";



        public Update() { }

        public Update(string apiToken)
        {
            if (string.IsNullOrWhiteSpace(apiToken))
            {
                return;
            }

            au = new AutoUpdater(apiToken);
        }



        public void ProcessMessage(Message msg, ref Room rm)
        {
            if (au == null) return;

            rm.PostReplyFast(msg, "Checking for updates...");

            var oldVer = "";
            var newVer = "";
            var updMsg = "";

            if (!au.Update(out oldVer, out newVer, out updMsg))
            {
                rm.PostReplyFast(msg, "No updates available.");
            }
            else
            {
                rm.PostReplyFast(msg, $"New update found and applied: {newVer} (current: {oldVer}). \"{updMsg}\".");

                var cp = Process.GetCurrentProcess();

                au.StartNewVersion();
                cp.CloseMainWindow();
            }
        }
    }
}
