using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System;

public class MyContext
{
    DiscordMessage _Message;
    DiscordChannel _Channel;
    DiscordClient _Client;
    DiscordUser _User;
    DiscordMember _Member;
    DiscordGuild _Guild;
    IServiceProvider _Services;
    string _Prefix;
    string _CommandName;

    public DiscordMessage Message { get { return _Message; } }
    public DiscordChannel Channel { get { return _Channel; } }
    public DiscordClient Client { get { return _Client; } }
    public DiscordUser User { get { return _User; } }
    public DiscordMember Member { get { return _Member; } }
    public DiscordGuild Guild { get { return _Guild; } }
    public IServiceProvider Services { get { return _Services; } }
    public string Prefix { get { return _Prefix; } }
    public string CommandName { get { return _CommandName; } }

    public MyContext(CommandContext ctx)
    {
        _Channel = ctx.Channel;
        _Client = ctx.Client;
        _User = ctx.User;
        _Member = ctx.Member;
        _Guild = ctx.Guild;
        _Services = ctx.Services;
        _Prefix = ctx.Prefix;
        _CommandName = ctx.Command.Name;
        _Message = ctx.Message;
    }
    public MyContext(InteractionContext ctx)
    {
        _Channel = ctx.Channel;
        _Client = ctx.Client;
        _User = ctx.User;
        _Member = ctx.Member;
        _Guild = ctx.Guild;
        _Services = ctx.Services;
        _Prefix = "/";
        _CommandName = ctx.CommandName;
        _Message = null;
    }
}