﻿using System.Text.RegularExpressions;
using ChatExchangeDotNet;

namespace Hatman.Commands
{
    public interface ICommand
    {
        Regex CommandPattern { get; }
        string Description { get; }
        string Usage { get; }

        void ProcessMessage(Message msg, ref Room rm);
    }
}
