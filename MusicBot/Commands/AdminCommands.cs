using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class AdminCommands : BaseCommandModule
    {
        [Command("/commands")]
        [RequireOwner]
        public async Task Commands2(CommandContext ctx)
        {
            IReadOnlyDictionary<string, Command> commands = ctx.Client.GetCommandsNext().RegisteredCommands;
            string commandTest = string.Empty;
            foreach (var command in commands)
            {
                if (command.Value.ExecutionChecks.Count > 0 && command.Value.ExecutionChecks.First().Equals(ctx.Command.ExecutionChecks.First()))//{ command.Value.Module.ModuleType.Name}
                {
                    commandTest += $"{command.Key}\n";
                }
            }
            await ctx.Channel.SendMessageAsync(DataMethods.SimpleEmbed(description: commandTest));
        }

        [Command("/getguilds")]
        [RequireOwner]
        public async Task ServerList(CommandContext ctx)
        {
            string guilds = string.Empty;
            int i = 0;
            foreach (var guild in ctx.Client.Guilds)
            {
                guilds += $"{guild.Value.Name} [{guild.Value.MemberCount}] `{guild.Value.Id}`\n";
                i++;
                if (i / 10 > 0)
                {
                    i = 0;
                    await ctx.Channel.SendMessageAsync(guilds);
                    guilds = string.Empty;
                }
            }
            if (i > 0 && i < 10)
            {
                await ctx.Channel.SendMessageAsync(guilds);
            }
        }

        [Command("/leaveguild")]
        [RequireOwner]
        public async Task Leave(CommandContext ctx, ulong guildId)
        {
            var guild = await ctx.Client.GetGuildAsync(guildId);
            await guild.LeaveAsync();
            await ctx.Channel.SendMessageAsync($"Left `{guild.Name}`");
        }
    }

}